using Microsoft.AspNetCore.Identity;

namespace Saas.Data.Models;

/// <summary>
/// Application user. Global rather than tenant-scoped -- one identity can
/// belong to several organizations through <see cref="MembershipEntity"/>.
/// </summary>
public class UserEntity : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>Denormalized display name, maintained by the user service.</summary>
    public string FullName { get; set; } = string.Empty;

    public string? Avatar { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Organization selected at last sign-in, used to pick the initial
    /// <c>org_id</c> claim. Falls back to the personal organization.
    /// </summary>
    public Guid? LastActiveOrganizationId { get; set; }

    public ICollection<MembershipEntity> Memberships { get; set; } = [];
}
