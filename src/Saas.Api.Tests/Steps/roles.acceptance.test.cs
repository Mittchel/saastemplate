using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Npgsql;
using Reqnroll;
using Xunit;

namespace Saas.Api.Tests.Steps;

[Binding]
public sealed class RolesAcceptanceSteps
{
    private static readonly string ApiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5000";
    private static readonly string AuthUrl = Environment.GetEnvironmentVariable("AUTH_URL") ?? "http://localhost:5001";
    private static readonly string OwnerConn = Environment.GetEnvironmentVariable("PostgresOwner") ?? "";
    private static readonly string Password = Environment.GetEnvironmentVariable("SEED_PASSWORD") ?? "Password123!";
    private static readonly string ClientId = Environment.GetEnvironmentVariable("OIDC_CLIENT_ID") ?? "saas-web";

    private readonly HttpClient _http = new();
    private readonly Dictionary<string, string> _tokens = new();
    private string _lastToken = "";
    private HttpStatusCode _status;
    private string _body = "";
    private Guid _orgId;
    private Guid _memberUserId;

    private async Task<string> SignIn(string email)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = email,
            ["password"] = Password,
            ["client_id"] = ClientId,
            ["scope"] = "openid offline_access",
        });
        var res = await _http.PostAsync($"{AuthUrl}/connect/token", form);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task<(HttpStatusCode, string)> Send(HttpMethod method, string path, string token, string? json = null)
    {
        var req = new HttpRequestMessage(method, ApiUrl + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (json != null)
        {
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        var res = await _http.SendAsync(req);
        return (res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<NpgsqlConnection> Open()
    {
        var c = new NpgsqlConnection(OwnerConn);
        await c.OpenAsync();
        return c;
    }

    private async Task LoadIds()
    {
        await using var c = await Open();
        await using var o = new NpgsqlCommand(
            "select m.\"OrganizationId\" from \"Memberships\" m join \"Users\" u on u.\"Id\"=m.\"UserId\" where u.\"Email\"='dev@saas.local' limit 1", c);
        _orgId = (Guid)(await o.ExecuteScalarAsync())!;
        await using var u2 = new NpgsqlCommand("select \"Id\" from \"Users\" where \"Email\"='member@saas.local'", c);
        _memberUserId = (Guid)(await u2.ExecuteScalarAsync())!;
    }

    [Given("acct {string} is signed in")]
    public async Task GivenSignedIn(string email)
    {
        _tokens[email] = await SignIn(email);
        _lastToken = _tokens[email];
    }

    [When("acct requests {string}")]
    public async Task WhenRequests(string spec)
    {
        var parts = spec.Split(' ', 2);
        (_status, _body) = await Send(new HttpMethod(parts[0]), parts[1], _lastToken);
    }

    [When("acct requests {string} with the same token")]
    public async Task WhenRequestsSame(string spec) => await WhenRequests(spec);

    [Then("the me organizationRole is {string}")]
    public void ThenRole(string role)
    {
        using var doc = JsonDocument.Parse(_body);
        Assert.Equal(role, doc.RootElement.GetProperty("organizationRole").GetString());
    }

    [Then("no stored membership or invitation role is outside 0 to 2")]
    public async Task ThenNoOutOfRange()
    {
        await using var c = await Open();
        foreach (var table in new[] { "Memberships", "Invitations" })
        {
            await using var cmd = new NpgsqlCommand($"select count(*) from \"{table}\" where \"Role\" not in (0,1,2)", c);
            Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync())!);
        }
    }

    [Given("acct {string} is a member of the dev organisation with role value {int}")]
    public async Task GivenMember(string email, int role)
    {
        await LoadIds();
        await using var c = await Open();
        await using var del = new NpgsqlCommand(
            "delete from \"Memberships\" where \"OrganizationId\"=@o and \"UserId\"=@u", c);
        del.Parameters.AddWithValue("o", _orgId);
        del.Parameters.AddWithValue("u", _memberUserId);
        await del.ExecuteNonQueryAsync();
        await using var ins = new NpgsqlCommand(
            "insert into \"Memberships\" (\"Id\",\"OrganizationId\",\"UserId\",\"Role\",\"CreatedAt\") values (gen_random_uuid(),@o,@u,@r,now())", c);
        ins.Parameters.AddWithValue("o", _orgId);
        ins.Parameters.AddWithValue("u", _memberUserId);
        ins.Parameters.AddWithValue("r", role);
        await ins.ExecuteNonQueryAsync();
    }

    [Given("acct {string} is signed in to the dev organisation")]
    public async Task GivenSignedInToOrg(string email)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = email,
            ["password"] = Password,
            ["client_id"] = ClientId,
            ["scope"] = "openid offline_access",
        });
        var res = await _http.PostAsync($"{AuthUrl}/connect/token", form);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var refresh = doc.RootElement.GetProperty("refresh_token").GetString()!;
        var form2 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh,
            ["client_id"] = ClientId,
            ["organization_id"] = _orgId.ToString(),
        });
        var res2 = await _http.PostAsync($"{AuthUrl}/connect/token", form2);
        using var doc2 = JsonDocument.Parse(await res2.Content.ReadAsStringAsync());
        _tokens[email] = doc2.RootElement.GetProperty("access_token").GetString()!;
    }

    [When("acct {string} requests the members list")]
    [When("acct {string} requests the members list with the same token")]
    public async Task WhenMembers(string email)
    {
        (_status, _body) = await Send(HttpMethod.Get, $"/organizations/{_orgId}/members", _tokens[email]);
        _lastToken = _tokens[email];
    }

    [Then("the status is {int}")]
    public void ThenStatus(int status) => Assert.Equal(status, (int)_status);

    [When("the dev HR admin sets {string} role to {string}")]
    public async Task WhenSetRole(string email, string role)
    {
        var dev = await SignIn("dev@saas.local");
        var (st, _) = await Send(HttpMethod.Put, $"/organizations/{_orgId}/members/{_memberUserId}", dev, $"{{\"role\":\"{role}\"}}");
        Assert.True((int)st is >= 200 and < 300);
    }

    [When("the member row raw role is set to {int}")]
    public async Task WhenRaw(int value)
    {
        await using var c = await Open();
        await using var cmd = new NpgsqlCommand(
            "update \"Memberships\" set \"Role\"=@r where \"OrganizationId\"=@o and \"UserId\"=@u", c);
        cmd.Parameters.AddWithValue("r", value);
        cmd.Parameters.AddWithValue("o", _orgId);
        cmd.Parameters.AddWithValue("u", _memberUserId);
        await cmd.ExecuteNonQueryAsync();
    }

    [When("the member row is removed")]
    [Then("the member row is removed")]
    public async Task RemoveRow()
    {
        await using var c = await Open();
        await using var cmd = new NpgsqlCommand(
            "delete from \"Memberships\" where \"OrganizationId\"=@o and \"UserId\"=@u", c);
        cmd.Parameters.AddWithValue("o", _orgId);
        cmd.Parameters.AddWithValue("u", _memberUserId);
        await cmd.ExecuteNonQueryAsync();
    }
}
