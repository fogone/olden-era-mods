using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OldenEra.Modding.Manager;

// The bytes at a catalog URL, over HTTPS (CatalogClient's fetch). For a private GitHub repository (debugging the whole
// way before the repository is public, fogone 07.11) a token reads it: raw files with the token in the header, release
// assets through GitHub's API (a private release's download link does not take a token). Without a token, plain GET.
// The token is the player's own, from a local file (the manager's ManagerService), never in the catalog, git or a build.
public sealed class GitHubFetch
{
    static readonly Regex releaseAsset = new(@"^https://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/releases/download/(?<tag>[^/]+)/(?<name>[^/?#]+)$");

    readonly HttpClient http;
    readonly string token;

    public GitHubFetch(string userAgent, string token = null)
    {
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        this.token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    public bool HasToken => token != null;

    public async Task<byte[]> Get(string url)
    {
        if (token != null && releaseAsset.Match(url) is { Success: true } asset)
            return await ReleaseAsset(asset.Groups["owner"].Value, asset.Groups["repo"].Value, Uri.UnescapeDataString(asset.Groups["tag"].Value), Uri.UnescapeDataString(asset.Groups["name"].Value));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token != null && IsGitHub(url))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await Send(request, url);
    }

    // The token goes only to GitHub's own hosts, never to another source's.
    static bool IsGitHub(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host == "raw.githubusercontent.com" || uri.Host == "api.github.com" || uri.Host == "github.com");

    async Task<byte[]> ReleaseAsset(string owner, string repo, string tag, string name)
    {
        using var find = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{Uri.EscapeDataString(tag)}");
        find.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        find.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var release = JsonDocument.Parse(await Send(find, $"release {tag}"));
        var id = release.RootElement.GetProperty("assets").EnumerateArray()
            .Where(a => a.GetProperty("name").GetString() == name).Select(a => a.GetProperty("id").GetInt64()).Cast<long?>().FirstOrDefault()
            ?? throw new InvalidOperationException($"release {tag} has no {name}");
        using var download = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/releases/assets/{id}");
        download.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        download.Headers.Accept.ParseAdd("application/octet-stream");
        return await Send(download, $"{tag}/{name}");
    }

    async Task<byte[]> Send(HttpRequestMessage request, string what)
    {
        // GitHub answers an asset with a redirect to its storage: followed by HttpClient, without the token (it drops
        // the Authorization header on a redirect to another host).
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{what}: {(int)response.StatusCode} {response.ReasonPhrase}");
        if (response.Content.Headers.ContentLength > CatalogClient.MaxFileSize)
            throw new InvalidOperationException($"{what}: too large");
        return await response.Content.ReadAsByteArrayAsync();
    }
}
