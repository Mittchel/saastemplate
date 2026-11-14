namespace Saas.Data.Models;

/// <summary>
/// Marks an entity as belonging to exactly one organization.
/// </summary>
/// <remarks>
/// Drives both isolation layers: the EF Core global query filter (layer 1,
/// which produces friendly behaviour for ordinary mistakes) and the Postgres
/// row-level security policy generated for the table (layer 2, which makes a
/// missed filter return zero rows instead of another tenant's data).
/// </remarks>
public interface ITenantScoped
{
    Guid OrganizationId { get; set; }
}
