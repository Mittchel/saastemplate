@MVP-01 @RB-ACCESS-01
Feature: Permission-only organisation endpoints
  Organisation endpoints are guarded by permissions only, and a foreign organisation id answers 404.

  Scenario: criterion 1 - the development user gets 404 listing the members of another organisation
    Given the endpoint caller is the development user and knows another organisation's id
    When the endpoint caller lists the members of the other organisation
    Then the endpoint answer is 404

  Scenario: criterion 2 - the development user gets 404 updating a member of another organisation
    Given the endpoint caller is the development user and knows another organisation's id
    When the endpoint caller sets the role of a user to "Member" in the other organisation
    Then the endpoint answer is 404

  Scenario: criterion 3 - a Member is refused the member list while the development user gets it
    Given the endpoint member holds role 0 in the development organisation and is switched in
    When the endpoint member lists the members of the active organisation
    Then the endpoint answer is 403
    When the endpoint development user lists the members of the active organisation
    Then the endpoint answer is 200

  Scenario: criterion 4 - a Member is refused member updates and the invitation list
    Given the endpoint member holds role 0 in the development organisation and is switched in
    When the endpoint member updates its own role to "Member" in the active organisation
    Then the endpoint answer is 403
    When the endpoint member lists the invitations of the active organisation
    Then the endpoint answer is 403

  Scenario: criterion 5 - a promoted member gets the member list with the same token
    Given the endpoint member holds role 0 in the development organisation and is switched in
    When the endpoint member lists the members of the active organisation
    Then the endpoint answer is 403
    When the endpoint development user promotes the member to "Admin"
    Then the endpoint answer is 200
    When the endpoint member lists the members of the active organisation
    Then the endpoint answer is 200

  Scenario: criterion 6 - the development user's profile lists the new permissions
    Given the endpoint caller is the development user and knows another organisation's id
    When the endpoint caller requests its profile
    Then the endpoint profile lists "invitation.manage", "team.read" and "profile.own"
