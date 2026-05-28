using GitExtensions.Extensibility.Git;
using GitUI.UserControls.RevisionGrid.RefContextMenus;
using NSubstitute;

namespace GitUITests.UserControls.RevisionGrid.RefContextMenus;
public class LocalBranchContextMenuProviderTests
{
    private LocalBranchContextMenuProvider _provider = null!;
    private IGitUICommands _uiCommands = null!;
    private RefContextMenuContext _context = null!;
    private ObjectId _currentCheckout = ObjectId.Random();

    [SetUp]
    public void Setup()
    {
        _provider = new LocalBranchContextMenuProvider();
        _uiCommands = Substitute.For<IGitUICommands>();
        _currentCheckout = ObjectId.Random();
        _context = new RefContextMenuContext
        {
            UICommands = _uiCommands,
            ParentForm = null,
            CurrentBranchRef = "refs/heads/main",
            CurrentBranchName = "main",
            CurrentCheckout = _currentCheckout,
            IsBareRepository = false,
            GetRefUnambiguousName = r => r.Name,
            GetLatestSelectedRevision = () => null,
            PerformRefreshRevisions = () => { },
            DropStash = (_, _) => { },
            GetWorktreePathForBranch = _ => null,
            ShowFormDiff = (_, _, _, _) => { },
            IsAncestorOf = (_, _) => false,
            GoToRevision = _ => { },
            FindLocalBranchTrackingRemote = _ => null,
        };
    }

    [TearDown]
    public void TearDown()
    {
        ((IDisposable)_provider).Dispose();
    }

    [Test]
    public void Handles_should_return_true_for_head_ref()
    {
        IGitRef gitRef = Substitute.For<IGitRef>();
        gitRef.IsHead.Returns(true);

        _provider.Handles(gitRef, stashReflogSelector: null).Should().BeTrue();
    }

    [Test]
    public void Handles_should_return_false_for_remote_ref()
    {
        IGitRef gitRef = Substitute.For<IGitRef>();
        gitRef.IsHead.Returns(false);
        gitRef.IsRemote.Returns(true);

        _provider.Handles(gitRef, stashReflogSelector: null).Should().BeFalse();
    }

    [Test]
    public void Handles_should_return_false_for_tag_ref()
    {
        IGitRef gitRef = Substitute.For<IGitRef>();
        gitRef.IsHead.Returns(false);
        gitRef.IsTag.Returns(true);

        _provider.Handles(gitRef, stashReflogSelector: null).Should().BeFalse();
    }

    [Test]
    public void Handles_should_return_false_for_null_ref()
    {
        _provider.Handles(gitRef: null, stashReflogSelector: null).Should().BeFalse();
    }

    [Test]
    public void Populate_should_add_nothing_when_gitRef_is_null()
    {
        using ContextMenuStrip menu = new();
        _provider.Populate(menu, gitRef: null, stashReflogSelector: null, _context);

        menu.Items.Count.Should().Be(0);
    }

