namespace Saas.Data.Models.Constants;

/// <summary>
/// Maps an organization role to the permissions it implies.
/// </summary>
/// <remarks>
/// Keeps the two authorization systems coherent: the identity service expands a
/// member's <see cref="OrganizationRole"/> into <c>permission</c> claims, so the
/// resource API only ever checks permissions, never roles.
/// </remarks>
public static class OrganizationRolePermissions
{
    public static IReadOnlyList<string> For(OrganizationRole role) => role switch
    {
        OrganizationRole.HrAdmin =>
        [
            Permissions.OrganizationRead, Permissions.OrganizationWrite, Permissions.OrganizationDelete,
            Permissions.MemberRead, Permissions.MemberInvite, Permissions.MemberWrite, Permissions.MemberRemove,
            Permissions.UserRead,
            Permissions.RunCreate, Permissions.RunRead,
            Permissions.TeamRead, Permissions.TeamWrite,
            Permissions.EmployeeRead, Permissions.EmployeeWrite, Permissions.EmployeeImport,
            Permissions.InvitationManage, Permissions.OrgChartRead, Permissions.ProfileOwn,
        ],
        OrganizationRole.Manager or OrganizationRole.Employee =>
        [
            Permissions.OrganizationRead,
            Permissions.RunCreate, Permissions.RunRead,
            Permissions.TeamRead, Permissions.EmployeeRead,
            Permissions.ProfileOwn,
        ],
        _ => [],
    };
}
