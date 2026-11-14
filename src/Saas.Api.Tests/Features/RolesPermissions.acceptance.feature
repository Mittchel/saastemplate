@MVP-01 @RB-ACCESS-01
Feature: Per-request permissions from the membership row (acceptance)

  Scenario: criterion 1: an undefined stored role value gets 403 with the same token
    Given acc the member is switched into the development user's organisation
    When acc the member's stored role is set to 99
    Then acc the member gets 403 from the members list and from runs

  Scenario: criterion 2 and 3: removed membership gets 403, before removal 200
    Given acc the member is switched into the development user's organisation
    Then acc the member gets 200 from runs
    When acc the member's membership is removed
    Then acc the member gets 403 from the members list and from runs

  Scenario: criterion 4: operator in a workspace without membership gets 403
    Given acc the operator has entered a workspace they are not a member of
    Then acc the operator gets 403 from runs

  Scenario: criterion 5: the development user keeps access in its own organisation
    Given acc the development user is signed in
    Then acc the development user gets 200 from runs and the members list
