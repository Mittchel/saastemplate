@MVP-01 @RB-ACCESS-01
Feature: Roles acceptance (DIA-88)

  Scenario: criterion 1: seeded dev account is HrAdmin and no stored role is out of range
    Given acceptance: dev is signed in
    When acceptance: dev requests GET /me
    Then acceptance: organizationRole is "HrAdmin"
    And acceptance: no stored membership or invitation role is outside 0, 1 and 2

  Scenario: criterion 2: demoting HrAdmin to Employee applies on the next request
    Given acceptance: dev is signed in
    And acceptance: member is added to dev's organisation as "HrAdmin" and signed in there
    When acceptance: member requests the members list
    Then acceptance: the status is 200
    When acceptance: dev changes member's role to "Employee"
    And acceptance: member requests the members list
    Then acceptance: the status is 403
    And acceptance: the membership is removed

  Scenario: criterion 3: promoting Employee to HrAdmin applies on the next request
    Given acceptance: dev is signed in
    And acceptance: member is added to dev's organisation as "Employee" and signed in there
    When acceptance: member requests the members list
    Then acceptance: the status is 403
    When acceptance: dev changes member's role to "HrAdmin"
    And acceptance: member requests the members list
    Then acceptance: the status is 200
    And acceptance: the membership is removed

  Scenario: criterion 4: unknown stored role gets 403 everywhere
    Given acceptance: dev is signed in
    And acceptance: member is added to dev's organisation as "Employee" and signed in there
    When acceptance: the member's raw stored role is set to 99
    And acceptance: member requests the members list
    Then acceptance: the status is 403
    When acceptance: member requests GET /runs
    Then acceptance: the status is 403
    And acceptance: the membership is removed

  Scenario: criterion 5: removed membership gets 403 with the old token
    Given acceptance: dev is signed in
    And acceptance: member is added to dev's organisation as "HrAdmin" and signed in there
    When acceptance: the membership is removed
    And acceptance: member requests the members list
    Then acceptance: the status is 403

  Scenario Outline: criterion 6: members list is HR admin only
    Given acceptance: dev is signed in
    And acceptance: member is added to dev's organisation as "<role>" and signed in there
    When acceptance: member requests the members list
    Then acceptance: the status is <status>
    And acceptance: the membership is removed

    Examples:
      | role     | status |
      | Employee | 403    |
      | Manager  | 403    |
      | HrAdmin  | 200    |
