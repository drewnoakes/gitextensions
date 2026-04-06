using FluentAssertions;
using GitCommands.Git.Operations;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Git.Operations;
using NSubstitute;
using NUnit.Framework;

namespace GitCommandsTests.Git.Operations;

[TestFixture]
public sealed class PullOperationTests
{
    private IOperationRunner _runner = null!;
    private IOperationContext _context = null!;
    private List<IOperation> _executedOperations = null!;

    [SetUp]
    public void SetUp()
    {
        _executedOperations = [];
        _runner = Substitute.For<IOperationRunner>();

        // Capture each operation that gets run via the runner
        _runner.RunAsync(Arg.Any<IOperation>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _executedOperations.Add(callInfo.Arg<IOperation>());
                return Task.CompletedTask;
            });

        _context = Substitute.For<IOperationContext>();
        _context.Runner.Returns(_runner);
        _context.Progress.Returns(NullProgress<string>.Instance);
    }

    [Test]
    public async Task ExecuteAsync_with_merge_should_fetch_then_merge()
    {
        PullOperation operation = new()
        {
            Remote = "origin",
            RemoteBranch = "main",
            Action = GitPullAction.Merge,
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);

        _executedOperations.Should().HaveCount(2);
        _executedOperations[0].Should().BeOfType<FetchOperation>();
        _executedOperations[1].Should().BeOfType<MergeBranchOperation>();

        FetchOperation fetch = (FetchOperation)_executedOperations[0];
        fetch.Remote.Should().Be("origin");
        fetch.RemoteBranch.Should().Be("main");

        MergeBranchOperation merge = (MergeBranchOperation)_executedOperations[1];
        merge.Branch.Should().Be("origin/main");
    }

    [Test]
    public async Task ExecuteAsync_with_rebase_should_fetch_then_rebase()
    {
        PullOperation operation = new()
        {
            Remote = "origin",
            RemoteBranch = "main",
            Action = GitPullAction.Rebase,
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);

        _executedOperations.Should().HaveCount(2);
        _executedOperations[0].Should().BeOfType<FetchOperation>();
        _executedOperations[1].Should().BeOfType<RebaseOperation>();

        RebaseOperation rebase = (RebaseOperation)_executedOperations[1];
        rebase.Onto.Should().Be("origin/main");
    }

    [Test]
    public async Task ExecuteAsync_with_fetch_only_should_not_merge()
    {
        PullOperation operation = new()
        {
            Remote = "origin",
            Action = GitPullAction.Fetch,
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);

        _executedOperations.Should().HaveCount(1);
        _executedOperations[0].Should().BeOfType<FetchOperation>();
    }

    [Test]
    public async Task ExecuteAsync_without_remote_branch_should_use_fetch_head()
    {
        PullOperation operation = new()
        {
            Remote = "origin",
            Action = GitPullAction.Merge,
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);

        MergeBranchOperation merge = (MergeBranchOperation)_executedOperations[1];
        merge.Branch.Should().Be("FETCH_HEAD");
    }

    [Test]
    public void Metadata_should_indicate_remote_repo_changing_operation()
    {
        PullOperation operation = new();

        operation.Title.Should().Be("Pull");
        operation.CanChangeRepo.Should().BeTrue();
        operation.AccessesRemote.Should().BeTrue();
        operation.ProvidesProgress.Should().BeTrue();
    }
}
