using Microsoft.AspNetCore.Authorization;

namespace Saas.Api.Authorization;

/// <summary>Requires a single permission string on the access token.</summary>
public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
