namespace GitExtensions.Extensibility.Git.Operations;

/// <summary>
///  Runs git operations, applying cross-cutting concerns such as working directory
///  validation, repository change notifications, and hook execution.
/// </summary>
public interface IOperationRunner
{
    /// <summary>
    ///  Runs an operation that produces no result value.
    /// </summary>
    /// <param name="operation">The operation to run.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task RunAsync(IOperation operation, CancellationToken cancellationToken);

    /// <summary>
    ///  Runs an operation that produces a typed result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result produced by the operation.</typeparam>
    /// <param name="operation">The operation to run.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The result of the operation.</returns>
    Task<TResult> RunAsync<TResult>(IOperation<TResult> operation, CancellationToken cancellationToken);
}
