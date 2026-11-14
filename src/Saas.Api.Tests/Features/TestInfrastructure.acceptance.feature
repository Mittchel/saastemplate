Feature: Test infrastructure (DIA-128, MVP-02 / RB-ACCESS-01)

  Scenario: criterion 1: Postgres runs via Testcontainers with the compose image
    Then the infra file "src/Saas.Api.Tests/Saas.Api.Tests.csproj" contains "Testcontainers.PostgreSql"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "pgvector/pgvector:pg17"

  Scenario: criterion 2: logins are created by the init script
    Then the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "00-init.sh"
    And the infra test sources do not contain "CREATE ROLE"

  Scenario: criterion 3: tests no longer depend on the compose stack
    Then the infra test sources do not contain "localhost:5001"
    And the infra test sources do not contain "localhost:5002"

  Scenario: criterion 4: hosts are in-process in Development
    Then the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "WebApplicationFactory"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "Development"

  Scenario: criterion 5: host configuration
    Then the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "saas_owner"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "saas_app"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "Platform:AdministratorEmail"

  Scenario: criterion 6: both hosts are started so hosted services run
    Then the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "WebApplicationFactory<ApiEntryPoint>"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "WebApplicationFactory<AuthEntryPoint>"

  Scenario: criterion 7: drivers use the in-process hosts and TestSettings
    Then the infra file "src/Saas.Api.Tests/Drivers/ApiDriver.cs" does not contain "localhost:5002"
    And the infra file "src/Saas.Api.Tests/Drivers/AuthDriver.cs" does not contain "localhost:5001"
    And the infra file "src/Saas.Api.Tests/Drivers/TestSettings.cs" contains "saas_ai"

  Scenario: criterion 8: row-level security uses real Postgres
    Then the infra test sources do not contain "UseInMemoryDatabase"
    And the infra test sources do not contain "UseSqlite"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "saas_app"

  Scenario: criterion 9: tokens resolve against the in-process identity host
    Then the infra test sources do not contain "localhost:5001"
    And the infra test sources do not contain "localhost:5002"

  Scenario: criterion 10: Program classes are distinguishable and the AI service is not hosted
    Then the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "WebApplicationFactory<ApiEntryPoint>"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "WebApplicationFactory<AuthEntryPoint>"

  Scenario: criterion 11: CI needs only Docker
    Then the infra file ".github/workflows/ci.yml" does not contain "docker compose up"

  Scenario: criterion 12: remarks describe Testcontainers and in-process hosts
    Then the infra file "src/Saas.Api.Tests/Drivers/TestSettings.cs" contains "Testcontainers"
    And the infra file "CLAUDE.md" contains "Testcontainers"
    And the infra file "CLAUDE.md" contains "Docker"
