using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GitCommands.Remotes.PullRequests;

/// <summary>
///  Queries Azure DevOps REST API for open pull requests.
/// </summary>
public sealed class AzureDevOpsPullRequestProvider : IPullRequestProvider
{
    private static readonly AzureDevOpsRemoteParser _parser = new();
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

        if (!_parser.TryExtractAzureDevopsDataFromRemoteUrl(remoteUrl, out string? owner, out string? project, out string? repository))
        {
            return [];
        }

        string? token = await GetAccessTokenAsync(remoteUrl);
        if (token is null)
        {
            return [];
        }

        string apiBaseUrl = remoteUrl.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase)
            ? $"https://dev.azure.com/{owner}/{project}/_apis"
            : $"https://{owner}.visualstudio.com/{project}/_apis";

        string requestUrl = $"{apiBaseUrl}/git/repositories/{repository}/pullrequests?searchCriteria.status=active&api-version=7.1";

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Trace.WriteLine($"Azure DevOps PR query failed: {response.StatusCode}");
                return [];
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            List<PullRequestInfo> results = ParsePullRequests(json, remoteUrl, owner, project);

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
            Trace.WriteLine($"Azure DevOps PR query error: {ex.Message}");
            return [];
        }
    }

    public async Task<PullRequestInfo?> FindPullRequestForBranchAsync(string remoteUrl, string branchName, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PullRequestInfo> prs = await GetOpenPullRequestsAsync(remoteUrl, cancellationToken);
        return prs.FirstOrDefault(pr => pr.SourceBranch.Equals(branchName, StringComparison.OrdinalIgnoreCase));
    }

    private static List<PullRequestInfo> ParsePullRequests(string json, string remoteUrl, string owner, string project)
    {
        List<PullRequestInfo> results = [];

        using JsonDocument doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("value", out JsonElement value))
        {
            return results;
        }

        string pullRequestBaseUrl = remoteUrl.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase)
            ? $"https://dev.azure.com/{owner}/{project}/_git/pullrequest"
            : $"https://{owner}.visualstudio.com/{project}/_git/pullrequest";

        foreach (JsonElement pr in value.EnumerateArray())
        {
            int id = pr.GetProperty("pullRequestId").GetInt32();
            string title = pr.GetProperty("title").GetString() ?? "";
            string sourceBranch = pr.GetProperty("sourceRefName").GetString() ?? "";
            string targetBranch = pr.GetProperty("targetRefName").GetString() ?? "";

            // Strip refs/heads/ prefix
            const string refsHeads = "refs/heads/";
            if (sourceBranch.StartsWith(refsHeads, StringComparison.OrdinalIgnoreCase))
            {
                sourceBranch = sourceBranch[refsHeads.Length..];
            }

            if (targetBranch.StartsWith(refsHeads, StringComparison.OrdinalIgnoreCase))
            {
                targetBranch = targetBranch[refsHeads.Length..];
            }

            string url = $"{pullRequestBaseUrl}/{id}";
            results.Add(new PullRequestInfo(id, title, url, sourceBranch, targetBranch));
        }

        return results;
    }

    private static async Task<string?> GetAccessTokenAsync(string remoteUrl)
    {
        try
        {
            // Use Git Credential Manager to get token
            ProcessStartInfo psi = new("git", $"credential fill")
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

            Uri uri = new(remoteUrl.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase)
                ? $"https://dev.azure.com"
                : $"https://{new Uri(remoteUrl).Host}");

            await process.StandardInput.WriteLineAsync($"protocol={uri.Scheme}");
            await process.StandardInput.WriteLineAsync($"host={uri.Host}");
            await process.StandardInput.WriteLineAsync("");
            process.StandardInput.Close();

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return null;
            }

            // Parse credential output for password (which is the token)
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
            Trace.WriteLine($"Failed to get Azure DevOps credential: {ex.Message}");
        }

        return null;
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }
}
