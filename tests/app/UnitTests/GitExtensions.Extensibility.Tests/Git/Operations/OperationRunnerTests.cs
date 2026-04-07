using FluentAssertions;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensionsTests.Git.Operations;

[TestFixture]
public sealed class OperationRunnerTests
{
    private IGitModule _module = null!;
    private ILockableNotifier _notifier = null!;

    [SetUp]
    public void SetUp()
    {
        _module = Substitute.For<IGitModule>();
        _module.IsValidGitWorkingDir().Returns(true);
        _module.Repository.Returns(_module);
        _notifier = Substitute.For<ILockableNotifier>();
    }

    [Test]
    public async Task RunAsync_should_execute_operation()
    {
        bool executed = false;
        TestOperation operation = new() { OnExecute = _ => executed = true };

        OperationRunner runner = new(_module, _notifier);

        await runner.RunAsync(operation, CancellationToken.None);

        executed.Should().BeTrue();
    }

    [Test]
    public async Task RunAsync_should_lock_and_unlock_notifier()
    {
        TestOperation operation = new();

        OperationRunner runner = new(_module, _notifier);

        await runner.RunAsync(operation, CancellationToken.None);

        _notifier.Received(1).Lock();
        _notifier.Received(1).UnLock(Arg.Any<bool>());
    }

    [Test]
    public async Task RunAsync_should_notify_when_operation_changes_repo()
    {
        TestOperation operation = new() { CanChangeRepo = true };

        OperationRunner runner = new(_module, _notifier);

        await runner.RunAsync(operation, CancellationToken.None);

        _notifier.Received(1).UnLock(requestNotify: true);
    }

    [Test]
    public async Task RunAsync_should_not_notify_when_operation_does_not_change_repo()
    {
        TestOperation operation = new() { CanChangeRepo = false };

        OperationRunner runner = new(_module, _notifier);

        await runner.RunAsync(operation, CancellationToken.None);

        _notifier.Received(1).UnLock(requestNotify: false);
    }

    [Test]
    public async Task RunAsync_should_not_notify_when_working_dir_invalid()
    {
        _module.IsValidGitWorkingDir().Returns(false);
        TestOperation operation = new()
        {
            CanChangeRepo = true,
            RequiresValidWorkingDirectory = false,
        };

        OperationRunner runner = new(_module, _notifier);

        await runner.RunAsync(operation, CancellationToken.None);

        _notifier.Received(1).UnLock(requestNotify: false);
    }

    [Test]
    public void RunAsync_should_throw_for_invalid_working_directory()
    {
        _module.IsValidGitWorkingDir().Returns(false);
        TestOperation operation = new() { RequiresValidWorkingDirectory = true };

        OperationRunner runner = new(_module, _notifier);

        Func<Task> act = () => runner.RunAsync(operation, CancellationToken.None);

        act.Should().ThrowAsync<InvalidWorkingDirectoryException>();
    }

    [Test]
    public void RunAsync_should_throw_when_cancelled()
    {
        TestOperation operation = new();

        OperationRunner runner = new(_module, _notifier);
        CancellationToken cancelled = new(canceled: true);

        Func<Task> act = () => runner.RunAsync(operation, cancelled);

        act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task RunAsync_should_unlock_notifier_even_on_exception()
    {
        TestOperation operation = new()
        {
            OnExecute = _ => throw new InvalidOperationException("test error"),
        };

        OperationRunner runner = new(_module, _notifier);

        try
        {
            await runner.RunAsync(operation, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
        }

        _notifier.Received(1).Lock();
        _notifier.Received(1).UnLock(requestNotify: false);
    }

    [Test]
    public async Task RunAsync_should_provide_context_with_module()
    {
        IGitModule? receivedModule = null;
        TestOperation operation = new()
        {
            OnExecute = ctx => receivedModule = ctx.Module,
        };

        OperationRunner runner = new(_module, _notifier);

        await runner.RunAsync(operation, CancellationToken.None);

        receivedModule.Should().BeSameAs(_module);
    }

    [Test]
    public async Task RunAsync_should_provide_context_with_runner_for_sub_operations()
    {
        IOperationRunner? receivedRunner = null;
        TestOperation operation = new()
        {
            OnExecute = ctx => receivedRunner = ctx.Runner,
        };

        OperationRunner runner = new(_module, _notifier);

        await runner.RunAsync(operation, CancellationToken.None);

        receivedRunner.Should().BeSameAs(runner);
    }

    [Test]
    public async Task RunAsync_should_provide_non_null_progress()
    {
        IProgress<string>? receivedProgress = null;
        TestOperation operation = new()
        {
            OnExecute = ctx => receivedProgress = ctx.Progress,
        };

        OperationRunner runner = new(_module, _notifier);

        await runner.RunAsync(operation, CancellationToken.None);

        receivedProgress.Should().NotBeNull();
    }

    [Test]
    public async Task RunAsync_nested_operations_should_lock_and_unlock_symmetrically()
    {
        TestOperation innerOperation = new() { CanChangeRepo = true };
        TestOperation outerOperation = new()
        {
            CanChangeRepo = true,
            OnExecuteAsync = ctx =>
                ctx.Runner.RunAsync(innerOperation, CancellationToken.None),
        };

        OperationRunner runner = new(_module, _notifier);

        await runner.RunAsync(outerOperation, CancellationToken.None);

        _notifier.Received(2).Lock();
        _notifier.Received(2).UnLock(Arg.Any<bool>());
    }

    [Test]
    public async Task RunAsync_with_result_should_return_value()
    {
        TestResultOperation operation = new() { Result = 42 };

        OperationRunner runner = new(_module, _notifier);

        int result = await runner.RunAsync(operation, CancellationToken.None);

        result.Should().Be(42);
    }

    private sealed class TestOperation : IOperation
    {
        public string Title => "Test";
        public bool CanChangeRepo { get; init; }
        public bool AccessesRemote { get; init; }
        public bool RequiresValidWorkingDirectory { get; init; }
        public bool ProvidesProgress { get; init; }

        public Func<IOperationContext, Task>? OnExecuteAsync { get; init; }

        public Action<IOperationContext>? OnExecute { get; init; }

        public async Task ExecuteAsync(IOperationContext context, CancellationToken cancellationToken)
        {
            if (OnExecuteAsync is not null)
            {
                await OnExecuteAsync(context);
            }
            else
            {
                OnExecute?.Invoke(context);
            }
        }
    }

    private sealed class TestResultOperation : IOperation<int>
    {
        public string Title => "Test Result";
        public bool CanChangeRepo { get; init; }
        public bool AccessesRemote { get; init; }
        public bool RequiresValidWorkingDirectory { get; init; }
        public bool ProvidesProgress { get; init; }

        public int Result { get; init; }

        public Task<int> ExecuteAsync(IOperationContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result);
        }
    }
}
