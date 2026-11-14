using System.Buffers.Text;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

[Binding]
public class TestCertificatesAcceptanceSteps
{
    private static string? _folderDuringRun;

    [BeforeTestRun(Order = 20000)]
    public static void CaptureCertificateFolder() => _folderDuringRun = TestHosts.CertificateFolder;

    // Runs after DriverHooks.StopHosts (default order is lower).
    [AfterTestRun(Order = 20000)]
    public static void CertificateFolderIsGone()
    {
        _folderDuringRun.ShouldNotBeNullOrWhiteSpace();
        Directory.Exists(_folderDuringRun).ShouldBeFalse(
            $"the certificate folder {_folderDuringRun} must be removed when the run ends");
    }

    [Then("the generated certificate folder exists")]
    public void FolderExists() => Directory.Exists(TestHosts.CertificateFolder).ShouldBeTrue();

    [Then("the generated certificate folder is under the temporary path")]
    public void FolderUnderTemp()
    {
        var temp = Path.GetFullPath(Path.GetTempPath());
        Path.GetFullPath(TestHosts.CertificateFolder).StartsWith(temp, StringComparison.Ordinal).ShouldBeTrue();
    }

    [Then("the generated certificate folder holds the signing pfx and at least two pfx files")]
    public void FolderHoldsPfx()
    {
        File.Exists(TestHosts.SigningCertificatePath).ShouldBeTrue();
        TestHosts.SigningCertificatePath.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        Path.GetFullPath(Path.GetDirectoryName(TestHosts.SigningCertificatePath)!)
            .TrimEnd(Path.DirectorySeparatorChar)
            .ShouldBe(Path.GetFullPath(TestHosts.CertificateFolder).TrimEnd(Path.DirectorySeparatorChar));
        Directory.GetFiles(TestHosts.CertificateFolder, "*.pfx").Length.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Then("the published signing keys are the generated self-signed certificate")]
    public async Task JwksKeyIsSelfSignedCertificate()
    {
        using var client = TestHosts.CreateAuthClient();

        using var discovery = JsonDocument.Parse(
            await client.GetStringAsync("/.well-known/openid-configuration"));
        var jwksUri = new Uri(discovery.RootElement.GetProperty("jwks_uri").GetString()!);

        using var jwks = JsonDocument.Parse(await client.GetStringAsync(jwksUri.AbsolutePath));
        var keys = jwks.RootElement.GetProperty("keys").EnumerateArray().ToList();

        keys.ShouldNotBeEmpty();

        foreach (var key in keys)
        {
            key.TryGetProperty("x5c", out var x5c).ShouldBeTrue("the JWKS key must carry x5c");
            key.TryGetProperty("x5t", out var x5t).ShouldBeTrue("the JWKS key must carry x5t");

            var der = Convert.FromBase64String(x5c[0].GetString()!);
            using var certificate = X509CertificateLoader.LoadCertificate(der);

            certificate.Subject.ShouldBe("CN=saas-test-signing");
            certificate.Subject.ShouldBe(certificate.Issuer);
            Base64Url.EncodeToString(certificate.GetCertHash()).ShouldBe(x5t.GetString());
        }
    }

    [Then("the generated certificate folder is checked for removal after the run")]
    public void FolderCheckedAfterRun()
    {
        // The AfterTestRun hook in this class asserts the removal; here the folder must exist now.
        Directory.Exists(TestHosts.CertificateFolder).ShouldBeTrue();
        _folderDuringRun.ShouldBe(TestHosts.CertificateFolder);
    }

    [Then("the generated certificate folder is outside the repository")]
    public void FolderOutsideRepo()
    {
        var root = FindRepositoryRoot();
        Path.GetFullPath(TestHosts.CertificateFolder)
            .StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Then("no pfx file exists in the repository tree")]
    public void NoPfxInRepo()
    {
        var found = EnumerateRepositoryFiles("*.pfx").ToList();

        found.ShouldBeEmpty();
    }

    [Then("no compose or appsettings file mentions the test certificates")]
    public void ConfigUnchanged()
    {
        var files = EnumerateRepositoryFiles("*")
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return (name.StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("compose", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
                    && (name.EndsWith(".yml") || name.EndsWith(".yaml") || name.EndsWith(".json"));
            });

        foreach (var file in files)
        {
            File.ReadAllText(file).ShouldNotContain("saas-test-signing", Case.Insensitive, file);
        }
    }

    [Then("the Testing section of CLAUDE.md says tests need no certificate store or keychain")]
    public void ClaudeMdTestingSection()
    {
        var lines = File.ReadAllLines(Path.Combine(FindRepositoryRoot(), "CLAUDE.md"));
        var start = Array.FindIndex(lines, l => l.TrimStart().StartsWith('#') && l.Contains("Testing", StringComparison.OrdinalIgnoreCase));
        start.ShouldBeGreaterThanOrEqualTo(0, "CLAUDE.md must have a Testing section");

        var level = lines[start].TakeWhile(c => c == '#').Count();
        var section = new List<string>();

        for (var i = start + 1; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith('#') && trimmed.TakeWhile(c => c == '#').Count() <= level)
            {
                break;
            }

            section.Add(lines[i]);
        }

        var text = string.Join('\n', section);
        text.ShouldContain("certificate", Case.Insensitive);
        text.ShouldContain("keychain", Case.Insensitive);
    }

    private static IEnumerable<string> EnumerateRepositoryFiles(string pattern)
    {
        var sep = Path.DirectorySeparatorChar;

        return Directory.EnumerateFiles(FindRepositoryRoot(), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}node_modules{sep}")
                && !f.Contains($"{sep}bin{sep}")
                && !f.Contains($"{sep}obj{sep}")
                && !f.Contains($"{sep}.git{sep}"));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Saas.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root (Saas.slnx) not found.");
    }
}
