namespace Saas.Data.Models.Constants;

/// <summary>
/// Maps an organization role to the permissions it implies.
/// </summary>
/// <remarks>
/// Keeps the two authorization systems coherent: the identity service expands a
/// member's <see cref="OrganizationRole"/> into <c>permission</c> claims for the
/// UI, and the resource API looks the role up per request, so it only ever
/// checks permissions, never roles.
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

        // A stored value that is not a defined role grants nothing.
        _ => [],
    };
}
