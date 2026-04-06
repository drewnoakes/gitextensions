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
public sealed class FetchOperationTests
{
    private MockExecutable _executable = null!;
    private IGitModule _module = null!;
    private IOperationContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _executable = new MockExecutable();
        _module = Substitute.For<IGitModule>();
        _module.GitExecutable.Returns(_executable);
        _module.GetEffectiveSetting("fetch.parallel").Returns("");
        _module.GetEffectiveSetting("submodule.fetchjobs").Returns("");

        _context = Substitute.For<IOperationContext>();
        _context.Module.Returns(_module);
        _context.Repository.Returns(_module);
        _context.Progress.Returns(NullProgress<string>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _executable.Verify();
    }

    [Test]
    public async Task ExecuteAsync_should_fetch_all()
    {
        using IDisposable staged = _executable.StageCommand(
            "-c fetch.parallel=0 -c submodule.fetchjobs=0 fetch --progress");

        FetchOperation operation = new();

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_with_remote_should_pass_remote()
    {
        _module.FormatBranchName("main").Returns("refs/heads/main");

        using IDisposable staged = _executable.StageCommand(
            "-c fetch.parallel=0 -c submodule.fetchjobs=0 fetch --progress \"origin\" +refs/heads/main");

        FetchOperation operation = new()
        {
            Remote = "origin",
            RemoteBranch = "main",
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_with_prune_should_pass_prune_flags()
    {
        using IDisposable staged = _executable.StageCommand(
            "-c fetch.parallel=0 -c submodule.fetchjobs=0 fetch --progress \"origin\" --prune --force");

        FetchOperation operation = new()
        {
            Remote = "origin",
            PruneRemoteBranches = true,
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_with_tags_should_pass_tags_flag()
    {
        using IDisposable staged = _executable.StageCommand(
            "-c fetch.parallel=0 -c submodule.fetchjobs=0 fetch --progress \"origin\" --tags");

        FetchOperation operation = new()
        {
            Remote = "origin",
            FetchTags = true,
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_with_existing_fetch_parallel_should_skip_option()
    {
        _module.GetEffectiveSetting("fetch.parallel").Returns("4");

        using IDisposable staged = _executable.StageCommand(
            "-c submodule.fetchjobs=0 fetch --progress \"origin\"");

        FetchOperation operation = new()
        {
            Remote = "origin",
        };

        await operation.ExecuteAsync(_context, CancellationToken.None);
    }

    [Test]
    public void Metadata_should_indicate_remote_repo_changing_operation()
    {
        FetchOperation operation = new();

        operation.Title.Should().Be("Fetch");
        operation.CanChangeRepo.Should().BeTrue();
        operation.AccessesRemote.Should().BeTrue();
        operation.ProvidesProgress.Should().BeTrue();
    }
}
