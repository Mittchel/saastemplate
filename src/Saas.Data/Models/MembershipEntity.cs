namespace Saas.Data.Models;

/// <summary>Links a user to an organization with a role.</summary>
/// <remarks>
/// Its RLS policy is deliberately wider than the other tenant tables: a row is
/// visible when it belongs to the active organization <em>or</em> to the
/// calling user. Without the second clause the organization switcher could not
/// list the organizations a user may switch into.
/// </remarks>
public class MembershipEntity : BaseEntity, ITenantScoped
{
    public Guid OrganizationId { get; set; }

    public OrganizationEntity Organization { get; set; } = null!;

    public Guid UserId { get; set; }

    public UserEntity User { get; set; } = null!;

    public OrganizationRole Role { get; set; } = OrganizationRole.Employee;
}
