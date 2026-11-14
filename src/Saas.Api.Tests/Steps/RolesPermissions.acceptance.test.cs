using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using Npgsql;
using Reqnroll;

namespace Saas.Api.Tests.Steps;

[Binding]
public sealed class RolesPermissionsAcceptanceSteps
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private readonly dynamic _container;
    private string? _devToken;
    private string? _memberToken;
    private string? _operatorToken;
    private Guid _orgId;
    private Guid _memberId;

    public RolesPermissionsAcceptanceSteps(ScenarioContext context)
    {
        _container = context.ScenarioContainer;
    }

    private static Type FindType(string name) =>
        typeof(RolesPermissionsAcceptanceSteps).Assembly.GetTypes().First(t => t.Name == name);

    private object Driver(string name) => (object)_container.Resolve(FindType(name));

    private static object? Setting(params string[] names)
    {
        var t = FindType("TestSettings");
        foreach (var n in names)
        {
            var p = t.GetProperty(n, All);
            if (p != null)
            {
                return p.GetValue(null);
            }
            var f = t.GetField(n, All);
            if (f != null)
            {
                return f.GetValue(null);
            }
        }
        throw new InvalidOperationException("No TestSettings member among: " + string.Join(", ", names));
    }

    private static async Task<object?> CallAsync(object target, string[] keywords, params object[] args)
    {
        var candidates = target.GetType().GetMethods(All)
            .Where(m => keywords.All(k => m.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Where(m => m.GetParameters().Length == args.Length)
            .ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No driver method with: " + string.Join("+", keywords) + "/" + args.Length);
        }
        var method = candidates[0];
        var converted = method.GetParameters().Select((p, i) =>
        {
            var a = args[i];
            if (p.ParameterType == typeof(Guid) && a is string s)
            {
                return (object)Guid.Parse(s);
            }
            if (p.ParameterType == typeof(string) && a is Guid g)
            {
                return g.ToString();
            }
            return a;
        }).ToArray();
        var result = method.Invoke(target, converted);
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            var rt = task.GetType();
            return rt.IsGenericType ? rt.GetProperty("Result")!.GetValue(task) : null;
        }
        return result;
    }

    private static string AsToken(object? r)
    {
        if (r is string s)
        {
            return s;
        }
        var p = r!.GetType().GetProperties().FirstOrDefault(x => x.Name.Contains("Access", StringComparison.OrdinalIgnoreCase) && x.PropertyType == typeof(string));
        return (string)p!.GetValue(r)!;
    }

    private static string Claim(string token, string name) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.First(c => c.Type == name).Value;

    private async Task<string> SignInDevAsync()
    {
        var auth = Driver("AuthDriver");
        return AsToken(await CallAsync(auth, new[] { "Dev" }));
    }

    private async Task<string> SignInMemberAsync()
    {
        var auth = Driver("AuthDriver");
        try
        {
            return AsToken(await CallAsync(auth, new[] { "Member" }));
        }
        catch (InvalidOperationException)
        {
            return AsToken(await CallAsync(auth, new[] { "SignIn" },
                (string)Setting("MemberUserEmail")!, (string)Setting("MemberUserPassword")!));
        }
    }

    private async Task<string> SwitchAsync(string token, Guid org)
    {
        var auth = Driver("AuthDriver");
        try
        {
            return AsToken(await CallAsync(auth, new[] { "SwitchOrganization" }, token, org));
        }
        catch (InvalidOperationException)
        {
            return AsToken(await CallAsync(auth, new[] { "SwitchOrganization" }, org));
        }
    }

    private static async Task ExecAsync(Guid org, Guid user, string sql, params (string, object)[] ps)
    {
        await using var conn = new NpgsqlConnection((string)Setting("PostgresOwner")!);
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

    private static async Task<int> GetAsync(string token, string path)
    {
        using var http = new HttpClient();
        http.BaseAddress = new Uri((string)Setting("ApiBaseUrl", "ApiUrl", "ApiBase", "ApiBaseAddress")!);
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await http.SendAsync(req);
        return (int)res.StatusCode;
    }

    private static void Expect(int expected, int actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"Expected HTTP {expected} but got {actual}.");
        }
    }

    [Given("acc: the development user is signed in")]
    public async Task GivenDev()
    {
        _devToken = await SignInDevAsync();
        _orgId = Guid.Parse(Claim(_devToken, "org_id"));
    }

    [Given("acc: the member is switched into the development organisation")]
    public async Task GivenMember()
    {
        await GivenDev();
        var initial = await SignInMemberAsync();
        _memberId = Guid.Parse(Claim(initial, "sub"));
        await ExecAsync(_orgId, _memberId,
            "insert into \"Memberships\" (\"Id\", \"UserId\", \"OrganizationId\", \"Role\", \"CreatedDate\") values (gen_random_uuid(), @u, @o, 3, now())",
            ("u", _memberId), ("o", _orgId));
        _memberToken = await SwitchAsync(initial, _orgId);
    }

    [When("acc: the member row role is set to 99")]
    public Task WhenRole99() =>
        ExecAsync(_orgId, _memberId,
            "update \"Memberships\" set \"Role\" = 99 where \"UserId\" = @u and \"OrganizationId\" = @o",
            ("u", _memberId), ("o", _orgId));

    [When("acc: the member row is removed")]
    public Task WhenRemoved() =>
        ExecAsync(_orgId, _memberId,
            "delete from \"Memberships\" where \"UserId\" = @u and \"OrganizationId\" = @o",
            ("u", _memberId), ("o", _orgId));

    [Given("acc: a platform operator has entered a workspace without membership")]
    public async Task GivenOperator()
    {
        await GivenDev();
        var auth = Driver("AuthDriver");
        var initial = AsToken(await CallAsync(auth, new[] { "Operator" }));
        _operatorToken = await SwitchAsync(initial, _orgId);
    }

    [Then("acc: GET {string} answers {int} with the member token")]
    public async Task ThenMember(string path, int status) =>
        Expect(status, await GetAsync(_memberToken!, path));

    [Then("acc: GET {string} answers {int} with the operator token")]
    public async Task ThenOperator(string path, int status) =>
        Expect(status, await GetAsync(_operatorToken!, path));

    [Then("acc: GET {string} answers {int} with the development token")]
    public async Task ThenDev(string path, int status) =>
        Expect(status, await GetAsync(_devToken!, path));

    [Then("acc: GET members of the development organisation answers {int} with the member token")]
    public async Task ThenMembersMember(int status) =>
        Expect(status, await GetAsync(_memberToken!, $"/organizations/{_orgId}/members"));

    [Then("acc: GET members of the development organisation answers {int} with the development token")]
    public async Task ThenMembersDev(int status) =>
        Expect(status, await GetAsync(_devToken!, $"/organizations/{_orgId}/members"));

    [Then("acc: the entered workspace holds 0 of the operator's runs by direct count")]
    public async Task ThenCount()
    {
        var operatorId = Guid.Parse(Claim(_operatorToken!, "sub"));
        await using var conn = new NpgsqlConnection((string)Setting("PostgresOwner")!);
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
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        if (count != 0)
        {
            throw new InvalidOperationException($"Expected 0 runs but found {count}.");
        }
    }

    [AfterScenario]
    public async Task Cleanup()
    {
        if (_memberId != Guid.Empty && _orgId != Guid.Empty)
        {
            await ExecAsync(_orgId, _memberId,
                "delete from \"Memberships\" where \"UserId\" = @u and \"OrganizationId\" = @o",
                ("u", _memberId), ("o", _orgId));
        }
    }
}