    [Test]
    public void Populate_should_include_checkout_for_non_current_branch()
    {
        IGitRef gitRef = CreateLocalBranchRef("feature", ObjectId.Random());
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text?.Replace("&", ""))
            .Should().Contain(t => t.Contains("Checkout"));
    }

    [Test]
    public void Populate_should_not_include_checkout_for_current_branch()
    {
        IGitRef gitRef = CreateLocalBranchRef("main", ObjectId.Random());
        gitRef.CompleteName.Returns("refs/heads/main");
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text)
            .Should().NotContain(t => t.Contains("Checkout"));
    }

    [Test]
    public void Populate_should_include_merge_and_rebase_for_non_current_head_branch()
    {
        IGitRef gitRef = CreateLocalBranchRef("feature", ObjectId.Random());
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        IEnumerable<string> texts = menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text!);
        texts.Should().Contain(t => t.Contains("Merge"));
        texts.Should().Contain(t => t.Contains("Rebase"));
    }

    [Test]
    public void Populate_should_include_fast_forward_for_descendant_branch()
    {
        ObjectId featureId = ObjectId.Random();
        IGitRef gitRef = CreateLocalBranchRef("feature", featureId);
        using ContextMenuStrip menu = new();

        RefContextMenuContext context = CreateContext(
            isAncestorOf: (ancestorId, descendantId) => ancestorId == _currentCheckout && descendantId == featureId);

        _provider.Populate(menu, gitRef, stashReflogSelector: null, context);

        menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text?.Replace("&", ""))
            .Should().Contain(t => t == "Fast-forward to this branch");
    }

    [Test]
    public void Populate_should_include_fast_forward_for_descendant_branch_checked_out_in_worktree()
    {
        ObjectId featureId = ObjectId.Random();
        IGitRef gitRef = CreateLocalBranchRef("feature", featureId);
        using ContextMenuStrip menu = new();

        RefContextMenuContext context = CreateContext(
            getWorktreePathForBranch: name => name == "feature" ? @"C:\repo-wt" : null,
            isAncestorOf: (ancestorId, descendantId) => ancestorId == _currentCheckout && descendantId == featureId);

        _provider.Populate(menu, gitRef, stashReflogSelector: null, context);

        IEnumerable<string?> texts = menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text?.Replace("&", ""));
        texts.Should().Contain(t => t == "Open branch's worktree");
        texts.Should().Contain(t => t == "Fast-forward to this branch");
        texts.Should().NotContain(t => t != null && t.Contains("Checkout"));
    }

    [Test]
    public void Populate_should_not_include_merge_or_rebase_when_at_current_head()
    {
        IGitRef gitRef = CreateLocalBranchRef("feature", _currentCheckout);
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text)
            .Should().NotContain(t => t.Contains("Merge"))
            .And.NotContain(t => t.Contains("Rebase"));
    }

    [Test]
    public void Populate_should_include_delete_for_non_current_branch()
    {
        IGitRef gitRef = CreateLocalBranchRef("feature", ObjectId.Random());
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text)
            .Should().Contain(t => t.Contains("Delete"));
    }

    [Test]
    public void Populate_should_not_include_delete_for_current_branch()
    {
        IGitRef gitRef = CreateLocalBranchRef("main", ObjectId.Random());
        gitRef.CompleteName.Returns("refs/heads/main");
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text)
            .Should().NotContain(t => t.Contains("Delete"));
    }

    [Test]
    public void Populate_should_always_include_rename()
    {
        IGitRef gitRef = CreateLocalBranchRef("main", ObjectId.Random());
        gitRef.CompleteName.Returns("refs/heads/main");
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text)
            .Should().Contain(t => t.Contains("name"));
    }

    [Test]
    public void Populate_should_include_push_for_non_bare_repository()
    {
        IGitRef gitRef = CreateLocalBranchRef("feature", ObjectId.Random());
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text?.Replace("&", ""))
            .Should().Contain(t => t.Contains("Push"));
    }

    [Test]
    public void Populate_should_not_include_push_for_bare_repository()
    {
        RefContextMenuContext bareContext = new()
        {
            UICommands = _uiCommands,
            ParentForm = null,
            CurrentBranchRef = "refs/heads/main",
            CurrentBranchName = "main",
            CurrentCheckout = _currentCheckout,
            IsBareRepository = true,
            GetRefUnambiguousName = r => r.Name,
            GetLatestSelectedRevision = () => null,
            PerformRefreshRevisions = () => { },
            DropStash = (_, _) => { },
            GetWorktreePathForBranch = _ => null,
            ShowFormDiff = (_, _, _, _) => { },
            IsAncestorOf = (_, _) => false,
            GoToRevision = _ => { },
            FindLocalBranchTrackingRemote = _ => null,
        };

        IGitRef gitRef = CreateLocalBranchRef("feature", ObjectId.Random());
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, bareContext);

        menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text)
            .Should().NotContain(t => t.Contains("Push"));
    }

    [Test]
    public void Populate_should_show_open_worktree_when_branch_is_in_another_worktree()
    {
        const string worktreePath = @"C:\repo-worktree";
        RefContextMenuContext worktreeContext = new()
        {
            UICommands = _uiCommands,
            ParentForm = null,
            CurrentBranchRef = "refs/heads/main",
            CurrentBranchName = "main",
            CurrentCheckout = _currentCheckout,
            IsBareRepository = false,
            GetRefUnambiguousName = r => r.Name,
            GetLatestSelectedRevision = () => null,
            PerformRefreshRevisions = () => { },
            DropStash = (_, _) => { },
            GetWorktreePathForBranch = name => name == "feature" ? worktreePath : null,
            ShowFormDiff = (_, _, _, _) => { },
            IsAncestorOf = (_, _) => false,
            GoToRevision = _ => { },
            FindLocalBranchTrackingRemote = _ => null,
        };

        IGitRef gitRef = CreateLocalBranchRef("feature", ObjectId.Random());
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, worktreeContext);

        IEnumerable<string?> texts = menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text);
        texts.Should().Contain(t => t != null && t.Contains("worktree"));
        texts.Should().NotContain(t => t != null && t.Contains("Checkout"));
    }

    [Test]
    public void Populate_should_show_checkout_when_branch_is_not_in_another_worktree()
    {
        IGitRef gitRef = CreateLocalBranchRef("feature", ObjectId.Random());
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text?.Replace("&", ""))
            .Should().Contain(t => t != null && t.Contains("Checkout"))
            .And.NotContain(t => t != null && t.Contains("worktree"));
    }

    [Test]
    public void Populate_should_show_delete_branch_and_worktree_when_branch_is_in_another_worktree()
    {
        RefContextMenuContext worktreeContext = new()
        {
            UICommands = _uiCommands,
            ParentForm = null,
            CurrentBranchRef = "refs/heads/main",
            CurrentBranchName = "main",
            CurrentCheckout = _currentCheckout,
            IsBareRepository = false,
            GetRefUnambiguousName = r => r.Name,
            GetLatestSelectedRevision = () => null,
            PerformRefreshRevisions = () => { },
            DropStash = (_, _) => { },
            GetWorktreePathForBranch = name => name == "feature" ? @"C:\repo-wt" : null,
            ShowFormDiff = (_, _, _, _) => { },
            IsAncestorOf = (_, _) => false,
            GoToRevision = _ => { },
            FindLocalBranchTrackingRemote = _ => null,
        };

        IGitRef gitRef = CreateLocalBranchRef("feature", ObjectId.Random());
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, worktreeContext);

        IEnumerable<string?> texts = menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text?.Replace("&", ""));
        texts.Should().Contain(t => t != null && t.Contains("Delete branch and worktree"));
        texts.Should().NotContain(t => t != null && t.Contains("Delete this branch"));
    }

    [Test]
    public void Populate_should_show_regular_delete_when_branch_is_not_in_worktree()
    {
        IGitRef gitRef = CreateLocalBranchRef("feature", ObjectId.Random());
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        IEnumerable<string?> texts = menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text?.Replace("&", ""));
        texts.Should().Contain(t => t != null && t.Contains("Delete this branch"));
    }

    [Test]
    public void Populate_should_include_diff_items_for_non_current_head_branch()
    {
        IGitRef gitRef = CreateLocalBranchRef("feature", ObjectId.Random());
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        IEnumerable<string> texts = menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text!.Replace("&", ""));
        texts.Should().Contain(t => t.Contains("current") && t.Contains("→"))
            .And.HaveCountGreaterThanOrEqualTo(2, "both diff directions should appear");
    }

    [Test]
    public void Populate_should_not_include_diff_items_when_at_current_head()
    {
        IGitRef gitRef = CreateLocalBranchRef("feature", _currentCheckout);
        using ContextMenuStrip menu = new();

        _provider.Populate(menu, gitRef, stashReflogSelector: null, _context);

        menu.Items.Cast<ToolStripItem>()
            .Where(i => i is not ToolStripSeparator)
            .Select(i => i.Text)
            .Should().NotContain(t => t != null && t.Contains("→"));
    }

    [Test]
    public void Populate_should_invoke_ShowFormDiff_with_correct_order_for_diff_current_to_this()
    {
        ObjectId featureId = ObjectId.Random();
        IGitRef gitRef = CreateLocalBranchRef("feature", featureId);
        using ContextMenuStrip menu = new();

        (ObjectId capturedBase, ObjectId capturedHead, string capturedBaseStr, string capturedHeadStr) captured = default;
        RefContextMenuContext context = new()
        {
            UICommands = _uiCommands,
            ParentForm = null,
            CurrentBranchRef = "refs/heads/main",
            CurrentBranchName = "main",
            CurrentCheckout = _currentCheckout,
            IsBareRepository = false,
            GetRefUnambiguousName = r => r.Name,
            GetLatestSelectedRevision = () => null,
            PerformRefreshRevisions = () => { },
            DropStash = (_, _) => { },
            GetWorktreePathForBranch = _ => null,
            ShowFormDiff = (b, h, bs, hs) => captured = (b, h, bs, hs),
            IsAncestorOf = (_, _) => false,
            GoToRevision = _ => { },
            FindLocalBranchTrackingRemote = _ => null,
        };

        _provider.Populate(menu, gitRef, stashReflogSelector: null, context);

        ToolStripItem diffItem = menu.Items.Cast<ToolStripItem>()
            .First(i => i is not ToolStripSeparator && i.Text?.Contains("current") is true && i.Text?.Contains("→") is true && !i.Text.StartsWith("Diff this"));
        diffItem.PerformClick();

        captured.capturedBase.Should().Be(_currentCheckout);
        captured.capturedHead.Should().Be(featureId);
        captured.capturedBaseStr.Should().Be("main");
        captured.capturedHeadStr.Should().Be("feature");
    }

    [Test]
    public void Populate_should_invoke_ShowFormDiff_with_correct_order_for_diff_this_to_current()
    {
        ObjectId featureId = ObjectId.Random();
        IGitRef gitRef = CreateLocalBranchRef("feature", featureId);
        using ContextMenuStrip menu = new();

        (ObjectId capturedBase, ObjectId capturedHead, string capturedBaseStr, string capturedHeadStr) captured = default;
        RefContextMenuContext context = new()
        {
            UICommands = _uiCommands,
            ParentForm = null,
            CurrentBranchRef = "refs/heads/main",
            CurrentBranchName = "main",
            CurrentCheckout = _currentCheckout,
            IsBareRepository = false,
            GetRefUnambiguousName = r => r.Name,
            GetLatestSelectedRevision = () => null,
            PerformRefreshRevisions = () => { },
            DropStash = (_, _) => { },
            GetWorktreePathForBranch = _ => null,
            ShowFormDiff = (b, h, bs, hs) => captured = (b, h, bs, hs),
            IsAncestorOf = (_, _) => false,
            GoToRevision = _ => { },
            FindLocalBranchTrackingRemote = _ => null,
        };

        _provider.Populate(menu, gitRef, stashReflogSelector: null, context);

        ToolStripItem diffItem = menu.Items.Cast<ToolStripItem>()
            .First(i => i is not ToolStripSeparator && i.Text?.StartsWith("Diff this") is true);
        diffItem.PerformClick();

        captured.capturedBase.Should().Be(featureId);
        captured.capturedHead.Should().Be(_currentCheckout);
        captured.capturedBaseStr.Should().Be("feature");
        captured.capturedHeadStr.Should().Be("main");
    }

    private RefContextMenuContext CreateContext(
        Func<string, string?>? getWorktreePathForBranch = null,
        Func<ObjectId, ObjectId, bool>? isAncestorOf = null)
    {
        return new RefContextMenuContext
        {
            UICommands = _uiCommands,
            ParentForm = null,
            CurrentBranchRef = "refs/heads/main",
            CurrentBranchName = "main",
            CurrentCheckout = _currentCheckout,
            IsBareRepository = false,
            GetRefUnambiguousName = r => r.Name,
            GetLatestSelectedRevision = () => null,
            PerformRefreshRevisions = () => { },
            DropStash = (_, _) => { },
            GetWorktreePathForBranch = getWorktreePathForBranch ?? (_ => null),
            ShowFormDiff = (_, _, _, _) => { },
            IsAncestorOf = isAncestorOf ?? ((_, _) => false),
            GoToRevision = _ => { },
            FindLocalBranchTrackingRemote = _ => null,
        };
    }

    private static IGitRef CreateLocalBranchRef(string name, ObjectId objectId)
    {
        IGitRef gitRef = Substitute.For<IGitRef>();
        gitRef.IsHead.Returns(true);
        gitRef.IsRemote.Returns(false);
        gitRef.IsTag.Returns(false);
        gitRef.Name.Returns(name);
        gitRef.CompleteName.Returns($"refs/heads/{name}");
        gitRef.ObjectId.Returns(objectId);
        return gitRef;
    }
}
