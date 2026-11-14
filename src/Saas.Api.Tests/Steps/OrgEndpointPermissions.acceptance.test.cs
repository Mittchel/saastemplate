using System.Net.Http.Headers;
using System.Net.Http.Json;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>Acceptance steps for permission-only organization endpoints (DIA-122).</summary>
[Binding]
public class OrgEndpointPermissionsAcceptanceSteps(
    AuthDriver auth,
    DatabaseDriver database,
    TestSettings settings)
{
    private readonly HttpClient _dev = TestHosts.CreateApiClient();
    private readonly HttpClient _member = TestHosts.CreateApiClient();
    private Guid _devOrganizationId;
    private Guid _memberUserId;
    private Guid _activeOrganizationId;
    private bool _memberAdded;
    private int _lastStatus;
    private string _lastBody = string.Empty;

    [Given("dia122 the development user is signed in")]
    public async Task DevSignedIn()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);
        _dev.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dev.AccessToken);
    }

    [Given("dia122 the member holds role value {int} in the development organization and is switched in")]
    public async Task MemberSwitchedIn(int role)
    {
        await DevSignedIn();

        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        await database.RemoveMembershipAsync(_devOrganizationId, _memberUserId);
        _memberAdded = true;
        await database.AddMembershipAsync(_devOrganizationId, _memberUserId, role);

        var (tokens, status) = await auth.SwitchOrganizationAsync(member.RefreshToken, _devOrganizationId);
        status.ShouldBeInRange(200, 299);

        _activeOrganizationId = _devOrganizationId;
        _member.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.ShouldNotBeNull().AccessToken);
    }

    [When("dia122 the development user lists the members of another organization")]
    public async Task DevListsForeign() =>
        await Record(_dev.GetAsync($"/organizations/{await ForeignOrganizationIdAsync()}/members"));

    [When("dia122 the development user sets a member role in another organization")]
    public async Task DevUpdatesForeign()
    {
        var foreignId = await ForeignOrganizationIdAsync();
        await Record(_dev.PutAsJsonAsync(
            $"/organizations/{foreignId}/members/{Guid.NewGuid()}", new { role = "Member" }));
    }

    [When("dia122 the member lists the members of the active organization")]
    public async Task MemberListsMembers() =>
        await Record(_member.GetAsync($"/organizations/{_activeOrganizationId}/members"));

    [When("dia122 the development user lists the members of the development organization")]
    public async Task DevListsMembers() =>
        await Record(_dev.GetAsync($"/organizations/{_devOrganizationId}/members"));

    [When("dia122 the member updates a role in the active organization")]
    public async Task MemberUpdatesRole() =>
        await Record(_member.PutAsJsonAsync(
            $"/organizations/{_activeOrganizationId}/members/{_memberUserId}", new { role = "Admin" }));

    [When("dia122 the member lists the invitations of the active organization")]
    public async Task MemberListsInvitations() =>
        await Record(_member.GetAsync($"/organizations/{_activeOrganizationId}/invitations"));

    [When("dia122 the development user promotes the member to Admin")]
    public async Task DevPromotes() =>
        await Record(_dev.PutAsJsonAsync(
            $"/organizations/{_devOrganizationId}/members/{_memberUserId}", new { role = "Admin" }));

    [When("dia122 the development user requests their profile")]
    public async Task DevRequestsProfile() => await Record(_dev.GetAsync("/me"));

    [Then("dia122 the answer is {int}")]
    public void AnswerIs(int status) => _lastStatus.ShouldBe(status);

    [Then("dia122 the profile lists the permissions invitation.manage, team.read and profile.own")]
    public void ProfileListsPermissions()
    {
        _lastStatus.ShouldBe(200);
        foreach (var permission in new[] { "invitation.manage", "team.read", "profile.own" })
        {
            _lastBody.ShouldContain($"\"{permission}\"");
        }
    }

    [AfterScenario]
    public async Task Cleanup()
    {
        if (_memberAdded)
        {
            await database.RemoveMembershipAsync(_devOrganizationId, _memberUserId);
        }
    }

    private async Task Record(Task<HttpResponseMessage> request)
    {
        var response = await request;
        _lastStatus = (int)response.StatusCode;
        _lastBody = await response.Content.ReadAsStringAsync();
    }

    private async Task<Guid> ForeignOrganizationIdAsync()
    {
        // The seeded member account's own personal organization is "another organization".
        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        using var client = TestHosts.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);
        var response = await client.GetAsync("/organizations");
        response.EnsureSuccessStatusCode();
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement[0].GetProperty("id").GetGuid();
        id.ShouldNotBe(_devOrganizationId);

        return id;
    }
}
