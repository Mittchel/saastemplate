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
        _api.Authenticate(tokens!.AccessToken);
    }

    [When("acceptance: the member's raw role is set to {int}")]
    public async Task WhenRoleSet(int role)
    {
        await _db.SetMembershipRoleAsync(_orgId, _memberId, role);
    }

    [When("acceptance: the member's membership is removed")]
    public async Task WhenRemoved()
    {
        await _db.RemoveMembershipAsync(_orgId, _memberId);
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
        var dev = await _auth.SignInAsync(_settings.DevUserEmail, _settings.DevUserPassword);
        var own = AuthDriver.ReadClaim(dev!.AccessToken, "org_id");
        var other = await _auth.SignInAsync(_settings.MemberUserEmail, _settings.MemberUserPassword);
        var foreign = AuthDriver.ReadClaim(other!.AccessToken, "org_id")!;
        foreign.ShouldNotBe(own);
        var (tokens, status) = await _auth.SwitchOrganizationAsync(dev.RefreshToken, Guid.Parse(foreign));
        status.ShouldBeInRange(200, 299);
        _api.Authenticate(tokens!.AccessToken);
        _orgForPaths = foreign;
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
