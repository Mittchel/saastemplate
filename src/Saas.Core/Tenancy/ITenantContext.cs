namespace Saas.Core.Tenancy;

/// <summary>
/// The organization the current request acts on behalf of.
/// </summary>
/// <remarks>
/// Lives in Core rather than the API so the data layer can consume it without
/// taking a dependency on ASP.NET. Resolved from the <c>org_id</c> access-token
/// claim and pushed into the Postgres session, where row-level security reads
/// it back as <c>app.organization_id</c>.
/// </remarks>
public interface ITenantContext
{
    /// <summary>The active organization, or <c>null</c> for unauthenticated requests.</summary>
    Guid? OrganizationId { get; }

    /// <summary>The authenticated user, or <c>null</c>.</summary>
    Guid? UserId { get; }

    /// <summary>
    /// True when the request runs outside any tenant -- migrations, seeding and
    /// background jobs. Such work must set the tenant explicitly.
    /// </summary>
    bool IsAmbient { get; }

    /// <summary>
    /// Whether the caller operates the platform.
    /// </summary>
    /// <remarks>
    /// Read from the access token, which only the identity service can mint.
    /// It does not widen what this request can read -- the tenant is still a
    /// single organization -- it gates the operator-only endpoints.
    /// </remarks>
    bool IsPlatformAdmin { get; }

    /// <summary>Throws when no organization is in scope. Use before tenant-scoped writes.</summary>
    Guid RequireOrganizationId();
}
