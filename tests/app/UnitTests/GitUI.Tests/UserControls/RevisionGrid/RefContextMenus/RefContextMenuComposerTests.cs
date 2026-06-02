using GitExtensions.Extensibility.Git;
using GitUI.UserControls.RevisionGrid.RefContextMenus;
using NSubstitute;

namespace GitUITests.UserControls.RevisionGrid.RefContextMenus;
public class RefContextMenuComposerTests
{
    private IGitUICommands _uiCommands = null!;
    private RefContextMenuContext _context = null!;

    [SetUp]
    public void Setup()
    {
        _uiCommands = Substitute.For<IGitUICommands>();
        _context = new RefContextMenuContext
        {
            UICommands = _uiCommands,
            ParentForm = null,
            CurrentBranchRef = "refs/heads/main",
            CurrentBranchName = "main",
            CurrentCheckout = ObjectId.Random(),
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

    [Test]
    public void Build_should_return_null_when_no_provider_handles_ref()
    {
        IRefContextMenuProvider provider = Substitute.For<IRefContextMenuProvider>();
        provider.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(false);

        RefContextMenuComposer composer = new([provider]);

        ContextMenuStrip? menu = composer.Build(gitRef: null, stashReflogSelector: null, _context);

        menu.Should().BeNull();
    }

    [Test]
    public void Build_should_return_null_when_provider_handles_but_produces_no_items()
    {
        IRefContextMenuProvider provider = Substitute.For<IRefContextMenuProvider>();
        provider.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(true);

        RefContextMenuComposer composer = new([provider]);

        ContextMenuStrip? menu = composer.Build(gitRef: null, stashReflogSelector: null, _context);

        menu.Should().BeNull();
    }

    [Test]
    public void Build_should_return_menu_with_copy_branch_name_for_head_ref()
    {
        IRefContextMenuProvider provider = Substitute.For<IRefContextMenuProvider>();
        provider.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(true);
        provider.When(p => p.Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>()))
            .Do(ci => ci.Arg<ContextMenuStrip>().Items.Add(new ToolStripMenuItem("Test")));

        RefContextMenuComposer composer = new([provider]);

        IGitRef gitRef = Substitute.For<IGitRef>();
        gitRef.Name.Returns("feature/test");
        gitRef.IsHead.Returns(true);

        ContextMenuStrip? menu = composer.Build(gitRef, stashReflogSelector: null, _context);

        menu.Should().NotBeNull();
        // Provider item + separator + copy item
        menu!.Items.Count.Should().Be(3);
        menu.Items[0].Text.Should().Be("Test");
        menu.Items[1].Should().BeOfType<ToolStripSeparator>();
        menu.Items[2].Text.Should().Contain("branch name");

        menu.Dispose();
    }

    [Test]
    public void Build_should_return_menu_with_copy_tag_name_for_tag_ref()
    {
        IRefContextMenuProvider provider = Substitute.For<IRefContextMenuProvider>();
        provider.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(true);
        provider.When(p => p.Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>()))
            .Do(ci => ci.Arg<ContextMenuStrip>().Items.Add(new ToolStripMenuItem("Test")));

        RefContextMenuComposer composer = new([provider]);

        IGitRef gitRef = Substitute.For<IGitRef>();
        gitRef.Name.Returns("v1.0");
        gitRef.IsTag.Returns(true);

        ContextMenuStrip? menu = composer.Build(gitRef, stashReflogSelector: null, _context);

        menu.Should().NotBeNull();
        menu!.Items[^1].Text.Should().Contain("tag name");

        menu.Dispose();
    }

    [Test]
    public void Build_should_return_menu_with_generic_copy_name_for_stash()
    {
        IRefContextMenuProvider provider = Substitute.For<IRefContextMenuProvider>();
        provider.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(true);
        provider.When(p => p.Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>()))
            .Do(ci => ci.Arg<ContextMenuStrip>().Items.Add(new ToolStripMenuItem("Test")));

        RefContextMenuComposer composer = new([provider]);

        ContextMenuStrip? menu = composer.Build(gitRef: null, stashReflogSelector: "stash@{0}", _context);

        menu.Should().NotBeNull();
        menu!.Items[^1].Text.Should().Contain("Copy name");

