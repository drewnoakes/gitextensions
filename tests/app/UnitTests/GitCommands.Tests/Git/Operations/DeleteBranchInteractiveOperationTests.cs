using FluentAssertions;
using GitCommands.Git.Operations;
using GitCommands.Git.Operations.Interactive;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using NSubstitute;
using NUnit.Framework;

namespace GitCommandsTests.Git.Operations;

[TestFixture]
public sealed class DeleteBranchInteractiveOperationTests
{
    [Test]
    public void Should_implement_IRequiresUI()
    {
        IGitRef branch = Substitute.For<IGitRef>();
        branch.Name.Returns("feature");

        DeleteBranchInteractiveOperation operation = new()
        {
            Branches = [branch],
        };

        operation.Should().BeAssignableTo<IRequiresUI>();
        operation.Should().BeAssignableTo<IInteractiveOperation<OperationResult>>();
    }

    [Test]
    public void Metadata_should_indicate_repo_changing_local_operation()
    {
        IGitRef branch = Substitute.For<IGitRef>();
        branch.Name.Returns("feature");

        DeleteBranchInteractiveOperation operation = new()
        {
            Branches = [branch],
        };

        operation.Title.Should().Be("Delete Branch");
        operation.CanChangeRepo.Should().BeTrue();
        operation.AccessesRemote.Should().BeFalse();
    }

    [Test]
    public void OperationResult_success_should_indicate_completed()
    {
        OperationResult result = OperationResult.Success;

        result.Completed.Should().BeTrue();
    }

    [Test]
    public void OperationResult_cancelled_should_indicate_not_completed()
    {
        OperationResult result = OperationResult.Cancelled;

        result.Completed.Should().BeFalse();
    }
}
