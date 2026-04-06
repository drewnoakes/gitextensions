namespace GitExtensions.Extensibility.Git.Operations;

/// <summary>
///  Result of an operation that may or may not have been performed.
/// </summary>
/// <param name="Completed">
///  <see langword="true"/> if the operation was performed successfully;
///  <see langword="false"/> if it was cancelled by the user or skipped.
/// </param>
public readonly record struct OperationResult(bool Completed)
{
    /// <summary>
    ///  An operation result indicating the operation was completed successfully.
    /// </summary>
    public static OperationResult Success { get; } = new(Completed: true);

    /// <summary>
    ///  An operation result indicating the operation was cancelled or not performed.
    /// </summary>
    public static OperationResult Cancelled { get; } = new(Completed: false);
}
