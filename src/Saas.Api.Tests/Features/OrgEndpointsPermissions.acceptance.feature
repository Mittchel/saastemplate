@MVP-01 @RB-ACCESS-01
Feature: Organization endpoints guarded by permissions only
  Organization endpoints answer from permissions and the active organization, and a foreign organization id is not found.

  Scenario: criterion 1 - a foreign organization id gives 404 on the member list
    Given DIA-122 the development user is signed in
    And DIA-122 another organization's id is known
    When DIA-122 the development user lists the members of the other organization
    Then DIA-122 the answer is 404 naming "Organization"

  Scenario: criterion 2 - a foreign organization id gives 404 on a role change
    Given DIA-122 the development user is signed in
    And DIA-122 another organization's id is known
    When DIA-122 the development user sets the role of the other account to "Member" in the other organization
    Then DIA-122 the answer is 404 naming "Organization"

  Scenario: criterion 3 - a Member gets 403 on the member list and the development user gets 200
    Given DIA-122 the development user is signed in
    And DIA-122 the member account holds raw role 0 in the development organization and is switched in
    When DIA-122 the member lists the members of its active organization
    Then DIA-122 the answer is 403
    When DIA-122 the development user lists the members of the development organization
    Then DIA-122 the answer is 200

  Scenario: criterion 4 - a Member gets 403 on a role change and on the invitation list
    Given DIA-122 the development user is signed in
    And DIA-122 the member account holds raw role 0 in the development organization and is switched in
    When DIA-122 the member changes its own role to "Admin" in its active organization
    Then DIA-122 the answer is 403
    When DIA-122 the member lists the invitations of its active organization
    Then DIA-122 the answer is 403

  Scenario: criterion 5 - a promotion to Admin takes effect with the same token
    Given DIA-122 the development user is signed in
    And DIA-122 the member account holds raw role 0 in the development organization and is switched in
    When DIA-122 the member lists the members of its active organization
    Then DIA-122 the answer is 403
    When DIA-122 the development user promotes the member to "Admin"
    Then DIA-122 the answer is 200
    When DIA-122 the member lists the members of its active organization
    Then DIA-122 the answer is 200

  Scenario: criterion 6 - the development user's permissions include the new ones
    Given DIA-122 the development user is signed in
    When DIA-122 the development user requests its profile
    Then DIA-122 the profile lists "invitation.manage"
    And DIA-122 the profile lists "team.read"
    And DIA-122 the profile lists "profile.own"
