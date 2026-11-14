using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>Acceptance steps for DIA-122 (permission-only organization endpoints).</summary>
[Binding]
public class OrgEndpointsPermissionsAcceptanceSteps(
    AuthDriver auth,
    DatabaseDriver database,
    TestSettings settings)
{
    private readonly HttpClient _dev = TestHosts.CreateApiClient();
    private readonly HttpClient _member = TestHosts.CreateApiClient();

    private Guid _devOrganizationId;
    private Guid _devUserId;
    private Guid _memberUserId;
    private Guid _otherOrganizationId;
    private bool _membershipAdded;
    private int _status;
    private string _body = string.Empty;

    [Given("DIA-122 the development user is signed in")]
    public async Task DevSignedIn()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);
        _devUserId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "sub")!);
        _dev.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dev.AccessToken);
    }

    [Given("DIA-122 another organization's id is known")]
    public async Task OtherOrganizationKnown()
    {
        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        using var client = TestHosts.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);
        var response = await client.GetAsync("/organizations");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var items = root.ValueKind == JsonValueKind.Array
            ? root
            : root.EnumerateObject().First(p => p.Value.ValueKind == JsonValueKind.Array).Value;

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("isPersonal", out var personal) && personal.ValueKind == JsonValueKind.True)
            {
                _otherOrganizationId = item.GetProperty("id").GetGuid();
            }
        }

        _otherOrganizationId.ShouldNotBe(Guid.Empty);
        _otherOrganizationId.ShouldNotBe(_devOrganizationId);
    }

    [Given("DIA-122 the member account holds raw role {int} in the development organization and is switched in")]
    public async Task MemberSwitchedIn(int role)
    {
        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        await database.RemoveMembershipAsync(_devOrganizationId, _memberUserId);
        _membershipAdded = true;
        await database.AddMembershipAsync(_devOrganizationId, _memberUserId, role);

        var (tokens, status) = await auth.SwitchOrganizationAsync(member.RefreshToken, _devOrganizationId);
        status.ShouldBe(200);
        _member.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.ShouldNotBeNull().AccessToken);
    }

    [When("DIA-122 the development user lists the members of the other organization")]
    public Task DevListsOther() => Record(_dev.GetAsync($"/organizations/{_otherOrganizationId}/members"));

    [When("DIA-122 the development user sets the role of the other account to {string} in the other organization")]
    public Task DevSetsRoleOther(string role) =>
        Record(_dev.PutAsJsonAsync(
            $"/organizations/{_otherOrganizationId}/members/{_memberUserId}", new { role }));

    [When("DIA-122 the member lists the members of its active organization")]
    public Task MemberListsMembers() => Record(_member.GetAsync($"/organizations/{_devOrganizationId}/members"));

    [When("DIA-122 the development user lists the members of the development organization")]
    public Task DevListsMembers() => Record(_dev.GetAsync($"/organizations/{_devOrganizationId}/members"));

    [When("DIA-122 the member changes its own role to {string} in its active organization")]
    public Task MemberChangesRole(string role) =>
        Record(_member.PutAsJsonAsync(
            $"/organizations/{_devOrganizationId}/members/{_memberUserId}", new { role }));

    [When("DIA-122 the member lists the invitations of its active organization")]
    public Task MemberListsInvitations() =>
        Record(_member.GetAsync($"/organizations/{_devOrganizationId}/invitations"));

    [When("DIA-122 the development user promotes the member to {string}")]
    public Task DevPromotes(string role) =>
        Record(_dev.PutAsJsonAsync(
            $"/organizations/{_devOrganizationId}/members/{_memberUserId}", new { role }));

    [When("DIA-122 the development user requests its profile")]
    public Task DevProfile() => Record(_dev.GetAsync("/me"));

    [Then("DIA-122 the answer is {int}")]
    public void AnswerIs(int status) => _status.ShouldBe(status, _body);

    [Then("DIA-122 the answer is {int} naming {string}")]
    public void AnswerIsNaming(int status, string name)
    {
        _status.ShouldBe(status, _body);
        _body.ShouldContain(name);
        _body.ShouldNotContain("Membership");
    }

    [Then("DIA-122 the profile lists {string}")]
    public void ProfileLists(string permission)
    {
        _status.ShouldBe(200, _body);
        _body.ShouldContain(permission);
    }

    [AfterScenario]
    public async Task Cleanup()
    {
        if (_membershipAdded)
        {
            await database.RemoveMembershipAsync(_devOrganizationId, _memberUserId);
        }
    }

    private async Task Record(Task<HttpResponseMessage> request)
    {
        var response = await request;
        _status = (int)response.StatusCode;
        _body = await response.Content.ReadAsStringAsync();
    }
}
