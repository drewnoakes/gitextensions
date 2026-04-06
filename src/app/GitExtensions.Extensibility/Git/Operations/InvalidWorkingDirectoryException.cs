namespace GitExtensions.Extensibility.Git.Operations;

/// <summary>
///  Thrown when an operation requires a valid git working directory
///  but the repository's working directory is not valid.
/// </summary>
public sealed class InvalidWorkingDirectoryException : InvalidOperationException
{
    /// <summary>
    ///  Initializes a new instance of the <see cref="InvalidWorkingDirectoryException"/> class.
    /// </summary>
    public InvalidWorkingDirectoryException()
        : base("The operation requires a valid git working directory.")
    {
    }
}
