using GitExtensions.Extensibility.Git.Operations;

namespace GitCommands.Git.Operations.Interactive;

/// <summary>
///  Interactive operation that runs <c>git stash save</c> with a progress dialog.
///  This is the "porcelain" layer that wraps the plumbing <see cref="StashSaveOperation"/>.
/// </summary>
/// <remarks>
///  <para>
///   This demonstrates the interactive operation pattern. In the full implementation,
///   this would show a <c>FormProcess</c>-style dialog with console output while
///   the stash runs. For now, it delegates directly to the plumbing operation.
///  </para>
///  <para>
///   The key difference from <see cref="StashSaveOperation"/> is that this implements
///   <see cref="IInteractiveOperation"/>, meaning the runner will reject it if no
///   <see cref="IOperationContext.Window"/> is available.
///  </para>
/// </remarks>
public sealed class StashSaveInteractiveOperation : IInteractiveOperation
{
    /// <summary>
    ///  Gets a value indicating whether to include untracked files.
    /// </summary>
    public bool IncludeUntrackedFiles { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to keep the index intact.
    /// </summary>
    public bool KeepIndex { get; init; }

    /// <summary>
    ///  Gets an optional message for the stash entry.
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    ///  Gets an optional list of specific files to stash.
    /// </summary>
    public IReadOnlyList<string>? SelectedFiles { get; init; }

    /// <inheritdoc />
    public string Title => "Stash Save";

    /// <inheritdoc />
    public bool CanChangeRepo => true;

    /// <inheritdoc />
    public bool AccessesRemote => false;

    /// <inheritdoc />
    public bool RequiresValidWorkingDirectory => true;

    /// <inheritdoc />
    public bool ProvidesProgress => true;

    /// <inheritdoc />
    public async Task ExecuteAsync(IOperationContext context, CancellationToken cancellationToken)
    {
        // Delegate to the plumbing operation via the runner.
        // The runner applies cross-cutting concerns (notification locking, etc.)
        // to the sub-operation as well.
        await context.Runner.RunAsync(
            new StashSaveOperation
            {
                IncludeUntrackedFiles = IncludeUntrackedFiles,
                KeepIndex = KeepIndex,
                Message = Message,
                SelectedFiles = SelectedFiles,
            },
            cancellationToken);
    }
}
