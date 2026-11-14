using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Saas.Core.Tenancy;
using Saas.Data;
using Saas.Data.Models;
using Saas.Data.Models.Constants;

namespace Saas.Api.Authorization;

/// <summary>
/// Satisfies a <see cref="PermissionRequirement"/> from the caller's current
/// membership row in the active organization.
/// </summary>
/// <remarks>
/// Controllers check permissions, never roles. The role is looked up on every
/// request, so a role change or a removed membership takes effect immediately
/// rather than when the access token (up to 30 minutes old) is replaced. The
/// token's <c>permission</c> claims are for the UI only and are never read here,
/// nor is the platform-admin claim: an operator who entered a workspace they
/// are not a member of has no row and is refused. The lookup goes through the
/// scoped context, so row-level security applies as usual.
/// </remarks>
public class PermissionHandler(ApplicationDbContext dbContext, ITenantContext tenant)
    : AuthorizationHandler<PermissionRequirement>
{
    // One request can evaluate several requirements; read the row once.
    private bool _loaded;
    private IReadOnlyList<string> _granted = [];

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!_loaded)
        {
            if (tenant.UserId is { } userId && tenant.OrganizationId is { } organizationId)
            {
                var role = await dbContext.Memberships
                    .AsNoTracking()
                    .Where(m => m.UserId == userId && m.OrganizationId == organizationId)
                    .Select(m => (OrganizationRole?)m.Role)
                    .FirstOrDefaultAsync();

                // No row means no permissions, with no fallback role.
                _granted = role is { } found ? OrganizationRolePermissions.For(found) : [];
            }

            _loaded = true;
        }

        if (_granted.Contains(requirement.Permission, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }
    }
}
