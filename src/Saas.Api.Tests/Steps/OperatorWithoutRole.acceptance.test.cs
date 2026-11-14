using System.Text.Json;
using Npgsql;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>Acceptance steps for DIA-123: no role or permissions without a membership row.</summary>
[Binding]
public class OperatorWithoutRoleAcceptanceSteps(
    AuthDriver auth,
    ApiDriver api,
    TestSettings settings)
{
    private AuthDriver.TokenPair? _devTokens;
    private AuthDriver.TokenPair? _entered;
    private AuthDriver.TokenPair? _memberTokens;
    private Guid _homeOrgId;
    private Guid _foreignOrgId;
    private Guid _memberUserId;
    private int _status;
    private JsonDocument? _me;

    [Given("dia123 the operator is signed in and a foreign workspace exists")]
    public async Task OperatorSignedIn()
    {
        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        var memberApi = new ApiDriver();
        memberApi.Authenticate(member.AccessToken);
        using var organizations = await memberApi.GetJsonAsync("/organizations");
        _foreignOrgId = organizations.RootElement[0].GetProperty("id").GetGuid();

        _devTokens = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _homeOrgId = Guid.Parse(AuthDriver.ReadClaim(_devTokens.AccessToken, "org_id")!);
        api.Authenticate(_devTokens.AccessToken);
    }

    [When("dia123 the operator enters the foreign workspace")]
    public async Task OperatorEnters()
    {
        var (tokens, status) = await auth.SwitchOrganizationAsync(_devTokens!.RefreshToken, _foreignOrgId);
        _status = status;
        _entered = tokens;
        if (tokens is not null)
        {
            api.Authenticate(tokens.AccessToken);
        }
    }

    [Then("dia123 entering succeeded")]
    public void EnteringSucceeded() => _entered.ShouldNotBeNull($"entering answered {_status}");

    [Then("dia123 the token has no org_role claim and no permission claims")]
    public void NoRoleNoPermissions()
    {
        var token = _entered.ShouldNotBeNull().AccessToken;
        AuthDriver.ReadClaim(token, "org_role").ShouldBeNull();
        AuthDriver.ReadClaim(token, "permission").ShouldBeNullOrEmpty();
    }

    [Then("dia123 the token still carries the entered org_id and the platform-admin claim")]
    public void OrgIdAndAdminClaim()
    {
        var token = _entered.ShouldNotBeNull().AccessToken;
        AuthDriver.ReadClaim(token, "org_id").ShouldBe(_foreignOrgId.ToString());
        AuthDriver.ReadClaim(token, "is_platform_admin").ShouldBe("true");
    }

    [When("dia123 the operator requests the current user")]
    [When("dia123 the member requests the current user")]
    public async Task RequestMe()
    {
        var response = await api.GetAsync("/me");
        _status = (int)response.StatusCode;
        _me?.Dispose();
        _me = response.IsSuccessStatusCode
            ? JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            : null;
    }

    [Then("dia123 the current user answer is {int}")]
    public void MeAnswer(int status) => _status.ShouldBe(status);

    [Then("dia123 the current user has a null organizationRole and empty permissions")]
    public void MeNullRole()
    {
        var root = _me.ShouldNotBeNull().RootElement;
        root.GetProperty("organizationRole").ValueKind.ShouldBe(JsonValueKind.Null);
        var permissions = root.GetProperty("permissions");
        permissions.ValueKind.ShouldBe(JsonValueKind.Array);
        permissions.GetArrayLength().ShouldBe(0);
    }

    [Then("dia123 the current user organization is the foreign workspace with a null role")]
    public void MeOrganization()
    {
        var organization = _me.ShouldNotBeNull().RootElement.GetProperty("organization");
        organization.GetProperty("id").GetGuid().ShouldBe(_foreignOrgId);
        organization.GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Then("dia123 the organizations list has the home workspace with a role and the foreign workspace with a null role")]
    public void MeOrganizations()
    {
        var list = _me.ShouldNotBeNull().RootElement.GetProperty("organizations")
            .EnumerateArray().ToList();

        var home = list.Single(o => o.GetProperty("id").GetGuid() == _homeOrgId);
        home.GetProperty("role").ValueKind.ShouldNotBe(JsonValueKind.Null);

        var foreign = list.Single(o => o.GetProperty("id").GetGuid() == _foreignOrgId);
        foreign.GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [When("dia123 the operator requests the runs list")]
    public async Task RequestRuns() => _status = (int)(await api.GetAsync("/runs")).StatusCode;

    [Then("dia123 the runs answer is {int}")]
    public void RunsAnswer(int status) => _status.ShouldBe(status);

    [Then("dia123 the token has an org_role claim and the permission organization.read")]
    public void DevTokenHasRole()
    {
        var token = _devTokens.ShouldNotBeNull().AccessToken;
        AuthDriver.ReadClaim(token, "org_role").ShouldNotBeNullOrWhiteSpace();
        (AuthDriver.ReadClaim(token, "permission") ?? string.Empty)
            .Split(',').ShouldContain("organization.read");
    }

    [Given("dia123 an ordinary account is signed in and the operator's workspace is known")]
    public async Task OrdinarySignedIn()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _homeOrgId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);
        _memberTokens = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
    }

    [When("dia123 the ordinary account enters the operator's workspace")]
    public async Task OrdinaryEnters()
    {
        var (tokens, status) = await auth.SwitchOrganizationAsync(_memberTokens!.RefreshToken, _homeOrgId);
        _entered = tokens;
        _status = status;
    }

    [Then("dia123 entering was refused")]
    public void EnteringRefused() => _entered.ShouldBeNull();

    [Given("dia123 the member holds a membership in the development organization and is switched in")]
    public async Task MemberSwitchedIn()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _homeOrgId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);

        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        await ExecuteAsync(
            "INSERT INTO \"Memberships\" (\"Id\", \"UserId\", \"OrganizationId\", \"Role\", \"CreatedDate\") " +
            "VALUES (gen_random_uuid(), @user, @org, 0, now())");

        var (tokens, status) = await auth.SwitchOrganizationAsync(member.RefreshToken, _homeOrgId);
        status.ShouldBeInRange(200, 299);
        _memberTokens = tokens;
        api.Authenticate(tokens.ShouldNotBeNull().AccessToken);
    }

    [When("dia123 the member's membership is removed")]
    public async Task RemoveMembership() =>
        await ExecuteAsync(
            "DELETE FROM \"Memberships\" WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");

    [Then("dia123 the current user has a non-null organizationRole and some permissions")]
    public void MeHasRole()
    {
        var root = _me.ShouldNotBeNull().RootElement;
        root.GetProperty("organizationRole").ValueKind.ShouldNotBe(JsonValueKind.Null);
        root.GetProperty("permissions").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [AfterScenario]
    public async Task Cleanup()
    {
        _me?.Dispose();
        if (_memberUserId != Guid.Empty)
        {
            await ExecuteAsync(
                "DELETE FROM \"Memberships\" WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");
        }
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(settings.PostgresOwner);
        await connection.OpenAsync();

        await using (var tenant = new NpgsqlCommand(
            "SELECT set_config('app.organization_id', @org, false), set_config('app.user_id', @user, false)",
            connection))
        {
            tenant.Parameters.AddWithValue("org", _homeOrgId.ToString());
            tenant.Parameters.AddWithValue("user", _memberUserId.ToString());
            await tenant.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user", _memberUserId);
        command.Parameters.AddWithValue("org", _homeOrgId);
        await command.ExecuteNonQueryAsync();
    }
}
