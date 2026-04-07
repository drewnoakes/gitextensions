using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;

namespace GitUI.Operations;

/// <summary>
///  An <see cref="IOperationRunner"/> decorator that injects a specific
///  <see cref="IProgress{T}"/> and <see cref="IWin32Window"/> into the operation context.
///  Used by <see cref="OperationProgressDialog"/> to route operation output to its display.
/// </summary>
internal sealed class ProgressInjectingRunner : IOperationRunner
{
    private readonly IOperationRunner _inner;
    private readonly IProgress<string> _progress;
    private readonly IWin32Window? _window;

    internal ProgressInjectingRunner(IOperationRunner inner, IProgress<string> progress, IWin32Window? window)
    {
        _inner = inner;
        _progress = progress;
        _window = window;
    }

    public async Task RunAsync(IOperation operation, CancellationToken cancellationToken)
    {
        // For simple operations, we need to provide our progress.
        // The inner runner creates the context — we wrap the operation to inject progress.
        ProgressWrappedOperation wrapped = new(operation, _progress, _window);
        await _inner.RunAsync(wrapped, cancellationToken);
    }

    public async Task<TResult> RunAsync<TResult>(IOperation<TResult> operation, CancellationToken cancellationToken)
    {
        ProgressWrappedOperation<TResult> wrapped = new(operation, _progress, _window);
        return await _inner.RunAsync(wrapped, cancellationToken);
    }

    private sealed class ProgressWrappedOperation : IOperation
    {
        private readonly IOperation _inner;
        private readonly IProgress<string> _progress;
        private readonly IWin32Window? _window;

        internal ProgressWrappedOperation(IOperation inner, IProgress<string> progress, IWin32Window? window)
        {
            _inner = inner;
            _progress = progress;
            _window = window;
        }

        public string Title => _inner.Title;
        public bool CanChangeRepo => _inner.CanChangeRepo;
        public bool AccessesRemote => _inner.AccessesRemote;
        public bool RequiresValidWorkingDirectory => _inner.RequiresValidWorkingDirectory;
        public bool ProvidesProgress => _inner.ProvidesProgress;

        public Task ExecuteAsync(IOperationContext context, CancellationToken cancellationToken)
        {
            IOperationContext wrappedContext = new OverriddenContext(context, _progress, _window);
            return _inner.ExecuteAsync(wrappedContext, cancellationToken);
        }
    }

    private sealed class ProgressWrappedOperation<TResult> : IOperation<TResult>
    {
        private readonly IOperation<TResult> _inner;
        private readonly IProgress<string> _progress;
        private readonly IWin32Window? _window;

        internal ProgressWrappedOperation(IOperation<TResult> inner, IProgress<string> progress, IWin32Window? window)
        {
            _inner = inner;
            _progress = progress;
            _window = window;
        }

        public string Title => _inner.Title;
        public bool CanChangeRepo => _inner.CanChangeRepo;
        public bool AccessesRemote => _inner.AccessesRemote;
        public bool RequiresValidWorkingDirectory => _inner.RequiresValidWorkingDirectory;
        public bool ProvidesProgress => _inner.ProvidesProgress;

        public Task<TResult> ExecuteAsync(IOperationContext context, CancellationToken cancellationToken)
        {
            IOperationContext wrappedContext = new OverriddenContext(context, _progress, _window);
            return _inner.ExecuteAsync(wrappedContext, cancellationToken);
        }
    }

    private sealed class OverriddenContext : IOperationContext
    {
        private readonly IOperationContext _inner;

        internal OverriddenContext(IOperationContext inner, IProgress<string> progress, IWin32Window? window)
        {
            _inner = inner;
            Progress = progress;
            Window = window ?? inner.Window;
        }

        public IOperationRunner Runner => _inner.Runner;
        public IGitRepository Repository => _inner.Repository;
        public IGitModule Module => _inner.Module;
        public IWin32Window? Window { get; }
        public IProgress<string> Progress { get; }
    }
}
