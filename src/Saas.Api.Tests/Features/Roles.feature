@MVP-01 @RB-ACCESS-01
Feature: Roles
    Permissions are resolved on every request from the caller's membership row
    in the active organization, not from the claims in the access token. A role
    change or a removed membership therefore applies to a token that is still
    valid.

    Scenario: A role value that is not defined grants nothing
        Given I am signed in as an ordinary account who has joined the development user's organization
        When the membership role is set to 99
        And I request the members of my organization
        Then the response status is 403
        When I request "/runs"
        Then the response status is 403

    Scenario: A removed membership grants nothing even with a valid token
        Given I am signed in as an ordinary account who has joined the development user's organization
        When I request "/runs"
        Then the response status is 200
        When the membership is removed
        And I request "/runs"
        Then the response status is 403
        When I request the members of my organization
        Then the response status is 403

    Scenario: The development user still has access in its own organization
        Given I am signed in as the development user
        When I request "/runs"
        Then the response status is 200
        When I request the members of my organization
        Then the response status is 200
