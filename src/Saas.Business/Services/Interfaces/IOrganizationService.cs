using Saas.Business.Contracts;
using Saas.Data.Models;

namespace Saas.Business.Services.Interfaces;

/// <summary>Organization lifecycle and membership lookups.</summary>
public interface IOrganizationService
{
    /// <summary>Every organization the signed-in user can switch into.</summary>
    Task<IReadOnlyList<OrganizationSummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>One organization, including its member count.</summary>
    Task<OrganizationDetail> GetAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Creates an additional organization and makes the caller its owner.</summary>
    /// <remarks>Requires the Organizations feature; personal organizations are
    /// created by <see cref="IProvisioningService"/> at signup regardless.</remarks>
    Task<OrganizationSummary> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<OrganizationDetail> UpdateAsync(
        Guid organizationId,
        string name,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a user's role in an organization, or <c>null</c> when they are
    /// not a member.
    /// </summary>
    /// <remarks>
    /// Called by the identity service to authorise the organization-switch
    /// grant before minting a token with a new <c>org_id</c> claim. It must
    /// therefore work while the caller's active tenant is still the previous
    /// organization.
    /// </remarks>
    Task<OrganizationRole?> GetRoleAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The organization a freshly signed-in user should start in: their last
    /// active one when still valid, otherwise their personal organization.
    /// </summary>
    Task<Guid?> ResolveDefaultOrganizationAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
