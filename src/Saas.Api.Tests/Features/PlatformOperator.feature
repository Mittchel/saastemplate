Feature: Platform operator
    One account operates the installation. It can see every workspace and enter
    any of them for support -- but it enters as an ordinary member, and once
    inside, the database scopes it to that single tenant like anyone else.

    Background:
        Given a workspace exists that belongs to somebody else

    Scenario: The operator's token is marked as such
        Given I am signed in as the development user
        Then the access token marks me as a platform operator

    Scenario: An ordinary account's token is not
        Given I am signed in as an ordinary account
        Then the access token does not mark me as a platform operator

    Scenario: The operator can see every workspace
        Given I am signed in as the development user
        When I request "/platform/organizations"
        Then the response status is 200
        And the workspace belonging to somebody else is listed

    Scenario: An ordinary account cannot see every workspace
        Given I am signed in as an ordinary account
        When I request "/platform/organizations"
        Then the response status is 403

    Scenario: The operator enters a foreign workspace as a member, not an owner
        Given I am signed in as the development user
        And the workspace belonging to somebody else is not in my organization list
        When I enter the workspace belonging to somebody else
        Then entering succeeds
        And the access token carries the role "Member"

    Scenario: An ordinary account cannot enter a workspace that exists but is not theirs
        Given I am signed in as an ordinary account
        When I enter the operator's own workspace
        Then entering is refused

    Scenario: Having entered, the operator is scoped to that one workspace
        Given I am signed in as the development user
        And I have started a run
        And I have entered the workspace belonging to somebody else
        When I request "/me"
        Then the response status is 200
        And the response field "organization.id" matches the token's organization
        But my own workspace's run is no longer visible to me
        And the run is still there for the workspace it belongs to
