using System.Net.Http.Json;
using System.Text.Json;

namespace Everdue.Server.Tests.Support;

public static class ApiExtensions
{
    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        await response.ShouldBeSuccessAsync();
        return (await response.Content.ReadFromJsonAsync<T>(EverdueApp.Json))!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object? body = null)
        => client.PostAsJsonAsync(url, body ?? new { }, EverdueApp.Json);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body)
        => client.PutAsJsonAsync(url, body, EverdueApp.Json);

    public static async Task<T> PostJsonAsync<T>(this HttpClient client, string url, object? body = null)
    {
        var response = await client.PostJsonAsync(url, body);
        await response.ShouldBeSuccessAsync();
        return (await response.Content.ReadFromJsonAsync<T>(EverdueApp.Json))!;
    }

    public static async Task<T> PutJsonAsync<T>(this HttpClient client, string url, object body)
    {
        var response = await client.PutJsonAsync(url, body);
        await response.ShouldBeSuccessAsync();
        return (await response.Content.ReadFromJsonAsync<T>(EverdueApp.Json))!;
    }

    /// <summary>DELETE endpoints that answer with the updated resource rather than 204.</summary>
    public static async Task<T> DeleteFromJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.DeleteAsync(url);
        await response.ShouldBeSuccessAsync();
        return (await response.Content.ReadFromJsonAsync<T>(EverdueApp.Json))!;
    }

    /// <summary>Fails with the ProblemDetails body rather than a bare status code — the message is the point.</summary>
    public static async Task ShouldBeSuccessAsync(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Shouldly.ShouldAssertException(
                $"Expected a success status but got {(int)response.StatusCode} {response.StatusCode}.\n{body}");
        }
    }

    /// <summary>The <c>code</c> extension the API attaches to every ProblemDetails.</summary>
    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
