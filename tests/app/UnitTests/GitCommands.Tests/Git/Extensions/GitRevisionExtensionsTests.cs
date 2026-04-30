using GitCommands.Git.Extensions;
using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;

namespace GitCommandsTests.Git.Extensions;
public class GitRevisionExtensionsTests
{
    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase(" ", false)]
    [TestCase("0000", false)]
    [TestCase(GitRevision.WorkTreeGuid, true)]
    [TestCase(GitRevision.IndexGuid, true)]
    [TestCase(GitRevision.CombinedDiffGuid, true)]
    public void IsArtificial_tests(string? sha1, bool expected)
    {
        sha1.IsArtificial().Should().Be(expected);
    }

    [Test]
    public void IsArtificial_should_return_true_for_generated_artificial_ids()
    {
        ObjectId.CreateWorkTreeId(2).ToString().IsArtificial().Should().BeTrue();
        ObjectId.CreateIndexId(2).ToString().IsArtificial().Should().BeTrue();
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase(" ", false)]
    [TestCase("0000", false)]
    [TestCase(GitRevision.WorkTreeGuid, true)]
    [TestCase(GitRevision.IndexGuid, false)]
    [TestCase(GitRevision.CombinedDiffGuid, false)]
    public void IsArtificialWorkTree_tests(string? sha1, bool expected)
    {
        sha1.IsArtificialWorkTree().Should().Be(expected);
    }

    [Test]
    public void IsArtificialWorkTree_should_return_true_for_generated_worktree_id()
    {
        ObjectId.CreateWorkTreeId(2).ToString().IsArtificialWorkTree().Should().BeTrue();
        ObjectId.CreateIndexId(2).ToString().IsArtificialWorkTree().Should().BeFalse();
    }
}
