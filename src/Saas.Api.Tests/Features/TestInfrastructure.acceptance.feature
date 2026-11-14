Feature: Integration test infrastructure (DIA-128, MVP-02 / RB-ACCESS-01)
  Integration tests run in-process against a Testcontainers Postgres.

  Scenario: criterion 1: one Postgres container from the compose image via Testcontainers
    Then the infra test project file contains "Testcontainers.PostgreSql"
    And the infra test sources contain "pgvector/pgvector:pg17"
    And the infra test sources contain "PostgreSqlBuilder"

  Scenario: criterion 2: logins are created by running 00-init.sh in the container
    Then the infra test sources contain "00-init.sh"
    And the infra file "src/Saas.Api.Tests/Drivers/TestHosts.cs" does not contain "CREATE ROLE"

  Scenario: criterion 3: containers are removed when the run ends
    Then the infra test sources contain "DisposeAsync"

  Scenario: criterion 4: hosts are in-process WebApplicationFactory in Development
    Then the infra test project file contains "Microsoft.AspNetCore.Mvc.Testing"
    And the infra test sources contain "WebApplicationFactory<"
    And the infra test sources contain "Development"
    And the infra web application factories use two distinct entry points

  Scenario: criterion 5: host configuration
    Then the infra test sources contain "saas_owner"
    And the infra test sources contain "saas_app"
    And the infra test sources contain "Platform:AdministratorEmail"

  Scenario: criterion 6: hosted services are not removed
    Then the infra test sources do not contain "RemoveAll<IHostedService>"
    And the infra test sources do not contain "RemoveAll(typeof(IHostedService))"

  Scenario: criterion 7: drivers use in-process hosts and TestSettings
    Then the infra file "src/Saas.Api.Tests/Drivers/ApiDriver.cs" does not contain "localhost:5002"
    And the infra file "src/Saas.Api.Tests/Drivers/AuthDriver.cs" does not contain "localhost:5001"
    And the infra file "src/Saas.Api.Tests/Drivers/TestSettings.cs" does not contain "localhost:5432"

  Scenario: criterion 8: real Postgres, no in-memory or SQLite
    Then the infra test project file does not contain "Sqlite"
    And the infra test project file does not contain "InMemory"
    And the infra test sources do not contain "UseInMemoryDatabase"
    And the infra test sources do not contain "UseSqlite"
    And the infra test sources contain "saas_ai"

  Scenario: criterion 9: no requests to localhost:5001 or localhost:5002
    Then the infra test sources do not contain "localhost:5001"
    And the infra test sources do not contain "localhost:5002"

  Scenario: criterion 10: two hosts distinguishable and saas-ai not hosted
    Then the infra web application factories use two distinct entry points
    And the infra test sources do not contain "saas-ai:"

  Scenario: criterion 11: CI needs only Docker
    Then the infra file ".github/workflows/ci.yml" does not contain "docker compose up"
    And the infra file ".github/workflows/ci.yml" does not contain "docker-compose up"
    And the infra file ".github/workflows/ci.yml" does not contain "dev-certs"
    And the infra file ".github/workflows/ci.yml" does not contain "cp .env"

  Scenario: criterion 12: remarks describe Testcontainers and in-process hosts
    Then the infra file "src/Saas.Api.Tests/Drivers/TestSettings.cs" contains "Testcontainers"
    And the infra file "src/Saas.Api.Tests/Drivers/TestSettings.cs" contains "Docker"
    And the infra file "src/Saas.Api.Tests/Drivers/TestSettings.cs" contains "in-process" ignoring case
    And the infra file "CLAUDE.md" contains "Testcontainers"
    And the infra file "CLAUDE.md" contains "Docker"
    And the infra file "CLAUDE.md" contains "in-process" ignoring case
