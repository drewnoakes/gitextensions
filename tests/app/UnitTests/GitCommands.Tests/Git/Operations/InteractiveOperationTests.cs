using CommonTestUtils;
using FluentAssertions;
using GitCommands.Git.Operations;
using GitCommands.Git.Operations.Interactive;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using NSubstitute;
using NUnit.Framework;

namespace GitCommandsTests.Git.Operations;

[TestFixture]
public sealed class InteractiveOperationTests
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
    public void RunAsync_should_reject_interactive_operation_without_window()
    {
        StashSaveInteractiveOperation operation = new();

        OperationRunner runner = new(_module, _notifier, window: null);

        Func<Task> act = () => runner.RunAsync(operation, CancellationToken.None);

        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a UI context*");
    }

    [Test]
    public async Task RunAsync_should_accept_interactive_operation_with_window()
    {
        IWin32Window window = Substitute.For<IWin32Window>();
        MockExecutable executable = new();
        _module.GitExecutable.Returns(executable);

        // Stage the expected git command from the inner StashSaveOperation
        using IDisposable staged = executable.StageCommand("stash save -u \"wip\"");

        StashSaveInteractiveOperation operation = new()
        {
            IncludeUntrackedFiles = true,
            Message = "wip",
        };

        OperationRunner runner = new(_module, _notifier, window: window);
        await runner.RunAsync(operation, CancellationToken.None);

        executable.Verify();
    }

    [Test]
    public async Task RunAsync_should_accept_non_interactive_operation_without_window()
    {
        StashSaveOperation operation = new();

        OperationRunner runner = new(_module, _notifier, window: null);

        // Non-interactive should work fine without a window
        // (will fail on execution since no mock executable, but won't fail validation)
        // Just verify it doesn't throw InvalidOperationException about UI context
        try
        {
            await runner.RunAsync(operation, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // Expected — the actual git execution will fail without a real executable
        }
    }

    [Test]
    public void StashSaveInteractiveOperation_should_implement_IRequiresUI()
    {
        StashSaveInteractiveOperation operation = new();

        operation.Should().BeAssignableTo<IRequiresUI>();
        operation.Should().BeAssignableTo<IInteractiveOperation>();
    }

    [Test]
    public void StashSaveOperation_should_not_implement_IRequiresUI()
    {
        StashSaveOperation operation = new();

        operation.Should().NotBeAssignableTo<IRequiresUI>();
    }
}
