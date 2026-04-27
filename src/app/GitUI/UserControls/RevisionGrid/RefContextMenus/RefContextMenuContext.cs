using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;

namespace GitUI.UserControls.RevisionGrid.RefContextMenus;

/// <summary>
///  Provides contextual state needed by <see cref="IRefContextMenuProvider"/> implementations
///  to build their menu items.
/// </summary>
internal sealed class RefContextMenuContext
{
    public required IGitUICommands UICommands { get; init; }
    public required Form? ParentForm { get; init; }
    public required string CurrentBranchRef { get; init; }
    public required ObjectId? CurrentCheckout { get; init; }
    public required bool IsBareRepository { get; init; }
    public required Func<IGitRef, string> GetRefUnambiguousName { get; init; }
    public required Func<GitRevision?> GetLatestSelectedRevision { get; init; }
    public required Action PerformRefreshRevisions { get; init; }
    public required Action<object, EventArgs> DropStash { get; init; }

    /// <summary>
    ///  Returns the path of the worktree in which the given branch is checked out,
    ///  or <see langword="null"/> if the branch is not checked out in any other worktree.
    /// </summary>
    public required Func<string, string?> GetWorktreePathForBranch { get; init; }
}
