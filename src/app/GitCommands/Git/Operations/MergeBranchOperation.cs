using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Merges a branch into the current branch via <c>git merge</c>.
/// </summary>
public sealed class MergeBranchOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the branch name to merge. Required.
    /// </summary>
    public required string Branch { get; init; }

    /// <summary>
    ///  Gets a value indicating whether fast-forward merges are allowed.
    /// </summary>
    public bool AllowFastForward { get; init; } = true;

    /// <summary>
    ///  Gets a value indicating whether to create a squash merge.
    /// </summary>
    public bool Squash { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to skip the automatic commit.
    /// </summary>
    public bool NoCommit { get; init; }

    /// <summary>
    ///  Gets the merge strategy to use (e.g. <c>recursive</c>, <c>ort</c>).
    /// </summary>
    public string Strategy { get; init; } = "";

    /// <summary>
    ///  Gets a value indicating whether to allow merging unrelated histories.
    /// </summary>
    public bool AllowUnrelatedHistories { get; init; }

    /// <inheritdoc />
    public override string Title => "Merge Branch";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        return new GitArgumentBuilder("merge")
        {
            { !AllowFastForward, "--no-ff" },
            { !string.IsNullOrEmpty(Strategy), $"--strategy={Strategy}" },
            { Squash, "--squash" },
            { NoCommit, "--no-commit" },
            { AllowUnrelatedHistories, "--allow-unrelated-histories" },
            Branch,
        };
    }
}
