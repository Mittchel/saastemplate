using System.Text.Json;
using Reqnroll;
using Saas.Api.Tests.Contexts;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>
/// Role scenarios: the seeded member account is brought into the development
/// user's organization with a given role, and keeps one access token for the
/// whole scenario so a role change must apply without a new token.
/// </summary>
[Binding]
public class RoleSteps(
    AuthDriver auth,
    ApiDriver api,
    DatabaseDriver database,
    TestSettings settings,
    ApiScenarioContext context)
{
    private readonly ApiDriver _memberApi = new(settings);

    private Guid? _memberUserId;
    private Guid? _organizationId;
    private int _memberStatus;
    private string? _givenRole;
    private bool _rawRoleWritten;

    [Then("the response field {string} is {string}")]
    public void ResponseFieldIs(string path, string expected)
    {
        var element = context.LastJson!.RootElement;

        foreach (var segment in path.Split('.'))
        {
            element = element.GetProperty(segment);
        }

        element.GetString().ShouldBe(expected);
    }

    [Then("no membership or invitation holds an unknown role")]
    public async Task NoUnknownRoles() =>
        (await database.CountRowsWithUnknownRoleAsync()).ShouldBe(0);

    [Given("the member account belongs to my organization as {string}")]
    public async Task MemberBelongsToMyOrganization(string role)
    {
        _organizationId = context.OrganizationId!.Value;
        _memberUserId = await database.FindUserIdAsync(settings.MemberUserEmail)
            ?? throw new InvalidOperationException(
                $"{settings.MemberUserEmail} does not exist; is the stack seeded?");

        // A previous aborted run may have left the row behind.
        await database.RemoveMembershipAsync(_memberUserId.Value, _organizationId.Value);
        await database.AddMembershipAsync(_memberUserId.Value, _organizationId.Value, role);
        _givenRole = role;

        var signedIn = await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword)
            ?? throw new InvalidOperationException("could not sign in as the member account.");

        var (switched, status) = await auth.SwitchOrganizationAsync(
            signedIn.RefreshToken, _organizationId.Value);

        switched.ShouldNotBeNull($"switching the member into the organization returned {status}");
        _memberApi.Authenticate(switched.AccessToken);
    }

    [Given("the member's stored role value is {int}")]
    public async Task SetStoredRoleValue(int value)
    {
        _rawRoleWritten = true;
        await database.SetMembershipRoleValueAsync(_memberUserId!.Value, _organizationId!.Value, value);
    }

    [When("I change the member's role to {string}")]
    public async Task ChangeMemberRole(string role)
    {
        var response = await api.PutAsync(
            $"/organizations/{_organizationId}/members/{_memberUserId}", new { role });

        response.IsSuccessStatusCode.ShouldBeTrue($"changing the role returned {(int)response.StatusCode}");
    }

    [When("the member changes their own role to {string}")]
    public async Task MemberChangesOwnRole(string role)
    {
        var response = await _memberApi.PutAsync(
            $"/organizations/{_organizationId}/members/{_memberUserId}", new { role });
        _memberStatus = (int)response.StatusCode;
    }

    [When("I list the members of the workspace belonging to somebody else")]
    public async Task ListForeignMembers()
    {
        var response = await api.GetAsync($"/organizations/{context.ForeignOrganizationId}/members");
        context.LastStatusCode = (int)response.StatusCode;
    }

    [When("I change my own role in the workspace belonging to somebody else to {string}")]
    public async Task ChangeOwnRoleInForeignWorkspace(string role)
    {
        var userId = await database.FindUserIdAsync(settings.DevUserEmail)
            ?? throw new InvalidOperationException("the development user does not exist.");
        var response = await api.PutAsync(
            $"/organizations/{context.ForeignOrganizationId}/members/{userId}", new { role });
        context.LastStatusCode = (int)response.StatusCode;
    }

    [When("the member's membership is removed")]
    public Task RemoveMemberMembership() =>
        database.RemoveMembershipAsync(_memberUserId!.Value, _organizationId!.Value);

    [When("the member lists the organization's members")]
    [Given("the member lists the organization's members")]
    [Then("the member lists the organization's members")]
    public Task MemberListsMembers() => MemberRequests($"/organizations/{_organizationId}/members");

    [When("the member requests {string}")]
    [Then("the member requests {string}")]
    public async Task MemberRequests(string path)
    {
        var response = await _memberApi.GetAsync(path);
        _memberStatus = (int)response.StatusCode;
    }

    [Given("the member's response status is {int}")]
    [Then("the member's response status is {int}")]
    public void MemberStatusIs(int expected) => _memberStatus.ShouldBe(expected);

    /// <summary>
    /// A scenario that wrote a raw role value puts the given role back first, so
    /// no unknown value survives even if the removal fails; then the membership
    /// this scenario added is removed, leaving the database as it was found.
    /// </summary>
    [AfterScenario("MVP-01")]
    public async Task RemoveMemberFromOrganization()
    {
        if (_memberUserId is { } userId && _organizationId is { } organizationId)
        {
            if (_rawRoleWritten && _givenRole is not null)
            {
                await database.SetMembershipRoleValueAsync(
                    userId, organizationId, DatabaseDriver.RoleValue(_givenRole));
            }

            await database.RemoveMembershipAsync(userId, organizationId);
        }
    }
}
