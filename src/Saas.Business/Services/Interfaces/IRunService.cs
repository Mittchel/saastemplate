using Saas.Business.Contracts;
using Saas.Data.Models;

namespace Saas.Business.Services.Interfaces;

/// <summary>
/// Owns the lifecycle of an agent run.
/// </summary>
/// <remarks>
/// The run row is the authorization handoff between the two services. The API
/// authorises the caller, then persists a run; the AI service reads it back
/// with the caller's own access token and streams the result. Nothing
/// mints a bespoke token for the handoff -- row-level security decides whether
/// the AI service can see the run at all.
/// </remarks>
public interface IRunService
{
    /// <summary>Authorises the caller and persists a new run.</summary>
    Task<RunResponse> CreateAsync(
        CreateRunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The run as the AI service needs it, scoped by row-level security.</summary>
    Task<InternalRunResponse> GetInternalAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<RunResponse> GetAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RunResponse>> ListAsync(
        string? threadId,
        CancellationToken cancellationToken = default);

    /// <summary>Reports progress back from the AI service.</summary>
    Task UpdateStatusAsync(
        Guid runId,
        RunStatus status,
        string? error = null,
        CancellationToken cancellationToken = default);
}
