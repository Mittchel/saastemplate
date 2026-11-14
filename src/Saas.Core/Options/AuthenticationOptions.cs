namespace Saas.Core.Options;

/// <summary>How the resource API validates access tokens.</summary>
public class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    /// <summary>
    /// Issuer reachable from inside the container network, used to fetch JWKS.
    /// </summary>
    public string Authority { get; set; } = "http://auth:8080";

    /// <summary>
    /// Issuer as it appears in the <c>iss</c> claim -- the public hostname.
    /// Differs from <see cref="Authority"/> because tokens are minted behind
    /// Traefik but validated over the internal network.
    /// </summary>
    public string PublicAuthority { get; set; } = "http://auth.localhost";

    public bool RequireHttpsMetadata { get; set; }
}
