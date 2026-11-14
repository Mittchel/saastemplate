using Saas.Business.Contracts;

namespace Saas.Business.Services.Interfaces;

/// <summary>Reads and updates the signed-in user.</summary>
public interface IUserService
{
    /// <summary>
    /// Builds the payload behind <c>GET /me</c>: the user, their active
    /// organization, every organization they can switch into, their effective
    /// permissions and the server's feature flags.
    /// </summary>
    /// <remarks>
    /// The feature flags travel with this response on purpose, so the frontend
    /// never hard-codes whether organizations are enabled.
    /// </remarks>
    Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates the signed-in user's own profile.</summary>
    Task<CurrentUserResponse> UpdateProfileAsync(
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);
}
