using System.Reflection;
using Reqnroll;
using Shouldly;

namespace Saas.Api.Tests.Steps;

[Binding]
public sealed class SourceTextRemovalAcceptanceSteps
{
    private string? _projectDir;

    [Given(@"the test project folder is located")]
    public void GivenTheTestProjectFolderIsLocated()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Saas.Api.Tests.csproj")))
            {
                _projectDir = dir.FullName;
                return;
            }

            var nested = Path.Combine(dir.FullName, "src", "Saas.Api.Tests", "Saas.Api.Tests.csproj");
            if (File.Exists(nested))
            {
                _projectDir = Path.GetDirectoryName(nested);
                return;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate Saas.Api.Tests project folder.");
    }

    [Then(@"the file ""(.*)"" does not exist in the test project")]
    public void ThenTheFileDoesNotExistInTheTestProject(string relativePath)
    {
        _projectDir.ShouldNotBeNull();
        File.Exists(Path.Combine(_projectDir, relativePath)).ShouldBeFalse();
    }

    [Then(@"the file ""(.*)"" exists in the test project")]
    public void ThenTheFileExistsInTheTestProject(string relativePath)
    {
        _projectDir.ShouldNotBeNull();
        File.Exists(Path.Combine(_projectDir, relativePath)).ShouldBeTrue();
    }

    [Then(@"the compiled test assembly has no type named ""(.*)""")]
    public void ThenTheAssemblyHasNoTypeNamed(string typeName)
    {
        Type[] types;
        try
        {
            types = typeof(SourceTextRemovalAcceptanceSteps).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Select(t => t!).ToArray();
        }

        types.Select(t => t.Name).ShouldNotContain(typeName);
    }
}
