using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace Saas.Api.Tests;

[TestFixture]
public class SourceTextChecksRemovalAcceptanceTests
{
    private static readonly string Removed = "TestInfra" + "structure";

    private static IEnumerable<Type> SafeTypes()
    {
        Type[] types;
        try
        {
            types = Assembly.GetExecutingAssembly().GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        return types.Where(t => t != typeof(SourceTextChecksRemovalAcceptanceTests));
    }

    private static string? FindTestProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Saas.Api.Tests.csproj");
            if (File.Exists(candidate))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    [Test]
    public void Criterion_1_feature_file_no_longer_exists()
    {
        var dir = FindTestProjectDir();
        if (dir is null)
        {
            Assert.Ignore("Test project directory not found from the build output.");
        }

        File.Exists(Path.Combine(dir!, "Features", Removed + ".acceptance.feature")).ShouldBeFalse();
    }

    [Test]
    public void Criterion_1_no_removed_feature_class_remains_in_test_assembly()
    {
        SafeTypes()
            .Where(t => t.Name.Contains(Removed, StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ShouldBeEmpty();
    }

    [Test]
    public void Criterion_2_generated_code_behind_is_removed()
    {
        var dir = FindTestProjectDir();
        if (dir is null)
        {
            Assert.Ignore("Test project directory not found from the build output.");
        }

        File.Exists(Path.Combine(dir!, "Features", Removed + ".acceptance.feature.cs")).ShouldBeFalse();
    }

    [Test]
    public void Criterion_3_steps_file_is_deleted()
    {
        var dir = FindTestProjectDir();
        if (dir is null)
        {
            Assert.Ignore("Test project directory not found from the build output.");
        }

        File.Exists(Path.Combine(dir!, "Steps", Removed + "Steps.cs")).ShouldBeFalse();
    }

    [Test]
    public void Criterion_3_source_text_steps_and_helpers_are_gone()
    {
        var names = new[]
        {
            "FileContains", "FileDoesNotContain", "SourcesContain", "SourcesDoNotContain",
            "ReadFile", "TestSources", "FindRepositoryRoot",
        };
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var found = SafeTypes()
            .SelectMany(t => t.GetMembers(all).Select(m => t.FullName + "." + m.Name))
            .Where(n => names.Any(x => n.EndsWith("." + x, StringComparison.Ordinal)))
            .ToList();

        found.ShouldBeEmpty();
    }

    [Test]
    public void Criterion_5_remaining_steps_and_drivers_build_and_load()
    {
        var names = SafeTypes().Select(t => t.Name).ToList();
        names.ShouldContain("OrganizationSteps");
        names.ShouldContain("TenantIsolationSteps");
        names.ShouldContain("AuthenticationSteps");
        names.ShouldContain("PlatformOperatorSteps");
        names.ShouldContain("ApiDriver");
    }

    [Test]
    public void Criterion_6_hosts_and_settings_are_unchanged_in_presence()
    {
        var names = SafeTypes().Select(t => t.Name).ToList();
        names.ShouldContain("TestHosts");
        names.ShouldContain("TestSettings");
    }

    [Test]
    public void Criterion_4_no_remaining_step_class_is_a_source_text_step_class()
    {
        SafeTypes()
            .Where(t => t.Name.Contains("SourceText", StringComparison.Ordinal)
                || t.Name.Contains(Removed, StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ShouldBeEmpty();
    }
}
