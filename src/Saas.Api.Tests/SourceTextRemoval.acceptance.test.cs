using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace Saas.Api.Tests;

[TestFixture]
public class SourceTextRemovalAcceptanceTests
{
    private static readonly string[] _removedStepMethods =
    [
        "FileContains", "FileDoesNotContain", "SourcesContain", "SourcesDoNotContain",
    ];

    private static string? FindRoot()
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

        return null;
    }

    private static Type[] LoadTypes()
    {
        try
        {
            return typeof(SourceTextRemovalAcceptanceTests).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Select(t => t!).ToArray();
        }
    }

    [Test]
    public void Criterion_1_TestInfrastructure_feature_file_is_gone()
    {
        var root = FindRoot();
        Assert.That(root, Is.Not.Null);
        File.Exists(Path.Combine(root!, "src", "Saas.Api.Tests", "Features", "TestInfrastructure.acceptance.feature"))
            .ShouldBeFalse();
    }

    [Test]
    public void Criterion_2_generated_feature_code_is_gone()
    {
        var root = FindRoot();
        Assert.That(root, Is.Not.Null);
        File.Exists(Path.Combine(root!, "src", "Saas.Api.Tests", "Features", "TestInfrastructure.acceptance.feature.cs"))
            .ShouldBeFalse();
        LoadTypes().Where(t => t.Name.Contains("TestInfrastructureFeature", StringComparison.Ordinal))
            .ShouldBeEmpty();
    }

    [Test]
    public void Criterion_3_TestInfrastructureSteps_is_deleted()
    {
        var root = FindRoot();
        Assert.That(root, Is.Not.Null);
        File.Exists(Path.Combine(root!, "src", "Saas.Api.Tests", "Steps", "TestInfrastructureSteps.cs"))
            .ShouldBeFalse();
        LoadTypes().Where(t => t.Name == "TestInfrastructureSteps").ShouldBeEmpty();
    }

    [Test]
    public void Criterion_4_no_source_text_step_methods_remain()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var found = LoadTypes()
            .SelectMany(t => t.GetMethods(all))
            .Where(m => _removedStepMethods.Contains(m.Name))
            .Select(m => m.DeclaringType?.Name + "." + m.Name)
            .ToArray();
        found.ShouldBeEmpty();
    }
}
