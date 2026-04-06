using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;

namespace GitCommands.Git.Operations;

/// <summary>
///  Pulls from a remote repository by composing a <see cref="FetchOperation"/>
///  followed by a <see cref="MergeBranchOperation"/>.
/// </summary>
/// <remarks>
///  This demonstrates the composite operation pattern: rather than implementing
///  a monolithic pull, it delegates to sub-operations via the runner, which ensures
///  each sub-operation gets proper cross-cutting concerns (notifications, cancellation, etc.).
/// </remarks>
public sealed class PullOperation : IOperation
{
    /// <summary>
    ///  Gets the remote to pull from (e.g. <c>origin</c>).
    /// </summary>
    public string? Remote { get; init; }

    /// <summary>
    ///  Gets the remote branch to pull.
    /// </summary>
    public string? RemoteBranch { get; init; }

    /// <summary>
    ///  Gets the pull action to perform after fetching.
    /// </summary>
    public GitPullAction Action { get; init; } = GitPullAction.Merge;

    /// <summary>
    ///  Gets a value controlling tag fetching during pull.
    /// </summary>
    public bool? FetchTags { get; init; }

    /// <inheritdoc />
    public string Title => "Pull";

    /// <inheritdoc />
    public bool CanChangeRepo => true;

    /// <inheritdoc />
    public bool AccessesRemote => true;

    /// <inheritdoc />
    public bool RequiresValidWorkingDirectory => true;

    /// <inheritdoc />
    public bool ProvidesProgress => true;

    /// <inheritdoc />
    public async Task ExecuteAsync(IOperationContext context, CancellationToken cancellationToken)
    {
        if (Action == GitPullAction.Fetch)
        {
            await context.Runner.RunAsync(
                new FetchOperation
                {
                    Remote = Remote,
                    RemoteBranch = RemoteBranch,
                    FetchTags = FetchTags,
                },
                cancellationToken);

            return;
        }

        await context.Runner.RunAsync(
            new FetchOperation
            {
                Remote = Remote,
                RemoteBranch = RemoteBranch,
                FetchTags = FetchTags,
            },
            cancellationToken);

        string mergeBranch = BuildMergeTarget();

        if (Action == GitPullAction.Rebase)
        {
            await context.Runner.RunAsync(
                new RebaseOperation { Onto = mergeBranch },
                cancellationToken);
        }
        else
        {
            await context.Runner.RunAsync(
                new MergeBranchOperation { Branch = mergeBranch },
                cancellationToken);
        }

        return;

        string BuildMergeTarget()
        {
            if (!string.IsNullOrEmpty(Remote) && !string.IsNullOrEmpty(RemoteBranch))
            {
                return $"{Remote}/{RemoteBranch}";
            }

            return "FETCH_HEAD";
        }
    }
}
