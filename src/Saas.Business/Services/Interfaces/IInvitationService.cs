using Saas.Business.Contracts;
using Saas.Data.Models;

namespace Saas.Business.Services.Interfaces;

/// <summary>Invites people into an organization and redeems those invitations.</summary>
public interface IInvitationService
{
    Task<IReadOnlyList<InvitationSummary>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates an invitation and returns it with the token to send.</summary>
    Task<(InvitationSummary Invitation, string Token)> CreateAsync(
        Guid organizationId,
        string email,
        OrganizationRole role,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems an invitation for the signed-in user.
    /// </summary>
    /// <remarks>
    /// The caller is by definition not yet a member of the target
    /// organization, so the lookup deliberately bypasses the query filter and
    /// the membership insert widens the tenant for one transaction.
    /// </remarks>
    Task<OrganizationSummary> AcceptAsync(string token, CancellationToken cancellationToken = default);
}
