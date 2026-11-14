@MVP-01 @RB-ACCESS-01
Feature: Roles
    What a caller may do is decided from their membership row on every request,
    not from the permissions baked into an access token.

    Scenario: A role value that is not defined grants nothing
        Given an ordinary account has been switched into the development user's organization
        When the account's role is set to the unknown value 99
        And I request "/runs"
        Then the response status is 403
        When I request the members of my organization
        Then the response status is 403

    Scenario: A removed membership stops working with the same token
        Given an ordinary account has been switched into the development user's organization
        When I request "/runs"
        Then the response status is 200
        When the account's membership is removed
        And I request "/runs"
        Then the response status is 403
        When I request the members of my organization
        Then the response status is 403

    Scenario: The development user keeps access in its own organization
        Given I am signed in as the development user
        When I request "/runs"
        Then the response status is 200
        When I request the members of my organization
        Then the response status is 200
