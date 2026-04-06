using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Deletes remote branches via <c>git push &lt;remote&gt; :refs/heads/&lt;branch&gt;</c>.
/// </summary>
public sealed class DeleteRemoteBranchesOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the remote to delete branches from (e.g. <c>origin</c>). Required.
    /// </summary>
    public required string Remote { get; init; }

    /// <summary>
    ///  Gets the local names of the branches to delete on the remote. Required, must not be empty.
    /// </summary>
    public required IEnumerable<string> BranchLocalNames { get; init; }

    /// <inheritdoc />
    public override string Title => "Delete Remote Branches";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    public override bool AccessesRemote => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        ArgumentNullException.ThrowIfNull(Remote);
        ArgumentNullException.ThrowIfNull(BranchLocalNames);

        return new GitArgumentBuilder("push")
        {
            Remote,
            BranchLocalNames.Select(branch => $":refs/heads/{branch.Quote()}"),
        };
    }
}
