using System.Text.Json;
using Npgsql;
using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

/// <summary>Acceptance steps for "no role or permissions without a membership row" (DIA-123).</summary>
[Binding]
public class NoRoleWithoutMembershipAcceptanceSteps(
    AuthDriver auth,
    ApiDriver api,
    TestSettings settings)
{
    private static Guid? _foreignWorkspace;

    private AuthDriver.TokenPair? _operator;
    private AuthDriver.TokenPair? _ordinary;
    private AuthDriver.TokenPair? _current;
    private Guid _homeWorkspace;
    private Guid _foreignId;
    private Guid _operatorsWorkspace;
    private AuthDriver.TokenPair? _entered;
    private int _status;
    private JsonDocument? _json;
    private Guid? _devOrganizationId;
    private Guid? _memberUserId;

    [Given("the nr operator is signed in")]
    public async Task OperatorSignedIn()
    {
        _operator = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _current = _operator;
        _homeWorkspace = Guid.Parse(AuthDriver.ReadClaim(_operator.AccessToken, "org_id")!);
        api.Authenticate(_operator.AccessToken);
    }

    [Given("the nr foreign workspace exists")]
    public async Task ForeignWorkspaceExists()
    {
        if (_foreignWorkspace is null)
        {
            var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
                .ShouldNotBeNull();
            var theirs = new ApiDriver();
            theirs.Authenticate(member.AccessToken);
            using var organizations = await theirs.GetJsonAsync("/organizations");
            _foreignWorkspace = organizations.RootElement[0].GetProperty("id").GetGuid();
        }

        _foreignId = _foreignWorkspace.Value;
    }

    [When("the nr operator enters the foreign workspace")]
    public async Task OperatorEnters()
    {
        var (tokens, status) = await auth.SwitchOrganizationAsync(_operator!.RefreshToken, _foreignId);
        _status = status;
        _entered = tokens;

        if (tokens is not null)
        {
            _current = tokens;
            api.Authenticate(tokens.AccessToken);
        }
    }

    [Then("the nr entering succeeds")]
    public void EnteringSucceeds() => _entered.ShouldNotBeNull($"entering answered {_status}");

    [Then("the nr entering is refused")]
    public void EnteringRefused() => _entered.ShouldBeNull();

    [Then("the nr token has no org_role claim")]
    public void NoOrgRole() =>
        AuthDriver.ReadClaim(_current!.AccessToken, "org_role").ShouldBeNull();

    [Then("the nr token has no permission claims")]
    public void NoPermissions()
    {
        var value = AuthDriver.ReadClaim(_current!.AccessToken, "permission");
        string.IsNullOrEmpty(value).ShouldBeTrue($"unexpected permission claim: {value}");
    }

    [Then("the nr token carries the entered workspace as org_id")]
    public void CarriesOrgId() =>
        AuthDriver.ReadClaim(_current!.AccessToken, "org_id").ShouldBe(_foreignId.ToString());

    [Then("the nr token marks the caller as a platform operator")]
    public void MarksOperator() =>
        AuthDriver.ReadClaim(_current!.AccessToken, "is_platform_admin").ShouldBe("true");

    [Then("the nr token has a non-empty org_role claim")]
    public void HasOrgRole() =>
        string.IsNullOrEmpty(AuthDriver.ReadClaim(_current!.AccessToken, "org_role")).ShouldBeFalse();

    [Then("the nr token has the permission claim {string}")]
    public void HasPermission(string permission) =>
        (AuthDriver.ReadClaim(_current!.AccessToken, "permission") ?? string.Empty)
            .Split(',')
            .ShouldContain(permission);

    [When("the nr caller requests {string}")]
    public async Task Request(string path)
    {
        var response = await api.GetAsync(path);
        _status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync();
        _json?.Dispose();
        _json = response.IsSuccessStatusCode && body.Length > 0 ? JsonDocument.Parse(body) : null;
    }

    [Then("the nr response status is {int}")]
    public void StatusIs(int status) => _status.ShouldBe(status);

    [Then("the nr current user organizationRole is null")]
    public void OrganizationRoleNull() =>
        _json.ShouldNotBeNull().RootElement.GetProperty("organizationRole").ValueKind
            .ShouldBe(JsonValueKind.Null);

    [Then("the nr current user organizationRole is not null")]
    public void OrganizationRoleNotNull() =>
        _json.ShouldNotBeNull().RootElement.GetProperty("organizationRole").ValueKind
            .ShouldNotBe(JsonValueKind.Null);

    [Then("the nr current user permissions are empty")]
    public void PermissionsEmpty()
    {
        var permissions = _json.ShouldNotBeNull().RootElement.GetProperty("permissions");
        permissions.ValueKind.ShouldBe(JsonValueKind.Array);
        permissions.GetArrayLength().ShouldBe(0);
    }

    [Then("the nr current user organization is the entered workspace with role null")]
    public void OrganizationIsEntered()
    {
        var organization = _json.ShouldNotBeNull().RootElement.GetProperty("organization");
        organization.GetProperty("id").GetGuid().ShouldBe(_foreignId);
        organization.GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Then("the nr organizations list holds the home workspace with a real role")]
    public void ListHoldsHome()
    {
        var entry = FindOrganization(_homeWorkspace);
        entry.GetProperty("role").ValueKind.ShouldNotBe(JsonValueKind.Null);
    }

    [Then("the nr organizations list holds the entered workspace with role null")]
    public void ListHoldsEntered()
    {
        var entry = FindOrganization(_foreignId);
        entry.GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Then("the nr current user permissions equal the token permission claims")]
    public void PermissionsMatchToken()
    {
        var fromMe = _json.ShouldNotBeNull().RootElement.GetProperty("permissions")
            .EnumerateArray().Select(p => p.GetString()!).Order().ToList();
        var fromToken = (AuthDriver.ReadClaim(_current!.AccessToken, "permission") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries).Order().ToList();

        fromMe.ShouldBe(fromToken);
    }

    [Given("the nr ordinary account is signed in")]
    public async Task OrdinarySignedIn()
    {
        _ordinary = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _current = _ordinary;
        api.Authenticate(_ordinary.AccessToken);
    }

    [Given("the nr operator's workspace is known")]
    public async Task OperatorsWorkspaceKnown()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _operatorsWorkspace = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);
    }

    [When("the nr ordinary account tries to enter the operator's workspace")]
    public async Task OrdinaryTriesToEnter()
    {
        var (tokens, status) = await auth.SwitchOrganizationAsync(
            _ordinary!.RefreshToken, _operatorsWorkspace);
        _entered = tokens;
        _status = status;
    }

    [Given("the nr ordinary account holds a membership in the development organization and is switched in")]
    public async Task OrdinaryHoldsMembership()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);

        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        await DeleteMembershipAsync();
        await ExecuteAsync(
            "INSERT INTO \"Memberships\" (\"Id\", \"UserId\", \"OrganizationId\", \"Role\", \"CreatedDate\") " +
            "VALUES (gen_random_uuid(), @user, @org, 0, now())");

        var (tokens, status) = await auth.SwitchOrganizationAsync(
            member.RefreshToken, _devOrganizationId.Value);
        status.ShouldBeInRange(200, 299);

        _current = tokens.ShouldNotBeNull();
        api.Authenticate(_current.AccessToken);
    }

    [When("the nr ordinary account's membership is removed")]
    public async Task MembershipRemoved() => await DeleteMembershipAsync();

    [AfterScenario]
    public async Task Cleanup()
    {
        _json?.Dispose();

        if (_memberUserId is not null && _devOrganizationId is not null)
        {
            await DeleteMembershipAsync();
        }
    }

    private JsonElement FindOrganization(Guid id)
    {
        var organizations = _json.ShouldNotBeNull().RootElement.GetProperty("organizations");
        foreach (var organization in organizations.EnumerateArray())
        {
            if (organization.GetProperty("id").GetGuid() == id)
            {
                return organization;
            }
        }

        throw new ShouldAssertException($"organization {id} is not in the organizations list");
    }

    private Task DeleteMembershipAsync() =>
        ExecuteAsync(
            "DELETE FROM \"Memberships\" WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(settings.PostgresOwner);
        await connection.OpenAsync();

        await using (var tenant = new NpgsqlCommand(
            "SELECT set_config('app.organization_id', @org, false), set_config('app.user_id', @user, false)",
            connection))
        {
            tenant.Parameters.AddWithValue("org", _devOrganizationId!.Value.ToString());
            tenant.Parameters.AddWithValue("user", _memberUserId!.Value.ToString());
            await tenant.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user", _memberUserId!.Value);
        command.Parameters.AddWithValue("org", _devOrganizationId!.Value);
        await command.ExecuteNonQueryAsync();
    }
}
