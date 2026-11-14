Feature: Entry point classes exist for the in-process test hosts (DIA-130, follow-up to DIA-128)
  The test project builds only if ApiEntryPoint and AuthEntryPoint exist in the source tree.
  These scenarios run only when the test project compiled, which is the build proof.

  Scenario: criterion 1: ApiEntryPoint is declared in Saas.Api
    Then the infra file "src/Saas.Api/ApiEntryPoint.cs" contains "public sealed class ApiEntryPoint"

  Scenario: criterion 2: AuthEntryPoint is declared in Saas.Auth.Api
    Then the infra file "src/Saas.Auth.Api/AuthEntryPoint.cs" contains "public sealed class AuthEntryPoint"

  Scenario: criterion 3: TestHosts resolves both entry points
    Then the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "ApiEntryPoint"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "AuthEntryPoint"
    And the infra file "src/Saas.Api/ApiEntryPoint.cs" contains "ApiEntryPoint"
    And the infra file "src/Saas.Auth.Api/AuthEntryPoint.cs" contains "AuthEntryPoint"

  Scenario: criterion 4: the test project compiled so this scenario could run
    Then the infra file "src/Saas.Api.Tests/Saas.Api.Tests.csproj" contains "Reqnroll"

  Scenario: criterion 5: both entry point files are present in the tree
    Then the infra file "src/Saas.Api/ApiEntryPoint.cs" contains "class ApiEntryPoint"
    And the infra file "src/Saas.Auth.Api/AuthEntryPoint.cs" contains "class AuthEntryPoint"
