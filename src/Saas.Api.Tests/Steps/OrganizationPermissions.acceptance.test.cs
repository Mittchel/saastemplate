using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>Acceptance steps for permission-only organization endpoints (DIA-122).</summary>
[Binding]
public class OrganizationPermissionsAcceptanceSteps(
    AuthDriver auth,
    DatabaseDriver database,
    TestSettings settings)
{
    private readonly HttpClient _devClient = TestHosts.CreateApiClient();
    private readonly HttpClient _memberClient = TestHosts.CreateApiClient();
    private Guid _devOrganizationId;
    private Guid _memberUserId;
    private bool _membershipAdded;
    private int _status;
    private string _body = string.Empty;

    [Given("dia122 the development user is signed in")]
    public async Task DevSignedIn()
    {
        await SignInDevAsync();
        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);
    }

    [Given("dia122 a Member holds a membership in the development organization and is switched in")]
    public async Task MemberSwitchedIn()
    {
        await SignInDevAsync();

        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        await database.RemoveMembershipAsync(_devOrganizationId, _memberUserId);
        _membershipAdded = true;
        await database.AddMembershipAsync(_devOrganizationId, _memberUserId, 0);

        var (switched, status) = await auth.SwitchOrganizationAsync(member.RefreshToken, _devOrganizationId);
        status.ShouldBe(200);
        _memberClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", switched.ShouldNotBeNull().AccessToken);
    }

    [When("dia122 the development user lists the members of another organization")]
    public async Task DevListsForeignMembers() =>
        await SendAsync(_devClient, HttpMethod.Get, $"/organizations/{await ForeignOrganizationIdAsync()}/members");

    [When("dia122 the development user sets the Member role on a member of another organization")]
    public async Task DevPutsForeign() =>
        await SendAsync(
            _devClient,
            HttpMethod.Put,
            $"/organizations/{await ForeignOrganizationIdAsync()}/members/{_memberUserId}",
            new { role = "Member" });

    [When("dia122 the member lists the members of the active organization")]
    public async Task MemberListsMembers() =>
        await SendAsync(_memberClient, HttpMethod.Get, $"/organizations/{_devOrganizationId}/members");

    [When("dia122 the development user lists the members of the development organization")]
    public async Task DevListsMembers() =>
        await SendAsync(_devClient, HttpMethod.Get, $"/organizations/{_devOrganizationId}/members");

    [When("dia122 the member sets the Member role on the member in the active organization")]
    public async Task MemberPuts() =>
        await SendAsync(
            _memberClient,
            HttpMethod.Put,
            $"/organizations/{_devOrganizationId}/members/{_memberUserId}",
            new { role = "Member" });

    [When("dia122 the member lists the invitations of the active organization")]
    public async Task MemberListsInvitations() =>
        await SendAsync(_memberClient, HttpMethod.Get, $"/organizations/{_devOrganizationId}/invitations");

    [When("dia122 the development user promotes the member to Admin")]
    public async Task DevPromotes() =>
        await SendAsync(
            _devClient,
            HttpMethod.Put,
            $"/organizations/{_devOrganizationId}/members/{_memberUserId}",
            new { role = "Admin" });

    [When("dia122 the development user requests its profile")]
    public async Task DevRequestsProfile() => await SendAsync(_devClient, HttpMethod.Get, "/me");

    [Then("dia122 the answer is {int}")]
    public void AnswerIs(int status) => _status.ShouldBe(status);

    [Then("dia122 the answer is 2xx")]
    public void AnswerIsSuccess() => _status.ShouldBeInRange(200, 299);

    [Then("dia122 the profile lists invitation.manage, team.read and profile.own")]
    public void ProfileLists()
    {
        _status.ShouldBe(200);
        _body.ShouldContain("invitation.manage");
        _body.ShouldContain("team.read");
        _body.ShouldContain("profile.own");
    }

    [AfterScenario]
    public async Task Cleanup()
    {
        if (_membershipAdded)
        {
            await database.RemoveMembershipAsync(_devOrganizationId, _memberUserId);
        }
    }

    private async Task SignInDevAsync()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);
        _devClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dev.AccessToken);
    }

    // The member's own personal organization is another organization from the development user's view.
    private async Task<Guid> ForeignOrganizationIdAsync()
    {
        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        using var client = TestHosts.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);
        var response = await client.GetAsync("/organizations");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement[0].GetProperty("id").GetGuid();
        id.ShouldNotBe(_devOrganizationId);

        return id;
    }

    private async Task SendAsync(HttpClient client, HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request);
        _status = (int)response.StatusCode;
        _body = await response.Content.ReadAsStringAsync();
    }
}
