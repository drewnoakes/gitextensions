using CommonTestUtils;
using FluentAssertions;
using GitCommands;
using GitCommands.Git;
using GitCommands.Git.Operations;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using NSubstitute;
using NUnit.Framework;

namespace GitCommandsTests.Git.Operations;

[TestFixture]
public sealed class CheckoutBranchOperationTests
{
    private MockExecutable _executable = null!;
    private IOperationContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _executable = new MockExecutable();
        _context = CreateTestContext(_executable);
    }

    [TearDown]
    public void TearDown()
    {
        _executable.Verify();
    }

    [Test]
    public async Task ExecuteAsync_should_checkout_branch()
    {
        using IDisposable staged = _executable.StageCommand("checkout \"main\"");

        CheckoutBranchOperation operation = new() { BranchName = "main" };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_with_merge_should_pass_flag()
    {
        using IDisposable staged = _executable.StageCommand("checkout --merge \"develop\"");

        CheckoutBranchOperation operation = new()
        {
            BranchName = "develop",
            LocalChanges = LocalChangesAction.Merge,
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_with_force_should_pass_flag()
    {
        using IDisposable staged = _executable.StageCommand("checkout --force \"develop\"");

        CheckoutBranchOperation operation = new()
        {
            BranchName = "develop",
            LocalChanges = LocalChangesAction.Reset,
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_remote_with_new_branch_should_use_create()
    {
        using IDisposable staged = _executable.StageCommand("checkout -b \"local\" \"origin/main\"");

        CheckoutBranchOperation operation = new()
        {
            BranchName = "origin/main",
            Remote = true,
            NewBranchMode = CheckoutNewBranchMode.Create,
            NewBranchName = "local",
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_remote_with_reset_branch_should_use_reset()
    {
        using IDisposable staged = _executable.StageCommand("checkout -B \"local\" \"origin/main\"");

        CheckoutBranchOperation operation = new()
        {
            BranchName = "origin/main",
            Remote = true,
            NewBranchMode = CheckoutNewBranchMode.Reset,
            NewBranchName = "local",
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public void Metadata_should_indicate_repo_changing_local_operation()
    {
        CheckoutBranchOperation operation = new() { BranchName = "main" };

        operation.Title.Should().Be("Checkout Branch");
        operation.CanChangeRepo.Should().BeTrue();
        operation.AccessesRemote.Should().BeFalse();
    }

    private static IOperationContext CreateTestContext(MockExecutable executable)
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.GitExecutable.Returns(executable);

        IOperationContext context = Substitute.For<IOperationContext>();
        context.Module.Returns(module);
        context.Repository.Returns(module);
        context.Progress.Returns(NullProgress<string>.Instance);

        return context;
    }
}
