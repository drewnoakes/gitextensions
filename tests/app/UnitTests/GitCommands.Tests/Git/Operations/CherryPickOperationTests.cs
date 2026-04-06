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
public sealed class CherryPickOperationTests
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
    public async Task ExecuteAsync_should_cherry_pick_commit()
    {
        ObjectId commitId = ObjectId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        using IDisposable staged = _executable.StageCommand(
            $"cherry-pick {commitId}");

        CherryPickOperation operation = new() { CommitId = commitId };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_without_commit_should_pass_no_commit_flag()
    {
        ObjectId commitId = ObjectId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        using IDisposable staged = _executable.StageCommand(
            $"cherry-pick --no-commit {commitId}");

        CherryPickOperation operation = new()
        {
            CommitId = commitId,
            Commit = false,
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public void Metadata_should_indicate_repo_changing_local_operation()
    {
        ObjectId commitId = ObjectId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        CherryPickOperation operation = new() { CommitId = commitId };

        operation.Title.Should().Be("Cherry Pick");
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
