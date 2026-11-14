using System.Text.Json;
using Npgsql;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>Acceptance steps for DIA-123: no role or permissions without a membership row.</summary>
[Binding]
public class NoRoleWithoutMembershipAcceptanceSteps(AuthDriver auth, TestSettings settings)
{
    private readonly ApiDriver _api = new();

    private AuthDriver.TokenPair? _tokens;
    private Guid _homeOrganizationId;
    private Guid _foreignOrganizationId;
    private Guid? _memberUserId;
    private Guid? _devOrganizationId;
    private string? _accessToken;
    private int _enterStatus;
    private int _statusCode;
    private int _meStatus;
    private JsonDocument? _me;
    private Dictionary<Guid, string?> _membershipsBefore = [];

    [Given("the dia123 operator is signed in")]
    public async Task OperatorSignedIn()
    {
        _tokens = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _accessToken = _tokens.AccessToken;
        _homeOrganizationId = Guid.Parse(AuthDriver.ReadClaim(_accessToken, "org_id")!);
        _api.Authenticate(_accessToken);
    }

    [Given("the dia123 operator's memberships are known")]
    public async Task MembershipsKnown()
    {
        using var me = await _api.GetJsonAsync("/me");
        _membershipsBefore = ReadOrganizations(me);
        _membershipsBefore.ShouldContainKey(_homeOrganizationId);
        _membershipsBefore[_homeOrganizationId].ShouldNotBeNull();
    }

    [Given("the dia123 workspace of somebody else exists")]
    public async Task ForeignWorkspaceExists()
    {
        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        var memberApi = new ApiDriver();
        memberApi.Authenticate(member.AccessToken);
        using var organizations = await memberApi.GetJsonAsync("/organizations");
        _foreignOrganizationId = organizations.RootElement[0].GetProperty("id").GetGuid();
    }

    [When("the dia123 operator enters that workspace")]
    public async Task OperatorEnters()
    {
        var (tokens, status) = await auth.SwitchOrganizationAsync(
            _tokens!.RefreshToken, _foreignOrganizationId);
        _enterStatus = status;
        if (tokens is not null)
        {
            _accessToken = tokens.AccessToken;
            _api.Authenticate(tokens.AccessToken);
        }
    }

    [Then("the dia123 entering succeeded")]
    public void EnteringSucceeded() => _enterStatus.ShouldBeInRange(200, 299);

    [Then("the dia123 token has no org_role claim")]
    public void NoOrgRole() =>
        AuthDriver.ReadClaim(_accessToken!, "org_role").ShouldBeNull();

    [Then("the dia123 token has no permission claims")]
    public void NoPermissions() =>
        string.IsNullOrEmpty(AuthDriver.ReadClaim(_accessToken!, "permission")).ShouldBeTrue();

    [Then("the dia123 token carries the org_id of the entered workspace")]
    public void CarriesOrgId() =>
        AuthDriver.ReadClaim(_accessToken!, "org_id").ShouldBe(_foreignOrganizationId.ToString());

    [Then("the dia123 token carries the platform admin claim")]
    public void CarriesPlatformAdmin() =>
        AuthDriver.ReadClaim(_accessToken!, "is_platform_admin").ShouldBe("true");

    [Then("the dia123 token has a non-empty org_role claim")]
    public void HasOrgRole() =>
        AuthDriver.ReadClaim(_accessToken!, "org_role").ShouldNotBeNullOrWhiteSpace();

    [Then("the dia123 token has the permission claim {string}")]
    public void HasPermission(string permission) =>
        (AuthDriver.ReadClaim(_accessToken!, "permission") ?? string.Empty)
            .Split(',')
            .ShouldContain(permission);

    [When("the dia123 caller requests the current user")]
    public async Task RequestMe()
    {
        var response = await _api.GetAsync("/me");
        _meStatus = (int)response.StatusCode;
        if (response.IsSuccessStatusCode)
        {
            _me = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }
    }

    [When("the dia123 caller requests the runs list")]
    public async Task RequestRuns() =>
        _statusCode = (int)(await _api.GetAsync("/runs")).StatusCode;

