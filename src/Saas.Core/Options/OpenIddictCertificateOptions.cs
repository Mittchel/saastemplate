namespace Saas.Core.Options;

/// <summary>
/// Paths to the token signing and encryption certificates.
/// </summary>
/// <remarks>
/// Deliberately not OpenIddict's development certificates: both the resource
/// API and the Python AI service validate tokens against this service's JWKS,
/// so a key that regenerates on restart breaks every consumer. Generate with
/// <c>scripts/generate-certs.ps1</c>.
/// </remarks>
public class OpenIddictCertificateOptions
{
    public const string SectionName = "OpenIddict";

    /// <summary>
    /// The value written into every token as <c>iss</c>.
    /// </summary>
    /// <remarks>
    /// Pinned rather than derived from the request. OpenIddict otherwise takes
    /// the issuer from the host header, so the same service hands out tokens
    /// claiming <c>http://auth.localhost/</c> through the gateway and
    /// <c>http://localhost:5001/</c> on its published port -- and a consumer
    /// validating the issuer rejects whichever one it was not told about. A
    /// fixed value makes a token identical however the service was reached.
    /// </remarks>
    public string? Issuer { get; set; }

    public string? SigningCertificatePath { get; set; }
    public string? SigningCertificatePassword { get; set; }
    public string? EncryptionCertificatePath { get; set; }
    public string? EncryptionCertificatePassword { get; set; }

    public List<OpenIddictClientOptions> Clients { get; set; } = [];

    public bool HasCertificates =>
        !string.IsNullOrWhiteSpace(SigningCertificatePath) &&
        !string.IsNullOrWhiteSpace(EncryptionCertificatePath);
}

public class OpenIddictClientOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
