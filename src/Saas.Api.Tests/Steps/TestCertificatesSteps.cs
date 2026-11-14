using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

[Binding]
public class TestCertificatesSteps
{
    private static string Folder => TestHosts.CertificateFolder.ShouldNotBeNull();

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
        // macOS temp lives under /var, a symlink to /private/var; compare resolved paths too.
        return full.StartsWith(parent, StringComparison.Ordinal)
            || (Directory.ResolveLinkTarget(full, true)?.FullName ?? full)
                .StartsWith(Directory.ResolveLinkTarget(parent, true)?.FullName ?? parent, StringComparison.Ordinal);
    }
}
