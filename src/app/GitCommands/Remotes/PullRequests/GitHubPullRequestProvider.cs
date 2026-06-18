using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GitCommands.Remotes.PullRequests;

/// <summary>
///  Queries GitHub REST API for open pull requests.
/// </summary>
public sealed class GitHubPullRequestProvider : IPullRequestProvider
{
    private static readonly GitHubRemoteParser _parser = new();
    private static readonly HttpClient _httpClient = CreateHttpClient();

    private readonly object _cacheLock = new();
    private string? _cachedRemoteUrl;
    private IReadOnlyList<PullRequestInfo>? _cachedPullRequests;
    private DateTime _cacheTime;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public bool IsValidRemoteUrl(string remoteUrl)
        => _parser.IsValidRemoteUrl(remoteUrl);

    public async Task<IReadOnlyList<PullRequestInfo>> GetOpenPullRequestsAsync(string remoteUrl, CancellationToken cancellationToken = default)
    {
        lock (_cacheLock)
        {
            if (_cachedRemoteUrl == remoteUrl
                && _cachedPullRequests is not null
                && DateTime.UtcNow - _cacheTime < CacheDuration)
            {
                return _cachedPullRequests;
            }
        }

        if (!_parser.TryExtractGitHubDataFromRemoteUrl(remoteUrl, out string? owner, out string? repository))
        {
            return [];
        }

        string? token = await GetAccessTokenAsync();
        if (token is null)
        {
            return [];
        }

        string requestUrl = $"https://api.github.com/repos/{owner}/{repository}/pulls?state=open&per_page=100";

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Trace.WriteLine($"GitHub PR query failed: {response.StatusCode}");
                return [];
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            List<PullRequestInfo> results = ParsePullRequests(json);

            lock (_cacheLock)
            {
                _cachedRemoteUrl = remoteUrl;
                _cachedPullRequests = results;
                _cacheTime = DateTime.UtcNow;
            }

            return results;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace.WriteLine($"GitHub PR query error: {ex.Message}");
            return [];
        }
    }

    public async Task<PullRequestInfo?> FindPullRequestForBranchAsync(string remoteUrl, string branchName, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PullRequestInfo> prs = await GetOpenPullRequestsAsync(remoteUrl, cancellationToken);
        return prs.FirstOrDefault(pr => pr.SourceBranch.Equals(branchName, StringComparison.OrdinalIgnoreCase));
    }

    private static List<PullRequestInfo> ParsePullRequests(string json)
    {
        List<PullRequestInfo> results = [];

        using JsonDocument doc = JsonDocument.Parse(json);
        foreach (JsonElement pr in doc.RootElement.EnumerateArray())
        {
            int id = pr.GetProperty("number").GetInt32();
            string title = pr.GetProperty("title").GetString() ?? "";
            string url = pr.GetProperty("html_url").GetString() ?? "";

            string sourceBranch = "";
            if (pr.TryGetProperty("head", out JsonElement head))
            {
                sourceBranch = head.GetProperty("ref").GetString() ?? "";
            }

            string targetBranch = "";
            if (pr.TryGetProperty("base", out JsonElement baseProp))
            {
                targetBranch = baseProp.GetProperty("ref").GetString() ?? "";
            }

            results.Add(new PullRequestInfo(id, title, url, sourceBranch, targetBranch));
        }

        return results;
    }

    private static async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            ProcessStartInfo psi = new("git", "credential fill")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using Process? process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            await process.StandardInput.WriteLineAsync("protocol=https");
            await process.StandardInput.WriteLineAsync("host=github.com");
            await process.StandardInput.WriteLineAsync("");
            process.StandardInput.Close();

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return null;
            }

            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("password=", StringComparison.OrdinalIgnoreCase))
                {
                    return line["password=".Length..].Trim();
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace.WriteLine($"Failed to get GitHub credential: {ex.Message}");
        }

        return null;
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitExtensions", "1.0"));
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }
}
