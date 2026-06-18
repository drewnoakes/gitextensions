namespace GitCommands.Remotes.PullRequests;

/// <summary>
///  Provides pull request information for branches in a repository hosted on a
///  specific provider (e.g. GitHub, Azure DevOps).
/// </summary>
public interface IPullRequestProvider
{
    /// <summary>
    ///  Returns <see langword="true"/> if this provider can handle the given remote URL.
    /// </summary>
    bool IsValidRemoteUrl(string remoteUrl);

    /// <summary>
    ///  Gets open pull requests for the repository.
    ///  Results are cached; callers may invoke this frequently without concern.
    /// </summary>
    /// <param name="remoteUrl">The remote URL to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of open pull requests, or an empty list if unavailable.</returns>
    Task<IReadOnlyList<PullRequestInfo>> GetOpenPullRequestsAsync(string remoteUrl, CancellationToken cancellationToken = default);

    /// <summary>
    ///  Finds an open pull request for the given branch name, or <see langword="null"/> if none exists.
    /// </summary>
    /// <param name="remoteUrl">The remote URL to query.</param>
    /// <param name="branchName">The source branch name (without <c>refs/heads/</c> prefix).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PullRequestInfo?> FindPullRequestForBranchAsync(string remoteUrl, string branchName, CancellationToken cancellationToken = default);
}
