Feature: Organizations
    Every user always has a personal workspace, and switching between
    workspaces is a token operation rather than client state.

    Scenario: A new account is provisioned with a personal workspace
        When I register a new account
        Then the registration succeeds
        And signing in with the new account succeeds
        And the new account has exactly 1 organization
        And the organization is personal

    Scenario: Creating an organization makes the caller its owner
        Given I am signed in as the development user
        When I create an organization named "Test Workspace"
        Then the organization is created
        And my organization list includes "Test Workspace"

    Scenario: Switching organization mints a token for the new tenant
        Given I am signed in as the development user
        And I have created an organization named "Switch Target"
        When I switch to that organization
        Then the switch succeeds
        And the access token carries that organization

    Scenario: Switching into an organization I do not belong to is refused
        Given I am signed in as the development user
        When I switch to an organization I am not a member of
        Then the switch is refused
