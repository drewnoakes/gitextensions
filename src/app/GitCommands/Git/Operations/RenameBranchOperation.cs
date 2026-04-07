using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Renames a branch via <c>git branch -m</c>.
/// </summary>
public sealed class RenameBranchOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the current name of the branch. Required.
    /// </summary>
    public required string OldName { get; init; }

    /// <summary>
    ///  Gets the new name for the branch. Required.
    /// </summary>
    public required string NewName { get; init; }

    /// <inheritdoc />
    public override string Title => "Rename Branch";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        return new GitArgumentBuilder("branch")
        {
            "-m",
            OldName.QuoteNE(),
            NewName.QuoteNE(),
        };
    }
}
