using Npgsql;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>Acceptance steps for per-request permission resolution (DIA-121).</summary>
[Binding]
public class RolesPermissionsAcceptanceSteps(
    AuthDriver auth,
    ApiDriver api,
    DatabaseDriver database,
    TestSettings settings)
{
    private Guid? _memberUserId;
    private Guid? _devOrganizationId;
    private Guid _activeOrganizationId;
    private Guid _homeOrganizationId;
    private Guid _foreignOrganizationId;
    private int _lastStatus;

    [Given("the permission member holds a Member membership in the development organization and is switched in")]
    public async Task MemberIsSwitchedIn()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);

        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        await ExecuteAsTenantAsync(
            _devOrganizationId.Value,
            _memberUserId.Value,
            "INSERT INTO \"Memberships\" (\"Id\", \"UserId\", \"OrganizationId\", \"Role\", \"CreatedDate\") " +
            "VALUES (gen_random_uuid(), @user, @org, 0, now())");

        var (tokens, status) = await auth.SwitchOrganizationAsync(
            member.RefreshToken, _devOrganizationId.Value);
        status.ShouldBeInRange(200, 299);

        _activeOrganizationId = _devOrganizationId.Value;
        api.Authenticate(tokens.ShouldNotBeNull().AccessToken);
    }

    [When("the permission member's role value is set to {int}")]
    public async Task SetRole(int role) =>
        await ExecuteAsTenantAsync(
            _devOrganizationId!.Value,
            _memberUserId!.Value,
            "UPDATE \"Memberships\" SET \"Role\" = " + role +
            " WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");

    [When("the permission member's membership is removed")]
    public async Task RemoveMembership() =>
        await ExecuteAsTenantAsync(
            _devOrganizationId!.Value,
            _memberUserId!.Value,
            "DELETE FROM \"Memberships\" WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");

    [Given("the permission development user is signed in")]
    public async Task DevSignedIn()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _activeOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);
        api.Authenticate(dev.AccessToken);
    }

    [Given("the permission operator has created a run in the home workspace")]
    public async Task OperatorCreatedRun()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _homeOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);
        _devRefresh = dev.RefreshToken;
        api.Authenticate(dev.AccessToken);

        var (runId, status) = await api.CreateRunAsync("acceptance " + Guid.CreateVersion7().ToString("n"));
        runId.ShouldNotBeNull($"creating a run answered {status}");
    }

    private string _devRefresh = string.Empty;

    [When("the permission operator enters a workspace of somebody else")]
    public async Task OperatorEnters()
    {
        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        var memberApi = new ApiDriver();
        memberApi.Authenticate(member.AccessToken);
        using var organizations = await memberApi.GetJsonAsync("/organizations");
        _foreignOrganizationId = organizations.RootElement[0].GetProperty("id").GetGuid();

        var (tokens, status) = await auth.SwitchOrganizationAsync(_devRefresh, _foreignOrganizationId);
        status.ShouldBeInRange(200, 299);

        _activeOrganizationId = _foreignOrganizationId;
        api.Authenticate(tokens.ShouldNotBeNull().AccessToken);
    }

    [When("the permission caller requests the runs list")]
    public async Task RequestRuns() =>
        _lastStatus = (int)(await api.GetAsync("/runs")).StatusCode;

    [When("the permission caller requests the members list")]
    public async Task RequestMembers() =>
        _lastStatus = (int)(await api.GetAsync($"/organizations/{_activeOrganizationId}/members")).StatusCode;

    [Then("the permission answer is {int}")]
    public void AnswerIs(int status) => _lastStatus.ShouldBe(status);

    [Then("the entered workspace shows 0 runs when counted directly")]
    public async Task EnteredShowsNone() =>
        (await database.CountAsTenantAsync("Runs", _foreignOrganizationId)).ShouldBe(0);

    [Then("the run still exists in its home workspace")]
    public async Task RunStillInHome() =>
        (await database.CountAsTenantAsync("Runs", _homeOrganizationId)).ShouldBeGreaterThan(0);

    [AfterScenario]
    public async Task Cleanup()
    {
        if (_memberUserId is not null && _devOrganizationId is not null)
        {
            await ExecuteAsTenantAsync(
                _devOrganizationId.Value,
                _memberUserId.Value,
                "DELETE FROM \"Memberships\" WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");
        }
    }

    private async Task ExecuteAsTenantAsync(Guid organizationId, Guid userId, string sql)
    {
        await using var connection = new NpgsqlConnection(settings.PostgresOwner);
        await connection.OpenAsync();

        await using (var tenant = new NpgsqlCommand(
            "SELECT set_config('app.organization_id', @org, false), set_config('app.user_id', @user, false)",
            connection))
        {
            tenant.Parameters.AddWithValue("org", organizationId.ToString());
            tenant.Parameters.AddWithValue("user", userId.ToString());
            await tenant.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection);
        if (sql.Contains("@user", StringComparison.Ordinal))
        {
            command.Parameters.AddWithValue("user", userId);
            command.Parameters.AddWithValue("org", organizationId);
        }

        await command.ExecuteNonQueryAsync();
    }
}
