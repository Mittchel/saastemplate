using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saas.Business.Contracts;
using Saas.Business.Services.Interfaces;
using Saas.Core.Constants;

namespace Saas.Api.Controllers;

/// <summary>
/// Operator endpoints, for whoever runs the deployment.
/// </summary>
/// <remarks>
/// <para>
/// Gated on the <c>is_platform_admin</c> claim, which only the identity service
/// can mint and only for an account holding the Administrator role.
/// </para>
/// <para>
/// Read-only on purpose. An operator who needs to act inside a workspace
/// switches into it and is then scoped to that tenant like any other member,
/// with every policy applying. That keeps the cross-tenant surface to a list of
/// summary counts rather than a parallel API that can write anywhere.
/// </para>
/// </remarks>
[ApiController]
[Route("platform")]
[Consumes("application/json")]
[Produces("application/json")]
[Authorize(Policy = PlatformPolicies.Administrator)]
public class PlatformController(IPlatformAdminService platformAdminService) : Controller
{
    /// <summary>Every organization on the deployment.</summary>
    [HttpGet("organizations")]
    [ProducesResponseType<IReadOnlyList<PlatformOrganizationSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListOrganizations(CancellationToken cancellationToken) =>
        Ok(await platformAdminService.ListOrganizationsAsync(cancellationToken));
}
