using Microsoft.AspNetCore.Authorization;
using Saas.Core.Constants;

namespace Saas.Api.Authorization;

/// <summary>
/// Satisfies a <see cref="PermissionRequirement"/> from the token's
/// <c>permission</c> claims.
/// </summary>
/// <remarks>
/// Controllers check permissions, never roles. The identity service expands the
/// caller's organization role into permission claims when it mints the token,
/// which keeps the two authorization models from drifting apart and means a
/// role change takes effect on the next token rather than needing a lookup on
/// every request.
/// </remarks>
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var granted = context.User.Claims.Any(claim =>
            claim.Type == SaasClaimTypes.Permission &&
            string.Equals(claim.Value, requirement.Permission, StringComparison.Ordinal));

        if (granted)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
