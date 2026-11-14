using Microsoft.Extensions.Configuration;

namespace Saas.Api.Tests.Drivers;

/// <summary>
/// Where the tests point.
/// </summary>
/// <remarks>
/// These are integration tests against the running compose stack rather than an
/// in-memory <c>WebApplicationFactory</c>. That is deliberate: the guarantee
/// most worth testing is Postgres row-level security, and an in-memory or
/// SQLite database has no RLS to enforce -- a passing test there would prove
/// nothing. Start the stack with <c>docker compose up -d</c> first.
/// </remarks>
public class TestSettings
{
    private static readonly IConfigurationRoot Configuration = new ConfigurationBuilder()
        .AddJsonFile("integrationsettings.json", optional: false)
        .AddEnvironmentVariables()
        .Build();

    public string AuthBaseUrl => Get("AuthBaseUrl");
    public string ApiBaseUrl => Get("ApiBaseUrl");
    public string AiBaseUrl => Get("AiBaseUrl");
    public string Postgres => Get("Postgres");
    public string PostgresOwner => Get("PostgresOwner");
    public string ClientId => Get("ClientId");
    public string ClientSecret => Get("ClientSecret");
    public string DevUserEmail => Get("DevUserEmail");
    public string DevUserPassword => Get("DevUserPassword");

    /// <summary>A seeded account that is deliberately not the platform operator.</summary>
    public string MemberUserEmail => Get("MemberUserEmail");
    public string MemberUserPassword => Get("MemberUserPassword");

    private static string Get(string key) =>
        Configuration[key] ?? throw new InvalidOperationException($"Missing setting: {key}");
}
