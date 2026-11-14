Feature: Tenant isolation
    Isolation is enforced twice: by the EF Core query filters, and by Postgres
    row-level security. These scenarios bypass the application entirely and
    query the database as the runtime role, so only the second layer is under
    test -- a pass means the database refuses, not the ORM.

    Scenario: Row-level security is enabled and forced on tenant tables
        Then row-level security is enabled and forced on "Runs"
        And row-level security is enabled and forced on "Memberships"
        And row-level security is enabled and forced on "Invitations"

    Scenario: A run is invisible to another tenant
        Given I am signed in as the development user
        And I have created an organization named "Isolated Workspace"
        And I have switched to that organization
        And I have started a run
        Then the run is visible to its own organization
        But the run is invisible to a different organization
        And the run is invisible with no organization set

    Scenario: The AI service role cannot reach business tables
        Then the AI database role cannot read "Runs"
