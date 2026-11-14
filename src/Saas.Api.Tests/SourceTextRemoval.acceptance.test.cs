using System.Reflection;
using NUnit.Framework;

namespace Saas.Api.Tests;

[TestFixture]
public class SourceTextRemovalAcceptanceTests
{
    private static readonly string[] RemovedMembers =
    [
        "FileContains", "FileDoesNotContain", "SourcesContain", "SourcesDoNotContain",
        "ReadFile", "TestSources", "FindRepositoryRoot",
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
    public void Criterion_1_infrastructure_feature_file_is_gone()
    {
        var root = FindRoot();
        Assert.That(root, Is.Not.Null);
        var path = Path.Combine(root!, "src", "Saas.Api.Tests", "Features", "TestInfrastructure.acceptance.feature");
        Assert.That(File.Exists(path), Is.False);
    }

    [Test]
    public void Criterion_2_generated_feature_code_is_gone()
    {
        var root = FindRoot();
        Assert.That(root, Is.Not.Null);
        var path = Path.Combine(root!, "src", "Saas.Api.Tests", "Features", "TestInfrastructure.acceptance.feature.cs");
        Assert.That(File.Exists(path), Is.False);

        var leftovers = LoadTypes()
            .Where(t => t.Name.StartsWith("TestInfrastructure") && t.Name != "TestInfrastructureSteps")
            .Select(t => t.FullName)
            .ToArray();
        Assert.That(leftovers, Is.Empty);
    }

    [Test]
    public void Criterion_3_infrastructure_steps_are_gone()
    {
        var root = FindRoot();
        Assert.That(root, Is.Not.Null);
        var path = Path.Combine(root!, "src", "Saas.Api.Tests", "Steps", "TestInfrastructureSteps.cs");
        Assert.That(File.Exists(path), Is.False);
        Assert.That(LoadTypes().Any(t => t.Name == "TestInfrastructureSteps"), Is.False);
    }

    [Test]
    public void Criterion_4_no_source_text_check_members_remain()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var found = LoadTypes()
            .Where(t => t != typeof(SourceTextRemovalAcceptanceTests))
            .SelectMany(t => t.GetMethods(all).Select(m => t.Name + "." + m.Name)
                .Concat(t.GetProperties(all).Select(p => t.Name + "." + p.Name)))
            .Where(n => RemovedMembers.Any(r => n.EndsWith("." + r, StringComparison.Ordinal)))
            .ToArray();
        Assert.That(found, Is.Empty);
    }

    [Test]
    public void Criterion_5_remaining_steps_and_drivers_still_present()
    {
        var names = LoadTypes().Select(t => t.Name).ToArray();
        Assert.That(names, Does.Contain("OrganizationSteps"));
        Assert.That(names, Does.Contain("AuthenticationSteps"));
        Assert.That(names, Does.Contain("TenantIsolationSteps"));
        Assert.That(names, Does.Contain("PlatformOperatorSteps"));
    }

    [Test]
    public void Criterion_6_infrastructure_files_remain()
    {
        var root = FindRoot();
        Assert.That(root, Is.Not.Null);
        var dir = Path.Combine(root!, "src", "Saas.Api.Tests", "Drivers");
        Assert.That(File.Exists(Path.Combine(dir, "TestHosts.cs")), Is.True);
        Assert.That(File.Exists(Path.Combine(dir, "TestSettings.cs")), Is.True);
    }
}
