using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Saas.Core.Tenancy;
using Saas.Data;
using Saas.Data.Models.Constants;

namespace Saas.Api.Authorization;

/// <summary>
/// Satisfies a <see cref="PermissionRequirement"/> from the caller's current
/// membership (employee record) in the active organization.
/// </summary>
/// <remarks>
/// Controllers check permissions, never roles. The role is read from the
/// database on every request through the normal, RLS-scoped context, so a role
/// change or removal applies on the next request. The <c>permission</c> token
/// claims are for the UI only and are never read here.
/// </remarks>
public class PermissionHandler(ApplicationDbContext context, ITenantContext tenantContext)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext authorizationContext,
        PermissionRequirement requirement)
    {
        if (tenantContext.UserId is not { } userId || tenantContext.OrganizationId is not { } organizationId)
        {
            return;
        }

        var membership = await context.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.OrganizationId == organizationId)
            .Select(m => new { m.Role })
            .FirstOrDefaultAsync();

        if (membership is not null &&
            OrganizationRolePermissions.For(membership.Role).Contains(requirement.Permission))
        {
            authorizationContext.Succeed(requirement);
        }
    }
}
