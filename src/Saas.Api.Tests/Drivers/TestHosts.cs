using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Saas.Api;
using Saas.Auth.Api;
using Testcontainers.PostgreSql;

namespace Saas.Api.Tests.Drivers;

/// <summary>
/// The test run's own infrastructure: a Postgres container and the API and
/// identity service hosted in-process from the working tree.
/// </summary>
/// <remarks>
/// <para>
/// Started once per test run by <c>DriverHooks</c> and torn down when the run
/// ends. Postgres is the same image the compose stack uses, with the logins
/// created by <c>src/Saas.Data/scripts/00-init.sh</c> itself, so row-level
/// security is real and enforced against a non-owner role.
/// </para>
/// <para>
/// Tokens never leave the process: the identity service's issuer is a fixed
/// name that resolves nowhere, and the API's metadata/JWKS requests are routed
/// to the in-process identity host through its backchannel handler.
/// </para>
/// </remarks>
public static class TestHosts
{
    private const string Database = "saas";
    private const string OwnerPassword = "test_owner_pw";
    private const string AppPassword = "test_app_pw";
    private const string AiPassword = "test_ai_pw";
    private const string AdminPassword = "test_postgres_pw";
    private const string InitScriptInContainer = "/tmp/00-init.sh";

    /// <summary>Issuer written into tokens and expected by the API. Never resolved over a network.</summary>
    private const string Issuer = "http://auth.test";

    private static TestCertificates? _certificates;
    private static PostgreSqlContainer? _postgres;
    private static WebApplicationFactory<ApiEntryPoint>? _api;
    private static WebApplicationFactory<AuthEntryPoint>? _auth;

    /// <summary>The temporary folder holding this run's generated certificates, or null when not started.</summary>
    public static string? CertificateFolder => _certificates?.Folder;

    /// <summary>Path of this run's generated signing <c>.pfx</c>, or null when not started.</summary>
    public static string? SigningCertificatePath => _certificates?.SigningCertificatePath;

    /// <summary>Path of this run's generated encryption <c>.pfx</c>, or null when not started.</summary>
    public static string? EncryptionCertificatePath => _certificates?.EncryptionCertificatePath;

    /// <summary>The identity host's services, to inspect the credentials it actually loaded.</summary>
    public static IServiceProvider AuthServices =>
        (_auth ?? throw new InvalidOperationException("Test hosts are not started.")).Services;

    /// <summary>Password of this run's generated <c>.pfx</c> files, or null when not started.</summary>
    public static string? CertificatePassword => _certificates?.Password;

    public static string AppConnectionString { get; private set; } = string.Empty;
    public static string OwnerConnectionString { get; private set; } = string.Empty;
    public static string AiConnectionString { get; private set; } = string.Empty;

    /// <remarks>Each client is owned by its caller; the factory itself is disposed in <see cref="StopAsync"/>.</remarks>
    public static HttpClient CreateApiClient() =>
        (_api ?? throw new InvalidOperationException("Test hosts are not started.")).CreateClient();

    /// <remarks>Each client is owned by its caller; the factory itself is disposed in <see cref="StopAsync"/>.</remarks>
    public static HttpClient CreateAuthClient() =>
        (_auth ?? throw new InvalidOperationException("Test hosts are not started.")).CreateClient();

    public static async Task StartAsync(string clientId, string clientSecret, string administratorEmail)
    {
        try
        {
            _certificates = await StartWithCertificatesAsync(
                certificates => StartHostsAsync(certificates, clientId, clientSecret, administratorEmail));
        }
        catch
        {
            try
            {
                await StopAsync();
            }
            catch
            {
                // Cleanup is best effort; the startup failure is the error to surface.
            }

            throw;
        }
    }

    /// <summary>
    /// Generates the certificates and runs <paramref name="start"/> with them. If it throws, the
    /// certificate folder is deleted before the failure propagates, so a failed startup leaves no keys behind.
    /// </summary>
    internal static async Task<TestCertificates> StartWithCertificatesAsync(Func<TestCertificates, Task> start)
    {
        var certificates = TestCertificates.Create();
        try
        {
            await start(certificates);
            return certificates;
        }
        catch
        {
            certificates.Dispose();
            throw;
        }
    }

    private static async Task StartHostsAsync(
        TestCertificates certificates,
        string clientId,
        string clientSecret,
        string administratorEmail)
    {
        _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase(Database)
            .WithUsername("postgres")
            .WithPassword(AdminPassword)
            .Build();

        await _postgres.StartAsync();

        await CreateLoginsAsync(_postgres);

        AppConnectionString = ConnectionString(_postgres, "saas_app", AppPassword);
        OwnerConnectionString = ConnectionString(_postgres, "saas_owner", OwnerPassword);
        AiConnectionString = ConnectionString(_postgres, "saas_ai", AiPassword);

        // The API first: its hosted service applies the migrations the identity
        // service's seeder waits for.
        _api = new ApiFactory(AppConnectionString, OwnerConnectionString, () => _auth!);
        _ = _api.Server;

        _auth = new AuthFactory(AppConnectionString, certificates, clientId, clientSecret, administratorEmail);
        _ = _auth.Server;
    }

