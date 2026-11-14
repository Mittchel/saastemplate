using System.Text.Json;
using Reqnroll;
using Saas.Api.Tests.Contexts;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>
/// Permissions follow the membership row, not the token.
/// </summary>
/// <remarks>
/// The ordinary account is put into the development user's organization with a
/// membership row written through the owner connection. Every scenario that
/// does so removes the row afterwards, otherwise the PlatformOperator scenario
/// where an ordinary account cannot enter the operator's workspace would fail.
/// </remarks>
[Binding]
public class RoleSteps(
    AuthDriver auth,
    ApiDriver api,
    DatabaseDriver database,
    TestSettings settings,
    ApiScenarioContext context)
{
    private Guid? _sharedOrganizationId;
    private Guid? _memberUserId;
    private AuthDriver.TokenPair? _devTokens;
    private Guid _devOrganizationId;
    private AuthDriver.TokenPair? _memberPersonalTokens;
    private Guid? _foreignOrganizationId;
    private string _lastBody = string.Empty;

    [Given("an ordinary account has been switched into the development user's organization")]
    public async Task OrdinaryAccountSwitchedIn()
    {
        var devTokens = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull("could not sign in as the development user");
        var organizationId = Guid.Parse(AuthDriver.ReadClaim(devTokens.AccessToken, "org_id")!);

        var memberTokens = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull("could not sign in as the ordinary account");
        var userId = Guid.Parse(AuthDriver.ReadClaim(memberTokens.AccessToken, "sub")!);

        // Kept for the later steps: the identity service rate limits token requests, so a scenario signs in once per account.
        _devTokens = devTokens;
        _devOrganizationId = organizationId;
        _memberPersonalTokens = memberTokens;

        // Recorded before the insert so the hook cleans up even if a later step fails.
        _sharedOrganizationId = organizationId;
        _memberUserId = userId;

        // Clear any row a previous run left behind, so the insert cannot hit the unique index.
        await database.RemoveMembershipAsync(organizationId, userId);

        // Role 0 is Member, which may read runs.
        await database.AddMembershipAsync(organizationId, userId, 0);

        var (switched, status) = await auth.SwitchOrganizationAsync(
            memberTokens.RefreshToken, organizationId);

        status.ShouldBe(200);
        switched.ShouldNotBeNull();

        context.Tokens = switched;
        context.OrganizationId = organizationId;
        api.Authenticate(switched.AccessToken);
    }

    [When("the account's role is set to the unknown value {int}")]
    public async Task RoleSetToUnknownValue(int role) =>
        (await database.SetMembershipRoleAsync(_sharedOrganizationId!.Value, _memberUserId!.Value, role))
            .ShouldBe(1);

    [When("the account's membership is removed")]
    public async Task MembershipRemoved()
    {
        (await database.RemoveMembershipAsync(_sharedOrganizationId!.Value, _memberUserId!.Value))
            .ShouldBe(1);

        _memberUserId = null;
    }

    [When("I request the members of my organization")]
    public async Task RequestMembers()
    {
        var response = await api.GetAsync($"/organizations/{context.OrganizationId}/members");

        context.LastStatusCode = (int)response.StatusCode;
    }

    [Given("the development user is signed in as a second caller")]
    public async Task DevelopmentUserSignedInAsSecondCaller()
    {
        _devTokens ??= (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull("could not sign in as the development user");
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(_devTokens.AccessToken, "org_id")!);
    }

    [When("I request the members of another organization")]
    public async Task RequestMembersOfAnotherOrganization()
    {
        var foreignId = await ForeignOrganizationIdAsync();
        var response = await GetAsync(_devTokens!.AccessToken, $"/organizations/{foreignId}/members");

        context.LastStatusCode = (int)response.StatusCode;
        _lastBody = await response.Content.ReadAsStringAsync();
    }

    [When("I change a member's role to Member in another organization")]
    public async Task ChangeRoleInAnotherOrganization()
    {
        var foreignId = await ForeignOrganizationIdAsync();
        // The development user's own id: a real member of its organization, so only the
        // organization check can produce the 404 (without it the call would reach the
        // member lookup in the wrong tenant and fail with a Membership 404 instead).
        var userId = AuthDriver.ReadClaim(_devTokens!.AccessToken, "sub");
        var response = await PutAsync(
            _devTokens!.AccessToken,
            $"/organizations/{foreignId}/members/{userId}", new { role = "Member" });

        context.LastStatusCode = (int)response.StatusCode;
        _lastBody = await response.Content.ReadAsStringAsync();
    }

    [When("I remove a member from another organization")]
    public async Task RemoveMemberFromAnotherOrganization()
    {
        var foreignId = await ForeignOrganizationIdAsync();
        // The development user's own id, a real member of its organization, so only the
        // organization check can produce the 404.
        var userId = AuthDriver.ReadClaim(_devTokens!.AccessToken, "sub");
        var response = await api.SendAsAsync(
            _devTokens!.AccessToken, HttpMethod.Delete, $"/organizations/{foreignId}/members/{userId}");

        context.LastStatusCode = (int)response.StatusCode;
        _lastBody = await response.Content.ReadAsStringAsync();
    }

    [When("I send {word} to another organization's {string}")]
    public async Task SendToAnotherOrganization(string method, string suffix)
    {
        var foreignId = await ForeignOrganizationIdAsync();
        object? body = method switch
        {
            "PUT" when suffix == "" => new { name = "Renamed" },
            "POST" => new { email = "nobody@example.com", role = "Member" },
            _ => null,
        };
        var response = await api.SendAsAsync(
            _devTokens!.AccessToken,
            new HttpMethod(method),
            $"/organizations/{foreignId}{suffix}",
            body);

        context.LastStatusCode = (int)response.StatusCode;
        _lastBody = await response.Content.ReadAsStringAsync();
    }

    [Then("the error names {string}")]
    public void ErrorNames(string what)
    {
        _lastBody.ShouldContain($"{what} was not found");
        _lastBody.ShouldNotContain("Membership");
    }

    [When("I change the account's role in my organization")]
    public async Task ChangeAccountRole()
    {
        var response = await PutAsync(
            context.Tokens!.AccessToken,
            $"/organizations/{context.OrganizationId}/members/{_memberUserId}", new { role = "Member" });

        context.LastStatusCode = (int)response.StatusCode;
    }

    [When("I request the invitations of my organization")]
    public async Task RequestInvitations()
    {
        var response = await api.GetAsync($"/organizations/{context.OrganizationId}/invitations");

        context.LastStatusCode = (int)response.StatusCode;
    }

    [When("the development user promotes the account to Admin")]
    public async Task DevelopmentUserPromotesAccount()
    {
        var response = await PutAsync(
            _devTokens!.AccessToken,
            $"/organizations/{_devOrganizationId}/members/{_memberUserId}", new { role = "Admin" });

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Then("my permissions include {string}")]
    public async Task PermissionsInclude(string permission)
    {
        using var me = await api.GetJsonAsync("/me");
        var permissions = me.RootElement.GetProperty("permissions")
            .EnumerateArray().Select(e => e.GetString()).ToList();

        permissions.ShouldContain(permission);
    }

    // A GET as the given caller, so the shared driver keeps its account.
    private Task<HttpResponseMessage> GetAsync(string accessToken, string path) =>
        api.SendAsAsync(accessToken, HttpMethod.Get, path);

    // A PUT as the given caller, so the shared driver keeps its account.
    private Task<HttpResponseMessage> PutAsync(string accessToken, string path, object body) =>
        api.SendAsAsync(accessToken, HttpMethod.Put, path, body);

    // The ordinary account's own personal organization stands in for "somebody else's".
    private async Task<Guid> ForeignOrganizationIdAsync()
    {
        if (_foreignOrganizationId is { } known)
        {
            return known;
        }

        var tokens = _memberPersonalTokens
            ?? (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
                .ShouldNotBeNull("could not sign in as the ordinary account");
        using var listing = await GetAsync(tokens.AccessToken, "/organizations");
        listing.EnsureSuccessStatusCode();
        using var organizations = JsonDocument.Parse(await listing.Content.ReadAsStringAsync());

        _foreignOrganizationId = organizations.RootElement.EnumerateArray()
            .First(o => o.GetProperty("isPersonal").GetBoolean())
            .GetProperty("id").GetGuid();

        return _foreignOrganizationId.Value;
    }

    [AfterScenario]
    public async Task RemoveAddedMembership()
    {
        // Covers a scenario that failed before removing the row, and the
        // role-99 scenario, whose row would otherwise be left behind.
        if (_sharedOrganizationId is { } organizationId && _memberUserId is { } userId)
        {
            await database.RemoveMembershipAsync(organizationId, userId);
        }
    }
}
