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
public sealed class SimpleGitOperationProgressTests
{
    private MockExecutable _executable = null!;
    private IOperationContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _executable = new MockExecutable();

        IGitModule module = Substitute.For<IGitModule>();
        module.GitExecutable.Returns(_executable);

        CollectingProgress progress = new();

        _context = Substitute.For<IOperationContext>();
        _context.Module.Returns(module);
        _context.Repository.Returns(module);
        _context.Progress.Returns(progress);
    }

    [TearDown]
    public void TearDown()
    {
        _executable.Verify();
    }

    [Test]
    public async Task ExecuteAsync_should_report_output_lines_via_progress()
    {
        using IDisposable staged = _executable.StageOutput(
            "stash drop \"stash@{0}\"",
            "Dropped stash@{0} (abc123)\n");

        StashDropOperation operation = new() { StashName = "stash@{0}" };

        await operation.ExecuteAsync(_context, CancellationToken.None);

        CollectingProgress progress = (CollectingProgress)_context.Progress;
        progress.Messages.Should().Contain("Dropped stash@{0} (abc123)");
    }

    [Test]
    public async Task ExecuteAsync_should_report_multiline_output()
    {
        using IDisposable staged = _executable.StageOutput(
            "tag -d \"v1.0\"",
            "Deleted tag 'v1.0'\nwas abc1234\n");

        DeleteTagOperation operation = new() { TagName = "v1.0" };

        await operation.ExecuteAsync(_context, CancellationToken.None);

        CollectingProgress progress = (CollectingProgress)_context.Progress;
        progress.Messages.Should().HaveCountGreaterThanOrEqualTo(2);
        progress.Messages.Should().Contain("Deleted tag 'v1.0'");
    }

    [Test]
    public async Task ExecuteAsync_should_report_empty_output_without_error()
    {
        using IDisposable staged = _executable.StageOutput("stash --staged", "");

        StashStagedOperation operation = new();

        await operation.ExecuteAsync(_context, CancellationToken.None);

        CollectingProgress progress = (CollectingProgress)_context.Progress;
        progress.Messages.Should().BeEmpty();
    }

    private sealed class CollectingProgress : IProgress<string>
    {
        public List<string> Messages { get; } = [];

        public void Report(string value) => Messages.Add(value);
    }
}
