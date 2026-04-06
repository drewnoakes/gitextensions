using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;

namespace GitCommands.Git.Operations.Interactive;

/// <summary>
///  Interactive operation that confirms branch deletion with the user,
///  then delegates to the plumbing <see cref="DeleteBranchOperation"/>.
/// </summary>
/// <remarks>
///  This demonstrates the porcelain pattern for a result-producing interactive
///  operation. The UI confirmation is the interactive part; the actual branch
///  deletion is a plumbing operation invoked via the runner.
/// </remarks>
public sealed class DeleteBranchInteractiveOperation : IInteractiveOperation<OperationResult>
{
    /// <summary>
    ///  Gets the branches to delete. Required.
    /// </summary>
    public required IReadOnlyCollection<IGitRef> Branches { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to force-delete branches.
    /// </summary>
    public bool Force { get; init; }

    /// <inheritdoc />
    public string Title => "Delete Branch";

    /// <inheritdoc />
    public bool CanChangeRepo => true;

    /// <inheritdoc />
    public bool AccessesRemote => false;

    /// <inheritdoc />
    public bool RequiresValidWorkingDirectory => true;

    /// <inheritdoc />
    public bool ProvidesProgress => false;

    /// <inheritdoc />
    public async Task<OperationResult> ExecuteAsync(IOperationContext context, CancellationToken cancellationToken)
    {
        string branchNames = string.Join(", ", Branches.Select(b => b.Name));
        string message = Branches.Count == 1
            ? $"Are you sure you want to delete the branch '{branchNames}'?"
            : $"Are you sure you want to delete these {Branches.Count} branches?\n\n{branchNames}";

        DialogResult dialogResult = MessageBoxes.Show(
            context.Window,
            message,
            "Delete Branch",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (dialogResult != DialogResult.Yes)
        {
            return OperationResult.Cancelled;
        }

        await context.Runner.RunAsync(
            new DeleteBranchOperation
            {
                Branches = Branches,
                Force = Force,
            },
            cancellationToken);

        return OperationResult.Success;
    }
}
