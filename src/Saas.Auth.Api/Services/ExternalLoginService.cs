using Microsoft.AspNetCore.Identity;
using Saas.Auth.Api.Tenancy;
using Saas.Business.Services.Interfaces;
using Saas.Data;
using Saas.Data.Extensions;
using Saas.Data.Models;

namespace Saas.Auth.Api.Services;

/// <summary>Maps a verified social identity onto a local account.</summary>
public interface IExternalLoginService
{
    Task<UserEntity> FindOrCreateAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public class ExternalLoginService(
    UserManager<UserEntity> userManager,
    IProvisioningService provisioningService,
    MutableTenantContext tenantContext,
    ApplicationDbContext context,
    ILogger<ExternalLoginService> logger) : IExternalLoginService
{
    /// <inheritdoc />
    public async Task<UserEntity> FindOrCreateAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken = default)
    {
        // An unverified address must not be able to claim an existing account:
        // some providers will hand out a token for an address the user never
        // proved they control, which would be account takeover by sign-up.
        if (!identity.EmailVerified)
        {
            throw new ExternalLoginException(
                "The provider has not verified this email address.");
        }

        var existing = await userManager.FindByEmailAsync(identity.Email);

        if (existing is not null)
        {
            await LinkLoginAsync(existing, identity);
            return existing;
        }

        var (firstName, lastName) = SplitName(identity.Name, identity.Email);

        var user = new UserEntity
        {
            Id = Guid.CreateVersion7(),
            UserName = identity.Email,
            Email = identity.Email,
            // The provider vouched for it, which is the whole point of
            // requiring EmailVerified above.
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            FullName = identity.Name?.Trim() is { Length: > 0 } name
                ? name
                : string.Join(' ', new[] { firstName, lastName }.Where(p => p.Length > 0)),
        };

        // Same atomicity requirement as password registration: an account
        // without a workspace cannot sign in and has no way to repair itself.
        await context.ExecuteInTransactionAsync(async ct =>
        {
            // No password: this account can only be reached through the
            // provider until the user sets one.
            var created = await userManager.CreateAsync(user);

            if (!created.Succeeded)
            {
                throw new ExternalLoginException(
                    string.Join(' ', created.Errors.Select(e => e.Description)));
            }

            await LinkLoginAsync(user, identity);

            tenantContext.SetUser(user.Id);
            await provisioningService.CreatePersonalOrganizationAsync(user, ct);
        }, cancellationToken);

        logger.LogInformation(
            "Created {UserId} from a verified {Provider} identity.", user.Id, identity.Provider);

        return user;
    }

    /// <summary>
    /// Records the provider's subject against the account.
    /// </summary>
    /// <remarks>
    /// Stored so a later email change at the provider does not orphan the link,
    /// and so the same person signing in twice is recognised as one account.
    /// </remarks>
    private async Task LinkLoginAsync(UserEntity user, ExternalIdentity identity)
    {
        var logins = await userManager.GetLoginsAsync(user);

        var alreadyLinked = logins.Any(login =>
            string.Equals(login.LoginProvider, identity.Provider, StringComparison.OrdinalIgnoreCase)
            && login.ProviderKey == identity.Subject);

        if (alreadyLinked)
        {
            return;
        }

        await userManager.AddLoginAsync(
            user,
            new UserLoginInfo(identity.Provider, identity.Subject, identity.Provider));
    }

    private static (string FirstName, string LastName) SplitName(string? name, string email)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return (email.Split('@')[0], string.Empty);
        }

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], string.Empty);
    }
}
