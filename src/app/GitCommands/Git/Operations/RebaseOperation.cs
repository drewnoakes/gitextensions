using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Rebases the current branch onto a target via <c>git rebase</c>.
/// </summary>
public sealed class RebaseOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the target to rebase onto (e.g. <c>origin/main</c>, <c>FETCH_HEAD</c>). Required.
    /// </summary>
    public required string Onto { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to perform an interactive rebase.
    /// </summary>
    public bool Interactive { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to autosquash during interactive rebase.
    /// </summary>
    public bool AutoSquash { get; init; }

    /// <inheritdoc />
    public override string Title => "Rebase";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        return new GitArgumentBuilder("rebase")
        {
            { Interactive, "-i" },
            { Interactive && AutoSquash, "--autosquash" },
            Onto,
        };
    }
}
