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
        And my permissions include "invitation.manage"
        And my permissions include "team.read"
        And my permissions include "profile.own"

    Scenario: Another organization's id answers 404 for its owner
        Given the development user is signed in as a second caller
        When I request the members of another organization
        Then the response status is 404
        When I change a member's role to Member in another organization
        Then the response status is 404

    Scenario: A member cannot manage members until promoted, and the promotion applies to the same token
        Given an ordinary account has been switched into the development user's organization
        And the development user is signed in as a second caller
        When I request the members of my organization
        Then the response status is 403
        When I change the account's role in my organization
        Then the response status is 403
        When I request the invitations of my organization
        Then the response status is 403
        When the development user promotes the account to Admin
        And I request the members of my organization
        Then the response status is 200