    [Then("the dia123 runs answer is {int}")]
    public void RunsAnswerIs(int status) => _statusCode.ShouldBe(status);

    [Then("the dia123 current user answer is {int}")]
    public void MeAnswerIs(int status) => _meStatus.ShouldBe(status);

    [Then("the dia123 current user organizationRole is null")]
    public void MeRoleNull() =>
        _me!.RootElement.GetProperty("organizationRole").ValueKind.ShouldBe(JsonValueKind.Null);

    [Then("the dia123 current user organizationRole is {string}")]
    public void MeRoleIs(string role) =>
        _me!.RootElement.GetProperty("organizationRole").GetString().ShouldBe(role);

    [Then("the dia123 current user permissions are empty")]
    public void MePermissionsEmpty()
    {
        var permissions = _me!.RootElement.GetProperty("permissions");
        permissions.ValueKind.ShouldBe(JsonValueKind.Array);
        permissions.GetArrayLength().ShouldBe(0);
    }

    [Then("the dia123 current user has permissions")]
    public void MeHasPermissions()
    {
        var permissions = _me!.RootElement.GetProperty("permissions");
        permissions.ValueKind.ShouldBe(JsonValueKind.Array);
        permissions.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Then("the dia123 current user organization is the entered workspace with role null")]
    public void MeOrganizationIsEntered()
    {
        var organization = _me!.RootElement.GetProperty("organization");
        organization.GetProperty("id").GetGuid().ShouldBe(_foreignOrganizationId);
        organization.GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Then("the dia123 organizations hold the home workspace with its real role")]
    public void OrganizationsHoldHome()
    {
        var organizations = ReadOrganizations(_me!);
        organizations.ShouldContainKey(_homeOrganizationId);
        organizations[_homeOrganizationId].ShouldNotBeNull();
        organizations[_homeOrganizationId].ShouldBe(_membershipsBefore[_homeOrganizationId]);
    }

    [Then("the dia123 organizations hold the entered workspace with role null")]
    public void OrganizationsHoldEntered()
    {
        var organizations = ReadOrganizations(_me!);
        organizations.ShouldContainKey(_foreignOrganizationId);
        organizations[_foreignOrganizationId].ShouldBeNull();
    }

    [Given("the dia123 ordinary account is signed in")]
    public async Task OrdinarySignedIn()
    {
        _tokens = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _accessToken = _tokens.AccessToken;
        _api.Authenticate(_accessToken);
    }

    [When("the dia123 ordinary account enters the development workspace")]
    public async Task OrdinaryEntersDev()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);

        var (_, status) = await auth.SwitchOrganizationAsync(
            _tokens!.RefreshToken, _devOrganizationId.Value);
        _enterStatus = status;
        _devOrganizationId = null;
    }

    [Then("the dia123 entering was refused")]
    public void EnteringRefused() => _enterStatus.ShouldBeInRange(400, 499);

    [Given("the dia123 ordinary account holds a membership in the development workspace and is switched in")]
    public async Task OrdinaryHoldsMembership()
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
        _api.Authenticate(tokens.ShouldNotBeNull().AccessToken);
    }

    [When("the dia123 membership is removed")]
    public async Task MembershipRemoved() =>
        await ExecuteAsTenantAsync(
            _devOrganizationId!.Value,
            _memberUserId!.Value,
            "DELETE FROM \"Memberships\" WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");

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

        _me?.Dispose();
    }

    private static Dictionary<Guid, string?> ReadOrganizations(JsonDocument me)
    {
        var result = new Dictionary<Guid, string?>();
        foreach (var organization in me.RootElement.GetProperty("organizations").EnumerateArray())
        {
            var role = organization.GetProperty("role");
            result[organization.GetProperty("id").GetGuid()] =
                role.ValueKind == JsonValueKind.Null ? null : role.GetString();
        }

        return result;
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
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("org", organizationId);

        await command.ExecuteNonQueryAsync();
    }
}
