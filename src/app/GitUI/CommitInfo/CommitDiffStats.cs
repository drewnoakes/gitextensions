namespace GitUI.CommitInfo;

/// <summary>
///  Summary of lines added, removed, and files changed for a single commit.
///  Parsed from <c>git diff-tree --shortstat</c> output.
/// </summary>
internal sealed record CommitDiffStats(int FilesChanged, int Insertions, int Deletions);
