using Shouldly;

namespace Saas.Api.Tests;

[TestFixture]
public class GeneratedFeatureLineEndingsProbe
{
    private static string FindDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Saas.Api.Tests.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("project dir not found");
    }

    [TestCase("Authentication")]
    [TestCase("Organizations")]
    [TestCase("PlatformOperator")]
    public void Remaining_generated_feature_code_is_unchanged_by_the_removal(string feature)
    {
        var text = File.ReadAllText(Path.Combine(FindDir(), "Features", feature + ".feature.cs"));
        text.Contains("\\r\\n", StringComparison.Ordinal).ShouldBeFalse();
    }
}
