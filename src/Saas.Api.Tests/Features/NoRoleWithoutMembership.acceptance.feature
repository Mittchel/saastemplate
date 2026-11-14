Feature: No role or permissions without a membership row
    Roles and permissions come only from a real membership row, so an operator
    who enters a workspace they do not belong to carries none.

    Scenario: Criteria 1 and 2 - the operator enters a foreign workspace with no role claim and no permission claims
        Given the nr operator is signed in
        And the nr foreign workspace exists
        When the nr operator enters the foreign workspace
        Then the nr entering succeeds
        And the nr token has no org_role claim
        And the nr token has no permission claims
        And the nr token carries the entered workspace as org_id
        And the nr token marks the caller as a platform operator

    Scenario: Criteria 3, 10 and 11 - after entering, GET /me has a null role and no permissions
        Given the nr operator is signed in
        And the nr foreign workspace exists
        When the nr operator enters the foreign workspace
        And the nr caller requests "/me"
        Then the nr response status is 200
        And the nr current user organizationRole is null
        And the nr current user permissions are empty
        And the nr current user organization is the entered workspace with role null

    Scenario: Criteria 4 and 12 - after entering, GET /me lists the real memberships plus the entered workspace with role null
        Given the nr operator is signed in
        And the nr foreign workspace exists
        When the nr operator enters the foreign workspace
        And the nr caller requests "/me"
        Then the nr response status is 200
        And the nr organizations list holds the home workspace with a real role
        And the nr organizations list holds the entered workspace with role null

    Scenario: Criterion 5 - after entering, GET /runs is forbidden
        Given the nr operator is signed in
        And the nr foreign workspace exists
        When the nr operator enters the foreign workspace
        And the nr caller requests "/runs"
        Then the nr response status is 403

    Scenario: Criterion 6 - the development user in its own organisation has a role and organization.read
        Given the nr operator is signed in
        Then the nr token has a non-empty org_role claim
        And the nr token has the permission claim "organization.read"

    Scenario: Criterion 7 - an ordinary account cannot enter a workspace it does not belong to
        Given the nr ordinary account is signed in
        And the nr operator's workspace is known
        When the nr ordinary account tries to enter the operator's workspace
        Then the nr entering is refused

    Scenario: Criterion 8 - a non-operator without a membership row gets 403 from GET /me
        Given the nr ordinary account holds a membership in the development organization and is switched in
        When the nr ordinary account's membership is removed
        And the nr caller requests "/me"
        Then the nr response status is 403

    Scenario: Criterion 9 - with a membership row, GET /me role and permissions come from that row
        Given the nr ordinary account holds a membership in the development organization and is switched in
        When the nr caller requests "/me"
        Then the nr response status is 200
        And the nr current user organizationRole is not null
        And the nr current user permissions equal the token permission claims
