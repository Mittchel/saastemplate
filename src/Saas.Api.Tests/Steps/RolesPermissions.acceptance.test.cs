using Reqnroll;
using Saas.Api.Tests.Drivers;
using Shouldly;

namespace Saas.Api.Tests.Steps;

[Binding]
public sealed class RolesPermissionsAcceptanceSteps
{
    private readonly ApiDriver _api;
    private readonly AuthDriver _auth;
    private readonly DatabaseDriver _db;
    private readonly TestSettings _settings;

    private Guid _memberId;
    private Guid _orgId;
    private bool _membershipAdded;
    private bool _membershipRemoved;
    private string _orgForPaths = string.Empty;
    private Guid _homeOrgId;
    private int _status;

    public RolesPermissionsAcceptanceSteps(ApiDriver api, AuthDriver auth, DatabaseDriver db, TestSettings settings)
    {
        _api = api;
        _auth = auth;
        _db = db;
        _settings = settings;
    }

    [Given("acceptance: the member is switched into the development user's organisation with raw role {int}")]
    public async Task GivenMember(int role)
    {
        var dev = await _auth.SignInAsync(_settings.DevUserEmail, _settings.DevUserPassword);
        _orgId = Guid.Parse(AuthDriver.ReadClaim(dev!.AccessToken, "org_id")!);
        _orgForPaths = _orgId.ToString();

        var member = await _auth.SignInAsync(_settings.MemberUserEmail, _settings.MemberUserPassword);
        _memberId = Guid.Parse(AuthDriver.ReadClaim(member!.AccessToken, "sub")!);

        await _db.AddMembershipAsync(_orgId, _memberId, role);
        _membershipAdded = true;

        var (tokens, status) = await _auth.SwitchOrganizationAsync(member.RefreshToken, _orgId);
        status.ShouldBeInRange(200, 299);
        _api.Authenticate(tokens.ShouldNotBeNull().AccessToken);
    }

    [When("acceptance: the member's raw role is set to {int}")]
    public async Task WhenRoleSet(int role)
    {
        (await _db.SetMembershipRoleAsync(_orgId, _memberId, role)).ShouldBe(1);
    }

    [When("acceptance: the member's membership is removed")]
    public async Task WhenRemoved()
    {
        (await _db.RemoveMembershipAsync(_orgId, _memberId)).ShouldBe(1);
        _membershipRemoved = true;
    }

    [When("acceptance: the member requests {string}")]
    public async Task WhenMemberRequests(string path)
    {
        var response = await _api.GetAsync(path.Replace("{org}", _orgForPaths));
        _status = (int)response.StatusCode;
    }

    [Given("acceptance: the development user is signed in")]
    public async Task GivenDev()
    {
        var dev = await _auth.SignInAsync(_settings.DevUserEmail, _settings.DevUserPassword);
        _orgForPaths = AuthDriver.ReadClaim(dev!.AccessToken, "org_id")!;
        _api.Authenticate(dev.AccessToken);
    }

    [Given("acceptance: the development user entered a foreign workspace")]
    public async Task GivenForeign()
    {
        // Same entry path as PlatformOperatorSteps: the seeded member's own workspace.
        var member = await _auth.SignInAsync(_settings.MemberUserEmail, _settings.MemberUserPassword);
        var theirs = new ApiDriver(_settings);
        theirs.Authenticate(member.ShouldNotBeNull().AccessToken);
        using var organizations = await theirs.GetJsonAsync("/organizations");
        var foreign = organizations.RootElement[0].GetProperty("id").GetGuid();

        var dev = await _auth.SignInAsync(_settings.DevUserEmail, _settings.DevUserPassword);
        _homeOrgId = Guid.Parse(AuthDriver.ReadClaim(dev.ShouldNotBeNull().AccessToken, "org_id")!);
        foreign.ShouldNotBe(_homeOrgId);

        var (tokens, status) = await _auth.SwitchOrganizationAsync(dev.RefreshToken, foreign);
        status.ShouldBeInRange(200, 299);
        _api.Authenticate(tokens.ShouldNotBeNull().AccessToken);
        _orgForPaths = foreign.ToString();
        _orgId = foreign;
    }

    [Then("acceptance: the entered workspace shows none of the operator's runs and the run still exists at home")]
    public async Task ThenCounts()
    {
        (await _db.CountAsTenantAsync("Runs", _orgId)).ShouldBe(0);
        (await _db.CountAsTenantAsync("Runs", _homeOrgId)).ShouldBeGreaterThan(0);
    }

    [When("acceptance: the operator requests {string}")]
    public async Task WhenOperatorRequests(string path)
    {
        var response = await _api.GetAsync(path.Replace("{org}", _orgForPaths));
        _status = (int)response.StatusCode;
    }

    [Then("acceptance: the status was {int}")]
    public void ThenStatus(int expected)
    {
        _status.ShouldBe(expected);
    }

    [AfterScenario]
    public async Task Cleanup()
    {
        if (_membershipAdded && !_membershipRemoved)
        {
            await _db.RemoveMembershipAsync(_orgId, _memberId);
        }
    }
}
