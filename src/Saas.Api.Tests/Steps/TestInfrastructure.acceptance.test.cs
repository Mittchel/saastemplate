using System.Text.RegularExpressions;
using Reqnroll;

namespace Saas.Api.Tests.Steps;

[Binding]
public sealed class TestInfrastructureAcceptanceSteps
{
    [Then("^the infra file \"(.*)\" contains \"(.*)\"$")]
    public void ThenInfraFileContains(string path, string text)
    {
        var content = File.ReadAllText(Resolve(path));
        if (!content.Contains(text, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected {path} to contain '{text}'.");
        }
    }

    [Then("^the infra file \"(.*)\" does not contain \"(.*)\"$")]
    public void ThenInfraFileDoesNotContain(string path, string text)
    {
        var content = File.ReadAllText(Resolve(path));
        if (content.Contains(text, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected {path} not to contain '{text}'.");
        }
    }

    [Then("^the infra test sources contain \"(.*)\"$")]
    public void ThenInfraSourcesContain(string text)
    {
        if (!Sources().Any(f => File.ReadAllText(f).Contains(text, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Expected test sources to contain '{text}'.");
        }
    }

    [Then("^the infra test sources do not contain \"(.*)\"$")]
    public void ThenInfraSourcesDoNotContain(string text)
    {
        var hit = Sources().FirstOrDefault(f => File.ReadAllText(f).Contains(text, StringComparison.Ordinal));
        if (hit is not null)
        {
            throw new InvalidOperationException($"Test source {hit} must not contain '{text}'.");
        }
    }

    private static string Root()
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

        throw new InvalidOperationException("Saas.slnx not found above " + AppContext.BaseDirectory);
    }

    private static string Resolve(string relative)
    {
        return Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static IEnumerable<string> Sources()
    {
        var baseDir = Resolve("src/Saas.Api.Tests");
        var sep = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(baseDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}bin{sep}") && !f.Contains($"{sep}obj{sep}"))
            .Where(f => !f.EndsWith(".feature.cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Regex.IsMatch(Path.GetFileName(f), @"\.acceptance\.test\.cs$"));
    }
}
