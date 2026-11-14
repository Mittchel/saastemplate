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
    private static readonly string[] HrAdmin =
    [
        Permissions.OrganizationRead, Permissions.OrganizationWrite, Permissions.OrganizationDelete,
        Permissions.MemberRead, Permissions.MemberInvite, Permissions.MemberWrite, Permissions.MemberRemove,
        Permissions.UserRead, Permissions.RunCreate, Permissions.RunRead,
        Permissions.TeamRead, Permissions.TeamWrite,
        Permissions.EmployeeRead, Permissions.EmployeeWrite, Permissions.EmployeeImport,
        Permissions.InvitationManage, Permissions.OrgChartRead, Permissions.ProfileOwn,
    ];

    public static IReadOnlyList<string> For(OrganizationRole role) => role switch
    {
        // Owner and Admin both become the HR admin role later.
        OrganizationRole.Owner or OrganizationRole.Admin => HrAdmin,
        OrganizationRole.Member =>
        [
            Permissions.OrganizationRead,
            Permissions.RunCreate, Permissions.RunRead,
            Permissions.TeamRead, Permissions.EmployeeRead, Permissions.ProfileOwn,
        ],

        // A stored value that is not a defined role grants nothing.
        _ => [],
    };
}