    public static async Task StopAsync()
    {
        try
        {
            try
            {
                if (_auth is not null)
                {
                    await _auth.DisposeAsync();
                }
            }
            finally
            {
                try
                {
                    if (_api is not null)
                    {
                        await _api.DisposeAsync();
                    }
                }
                finally
                {
                    if (_postgres is not null)
                    {
                        await _postgres.DisposeAsync();
                    }
                }
            }
        }
        finally
        {
            // Always remove the temporary certificate folder, even if a disposal above threw.
            _certificates?.Dispose();
            _certificates = null;
        }
    }

    /// <summary>Runs the repository's own init script, so the roles are never re-declared here.</summary>
    private static async Task CreateLoginsAsync(PostgreSqlContainer postgres)
    {
        var script = Path.Combine(AppContext.BaseDirectory, "00-init.sh");

        await postgres.CopyAsync(File.ReadAllBytes(script), InitScriptInContainer);

        var result = await postgres.ExecAsync(
        [
            "env",
            "POSTGRES_USER=postgres",
            $"POSTGRES_DB={Database}",
            $"SAAS_OWNER_PASSWORD={OwnerPassword}",
            $"SAAS_APP_PASSWORD={AppPassword}",
            $"SAAS_AI_PASSWORD={AiPassword}",
            "bash",
            InitScriptInContainer,
        ]);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"00-init.sh failed ({result.ExitCode}): {result.Stderr}{result.Stdout}");
        }
    }

    private static string ConnectionString(PostgreSqlContainer postgres, string user, string password) =>
        new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = postgres.Hostname,
            Port = postgres.GetMappedPublicPort(5432),
            Database = Database,
            Username = user,
            Password = password,
        }.ConnectionString;

    /// <summary>Configuration shared by both hosts, matching the dev stack.</summary>
    private static void ConfigureCommon(
        Microsoft.AspNetCore.Hosting.IWebHostBuilder builder,
        string appConnectionString)
    {
        builder.UseEnvironment("Development");

        // UseSetting rather than ConfigureAppConfiguration: Program reads these
        // while it is still building the services.
        builder.UseSetting("ConnectionStrings:DefaultConnection", appConnectionString);
        builder.UseSetting("Features:Organizations", "true");
        builder.UseSetting("Features:PublicSignup", "true");

        // Nothing listens here, so the (logged and swallowed) mail send fails fast.
        builder.UseSetting("Mail:Host", "127.0.0.1");
        builder.UseSetting("Mail:Port", "1");
    }

    private sealed class ApiFactory(
        string appConnectionString,
        string ownerConnectionString,
        Func<WebApplicationFactory<AuthEntryPoint>> auth) : WebApplicationFactory<ApiEntryPoint>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            ConfigureCommon(builder, appConnectionString);

            builder.UseSetting("ConnectionStrings:Migrations", ownerConnectionString);
            builder.UseSetting("Authentication:Authority", Issuer);
            builder.UseSetting("Authentication:PublicAuthority", Issuer);
            builder.UseSetting("Authentication:RequireHttpsMetadata", "false");

            builder.ConfigureServices(services =>
                services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwt =>
                    jwt.BackchannelHttpHandler = new InProcessHandler(() => auth().Server.CreateHandler())));
        }
    }

    private sealed class AuthFactory(
        string appConnectionString,
        TestCertificates certificates,
        string clientId,
        string clientSecret,
        string administratorEmail) : WebApplicationFactory<AuthEntryPoint>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            ConfigureCommon(builder, appConnectionString);

            builder.UseSetting("OpenIddict:Issuer", Issuer + "/");
            // Files, so the service never reaches the development certificates in the OS store.
            builder.UseSetting("OpenIddict:SigningCertificatePath", certificates.SigningCertificatePath);
            builder.UseSetting("OpenIddict:SigningCertificatePassword", certificates.Password);
            builder.UseSetting("OpenIddict:EncryptionCertificatePath", certificates.EncryptionCertificatePath);
            builder.UseSetting("OpenIddict:EncryptionCertificatePassword", certificates.Password);
            builder.UseSetting("OpenIddict:Clients:0:ClientId", clientId);
            builder.UseSetting("OpenIddict:Clients:0:ClientSecret", clientSecret);
            builder.UseSetting("Platform:AdministratorEmail", administratorEmail);
        }
    }

    /// <summary>Forwards to the identity host's handler, resolved on first use.</summary>
    private sealed class InProcessHandler(Func<HttpMessageHandler> resolve) : HttpMessageHandler
    {
        private readonly Lazy<HttpMessageInvoker> _invoker = new(() => new HttpMessageInvoker(resolve()));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _invoker.Value.SendAsync(request, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing && _invoker.IsValueCreated)
            {
                _invoker.Value.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
