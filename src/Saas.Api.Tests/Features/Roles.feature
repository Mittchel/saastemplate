@MVP-01 @RB-ACCESS-01
Feature: Roles
    Dialog has three roles -- Employee, Manager and HR admin -- and every
    request is authorised from the caller's current role in the database, not
    from the claims in the access token.

    Scenario: The development account is an HR admin of its own organization
        Given I am signed in as the development user
        When I request "/me"
        Then the response status is 200
        And the response field "organizationRole" is "HrAdmin"
        And no membership or invitation holds an unknown role

    Scenario: A member with the Employee role gets 403 when listing the members
        Given I am signed in as the development user
        And the member account belongs to my organization as "Employee"
        When the member lists the organization's members
        Then the member's response status is 403

    Scenario: A member with the Manager role gets 403 when listing the members
        Given I am signed in as the development user
        And the member account belongs to my organization as "Manager"
        When the member lists the organization's members
        Then the member's response status is 403

    Scenario: A member with the HrAdmin role gets 200 when listing the members
        Given I am signed in as the development user
        And the member account belongs to my organization as "HrAdmin"
        When the member lists the organization's members
        Then the member's response status is 200

    Scenario: Demoting an HR admin takes effect on their next request
        Given I am signed in as the development user
        And the member account belongs to my organization as "HrAdmin"
        And the member lists the organization's members
        And the member's response status is 200
        When I change the member's role to "Employee"
        And the member lists the organization's members
        Then the member's response status is 403

    Scenario: Promoting an employee takes effect on their next request
        Given I am signed in as the development user
        And the member account belongs to my organization as "Employee"
        And the member lists the organization's members
        And the member's response status is 403
        When I change the member's role to "HrAdmin"
        And the member lists the organization's members
        Then the member's response status is 200

    Scenario: A role value that is not Employee, Manager or HrAdmin grants nothing
        Given I am signed in as the development user
        And the member account belongs to my organization as "HrAdmin"
        And the member's stored role value is 99
        When the member lists the organization's members
        Then the member's response status is 403
        When the member requests "/runs"
        Then the member's response status is 403

    Scenario: A removed member is refused with a token that still names the organization
        Given I am signed in as the development user
        And the member account belongs to my organization as "HrAdmin"
        And the member lists the organization's members
        And the member's response status is 200
        When the member's membership is removed
        And the member lists the organization's members
        Then the member's response status is 403

    Scenario: A member with the Employee role gets 403 when changing a member's role
        Given I am signed in as the development user
        And the member account belongs to my organization as "Employee"
        When the member changes their own role to "HrAdmin"
        Then the member's response status is 403

    Scenario: A member with the Manager role gets 403 when changing a member's role
        Given I am signed in as the development user
        And the member account belongs to my organization as "Manager"
        When the member changes their own role to "HrAdmin"
        Then the member's response status is 403

    Scenario: A member with the HrAdmin role can change a member's role
        Given I am signed in as the development user
        And the member account belongs to my organization as "HrAdmin"
        When the member changes their own role to "HrAdmin"
        Then the member's response status is 200

    Scenario: An HR admin gets 404 for another organization's members
        Given I am signed in as the development user
        And a workspace exists that belongs to somebody else
        When I list the members of the workspace belonging to somebody else
        Then the response status is 404

    Scenario: An HR admin gets 404 when changing a role in another organization
        Given I am signed in as the development user
        And a workspace exists that belongs to somebody else
        When I change my own role in the workspace belonging to somebody else to "HrAdmin"
        Then the response status is 404

    Scenario: A platform operator who entered a workspace gets 403 for HR-admin endpoints
        Given I am signed in as the development user
        And a workspace exists that belongs to somebody else
        And I have entered the workspace belonging to somebody else
        When I list the members of the workspace belonging to somebody else
        Then the response status is 403
        When I change my own role in the workspace belonging to somebody else to "HrAdmin"
        Then the response status is 403
