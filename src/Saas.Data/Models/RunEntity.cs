namespace Saas.Data.Models;

/// <summary>
/// One agent invocation. Created by the .NET API once the caller is authorized;
/// the Python service reads it back and streams the result.
/// </summary>
/// <remarks>
/// This row is the authorization handoff between the two services. The AI
/// service does not mint or verify any token of its own: it calls
/// <c>GET /internal/runs/{id}</c> carrying the caller's access token, and
/// row-level security decides whether the row is visible. Ownership and tenancy
/// are therefore enforced by the database rather than by bespoke token logic.
/// </remarks>
public class RunEntity : BaseEntity, ITenantScoped
{
    public Guid OrganizationId { get; set; }

    public OrganizationEntity Organization { get; set; } = null!;

    public Guid UserId { get; set; }

    /// <summary>LangGraph conversation thread; the checkpointer keys state on this.</summary>
    public string ThreadId { get; set; } = string.Empty;

    public RunStatus Status { get; set; } = RunStatus.Pending;

    public string Model { get; set; } = string.Empty;

    /// <summary>The user turn that started the run.</summary>
    public string Input { get; set; } = string.Empty;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? Error { get; set; }

}
