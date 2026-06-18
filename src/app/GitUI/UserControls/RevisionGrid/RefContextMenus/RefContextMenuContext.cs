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

    /// <summary>
    ///  Gets the display name of the currently checked-out branch (e.g. <c>main</c>).
    /// </summary>
    public required string CurrentBranchName { get; init; }

    public required ObjectId? CurrentCheckout { get; init; }
    public required bool IsBareRepository { get; init; }
    public required Func<IGitRef, string> GetRefUnambiguousName { get; init; }
    public required Func<GitRevision?> GetLatestSelectedRevision { get; init; }
    public required Action PerformRefreshRevisions { get; init; }
    public required Action<object, EventArgs> DropStash { get; init; }

    /// <summary>
    ///  Returns the path of the worktree in which the given branch is checked out,
    ///  including the current worktree, or <see langword="null"/> if the branch is
    ///  not checked out in any worktree.
    /// </summary>
    public required Func<string, string?> GetWorktreePathForBranch { get; init; }

    /// <summary>
    ///  Opens the diff form comparing two commits.
    ///  Parameters are: base commit ID, head commit ID, base display string, head display string.
    /// </summary>
    public required Action<ObjectId, ObjectId, string, string> ShowFormDiff { get; init; }

    /// <summary>
    ///  Determines whether the first commit is an ancestor of the second commit in the in-memory revision graph.
    /// </summary>
    public required Func<ObjectId, ObjectId, bool> IsAncestorOf { get; init; }

    /// <summary>
    ///  Navigates the revision grid to the specified commit.
    /// </summary>
    public required Action<ObjectId> GoToRevision { get; init; }

    /// <summary>
    ///  For a remote branch ref, returns the name and <see cref="ObjectId"/> of the local
    ///  branch that tracks it, or <see langword="null"/> if no local branch tracks it.
    /// </summary>
    public required Func<IGitRef, (string Name, ObjectId ObjectId)?> FindLocalBranchTrackingRemote { get; init; }

    /// <summary>
    ///  Creates a new worktree for the given branch name, optionally creating a local branch
    ///  that tracks a remote. The path is computed automatically from the main worktree path
    ///  and the branch name.
    /// </summary>
    public required Action<string, string?> CreateWorktreeForBranch { get; init; }
}
