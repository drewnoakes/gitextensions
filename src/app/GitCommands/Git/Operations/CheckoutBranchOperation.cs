using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Checks out a branch or revision via <c>git checkout</c>.
/// </summary>
public sealed class CheckoutBranchOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the branch or revision name to check out. Required.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    ///  Gets a value indicating whether the branch is a remote tracking branch.
    /// </summary>
    public bool Remote { get; init; }

    /// <summary>
    ///  Gets the action to take with local changes during checkout.
    /// </summary>
    public LocalChangesAction LocalChanges { get; init; } = LocalChangesAction.DontChange;

    /// <summary>
    ///  Gets the mode for creating a new branch during checkout.
    /// </summary>
    public CheckoutNewBranchMode NewBranchMode { get; init; } = CheckoutNewBranchMode.DontCreate;

    /// <summary>
    ///  Gets the name for the new branch, when <see cref="NewBranchMode"/> is not <see cref="CheckoutNewBranchMode.DontCreate"/>.
    /// </summary>
    public string? NewBranchName { get; init; }

    /// <inheritdoc />
    public override string Title => "Checkout Branch";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        return new GitArgumentBuilder("checkout")
        {
            { LocalChanges == LocalChangesAction.Merge, "--merge" },
            { LocalChanges == LocalChangesAction.Reset, "--force" },
            { Remote && NewBranchMode == CheckoutNewBranchMode.Create, $"-b {NewBranchName.Quote()}" },
            { Remote && NewBranchMode == CheckoutNewBranchMode.Reset, $"-B {NewBranchName.Quote()}" },
            BranchName.QuoteNE(),
        };
    }
}
