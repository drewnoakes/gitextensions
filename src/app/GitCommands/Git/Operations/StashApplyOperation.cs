using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Applies a stash entry via <c>git stash apply</c>,
///  restoring changes without removing the stash entry.
/// </summary>
public sealed class StashApplyOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the stash entry to apply (e.g. <c>stash@{0}</c>). Required.
    /// </summary>
    public required string StashName { get; init; }

    /// <inheritdoc />
    public override string Title => "Stash Apply";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        return new GitArgumentBuilder("stash") { "apply", StashName.Quote() };
    }
}
