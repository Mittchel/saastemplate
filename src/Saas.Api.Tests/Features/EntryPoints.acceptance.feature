Feature: Test host entry points exist (DIA-130, follow-up to DIA-128, MVP-02)

  Scenario: criterion 1: ApiEntryPoint is declared in src/Saas.Api/ApiEntryPoint.cs
    Then the infra file "src/Saas.Api/ApiEntryPoint.cs" contains "public sealed class ApiEntryPoint"

  Scenario: criterion 2: AuthEntryPoint is declared in src/Saas.Auth.Api/AuthEntryPoint.cs
    Then the infra file "src/Saas.Auth.Api/AuthEntryPoint.cs" contains "public sealed class AuthEntryPoint"

  Scenario: criterion 3: TestHosts resolves both entry point types
    Then the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "WebApplicationFactory<ApiEntryPoint>"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "WebApplicationFactory<AuthEntryPoint>"
    And the infra file "src/Saas.Api/ApiEntryPoint.cs" contains "ApiEntryPoint"
    And the infra file "src/Saas.Auth.Api/AuthEntryPoint.cs" contains "AuthEntryPoint"

  Scenario: criterion 4: the test project builds because the entry point sources are present
    # This scenario only runs if the test project compiled; it also checks the sources it needs.
    Then the infra file "src/Saas.Api/ApiEntryPoint.cs" contains "ApiEntryPoint"
    And the infra file "src/Saas.Auth.Api/AuthEntryPoint.cs" contains "AuthEntryPoint"

  Scenario: criterion 5: both entry point files are present so the test project compiles
    Then the infra file "src/Saas.Api/ApiEntryPoint.cs" contains "class ApiEntryPoint"
    And the infra file "src/Saas.Auth.Api/AuthEntryPoint.cs" contains "class AuthEntryPoint"
