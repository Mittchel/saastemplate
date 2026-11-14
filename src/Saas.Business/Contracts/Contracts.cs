using Saas.Data.Models;

namespace Saas.Business.Contracts;

// ---------------------------------------------------------------------------
// API-facing shapes.
//
// Deliberate deviation from the earlier templates, which carried three parallel
// model sets (XxxDto in the API, Xxx in Business, XxxEntity in Data) with
// AutoMapper between each boundary. That triples the surface for no benefit at
// this size -- the middle model was never shaped differently from the entity in
// either prior repo. Two sets are kept: entities own persistence concerns,
// these records own the wire contract, and EntityMapper projects between them.
//
// If a product outgrows this, reintroducing a Business model is additive.
// ---------------------------------------------------------------------------

/// <summary>Everything the frontend needs to render a signed-in shell.</summary>
/// <remarks>
/// Returned by <c>GET /me</c>. Carries the feature flags so the UI never
/// hard-codes whether organizations are enabled -- the backend stays the single
/// source of truth.
/// </remarks>
public record CurrentUserResponse(
    Guid Id,
    string Email,
    string FullName,
    // The parts as well as the whole: the profile form edits these, and
    // splitting FullName back apart loses names that do not fit "first last".
    string FirstName,
    string LastName,
    string? Avatar,
    OrganizationSummary Organization,
    OrganizationRole OrganizationRole,
    IReadOnlyList<OrganizationSummary> Organizations,
    IReadOnlyList<string> Permissions,
    bool IsPlatformAdmin,
    FeatureFlags Features);

/// <summary>Server-controlled UI switches.</summary>
public record FeatureFlags(bool Organizations, bool PublicSignup);

public record OrganizationSummary(
    Guid Id,
    string Name,
    string Slug,
    bool IsPersonal,
    OrganizationRole Role);

public record OrganizationDetail(
    Guid Id,
    string Name,
    string Slug,
    bool IsPersonal,
    DateTimeOffset CreatedDate,
    int MemberCount);

/// <summary>One tenant, as the operator overview shows it.</summary>
/// <remarks>
/// Summary counts only. An operator who needs to see inside a workspace
/// switches into it, and is then scoped to it like any other member.
/// </remarks>
public record PlatformOrganizationSummary(
    Guid Id,
    string Name,
    string Slug,
    bool IsPersonal,
    DateTimeOffset CreatedDate,
    long MemberCount,
    long RunCount);

public record CreateOrganizationRequest(string Name);

public record UpdateOrganizationRequest(string Name);

public record MemberSummary(
    Guid UserId,
    string Email,
    string FullName,
    string? Avatar,
    OrganizationRole Role,
    DateTimeOffset JoinedAt);

public record UpdateMemberRoleRequest(OrganizationRole Role);

public record InvitationSummary(
    Guid Id,
    string Email,
    OrganizationRole Role,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt,
    bool IsPending);

public record CreateInvitationRequest(string Email, OrganizationRole Role);

public record AcceptInvitationRequest(string Token);

/// <summary>Starts an agent run. The caller supplies the conversation thread.</summary>
public record CreateRunRequest(string Input, string? ThreadId);

/// <summary>
/// The handoff to the AI service: the browser takes this and opens the stream.
/// </summary>
/// <remarks>
/// Carries no token of its own. The AI service re-reads the run through
/// <c>GET /internal/runs/{id}</c> using the caller's own access token, so
/// ownership is decided by row-level security rather than by a bearer secret
/// invented here.
/// </remarks>
public record RunResponse(
    Guid Id,
    string ThreadId,
    RunStatus Status,
    string Model,
    DateTimeOffset CreatedDate);

/// <summary>Everything the AI service needs to execute a run.</summary>
public record InternalRunResponse(
    Guid Id,
    Guid OrganizationId,
    Guid UserId,
    string ThreadId,
    string Model,
    string Input,
    RunStatus Status);

