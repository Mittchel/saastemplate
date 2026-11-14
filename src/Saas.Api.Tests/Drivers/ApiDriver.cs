using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Saas.Api.Tests.Drivers;

/// <summary>Talks to the resource API as a given user.</summary>
public class ApiDriver(TestSettings settings)
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri(settings.ApiBaseUrl) };

    public void Authenticate(string accessToken) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    public Task<HttpResponseMessage> GetAsync(string path) => _client.GetAsync(path);

    public Task<HttpResponseMessage> PostAsync(string path, object body) =>
        _client.PostAsJsonAsync(path, body);

    public Task<HttpResponseMessage> PutAsync(string path, object body) =>
        _client.PutAsJsonAsync(path, body);

    public async Task<JsonDocument> GetJsonAsync(string path)
    {
        var response = await _client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Creates an organization and returns its id.</summary>
    public async Task<Guid> CreateOrganizationAsync(string name)
    {
        var response = await PostAsync("/organizations", new { name });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Creates a run and returns its id.</summary>
    public async Task<(Guid? RunId, int StatusCode)> CreateRunAsync(string input)
    {
        var response = await PostAsync("/runs", new { input, threadId = (string?)null });

        if (!response.IsSuccessStatusCode)
        {
            return (null, (int)response.StatusCode);
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return (document.RootElement.GetProperty("id").GetGuid(), (int)response.StatusCode);
    }
}
