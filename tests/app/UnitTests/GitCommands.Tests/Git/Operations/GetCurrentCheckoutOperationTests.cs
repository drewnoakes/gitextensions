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
public sealed class GetCurrentCheckoutOperationTests
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
    public async Task ExecuteAsync_should_return_commit_id()
    {
        string sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        using IDisposable staged = _executable.StageOutput("rev-parse HEAD", sha + "\n");

        GetCurrentCheckoutOperation operation = new();

        ObjectId? result = await operation.ExecuteAsync(_context, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ToString().Should().Be(sha);
    }

    [Test]
    public async Task ExecuteAsync_should_return_null_on_error()
    {
        using IDisposable staged = _executable.StageOutput("rev-parse HEAD", "fatal: not a git repository", exitCode: 128);

        GetCurrentCheckoutOperation operation = new();

        ObjectId? result = await operation.ExecuteAsync(_context, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public void Metadata_should_indicate_read_only_operation()
    {
        GetCurrentCheckoutOperation operation = new();

        operation.Title.Should().Be("Get Current Checkout");
        operation.CanChangeRepo.Should().BeFalse();
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
