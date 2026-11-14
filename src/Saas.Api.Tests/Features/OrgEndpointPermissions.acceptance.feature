@MVP-01 @RB-ACCESS-01
Feature: Organisation endpoints are guarded by permissions only
  Organisation endpoints answer 404 for a foreign organisation id and follow the caller's role permissions.

  Scenario: criteria 1 and 2 - a foreign organisation id answers 404 for the development user
    Given the endpoint test knows another organisation's id
    When the development user requests the members of the other organisation
    Then the endpoint answer is 404
    When the development user sets the member's role to Member in the other organisation
    Then the endpoint answer is 404

  Scenario: criteria 3 and 4 - a plain Member is refused on administrator endpoints
    Given the endpoint test member holds role value 0 in the development organisation and is switched in
    When the member requests the members of the development organisation
    Then the endpoint answer is 403
    When the development user requests the members of the development organisation
    Then the endpoint answer is 200
    When the member sets its own role to Member in the development organisation
    Then the endpoint answer is 403
    When the member requests the invitations of the development organisation
    Then the endpoint answer is 403

  Scenario: criterion 5 - a promoted member can read the members with the same token
    Given the endpoint test member holds role value 0 in the development organisation and is switched in
    When the member requests the members of the development organisation
    Then the endpoint answer is 403
    When the development user promotes the member to Admin
    And the member requests the members of the development organisation
    Then the endpoint answer is 200

  Scenario: criterion 6 - the development user holds the new permissions
    Given the endpoint test development user is signed in
    When the development user requests "/me"
    Then the endpoint answer is 200
    And the endpoint response lists "invitation.manage", "team.read" and "profile.own"
