@MVP-01 @RB-ACCESS-01
Feature: No role or permissions without a real membership row
  The token and GET /me grant a role and permissions only from a real membership row.

  Scenario: criteria 1, 2 and 12 - entering a foreign workspace yields a token without org_role and permission claims
    Given the dia123 operator is signed in
    And the dia123 workspace of somebody else exists
    When the dia123 operator enters that workspace
    Then the dia123 entering succeeded
    And the dia123 token has no org_role claim
    And the dia123 token has no permission claims
    And the dia123 token carries the org_id of the entered workspace
    And the dia123 token carries the platform admin claim

  Scenario: criteria 3 and 10 - GET /me after entering has null role and no permissions
    Given the dia123 operator is signed in
    And the dia123 workspace of somebody else exists
    When the dia123 operator enters that workspace
    And the dia123 caller requests the current user
    Then the dia123 current user answer is 200
    And the dia123 current user organizationRole is null
    And the dia123 current user permissions are empty
    And the dia123 current user organization is the entered workspace with role null

  Scenario: criteria 4 and 12 - GET /me organizations lists real memberships plus the entered workspace with role null
    Given the dia123 operator is signed in
    And the dia123 operator's memberships are known
    And the dia123 workspace of somebody else exists
    When the dia123 operator enters that workspace
    And the dia123 caller requests the current user
    Then the dia123 organizations hold the home workspace with its real role
    And the dia123 organizations hold the entered workspace with role null

  Scenario: criteria 5 and 12 - the operator gets 403 from GET /runs in the entered workspace
    Given the dia123 operator is signed in
    And the dia123 workspace of somebody else exists
    When the dia123 operator enters that workspace
    And the dia123 caller requests the runs list
    Then the dia123 runs answer is 403

  Scenario: criterion 11 - API side only - the wire contract the frontend types must accept carries null roles
    Given the dia123 operator is signed in
    And the dia123 workspace of somebody else exists
    When the dia123 operator enters that workspace
    And the dia123 caller requests the current user
    Then the dia123 current user answer is 200
    And the dia123 current user organizationRole is null
    And the dia123 organizations hold the entered workspace with role null

  Scenario: criterion 6 -the development user keeps its role name and permission claims in its own organisation
    Given the dia123 operator is signed in
    Then the dia123 token has a non-empty org_role claim
    And the dia123 token has the permission claim "organization.read"

  Scenario: criterion 7 - an ordinary account cannot enter a workspace it is not a member of
    Given the dia123 ordinary account is signed in
    When the dia123 ordinary account enters the development workspace
    Then the dia123 entering was refused

  Scenario: criterion 8 - a non-operator without a membership row gets 403 from GET /me
    Given the dia123 ordinary account holds a membership in the development workspace and is switched in
    When the dia123 membership is removed
    And the dia123 caller requests the current user
    Then the dia123 current user answer is 403

  Scenario: criterion 9 - GET /me role and permissions come from the membership row
    Given the dia123 ordinary account holds a membership in the development workspace and is switched in
    When the dia123 caller requests the current user
    Then the dia123 current user answer is 200
    And the dia123 current user organizationRole is "Member"
    And the dia123 current user has permissions
