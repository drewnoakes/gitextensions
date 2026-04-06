using CommonTestUtils;
using FluentAssertions;
using GitCommands.Git.Operations;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using NSubstitute;
using NUnit.Framework;

namespace GitCommandsTests.Git.Operations;

[TestFixture]
public sealed class StashSaveOperationTests
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
    public async Task ExecuteAsync_should_run_stash_save()
    {
        using IDisposable staged = _executable.StageCommand("stash save");

        StashSaveOperation operation = new();

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_with_untracked_should_pass_flag()
    {
        using IDisposable staged = _executable.StageCommand("stash save -u");

        StashSaveOperation operation = new()
        {
            IncludeUntrackedFiles = true,
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_with_keep_index_should_pass_flag()
    {
        using IDisposable staged = _executable.StageCommand("stash save --keep-index");

        StashSaveOperation operation = new()
        {
            KeepIndex = true,
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_with_message_should_pass_message()
    {
        using IDisposable staged = _executable.StageCommand("stash save \"wip changes\"");

        StashSaveOperation operation = new()
        {
            Message = "wip changes",
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_with_selected_files_should_use_push()
    {
        using IDisposable staged = _executable.StageCommand("stash push -m \"wip\" -- \"src/Foo.cs\"");

        StashSaveOperation operation = new()
        {
            Message = "wip",
            SelectedFiles = ["src/Foo.cs"],
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public void Metadata_should_indicate_repo_changing_local_operation()
    {
        StashSaveOperation operation = new();

        operation.Title.Should().Be("Stash Save");
        operation.CanChangeRepo.Should().BeTrue();
        operation.AccessesRemote.Should().BeFalse();
        operation.RequiresValidWorkingDirectory.Should().BeTrue();
    }

    private static IOperationContext CreateTestContext(MockExecutable executable)
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.GitExecutable.Returns(executable);

        IOperationContext context = Substitute.For<IOperationContext>();
        context.Module.Returns(module);
        context.Progress.Returns(NullProgress<string>.Instance);

        return context;
    }
}
