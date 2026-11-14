@MVP-01 @RB-ACCESS-01
Feature: No role or permissions without a real membership row
  Roles and permissions are only ever reported from a real membership row in the active organisation.

  Scenario: criteria 1 and 2 - an operator entering a foreign workspace gets a token without role or permissions
    Given an operatorless workspace of somebody else exists
    And the operatorless development user is signed in
    When the operatorless operator enters that workspace
    Then the operatorless entering succeeded
    And the operatorless token has no "org_role" claim
    And the operatorless token has no "permission" claim
    And the operatorless token claim "is_platform_admin" is "true"
    And the operatorless token claim "org_id" is the entered workspace

  Scenario: criteria 3 and 10 and 11 - after entering, GET /me reports no role and no permissions (API side only for the frontend type)
    Given an operatorless workspace of somebody else exists
    And the operatorless development user is signed in
    When the operatorless operator enters that workspace
    And the operatorless caller requests the current user
    Then the operatorless answer is 200
    And the operatorless current user has a null organizationRole
    And the operatorless current user has an empty permissions list
    And the operatorless current organization is the entered workspace with a null role

  Scenario: criteria 4 and 10 - after entering, the organizations list keeps real memberships and adds the entered one with a null role
    Given an operatorless workspace of somebody else exists
    And the operatorless development user is signed in
    When the operatorless operator enters that workspace
    And the operatorless caller requests the current user
    Then the operatorless answer is 200
    And the operatorless organizations keep the real memberships with their roles
    And the operatorless organizations list the entered workspace with a null role

  Scenario: criteria 5 and 12 - after entering, GET /runs is refused
    Given an operatorless workspace of somebody else exists
    And the operatorless development user is signed in
    When the operatorless operator enters that workspace
    And the operatorless caller requests the runs list
    Then the operatorless answer is 403

  Scenario: criteria 6 and 9 - the development user in its own organisation keeps role and permissions
    Given the operatorless development user is signed in
    Then the operatorless token has a non-empty "org_role" claim
    And the operatorless token permission claims include "organization.read"
    When the operatorless caller requests the current user
    Then the operatorless answer is 200
    And the operatorless current user role matches the token's role claim
    And the operatorless current user permissions include "organization.read"

  Scenario: criterion 7 - an ordinary account cannot enter a workspace it is not a member of
    Given the operatorless development user is signed in
    When an operatorless ordinary account tries to enter the development workspace
    Then the operatorless entering was refused

  Scenario: criteria 8 and 9 - a non-operator without a membership row is refused on GET /me, with a row it gets its role
    Given an operatorless ordinary account holds a Member membership in the development workspace and is switched in
    When the operatorless caller requests the current user
    Then the operatorless answer is 200
    And the operatorless current user has the role "Member"
    And the operatorless current user has at least one permission
    When the operatorless ordinary account's membership is removed
    And the operatorless caller requests the current user
    Then the operatorless answer is 403
