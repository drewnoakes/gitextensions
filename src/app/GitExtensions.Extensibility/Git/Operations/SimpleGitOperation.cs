namespace GitExtensions.Extensibility.Git.Operations;

/// <summary>
///  Base class for operations that build git arguments and run a single git command.
///  Subclasses only need to implement <see cref="Title"/> and <see cref="BuildArguments"/>
///  and override metadata properties as needed.
/// </summary>
public abstract class SimpleGitOperation : IOperation
{
    /// <inheritdoc />
    public abstract string Title { get; }

    /// <inheritdoc />
    public virtual bool CanChangeRepo => false;

    /// <inheritdoc />
    public virtual bool AccessesRemote => false;

    /// <inheritdoc />
    public virtual bool RequiresValidWorkingDirectory => true;

    /// <inheritdoc />
    public virtual bool ProvidesProgress => false;

    /// <summary>
    ///  Builds the git command-line arguments for this operation.
    /// </summary>
    /// <returns>The arguments to pass to the git executable.</returns>
    protected abstract ArgumentString BuildArguments();

    /// <inheritdoc />
    public async Task ExecuteAsync(IOperationContext context, CancellationToken cancellationToken)
    {
        ArgumentString arguments = BuildArguments();

        using IProcess process = context.Module.GitExecutable.Start(
            arguments,
            throwOnErrorExit: true,
            cancellationToken: cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
    }
}
