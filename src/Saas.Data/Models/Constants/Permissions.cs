using System.Reflection;

namespace Saas.Data.Models.Constants;

/// <summary>
/// Fine-grained permission strings. One authorization policy is registered per
/// constant at startup, so adding a permission here is all that is required.
/// </summary>
public static class Permissions
{
    public const string UserRead = "user.read";
    public const string UserWrite = "user.write";
    public const string UserCreate = "user.create";
    public const string UserDelete = "user.delete";

    public const string RoleRead = "role.read";
    public const string RoleWrite = "role.write";
    public const string RoleCreate = "role.create";
    public const string RoleDelete = "role.delete";

    public const string OrganizationRead = "organization.read";
    public const string OrganizationWrite = "organization.write";
    public const string OrganizationDelete = "organization.delete";

    public const string MemberRead = "member.read";
    public const string MemberInvite = "member.invite";
    public const string MemberWrite = "member.write";
    public const string MemberRemove = "member.remove";

    public const string InvitationManage = "invitation.manage";

    public const string TeamRead = "team.read";
    public const string TeamWrite = "team.write";

    public const string EmployeeRead = "employee.read";
    public const string EmployeeWrite = "employee.write";
    public const string EmployeeImport = "employee.import";

    public const string OrgChartRead = "orgchart.read";
    public const string ProfileOwn = "profile.own";

    public const string RunCreate = "run.create";
    public const string RunRead = "run.read";

    /// <summary>Every permission declared above, discovered by reflection.</summary>
    public static IReadOnlyList<string> GetAll() => _all.Value;

    private static readonly Lazy<IReadOnlyList<string>> _all = new(() =>
        typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList());
}
