using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Saas.Core.Options;
using JsonWebTokenHandler = Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler;

namespace Saas.Auth.Api.Services;

/// <summary>A verified identity from a social provider.</summary>
public sealed record ExternalIdentity(
    string Provider,
    string Subject,
    string Email,
    bool EmailVerified,
    string? Name);

/// <summary>
/// Verifies ID tokens issued by social providers.
/// </summary>
/// <remarks>
/// <para>
/// The identity service validates the provider's token itself rather than
/// trusting the frontend's word for who signed in. The browser -- and anything
/// pretending to be it -- can claim any email address; only the provider's
/// signature over its own token proves one.
/// </para>
/// <para>
/// Signing keys are fetched from the provider's discovery document and cached
/// with automatic refresh, so key rotation needs no deployment.
/// </para>
/// </remarks>
public interface IExternalIdentityValidator
{
    Task<ExternalIdentity> ValidateAsync(
        string provider,
        string idToken,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public class ExternalIdentityValidator(
    IOptions<ExternalLoginOptions> options,
    ILogger<ExternalIdentityValidator> logger) : IExternalIdentityValidator
{
    private static readonly JsonWebTokenHandler Handler = new();

    // One manager per provider, created once: each caches the provider's keys
    // and refreshes them on its own schedule. Building one per request would
    // fetch the discovery document on every sign-in.
    private static readonly Dictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>
        Managers = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Lock ManagerLock = new();

    private readonly ExternalLoginOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ExternalIdentity> ValidateAsync(
        string provider,
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsEnabled(provider))
        {
            throw new ExternalLoginException($"The {provider} provider is not configured.");
        }

        var (metadataAddress, validIssuers, audience) = provider switch
        {
            ExternalLoginProviders.Google =>
                (_options.Google.MetadataAddress, _options.Google.ValidIssuers, _options.Google.ClientId!),
            _ => throw new ExternalLoginException($"Unknown provider: {provider}."),
        };

        var configuration = await GetConfigurationAsync(provider, metadataAddress, cancellationToken);

        var result = await Handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = validIssuers,

            // The audience check is the one that matters most here: without it
            // an ID token minted for any other Google application would be
            // accepted as a sign-in to this one.
            ValidateAudience = true,
            ValidAudience = audience,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),

            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
        });

        if (!result.IsValid)
        {
            logger.LogWarning(
                result.Exception,
                "Rejected an external {Provider} identity token.",
                provider);

            throw new ExternalLoginException("The sign-in token could not be verified.");
        }

        var claims = result.Claims;

        var email = Read(claims, "email");
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ExternalLoginException("The provider did not return an email address.");
        }

        var emailVerified = Read(claims, "email_verified") is "true" or "True";

        return new ExternalIdentity(
            Provider: provider,
            Subject: Read(claims, "sub") ?? throw new ExternalLoginException("No subject claim."),
            Email: email.Trim().ToLowerInvariant(),
            EmailVerified: emailVerified,
            Name: Read(claims, "name"));
    }

    private static string? Read(IDictionary<string, object> claims, string type) =>
        claims.TryGetValue(type, out var value) ? value?.ToString() : null;

    private static Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string provider,
        string metadataAddress,
        CancellationToken cancellationToken)
    {
        ConfigurationManager<OpenIdConnectConfiguration> manager;

        lock (ManagerLock)
        {
            if (!Managers.TryGetValue(provider, out manager!))
            {
                manager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever { RequireHttps = true });

                Managers[provider] = manager;
            }
        }

        return manager.GetConfigurationAsync(cancellationToken);
    }
}

/// <summary>A social sign-in that could not be verified.</summary>
public class ExternalLoginException(string message) : Exception(message);
