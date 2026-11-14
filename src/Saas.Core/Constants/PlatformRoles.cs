namespace Saas.Core.Constants;

/// <summary>
/// Roles that span the whole deployment, as opposed to one organization.
/// </summary>
/// <remarks>
/// Held through ASP.NET Identity role membership rather than a column, so the
/// existing role plumbing (and its audit trail) applies. Organization roles are
/// a separate concept entirely -- see <c>OrganizationRole</c>.
/// </remarks>
public static class PlatformRoles
{
    /// <summary>Operates the platform: can enter any workspace.</summary>
    public const string Administrator = "Administrator";
}
