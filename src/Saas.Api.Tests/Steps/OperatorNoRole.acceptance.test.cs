using System.Text.Json;
using Npgsql;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>Acceptance steps for DIA-123: no role or permissions without a real membership row.</summary>
[Binding]
public class OperatorNoRoleAcceptanceSteps(
    AuthDriver auth,
    ApiDriver api,
    TestSettings settings)
{
    private AuthDriver.TokenPair? _tokens;
    private AuthDriver.TokenPair? _entered;
    private Guid _homeOrganizationId;
    private Guid _foreignOrganizationId;
    private Guid _devOrganizationId;
    private Guid? _ordinaryUserId;
    private bool _membershipInserted;
    private int _status;
    private JsonDocument? _json;

    [Given("opnr the operator has signed in")]
    public async Task OperatorSignedIn()
    {
        _tokens = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _homeOrganizationId = Guid.Parse(AuthDriver.ReadClaim(_tokens.AccessToken, "org_id")!);
        api.Authenticate(_tokens.AccessToken);
    }

    [Given("opnr an ordinary account has signed in")]
    public async Task OrdinarySignedIn()
    {
        _tokens = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        api.Authenticate(_tokens.AccessToken);
    }

    [When("opnr the operator enters a workspace of somebody else")]
    public async Task OperatorEnters()
    {
        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        var memberApi = new ApiDriver();
        memberApi.Authenticate(member.AccessToken);
        using var organizations = await memberApi.GetJsonAsync("/organizations");
        _foreignOrganizationId = organizations.RootElement[0].GetProperty("id").GetGuid();

        var (tokens, status) = await auth.SwitchOrganizationAsync(
            _tokens!.RefreshToken, _foreignOrganizationId);
        _status = status;
        _entered = tokens;

        if (tokens is not null)
        {
            api.Authenticate(tokens.AccessToken);
        }
    }

    [When("opnr the ordinary account tries to enter the development workspace")]
    public async Task OrdinaryTriesToEnter()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        var devOrganization = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);

        var (tokens, status) = await auth.SwitchOrganizationAsync(_tokens!.RefreshToken, devOrganization);
        _entered = tokens;
        _status = status;
    }

    [Given("opnr the ordinary account holds a membership in the development workspace and is switched in")]
    public async Task OrdinaryHoldsMembership()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);

        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _ordinaryUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        await ExecuteAsTenantAsync(
            "INSERT INTO \"Memberships\" (\"Id\", \"UserId\", \"OrganizationId\", \"Role\", \"CreatedDate\") " +
            "VALUES (gen_random_uuid(), @user, @org, 0, now())");
        _membershipInserted = true;

        var (tokens, status) = await auth.SwitchOrganizationAsync(
            member.RefreshToken, _devOrganizationId);
        status.ShouldBeInRange(200, 299);
        api.Authenticate(tokens.ShouldNotBeNull().AccessToken);
    }

    [When("opnr the ordinary account's membership is removed")]
    public async Task RemoveMembership()
    {
        await ExecuteAsTenantAsync(
            "DELETE FROM \"Memberships\" WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");
        _membershipInserted = false;
    }

    [When("opnr the caller requests {string}")]
    public async Task CallerRequests(string path)
    {
        _json?.Dispose();
        _json = null;

        var response = await api.GetAsync(path);
        _status = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            _json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }
    }

    [Then("opnr entering succeeded")]
    public void EnteringSucceeded() =>
        _entered.ShouldNotBeNull($"entering answered {_status}");

    [Then("opnr entering was refused")]
    public void EnteringRefused() => _entered.ShouldBeNull();

    [Then("opnr the answer is {int}")]
    public void AnswerIs(int status) => _status.ShouldBe(status);

    [Then("opnr the access token has no {string} claim")]
    public void NoClaim(string claim) =>
        AuthDriver.ReadClaim(_entered!.AccessToken, claim).ShouldBeNull();

    [Then("opnr the access token has the entered workspace as {string}")]
    public void HasOrgId(string claim) =>
        AuthDriver.ReadClaim(_entered!.AccessToken, claim)
            .ShouldBe(_foreignOrganizationId.ToString());

    [Then("opnr the access token marks the caller as platform admin")]
    public void IsPlatformAdmin() =>
        AuthDriver.ReadClaim(_entered!.AccessToken, "is_platform_admin").ShouldBe("true");

    [Then("opnr the access token has a non-empty {string} claim")]
    public void HasNonEmptyClaim(string claim) =>
        AuthDriver.ReadClaim(_tokens!.AccessToken, claim).ShouldNotBeNullOrWhiteSpace();

    [Then("opnr the access token permission claims include {string}")]
    public void PermissionClaimsInclude(string permission) =>
        (AuthDriver.ReadClaim(_tokens!.AccessToken, "permission") ?? string.Empty)
            .Split(',')
            .ShouldContain(permission);

    [Then("opnr organizationRole is null")]
    public void OrganizationRoleNull() =>
        _json!.RootElement.GetProperty("organizationRole").ValueKind.ShouldBe(JsonValueKind.Null);

    [Then("opnr organizationRole is not null")]
    public void OrganizationRoleNotNull() =>
        _json!.RootElement.GetProperty("organizationRole").ValueKind
            .ShouldNotBe(JsonValueKind.Null);

    [Then("opnr permissions is an empty list")]
    public void PermissionsEmpty()
    {
        var permissions = _json!.RootElement.GetProperty("permissions");
        permissions.ValueKind.ShouldBe(JsonValueKind.Array);
        permissions.GetArrayLength().ShouldBe(0);
    }

    [Then("opnr permissions include {string}")]
    public void PermissionsInclude(string permission) =>
        _json!.RootElement.GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetString())
            .ShouldContain(permission);

    [Then("opnr organization id is the entered workspace and its role is null")]
    public void OrganizationIsEntered()
    {
        var organization = _json!.RootElement.GetProperty("organization");
        organization.GetProperty("id").GetGuid().ShouldBe(_foreignOrganizationId);
        organization.GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Then("opnr organizations hold the home workspace with a real role")]
    public void OrganizationsHoldHome()
    {
        var home = _json!.RootElement.GetProperty("organizations").EnumerateArray()
            .Single(o => o.GetProperty("id").GetGuid() == _homeOrganizationId);
        home.GetProperty("role").ValueKind.ShouldNotBe(JsonValueKind.Null);
    }

    [Then("opnr organizations hold the entered workspace with a null role")]
    public void OrganizationsHoldEntered()
    {
        var entered = _json!.RootElement.GetProperty("organizations").EnumerateArray()
            .Single(o => o.GetProperty("id").GetGuid() == _foreignOrganizationId);
        entered.GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [AfterScenario]
    public async Task Cleanup()
    {
        _json?.Dispose();

        if (_membershipInserted && _ordinaryUserId is not null)
        {
            await ExecuteAsTenantAsync(
                "DELETE FROM \"Memberships\" WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");
        }
    }

    private async Task ExecuteAsTenantAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(settings.PostgresOwner);
        await connection.OpenAsync();

        await using (var tenant = new NpgsqlCommand(
            "SELECT set_config('app.organization_id', @org, false), set_config('app.user_id', @user, false)",
            connection))
        {
            tenant.Parameters.AddWithValue("org", _devOrganizationId.ToString());
            tenant.Parameters.AddWithValue("user", _ordinaryUserId!.Value.ToString());
            await tenant.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user", _ordinaryUserId!.Value);
        command.Parameters.AddWithValue("org", _devOrganizationId);

        await command.ExecuteNonQueryAsync();
    }
}
