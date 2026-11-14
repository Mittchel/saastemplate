using Reqnroll;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>Source-text checks for the generated test certificates (DIA-134).</summary>
[Binding]
public class TestCertificatesAcceptanceSteps
{
    private static readonly string[] _certificateSources =
    [
        "src/Saas.Api.Tests/Drivers/TestHosts.cs",
        "src/Saas.Api.Tests/Drivers/TestCertificates.cs",
    ];

    [Then("the certificate source file {string} exists")]
    public void SourceFileExists(string path) =>
        File.Exists(Path.Combine(FindRoot(), path)).ShouldBeTrue($"{path} should exist");

    [Then("the certificate sources together contain {string}")]
    public void SourcesTogetherContain(string text)
    {
        var root = FindRoot();
        _certificateSources
            .Select(path => Path.Combine(root, path))
            .Where(File.Exists)
            .Any(file => File.ReadAllText(file).Contains(text, StringComparison.Ordinal))
            .ShouldBeTrue($"TestHosts.cs / TestCertificates.cs should contain \"{text}\"");
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
