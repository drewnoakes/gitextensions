namespace GitExtensions.Extensibility.Git.Operations;

/// <summary>
///  Represents a git operation that produces no result value.
/// </summary>
public interface IOperation : IOperationMetadata
{
    /// <summary>
    ///  Executes the operation within the given context.
    /// </summary>
    /// <param name="context">The context providing access to the repository, runner, and progress reporting.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task ExecuteAsync(IOperationContext context, CancellationToken cancellationToken);
}

/// <summary>
///  Represents a git operation that produces a typed result.
/// </summary>
/// <typeparam name="TResult">The type of the result produced by the operation.</typeparam>
public interface IOperation<TResult> : IOperationMetadata
{
    /// <summary>
    ///  Executes the operation within the given context and returns a result.
    /// </summary>
    /// <param name="context">The context providing access to the repository, runner, and progress reporting.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The result of the operation.</returns>
    Task<TResult> ExecuteAsync(IOperationContext context, CancellationToken cancellationToken);
}
