namespace GitExtensions.Extensibility.Git.Operations;

/// <summary>
///  Provides the execution context for an operation, including access to the
///  repository, operation runner (for sub-operations), UI owner window, and
///  progress reporting.
/// </summary>
public interface IOperationContext
{
    /// <summary>
    ///  Gets the runner that can be used to invoke sub-operations.
    ///  Sub-operations run through the same cross-cutting concern pipeline.
    /// </summary>
    IOperationRunner Runner { get; }

    /// <summary>
    ///  Gets the git repository this operation targets.
    /// </summary>
    IGitModule Module { get; }

    /// <summary>
    ///  Gets the owner window for any UI that the operation needs to display,
    ///  or <see langword="null"/> if no UI context is available.
    /// </summary>
    IWin32Window? Window { get; }

    /// <summary>
    ///  Gets the progress reporter for the operation. Implementations may report
    ///  progress messages during execution. This instance is never <see langword="null"/>;
    ///  when no observer is attached, reports are silently discarded.
    /// </summary>
    IProgress<string> Progress { get; }
}
