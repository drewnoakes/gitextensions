using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Cherry-picks a commit via <c>git cherry-pick</c>.
/// </summary>
public sealed class CherryPickOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the commit to cherry-pick. Required.
    /// </summary>
    public required ObjectId CommitId { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to create a commit automatically.
    ///  When <see langword="false"/>, stages changes without committing.
    /// </summary>
    public bool Commit { get; init; } = true;

    /// <summary>
    ///  Gets additional arguments to pass to <c>git cherry-pick</c>.
    /// </summary>
    public string ExtraArguments { get; init; } = "";

    /// <inheritdoc />
    public override string Title => "Cherry Pick";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        return new GitArgumentBuilder("cherry-pick")
        {
            { !Commit, "--no-commit" },
            ExtraArguments,
            CommitId,
        };
    }
}
