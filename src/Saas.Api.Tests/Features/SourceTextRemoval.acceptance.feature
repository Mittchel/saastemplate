Feature: Source-text scenarios are removed from the test project
  The test project only checks external behaviour, so the source-text infrastructure scenarios are gone.

  Scenario: Criterion 1 - the infrastructure feature file no longer exists
    Given the test project folder is located
    Then the file "Features/TestInfrastructure.acceptance.feature" does not exist in the test project

  Scenario: Criterion 2 - the generated code behind the infrastructure feature is removed
    Given the test project folder is located
    Then the file "Features/TestInfrastructure.acceptance.feature.cs" does not exist in the test project

  Scenario: Criterion 3 - the infrastructure steps file is deleted
    Given the test project folder is located
    Then the file "Steps/TestInfrastructureSteps.cs" does not exist in the test project
    And the compiled test assembly has no type named "TestInfrastructureSteps"

  Scenario: Criterion 4 - no leftover source-reading step class remains
    Given the test project folder is located
    Then the compiled test assembly has no type named "TestInfrastructureSteps"
    And the file "Steps/TestInfrastructureSteps.cs" does not exist in the test project

  Scenario: Criterion 5 - the remaining scenarios build and run without the removed steps
    Given the test project folder is located
    Then the file "Features/TestInfrastructure.acceptance.feature" does not exist in the test project

  Scenario: Criterion 6 - the shared test drivers are still present
    Given the test project folder is located
    Then the file "Drivers/TestHosts.cs" exists in the test project
    And the file "Drivers/TestSettings.cs" exists in the test project
