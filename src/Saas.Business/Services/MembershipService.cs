using System.Net;
using Microsoft.EntityFrameworkCore;
using Saas.Business.Contracts;
using Saas.Business.Mapping;
using Saas.Business.Services.Interfaces;
using Saas.Core.ErrorScenarios;
using Saas.Core.Exceptions;
using Saas.Core.Tenancy;
using Saas.Data;
using Saas.Data.Models;

namespace Saas.Business.Services;

/// <inheritdoc />
public class MembershipService(
    ApplicationDbContext context,
    ITenantContext tenantContext) : IMembershipService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<MemberSummary>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        RequireActiveOrganization(organizationId);

        var members = await context.Memberships
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.OrganizationId == organizationId)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.User.FullName)
            .ToListAsync(cancellationToken);

        return members.Select(m => m.ToSummary()).ToList();
    }

    /// <inheritdoc />
    public async Task<MemberSummary> UpdateRoleAsync(
        Guid organizationId,
        Guid userId,
        OrganizationRole role,
        CancellationToken cancellationToken = default)
    {
        RequireActiveOrganization(organizationId);

        if (!Enum.IsDefined(role))
        {
            throw new FunctionalException("A valid role is required.", ErrorScenario.ValidationFailed);
        }

        var membership = await context.Memberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.UserId == userId,
                cancellationToken)
            ?? throw FunctionalException.NotFound("Membership");

        // Demoting the last HR admin would leave the organization unadministrable.
        if (membership.Role == OrganizationRole.HrAdmin && role != OrganizationRole.HrAdmin)
        {
            await RequireAnotherHrAdminAsync(organizationId, userId, cancellationToken);
        }

        membership.Role = role;
        await context.SaveChangesAsync(cancellationToken);

        return membership.ToSummary();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        RequireActiveOrganization(organizationId);
        await RemoveInternalAsync(organizationId, userId, cancellationToken);
    }

    /// <inheritdoc />
    public Task LeaveAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        RemoveInternalAsync(organizationId, RequireUserId(), cancellationToken);

    private async Task RemoveInternalAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await context.Memberships
            .Include(m => m.Organization)
            .FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.UserId == userId,
                cancellationToken)
            ?? throw FunctionalException.NotFound("Membership");

        // The personal workspace is the fallback every user lands in, so it
        // cannot be abandoned.
        if (membership.Organization.IsPersonal)
        {
            throw FunctionalException.Forbidden(
                "You cannot leave your personal workspace.",
                ErrorScenario.CannotLeavePersonalOrganization);
        }

        if (membership.Role == OrganizationRole.HrAdmin)
        {
            await RequireAnotherHrAdminAsync(organizationId, userId, cancellationToken);
        }

        context.Memberships.Remove(membership);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task RequireAnotherHrAdminAsync(
        Guid organizationId,
        Guid excludingUserId,
        CancellationToken cancellationToken)
    {
        var anotherHrAdminExists = await context.Memberships.AnyAsync(
            m => m.OrganizationId == organizationId
                 && m.UserId != excludingUserId
                 && m.Role == OrganizationRole.HrAdmin,
            cancellationToken);

        if (!anotherHrAdminExists)
        {
            throw FunctionalException.Conflict(
                "The last HR admin of an organization cannot be removed or demoted.",
                ErrorScenario.LastOwnerCannotBeRemoved);
        }
    }

    private Guid RequireUserId() =>
        tenantContext.UserId ?? throw new FunctionalException(
            "No authenticated user.",
            ErrorScenario.InvalidCredentials,
            HttpStatusCode.Unauthorized);

    private void RequireActiveOrganization(Guid organizationId)
    {
        if (tenantContext.OrganizationId != organizationId)
        {
            throw FunctionalException.NotFound("Organization");
        }
    }
}
