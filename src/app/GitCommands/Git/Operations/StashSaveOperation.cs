using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Stashes working directory changes via <c>git stash save</c> or <c>git stash push</c>.
/// </summary>
public sealed class StashSaveOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets a value indicating whether to include untracked files in the stash.
    /// </summary>
    public bool IncludeUntrackedFiles { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to keep the index intact.
    /// </summary>
    public bool KeepIndex { get; init; }

    /// <summary>
    ///  Gets an optional message to describe the stash entry.
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    ///  Gets an optional list of specific files to stash. When non-empty, uses <c>git stash push</c>.
    /// </summary>
    public IReadOnlyList<string>? SelectedFiles { get; init; }

    /// <inheritdoc />
    public override string Title => "Stash Save";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        IReadOnlyList<string> files = SelectedFiles ?? [];
        bool isPartialStash = files.Count > 0;

        return new GitArgumentBuilder("stash")
        {
            { isPartialStash, "push", "save" },
            { IncludeUntrackedFiles, "-u" },
            { KeepIndex, "--keep-index" },
            { isPartialStash && !string.IsNullOrWhiteSpace(Message), "-m" },
            { !string.IsNullOrWhiteSpace(Message), Message.Quote() },
            { isPartialStash, "--" },
            { isPartialStash, string.Join(" ", files.Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => path.QuoteNE())) },
        };
    }
}
