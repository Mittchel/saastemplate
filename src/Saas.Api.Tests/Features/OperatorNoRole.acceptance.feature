@MVP-01 @RB-ACCESS-01
Feature: No role or permissions without a real membership row
  The token and GET /me grant a role and permissions only from a real membership row in the active organisation.

  Scenario: criteria 1 and 2 - entering a foreign workspace mints no role and no permission claims
    Given opnr the operator has signed in
    When opnr the operator enters a workspace of somebody else
    Then opnr entering succeeded
    And opnr the access token has no "org_role" claim
    And opnr the access token has no "permission" claim
    And opnr the access token has the entered workspace as "org_id"
    And opnr the access token marks the caller as platform admin

  Scenario: criterion 3 - GET /me after entering shows no role and no permissions
    Given opnr the operator has signed in
    When opnr the operator enters a workspace of somebody else
    And opnr the caller requests "/me"
    Then opnr the answer is 200
    And opnr organizationRole is null
    And opnr permissions is an empty list
    And opnr organization id is the entered workspace and its role is null

  Scenario: criteria 4 and 10 - GET /me organizations lists real memberships plus the entered workspace with null role
    Given opnr the operator has signed in
    When opnr the operator enters a workspace of somebody else
    And opnr the caller requests "/me"
    Then opnr the answer is 200
    And opnr organizations hold the home workspace with a real role
    And opnr organizations hold the entered workspace with a null role

  Scenario: criterion 11 (API side only) - GET /me serialises a null role where the frontend type accepts null
    Given opnr the operator has signed in
    When opnr the operator enters a workspace of somebody else
    And opnr the caller requests "/me"
    Then opnr the answer is 200
    And opnr organizationRole is null
    And opnr organization id is the entered workspace and its role is null
    And opnr organizations hold the home workspace with a real role
    And opnr organizations hold the entered workspace with a null role

  Scenario: criteria 5 and 12- the operator gets 403 from runs in the entered workspace
    Given opnr the operator has signed in
    When opnr the operator enters a workspace of somebody else
    And opnr the caller requests "/runs"
    Then opnr the answer is 403

  Scenario: criteria 6 and 9 - the development user in its own organisation keeps its role and permissions
    Given opnr the operator has signed in
    Then opnr the access token has a non-empty "org_role" claim
    And opnr the access token permission claims include "organization.read"
    When opnr the caller requests "/me"
    Then opnr the answer is 200
    And opnr organizationRole is not null
    And opnr permissions include "organization.read"

  Scenario: criterion 7 - an ordinary account cannot enter a workspace it is not a member of
    Given opnr an ordinary account has signed in
    When opnr the ordinary account tries to enter the development workspace
    Then opnr entering was refused

  Scenario: criterion 8 - a non-operator with no membership row gets 403 from GET /me
    Given opnr the ordinary account holds a membership in the development workspace and is switched in
    When opnr the caller requests "/me"
    Then opnr the answer is 200
    When opnr the ordinary account's membership is removed
    And opnr the caller requests "/me"
    Then opnr the answer is 403
