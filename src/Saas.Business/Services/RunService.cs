using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
public class RunService(
    ApplicationDbContext context,
    ITenantContext tenantContext,
    IConfiguration configuration) : IRunService
{
    private string DefaultModel =>
        configuration["Ai:DefaultModel"] ?? "gpt-6.1-sol";

    /// <inheritdoc />
    public async Task<RunResponse> CreateAsync(
        CreateRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var organizationId = tenantContext.RequireOrganizationId();

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            throw new FunctionalException("A message is required.", ErrorScenario.ValidationFailed);
        }

        var run = new RunEntity
        {
            OrganizationId = organizationId,
            UserId = userId,
            // A new thread id starts a fresh conversation; passing an existing
            // one continues it, which is what the checkpointer keys state on.
            ThreadId = string.IsNullOrWhiteSpace(request.ThreadId)
                ? Guid.CreateVersion7().ToString("n")
                : request.ThreadId.Trim(),
            Status = RunStatus.Pending,
            Model = DefaultModel,
            Input = request.Input.Trim(),
        };

        context.Runs.Add(run);
        await context.SaveChangesAsync(cancellationToken);

        return run.ToResponse();
    }

    /// <inheritdoc />
    public async Task<InternalRunResponse> GetInternalAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        // No ownership predicate here on purpose. The query filter and the RLS
        // policy both scope this to the caller's organization, so a run
        // belonging to another tenant is simply not found.
        var run = await context.Runs
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
            ?? throw new FunctionalException(
                "Run was not found.", ErrorScenario.RunNotFound, HttpStatusCode.NotFound);

        return run.ToInternalResponse();
    }

    /// <inheritdoc />
    public async Task<RunResponse> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await context.Runs
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
            ?? throw new FunctionalException(
                "Run was not found.", ErrorScenario.RunNotFound, HttpStatusCode.NotFound);

        return run.ToResponse();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RunResponse>> ListAsync(
        string? threadId,
        CancellationToken cancellationToken = default)
    {
        var query = context.Runs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(threadId))
        {
            query = query.Where(r => r.ThreadId == threadId);
        }

        var runs = await query
            .OrderByDescending(r => r.CreatedDate)
            .Take(100)
            .ToListAsync(cancellationToken);

        return runs.Select(r => r.ToResponse()).ToList();
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(
        Guid runId,
        RunStatus status,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        var run = await context.Runs.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
            ?? throw new FunctionalException(
                "Run was not found.", ErrorScenario.RunNotFound, HttpStatusCode.NotFound);

        if (run.Status is RunStatus.Completed or RunStatus.Failed or RunStatus.Canceled)
        {
            throw FunctionalException.Conflict(
                "This run has already finished.", ErrorScenario.RunAlreadyCompleted);
        }

        run.Status = status;
        run.Error = error;

        if (status == RunStatus.Running)
        {
            run.StartedAt = DateTimeOffset.UtcNow;
        }
        else if (status is RunStatus.Completed or RunStatus.Failed or RunStatus.Canceled)
        {
            run.CompletedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private Guid RequireUserId() =>
        tenantContext.UserId ?? throw new FunctionalException(
            "No authenticated user.",
            ErrorScenario.InvalidCredentials,
            HttpStatusCode.Unauthorized);
}
