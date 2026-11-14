using Saas.Business.Contracts;
using Saas.Data.Models;

namespace Saas.Business.Services.Interfaces;

/// <summary>Manages who belongs to an organization and with what role.</summary>
public interface IMembershipService
{
    Task<IReadOnlyList<MemberSummary>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<MemberSummary> UpdateRoleAsync(
        Guid organizationId,
        Guid userId,
        OrganizationRole role,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the signed-in user from an organization they joined.</summary>
    Task LeaveAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
