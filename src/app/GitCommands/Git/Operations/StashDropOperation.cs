using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Drops a stash entry via <c>git stash drop</c>.
/// </summary>
public sealed class StashDropOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the stash entry to drop (e.g. <c>stash@{0}</c>). Required.
    /// </summary>
    public required string StashName { get; init; }

    /// <inheritdoc />
    public override string Title => "Stash Drop";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        return new GitArgumentBuilder("stash") { "drop", StashName.Quote() };
    }
}
