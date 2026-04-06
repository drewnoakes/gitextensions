using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Cleans untracked files and optionally directories via <c>git clean</c>.
/// </summary>
public sealed class CleanOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the clean mode (which types of files to remove).
    /// </summary>
    public CleanMode Mode { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to only show what would be removed without actually deleting.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to also remove untracked directories.
    /// </summary>
    public bool Directories { get; init; }

    /// <summary>
    ///  Gets optional path specifications to limit the clean scope.
    /// </summary>
    public string? Paths { get; init; }

    /// <summary>
    ///  Gets optional exclude patterns.
    /// </summary>
    public string? Excludes { get; init; }

    /// <inheritdoc />
    public override string Title => "Clean";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        return new GitArgumentBuilder("clean")
        {
            Mode,
            { Directories, "-d" },
            { DryRun, "--dry-run", "-f" },
            Paths,
            { !string.IsNullOrEmpty(Excludes), Excludes },
        };
    }
}
