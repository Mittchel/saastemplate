@MVP-01 @RB-ACCESS-01
Feature: Per-request permissions from the membership row
  Permissions are resolved from the caller's current membership row on every request, not from the token.

  Scenario: criterion 1 - an undefined role value 99 is refused with the same token
    Given the permission member holds a Member membership in the development organization and is switched in
    When the permission member's role value is set to 99
    And the permission caller requests the members list
    Then the permission answer is 403
    When the permission caller requests the runs list
    Then the permission answer is 403

  Scenario: criteria 2 and 3 - a removed membership is refused with the same token
    Given the permission member holds a Member membership in the development organization and is switched in
    When the permission caller requests the runs list
    Then the permission answer is 200
    When the permission member's membership is removed
    And the permission caller requests the runs list
    Then the permission answer is 403
    When the permission caller requests the members list
    Then the permission answer is 403

  Scenario: criterion 4 - an operator in a workspace without membership is refused
    Given the permission operator has created a run in the home workspace
    When the permission operator enters a workspace of somebody else
    And the permission caller requests the runs list
    Then the permission answer is 403
    And the entered workspace shows 0 runs when counted directly
    And the run still exists in its home workspace

  Scenario: criterion 5 - the development user keeps access to its own organization
    Given the permission development user is signed in
    When the permission caller requests the runs list
    Then the permission answer is 200
    When the permission caller requests the members list
    Then the permission answer is 200
