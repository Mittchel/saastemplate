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
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static Guid? _foreignWorkspace;

    private Guid _foreignId;
    private Guid _devOrganizationId;
    private AuthDriver.TokenPair? _devTokens;
    private AuthDriver.TokenPair? _currentTokens;
    private AuthDriver.TokenPair? _entered;
    private JsonDocument? _meBefore;
    private JsonDocument? _me;
    private int _status;
    private Guid? _memberUserId;

    [Given("an operatorless workspace of somebody else exists")]
    public async Task ForeignWorkspaceExists()
    {
        await Gate.WaitAsync();

        try
        {
            if (_foreignWorkspace is null)
            {
                var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
                    .ShouldNotBeNull();
                var memberApi = new ApiDriver();
                memberApi.Authenticate(member.AccessToken);
                using var organizations = await memberApi.GetJsonAsync("/organizations");
                _foreignWorkspace = organizations.RootElement[0].GetProperty("id").GetGuid();
            }
        }
        finally
        {
            Gate.Release();
        }

        _foreignId = _foreignWorkspace.Value;
    }

    [Given("the operatorless development user is signed in")]
    public async Task DevSignedIn()
    {
        _devTokens = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _currentTokens = _devTokens;
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(_devTokens.AccessToken, "org_id")!);
        api.Authenticate(_devTokens.AccessToken);
    }

    [When("the operatorless operator enters that workspace")]
    public async Task OperatorEnters()
    {
        // Remember the real memberships before entering, to compare afterwards.
        var response = await api.GetAsync("/me");
        response.EnsureSuccessStatusCode();
        _meBefore = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var (tokens, status) = await auth.SwitchOrganizationAsync(_devTokens!.RefreshToken, _foreignId);
        _status = status;
        _entered = tokens;

        if (tokens is not null)
        {
            _currentTokens = tokens;
            api.Authenticate(tokens.AccessToken);
        }
    }

    [When("an operatorless ordinary account tries to enter the development workspace")]
    public async Task OrdinaryTriesToEnter()
    {
        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        var (tokens, status) = await auth.SwitchOrganizationAsync(member.RefreshToken, _devOrganizationId);
        _entered = tokens;
        _status = status;
    }

    [Given("an operatorless ordinary account holds a Member membership in the development workspace and is switched in")]
    public async Task MemberSwitchedIn()
    {
        var dev = (await auth.SignInAsync(settings.DevUserEmail, settings.DevUserPassword))
            .ShouldNotBeNull();
        _devOrganizationId = Guid.Parse(AuthDriver.ReadClaim(dev.AccessToken, "org_id")!);

        var member = (await auth.SignInAsync(settings.MemberUserEmail, settings.MemberUserPassword))
            .ShouldNotBeNull();
        _memberUserId = Guid.Parse(AuthDriver.ReadClaim(member.AccessToken, "sub")!);

        await ExecuteAsync(
            "DELETE FROM \"Memberships\" WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");
        await ExecuteAsync(
            "INSERT INTO \"Memberships\" (\"Id\", \"UserId\", \"OrganizationId\", \"Role\", \"CreatedDate\") " +
            "VALUES (gen_random_uuid(), @user, @org, 0, now())");

        var (tokens, status) = await auth.SwitchOrganizationAsync(member.RefreshToken, _devOrganizationId);
        status.ShouldBeInRange(200, 299);
        api.Authenticate(tokens.ShouldNotBeNull().AccessToken);
    }

    [When("the operatorless ordinary account's membership is removed")]
    public async Task MembershipRemoved() =>
        await ExecuteAsync(
            "DELETE FROM \"Memberships\" WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");

    [When("the operatorless caller requests the current user")]
    public async Task RequestMe()
    {
        var response = await api.GetAsync("/me");
        _status = (int)response.StatusCode;
        _me = response.IsSuccessStatusCode
            ? JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            : null;
    }

    [When("the operatorless caller requests the runs list")]
    public async Task RequestRuns() =>
        _status = (int)(await api.GetAsync("/runs")).StatusCode;

    [Then("the operatorless entering succeeded")]
    public void EnteringSucceeded() =>
        _entered.ShouldNotBeNull($"entering answered {_status}");

    [Then("the operatorless entering was refused")]
    public void EnteringRefused() => _entered.ShouldBeNull();

    [Then("the operatorless token has no {string} claim")]
    public void TokenHasNoClaim(string claim)
    {
        var value = AuthDriver.ReadClaim(_currentTokens!.AccessToken, claim);
        string.IsNullOrEmpty(value).ShouldBeTrue($"claim {claim} was '{value}'");
    }

    [Then("the operatorless token claim {string} is {string}")]
    public void TokenClaimIs(string claim, string expected) =>
        AuthDriver.ReadClaim(_currentTokens!.AccessToken, claim).ShouldBe(expected);

    [Then("the operatorless token claim {string} is the entered workspace")]
    public void TokenClaimIsEntered(string claim) =>
        AuthDriver.ReadClaim(_currentTokens!.AccessToken, claim).ShouldBe(_foreignId.ToString());

    [Then("the operatorless token has a non-empty {string} claim")]
    public void TokenHasNonEmptyClaim(string claim) =>
        AuthDriver.ReadClaim(_currentTokens!.AccessToken, claim).ShouldNotBeNullOrWhiteSpace();

    [Then("the operatorless token permission claims include {string}")]
    public void TokenPermissionsInclude(string permission) =>
        AuthDriver.ReadClaim(_currentTokens!.AccessToken, "permission")
            .ShouldNotBeNull()
            .Split(',')
            .ShouldContain(permission);

    [Then("the operatorless answer is {int}")]
    public void AnswerIs(int status) => _status.ShouldBe(status);

    [Then("the operatorless current user has a null organizationRole")]
    public void RoleIsNull() =>
        _me!.RootElement.GetProperty("organizationRole").ValueKind.ShouldBe(JsonValueKind.Null);

    [Then("the operatorless current user has an empty permissions list")]
    public void PermissionsEmpty()
    {
        var permissions = _me!.RootElement.GetProperty("permissions");
        permissions.ValueKind.ShouldBe(JsonValueKind.Array);
        permissions.GetArrayLength().ShouldBe(0);
    }

    [Then("the operatorless current organization is the entered workspace with a null role")]
    public void CurrentOrganizationIsEntered()
    {
        var organization = _me!.RootElement.GetProperty("organization");
        organization.GetProperty("id").GetGuid().ShouldBe(_foreignId);
        organization.GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Then("the operatorless organizations keep the real memberships with their roles")]
    public void RealMembershipsKept()
    {
        var after = _me!.RootElement.GetProperty("organizations").EnumerateArray()
            .ToDictionary(o => o.GetProperty("id").GetGuid(), o => o.GetProperty("role").GetRawText());

        var before = _meBefore!.RootElement.GetProperty("organizations").EnumerateArray().ToList();
        before.ShouldNotBeEmpty();

        foreach (var organization in before)
        {
            var id = organization.GetProperty("id").GetGuid();
            after.ShouldContainKey(id);
            after[id].ShouldBe(organization.GetProperty("role").GetRawText());
        }
    }

    [Then("the operatorless organizations list the entered workspace with a null role")]
    public void EnteredListedWithNullRole()
    {
        var entered = _me!.RootElement.GetProperty("organizations").EnumerateArray()
            .Single(o => o.GetProperty("id").GetGuid() == _foreignId);

        entered.GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Then("the operatorless current user role matches the token's role claim")]
    public void RoleMatchesToken()
    {
        var claim = AuthDriver.ReadClaim(_currentTokens!.AccessToken, "org_role").ShouldNotBeNull();
        _me!.RootElement.GetProperty("organizationRole").GetString()
            .ShouldNotBeNull()
            .ToLowerInvariant()
            .ShouldBe(claim.ToLowerInvariant());
    }

    [Then("the operatorless current user permissions include {string}")]
    public void MePermissionsInclude(string permission) =>
        _me!.RootElement.GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetString())
            .ShouldContain(permission);

    [Then("the operatorless current user has the role {string}")]
    public void MeRoleIs(string role) =>
        _me!.RootElement.GetProperty("organizationRole").GetString()
            .ShouldNotBeNull()
            .ToLowerInvariant()
            .ShouldBe(role.ToLowerInvariant());

    [Then("the operatorless current user has at least one permission")]
    public void MeHasPermissions() =>
        _me!.RootElement.GetProperty("permissions").GetArrayLength().ShouldBeGreaterThan(0);

    [AfterScenario]
    public async Task Cleanup()
    {
        if (_memberUserId is not null)
        {
            await ExecuteAsync(
                "DELETE FROM \"Memberships\" WHERE \"UserId\" = @user AND \"OrganizationId\" = @org");
        }

        _me?.Dispose();
        _meBefore?.Dispose();
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(settings.PostgresOwner);
        await connection.OpenAsync();

        await using (var tenant = new NpgsqlCommand(
            "SELECT set_config('app.organization_id', @org, false), set_config('app.user_id', @user, false)",
            connection))
        {
            tenant.Parameters.AddWithValue("org", _devOrganizationId.ToString());
            tenant.Parameters.AddWithValue("user", _memberUserId!.Value.ToString());
            await tenant.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user", _memberUserId!.Value);
        command.Parameters.AddWithValue("org", _devOrganizationId);

        await command.ExecuteNonQueryAsync();
    }
}
