using System.Reflection;
using NUnit.Framework;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests;

[TestFixture]
public class TestInfrastructureRemovalAcceptanceTests
{
    private static readonly Assembly TestAssembly = typeof(ApiDriver).Assembly;

    private static readonly string[] RemovedMembers =
    [
        "FileContains", "FileDoesNotContain", "SourcesContain", "SourcesDoNotContain",
        "ReadFile", "TestSources", "FindRepositoryRoot",
    ];

    private static IEnumerable<Type> SafeTypes()
    {
        try
        {
            return TestAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Test]
    public void Criterion_1_no_TestInfrastructure_feature_class_remains_in_test_assembly()
    {
        SafeTypes()
            .Where(t => t.Name.Contains("TestInfrastructure", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ShouldBeEmpty();
    }

    [Test]
    public void Criterion_2_no_generated_code_behind_for_TestInfrastructure_feature()
    {
        SafeTypes()
            .Where(t => t.Namespace is not null
                && t.Namespace.EndsWith("Features", StringComparison.Ordinal)
                && t.Name.StartsWith("TestInfrastructure", StringComparison.Ordinal))
            .ShouldBeEmpty();
    }

    [Test]
    public void Criterion_3_TestInfrastructureSteps_and_its_helpers_are_gone()
    {
        SafeTypes().Any(t => t.Name == "TestInfrastructureSteps").ShouldBeFalse();

        var found = SafeTypes()
            .SelectMany(t => t.GetMembers(AllMembers).Select(m => $"{t.Name}.{m.Name}"))
            .Where(n => RemovedMembers.Contains(n[(n.IndexOf('.') + 1)..]))
            .ToList();
        found.ShouldBeEmpty();
    }

    [Test]
    public void Criterion_4_no_step_or_driver_exposes_source_text_checks()
    {
        var found = SafeTypes()
            .SelectMany(t => t.GetMembers(AllMembers).Select(m => $"{t.Name}.{m.Name}"))
            .Where(n => n.Contains("SourcesContain", StringComparison.Ordinal)
                || n.Contains("FileContains", StringComparison.Ordinal)
                || n.Contains("FileDoesNotContain", StringComparison.Ordinal)
                || n.Contains("FindRepositoryRoot", StringComparison.Ordinal))
            .ToList();
        found.ShouldBeEmpty();
    }

    [Test]
    public void Criterion_5_remaining_drivers_and_steps_still_build_and_load()
    {
        SafeTypes().Any(t => t.Name == "OrganizationSteps").ShouldBeTrue();
        SafeTypes().Any(t => t.Name == "TenantIsolationSteps").ShouldBeTrue();
        SafeTypes().Any(t => t.Name == "TestHosts").ShouldBeTrue();
        SafeTypes().Any(t => t.Name == "TestSettings").ShouldBeTrue();
    }

    [Test]
    public void Criterion_6_infrastructure_classes_are_unchanged_in_presence()
    {
        SafeTypes().Any(t => t.Name == "TestHosts").ShouldBeTrue();
        SafeTypes().Any(t => t.Name == "TestSettings").ShouldBeTrue();
        SafeTypes().Any(t => t.Name == "ApiDriver").ShouldBeTrue();
    }
}
