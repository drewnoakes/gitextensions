using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using GitExtUtils;

namespace GitCommands.Git.Operations;

/// <summary>
///  Gets the commit ID of the currently checked out commit via <c>git rev-parse HEAD</c>.
/// </summary>
public sealed class GetCurrentCheckoutOperation : IOperation<ObjectId?>
{
    /// <inheritdoc />
    public string Title => "Get Current Checkout";

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
        ArgumentString arguments = new GitArgumentBuilder("rev-parse") { "HEAD" };

        using IProcess process = context.Module.GitExecutable.Start(
            arguments,
            redirectOutput: true,
            throwOnErrorExit: false,
            cancellationToken: cancellationToken);

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        int exitCode = await process.WaitForExitAsync(cancellationToken);

        return exitCode == 0 && ObjectId.TryParse(output, offset: 0, out ObjectId? objectId)
            ? objectId
            : null;
    }
}
