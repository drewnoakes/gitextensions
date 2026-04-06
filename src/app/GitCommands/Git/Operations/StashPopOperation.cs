using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Pops the most recent (or named) stash entry via <c>git stash pop</c>,
///  restoring changes and removing the stash entry.
/// </summary>
public sealed class StashPopOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the stash entry to pop (e.g. <c>stash@{0}</c>). Empty for the most recent.
    /// </summary>
    public string StashName { get; init; } = "";

    /// <inheritdoc />
    public override string Title => "Stash Pop";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        return new GitArgumentBuilder("stash") { "pop", StashName.QuoteNE() };
    }
}
