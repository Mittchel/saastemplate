namespace Saas.Core.Constants;

/// <summary>
/// Custom claim types minted by the identity service.
/// </summary>
/// <remarks>
/// The tenant travels in the access token, which is what lets the resource API
/// and the Python AI service both resolve it without a database lookup.
/// </remarks>
public static class SaasClaimTypes
{
    /// <summary>Active organization. Always present -- every user has at least a personal org.</summary>
    public const string OrganizationId = "org_id";

    /// <summary>The caller's role within <see cref="OrganizationId"/>.</summary>
    public const string OrganizationRole = "org_role";

    /// <summary>Fine-grained permission string, repeated once per granted permission.</summary>
    public const string Permission = "permission";

    /// <summary>
    /// Present and "true" when the account operates the platform itself.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from the organization role. A platform admin is
    /// still scoped to exactly one organization at a time -- the claim does not
    /// widen what they can read, it only lets them switch into any workspace
    /// and reach the operator-only endpoints.
    /// </remarks>
    public const string PlatformAdmin = "is_platform_admin";
}
