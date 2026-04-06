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
public sealed class RevParseOperationTests
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
    public async Task ExecuteAsync_should_resolve_branch_name()
    {
        string sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        using IDisposable staged = _executable.StageOutput(
            "rev-parse --quiet --verify main^{commit}", sha + "\n");

        RevParseOperation operation = new() { RevisionExpression = "main" };

        ObjectId? result = await operation.ExecuteAsync(_context, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ToString().Should().Be(sha);
    }

    [Test]
    public async Task ExecuteAsync_should_return_null_for_invalid_expression()
    {
        using IDisposable staged = _executable.StageOutput(
            "rev-parse --quiet --verify nonexistent^{commit}", "", exitCode: 128);

        RevParseOperation operation = new() { RevisionExpression = "nonexistent" };

        ObjectId? result = await operation.ExecuteAsync(_context, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_should_short_circuit_for_full_sha()
    {
        string sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        RevParseOperation operation = new() { RevisionExpression = sha };

        ObjectId? result = await operation.ExecuteAsync(_context, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ToString().Should().Be(sha);
    }

    [Test]
    public async Task ExecuteAsync_should_return_null_for_empty_expression()
    {
        RevParseOperation operation = new() { RevisionExpression = "" };

        ObjectId? result = await operation.ExecuteAsync(_context, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public void Metadata_should_indicate_read_only_operation()
    {
        RevParseOperation operation = new() { RevisionExpression = "HEAD" };

        operation.Title.Should().Be("Rev Parse");
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
