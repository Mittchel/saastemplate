Feature: Test infrastructure with Testcontainers and in-process hosts (DIA-128, MVP-02, RB-ACCESS-01)

  Scenario: criterion 1: one Postgres container from the compose image via Testcontainers
    Then the infra file "src/Saas.Api.Tests/Saas.Api.Tests.csproj" contains "Testcontainers.PostgreSql"
    And the infra test sources contain "pgvector/pgvector:pg17"

  Scenario: criterion 2: logins are created by running the init script
    Then the infra test sources contain "00-init.sh"
    And the infra test sources do not contain "CREATE ROLE"

  Scenario: criterion 3: containers are managed by Testcontainers
    Then the infra file "src/Saas.Api.Tests/Saas.Api.Tests.csproj" contains "Testcontainers"
    And the infra test sources contain "PostgreSqlBuilder"

  Scenario: criterion 4: in-process hosts with WebApplicationFactory in Development
    Then the infra test sources contain "WebApplicationFactory"
    And the infra test sources contain "Development"

  Scenario: criterion 5: host configuration
    Then the infra test sources contain "Platform:AdministratorEmail"
    And the infra test sources contain "saas_owner"
    And the infra test sources contain "saas_app"

  Scenario: criterion 6: hosts are started so hosted services run
    Then the infra test sources contain "CreateClient"

  Scenario: criterion 7: drivers use the in-process hosts and the container settings
    Then the infra file "src/Saas.Api.Tests/Drivers/ApiDriver.cs" does not contain "localhost:5002"
    And the infra file "src/Saas.Api.Tests/Drivers/AuthDriver.cs" does not contain "localhost:5001"
    And the infra test sources contain "saas_ai"

  Scenario: criterion 8: real Postgres, no in-memory or SQLite
    Then the infra test sources do not contain "UseInMemoryDatabase"
    And the infra test sources do not contain "Sqlite"

  Scenario: criterion 9: no requests to the compose stack ports
    Then the infra test sources do not contain "localhost:5001"
    And the infra test sources do not contain "localhost:5002"

  Scenario: criterion 10: Program classes are distinguishable
    Then the infra file "src/Saas.Api/Program.cs" contains "partial class Program"
    And the infra file "src/Saas.Auth.Api/Program.cs" contains "partial class Program"

  Scenario: criterion 11: CI needs only Docker
    Then the infra file ".github/workflows/ci.yml" does not contain "docker compose up"

  Scenario: criterion 12: documentation describes Testcontainers
    Then the infra file "src/Saas.Api.Tests/Drivers/TestSettings.cs" contains "Testcontainers"
    And the infra file "CLAUDE.md" contains "Testcontainers"
    And the infra file "CLAUDE.md" contains "Docker"