        menu.Dispose();
    }

    [Test]
    public void Build_should_include_copy_worktree_path_for_head_ref_with_worktree()
    {
        const string worktreePath = @"C:\repo-worktree";
        RefContextMenuContext worktreeContext = new()
        {
            UICommands = _uiCommands,
            ParentForm = null,
            CurrentBranchRef = "refs/heads/main",
            CurrentBranchName = "main",
            CurrentCheckout = ObjectId.Random(),
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

        IRefContextMenuProvider provider = Substitute.For<IRefContextMenuProvider>();
        provider.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(true);
        provider.When(p => p.Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>()))
            .Do(ci => ci.Arg<ContextMenuStrip>().Items.Add(new ToolStripMenuItem("Test")));

        RefContextMenuComposer composer = new([provider]);

        IGitRef gitRef = Substitute.For<IGitRef>();
        gitRef.Name.Returns("feature");
        gitRef.IsHead.Returns(true);

        ContextMenuStrip? menu = composer.Build(gitRef, stashReflogSelector: null, worktreeContext);

        menu.Should().NotBeNull();

        // Provider item + separator + copy branch name + copy worktree path (+ optional VS Code items)
        menu!.Items.Count.Should().BeGreaterThanOrEqualTo(4);
        menu.Items[2].Text.Should().Contain("branch name");
        menu.Items[3].Text.Should().Contain("worktree");

        menu.Dispose();
    }

    [Test]
    public void Build_should_not_include_copy_worktree_path_when_no_worktree()
    {
        IRefContextMenuProvider provider = Substitute.For<IRefContextMenuProvider>();
        provider.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(true);
        provider.When(p => p.Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>()))
            .Do(ci => ci.Arg<ContextMenuStrip>().Items.Add(new ToolStripMenuItem("Test")));

        RefContextMenuComposer composer = new([provider]);

        IGitRef gitRef = Substitute.For<IGitRef>();
        gitRef.Name.Returns("feature");
        gitRef.IsHead.Returns(true);

        ContextMenuStrip? menu = composer.Build(gitRef, stashReflogSelector: null, _context);

        menu.Should().NotBeNull();
        // Provider item + separator + copy branch name (no worktree path)
        menu!.Items.Count.Should().Be(3);
        menu.Items[^1].Text.Should().NotContain("worktree");

        menu.Dispose();
    }

    [Test]
    public void Build_should_use_first_matching_provider()
    {
        IRefContextMenuProvider first = Substitute.For<IRefContextMenuProvider>();
        first.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(true);
        first.When(p => p.Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>()))
            .Do(ci => ci.Arg<ContextMenuStrip>().Items.Add(new ToolStripMenuItem("First")));

        IRefContextMenuProvider second = Substitute.For<IRefContextMenuProvider>();
        second.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(true);
        second.When(p => p.Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>()))
            .Do(ci => ci.Arg<ContextMenuStrip>().Items.Add(new ToolStripMenuItem("Second")));

        RefContextMenuComposer composer = new([first, second]);

        ContextMenuStrip? menu = composer.Build(gitRef: null, stashReflogSelector: "stash@{0}", _context);

        menu.Should().NotBeNull();
        menu!.Items[0].Text.Should().Be("First");
        second.DidNotReceive().Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>());

        menu.Dispose();
    }

    [Test]
    public void Build_should_use_stashReflogSelector_for_copy_when_gitRef_is_null()
    {
        IRefContextMenuProvider provider = Substitute.For<IRefContextMenuProvider>();
        provider.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(true);
        provider.When(p => p.Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>()))
            .Do(ci => ci.Arg<ContextMenuStrip>().Items.Add(new ToolStripMenuItem("Item")));

        RefContextMenuComposer composer = new([provider]);

        ContextMenuStrip? menu = composer.Build(gitRef: null, stashReflogSelector: "stash@{0}", _context);

        menu.Should().NotBeNull();
        // Last item is the copy item
        menu!.Items[^1].Should().BeOfType<ToolStripMenuItem>();

        menu.Dispose();
    }

    [Test]
    public void Build_should_skip_provider_that_does_not_handle()
    {
        IRefContextMenuProvider nonHandler = Substitute.For<IRefContextMenuProvider>();
        nonHandler.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(false);

        IRefContextMenuProvider handler = Substitute.For<IRefContextMenuProvider>();
        handler.Handles(Arg.Any<IGitRef?>(), Arg.Any<string?>()).Returns(true);
        handler.When(p => p.Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>()))
            .Do(ci => ci.Arg<ContextMenuStrip>().Items.Add(new ToolStripMenuItem("Handled")));

        RefContextMenuComposer composer = new([nonHandler, handler]);

        ContextMenuStrip? menu = composer.Build(gitRef: null, stashReflogSelector: "stash@{0}", _context);

        menu.Should().NotBeNull();
        nonHandler.DidNotReceive().Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>());
        handler.Received(1).Populate(Arg.Any<ContextMenuStrip>(), Arg.Any<IGitRef?>(), Arg.Any<string?>(), Arg.Any<RefContextMenuContext>());

        menu!.Dispose();
    }
}
