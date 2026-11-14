@MVP-01 @RB-ACCESS-01
Feature: Organization endpoints are guarded by permissions only
  Permission policies are the only access rule, and a foreign organization id answers 404.

  Scenario: criterion 1 - a foreign organization id on the member list answers 404
    Given dia122 the development user is signed in
    When dia122 the development user lists the members of another organization
    Then dia122 the answer is 404

  Scenario: criterion 2 - a foreign organization id on a role update answers 404
    Given dia122 the development user is signed in
    When dia122 the development user sets a member role in another organization
    Then dia122 the answer is 404

  Scenario: criterion 3 - a Member cannot list members but the development user can
    Given dia122 the member holds role value 0 in the development organization and is switched in
    When dia122 the member lists the members of the active organization
    Then dia122 the answer is 403
    When dia122 the development user lists the members of the development organization
    Then dia122 the answer is 200

  Scenario: criterion 4 - a Member cannot update roles or list invitations
    Given dia122 the member holds role value 0 in the development organization and is switched in
    When dia122 the member updates a role in the active organization
    Then dia122 the answer is 403
    When dia122 the member lists the invitations of the active organization
    Then dia122 the answer is 403

  Scenario: criterion 5 - a promoted member can list members with the same token
    Given dia122 the member holds role value 0 in the development organization and is switched in
    When dia122 the member lists the members of the active organization
    Then dia122 the answer is 403
    When dia122 the development user promotes the member to Admin
    Then dia122 the answer is 200
    When dia122 the member lists the members of the active organization
    Then dia122 the answer is 200

  Scenario: criterion 6 - the development user has the new permissions
    Given dia122 the development user is signed in
    When dia122 the development user requests their profile
    Then dia122 the profile lists the permissions invitation.manage, team.read and profile.own
