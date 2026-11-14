@MVP-01 @RB-ACCESS-01
Feature: Per-request permissions from membership row

  Scenario: criterion 1 and 8: an undefined stored role value grants nothing
    Given acceptance member is switched into the development organisation
    When acceptance member row role is set to 99
    And I request "/runs"
    Then the response status is 403
    When I request "/organizations/{acceptanceOrganizationId}/members"
    Then the response status is 403

  Scenario: criterion 3 and 2: removed membership is refused with the same token
    Given acceptance member is switched into the development organisation
    When I request "/runs"
    Then the response status is 200
    When acceptance member row is removed
    And I request "/runs"
    Then the response status is 403
    When I request "/organizations/{acceptanceOrganizationId}/members"
    Then the response status is 403

  Scenario: criterion 6 and 7: permissions follow the membership row per request, not token claims or a fallback role
    Given acceptance member is switched into the development organisation
    When I request "/runs"
    Then the response status is 200
    When acceptance member row role is set to 99
    And I request "/runs"
    Then the response status is 403
    When acceptance member row is removed
    And I request "/runs"
    Then the response status is 403

  Scenario: criterion 4, 9, 10 and 11: a caller without a membership in the active organisation is refused and cleanup restores state
    Given acceptance member is switched into the development organisation
    When acceptance member row is removed
    And I request "/runs"
    Then the response status is 403

  Scenario: criterion 5: development user keeps access in its own organisation
    Given acceptance development user is signed in
    When I request "/runs"
    Then the response status is 200
    When I request "/organizations/{acceptanceOrganizationId}/members"
    Then the response status is 200
