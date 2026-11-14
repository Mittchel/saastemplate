using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

[Binding]
public class TestCertificatesSteps(ScenarioContext scenario)
{
    private const string OwnCertificatesKey = "TestCertificates.Own";
    private const string FailureParentKey = "TestCertificates.FailureParent";

    private static string Folder => TestHosts.CertificateFolder.ShouldNotBeNull();

    [Given("a fresh set of test certificates")]
    public void FreshCertificates()
    {
        var certificates = TestCertificates.Create();
        Directory.Exists(certificates.Folder).ShouldBeTrue();
        scenario[OwnCertificatesKey] = certificates;
    }

    [When("the test certificates are disposed")]
    public void DisposeCertificates() =>
        scenario.Get<TestCertificates>(OwnCertificatesKey).Dispose();

    [Then("their folder no longer exists")]
    public void OwnFolderIsGone() =>
        Directory.Exists(scenario.Get<TestCertificates>(OwnCertificatesKey).Folder).ShouldBeFalse();

    [When("generating test certificates fails")]
    public void GenerationFails()
    {
        // A parent of our own, so the check looks at exactly the folder this attempt would have used.
        var parent = Path.Combine(Path.GetTempPath(), "saas-test-certs-failure-" + Guid.CreateVersion7().ToString("n"));
        Directory.CreateDirectory(parent);
        scenario[FailureParentKey] = parent;

        Should.Throw<Exception>(() => TestCertificates.Create(parent, "not a distinguished name"));
    }

    [Then("no certificate folder is left behind")]
    public void NoFolderLeft()
    {
        var parent = scenario.Get<string>(FailureParentKey);
        try
        {
            Directory.EnumerateFileSystemEntries(parent).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Then("the certificate folder is not inside the repository")]
    public void FolderIsNotInRepository()
    {
        // The repository is the first ancestor of the test output that holds .git (a folder, or a file in a worktree).
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Path.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("The test output is not inside a repository.");
        IsUnder(Folder, directory.FullName).ShouldBeFalse();
    }

    [Then("the identity service signs only with the generated signing certificate")]
    public void SignsOnlyWithGenerated() =>
        ShouldUseOnly(Options().SigningCredentials.Select(c => c.Key), TestHosts.SigningCertificatePath!);

    [Then("the identity service encrypts only with the generated encryption certificate")]
    public void EncryptsOnlyWithGenerated() =>
        ShouldUseOnly(Options().EncryptionCredentials.Select(c => c.Key), TestHosts.EncryptionCertificatePath!);

    private static OpenIddictServerOptions Options() =>
        TestHosts.AuthServices.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;

    /// <summary>
    /// The development certificates would show up here as extra keys with another thumbprint,
    /// so this checks directly that AddDevelopment*Certificate was not used.
    /// </summary>
    private static void ShouldUseOnly(IEnumerable<SecurityKey> keys, string generatedPath)
    {
        using var generated = ReadCertificate(generatedPath, TestHosts.CertificatePassword!);
        var all = keys.ToArray();
        all.ShouldNotBeEmpty();
        all.ShouldAllBe(k => k is X509SecurityKey);
        all.Cast<X509SecurityKey>().ShouldAllBe(k => k.Certificate.Thumbprint == generated.Thumbprint);
    }

    [Then("the certificate folder exists")]
    public void FolderExists() => Directory.Exists(Folder).ShouldBeTrue();

    [Then("the certificate folder is under the system temporary path")]
    public void FolderIsUnderTemp() =>
        IsUnder(Folder, Path.GetTempPath()).ShouldBeTrue();

    [Then("the certificate folder is outside the test output directory")]
    public void FolderIsOutsideOutput() =>
        IsUnder(Folder, AppContext.BaseDirectory).ShouldBeFalse();

    [Then("the certificate folder is not under the current directory")]
    public void FolderIsNotUnderCurrent() =>
        IsUnder(Folder, Directory.GetCurrentDirectory()).ShouldBeFalse();

    [Then("the certificate folder holds at least two pfx files")]
    public void FolderHoldsPfxFiles() =>
        Directory.GetFiles(Folder, "*.pfx").Length.ShouldBeGreaterThanOrEqualTo(2);

    [Then("the signing certificate file exists")]
    public void SigningFileExists() =>
        File.Exists(TestHosts.SigningCertificatePath.ShouldNotBeNull()).ShouldBeTrue();

    [Then("the published signing key is a self-signed certificate named {string}")]
    public async Task PublishedKeyIsGeneratedCertificate(string subject)
    {
        using var client = TestHosts.CreateAuthClient();

        using var discovery = JsonDocument.Parse(
            await client.GetStringAsync("/.well-known/openid-configuration"));
        var jwksUri = discovery.RootElement.GetProperty("jwks_uri").GetString().ShouldNotBeNull();
        using var jwks = JsonDocument.Parse(await client.GetStringAsync(new Uri(jwksUri).AbsolutePath));

        var key = jwks.RootElement.GetProperty("keys").EnumerateArray()
            .Single(k => k.GetProperty("use").GetString() == "sig");

        // Missing x5t/x5c means the key is not certificate-backed, which fails the check.
        key.TryGetProperty("x5t", out var x5t).ShouldBeTrue("The signing key has no x5t.");
        key.TryGetProperty("x5c", out var x5c).ShouldBeTrue("The signing key has no x5c.");

        using var published = X509CertificateLoader.LoadCertificate(
            Convert.FromBase64String(x5c[0].GetString()!));
        using var generated = ReadCertificate(TestHosts.SigningCertificatePath!, TestHosts.CertificatePassword!);

        Base64UrlTextEncoder.Decode(x5t.GetString()!).ShouldBe(generated.GetCertHash());
        published.Thumbprint.ShouldBe(generated.Thumbprint);
        published.Subject.ShouldBe(subject);
        published.Subject.ShouldBe(published.Issuer);
    }

    /// <summary>Reads only the certificate, so no private key is imported into any store.</summary>
    private static X509Certificate2 ReadCertificate(string path, string password)
    {
        var info = Pkcs12Info.Decode(File.ReadAllBytes(path), out _);
        foreach (var contents in info.AuthenticatedSafe)
        {
            if (contents.ConfidentialityMode == Pkcs12ConfidentialityMode.Password)
            {
                contents.Decrypt(password);
            }

            foreach (var bag in contents.GetBags().OfType<Pkcs12CertBag>())
            {
                return bag.GetCertificate();
            }
        }

        throw new InvalidOperationException("No certificate in " + path);
    }

    private static bool IsUnder(string path, string root)
    {
        var full = Path.GetFullPath(path);
        var parent = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(parent, StringComparison.Ordinal);
    }
}
