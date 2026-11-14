namespace Saas.Data.Models.Constants;

/// <summary>
/// Maps an organization role to the permissions it implies.
/// </summary>
/// <remarks>
/// The resource API resolves a caller's permissions from this mapping on every
/// request, so it only ever checks permissions, never roles. The identity
/// service uses the same mapping for the <c>permission</c> claims it mints for
/// the UI. Any stored value that is not a defined role grants nothing.
/// </remarks>
public static class OrganizationRolePermissions
{
    public static IReadOnlyList<string> For(OrganizationRole role) => role switch
    {
        OrganizationRole.Owner =>
        [
            Permissions.OrganizationRead, Permissions.OrganizationWrite, Permissions.OrganizationDelete,
            Permissions.MemberRead, Permissions.MemberInvite, Permissions.MemberWrite, Permissions.MemberRemove,
            Permissions.RunCreate, Permissions.RunRead,
            Permissions.UserRead,
        ],
        OrganizationRole.Admin =>
        [
            Permissions.OrganizationRead, Permissions.OrganizationWrite,
            Permissions.MemberRead, Permissions.MemberInvite, Permissions.MemberWrite, Permissions.MemberRemove,
            Permissions.RunCreate, Permissions.RunRead,
            Permissions.UserRead,
        ],
        OrganizationRole.Member =>
        [
            Permissions.OrganizationRead,
            Permissions.MemberRead,
            Permissions.RunCreate, Permissions.RunRead,
        ],
        _ => [],
    };
}
