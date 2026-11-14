using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>
/// Acceptance steps for permission-only organisation endpoints (DIA-122).
/// The development user's tokens and the member's first sign-in are cached for the
/// whole run, so a scenario adds at most one sign-in (the member's, for the switch).
/// </summary>
[Binding]
public class OrgEndpointPermissionsAcceptanceSteps(
    AuthDriver auth,
    DatabaseDriver database,
    TestSettings settings)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static AuthDriver.TokenPair? _cachedDev;
    private static AuthDriver.TokenPair? _cachedMember;
    private static Guid? _cachedForeignOrganizationId;

    private readonly HttpClient _devClient = TestHosts.CreateApiClient();
    private readonly HttpClient _memberClient = TestHosts.CreateApiClient();

    private Guid _devOrganizationId;
    private Guid _memberUserId;
    private Guid _otherOrganizationId;
    private bool _memberAdded;
    private int _status;
    private string _body = string.Empty;

    [Given("the endpoint test knows another organisation's id")]
    public async Task KnowsOtherOrganisation()
    {
        await EnsureCachedAsync();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(_cachedMember!.AccessToken, "sub")!);

        if (_cachedForeignOrganizationId is null)
        {
            var client = TestHosts.CreateApiClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _cachedMember.AccessToken);
            var response = await client.GetAsync("/organizations");
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            _cachedForeignOrganizationId = document.RootElement.EnumerateArray()
                .First(o => o.GetProperty("isPersonal").GetBoolean())
                .GetProperty("id").GetGuid();
        }

        _otherOrganizationId = _cachedForeignOrganizationId.Value;
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(_cachedDev!.AccessToken, "org_id")!);
        _otherOrganizationId.ShouldNotBe(_devOrganizationId);
        Authorize(_devClient, _cachedDev.AccessToken);
    }

    [Given("the endpoint test member holds role value {int} in the development organisation and is switched in")]
    public async Task MemberSwitchedIn(int role)
    {
        await EnsureCachedAsync();
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(_cachedDev!.AccessToken, "org_id")!);
        Authorize(_devClient, _cachedDev.AccessToken);

        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        await database.RemoveMembershipAsync(_devOrganizationId, _memberUserId);
        _memberAdded = true;
        await database.AddMembershipAsync(_devOrganizationId, _memberUserId, role);

        var (switched, status) = await auth.SwitchOrganizationAsync(member.RefreshToken, _devOrganizationId);
        status.ShouldBe(200);
        Authorize(_memberClient, switched.ShouldNotBeNull().AccessToken);
    }

    [Given("the endpoint test development user is signed in")]
    public async Task DevSignedIn()
    {
        await EnsureCachedAsync();
        Authorize(_devClient, _cachedDev!.AccessToken);
    }

    [When("the development user requests the members of the other organisation")]
    public async Task DevRequestsOtherMembers() =>
        await Record(_devClient.GetAsync($"/organizations/{_otherOrganizationId}/members"));

    [When("the development user sets the member's role to Member in the other organisation")]
    public async Task DevSetsRoleInOther() =>
        await Record(_devClient.PutAsJsonAsync(
            $"/organizations/{_otherOrganizationId}/members/{_memberUserId}",
            new { role = "Member" }));

    [When("the member requests the members of the development organisation")]
    public async Task MemberRequestsMembers() =>
        await Record(_memberClient.GetAsync($"/organizations/{_devOrganizationId}/members"));

    [When("the development user requests the members of the development organisation")]
    public async Task DevRequestsMembers() =>
        await Record(_devClient.GetAsync($"/organizations/{_devOrganizationId}/members"));

    [When("the member sets its own role to Member in the development organisation")]
    public async Task MemberSetsRole() =>
        await Record(_memberClient.PutAsJsonAsync(
            $"/organizations/{_devOrganizationId}/members/{_memberUserId}",
            new { role = "Member" }));

    [When("the member requests the invitations of the development organisation")]
    public async Task MemberRequestsInvitations() =>
        await Record(_memberClient.GetAsync($"/organizations/{_devOrganizationId}/invitations"));

    [When("the development user promotes the member to Admin")]
    public async Task DevPromotes()
    {
        await Record(_devClient.PutAsJsonAsync(
            $"/organizations/{_devOrganizationId}/members/{_memberUserId}",
            new { role = "Admin" }));
        _status.ShouldBeInRange(200, 299);
    }

    [When("the development user requests {string}")]
    public async Task DevRequests(string path) => await Record(_devClient.GetAsync(path));

    [Then("the endpoint answer is {int}")]
    public void AnswerIs(int status) => _status.ShouldBe(status);

    [Then("the endpoint response lists {string}, {string} and {string}")]
    public void ResponseLists(string first, string second, string third)
    {
        _body.ShouldContain(first);
        _body.ShouldContain(second);
        _body.ShouldContain(third);
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
        _status = (int)response.StatusCode;
        _body = await response.Content.ReadAsStringAsync();
    }

    private static void Authorize(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    private async Task EnsureCachedAsync()
    {
        await Gate.WaitAsync();
        try
        {
            _cachedDev ??= (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
                .ShouldNotBeNull("could not sign in as the development user");
            _cachedMember ??= (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
                .ShouldNotBeNull("could not sign in as the member account");
        }
        finally
        {
            Gate.Release();
        }
    }
}
