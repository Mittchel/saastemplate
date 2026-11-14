@MVP-01 @RB-ACCESS-01
Feature: Per-request permissions from the membership row (acceptance DIA-121)

  Scenario: criterion 1 and 8: an undefined stored role value 99 grants nothing
    Given acceptance: the member is switched into the development user's organisation with raw role 0
    When acceptance: the member's raw role is set to 99
    And acceptance: the member requests "/runs"
    Then acceptance: the status was 403
    When acceptance: the member requests "/organizations/{org}/members"
    Then acceptance: the status was 403

  Scenario: criterion 2, 3 and 6: a removed membership stops working with the same token
    Given acceptance: the member is switched into the development user's organisation with raw role 0
    When acceptance: the member requests "/runs"
    Then acceptance: the status was 200
    When acceptance: the member's membership is removed
    And acceptance: the member requests "/runs"
    Then acceptance: the status was 403
    When acceptance: the member requests "/organizations/{org}/members"
    Then acceptance: the status was 403

  Scenario: criterion 4 and 7: an operator in a workspace without membership gets 403
    Given acceptance: the development user entered a foreign workspace
    When acceptance: the operator requests "/runs"
    Then acceptance: the status was 403

  Scenario: criterion 5: the development user keeps access in its own organisation
    Given acceptance: the development user is signed in
    When acceptance: the operator requests "/runs"
    Then acceptance: the status was 200
    When acceptance: the operator requests "/organizations/{org}/members"
    Then acceptance: the status was 200
