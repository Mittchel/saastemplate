using Reqnroll;
using Saas.Api.Tests.Contexts;
using Saas.Api.Tests.Drivers;
using Saas.Data.Models;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>
/// Per-request permission resolution (MVP-01, RB-ACCESS-01).
/// </summary>
/// <remarks>
/// The ordinary account is put into the development user's organization by
/// writing a membership row directly, then switched in through the refresh
/// grant. Every scenario that does so removes the row again in
/// <see cref="RemoveMembership"/>, otherwise the account would keep a
/// membership in the operator's workspace and the scenario that expects it to
/// be refused there would start failing.
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

    [Given("I am signed in as an ordinary account who has joined the development user's organization")]
    public async Task SignedInAsMemberOfDevelopmentOrganization()
    {
        var developer = await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword);
        var organizationId = Guid.Parse(
            AuthDriver.ReadClaim(developer.ShouldNotBeNull().AccessToken, "org_id")!);

        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull($"could not sign in as {settings.MemberUserEmail}; is the stack seeded?");
        var userId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        await database.AddMembershipAsync(organizationId, userId, (int)OrganizationRole.Member);

        // Recorded only once the row exists, so the hook removes exactly what was added.
        _sharedOrganizationId = organizationId;
        _memberUserId = userId;

        var (switched, status) = await auth.SwitchOrganizationAsync(member.RefreshToken, organizationId);

        switched.ShouldNotBeNull($"switching into the organization returned {status}");

        context.Tokens = switched;
        context.OrganizationId = organizationId;
        api.Authenticate(switched.AccessToken);
    }

    [When("the membership role is set to {int}")]
    public async Task MembershipRoleIsSetTo(int role) =>
        (await database.SetMembershipRoleAsync(_sharedOrganizationId!.Value, _memberUserId!.Value, role))
            .ShouldBe(1);

    [When("the membership is removed")]
    public async Task MembershipIsRemoved() =>
        (await database.RemoveMembershipAsync(_sharedOrganizationId!.Value, _memberUserId!.Value))
            .ShouldBe(1);

    [When("I request the members of my organization")]
    public async Task RequestMembers() =>
        await RequestAsync($"/organizations/{context.OrganizationId!.Value}/members");

    [AfterScenario]
    public async Task RemoveMembership()
    {
        if (_sharedOrganizationId is { } organizationId && _memberUserId is { } userId)
        {
            // Deleting an already removed row is a no-op.
            await database.RemoveMembershipAsync(organizationId, userId);
        }
    }

    private async Task RequestAsync(string path)
    {
        var response = await api.GetAsync(path);

        context.LastStatusCode = (int)response.StatusCode;
    }
}
