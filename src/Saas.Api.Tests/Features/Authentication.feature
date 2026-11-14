Feature: Authentication
    Signing in exchanges credentials for a token that carries the tenant, so
    every downstream service resolves the organization without a lookup.

    Scenario: Signing in issues a token scoped to an organization
        When I sign in as the development user
        Then the sign in succeeds
        And the access token carries an organization
        And the access token carries the role "HrAdmin"
        And the access token carries the permission "run.create"

    Scenario: Signing in with the wrong password is refused
        When I sign in with email "dev@saas.local" and password "WrongPassword123"
        Then the sign in fails

    Scenario: An unknown account is refused indistinguishably
        When I sign in with email "nobody@saas.local" and password "DevPassword123"
        Then the sign in fails

    Scenario: The current user reflects the token's organization
        Given I am signed in as the development user
        When I request "/me"
        Then the response status is 200
        And the response field "organization.id" matches the token's organization

    Scenario: An unauthenticated request is rejected
        When I request "/me" without a token
        Then the response status is 401
