using System.Net.Http.Json;
using System.Text.Json;

namespace Saas.Api.Tests.Drivers;

/// <summary>Talks to the identity service's token endpoint.</summary>
public class AuthDriver(TestSettings settings)
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri(settings.AuthBaseUrl) };

    public sealed record TokenPair(string AccessToken, string RefreshToken);

    /// <summary>Signs in with the password grant.</summary>
    public async Task<TokenPair?> SignInAsync(string email, string password)
    {
        var response = await _client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = email,
                ["password"] = password,
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["scope"] = "openid email profile roles offline_access saas.api",
            }));

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await ReadTokensAsync(response);
    }

    /// <summary>
    /// Switches the active organization, which is the refresh grant plus an
    /// <c>organization_id</c> parameter.
    /// </summary>
    public async Task<(TokenPair? Tokens, int StatusCode)> SwitchOrganizationAsync(
        string refreshToken,
        Guid organizationId)
    {
        var response = await _client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["organization_id"] = organizationId.ToString(),
            }));

        if (!response.IsSuccessStatusCode)
        {
            return (null, (int)response.StatusCode);
        }

        return (await ReadTokensAsync(response), (int)response.StatusCode);
    }

    /// <summary>Registers a new account, which also provisions its personal workspace.</summary>
    public async Task<HttpResponseMessage> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName) =>
        await _client.PostAsJsonAsync("/account/register", new
        {
            email,
            password,
            firstName,
            lastName,
        });

    /// <summary>Reads a claim straight out of an access token.</summary>
    public static string? ReadClaim(string accessToken, string claim)
    {
        var payload = accessToken.Split('.')[1];
        var padded = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=')
            .Replace('-', '+')
            .Replace('_', '/');

        using var document = JsonDocument.Parse(Convert.FromBase64String(padded));

        return document.RootElement.TryGetProperty(claim, out var value)
            ? value.ValueKind == JsonValueKind.Array
                ? string.Join(',', value.EnumerateArray().Select(e => e.GetString()))
                : value.GetString()
            : null;
    }

    private static async Task<TokenPair> ReadTokensAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return new TokenPair(
            document.RootElement.GetProperty("access_token").GetString()!,
            document.RootElement.TryGetProperty("refresh_token", out var refresh)
                ? refresh.GetString()!
                : string.Empty);
    }
}
