namespace GitExtensions.Extensibility.Git.Operations;

/// <summary>
///  Describes the characteristics of a git operation.
/// </summary>
public interface IOperationMetadata
{
    /// <summary>
    ///  Gets a human-readable title for the operation, used in UI and diagnostics.
    /// </summary>
    string Title { get; }

    /// <summary>
    ///  Gets a value indicating whether the operation may change the repository state.
    ///  When <see langword="true"/>, the runner will notify subscribers after execution.
    /// </summary>
    bool CanChangeRepo { get; }

    /// <summary>
    ///  Gets a value indicating whether the operation accesses a remote repository.
    ///  Used by the runner to handle SSH authentication and related concerns.
    /// </summary>
    bool AccessesRemote { get; }

    /// <summary>
    ///  Gets a value indicating whether the operation requires a valid git working directory.
    ///  When <see langword="true"/>, the runner validates the working directory before execution.
    /// </summary>
    bool RequiresValidWorkingDirectory { get; }

    /// <summary>
    ///  Gets a value indicating whether the operation reports progress during execution.
    /// </summary>
    bool ProvidesProgress { get; }
}
