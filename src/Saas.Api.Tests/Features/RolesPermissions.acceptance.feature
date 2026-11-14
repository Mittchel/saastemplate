@MVP-01 @RB-ACCESS-01
Feature: Per-request permissions from the membership row (acceptance)

  Scenario: criterion 1, 6, 7 and 8: an undefined stored role grants nothing
    Given acc: the member is switched into the development organisation
    When acc: the member row role is set to 99
    Then acc: GET "/runs" answers 403 with the member token
    And acc: GET members of the development organisation answers 403 with the member token

  Scenario: criterion 2, 3, 6, 7, 9 and 10: a removed membership is refused on the same token
    Given acc: the member is switched into the development organisation
    Then acc: GET "/runs" answers 200 with the member token
    When acc: the member row is removed
    Then acc: GET "/runs" answers 403 with the member token
    And acc: GET members of the development organisation answers 403 with the member token

  Scenario: criterion 4 and 11: an operator in a workspace without membership is refused
    Given acc: a platform operator has entered a workspace without membership
    Then acc: GET "/runs" answers 403 with the operator token
    And acc: the entered workspace holds 0 of the operator's runs by direct count

  Scenario: criterion 5: the development user keeps access
    Given acc: the development user is signed in
    Then acc: GET "/runs" answers 200 with the development token
    And acc: GET members of the development organisation answers 200 with the development token
