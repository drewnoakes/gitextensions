using CommonTestUtils;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitCommandsTests.Git;
public sealed class GitModuleWorktreeTests
{
    private const string LogCommandPrefix = "-c log.showsignature=false log --no-walk --format=\"%H %aI\"";

    private GitModule _gitModule = null!;
    private MockExecutable _executable = null!;

    [SetUp]
    public void SetUp()
    {
        // Clear the static GitVersion cache so the mock executable is always queried for --version.
        GitVersion.ResetVersion();
        _executable = new MockExecutable();
        _executable.StageOutput("--version", $"git version {GitVersion.LastRecommendedVersion}");
        _gitModule = GetGitModuleWithExecutable(_executable);
    }

    [TearDown]
    public void TearDown()
    {
        _executable.Verify();
    }

    [Test]
    public void GetWorktrees_should_parse_single_worktree_with_branch()
    {
        string sha = "abc1234abc1234abc1234abc1234abc1234abc12";
        string output = string.Join('\0',
            "worktree C:/repos/main",
            $"HEAD {sha}",
            "branch refs/heads/master",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(1);
            worktrees[0].Path.Should().Be("C:\\repos\\main");
            worktrees[0].HeadType.Should().Be(GitWorktreeHeadType.Branch);
            worktrees[0].Sha1.Should().Be(sha);
            worktrees[0].Branch.Should().Be("master");
            worktrees[0].IsDeleted.Should().BeTrue();
            worktrees[0].LastCommitDate.Should().BeNull();
        }
    }

    [Test]
    public void GetWorktrees_should_parse_detached_head()
    {
        string sha = "def5678def5678def5678def5678def5678def56";
        string output = string.Join('\0',
            "worktree C:/repos/detached",
            $"HEAD {sha}",
            "detached",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(1);
            worktrees[0].HeadType.Should().Be(GitWorktreeHeadType.Detached);
            worktrees[0].Sha1.Should().Be(sha);
            worktrees[0].Branch.Should().BeNull();
        }
    }

    [Test]
    public void GetWorktrees_should_parse_bare_repo()
    {
        string output = string.Join('\0',
            "worktree C:/repos/bare",
            "bare",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(1);
            worktrees[0].HeadType.Should().Be(GitWorktreeHeadType.Bare);
            worktrees[0].Sha1.Should().BeNull();
            worktrees[0].Branch.Should().BeNull();
            worktrees[0].LastCommitDate.Should().BeNull();
        }
    }

    [Test]
    public void GetWorktrees_should_parse_multiple_worktrees()
    {
        string sha1 = "aaaa1234aaaa1234aaaa1234aaaa1234aaaa1234";
        string sha2 = "bbbb5678bbbb5678bbbb5678bbbb5678bbbb5678";
        string output = string.Join('\0',
            "worktree C:/repos/main",
            $"HEAD {sha1}",
            "branch refs/heads/master",
            "", // end of first record
            "worktree C:/repos/feature",
            $"HEAD {sha2}",
            "branch refs/heads/feature/my-feature",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(2);

            worktrees[0].Path.Should().Be("C:\\repos\\main");
            worktrees[0].Branch.Should().Be("master");

            worktrees[1].Path.Should().Be("C:\\repos\\feature");
            worktrees[1].Branch.Should().Be("feature/my-feature");
        }
    }

    [Test]
    public void GetWorktrees_should_handle_path_with_spaces()
    {
        string output = string.Join('\0',
            "worktree C:/my repos/work tree",
            "HEAD abc1234abc1234abc1234abc1234abc1234abc12",
            "branch refs/heads/main",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(1);
            worktrees[0].Path.Should().Be("C:\\my repos\\work tree");
            worktrees[0].Branch.Should().Be("main");
        }
    }

    [Test]
    public void GetWorktrees_should_strip_refs_heads_prefix_from_branch()
    {
        string output = string.Join('\0',
            "worktree C:/repos/main",
            "HEAD abc1234abc1234abc1234abc1234abc1234abc12",
            "branch refs/heads/feature/nested/branch",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees();

            worktrees[0].Branch.Should().Be("feature/nested/branch");
        }
    }

    [Test]
    public void GetWorktrees_should_return_empty_list_for_empty_output()
    {
        using (_executable.StageOutput("worktree list --porcelain -z", ""))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees();

            worktrees.Should().BeEmpty();
        }
    }

    [Test]
    public void GetWorktrees_should_populate_last_commit_date()
    {
        string sha = "abc1234abc1234abc1234abc1234abc1234abc12";
        string output = string.Join('\0',
            "worktree C:/repos/main",
            $"HEAD {sha}",
            "branch refs/heads/main",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        using (_executable.StageOutput($"{LogCommandPrefix} {sha}", $"{sha} 2025-05-20T10:30:00+10:00\n"))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees(includeCommitDates: true);

            worktrees.Should().HaveCount(1);
            worktrees[0].LastCommitDate.Should().Be(new DateTimeOffset(2025, 5, 20, 10, 30, 0, TimeSpan.FromHours(10)).LocalDateTime);
        }
    }

    [Test]
    public void GetWorktrees_should_handle_missing_date_gracefully()
    {
        string sha = "abc1234abc1234abc1234abc1234abc1234abc12";
        string output = string.Join('\0',
            "worktree C:/repos/main",
            $"HEAD {sha}",
            "branch refs/heads/main",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        using (_executable.StageOutput($"{LogCommandPrefix} {sha}", ""))
        {
            IReadOnlyList<GitWorktree> worktrees = _gitModule.GetWorktrees(includeCommitDates: true);

            worktrees.Should().HaveCount(1);
            worktrees[0].LastCommitDate.Should().BeNull();
        }
    }

    private static GitModule GetGitModuleWithExecutable(IExecutable executable)
    {
        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), "");

        GitExecutor.TestAccessor executorAccessor = module.GetTestAccessor().Executor;
        executorAccessor.GitExecutable = executable;
        executorAccessor.GitWindowsExecutable = executable;
        executorAccessor.GitCommandRunner = new GitCommandRunner(executable, () => GitModule.SystemEncoding);

        return module;
    }
}
