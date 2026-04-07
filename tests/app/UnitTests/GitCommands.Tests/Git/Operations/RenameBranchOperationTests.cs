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
public sealed class RenameBranchOperationTests
{
    private MockExecutable _executable = null!;
    private IOperationContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _executable = new MockExecutable();
        IGitModule module = Substitute.For<IGitModule>();
        module.GitExecutable.Returns(_executable);

        _context = Substitute.For<IOperationContext>();
        _context.Module.Returns(module);
        _context.Repository.Returns(module);
        _context.Progress.Returns(NullProgress<string>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _executable.Verify();
    }

    [Test]
    public async Task ExecuteAsync_should_rename_branch()
    {
        using IDisposable staged = _executable.StageCommand("branch -m \"old-name\" \"new-name\"");

        RenameBranchOperation operation = new()
        {
            OldName = "old-name",
            NewName = "new-name",
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public void Metadata_should_indicate_repo_changing_local_operation()
    {
        RenameBranchOperation operation = new()
        {
            OldName = "old",
            NewName = "new",
        };

        operation.Title.Should().Be("Rename Branch");
        operation.CanChangeRepo.Should().BeTrue();
        operation.AccessesRemote.Should().BeFalse();
    }
}
