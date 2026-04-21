using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DesktopMagic.Helpers;

public sealed class GitHubLatestRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = "Unknown";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "Unknown";

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = "https://github.com/Stone-Red-Code/DesktopMagic/releases/latest";
}

public static class GitHubReleaseService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/Stone-Red-Code/DesktopMagic/releases/latest";
    private static readonly HttpClient _httpClient = CreateReleaseHttpClient();

    private static HttpClient CreateReleaseHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopMagic");
        return client;
    }

    public static async Task<GitHubLatestRelease?> GetLatestReleaseInfoAsync()
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(LatestReleaseApiUrl);
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<GitHubLatestRelease>(responseStream);
    }
}
