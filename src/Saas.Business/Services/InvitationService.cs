using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Saas.Business.Contracts;
using Saas.Business.Mapping;
using Saas.Business.Services.Interfaces;
using Saas.Core.Email;
using Saas.Core.ErrorScenarios;
using Saas.Core.Exceptions;
using Saas.Core.Options;
using Saas.Core.Tenancy;
using Saas.Data;
using Saas.Data.Extensions;
using Saas.Data.Interceptors;
using Saas.Data.Models;

namespace Saas.Business.Services;

/// <inheritdoc />
public class InvitationService(
    ApplicationDbContext context,
    ITenantContext tenantContext,
    IOrganizationService organizationService,
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions,
    IOptions<FeatureOptions> featureOptions) : IInvitationService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly FeatureOptions _features = featureOptions.Value;

    /// <inheritdoc />
    public async Task<IReadOnlyList<InvitationSummary>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        await RequireHrAdminAsync(organizationId, cancellationToken);

        var invitations = await context.Invitations
            .AsNoTracking()
            .Where(i => i.OrganizationId == organizationId)
            .OrderByDescending(i => i.CreatedDate)
            .ToListAsync(cancellationToken);

        return invitations.Select(i => i.ToSummary()).ToList();
    }

    /// <inheritdoc />
    public async Task<(InvitationSummary Invitation, string Token)> CreateAsync(
        Guid organizationId,
        string email,
        OrganizationRole role,
        CancellationToken cancellationToken = default)
    {
        RequireOrganizationsEnabled();
        await RequireHrAdminAsync(organizationId, cancellationToken);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
        {
            throw new FunctionalException("A valid email address is required.", ErrorScenario.ValidationFailed);
        }

        var alreadyPending = await context.Invitations.AnyAsync(
            i => i.OrganizationId == organizationId
                 && i.Email == normalizedEmail
                 && i.AcceptedAt == null
                 && i.ExpiresAt > DateTimeOffset.UtcNow,
            cancellationToken);

        if (alreadyPending)
        {
            throw FunctionalException.Conflict(
                "An invitation for this address is already pending.",
                ErrorScenario.Conflict);
        }

        // 32 random bytes, url-safe. Stored as-is because the token is a
        // capability with a short life and single use, not a password.
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        var invitation = new InvitationEntity
        {
            OrganizationId = organizationId,
            Email = normalizedEmail,
            Role = role,
            Token = token,
            ExpiresAt = DateTimeOffset.UtcNow.Add(Lifetime),
            InvitedByUserId = RequireUserId(),
        };

        context.Invitations.Add(invitation);
        await context.SaveChangesAsync(cancellationToken);

        var organization = await context.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        var link = $"{appOptions.Value.WebUrl}/invitations/accept?token={token}";

        await emailSender.SendAsync(
            normalizedEmail,
            $"You have been invited to {organization?.Name ?? "a workspace"}",
            $"""
             You have been invited to join {organization?.Name ?? "a workspace"}.

             {link}

             The invitation expires in 7 days and can only be accepted by this
             email address.
             """,
            cancellationToken);

        // The token is also returned so the caller can show or re-send the link
        // itself -- useful when no mail provider is configured yet.
        return (invitation.ToSummary(), token);
    }

    /// <inheritdoc />
    public async Task RevokeAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        await RequireHrAdminAsync(organizationId, cancellationToken);

        var invitation = await context.Invitations
            .FirstOrDefaultAsync(
                i => i.Id == invitationId && i.OrganizationId == organizationId,
                cancellationToken)
            ?? throw FunctionalException.NotFound("Invitation");

        context.Invitations.Remove(invitation);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrganizationSummary> AcceptAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        var user = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw FunctionalException.NotFound("User");

        // The invitation belongs to an organization the caller is not yet in, so
        // the tenant query filter would hide it. The token itself is the
        // authorisation here, and it is matched against the caller's own email
        // below.
        var invitation = await context.Invitations
            .IgnoreQueryFilters()
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken)
            ?? throw FunctionalException.NotFound("Invitation");

        if (invitation.AcceptedAt is not null)
        {
            throw FunctionalException.Conflict(
                "This invitation has already been accepted.",
                ErrorScenario.InviteAlreadyAccepted);
        }

        if (invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw FunctionalException.Conflict("This invitation has expired.", ErrorScenario.InviteExpired);
        }

        // Binding to the address invited stops a leaked link being redeemed by
        // whoever happens to find it.
        if (!string.Equals(invitation.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw FunctionalException.Forbidden(
                "This invitation was issued to a different email address.",
                ErrorScenario.NotAMember);
        }

        var existingRole = await organizationService.GetRoleAsync(
            userId, invitation.OrganizationId, cancellationToken);

        if (existingRole is not null)
        {
            invitation.AcceptedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return invitation.Organization.ToSummary(existingRole.Value);
        }

        // Named ct rather than token, which would shadow the invitation token.
        await context.ExecuteInTransactionAsync(async ct =>
        {
            // Same bootstrap problem as organization creation: the membership
            // names an organization that is not the active tenant yet.
            await context.SetLocalOrganizationAsync(invitation.OrganizationId, ct);

            context.Memberships.Add(new MembershipEntity
            {
                OrganizationId = invitation.OrganizationId,
                UserId = userId,
                Role = invitation.Role,
            });

            invitation.AcceptedAt = DateTimeOffset.UtcNow;

            await context.SaveChangesAsync(ct);
        }, cancellationToken);

        return invitation.Organization.ToSummary(invitation.Role);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private Guid RequireUserId() =>
        tenantContext.UserId ?? throw new FunctionalException(
            "No authenticated user.",
            ErrorScenario.InvalidCredentials,
            HttpStatusCode.Unauthorized);

    private void RequireOrganizationsEnabled()
    {
        if (!_features.Organizations)
        {
            throw FunctionalException.Forbidden(
                "Organization management is disabled for this deployment.",
                ErrorScenario.OrganizationsDisabled);
        }
    }

    private async Task RequireHrAdminAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var role = await organizationService.GetRoleAsync(RequireUserId(), organizationId, cancellationToken)
            ?? throw FunctionalException.Forbidden(
                "You are not a member of this organization.",
                ErrorScenario.NotAMember);

        if (role != OrganizationRole.HrAdmin)
        {
            throw FunctionalException.Forbidden(
                "This action requires the HR admin role.",
                ErrorScenario.InsufficientOrganizationRole);
        }
    }
}
