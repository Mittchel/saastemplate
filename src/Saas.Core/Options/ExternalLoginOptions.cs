namespace Saas.Core.Options;

/// <summary>
/// Social sign-in providers.
/// </summary>
/// <remarks>
/// The client id is shared with the frontend, which uses it to run the OAuth
/// dance, while the identity service uses it as the expected audience when it
/// validates the resulting token. One value fed to both, so a provider can
/// never be half-configured -- enabled in the UI but unverifiable on the
/// server.
/// </remarks>
public class ExternalLoginOptions
{
    public const string SectionName = "ExternalLogin";

    public GoogleOptions Google { get; set; } = new();

    public bool IsEnabled(string provider) => provider switch
    {
        ExternalLoginProviders.Google => Google.IsConfigured,
        _ => false,
    };
}

public class GoogleOptions
{
    /// <summary>
    /// The OAuth client id, used as the expected <c>aud</c> of the ID token.
    /// </summary>
    /// <remarks>
    /// Checking the audience is what stops a token Google minted for a
    /// different application being replayed against this one.
    /// </remarks>
    public string? ClientId { get; set; }

    /// <summary>Google's OpenID discovery document.</summary>
    public string MetadataAddress { get; set; } =
        "https://accounts.google.com/.well-known/openid-configuration";

    /// <summary>Accepted <c>iss</c> values. Google uses both spellings.</summary>
    public string[] ValidIssuers { get; set; } =
        ["https://accounts.google.com", "accounts.google.com"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}

public static class ExternalLoginProviders
{
    public const string Google = "google";
}
