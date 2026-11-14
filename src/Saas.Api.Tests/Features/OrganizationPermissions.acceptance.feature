@MVP-01 @RB-ACCESS-01
Feature: Permission-only organization endpoints
  Organization endpoints are guarded by permissions only, and another organization's id answers 404.

  Scenario: criterion 1 - a foreign organization id on the members list answers 404
    Given dia122 the development user is signed in
    When dia122 the development user lists the members of another organization
    Then dia122 the answer is 404

  Scenario: criterion 2 - a foreign organization id on a role change answers 404
    Given dia122 the development user is signed in
    When dia122 the development user sets the Member role on a member of another organization
    Then dia122 the answer is 404

  Scenario: criterion 3 - a Member is refused the members list and the development user is allowed
    Given dia122 a Member holds a membership in the development organization and is switched in
    When dia122 the member lists the members of the active organization
    Then dia122 the answer is 403
    When dia122 the development user lists the members of the development organization
    Then dia122 the answer is 200

  Scenario: criterion 4 - a Member is refused a role change and the invitations list
    Given dia122 a Member holds a membership in the development organization and is switched in
    When dia122 the member sets the Member role on the member in the active organization
    Then dia122 the answer is 403
    When dia122 the member lists the invitations of the active organization
    Then dia122 the answer is 403

  Scenario: criterion 5 - a promoted member can list the members with the same token
    Given dia122 a Member holds a membership in the development organization and is switched in
    When dia122 the member lists the members of the active organization
    Then dia122 the answer is 403
    When dia122 the development user promotes the member to Admin
    Then dia122 the answer is 2xx
    When dia122 the member lists the members of the active organization
    Then dia122 the answer is 200

  Scenario: criterion 6 - the development user's permissions include the new ones
    Given dia122 the development user is signed in
    When dia122 the development user requests its profile
    Then dia122 the profile lists invitation.manage, team.read and profile.own
