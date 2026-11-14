using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Saas.Business.Contracts;
using Saas.Business.Mapping;
using Saas.Business.Services.Interfaces;
using Saas.Core.ErrorScenarios;
using Saas.Core.Exceptions;
using Saas.Core.Options;
using Saas.Core.Tenancy;
using Saas.Data;
using Saas.Data.Models;
using Saas.Data.Models.Constants;

namespace Saas.Business.Services;

/// <inheritdoc />
public class UserService(
    ApplicationDbContext context,
    ITenantContext tenantContext,
    IOptions<FeatureOptions> featureOptions) : IUserService
{
    private readonly FeatureOptions _features = featureOptions.Value;

    /// <inheritdoc />
    public async Task<CurrentUserResponse> GetCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = tenantContext.UserId
            ?? throw new FunctionalException(
                "No authenticated user.",
                ErrorScenario.InvalidCredentials,
                System.Net.HttpStatusCode.Unauthorized);

        var organizationId = tenantContext.RequireOrganizationId();

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw FunctionalException.NotFound("User");

        // The membership query filter permits rows belonging to the active
        // organization or to the caller, so this returns every organization the
        // user may switch into.
        IReadOnlyList<MembershipEntity> memberships = await context.Memberships
            .AsNoTracking()
            .Include(m => m.Organization)
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Organization.IsPersonal)
            .ThenBy(m => m.Organization.Name)
            .ToListAsync(cancellationToken);

        var isPlatformAdmin = tenantContext.IsPlatformAdmin;
        var active = memberships.FirstOrDefault(m => m.OrganizationId == organizationId);

        if (active is null)
        {
            // A platform operator who has entered a workspace to support it has
            // no membership row in it -- that is the point. Without this the
            // whole app 403s the moment they enter, because every page starts
            // by calling this method.
            //
            // The organization itself is readable: the row-level security
            // policy admits the row whose id is the active tenant, which it now
            // is. Everything else stays normal, so they see what a member sees.
            if (!isPlatformAdmin)
            {
                throw FunctionalException.Forbidden(
                    "You are not a member of the selected organization.",
                    ErrorScenario.NotAMember);
            }

            var organization = await context.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken)
                ?? throw FunctionalException.NotFound("Organization");

            active = new MembershipEntity
            {
                OrganizationId = organizationId,
                Organization = organization,
                UserId = userId,
                // Matches the role the token was issued with; an operator enters
                // as a member, not an owner.
                Role = OrganizationRole.Member,
            };

            // Surfaced in the switcher so they can get back out again.
            memberships = [.. memberships, active];
        }

        return BuildResponse(user, active, memberships, isPlatformAdmin);
    }

    /// <inheritdoc />
    public async Task<CurrentUserResponse> UpdateProfileAsync(
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        var userId = tenantContext.UserId
            ?? throw new FunctionalException(
                "No authenticated user.",
                ErrorScenario.InvalidCredentials,
                System.Net.HttpStatusCode.Unauthorized);

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw FunctionalException.NotFound("User");

        user.FirstName = firstName.Trim();
        user.LastName = lastName.Trim();

        // FullName is denormalized for display; keep it derived in one place.
        user.FullName = string.Join(' ', new[] { user.FirstName, user.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        await context.SaveChangesAsync(cancellationToken);

        return await GetCurrentUserAsync(cancellationToken);
    }

    private CurrentUserResponse BuildResponse(
        UserEntity user,
        MembershipEntity active,
        IReadOnlyCollection<MembershipEntity> memberships,
        bool isPlatformAdmin)
    {
        var organizations = memberships
            .Select(m => m.Organization.ToSummary(m.Role))
            .ToList();

        return new CurrentUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? string.Empty : user.FullName,
            user.FirstName,
            user.LastName,
            user.Avatar,
            active.Organization.ToSummary(active.Role),
            active.Role,
            organizations,
            OrganizationRolePermissions.For(active.Role),
            isPlatformAdmin,
            new FeatureFlags(_features.Organizations, _features.PublicSignup));
    }
}
