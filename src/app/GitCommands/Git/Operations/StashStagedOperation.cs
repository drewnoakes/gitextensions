using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Stashes staged changes via <c>git stash --staged</c>.
/// </summary>
public sealed class StashStagedOperation : SimpleGitOperation
{
    /// <inheritdoc />
    public override string Title => "Stash Staged";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        return new GitArgumentBuilder("stash") { "--staged" };
    }
}
