namespace GitExtensions.Extensibility.Git.Operations;

/// <summary>
///  Default implementation of <see cref="IOperationRunner"/> that applies cross-cutting
///  concerns around operation execution: working directory validation, repository change
///  notification locking, and cancellation.
/// </summary>
/// <remarks>
///  Supports nesting: when a sub-operation is run from within another operation, redundant
///  validation and locking are skipped. Notifications are deferred until the outermost
///  operation completes.
/// </remarks>
public sealed class OperationRunner : IOperationRunner
{
    private static readonly AsyncLocal<int> _nestingDepth = new();

    private readonly IGitModule _module;
    private readonly ILockableNotifier _repoChangedNotifier;
    private readonly IWin32Window? _window;
    private readonly IProgress<string> _progress;

    /// <summary>
    ///  Initializes a new instance of the <see cref="OperationRunner"/> class.
    /// </summary>
    /// <param name="module">The git module representing the repository.</param>
    /// <param name="repoChangedNotifier">The notifier used to signal repository state changes.</param>
    /// <param name="window">The optional owner window for UI operations.</param>
    /// <param name="progress">
    ///  The progress reporter. Pass <see cref="NullProgress{T}.Instance"/> when no observer is needed.
    /// </param>
    public OperationRunner(
        IGitModule module,
        ILockableNotifier repoChangedNotifier,
        IWin32Window? window = null,
        IProgress<string>? progress = null)
    {
        _module = module;
        _repoChangedNotifier = repoChangedNotifier;
        _window = window;
        _progress = progress ?? NullProgress<string>.Instance;
    }

    /// <inheritdoc />
    public async Task RunAsync(IOperation operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool isOutermost = _nestingDepth.Value == 0;

        if (isOutermost)
        {
            ValidateWorkingDirectory(operation);
            ValidateInteractiveContext(operation);
        }

        _repoChangedNotifier.Lock();
        _nestingDepth.Value++;
        bool success = false;

        try
        {
            IOperationContext context = CreateContext();
            await operation.ExecuteAsync(context, cancellationToken);
            success = true;
        }
        finally
        {
            _nestingDepth.Value--;

            bool requestNotify = success
                && operation.CanChangeRepo
                && _module.IsValidGitWorkingDir();

            _repoChangedNotifier.UnLock(requestNotify);
        }
    }

    /// <inheritdoc />
    public async Task<TResult> RunAsync<TResult>(IOperation<TResult> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool isOutermost = _nestingDepth.Value == 0;

        if (isOutermost)
        {
            ValidateWorkingDirectory(operation);
            ValidateInteractiveContext(operation);
        }

        _repoChangedNotifier.Lock();
        _nestingDepth.Value++;
        bool success = false;

        try
        {
            IOperationContext context = CreateContext();
            TResult result = await operation.ExecuteAsync(context, cancellationToken);
            success = true;

            return result;
        }
        finally
        {
            _nestingDepth.Value--;

            bool requestNotify = success
                && operation.CanChangeRepo
                && _module.IsValidGitWorkingDir();

            _repoChangedNotifier.UnLock(requestNotify);
        }
    }

    private void ValidateWorkingDirectory(IOperationMetadata operation)
    {
        if (operation.RequiresValidWorkingDirectory && !_module.IsValidGitWorkingDir())
        {
            throw new InvalidWorkingDirectoryException();
        }
    }

    private void ValidateInteractiveContext(object operation)
    {
        if (operation is IRequiresUI && _window is null)
        {
            throw new InvalidOperationException(
                $"Interactive operation '{operation.GetType().Name}' requires a UI context (Window), but none was provided.");
        }
    }

    private OperationContext CreateContext()
    {
        return new OperationContext(this, _module, _window, _progress);
    }
}
