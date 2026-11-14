using Saas.Business.Contracts;

namespace Saas.Business.Services.Interfaces;

/// <summary>
/// Operator-only reads that deliberately cross tenant boundaries.
/// </summary>
/// <remarks>
/// Every method here goes through a <c>SECURITY DEFINER</c> function rather
/// than an ordinary query, because row-level security is <c>FORCE</c>d and no
/// login can see across tenants. That is the point: the exemption is a short,
/// auditable list of read-only operations, not a flag that relaxes the
/// policies for a whole request.
///
/// Callers must already have established that the user is a platform admin --
/// this service does not re-check, and the API enforces it with
/// <c>RequirePlatformAdmin</c>.
/// </remarks>
public interface IPlatformAdminService
{
    /// <summary>Every organization on the deployment, with summary counts.</summary>
    Task<IReadOnlyList<PlatformOrganizationSummary>> ListOrganizationsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether an organization exists, regardless of who can see it.
    /// </summary>
    /// <remarks>
    /// Used when an operator switches into a workspace they are not a member
    /// of: the ordinary lookup cannot distinguish "no such tenant" from "not
    /// yours".
    /// </remarks>
    Task<bool> OrganizationExistsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
