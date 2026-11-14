using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Npgsql;
using NUnit.Framework;
using Reqnroll;

namespace Saas.Api.Tests.Steps;

[Binding]
public sealed class RolesAcceptanceSteps
{
    private static readonly string ApiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5001";
    private static readonly string AuthUrl = Environment.GetEnvironmentVariable("AUTH_URL") ?? "http://localhost:5002";
    private static readonly string ClientId = Environment.GetEnvironmentVariable("OIDC_CLIENT_ID") ?? "saas-web";
    private static readonly string Password = Environment.GetEnvironmentVariable("SEED_PASSWORD") ?? "Password123!";
    private static readonly string OwnerConn = Environment.GetEnvironmentVariable("POSTGRES_OWNER_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=saas;Username=postgres;Password=postgres";

    private readonly HttpClient _http = new();
    private string _devToken = "";
    private string _memberToken = "";
    private string _orgId = "";
    private string _memberId = "";
    private string _body = "";
    private int _status;

    private static string Claim(string jwt, string name)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
        using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
        return doc.RootElement.GetProperty(name).GetString() ?? "";
    }

    private async Task<(string Access, string Refresh)> Form(Dictionary<string, string> data)
    {
        var res = await _http.PostAsync($"{AuthUrl}/connect/token", new FormUrlEncodedContent(data));
        var text = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return (doc.RootElement.GetProperty("access_token").GetString()!,
            doc.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() ?? "" : "");
    }

    private Task<(string Access, string Refresh)> Login(string email) => Form(new()
    {
        ["grant_type"] = "password", ["client_id"] = ClientId, ["username"] = email,
        ["password"] = Password, ["scope"] = "openid offline_access",
    });

    private async Task Send(HttpMethod method, string path, string token, string? json = null)
    {
        var req = new HttpRequestMessage(method, $"{ApiUrl}{path}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (json != null)
        {
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        var res = await _http.SendAsync(req);
        _status = (int)res.StatusCode;
        _body = await res.Content.ReadAsStringAsync();
    }

    private static async Task Exec(string sql, params (string, object)[] ps)
    {
        await using var conn = new NpgsqlConnection(OwnerConn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var (n, v) in ps)
        {
            cmd.Parameters.AddWithValue(n, v);
        }
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> Scalar(string sql)
    {
        await using var conn = new NpgsqlConnection(OwnerConn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static int RoleValue(string role) => role switch { "Employee" => 0, "Manager" => 1, _ => 2 };

    [Given("acceptance: dev is signed in")]
    public async Task DevSignedIn()
    {
        var (access, _) = await Login("dev@saas.local");
        _devToken = access;
        _orgId = Claim(access, "org_id");
    }

    [When("acceptance: dev requests GET /me")]
    public Task DevMe() => Send(HttpMethod.Get, "/me", _devToken);

    [Then("acceptance: organizationRole is {string}")]
    public void RoleIs(string role)
    {
        using var doc = JsonDocument.Parse(_body);
        Assert.That(doc.RootElement.GetProperty("organizationRole").GetString(), Is.EqualTo(role));
    }

    [Then("acceptance: no stored membership or invitation role is outside 0, 1 and 2")]
    public async Task NoOutOfRange()
    {
        Assert.That(await Scalar("select count(*) from \"Memberships\" where \"Role\" not in (0,1,2)"), Is.EqualTo(0));
        Assert.That(await Scalar("select count(*) from \"Invitations\" where \"Role\" not in (0,1,2)"), Is.EqualTo(0));
    }

    [Given("acceptance: member is added to dev's organisation as {string} and signed in there")]
    public async Task AddMember(string role)
    {
        var (access, _) = await Login("member@saas.local");
        _memberId = Claim(access, "sub");
        await Exec("insert into \"Memberships\" (\"Id\",\"OrganizationId\",\"UserId\",\"Role\") " +
                   "values (gen_random_uuid(), @o::uuid, @u::uuid, @r)",
            ("o", _orgId), ("u", _memberId), ("r", RoleValue(role)));
        var (_, refresh) = await Login("member@saas.local");
        var (switched, _) = await Form(new()
        {
            ["grant_type"] = "refresh_token", ["client_id"] = ClientId,
            ["refresh_token"] = refresh, ["organization_id"] = _orgId,
        });
        _memberToken = switched;
    }

    [When("acceptance: member requests the members list")]
    public Task MemberList() => Send(HttpMethod.Get, $"/organizations/{_orgId}/members", _memberToken);

    [When("acceptance: member requests GET /runs")]
    public Task MemberRuns() => Send(HttpMethod.Get, "/runs", _memberToken);

    [When("acceptance: dev changes member's role to {string}")]
    public async Task ChangeRole(string role)
    {
        await Send(HttpMethod.Put, $"/organizations/{_orgId}/members/{_memberId}", _devToken,
            $"{{\"role\":\"{role}\"}}");
        Assert.That(_status, Is.InRange(200, 299));
    }

    [When("acceptance: the member's raw stored role is set to {int}")]
    public Task RawRole(int value) => Exec(
        "update \"Memberships\" set \"Role\" = @r where \"OrganizationId\" = @o::uuid and \"UserId\" = @u::uuid",
        ("r", value), ("o", _orgId), ("u", _memberId));

    [When("acceptance: the membership is removed")]
    [Then("acceptance: the membership is removed")]
    public Task Remove() => Exec(
        "delete from \"Memberships\" where \"OrganizationId\" = @o::uuid and \"UserId\" = @u::uuid",
        ("o", _orgId), ("u", _memberId));

    [Then("acceptance: the status is {int}")]
    public void Status(int expected) => Assert.That(_status, Is.EqualTo(expected));

    [AfterScenario]
    public async Task Cleanup()
    {
        if (_orgId != "" && _memberId != "")
        {
            await Remove();
        }
    }
}
