using Saas.Data.Models;

namespace Saas.Business.Services.Interfaces;

/// <summary>
/// Creates organizations together with their first membership.
/// </summary>
/// <remarks>
/// Shared by signup (which creates the personal organization) and by explicit
/// organization creation. Both are bootstrap operations: they insert a
/// membership row for an organization that is not yet the caller's active
/// tenant, which the row-level security policy rejects by default. This service
/// is the one place that widens the tenant for a single transaction, so the
/// policy on <c>Memberships</c> can stay strict everywhere else.
/// </remarks>
public interface IProvisioningService
{
    /// <summary>
    /// Creates the organization every user gets at signup.
    /// </summary>
    /// <remarks>
    /// Created regardless of whether the Organizations feature is enabled --
    /// the tenant is always an organization, so there is only ever one code
    /// path and enabling the feature later needs no migration.
    /// </remarks>
    Task<OrganizationEntity> CreatePersonalOrganizationAsync(
        UserEntity user,
        CancellationToken cancellationToken = default);

    /// <summary>Creates an additional organization owned by <paramref name="userId"/>.</summary>
    Task<OrganizationEntity> CreateOrganizationAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken = default);

}
