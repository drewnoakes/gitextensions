using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Resolves a revision expression to an <see cref="ObjectId"/> via <c>git rev-parse</c>.
///  Returns <see langword="null"/> if the expression cannot be resolved.
/// </summary>
public sealed class RevParseOperation : IOperation<ObjectId?>
{
    /// <summary>
    ///  Gets the revision expression to resolve (e.g. <c>HEAD</c>, <c>main</c>, a partial SHA).
    ///  Required.
    /// </summary>
    public required string RevisionExpression { get; init; }

    /// <inheritdoc />
    public string Title => "Rev Parse";

    /// <inheritdoc />
    public bool CanChangeRepo => false;

    /// <inheritdoc />
    public bool AccessesRemote => false;

    /// <inheritdoc />
    public bool RequiresValidWorkingDirectory => true;

    /// <inheritdoc />
    public bool ProvidesProgress => false;

    /// <inheritdoc />
    public async Task<ObjectId?> ExecuteAsync(IOperationContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RevisionExpression) || RevisionExpression.Length > 260)
        {
            return null;
        }

        if (ObjectId.TryParse(RevisionExpression, out ObjectId? objectId))
        {
            return objectId;
        }

        ArgumentString arguments = new GitArgumentBuilder("rev-parse")
        {
            "--quiet",
            "--verify",
            RevisionExpression + "^{commit}",
        };

        using IProcess process = context.Repository.GitExecutable.Start(
            arguments,
            redirectOutput: true,
            throwOnErrorExit: false,
            cancellationToken: cancellationToken);

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        int exitCode = await process.WaitForExitAsync(cancellationToken);

        return exitCode == 0 && ObjectId.TryParse(output, offset: 0, out objectId)
            ? objectId
            : null;
    }
}
