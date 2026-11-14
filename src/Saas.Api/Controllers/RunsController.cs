using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saas.Business.Contracts;
using Saas.Business.Services.Interfaces;
using Saas.Data.Models.Constants;

namespace Saas.Api.Controllers;

/// <summary>
/// Agent runs.
/// </summary>
/// <remarks>
/// This controller is the control plane; the token stream is the data plane and
/// belongs to the Python service. The browser calls <see cref="Create"/> to get
/// authorisation and a run id, then opens the stream directly against the AI
/// service. Relaying those tokens through ASP.NET would add a hop that
/// transforms nothing, holds a connection open for the duration, and risks the
/// response-buffering middleware turning a stream into one blob.
/// </remarks>
[ApiController]
[Route("runs")]
[Consumes("application/json")]
[Produces("application/json")]
[Authorize]
public class RunsController(ILogger<RunsController> logger, IRunService runService) : Controller
{
    /// <summary>
    /// Authorises and persists a new run.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.RunCreate)]
    [ProducesResponseType<RunResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRunRequest request,
        CancellationToken cancellationToken)
    {
        var run = await runService.CreateAsync(request, cancellationToken);

        logger.LogInformation(
            "Created run {RunId} on thread {ThreadId}", run.Id, run.ThreadId);

        return CreatedAtAction(nameof(Get), new { runId = run.Id }, run);
    }

    [HttpGet("{runId:guid}")]
    [Authorize(Policy = Permissions.RunRead)]
    [ProducesResponseType<RunResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid runId, CancellationToken cancellationToken) =>
        Ok(await runService.GetAsync(runId, cancellationToken));

    /// <summary>Recent runs, optionally narrowed to one conversation thread.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.RunRead)]
    [ProducesResponseType<IReadOnlyList<RunResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? threadId,
        CancellationToken cancellationToken) =>
        Ok(await runService.ListAsync(threadId, cancellationToken));
}
