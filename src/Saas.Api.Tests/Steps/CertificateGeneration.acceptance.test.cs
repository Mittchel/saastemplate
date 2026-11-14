using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>Acceptance checks for DIA-134: test hosts generate their own certificates.</summary>
[Binding]
public class CertificateGenerationAcceptanceSteps
{
    private static readonly Lazy<string> _root = new(FindRoot);

    [Then("the cert source file {string} contains {string}")]
    public void SourceFileContains(string path, string text) =>
        File.ReadAllText(Path.Combine(_root.Value, path))
            .ShouldContain(text, Case.Sensitive, $"{path} should contain \"{text}\"");

    [Then("the repository holds no pfx files outside build output")]
    public void NoPfxInRepository()
    {
        var separator = Path.DirectorySeparatorChar;
        var found = Directory.EnumerateFiles(_root.Value, "*.pfx", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{separator}obj{separator}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{separator}node_modules{separator}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{separator}.git{separator}", StringComparison.Ordinal));
        found.ShouldBeEmpty();
    }

    [Then("the identity host publishes a signing key backed by a self-signed certificate")]
    public async Task JwksKeyIsSelfSignedCertificate()
    {
        using var client = TestHosts.CreateAuthClient();

        using var discovery = await client.GetAsync("/.well-known/openid-configuration");
        discovery.IsSuccessStatusCode.ShouldBeTrue();
        using var discoveryDoc = JsonDocument.Parse(await discovery.Content.ReadAsStringAsync());
        var jwksUri = new Uri(discoveryDoc.RootElement.GetProperty("jwks_uri").GetString()!);

        using var jwks = await client.GetAsync(jwksUri.PathAndQuery);
        jwks.IsSuccessStatusCode.ShouldBeTrue();
        using var jwksDoc = JsonDocument.Parse(await jwks.Content.ReadAsStringAsync());
        var keys = jwksDoc.RootElement.GetProperty("keys").EnumerateArray().ToList();
        keys.ShouldNotBeEmpty();

        var key = keys[0];
        key.TryGetProperty("x5c", out var x5c).ShouldBeTrue("the signing key should carry its certificate (x5c)");
        var der = Convert.FromBase64String(x5c[0].GetString()!);
        using var certificate = X509CertificateLoader.LoadCertificate(der);

        certificate.Subject.ShouldBe(certificate.Issuer);
        certificate.GetRSAPublicKey()!.KeySize.ShouldBe(2048);
    }

    private static string FindRoot()
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
