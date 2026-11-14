@MVP-01 @RB-ACCESS-01
Feature: Roles acceptance (DIA-88)

  Scenario: criterion 1: dev account is HrAdmin and no stored role is out of range
    Given acct "dev@saas.local" is signed in
    When acct requests "GET /me"
    Then the me organizationRole is "HrAdmin"
    And no stored membership or invitation role is outside 0 to 2

  Scenario: criterion 2: demoting HrAdmin to Employee applies on the next request
    Given acct "member@saas.local" is a member of the dev organisation with role value 2
    And acct "member@saas.local" is signed in to the dev organisation
    When acct "member@saas.local" requests the members list
    Then the status is 200
    When the dev HR admin sets "member@saas.local" role to "Employee"
    And acct "member@saas.local" requests the members list with the same token
    Then the status is 403
    And the member row is removed

  Scenario: criterion 3: promoting Employee to HrAdmin applies on the next request
    Given acct "member@saas.local" is a member of the dev organisation with role value 0
    And acct "member@saas.local" is signed in to the dev organisation
    When the dev HR admin sets "member@saas.local" role to "HrAdmin"
    And acct "member@saas.local" requests the members list with the same token
    Then the status is 200
    And the member row is removed

  Scenario: criterion 4: unknown role value gets 403 everywhere
    Given acct "member@saas.local" is a member of the dev organisation with role value 0
    And acct "member@saas.local" is signed in to the dev organisation
    When the member row raw role is set to 99
    And acct "member@saas.local" requests the members list with the same token
    Then the status is 403
    When acct requests "GET /runs" with the same token
    Then the status is 403
    And the member row is removed

  Scenario: criterion 5: removed membership gets 403
    Given acct "member@saas.local" is a member of the dev organisation with role value 0
    And acct "member@saas.local" is signed in to the dev organisation
    When the member row is removed
    And acct "member@saas.local" requests the members list with the same token
    Then the status is 403

  Scenario Outline: criterion 6: members list is HR admin only
    Given acct "member@saas.local" is a member of the dev organisation with role value <value>
    And acct "member@saas.local" is signed in to the dev organisation
    When acct "member@saas.local" requests the members list with the same token
    Then the status is <status>
    And the member row is removed

    Examples:
      | value | status |
      | 0     | 403    |
      | 1     | 403    |
      | 2     | 200    |
