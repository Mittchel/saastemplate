using System.Net.Http.Headers;
using System.Net.Http.Json;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>Acceptance steps for permission-only organisation endpoints (DIA-122).</summary>
[Binding]
public class RolesOrgEndpointsAcceptanceSteps(
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
    private bool _memberAdded;
    private int _status;
    private string _body = string.Empty;

    [Given("the endpoint caller is the development user and knows another organisation's id")]
    public async Task DevKnowsOtherOrganisation()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword)).ShouldNotBeNull();
        _dev.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dev.AccessToken);
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);
        _devUserId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "sub")!);

        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword)).ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);
        _member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);

        var response = await _member.GetAsync("/organizations");
        response.EnsureSuccessStatusCode();
        using var organizations = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        _otherOrganizationId = organizations.RootElement[0].GetProperty("id").GetGuid();
        _otherOrganizationId.ShouldNotBe(_devOrganizationId);
    }

    [Given("the endpoint member holds role {int} in the development organisation and is switched in")]
    public async Task MemberSwitchedIn(int role)
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword)).ShouldNotBeNull();
        _dev.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dev.AccessToken);
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);

        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword)).ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        await database.RemoveMembershipAsync(_devOrganizationId, _memberUserId);
        _memberAdded = true;
        await database.AddMembershipAsync(_devOrganizationId, _memberUserId, role);

        var (tokens, status) = await auth.SwitchOrganizationAsync(member.RefreshToken, _devOrganizationId);
        status.ShouldBe(200);
        _member.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.ShouldNotBeNull().AccessToken);
    }

    [When("the endpoint caller lists the members of the other organisation")]
    public Task DevListsOther() => Send(_dev, HttpMethod.Get, $"/organizations/{_otherOrganizationId}/members");

    [When("the endpoint caller sets the role of a user to {string} in the other organisation")]
    public Task DevSetsRoleOther(string role) =>
        Send(_dev, HttpMethod.Put, $"/organizations/{_otherOrganizationId}/members/{_memberUserId}", new { role });

    [When("the endpoint member lists the members of the active organisation")]
    public Task MemberListsMembers() => Send(_member, HttpMethod.Get, $"/organizations/{_devOrganizationId}/members");

    [When("the endpoint development user lists the members of the active organisation")]
    public Task DevListsMembers() => Send(_dev, HttpMethod.Get, $"/organizations/{_devOrganizationId}/members");

    [When("the endpoint member updates its own role to {string} in the active organisation")]
    public Task MemberUpdates(string role) =>
        Send(_member, HttpMethod.Put, $"/organizations/{_devOrganizationId}/members/{_memberUserId}", new { role });

    [When("the endpoint member lists the invitations of the active organisation")]
    public Task MemberListsInvitations() =>
        Send(_member, HttpMethod.Get, $"/organizations/{_devOrganizationId}/invitations");

    [When("the endpoint development user promotes the member to {string}")]
    public Task DevPromotes(string role) =>
        Send(_dev, HttpMethod.Put, $"/organizations/{_devOrganizationId}/members/{_memberUserId}", new { role });

    [When("the endpoint caller requests its profile")]
    public Task RequestProfile() => Send(_dev, HttpMethod.Get, "/me");

    [Then("the endpoint answer is {int}")]
    public void AnswerIs(int status) => _status.ShouldBe(status);

    [Then("the endpoint profile lists {string}, {string} and {string}")]
    public void ProfileLists(string a, string b, string c)
    {
        _status.ShouldBe(200);
        _body.ShouldContain(a);
        _body.ShouldContain(b);
        _body.ShouldContain(c);
    }

    [AfterScenario]
    public async Task Cleanup()
    {
        if (_memberAdded)
        {
            await database.RemoveMembershipAsync(_devOrganizationId, _memberUserId);
        }
    }

    private async Task Send(HttpClient client, HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await client.SendAsync(request);
        _status = (int)response.StatusCode;
        _body = await response.Content.ReadAsStringAsync();
    }
}
