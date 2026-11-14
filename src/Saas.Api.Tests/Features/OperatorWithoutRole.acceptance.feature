@MVP-01 @RB-ACCESS-01
Feature: No role or permissions without a real membership row
  The token and GET /me grant a role and permissions only from a real membership row.

  Scenario: criteria 1 and 2 - an operator entering a foreign workspace gets a token without role or permissions
    Given dia123 the operator is signed in and a foreign workspace exists
    When dia123 the operator enters the foreign workspace
    Then dia123 entering succeeded
    And dia123 the token has no org_role claim and no permission claims
    And dia123 the token still carries the entered org_id and the platform-admin claim

  Scenario: criteria 3 and 10 - GET /me after entering has null role and empty permissions
    Given dia123 the operator is signed in and a foreign workspace exists
    When dia123 the operator enters the foreign workspace
    And dia123 the operator requests the current user
    Then dia123 the current user answer is 200
    And dia123 the current user has a null organizationRole and empty permissions
    And dia123 the current user organization is the foreign workspace with a null role

  Scenario: criterion 4 - the organizations list holds real memberships plus the entered one with null role
    Given dia123 the operator is signed in and a foreign workspace exists
    When dia123 the operator enters the foreign workspace
    And dia123 the operator requests the current user
    Then dia123 the organizations list has the home workspace with a role and the foreign workspace with a null role

  Scenario: criterion 5 - the operator gets 403 from runs in the entered workspace
    Given dia123 the operator is signed in and a foreign workspace exists
    When dia123 the operator enters the foreign workspace
    And dia123 the operator requests the runs list
    Then dia123 the runs answer is 403

  Scenario: criterion 6 - the development user keeps its role and permissions in its own organization
    Given dia123 the operator is signed in and a foreign workspace exists
    Then dia123 the token has an org_role claim and the permission organization.read

  Scenario: criterion 7 - an ordinary account cannot enter a workspace it is not a member of
    Given dia123 an ordinary account is signed in and the operator's workspace is known
    When dia123 the ordinary account enters the operator's workspace
    Then dia123 entering was refused

  Scenario: criterion 8 - a non-operator without a membership row gets 403 from GET /me
    Given dia123 the member holds a membership in the development organization and is switched in
    When dia123 the member's membership is removed
    And dia123 the member requests the current user
    Then dia123 the current user answer is 403

  Scenario: criterion 9 - GET /me takes role and permissions from the membership row
    Given dia123 the member holds a membership in the development organization and is switched in
    When dia123 the member requests the current user
    Then dia123 the current user answer is 200
    And dia123 the current user has a non-null organizationRole and some permissions
