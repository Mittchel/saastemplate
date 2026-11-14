namespace Saas.Data.Models;

/// <summary>A member's role within a single organization. The roles are not a hierarchy; never compare them by ordinal.</summary>
public enum OrganizationRole
{
    Employee = 0,
    Manager = 1,
    HrAdmin = 2,
}


public enum RunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Canceled = 4,
}
