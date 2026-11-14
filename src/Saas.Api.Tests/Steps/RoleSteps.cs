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

    [Given("an ordinary account has been switched into the development user's organization")]
    public async Task OrdinaryAccountSwitchedIn()
    {
        var devTokens = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull("could not sign in as the development user");
        var organizationId = Guid.Parse(AuthDriver.ReadClaim(devTokens.AccessToken, "org_id")!);

        var memberTokens = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull("could not sign in as the ordinary account");
        var userId = Guid.Parse(AuthDriver.ReadClaim(memberTokens.AccessToken, "sub")!);

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
