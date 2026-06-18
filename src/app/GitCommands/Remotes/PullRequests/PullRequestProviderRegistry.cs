namespace GitCommands.Remotes.PullRequests;

/// <summary>
///  Aggregates pull request providers and dispatches to the appropriate one
///  based on the remote URL.
/// </summary>
public sealed class PullRequestProviderRegistry : IPullRequestProvider
{
    private static readonly IPullRequestProvider[] _providers =
    [
        new AzureDevOpsPullRequestProvider(),
        new GitHubPullRequestProvider(),
    ];

    public bool IsValidRemoteUrl(string remoteUrl)
        => _providers.Any(p => p.IsValidRemoteUrl(remoteUrl));

    public Task<IReadOnlyList<PullRequestInfo>> GetOpenPullRequestsAsync(string remoteUrl, CancellationToken cancellationToken = default)
    {
        foreach (IPullRequestProvider provider in _providers)
        {
            if (provider.IsValidRemoteUrl(remoteUrl))
            {
                return provider.GetOpenPullRequestsAsync(remoteUrl, cancellationToken);
            }
        }

        return Task.FromResult<IReadOnlyList<PullRequestInfo>>([]);
    }

    public Task<PullRequestInfo?> FindPullRequestForBranchAsync(string remoteUrl, string branchName, CancellationToken cancellationToken = default)
    {
        foreach (IPullRequestProvider provider in _providers)
        {
            if (provider.IsValidRemoteUrl(remoteUrl))
            {
                return provider.FindPullRequestForBranchAsync(remoteUrl, branchName, cancellationToken);
            }
        }

        return Task.FromResult<PullRequestInfo?>(null);
    }
}
