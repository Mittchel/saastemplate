using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saas.Business.Contracts;
using Saas.Business.Services.Interfaces;
using Saas.Data.Models;

namespace Saas.Api.Controllers;

/// <summary>
/// Endpoints the AI service calls back into.
/// </summary>
/// <remarks>
/// <para>
/// "Internal" describes the caller, not the trust level. Every route here is
/// authenticated with the <em>end user's own access token</em>, forwarded by the
/// Python service -- there is no service account, no shared secret and no
/// network-level allowlist standing in for authorization.
/// </para>
/// <para>
/// That is what makes the handoff safe: <see cref="GetRun"/> carries no
/// ownership predicate because the query filter and the row-level security
/// policy already scope the run to the caller's organization. A run belonging
/// to another tenant is simply not found, and the guarantee comes from the
/// database rather than from this controller remembering to check.
/// </para>
/// </remarks>
[ApiController]
[Route("internal")]
[Consumes("application/json")]
[Produces("application/json")]
[Authorize]
public class InternalController(
    IRunService runService) : Controller
{
    /// <summary>Everything the AI service needs to execute a run.</summary>
    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType<InternalRunResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRun(Guid runId, CancellationToken cancellationToken) =>
        Ok(await runService.GetInternalAsync(runId, cancellationToken));

    /// <summary>Reports run progress back from the AI service.</summary>
    [HttpPut("runs/{runId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateRunStatus(
        Guid runId,
        [FromBody] UpdateRunStatusRequest request,
        CancellationToken cancellationToken)
    {
        await runService.UpdateStatusAsync(runId, request.Status, request.Error, cancellationToken);

        return NoContent();
    }

}

public record UpdateRunStatusRequest(RunStatus Status, string? Error);
