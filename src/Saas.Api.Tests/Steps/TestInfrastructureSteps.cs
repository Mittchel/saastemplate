using Reqnroll;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>
/// Source-text checks for the test infrastructure (MVP-02 / RB-ACCESS-01): the
/// tests use Testcontainers and in-process hosts, not the compose stack.
/// </summary>
[Binding]
public class TestInfrastructureSteps
{
    private static readonly Lazy<string> _repositoryRoot = new(FindRepositoryRoot);

    [Then("the infra file {string} contains {string}")]
    public void FileContains(string path, string text) =>
        ReadFile(path).ShouldContain(text, Case.Sensitive, $"{path} should contain \"{text}\"");

    [Then("the infra file {string} does not contain {string}")]
    public void FileDoesNotContain(string path, string text) =>
        ReadFile(path).ShouldNotContain(text, Case.Sensitive, $"{path} should not contain \"{text}\"");

    [Then("the infra test sources contain {string}")]
    public void SourcesContain(string text) =>
        TestSources().Any(file => File.ReadAllText(file).Contains(text, StringComparison.Ordinal))
            .ShouldBeTrue($"no test source contains \"{text}\"");

    [Then("the infra test sources do not contain {string}")]
    public void SourcesDoNotContain(string text) =>
        TestSources()
            .Where(file => File.ReadAllText(file).Contains(text, StringComparison.Ordinal))
            .ShouldBeEmpty($"test sources should not contain \"{text}\"");

    private static string ReadFile(string path) =>
        File.ReadAllText(Path.Combine(_repositoryRoot.Value, path));

    /// <summary>The project's hand-written C#: no bin/, obj/ or generated feature code.</summary>
    private static IEnumerable<string> TestSources()
    {
        var project = Path.Combine(_repositoryRoot.Value, "src", "Saas.Api.Tests");
        var separator = Path.DirectorySeparatorChar;

        return Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith(".feature.cs", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal))
            // This file names the forbidden strings it checks for.
            .Where(file => !file.EndsWith("TestInfrastructureSteps.cs", StringComparison.Ordinal));
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
