using Reqnroll;

namespace Saas.Api.Tests.Steps;

[Binding]
public sealed class RolesPermissionsAcceptanceSteps
{
    private readonly TestSettings _settings;
    private readonly ApiDriver _api;
    private readonly AuthDriver _auth;
    private readonly DatabaseDriver _db;

    private string _orgId = string.Empty;
    private string _memberUserId = string.Empty;
    private string _memberAccess = string.Empty;
    private string _devAccess = string.Empty;
    private string _operatorAccess = string.Empty;
    private bool _membershipAdded;

    public RolesPermissionsAcceptanceSteps(TestSettings settings, ApiDriver api, AuthDriver auth, DatabaseDriver db)
    {
        _settings = settings;
        _api = api;
        _auth = auth;
        _db = db;
    }

    [Given("acc the development user is signed in")]
    public async Task GivenDev()
    {
        var dev = await _auth.SignInAsync(_settings.DevUserEmail, _settings.DevUserPassword);
        _devAccess = dev.AccessToken;
        _orgId = _auth.ReadClaim(dev.AccessToken, "org_id");
    }

    [Given("acc the member is switched into the development user's organisation")]
    public async Task GivenMember()
    {
        await GivenDev();
        var member = await _auth.SignInAsync(_settings.MemberUserEmail, _settings.MemberUserPassword);
        _memberUserId = _auth.ReadClaim(member.AccessToken, "sub");
        await _db.AddMembershipWithRawRoleAsync(Guid.Parse(_orgId), Guid.Parse(_memberUserId), 0);
        _membershipAdded = true;
        var switched = await _auth.SwitchOrganizationAsync(member.RefreshToken, _orgId);
        _memberAccess = switched.AccessToken;
    }

    [When("acc the member's stored role is set to 99")]
    public async Task WhenRole99()
    {
        await _db.SetMembershipRawRoleAsync(Guid.Parse(_orgId), Guid.Parse(_memberUserId), 99);
    }

    [When("acc the member's membership is removed")]
    public async Task WhenRemoved()
    {
        await _db.RemoveMembershipAsync(Guid.Parse(_orgId), Guid.Parse(_memberUserId));
        _membershipAdded = false;
    }

    [Then("acc the member gets 200 from runs")]
    public async Task ThenRuns200()
    {
        var r = await _api.GetAsync("/runs", _memberAccess);
        Assert.That((int)r.StatusCode, Is.EqualTo(200));
    }

    [Then("acc the member gets 403 from the members list and from runs")]
    public async Task Then403()
    {
        var runs = await _api.GetAsync("/runs", _memberAccess);
        var members = await _api.GetAsync($"/organizations/{_orgId}/members", _memberAccess);
        Assert.That((int)runs.StatusCode, Is.EqualTo(403));
        Assert.That((int)members.StatusCode, Is.EqualTo(403));
    }

    [Given("acc the operator has entered a workspace they are not a member of")]
    public async Task GivenOperator()
    {
        var op = await _auth.SignInAsync(_settings.PlatformOperatorEmail, _settings.PlatformOperatorPassword);
        var org = await _auth.SignInAsync(_settings.DevUserEmail, _settings.DevUserPassword);
        _orgId = _auth.ReadClaim(org.AccessToken, "org_id");
        var entered = await _auth.SwitchOrganizationAsync(op.RefreshToken, _orgId);
        _operatorAccess = entered.AccessToken;
    }

    [Then("acc the operator gets 403 from runs")]
    public async Task ThenOperator403()
    {
        var r = await _api.GetAsync("/runs", _operatorAccess);
        Assert.That((int)r.StatusCode, Is.EqualTo(403));
    }

    [Then("acc the development user gets 200 from runs and the members list")]
    public async Task ThenDev200()
    {
        var runs = await _api.GetAsync("/runs", _devAccess);
        var members = await _api.GetAsync($"/organizations/{_orgId}/members", _devAccess);
        Assert.That((int)runs.StatusCode, Is.EqualTo(200));
        Assert.That((int)members.StatusCode, Is.EqualTo(200));
    }

    [AfterScenario]
    public async Task Cleanup()
    {
        if (_membershipAdded)
        {
            await _db.RemoveMembershipAsync(Guid.Parse(_orgId), Guid.Parse(_memberUserId));
            _membershipAdded = false;
        }
    }
}
