namespace Saas.Data.Models;

/// <summary>
/// The tenant. Every row in a tenant-scoped table points at one of these, and
/// the active organization travels in the access token as <c>org_id</c>.
/// </summary>
/// <remarks>
/// Every user gets one of these at signup even when the Organizations feature
/// is switched off -- see <see cref="IsPersonal"/>. That is what makes the
/// feature flag a pure UI concern: there is only ever one code path, and
/// enabling organizations later needs no migration.
/// </remarks>
public class OrganizationEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe unique identifier. Not currently used for routing, but
    /// reserved so per-organization deep links can be added without a migration.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// True for the organization created automatically for a single user.
    /// Personal organizations cannot be left, renamed away or deleted while
    /// they are the user's only tenant.
    /// </summary>
    public bool IsPersonal { get; set; }

    public ICollection<MembershipEntity> Memberships { get; set; } = [];

    public ICollection<InvitationEntity> Invitations { get; set; } = [];

}
