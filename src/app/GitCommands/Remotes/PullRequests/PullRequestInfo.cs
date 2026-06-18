namespace GitCommands.Remotes.PullRequests;

/// <summary>
///  Represents a pull request associated with a branch.
/// </summary>
public sealed record PullRequestInfo(
    int Id,
    string Title,
    string Url,
    string SourceBranch,
    string TargetBranch);
