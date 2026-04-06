using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Deletes a tag via <c>git tag -d</c>.
/// </summary>
public sealed class DeleteTagOperation : SimpleGitOperation
{
    /// <summary>
    ///  Gets the name of the tag to delete. Required.
    /// </summary>
    public required string TagName { get; init; }

    /// <inheritdoc />
    public override string Title => "Delete Tag";

    /// <inheritdoc />
    public override bool CanChangeRepo => true;

    /// <inheritdoc />
    protected override ArgumentString BuildArguments()
    {
        return new GitArgumentBuilder("tag") { "-d", TagName.QuoteNE() };
    }
}
