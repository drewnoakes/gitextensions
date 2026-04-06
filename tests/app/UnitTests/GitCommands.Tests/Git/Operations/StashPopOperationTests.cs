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
public sealed class StashPopOperationTests
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
    public async Task ExecuteAsync_should_run_stash_pop()
    {
        using IDisposable staged = _executable.StageCommand("stash pop");

        StashPopOperation operation = new();

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_with_stash_name_should_pass_name()
    {
        using IDisposable staged = _executable.StageCommand("stash pop \"stash@{1}\"");

        StashPopOperation operation = new()
        {
            StashName = "stash@{1}",
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public void Metadata_should_indicate_repo_changing_local_operation()
    {
        StashPopOperation operation = new();

        operation.Title.Should().Be("Stash Pop");
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
