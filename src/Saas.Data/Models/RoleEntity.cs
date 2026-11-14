using Microsoft.AspNetCore.Identity;

namespace Saas.Data.Models;

/// <summary>
/// Platform-level role (for example Administrator). Distinct from
/// <see cref="OrganizationRole"/>, which scopes a user inside one organization.
/// </summary>
public class RoleEntity : IdentityRole<Guid>
{
    public string? Description { get; set; }
}
