namespace Saas.Data.Models;

/// <summary>A member's role within a single organization.</summary>
public enum OrganizationRole
{
    Member = 0,
    Admin = 1,
    Owner = 2,
}


public enum RunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Canceled = 4,
}
