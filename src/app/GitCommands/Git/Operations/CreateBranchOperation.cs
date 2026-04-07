using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Creates a new branch via <c>git branch</c> or <c>git checkout -b</c>.
/// </summary>
public sealed class CreateBranchOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the name for the new branch. Required.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    ///  Gets the revision (commit) to create the branch at.
    /// </summary>
    public string? Revision { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to immediately check out the new branch.
    /// </summary>
    public bool Checkout { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to create an orphan branch (no parent commits).
    /// </summary>
    public bool Orphan { get; init; }

    /// <summary>
    ///  Gets the start point <see cref="ObjectId"/> for an orphan branch.
    /// </summary>
    public ObjectId? OrphanStartPoint { get; init; }

    /// <inheritdoc />
    public override string Title => "Create Branch";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        if (Orphan)
        {
            return new GitArgumentBuilder("checkout")
            {
                "--orphan",
                BranchName,
                OrphanStartPoint,
            };
        }

        return new GitArgumentBuilder(Checkout ? "checkout" : "branch")
        {
            { Checkout, "-b" },
            BranchName.Trim().Quote(),
            Revision?.Trim().QuoteNE(),
        };
    }
}
