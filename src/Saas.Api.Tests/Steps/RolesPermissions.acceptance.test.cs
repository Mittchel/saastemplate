using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using Npgsql;
using Reqnroll;

namespace Saas.Api.Tests.Steps;

[Binding]
public sealed class RolesPermissionsAcceptanceSteps
{
    private readonly ApiDriver _api;
    private readonly AuthDriver _auth;
    private string? _devToken;
    private string? _memberToken;
    private string? _operatorToken;
    private Guid _orgId;
    private Guid _memberId;
    private bool _memberAdded;

    public RolesPermissionsAcceptanceSteps(ApiDriver api, AuthDriver auth)
    {
        _api = api;
        _auth = auth;
    }

    private static string Claim(string token, string name) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.First(c => c.Type == name).Value;

    private static async Task ExecAsync(Guid org, Guid user, string sql, params (string, object)[] ps)
    {
        await using var conn = new NpgsqlConnection(TestSettings.PostgresOwner);
        await conn.OpenAsync();
        await using (var set = new NpgsqlCommand(
            "select set_config('app.organization_id', @o, false), set_config('app.user_id', @u, false)", conn))
        {
            set.Parameters.AddWithValue("o", org.ToString());
            set.Parameters.AddWithValue("u", user.ToString());
            await set.ExecuteNonQueryAsync();
        }
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var (n, v) in ps)
        {
            cmd.Parameters.AddWithValue(n, v);
        }
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<HttpStatusCode> GetAsync(string token, string path)
    {
        using var http = new HttpClient();
        http.BaseAddress = new Uri(TestSettings.ApiBaseUrl);
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await http.SendAsync(req);
        return res.StatusCode;
    }

    [Given("acc: the development user is signed in")]
    public async Task GivenDev()
    {
        _devToken = await _auth.SignInAsDevUserAsync();
        _orgId = Guid.Parse(Claim(_devToken, "org_id"));
    }

    [Given("acc: the member is switched into the development organisation")]
    public async Task GivenMember()
    {
        await GivenDev();
        var memberInitial = await _auth.SignInAsync(TestSettings.MemberUserEmail, TestSettings.MemberUserPassword);
        _memberId = Guid.Parse(Claim(memberInitial, "sub"));
        await ExecAsync(_orgId, _memberId,
            "insert into \"Memberships\" (\"Id\", \"UserId\", \"OrganizationId\", \"Role\", \"CreatedDate\") values (gen_random_uuid(), @u, @o, 2, now())",
            ("u", _memberId), ("o", _orgId));
        _memberAdded = true;
        _memberToken = await _auth.SwitchOrganizationAsync(_orgId);
    }

    [When("acc: the member row role is set to 99")]
    public Task WhenRole99() =>
        ExecAsync(_orgId, _memberId,
            "update \"Memberships\" set \"Role\" = 99 where \"UserId\" = @u and \"OrganizationId\" = @o",
            ("u", _memberId), ("o", _orgId));

    [When("acc: the member row is removed")]
    public async Task WhenRemoved()
    {
        await ExecAsync(_orgId, _memberId,
            "delete from \"Memberships\" where \"UserId\" = @u and \"OrganizationId\" = @o",
            ("u", _memberId), ("o", _orgId));
        _memberAdded = false;
    }

    [Given("acc: a platform operator has entered a workspace without membership")]
    public async Task GivenOperator()
    {
        await GivenDev();
        _operatorToken = await _auth.SignInAsPlatformOperatorAsync();
        _operatorToken = await _auth.SwitchOrganizationAsync(_orgId);
    }

    [Then("acc: GET {string} answers {int} with the member token")]
    public async Task ThenMember(string path, int status) =>
        Assert.That((int)await GetAsync(_memberToken!, path), Is.EqualTo(status));

    [Then("acc: GET {string} answers {int} with the operator token")]
    public async Task ThenOperator(string path, int status) =>
        Assert.That((int)await GetAsync(_operatorToken!, path), Is.EqualTo(status));

    [Then("acc: GET {string} answers {int} with the development token")]
    public async Task ThenDev(string path, int status) =>
        Assert.That((int)await GetAsync(_devToken!, path), Is.EqualTo(status));

    [Then("acc: GET members of the development organisation answers {int} with the member token")]
    public async Task ThenMembersMember(int status) =>
        Assert.That((int)await GetAsync(_memberToken!, $"/organizations/{_orgId}/members"), Is.EqualTo(status));

    [Then("acc: GET members of the development organisation answers {int} with the development token")]
    public async Task ThenMembersDev(int status) =>
        Assert.That((int)await GetAsync(_devToken!, $"/organizations/{_orgId}/members"), Is.EqualTo(status));

    [Then("acc: the entered workspace holds 0 of the operator's runs by direct count")]
    public async Task ThenCount()
    {
        var operatorId = Guid.Parse(Claim(_operatorToken!, "sub"));
        await using var conn = new NpgsqlConnection(TestSettings.PostgresOwner);
        await conn.OpenAsync();
        await using (var set = new NpgsqlCommand(
            "select set_config('app.organization_id', @o, false), set_config('app.user_id', @u, false)", conn))
        {
            set.Parameters.AddWithValue("o", _orgId.ToString());
            set.Parameters.AddWithValue("u", operatorId.ToString());
            await set.ExecuteNonQueryAsync();
        }
        await using var cmd = new NpgsqlCommand(
            "select count(*) from \"Runs\" where \"CreatedByUserId\" = @u", conn);
        cmd.Parameters.AddWithValue("u", operatorId);
        Assert.That(Convert.ToInt64(await cmd.ExecuteScalarAsync()), Is.EqualTo(0));
    }

    [AfterScenario]
    public async Task Cleanup()
    {
        if (_memberAdded || _memberId != Guid.Empty)
        {
            await ExecAsync(_orgId, _memberId,
                "delete from \"Memberships\" where \"UserId\" = @u and \"OrganizationId\" = @o",
                ("u", _memberId), ("o", _orgId));
        }
    }
}
