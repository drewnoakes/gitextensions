using GitExtensions.Extensibility.Git;

namespace GitUI.UserControls.RevisionGrid;

/// <summary>
///  Cached status for a single worktree.
/// </summary>
internal sealed record WorktreeStatus(int UnstagedCount, int StagedCount)
{
    public bool HasChanges => UnstagedCount > 0 || StagedCount > 0;
    public int TotalCount => UnstagedCount + StagedCount;
}

/// <summary>
///  Lazily queries and caches git status for non-current worktrees in the background.
///  Fires <see cref="StatusChanged"/> when any cached value changes, so the grid can repaint.
/// </summary>
internal sealed class WorktreeStatusCache : IDisposable
{
    private readonly object _lock = new();
    private Dictionary<string, WorktreeStatus> _cache = [];
    private CancellationTokenSource _cts = new();

    /// <summary>
    ///  Raised on a thread-pool thread when at least one worktree's status has changed.
    /// </summary>
    public event Action? StatusChanged;

    /// <summary>
    ///  Gets the cached status for the worktree at <paramref name="worktreePath"/>,
    ///  or <see langword="null"/> if the status has not been queried yet.
    /// </summary>
    public WorktreeStatus? GetStatus(string worktreePath)
    {
        lock (_lock)
        {
            return _cache.TryGetValue(NormalizePath(worktreePath), out WorktreeStatus? status) ? status : null;
        }
    }

    /// <summary>
    ///  Starts background status queries for the given worktrees.
    ///  Cancels any previously in-flight queries.
    /// </summary>
    /// <param name="uiCommands">The UI commands for the current repository.</param>
    /// <param name="worktrees">All worktrees to query (current worktree is skipped).</param>
    public void BeginRefresh(IGitUICommands uiCommands, IReadOnlyList<GitWorktree> worktrees)
    {
        CancellationTokenSource oldCts = _cts;
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        // Cancel any in-flight queries from a previous refresh.
        try
        {
            oldCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        oldCts.Dispose();

        string currentPath = NormalizePath(uiCommands.Module.WorkingDir);

        List<(string path, IGitModule module)> toQuery = [];
        foreach (GitWorktree wt in worktrees)
        {
            if (wt.IsDeleted || wt.HeadType is GitWorktreeHeadType.Bare)
            {
                continue;
            }

            string path = NormalizePath(wt.Path);
            if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            toQuery.Add((path, uiCommands.WithWorkingDirectory(wt.Path).Module));
        }

        if (toQuery.Count == 0)
        {
            return;
        }

        // Fire-and-forget parallel queries on the thread pool.
        Task.Run(() =>
        {
            bool anyChanged = false;

            Parallel.ForEach(toQuery, item =>
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    IReadOnlyList<GitItemStatus> files = item.module.GetAllChangedFiles(cancellationToken: token);

                    int unstaged = 0;
                    int staged = 0;
                    foreach (GitItemStatus file in files)
                    {
                        if (file.Staged == StagedStatus.WorkTree)
                        {
                            unstaged++;
                        }
                        else if (file.Staged == StagedStatus.Index)
                        {
                            staged++;
                        }
                    }

                    WorktreeStatus newStatus = new(unstaged, staged);

                    lock (_lock)
                    {
                        if (!_cache.TryGetValue(item.path, out WorktreeStatus? old) || old != newStatus)
                        {
                            _cache[item.path] = newStatus;
                            anyChanged = true;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                    // Swallow errors from unreachable worktrees (e.g., network drives).
                }
            });

            if (anyChanged && !token.IsCancellationRequested)
            {
                StatusChanged?.Invoke();
            }
        }, token);
    }

    /// <summary>
    ///  Clears the cache and cancels in-flight queries.
    /// </summary>
    public void Clear()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        lock (_lock)
        {
            _cache = [];
        }
    }

    public void Dispose()
    {
        Clear();
        _cts.Dispose();
    }

    private static string NormalizePath(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
