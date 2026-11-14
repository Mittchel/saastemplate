using Reqnroll;
using Saas.Api.Tests.Contexts;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>
/// The operator boundary: what crossing tenants is allowed to buy, and what it
/// deliberately is not.
/// </summary>
/// <remarks>
/// <para>
/// "Somebody else" is the seeded <c>member@saas.local</c> account and its own
/// personal workspace, so the workspace under test is genuinely one the
/// operator has no membership of. The same account doubles as the ordinary
/// caller for the scenarios that check an unprivileged user is refused.
/// </para>
/// <para>
/// It is seeded rather than registered on the fly because registration is rate
/// limited to 5 per minute per IP on purpose. A fixture that registered its own
/// accounts would make the suite fail against its own defences on the second
/// consecutive run -- the limit is right, so the fixture gives way.
/// </para>
/// <para>
/// Entering is phrased separately from switching because the two take different
/// paths through the token endpoint.
/// </para>
/// </remarks>
[Binding]
public class PlatformOperatorSteps(
    AuthDriver auth,
    ApiDriver api,
    DatabaseDriver database,
    TestSettings settings,
    ApiScenarioContext context)
{
    // Resolved once per run and shared. These are seeded accounts whose
    // personal workspace ids do not change while the suite is running, and the
    // identity service is rate limited per IP -- so looking them up again in
    // every scenario would spend the budget the scenarios themselves need.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static Guid? _somebodyElsesWorkspace;
    private static Guid? _operatorsWorkspace;

    private AuthDriver.TokenPair? _entered;
    private int _enterStatus;

    [Given("a workspace exists that belongs to somebody else")]
    public async Task AWorkspaceBelongsToSomebodyElse()
    {
        await Gate.WaitAsync();

        try
        {
            if (_somebodyElsesWorkspace is null)
            {
                var tokens = await SignInAsMemberAsync();

                // A driver of its own, so establishing the fixture does not
                // disturb whoever the scenario goes on to sign in as.
                var theirs = new ApiDriver();
                theirs.Authenticate(tokens.AccessToken);

                using var organizations = await theirs.GetJsonAsync("/organizations");

                _somebodyElsesWorkspace =
                    organizations.RootElement[0].GetProperty("id").GetGuid();
            }
        }
        finally
        {
            Gate.Release();
        }

        context.ForeignOrganizationId = _somebodyElsesWorkspace;
    }

    [Given("I am signed in as an ordinary account")]
    public async Task SignedInAsOrdinaryAccount()
    {
        // Signed in afresh rather than reusing the token the Background took,
        // so no scenario inherits another's single-use refresh token.
        var tokens = await SignInAsMemberAsync();

        context.Tokens = tokens;
        context.OrganizationId = Guid.Parse(AuthDriver.ReadClaim(tokens.AccessToken, "org_id")!);

        api.Authenticate(tokens.AccessToken);
    }

    [Then("the access token marks me as a platform operator")]
    public void TokenMarksOperator() =>
        AuthDriver.ReadClaim(context.Tokens!.AccessToken, "is_platform_admin").ShouldBe("true");

    [Then("the access token does not mark me as a platform operator")]
    public void TokenDoesNotMarkOperator() =>
        AuthDriver.ReadClaim(context.Tokens!.AccessToken, "is_platform_admin").ShouldBeNull();

    [Then("the workspace belonging to somebody else is listed")]
    public void ForeignWorkspaceIsListed() =>
        context.LastJson!.RootElement.EnumerateArray()
            .Select(o => o.GetProperty("id").GetGuid())
            .ShouldContain(context.ForeignOrganizationId!.Value);

    [Given("the workspace belonging to somebody else is not in my organization list")]
    public async Task ForeignWorkspaceIsNotMine()
    {
        // Proves the operator has no membership of it: this list is exactly
        // what the memberships row-level security policy lets them see.
        using var document = await api.GetJsonAsync("/organizations");

        document.RootElement.EnumerateArray()
            .Select(o => o.GetProperty("id").GetGuid())
            .ShouldNotContain(context.ForeignOrganizationId!.Value);
    }

    [When("I enter the workspace belonging to somebody else")]
    [Given("I have entered the workspace belonging to somebody else")]
    public async Task EnterForeignWorkspace()
    {
        (_entered, _enterStatus) = await auth.SwitchOrganizationAsync(
            context.Tokens!.RefreshToken, context.ForeignOrganizationId!.Value);

        if (_entered is not null)
        {
            context.HomeOrganizationId = context.OrganizationId;
            context.Tokens = _entered;
            context.OrganizationId = context.ForeignOrganizationId;
            api.Authenticate(_entered.AccessToken);
        }
    }

    [When("I enter the operator's own workspace")]
    public async Task EnterTheOperatorsWorkspace()
    {
        // A workspace that demonstrably exists and demonstrably is not the
        // caller's. The refusal has to come from the missing membership, not
        // from the organization being absent -- which is what distinguishes
        // this from the "organization I am not a member of" scenario, where a
        // random id would be refused either way.
        await Gate.WaitAsync();

        try
        {
            if (_operatorsWorkspace is null)
            {
                var operatorTokens = await auth.SignInAsync(
                    settings.DevUserEmail, settings.DevUserPassword);

                _operatorsWorkspace = Guid.Parse(
                    AuthDriver.ReadClaim(
                        operatorTokens.ShouldNotBeNull().AccessToken, "org_id")!);
            }
        }
        finally
        {
            Gate.Release();
        }

        context.ForeignOrganizationId = _operatorsWorkspace;

        await EnterForeignWorkspace();
    }

    [Then("entering succeeds")]
    public void EnteringSucceeds() =>
        _entered.ShouldNotBeNull($"entering returned {_enterStatus}");

    [Then("entering is refused")]
    public void EnteringIsRefused() => _entered.ShouldBeNull();

    [Then("my own workspace's run is no longer visible to me")]
    public async Task OwnRunIsNoLongerVisible()
    {
        // The point of the scenario. Entering somebody else's workspace is not
        // additive: the operator trades their own tenant for that one. They
        // have no membership there, so the API refuses /runs outright (that
        // status is asserted by the scenario) rather than listing anything.
        // The database agrees, asked directly as the runtime role -- so the
        // scoping is the row-level security policy, not an EF query filter.
        var entered = await database.CountAsTenantAsync(
            "Runs", context.ForeignOrganizationId!.Value);

        entered.ShouldBe(0, "the entered workspace must not show another tenant's runs");
    }

    [Then("the run is still there for the workspace it belongs to")]
    public async Task RunStillExistsInItsOwnTenant()
    {
        // Guards against the assertion above passing for the wrong reason: the
        // run must genuinely exist and merely be out of scope, not be missing.
        var home = await database.CountAsTenantAsync(
            "Runs", context.HomeOrganizationId!.Value);

        home.ShouldBeGreaterThan(0, "the run should still exist in its own tenant");
    }

    private async Task<AuthDriver.TokenPair> SignInAsMemberAsync() =>
        (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull(
                $"could not sign in as {settings.MemberUserEmail}; is the stack seeded?");
}
