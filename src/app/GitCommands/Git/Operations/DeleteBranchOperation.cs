using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Deletes one or more local or remote-tracking branches via <c>git branch --delete</c>.
/// </summary>
public sealed class DeleteBranchOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the branches to delete. Required, must not be empty.
    /// </summary>
    public required IReadOnlyCollection<IGitRef> Branches { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to force-delete branches (even if not fully merged).
    /// </summary>
    public bool Force { get; init; }

    /// <inheritdoc />
    public override string Title => "Delete Branch";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        ArgumentNullException.ThrowIfNull(Branches);
        if (Branches.Count == 0)
        {
            throw new ArgumentException("At least one branch is required.", nameof(Branches));
        }

        bool hasRemoteBranch = Branches.Any(branch => branch.IsRemote);
        bool hasNonRemoteBranch = Branches.Any(branch => !branch.IsRemote);

        return new GitArgumentBuilder("branch")
        {
            "--delete",
            { Force, "--force" },
            { hasRemoteBranch && hasNonRemoteBranch, "--all" },
            { hasRemoteBranch && !hasNonRemoteBranch, "--remotes" },
            Branches.Select(branch => branch.Name.Quote()),
        };
    }
}
