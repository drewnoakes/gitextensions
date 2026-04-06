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
public sealed class DeleteRemoteBranchesOperationTests
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
    public async Task ExecuteAsync_should_push_delete_refspecs()
    {
        using IDisposable staged = _executable.StageCommand(
            "push origin :refs/heads/\"feature\"");

        DeleteRemoteBranchesOperation operation = new()
        {
            Remote = "origin",
            BranchLocalNames = ["feature"],
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public void Metadata_should_indicate_remote_repo_changing_operation()
    {
        DeleteRemoteBranchesOperation operation = new()
        {
            Remote = "origin",
            BranchLocalNames = ["feature"],
        };

        operation.Title.Should().Be("Delete Remote Branches");
        operation.CanChangeRepo.Should().BeTrue();
        operation.AccessesRemote.Should().BeTrue();
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
