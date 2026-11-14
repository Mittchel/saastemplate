using System.Text.RegularExpressions;
using Reqnroll;

namespace Saas.Api.Tests.Steps;

[Binding]
public sealed class TestInfrastructureAcceptanceSteps
{
    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Saas.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repository root (Saas.slnx) not found.");
    }

    private static string ReadRepoFile(string relativePath)
    {
        var full = Path.Combine(FindRoot(), relativePath);
        if (!File.Exists(full))
        {
            throw new InvalidOperationException($"File not found: {relativePath}");
        }
        return File.ReadAllText(full);
    }

    private static string ReadTestProjectSources()
    {
        var root = Path.Combine(FindRoot(), "src", "Saas.Api.Tests");
        var parts = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !f.EndsWith(".acceptance.test.cs", StringComparison.Ordinal)
                && !f.EndsWith(".feature.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText);
        return string.Join("\n", parts);
    }

    [Then("the acceptance file {string} contains {string}")]
    public void ThenFileContains(string path, string text)
    {
        if (!ReadRepoFile(path).Contains(text, StringComparison.Ordinal))
        {
            throw new Exception($"{path} should contain '{text}'.");
        }
    }

    [Then("the acceptance file {string} does not contain {string}")]
    public void ThenFileDoesNotContain(string path, string text)
    {
        if (ReadRepoFile(path).Contains(text, StringComparison.Ordinal))
        {
            throw new Exception($"{path} should not contain '{text}'.");
        }
    }

    [Then("the acceptance file {string} matches {string}")]
    public void ThenFileMatches(string path, string pattern)
    {
        if (!Regex.IsMatch(ReadRepoFile(path), pattern))
        {
            throw new Exception($"{path} should match '{pattern}'.");
        }
    }

    [Then("the acceptance file {string} does not match {string}")]
    public void ThenFileDoesNotMatch(string path, string pattern)
    {
        if (Regex.IsMatch(ReadRepoFile(path), pattern))
        {
            throw new Exception($"{path} should not match '{pattern}'.");
        }
    }

    [Then("the acceptance test project sources contain {string}")]
    public void ThenSourcesContain(string text)
    {
        if (!ReadTestProjectSources().Contains(text, StringComparison.Ordinal))
        {
            throw new Exception($"Test project sources should contain '{text}'.");
        }
    }

    [Then("the acceptance test project sources do not contain {string}")]
    public void ThenSourcesDoNotContain(string text)
    {
        if (ReadTestProjectSources().Contains(text, StringComparison.Ordinal))
        {
            throw new Exception($"Test project sources should not contain '{text}'.");
        }
    }
}
