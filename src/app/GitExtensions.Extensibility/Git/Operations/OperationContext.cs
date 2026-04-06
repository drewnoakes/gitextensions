namespace GitExtensions.Extensibility.Git.Operations;

/// <summary>
///  Default implementation of <see cref="IOperationContext"/>.
/// </summary>
internal sealed class OperationContext : IOperationContext
{
    /// <inheritdoc />
    public IOperationRunner Runner { get; }

    /// <inheritdoc />
    public IGitModule Module { get; }

    /// <inheritdoc />
    public IWin32Window? Window { get; }

    /// <inheritdoc />
    public IProgress<string> Progress { get; }

    internal OperationContext(
        IOperationRunner runner,
        IGitModule module,
        IWin32Window? window,
        IProgress<string> progress)
    {
        Runner = runner;
        Module = module;
        Window = window;
        Progress = progress;
    }
}
