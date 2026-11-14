using Reqnroll;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>
/// Checks the test infrastructure itself (DIA-128, MVP-02, RB-ACCESS-01) by reading
/// repository files, since "uses Testcontainers" has no HTTP behaviour to observe.
/// </summary>
[Binding]
public class TestInfrastructureSteps
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Then("the infra file {string} contains {string}")]
    public void ThenTheInfraFileContains(string path, string text) =>
        ReadFile(path).ShouldContain(text);

    [Then("the infra file {string} does not contain {string}")]
    public void ThenTheInfraFileDoesNotContain(string path, string text) =>
        ReadFile(path).ShouldNotContain(text);

    [Then("the infra test sources contain {string}")]
    public void ThenTheInfraTestSourcesContain(string text) =>
        TestSources().ShouldContain(source => source.Contains(text, StringComparison.Ordinal));

    [Then("the infra test sources do not contain {string}")]
    public void ThenTheInfraTestSourcesDoNotContain(string text) =>
        TestSources().ShouldNotContain(source => source.Contains(text, StringComparison.Ordinal));

    private static string ReadFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

    /// <summary>Hand-written test sources: no bin/obj, no generated feature code.</summary>
    private static IEnumerable<string> TestSources()
    {
        var project = Path.Combine(RepositoryRoot, "src", "Saas.Api.Tests");
        var separator = Path.DirectorySeparatorChar;

        return Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith(".feature.cs", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal))
            .Where(file => !file.EndsWith("TestInfrastructureSteps.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToList();
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
