using Microsoft.Extensions.Configuration;

namespace Saas.Api.Tests.Drivers;

/// <summary>
/// Where the tests point.
/// </summary>
/// <remarks>
/// These are integration tests, but not against the compose stack: each run
/// starts its own Postgres container with Testcontainers and hosts
/// <c>Saas.Api</c> and <c>Saas.Auth.Api</c> in-process from the working tree
/// (see <see cref="TestHosts"/>). Postgres stays real because the guarantee
/// most worth testing is row-level security, and an in-memory or SQLite database
/// has none to enforce. Running the tests needs Docker, not the compose stack.
/// The database connection strings therefore come from the container, not from
/// <c>integrationsettings.json</c>.
/// </remarks>
public class TestSettings
{
    private static readonly IConfigurationRoot Configuration = new ConfigurationBuilder()
        .AddJsonFile("integrationsettings.json", optional: false)
        .AddEnvironmentVariables()
        .Build();

    /// <summary>The container's <c>saas_app</c> login: the runtime role, subject to RLS.</summary>
    public string Postgres => Started(TestHosts.AppConnectionString);

    /// <summary>The container's <c>saas_owner</c> login: owns the tables and runs migrations.</summary>
    public string PostgresOwner => Started(TestHosts.OwnerConnectionString);

    /// <summary>The container's <c>saas_ai</c> login, which has no access to business tables.</summary>
    public string PostgresAi => Started(TestHosts.AiConnectionString);

    private static string Started(string connectionString) =>
        connectionString.Length > 0
            ? connectionString
            : throw new InvalidOperationException("Test hosts are not started.");

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
