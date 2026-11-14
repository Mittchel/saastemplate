using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Saas.Core.Tenancy;
using Saas.Data;
using Saas.Data.Models;
using Saas.Data.Models.Constants;

namespace Saas.Api.Authorization;

/// <summary>
/// Satisfies a <see cref="PermissionRequirement"/> from the caller's current
/// membership row.
/// </summary>
/// <remarks>
/// Controllers check permissions, never roles. The caller's role is looked up
/// on every request, through the scoped <see cref="ApplicationDbContext"/> and
/// therefore under row-level security, so a role change or a removed membership
/// applies on the next request rather than when the token expires. The token's
/// <c>permission</c> claims and the platform-admin claim are never consulted:
/// no membership row in the active organization means no permissions.
/// </remarks>
public class PermissionHandler(ApplicationDbContext dbContext, ITenantContext tenant)
    : AuthorizationHandler<PermissionRequirement>
{
    // Several requirements can be evaluated in one request; the lookup is
    // made once and reused for the length of that request (this handler is scoped).
    private Task<OrganizationRole?>? _role;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        _role ??= LoadRoleAsync();

        var role = await _role;

        if (role is not null &&
            OrganizationRolePermissions.For(role.Value).Contains(requirement.Permission, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }
    }

    private async Task<OrganizationRole?> LoadRoleAsync()
    {
        if (tenant.UserId is not { } userId || tenant.OrganizationId is not { } organizationId)
        {
            return null;
        }

        var membership = await dbContext.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.OrganizationId == organizationId)
            .Select(m => new { m.Role })
            .FirstOrDefaultAsync();

        return membership?.Role;
    }
}
