using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>
/// Behavioural checks for DIA-134: the identity host signs with certificates
/// the test run generated, kept in a temporary folder that is removed when the
/// run ends.
/// </summary>
[Binding]
public class TestCertificatesAcceptanceSteps
{
    private static DateTime _runStartedUtc = DateTime.UtcNow;
    private static readonly List<string> RememberedFolders = [];
    private static readonly object Gate = new();

    private JsonElement _signingKey;

    // Runs before DriverHooks.StartHosts (default order 10000).
    [BeforeTestRun(Order = 0)]
    public static void NoteRunStart() => _runStartedUtc = DateTime.UtcNow.AddMinutes(-1);

    // Runs after DriverHooks.StopHosts (default order 10000): the run has ended.
    [AfterTestRun(Order = 20000)]
    public static void CertificateFoldersAreGoneAfterTheRun()
    {
        string[] folders;

        lock (Gate)
        {
            folders = [.. RememberedFolders];
        }

        foreach (var folder in folders)
        {
            Directory.Exists(folder).ShouldBeFalse(
                $"the generated certificate folder {folder} should be deleted when the run ends");
        }
    }

    [When("I read the signing key published by the identity host")]
    public async Task ReadSigningKey()
    {
        using var client = TestHosts.CreateAuthClient();

        var configuration = await client.GetAsync("/.well-known/openid-configuration");
        configuration.IsSuccessStatusCode.ShouldBeTrue();

        using var configDocument = JsonDocument.Parse(await configuration.Content.ReadAsStringAsync());
        var jwksUri = new Uri(configDocument.RootElement.GetProperty("jwks_uri").GetString()!);

        var jwks = await client.GetAsync(jwksUri.PathAndQuery);
        jwks.IsSuccessStatusCode.ShouldBeTrue();

        using var keysDocument = JsonDocument.Parse(await jwks.Content.ReadAsStringAsync());
        var key = keysDocument.RootElement.GetProperty("keys")
            .EnumerateArray()
            .First(k => k.GetProperty("kty").GetString() == "RSA");

        _signingKey = key.Clone();
    }

    [Then("the signing key is an RSA 2048 key")]
    public void SigningKeyIsRsa2048()
    {
        var modulus = Base64UrlDecode(_signingKey.GetProperty("n").GetString()!);

        // A leading zero byte may pad the modulus.
        modulus.SkipWhile(b => b == 0).Count().ShouldBe(256);
    }

    [Then("the signing key is not held in the operating system's certificate store")]
    public void SigningKeyNotInStore()
    {
        var thumbprint = SigningThumbprint();

        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        store.Certificates
            .Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false)
            .Count.ShouldBe(0, "the signing certificate should come from a generated file, not the store");
    }

    [Then("the signing certificate is not the OpenIddict development certificate")]
    public void SigningCertificateIsNotDevelopment()
    {
        if (!_signingKey.TryGetProperty("x5c", out var chain))
        {
            // Without the certificate in the key set, the store check is the evidence.
            return;
        }

        var raw = Convert.FromBase64String(chain[0].GetString()!);
        using var certificate = X509CertificateLoader.LoadCertificate(raw);

        certificate.Subject.ShouldNotContain("OpenIddict Server Signing Certificate");
        certificate.Subject.ShouldBe(certificate.Issuer, "the generated certificate is self-signed");
        certificate.GetRSAPublicKey()!.KeySize.ShouldBe(2048);
    }

    [Then("the run has generated password-protected pfx files in a temporary folder")]
    public void GeneratedFilesAreProtected()
    {
        var folders = FindGeneratedFolders();

        folders.ShouldNotBeEmpty("no folder with generated .pfx files was found under the temp path");

        foreach (var folder in folders)
        {
            var files = Directory.GetFiles(folder, "*.pfx");

            files.Length.ShouldBeGreaterThanOrEqualTo(2);

            foreach (var file in files)
            {
                Should.Throw<CryptographicException>(
                    () => X509CertificateLoader.LoadPkcs12FromFile(file, null),
                    $"{file} should need a password");
            }
        }

        lock (Gate)
        {
            RememberedFolders.AddRange(folders.Where(f => !RememberedFolders.Contains(f)));
        }
    }

    [Then("no certificate file is under the repository sources")]
    public void NoCertificatesInRepository()
    {
        var root = FindRepositoryRoot();
        var source = Path.Combine(root, "src");
        var separator = Path.DirectorySeparatorChar;

        var found = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separator}node_modules{separator}", StringComparison.Ordinal))
            .Where(f => f.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
            .ToList();

        found.ShouldBeEmpty("the test run must not write certificate files into the repository");
    }

    [Then("the identity service still falls back to the development certificates when no file is configured")]
    public void IdentityLoadingCodeKeepsItsFallback()
    {
        var path = Path.Combine(
            FindRepositoryRoot(), "src", "Saas.Auth.Api", "Extensions", "ServiceCollectionExtensions.cs");
        var text = File.ReadAllText(path);

        text.ShouldContain("AddDevelopmentSigningCertificate");
        text.ShouldContain("AddDevelopmentEncryptionCertificate");
        text.ShouldNotContain("TestCertificates");
    }

    [Then("the compose configuration does not refer to the test certificates")]
    public void ComposeUntouched()
    {
        var root = FindRepositoryRoot();

        foreach (var file in Directory.EnumerateFiles(root, "*compose*.y*ml", SearchOption.TopDirectoryOnly))
        {
            File.ReadAllText(file).ShouldNotContain("TestCertificates");
            File.ReadAllText(file).ShouldNotContain("saas-test");
        }
    }

    [Then("the Testing section of CLAUDE.md says the test hosts generate their own certificates")]
    public void ClaudeMdTestingSection()
    {
        var lines = File.ReadAllLines(Path.Combine(FindRepositoryRoot(), "CLAUDE.md"));

        var start = Array.FindIndex(lines, l => l.StartsWith('#') && l.Contains("Testing", StringComparison.OrdinalIgnoreCase));
        start.ShouldBeGreaterThanOrEqualTo(0, "CLAUDE.md has no Testing section");

        var level = lines[start].TakeWhile(c => c == '#').Count();
        var section = new List<string>();

        for (var i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#') && lines[i].TakeWhile(c => c == '#').Count() <= level)
            {
                break;
            }

            section.Add(lines[i]);
        }

        var text = string.Join('\n', section);

        text.ShouldContain("certificate", Case.Insensitive);
        text.ShouldContain("keychain", Case.Insensitive);
    }

    private string SigningThumbprint()
    {
        if (_signingKey.TryGetProperty("x5c", out var chain))
        {
            using var certificate = X509CertificateLoader.LoadCertificate(
                Convert.FromBase64String(chain[0].GetString()!));

            return certificate.Thumbprint;
        }

        var x5t = _signingKey.GetProperty("x5t").GetString()!;

        return Convert.ToHexString(Base64UrlDecode(x5t));
    }

    private static List<string> FindGeneratedFolders()
    {
        var temp = Path.GetTempPath();

        return Directory.EnumerateDirectories(temp)
            .Where(d => Directory.GetLastWriteTimeUtc(d) >= _runStartedUtc)
            .Where(d => Directory.GetFiles(d, "*.pfx").Length >= 2)
            .ToList();
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '='));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Saas.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Saas.slnx not found above the test output directory.");
    }
}
