Feature: DIA-128 integration test infrastructure (MVP-02 / RB-ACCESS-01)

  Scenario: criterion 1: Postgres comes from Testcontainers with the compose image
    Then the acceptance file "src/Saas.Api.Tests/Saas.Api.Tests.csproj" contains "Testcontainers.PostgreSql"
    And the acceptance test project sources contain "pgvector/pgvector:pg17"

  Scenario: criterion 2: logins are created by running 00-init.sh
    Then the acceptance file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "00-init.sh"
    And the acceptance test project sources do not contain "CREATE ROLE"

  Scenario: criterion 3: containers are disposed at the end of the run
    Then the acceptance test project sources contain "DisposeAsync"

  Scenario: criterion 4: hosts run in-process with WebApplicationFactory in Development
    Then the acceptance file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "WebApplicationFactory"
    And the acceptance file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "Development"

  Scenario: criterion 5: host configuration uses the roles and administrator email
    Then the acceptance file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "saas_owner"
    And the acceptance file "src/Saas.Api.Tests/Drivers/TestHosts.cs" contains "saas_app"
    And the acceptance file "src/Saas.Api.Tests/Drivers/TestHosts.cs" matches "Platform(:|__)AdministratorEmail"

  Scenario: criterion 6: hosted services are not removed
    Then the acceptance file "src/Saas.Api.Tests/Drivers/TestHosts.cs" does not contain "IHostedService"

  Scenario: criterion 7: drivers get their clients from the in-process hosts
    Then the acceptance file "src/Saas.Api.Tests/Drivers/ApiDriver.cs" contains "TestHosts"
    And the acceptance file "src/Saas.Api.Tests/Drivers/AuthDriver.cs" contains "TestHosts"
    And the acceptance file "src/Saas.Api.Tests/Drivers/TestSettings.cs" contains "saas_ai"

  Scenario: criterion 8: row-level security is real Postgres
    Then the acceptance file "src/Saas.Api.Tests/Saas.Api.Tests.csproj" does not contain "Sqlite"
    And the acceptance test project sources do not contain "UseInMemoryDatabase"
    And the acceptance test project sources do not contain "UseSqlite"
    And the acceptance file "src/Saas.Api.Tests/Features/TenantIsolation.feature" contains "Scenario"

  Scenario: criterion 9: no requests to the compose stack ports
    Then the acceptance file "src/Saas.Api.Tests/Drivers/ApiDriver.cs" does not contain "localhost:5002"
    And the acceptance file "src/Saas.Api.Tests/Drivers/ApiDriver.cs" does not contain "localhost:5001"
    And the acceptance file "src/Saas.Api.Tests/Drivers/AuthDriver.cs" does not contain "localhost:5001"
    And the acceptance file "src/Saas.Api.Tests/Drivers/AuthDriver.cs" does not contain "localhost:5002"
    And the acceptance file "src/Saas.Api.Tests/Drivers/TestHosts.cs" does not contain "localhost:5001"
    And the acceptance file "src/Saas.Api.Tests/Drivers/TestHosts.cs" does not contain "localhost:5002"

  Scenario: criterion 10: Program classes are distinguishable and saas-ai is not hosted
    Then the acceptance file "src/Saas.Api/Program.cs" contains "partial class Program"
    And the acceptance file "src/Saas.Auth.Api/Program.cs" contains "partial class Program"
    And the acceptance file "src/Saas.Api.Tests/Drivers/TestHosts.cs" does not contain "saas-ai"

  Scenario: criterion 11: CI integration job does not use docker compose up
    Then the acceptance file ".github/workflows/ci.yml" does not match "docker[ -]compose\s+.*\bup\b"

  Scenario: criterion 12: remarks describe Testcontainers and in-process hosts
    Then the acceptance file "src/Saas.Api.Tests/Drivers/TestSettings.cs" contains "Testcontainers"
    And the acceptance file "src/Saas.Api.Tests/Drivers/TestSettings.cs" contains "Docker"
    And the acceptance file "CLAUDE.md" contains "Testcontainers"
    And the acceptance file "CLAUDE.md" contains "in-process"
