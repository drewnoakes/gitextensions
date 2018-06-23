using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.Drawing;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using GitCommands;
using GitCommands.Config;
using GitCommands.Git;
using GitUI.BuildServerIntegration;
using GitUI.CommandsDialogs;
using GitUI.CommandsDialogs.BrowseDialog;
using GitUI.HelperDialogs;
using GitUI.Hotkey;
using GitUI.Script;
using GitUI.UserControls;
using GitUI.UserControls.RevisionGrid;
using GitUI.UserControls.RevisionGrid.Columns;
using GitUIPluginInterfaces;
using JetBrains.Annotations;
using Microsoft.VisualStudio.Threading;
using ResourceManager;

namespace GitUI
{
    public enum RevisionGraphDrawStyleEnum
    {
        Normal,
        DrawNonRelativesGray,
        HighlightSelected
    }

    [DefaultEvent("DoubleClick")]
    public sealed partial class RevisionGridControl : GitModuleControl
    {
        public event EventHandler<DoubleClickRevisionEventArgs> DoubleClickRevision;
        public event EventHandler<EventArgs> ShowFirstParentsToggled;
        public event EventHandler SelectionChanged;

        public static readonly string HotkeySettingsName = "RevisionGrid";

        private readonly TranslationString _droppingFilesBlocked = new TranslationString("For you own protection dropping more than 10 patch files at once is blocked!");
        private readonly TranslationString _cannotHighlightSelectedBranch = new TranslationString("Cannot highlight selected branch when revision graph is loading.");
        private readonly TranslationString _noRevisionFoundError = new TranslationString("No revision found.");
        private readonly TranslationString _baseForCompareNotSelectedError = new TranslationString("Base commit for compare is not selected.");
        private readonly TranslationString _strError = new TranslationString("Error");

        private readonly FormRevisionFilter _revisionFilter = new FormRevisionFilter();
        private readonly NavigationHistory _navigationHistory = new NavigationHistory();
        private readonly LoadingControl _loadingImage;
        private readonly RevisionGridToolTipProvider _toolTipProvider;
        private readonly QuickSearchProvider _quickSearchProvider;
        private readonly IGitRevisionTester _gitRevisionTester;
        private readonly ParentChildNavigationHistory _parentChildNavigationHistory;
        private readonly AuthorRevisionHighlighting _authorHighlighting;
        private readonly Lazy<IndexWatcher> _indexWatcher;
        private readonly BuildServerWatcher _buildServerWatcher;

        private RefFilterOptions _refFilterOptions = RefFilterOptions.All | RefFilterOptions.Boundary;
        private IEnumerable<IGitRef> _latestRefs = Enumerable.Empty<IGitRef>();
        private bool _initialLoad = true;

        /// <summary>Tracks status for the artificial commits while the revision graph is reloading</summary>
        private IReadOnlyList<GitItemStatus> _artificialStatus;
        private string _initialSelectedRevision;
        private RevisionReader _revisionReader;
        private IDisposable _revisionSubscription;
        private GitRevision _baseCommitToCompare;
        private string _rebaseOnTopOf;
        private bool _isRefreshingRevisions;
        private bool _isLoading;
        private string[] _lastSelectedRows;
        private string _fixedRevisionFilter = "";
        private string _fixedPathFilter = "";
        private string _branchFilter = string.Empty;
        private JoinableTask<SuperProjectInfo> _superprojectCurrentCheckout;
        private int _latestSelectedRowIndex;
        private string _filteredCurrentCheckout;
        private string[] _currentCheckoutParents;
        private bool _settingsLoaded;

        [Browsable(false)] public Action OnToggleBranchTreePanelRequested { get; set; }
        [Browsable(false)] public string QuickRevisionFilter { get; set; } = "";
        [Browsable(false)] public bool InMemFilterIgnoreCase { get; set; } = true;
        [Browsable(false)] public string InMemAuthorFilter { get; set; } = "";
        [Browsable(false)] public string InMemCommitterFilter { get; set; } = "";
        [Browsable(false)] public string InMemMessageFilter { get; set; } = "";
        [Browsable(false)] public string CurrentCheckout { get; private set; }
        [Browsable(false)] public bool ShowUncommittedChangesIfPossible { get; set; }
        [Browsable(false)] public bool ShowBuildServerInfo { get; set; }
        [Browsable(false)] public bool DoubleClickDoesNotOpenCommitInfo { get; set; }

        internal RevisionGridMenuCommands MenuCommands { get; }
        internal bool IsShowCurrentBranchOnlyChecked { get; private set; }
        internal bool IsShowAllBranchesChecked { get; private set; }
        internal bool IsShowFilteredBranchesChecked { get; private set; }

        /// <summary>
        /// Refs loaded while the latest processing of git log
        /// </summary>
        private IEnumerable<IGitRef> LatestRefs
        {
            get { return _latestRefs; }
            set
            {
                _latestRefs = value;
                AmbiguousRefs = null;
            }
        }

        public RevisionGridControl()
        {
            InitLayout();
            InitializeComponent();

            _loadingImage = new LoadingControl();

            _toolTipProvider = new RevisionGridToolTipProvider(this);

            _quickSearchProvider = new QuickSearchProvider(this);

            // Parent-child navigation can expect that SetSelectedRevision is always successful since it always uses first-parents
            _parentChildNavigationHistory = new ParentChildNavigationHistory(revision => SetSelectedRevision(revision));
            _authorHighlighting = new AuthorRevisionHighlighting();
            _indexWatcher = new Lazy<IndexWatcher>(() => new IndexWatcher(UICommandsSource));

            Translate();

            _gitRevisionTester = new GitRevisionTester(new FullPathResolver(() => Module.WorkingDir));

            copyToClipboardToolStripMenuItem.GetViewModel = () => new CopyContextMenuViewModel(LatestSelectedRevision);

            MenuCommands = new RevisionGridMenuCommands(this);
            MenuCommands.CreateOrUpdateMenuCommands();

            // fill View context menu from MenuCommands
            FillMenuFromMenuCommands(MenuCommands.GetViewMenuCommands(), viewToolStripMenuItem);

            // fill Navigate context menu from MenuCommands
            FillMenuFromMenuCommands(MenuCommands.GetNavigateMenuCommands(), navigateToolStripMenuItem);

            Graph.NormalFont = AppSettings.Font;
            Graph.ShowCellToolTips = false;
            Graph.KeyPress += (_, e) => _quickSearchProvider.OnKeyPress(e);
            Graph.KeyUp += OnGraphKeyUp;
            Graph.KeyDown += OnGraphKeyDown;
            Graph.MouseDown += OnGraphMouseDown;
            Graph.CellMouseDown += OnGraphCellMouseDown;
            Graph.MouseDoubleClick += OnGraphDoubleClick;
            Graph.MouseClick += OnGraphMouseClick;
            Graph.MouseEnter += (_, e) => _toolTipProvider.OnCellMouseEnter();
            Graph.CellMouseMove += (_, e) => _toolTipProvider.OnCellMouseMove(e.ColumnIndex, e.RowIndex);

            SetShowBranches();

            Hotkeys = HotkeySettingsManager.LoadHotkeys(HotkeySettingsName);
            HotkeysEnabled = true;

            Graph.Loading += OnGraphLoading;

            // Allow to drop patch file on revision grid
            Graph.DragEnter += OnGraphDragEnter;
            Graph.DragDrop += OnGraphDragDrop;
            Graph.AllowDrop = true;

            _buildServerWatcher = new BuildServerWatcher(this, Graph, () => Module);

            var graphColumnProvider = new GraphColumnProvider(this);
            Graph.GraphColumnProvider = graphColumnProvider;
            Graph.AddColumn(graphColumnProvider);
            Graph.AddColumn(new MessageColumnProvider(this));
            Graph.AddColumn(new AvatarColumnProvider(Graph));
            Graph.AddColumn(new AuthorColumnProvider(this, _authorHighlighting));
            Graph.AddColumn(new DateColumnProvider(this));
            Graph.AddColumn(new CommitIdColumnProvider(this));
            Graph.AddColumn(_buildServerWatcher.ColumnProvider);

            compareToBaseToolStripMenuItem.Enabled = false;
            fixupCommitToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeys(Commands.CreateFixupCommit).ToShortcutKeyDisplayString();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _revisionSubscription?.Dispose();
                _revisionReader?.Dispose();
                _buildServerWatcher?.Dispose();

                if (_indexWatcher.IsValueCreated)
                {
                    _indexWatcher.Value.Dispose();
                }
            }

            if (disposing)
            {
                components?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void SetPage(Control content)
        {
            Controls.Clear();
            Controls.Add(content);
        }

        internal void DrawColumnText(DataGridViewCellPaintingEventArgs e, string text, Font font, Color color, Rectangle bounds, bool useEllipsis = true)
        {
            var isTruncated = DrawColumnTextTruncated(e.Graphics, text, font, color, bounds, useEllipsis);

            _toolTipProvider.SetTruncation(e.ColumnIndex, e.RowIndex, isTruncated);
        }

        /// <returns>True if the text has been truncated.</returns>
        public static bool DrawColumnTextTruncated(Graphics graphics, string text, Font font, Color color, Rectangle bounds, bool useEllipsis = true)
        {
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

            if (useEllipsis)
            {
                flags |= TextFormatFlags.EndEllipsis;
            }

            var size = TextRenderer.MeasureText(graphics, text, font, graphics.ClipBounds.Size.ToSize(), flags);

            TextRenderer.DrawText(graphics, text, font, bounds, color, flags);

            return size.Width > bounds.Width;
        }

        [Browsable(false)] public IndexWatcher IndexWatcher => _indexWatcher.Value;

        [CanBeNull]
        [Browsable(false)]
        public GitRevision LatestSelectedRevision => IsValidRevisionIndex(_latestSelectedRowIndex) ? GetRevision(_latestSelectedRowIndex) : null;

        [Browsable(false)]
        public bool MultiSelect
        {
            get => Graph.MultiSelect;
            set => Graph.MultiSelect = value;
        }

        public RevisionGraphDrawStyleEnum RevisionGraphDrawStyle
        {
            get => Graph.RevisionGraphDrawStyle;
            set => Graph.RevisionGraphDrawStyle = value;
        }

        private static void FillMenuFromMenuCommands(IEnumerable<MenuCommand> menuCommands, ToolStripDropDownItem targetMenuItem)
        {
            foreach (var menuCommand in menuCommands)
            {
                var toolStripItem = MenuCommand.CreateToolStripItem(menuCommand);

                if (toolStripItem is ToolStripMenuItem toolStripMenuItem)
                {
                    menuCommand.RegisterMenuItem(toolStripMenuItem);
                }

                targetMenuItem.DropDownItems.Add(toolStripItem);
            }
        }

        public void SetFilters((string revision, string path) filter)
        {
            _fixedRevisionFilter = filter.revision;
            _fixedPathFilter = filter.path;
        }

        public void SetInitialRevision(string initialSelectedRevision)
        {
            _initialSelectedRevision = initialSelectedRevision;
        }

        private void OnGraphLoading(object sender, DvcsGraph.LoadingEventArgs e)
        {
            // Since this can happen on a background thread, we'll just set a
            // flag and deal with it next time we paint (a bit of a hack, but
            // it works)
            _isLoading = e.IsLoading;
        }

        #region Quick search

        #endregion

        private void InitiateRefAction([CanBeNull] IReadOnlyList<IGitRef> refs, Action<IGitRef> action, FormQuickGitRefSelector.Action actionLabel)
        {
            if (refs == null || refs.Count < 1)
            {
                return;
            }

            if (refs.Count == 1)
            {
                action(refs[0]);
                return;
            }

            var rect = Graph.GetCellDisplayRectangle(0, _latestSelectedRowIndex, true);
            using (var dlg = new FormQuickGitRefSelector())
            {
                dlg.Init(actionLabel, refs);
                dlg.Location = PointToScreen(new Point(rect.Right, rect.Bottom));
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedRef == null)
                {
                    return;
                }

                action(dlg.SelectedRef);
            }
        }

        private void ResetNavigationHistory()
        {
            var selectedRevisions = GetSelectedRevisions();
            if (selectedRevisions.Count == 1)
            {
                _navigationHistory.Push(selectedRevisions[0].Guid);
            }
            else
            {
                _navigationHistory.Clear();
            }
        }

        public void NavigateBackward()
        {
            if (_navigationHistory.CanNavigateBackward)
            {
                SetSelectedRevision(_navigationHistory.NavigateBackward());
            }
        }

        public void NavigateForward()
        {
            if (_navigationHistory.CanNavigateForward)
            {
                SetSelectedRevision(_navigationHistory.NavigateForward());
            }
        }

        private void ToggleBranchTreePanel()
        {
            OnToggleBranchTreePanelRequested();
        }

        internal void FindNextMatch(int startIndex, string searchString, bool reverse)
        {
            if (Graph.RowCount == 0)
            {
                return;
            }

            var result = reverse
                ? SearchBackwards()
                : SearchForward();

            if (result.HasValue)
            {
                Graph.ClearSelection();
                Graph.Rows[result.Value].Selected = true;

                Graph.CurrentCell = Graph.Rows[result.Value].Cells[1];
            }

            int? SearchForward()
            {
                // Check for out of bounds roll over if required
                int index;
                if (startIndex < 0 || startIndex >= Graph.RowCount)
                {
                    startIndex = 0;
                }

                for (index = startIndex; index < Graph.RowCount; ++index)
                {
                    if (_gitRevisionTester.Matches(GetRevision(index), searchString))
                    {
                        return index;
                    }
                }

                // We didn't find it so start searching from the top
                for (index = 0; index < startIndex; ++index)
                {
                    if (_gitRevisionTester.Matches(GetRevision(index), searchString))
                    {
                        return index;
                    }
                }

                return null;
            }

            int? SearchBackwards()
            {
                // Check for out of bounds roll over if required
                int index;
                if (startIndex < 0 || startIndex >= Graph.RowCount)
                {
                    startIndex = Graph.RowCount - 1;
                }

                for (index = startIndex; index >= 0; --index)
                {
                    if (_gitRevisionTester.Matches(GetRevision(index), searchString))
                    {
                        return index;
                    }
                }

                // We didn't find it so start searching from the bottom
                for (index = Graph.RowCount - 1; index > startIndex; --index)
                {
                    if (_gitRevisionTester.Matches(GetRevision(index), searchString))
                    {
                        return index;
                    }
                }

                return null;
            }
        }

        public void DisableContextMenu()
        {
            Graph.ContextMenuStrip = null;
        }

        public void FormatQuickFilter(string filter,
                                      bool filterCommit,
                                      bool filterCommitter,
                                      bool filterAuthor,
                                      bool filterDiffContent,
                                      out string revListArgs,
                                      out string inMemMessageFilter,
                                      out string inMemCommitterFilter,
                                      out string inMemAuthorFilter)
        {
            revListArgs = string.Empty;
            inMemMessageFilter = string.Empty;
            inMemCommitterFilter = string.Empty;
            inMemAuthorFilter = string.Empty;

            if (!string.IsNullOrEmpty(filter))
            {
                // hash filtering only possible in memory
                var cmdLineSafe = GitCommandHelpers.VersionInUse.IsRegExStringCmdPassable(filter);
                revListArgs = " --regexp-ignore-case ";
                if (filterCommit)
                {
                    if (cmdLineSafe && !ObjectId.IsValidPartial(filter, minLength: 5))
                    {
                        revListArgs += "--grep=\"" + filter + "\" ";
                    }
                    else
                    {
                        inMemMessageFilter = filter;
                    }
                }

                if (filterCommitter && !filter.IsNullOrWhiteSpace())
                {
                    if (cmdLineSafe)
                    {
                        revListArgs += "--committer=\"" + filter + "\" ";
                    }
                    else
                    {
                        inMemCommitterFilter = filter;
                    }
                }

                if (filterAuthor && !filter.IsNullOrWhiteSpace())
                {
                    if (cmdLineSafe)
                    {
                        revListArgs += "--author=\"" + filter + "\" ";
                    }
                    else
                    {
                        inMemAuthorFilter = filter;
                    }
                }

                if (filterDiffContent)
                {
                    if (cmdLineSafe)
                    {
                        revListArgs += "\"-S" + filter + "\" ";
                    }
                    else
                    {
                        throw new InvalidOperationException("Filter text not valid for \"Diff contains\" filter.");
                    }
                }
            }
        }

        public bool SetAndApplyBranchFilter(string filter)
        {
            if (filter == _revisionFilter.GetBranchFilter())
            {
                return false;
            }

            if (filter == "")
            {
                AppSettings.BranchFilterEnabled = false;
                AppSettings.ShowCurrentBranchOnly = true;
            }
            else
            {
                AppSettings.BranchFilterEnabled = true;
                AppSettings.ShowCurrentBranchOnly = false;
                _revisionFilter.SetBranchFilter(filter);
            }

            SetShowBranches();
            return true;
        }

        public override void Refresh()
        {
            if (IsDisposed)
            {
                return;
            }

            Graph.Refresh();

            base.Refresh();

            _toolTipProvider.Clear();
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            _isLoading = true;
            SetPage(_loadingImage);
        }

        public new void Load()
        {
            if (!DesignMode)
            {
                ReloadHotkeys();
            }

            ForceRefreshRevisions();
        }

        private void SetSelectedIndex(int index)
        {
            if (Graph.Rows[index].Selected)
            {
                return;
            }

            Graph.ClearSelection();

            Graph.Rows[index].Selected = true;
            Graph.CurrentCell = Graph.Rows[index].Cells[1];

            Graph.Select();
        }

        // Selects row containing revision given its revisionId
        // Returns whether the required revision was found and selected
        private bool InternalSetSelectedRevision(string revision)
        {
            var index = FindRevisionIndex(revision);

            if (index >= 0 && index < Graph.RowCount)
            {
                SetSelectedIndex(index);
                return true;
            }
            else
            {
                Graph.ClearSelection();
                Graph.Select();
                return false;
            }
        }

        /// <summary>
        /// Find specified revision in known to the grid revisions
        /// </summary>
        /// <param name="revision">Revision to lookup</param>
        /// <returns>Index of the found revision or -1 if nothing was found</returns>
        private int FindRevisionIndex(string revision)
        {
            return Graph.TryGetRevisionIndex(revision) ?? -1;
        }

        public bool SetSelectedRevision(string revision)
        {
            var found = InternalSetSelectedRevision(revision);
            if (found)
            {
                _navigationHistory.Push(revision);
            }

            return found;
        }

        [CanBeNull]
        public GitRevision GetRevision(string guid)
        {
            return Graph.GetRevision(guid);
        }

        public bool SetSelectedRevision([CanBeNull] GitRevision revision)
        {
            return SetSelectedRevision(revision?.Guid);
        }

        private void HighlightBranch(string id)
        {
            RevisionGraphDrawStyle = RevisionGraphDrawStyleEnum.HighlightSelected;
            Graph.HighlightBranch(id);
        }

        public string DescribeRevision(GitRevision revision, int maxLength = 0)
        {
            var gitRefListsForRevision = new GitRefListsForRevision(revision);

            var descriptiveRef = gitRefListsForRevision.AllBranches
                .Concat(gitRefListsForRevision.AllTags)
                .FirstOrDefault();

            var description = descriptiveRef != null
                ? GetRefUnambiguousName(descriptiveRef)
                : revision.Subject;

            if (!revision.IsArtificial)
            {
                description += " @" + revision.Guid.Substring(0, 4);
            }

            if (maxLength > 0)
            {
                description = description.ShortenTo(maxLength);
            }

            return description;
        }

        public IReadOnlyList<GitRevision> GetSelectedRevisions(SortDirection? direction = null)
        {
            var rows = Graph
                .SelectedRows
                .Cast<DataGridViewRow>()
                .Where(row => Graph.RowCount > row.Index);

            if (direction.HasValue)
            {
                var d = direction.Value == SortDirection.Ascending ? 1 : -1;
                rows = rows.OrderBy(row => row.Index, (r1, r2) => d * (r1 - r2));
            }

            return rows
                .Select(row => GetRevision(row.Index))
                .ToList();
        }

        public bool IsFirstParentValid()
        {
            var revisions = GetSelectedRevisions();

            // Parents to First (A) are only known if A is explicitly selected (there is no explicit search for parents to parents of a single selected revision)
            return revisions != null && revisions.Count > 1;
        }

        public IReadOnlyList<string> GetRevisionChildren(string revision)
        {
            return Graph.GetRevisionChildren(revision);
        }

        private bool IsValidRevisionIndex(int index)
        {
            return index >= 0 && index < Graph.RowCount;
        }

        [CanBeNull]
        private GitRevision GetRevision(int row)
        {
            return Graph.GetRevision(row);
        }

        public GitRevision GetCurrentRevision()
        {
            return Module.GetRevision(CurrentCheckout, shortFormat: true, loadRefs: true);
        }

        public void RefreshRevisions()
        {
            if (IndexWatcher.IndexChanged)
            {
                ForceRefreshRevisions();
            }
        }

        public void ReloadHotkeys()
        {
            Hotkeys = HotkeySettingsManager.LoadHotkeys(HotkeySettingsName);
            MenuCommands.CreateOrUpdateMenuCommands();
        }

        public void ReloadTranslation()
        {
            Translate();
        }

        internal static bool ShowRemoteRef(IGitRef r)
        {
            if (r.IsTag)
            {
                return AppSettings.ShowSuperprojectTags;
            }

            if (r.IsHead)
            {
                return AppSettings.ShowSuperprojectBranches;
            }

            if (r.IsRemote)
            {
                return AppSettings.ShowSuperprojectRemoteBranches;
            }

            return false;
        }

        public void ForceRefreshRevisions()
        {
            ThreadHelper.AssertOnUIThread();

            try
            {
                RevisionGraphDrawStyle = RevisionGraphDrawStyleEnum.DrawNonRelativesGray;

                // Apply filter from revision filter dialog
                _branchFilter = _revisionFilter.GetBranchFilter();
                SetShowBranches();

                _initialLoad = true;

                _buildServerWatcher.CancelBuildStatusFetchOperation();

                DisposeRevisionReader();

                var newCurrentCheckout = Module.GetCurrentCheckout();
                GitModule capturedModule = Module;
                JoinableTask<SuperProjectInfo> newSuperProjectInfo =
                    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        await TaskScheduler.Default.SwitchTo(alwaysYield: true);
                        return GetSuperprojectCheckout(ShowRemoteRef, capturedModule);
                    });
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    try
                    {
                        await newSuperProjectInfo;
                    }
                    finally
                    {
                        await this.SwitchToMainThreadAsync();
                        Refresh();
                    }
                }).FileAndForget();

                // If the current checkout changed, don't get the currently selected rows, select the
                // new current checkout instead.
                if (newCurrentCheckout == CurrentCheckout)
                {
                    _lastSelectedRows = Graph.SelectedIds;
                }
                else
                {
                    // This is a new checkout, so ensure the variable is cleared out.
                    _lastSelectedRows = null;
                }

                Graph.ClearSelection();
                CurrentCheckout = newCurrentCheckout;
                _filteredCurrentCheckout = null;
                _currentCheckoutParents = null;
                _superprojectCurrentCheckout = newSuperProjectInfo;
                Graph.Clear();
                SetPage(_loadingImage);
                _isLoading = true;
                _isRefreshingRevisions = true;
                base.Refresh();

                IndexWatcher.Reset();

                if (!AppSettings.ShowGitNotes && _refFilterOptions.HasFlag(RefFilterOptions.All | RefFilterOptions.Boundary))
                {
                    _refFilterOptions |= RefFilterOptions.ShowGitNotes;
                }

                if (AppSettings.ShowGitNotes)
                {
                    _refFilterOptions &= ~RefFilterOptions.ShowGitNotes;
                }

                if (!AppSettings.ShowMergeCommits)
                {
                    _refFilterOptions |= RefFilterOptions.NoMerges;
                }

                if (AppSettings.ShowFirstParent)
                {
                    _refFilterOptions |= RefFilterOptions.FirstParent;
                }

                if (AppSettings.ShowSimplifyByDecoration)
                {
                    _refFilterOptions |= RefFilterOptions.SimplifyByDecoration;
                }

                var formFilter = RevisionGridInMemFilter.CreateIfNeeded(
                    _revisionFilter.GetInMemAuthorFilter(),
                    _revisionFilter.GetInMemCommitterFilter(),
                    _revisionFilter.GetInMemMessageFilter(),
                    _revisionFilter.GetIgnoreCase());

                var toolStripFilter = RevisionGridInMemFilter.CreateIfNeeded(
                    InMemAuthorFilter,
                    InMemCommitterFilter,
                    InMemMessageFilter,
                    InMemFilterIgnoreCase);

                Func<GitRevision, bool> predicate;
                if (formFilter != null && toolStripFilter != null)
                {
                    // either or
                    predicate = r => formFilter.Predicate(r) || toolStripFilter.Predicate(r);
                }
                else if (formFilter != null)
                {
                    predicate = formFilter.Predicate;
                }
                else if (toolStripFilter != null)
                {
                    predicate = toolStripFilter.Predicate;
                }
                else
                {
                    predicate = null;
                }

                var revisions = new Subject<GitRevision>();
                _revisionSubscription?.Dispose();
                _revisionSubscription = revisions
                    .ObserveOn(ThreadPoolScheduler.Instance)
                    .Subscribe(OnRevisionRead, OnRevisionReaderError, OnRevisionReadCompleted);

                if (_revisionReader == null)
                {
                    _revisionReader = new RevisionReader();
                }

                _revisionReader.Execute(
                    Module,
                    revisions,
                    _refFilterOptions,
                    _branchFilter,
                    _revisionFilter.GetRevisionFilter() + QuickRevisionFilter + _fixedRevisionFilter,
                    _revisionFilter.GetPathFilter() + _fixedPathFilter,
                    predicate);

                LoadRevisions();
                Graph.Refresh();
                ResetNavigationHistory();
            }
            catch (Exception)
            {
                SetPage(new ErrorControl());
                throw;
            }

            return;

            void OnRevisionReaderError(Exception exception)
            {
                // This has to happen on the UI thread
                this.InvokeAsync(() => SetPage(new ErrorControl()))
                    .FileAndForget();

                DisposeRevisionReader();

                // Rethrow the exception on the UI thread
                this.InvokeAsync(() => throw new AggregateException(exception))
                    .FileAndForget();
            }

            void OnRevisionReadCompleted()
            {
                _isLoading = false;

                if (_revisionReader.RevisionCount == 0 && !FilterIsApplied(true))
                {
                    var isBare = Module.IsBareRepository();

                    // This has to happen on the UI thread
                    this.InvokeAsync(
                            () =>
                            {
                                SetPage(isBare ? (Control)new BareRepoControl() : new EmptyRepoControl());
                                _isRefreshingRevisions = false;
                            })
                        .FileAndForget();
                }
                else
                {
                    // This has to happen on the UI thread
                    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        await this.SwitchToMainThreadAsync();
                        OnRevisionRead();
                        SetPage(Graph);
                        _isRefreshingRevisions = false;
                        SelectInitialRevision();
                        if (ShowBuildServerInfo)
                        {
                            await _buildServerWatcher.LaunchBuildServerInfoFetchOperationAsync();
                        }
                    }).FileAndForget();
                }

                DisposeRevisionReader();
            }

            void DisposeRevisionReader()
            {
                if (_revisionReader != null)
                {
                    LatestRefs = _revisionReader.LatestRefs;

                    _revisionReader.Dispose();
                    _revisionReader = null;
                }
            }
        }

        [CanBeNull]
        private static SuperProjectInfo GetSuperprojectCheckout(Func<IGitRef, bool> showRemoteRef, GitModule gitModule)
        {
            if (gitModule.SuperprojectModule == null)
            {
                return null;
            }

            var spi = new SuperProjectInfo();
            var (code, commit) = gitModule.GetSuperprojectCurrentCheckout();
            if (code == 'U')
            {
                // return local and remote hashes
                var array = gitModule.SuperprojectModule.GetConflict(gitModule.SubmodulePath);
                spi.Conflict_Base = array.Base.Hash;
                spi.Conflict_Local = array.Local.Hash;
                spi.Conflict_Remote = array.Remote.Hash;
            }
            else
            {
                spi.CurrentBranch = commit;
            }

            var refs = gitModule.SuperprojectModule.GetSubmoduleItemsForEachRef(gitModule.SubmodulePath, showRemoteRef);

            if (refs != null)
            {
                spi.Refs = refs.Where(a => a.Value != null).GroupBy(a => a.Value.Guid).ToDictionary(gr => gr.Key, gr => gr.Select(a => a.Key).ToList());
            }

            return spi;
        }

        internal bool FilterIsApplied(bool inclBranchFilter)
        {
            return (inclBranchFilter && !string.IsNullOrEmpty(_branchFilter)) ||
                   !(string.IsNullOrEmpty(QuickRevisionFilter) &&
                     !_revisionFilter.FilterEnabled() &&
                     string.IsNullOrEmpty(InMemAuthorFilter) &&
                     string.IsNullOrEmpty(InMemCommitterFilter) &&
                     string.IsNullOrEmpty(InMemMessageFilter));
        }

        internal bool ShouldHideGraph(bool inclBranchFilter)
        {
            return (inclBranchFilter && !string.IsNullOrEmpty(_branchFilter)) ||
                   !(!_revisionFilter.ShouldHideGraph() &&
                     string.IsNullOrEmpty(InMemAuthorFilter) &&
                     string.IsNullOrEmpty(InMemCommitterFilter) &&
                     string.IsNullOrEmpty(InMemMessageFilter));
        }

        private void SelectInitialRevision()
        {
            string filteredCurrentCheckout = _filteredCurrentCheckout;
            string[] lastSelectedRows = _lastSelectedRows ?? Array.Empty<string>();

            // filter out all unavailable commits from LastSelectedRows.
            lastSelectedRows = lastSelectedRows.Where(revision => FindRevisionIndex(revision) >= 0).ToArray();

            if (lastSelectedRows.Any())
            {
                Graph.SelectedIds = lastSelectedRows;
                _lastSelectedRows = null;
            }
            else
            {
                if (!string.IsNullOrEmpty(_initialSelectedRevision))
                {
                    int index = SearchRevision(_initialSelectedRevision);
                    if (index >= 0)
                    {
                        SetSelectedIndex(index);
                    }
                }
                else
                {
                    SetSelectedRevision(filteredCurrentCheckout);
                }
            }

            if (string.IsNullOrEmpty(filteredCurrentCheckout))
            {
                return;
            }

            if (!Graph.IsRevisionRelative(filteredCurrentCheckout))
            {
                HighlightBranch(filteredCurrentCheckout);
            }
        }

        private string[] GetAllParents(string initRevision)
        {
            var args = new ArgumentBuilder
            {
                "rev-list",
                { AppSettings.OrderRevisionByDate, "--date-order", "--topo-order" },
                { AppSettings.MaxRevisionGraphCommits > 0, $"--max-count=\"{AppSettings.MaxRevisionGraphCommits}\" " },
                initRevision
            };

            return Module.ReadGitOutputLines(args.ToString()).ToArray();
        }

        private int SearchRevision(string initRevision)
        {
            var exactIndex = Graph.TryGetRevisionIndex(initRevision);
            if (exactIndex.HasValue)
            {
                return exactIndex.Value;
            }

            foreach (var parentHash in GetAllParents(initRevision))
            {
                var parentIndex = Graph.TryGetRevisionIndex(parentHash);
                if (parentIndex.HasValue)
                {
                    return parentIndex.Value;
                }
            }

            return -1;
        }

        private void LoadRevisions()
        {
            if (_revisionReader == null)
            {
                return;
            }

            Graph.SuspendLayout();

            Graph.SelectionChanged -= OnGraphSelectionChanged;
            Graph.Enabled = true;
            Graph.Focus();
            Graph.SelectionChanged += OnGraphSelectionChanged;

            Graph.ResumeLayout();

            if (!_initialLoad)
            {
                return;
            }

            _initialLoad = false;

            SelectionTimer.Enabled = false;
            SelectionTimer.Stop();
            SelectionTimer.Enabled = true;
            SelectionTimer.Start();
        }

        #region Graph event handlers

        private void OnGraphSelectionChanged(object sender, EventArgs e)
        {
            _parentChildNavigationHistory.RevisionsSelectionChanged();

            if (Graph.SelectedRows.Count > 0)
            {
                _latestSelectedRowIndex = Graph.SelectedRows[0].Index;

                // if there was selected a new revision while data is being loaded
                // then don't change the new selection when restoring selected revisions after data is loaded
                if (_isRefreshingRevisions && !Graph.UpdatingVisibleRows)
                {
                    _lastSelectedRows = Graph.SelectedIds;
                }
            }

            SelectionTimer.Enabled = false;
            SelectionTimer.Stop();
            SelectionTimer.Enabled = true;
            SelectionTimer.Start();

            var selectedRevisions = GetSelectedRevisions();
            var firstSelectedRevision = selectedRevisions.FirstOrDefault();
            if (selectedRevisions.Count == 1 && firstSelectedRevision != null)
            {
                _navigationHistory.Push(firstSelectedRevision.Guid);
            }

            if (Parent != null &&
                !Graph.UpdatingVisibleRows &&
                _authorHighlighting.ProcessRevisionSelectionChange(Module, selectedRevisions))
            {
                Refresh();
            }
        }

        private void OnGraphKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.BrowserBack:
                case Keys.Left when e.Modifiers.HasFlag(Keys.Alt):
                {
                    NavigateBackward();
                    break;
                }

                case Keys.BrowserForward:
                case Keys.Right when e.Modifiers.HasFlag(Keys.Alt):
                {
                    NavigateForward();
                    break;
                }
            }
        }

        private void OnGraphKeyUp(object sender, KeyEventArgs e)
        {
            var selectedRevision = LatestSelectedRevision;

            if (selectedRevision == null)
            {
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.F2:
                {
                    InitiateRefAction(
                        new GitRefListsForRevision(selectedRevision).GetRenameableLocalBranches(),
                        gitRef => UICommands.StartRenameDialog(this, gitRef.Name),
                        FormQuickGitRefSelector.Action.Rename);
                    break;
                }

                case Keys.Delete:
                {
                    InitiateRefAction(
                        new GitRefListsForRevision(selectedRevision).GetDeletableLocalRefs(Module.GetSelectedBranch()),
                        gitRef =>
                        {
                            if (gitRef.IsTag)
                            {
                                UICommands.StartDeleteTagDialog(this, gitRef.Name);
                            }
                            else
                            {
                                UICommands.StartDeleteBranchDialog(this, gitRef.Name);
                            }
                        },
                        FormQuickGitRefSelector.Action.Delete);
                    break;
                }
            }
        }

        private void OnGraphMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.XButton1)
            {
                NavigateBackward();
            }
            else if (e.Button == MouseButtons.XButton2)
            {
                NavigateForward();
            }
        }

        internal bool TryGetSuperProjectInfo(out SuperProjectInfo spi)
        {
            if (_superprojectCurrentCheckout.Task.IsCompleted)
            {
                spi = default;
                return false;
            }

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            spi = _superprojectCurrentCheckout.Task.Result;
#pragma warning restore VSTHRD002
            return true;
        }

        private void OnGraphDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            DoubleClickRevision?.Invoke(this, new DoubleClickRevisionEventArgs(GetSelectedRevisions().FirstOrDefault()));

            if (!DoubleClickDoesNotOpenCommitInfo)
            {
                ViewSelectedRevisions();
            }
        }

        private void OnGraphMouseClick(object sender, MouseEventArgs e)
        {
            var pt = Graph.PointToClient(Cursor.Position);
            var hti = Graph.HitTest(pt.X, pt.Y);
            _latestSelectedRowIndex = hti.RowIndex;
        }

        private void OnGraphCellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            var pt = Graph.PointToClient(Cursor.Position);
            var hti = Graph.HitTest(pt.X, pt.Y);

            if (_latestSelectedRowIndex == hti.RowIndex)
            {
                return;
            }

            _latestSelectedRowIndex = hti.RowIndex;
            Graph.ClearSelection();

            if (IsValidRevisionIndex(_latestSelectedRowIndex))
            {
                Graph.Rows[_latestSelectedRowIndex].Selected = true;
            }
        }

        #endregion

        public void ViewSelectedRevisions()
        {
            var selectedRevisions = GetSelectedRevisions();
            if (selectedRevisions.Any(rev => !rev.IsArtificial))
            {
                Form ProvideForm()
                {
                    return new FormCommitDiff(UICommands, selectedRevisions[0].Guid);
                }

                UICommands.ShowModelessForm(this, false, null, null, ProvideForm);
            }
            else if (!selectedRevisions.Any())
            {
                UICommands.StartCompareRevisionsDialog(this);
            }
        }

        private void SelectionTimerTick(object sender, EventArgs e)
        {
            SelectionTimer.Enabled = false;
            SelectionTimer.Stop();
            SelectionChanged?.Invoke(this, e);
        }

        private void CreateTagToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (LatestSelectedRevision == null)
            {
                return;
            }

            UICommands.DoActionOnRepo(() =>
                {
                    using (var frm = new FormCreateTag(UICommands, LatestSelectedRevision))
                    {
                        return frm.ShowDialog(this) == DialogResult.OK;
                    }
                });
        }

        private void ResetCurrentBranchToHereToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (LatestSelectedRevision == null)
            {
                return;
            }

            var frm = new FormResetCurrentBranch(UICommands, LatestSelectedRevision);
            frm.ShowDialog(this);
        }

        private void CreateNewBranchToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (LatestSelectedRevision == null)
            {
                return;
            }

            UICommands.DoActionOnRepo(() =>
                {
                    var frm = new FormCreateBranch(UICommands, LatestSelectedRevision);

                    return frm.ShowDialog(this) == DialogResult.OK;
                });
        }

        internal void ShowCurrentBranchOnly()
        {
            if (IsShowCurrentBranchOnlyChecked)
            {
                return;
            }

            AppSettings.BranchFilterEnabled = true;
            AppSettings.ShowCurrentBranchOnly = true;

            SetShowBranches();
            ForceRefreshRevisions();
        }

        internal void ShowAllBranches()
        {
            if (IsShowAllBranchesChecked)
            {
                return;
            }

            AppSettings.BranchFilterEnabled = false;

            SetShowBranches();
            ForceRefreshRevisions();
        }

        internal void ShowFilteredBranches()
        {
            if (IsShowFilteredBranchesChecked)
            {
                return;
            }

            AppSettings.BranchFilterEnabled = true;
            AppSettings.ShowCurrentBranchOnly = false;

            SetShowBranches();
            ForceRefreshRevisions();
        }

        private void SetShowBranches()
        {
            IsShowAllBranchesChecked = !AppSettings.BranchFilterEnabled;
            IsShowCurrentBranchOnlyChecked = AppSettings.BranchFilterEnabled && AppSettings.ShowCurrentBranchOnly;
            IsShowFilteredBranchesChecked = AppSettings.BranchFilterEnabled && !AppSettings.ShowCurrentBranchOnly;

            _branchFilter = _revisionFilter.GetBranchFilter();

            if (!AppSettings.BranchFilterEnabled)
            {
                _refFilterOptions = RefFilterOptions.All | RefFilterOptions.Boundary;
            }
            else if (AppSettings.ShowCurrentBranchOnly)
            {
                _refFilterOptions = 0;
            }
            else
            {
                _refFilterOptions = _branchFilter.Length > 0
                    ? 0
                    : RefFilterOptions.All | RefFilterOptions.Boundary;
            }

            // Apply checkboxes changes also to FormBrowse main menu
            MenuCommands.TriggerMenuChanged();
        }

        internal void ShowRevisionFilterDialog()
        {
            _revisionFilter.ShowDialog(this);
            ForceRefreshRevisions();
        }

        private void ContextMenuOpening(object sender, CancelEventArgs e)
        {
            if (LatestSelectedRevision == null)
            {
                return;
            }

            var inTheMiddleOfBisect = Module.InTheMiddleOfBisect();
            markRevisionAsBadToolStripMenuItem.Visible = inTheMiddleOfBisect;
            markRevisionAsGoodToolStripMenuItem.Visible = inTheMiddleOfBisect;
            bisectSkipRevisionToolStripMenuItem.Visible = inTheMiddleOfBisect;
            stopBisectToolStripMenuItem.Visible = inTheMiddleOfBisect;
            bisectSeparator.Visible = inTheMiddleOfBisect;
            compareWithCurrentBranchToolStripMenuItem.Visible = Module.GetSelectedBranch().IsNotNullOrWhitespace();

            var deleteTagDropDown = new ContextMenuStrip();
            var deleteBranchDropDown = new ContextMenuStrip();
            var checkoutBranchDropDown = new ContextMenuStrip();
            var mergeBranchDropDown = new ContextMenuStrip();
            var renameDropDown = new ContextMenuStrip();

            var revision = LatestSelectedRevision;
            var gitRefListsForRevision = new GitRefListsForRevision(revision);
            _rebaseOnTopOf = null;
            foreach (var head in gitRefListsForRevision.AllTags)
            {
                var deleteItem = new ToolStripMenuItem(head.Name);
                deleteItem.Click += delegate { UICommands.StartDeleteTagDialog(this, head.Name); };
                deleteTagDropDown.Items.Add(deleteItem);

                var mergeItem = new ToolStripMenuItem(head.Name) { Tag = GetRefUnambiguousName(head) };
                mergeItem.Click += delegate { UICommands.StartMergeBranchDialog(this, GetRefUnambiguousName(head)); };
                mergeBranchDropDown.Items.Add(mergeItem);
            }

            // For now there is no action that could be done on currentBranch
            string currentBranchRef = GitRefName.RefsHeadsPrefix + Module.GetSelectedBranch();
            var branchesWithNoIdenticalRemotes = gitRefListsForRevision.BranchesWithNoIdenticalRemotes;

            bool currentBranchPointsToRevision = false;
            foreach (var head in branchesWithNoIdenticalRemotes)
            {
                if (head.CompleteName == currentBranchRef)
                {
                    currentBranchPointsToRevision = !revision.IsArtificial;
                }
                else
                {
                    var toolStripItem = new ToolStripMenuItem(head.Name);
                    toolStripItem.Click += delegate { UICommands.StartMergeBranchDialog(this, GetRefUnambiguousName(head)); };
                    mergeBranchDropDown.Items.Add(toolStripItem);

                    if (_rebaseOnTopOf == null)
                    {
                        _rebaseOnTopOf = (string)toolStripItem.Tag;
                    }
                }
            }

            // if there is no branch to rebase on, then allow user to rebase on selected commit
            if (_rebaseOnTopOf == null && !currentBranchPointsToRevision)
            {
                _rebaseOnTopOf = revision.Guid;
            }

            // if there is no branch to merge, then let user to merge selected commit into current branch
            if (mergeBranchDropDown.Items.Count == 0 && !currentBranchPointsToRevision)
            {
                var toolStripItem = new ToolStripMenuItem(revision.Guid);
                toolStripItem.Click += delegate { UICommands.StartMergeBranchDialog(this, revision.Guid); };
                mergeBranchDropDown.Items.Add(toolStripItem);
                if (_rebaseOnTopOf == null)
                {
                    _rebaseOnTopOf = toolStripItem.Tag as string;
                }
            }

            var allBranches = gitRefListsForRevision.AllBranches;
            foreach (var head in allBranches)
            {
                // skip remote branches - they can not be deleted this way
                if (!head.IsRemote)
                {
                    if (head.CompleteName != currentBranchRef)
                    {
                        var deleteBranchMenuItem = new ToolStripMenuItem(head.Name);
                        deleteBranchMenuItem.Click += delegate { UICommands.StartDeleteBranchDialog(this, head.Name); };
                        deleteBranchDropDown.Items.Add(deleteBranchMenuItem);
                    }

                    var renameBranchMenuItem = new ToolStripMenuItem(head.Name);
                    renameBranchMenuItem.Click += delegate { UICommands.StartRenameDialog(this, head.Name); };
                    renameDropDown.Items.Add(renameBranchMenuItem);
                }

                if (head.CompleteName != currentBranchRef)
                {
                    var checkoutBranchMenuItem = new ToolStripMenuItem(head.Name);
                    checkoutBranchMenuItem.Click += delegate
                    {
                        if (head.IsRemote)
                        {
                            UICommands.StartCheckoutRemoteBranch(this, head.Name);
                        }
                        else
                        {
                            UICommands.StartCheckoutBranch(this, head.Name);
                        }
                    };
                    checkoutBranchDropDown.Items.Add(checkoutBranchMenuItem);
                }
            }

            bool firstRemoteBranchForDelete = true;
            foreach (var head in allBranches)
            {
                if (head.IsRemote)
                {
                    if (firstRemoteBranchForDelete)
                    {
                        firstRemoteBranchForDelete = false;
                        if (deleteBranchDropDown.Items.Count > 0)
                        {
                            deleteBranchDropDown.Items.Add(new ToolStripSeparator());
                        }
                    }

                    var toolStripItem = new ToolStripMenuItem(head.Name);
                    toolStripItem.Click += delegate { UICommands.StartDeleteRemoteBranchDialog(this, head.Name); };
                    deleteBranchDropDown.Items.Add(toolStripItem);
                }
            }

            bool bareRepositoryOrArtificial = Module.IsBareRepository() || revision.IsArtificial;
            deleteTagToolStripMenuItem.DropDown = deleteTagDropDown;
            deleteTagToolStripMenuItem.Enabled = deleteTagDropDown.Items.Count > 0;

            deleteBranchToolStripMenuItem.DropDown = deleteBranchDropDown;
            deleteBranchToolStripMenuItem.Enabled = deleteBranchDropDown.Items.Count > 0 && !Module.IsBareRepository();

            checkoutBranchToolStripMenuItem.DropDown = checkoutBranchDropDown;
            checkoutBranchToolStripMenuItem.Enabled = !bareRepositoryOrArtificial && checkoutBranchDropDown.Items.Count > 0 && !Module.IsBareRepository();

            mergeBranchToolStripMenuItem.DropDown = mergeBranchDropDown;
            mergeBranchToolStripMenuItem.Enabled = !bareRepositoryOrArtificial && mergeBranchDropDown.Items.Count > 0 && !Module.IsBareRepository();

            rebaseOnToolStripMenuItem.Enabled = !bareRepositoryOrArtificial && !Module.IsBareRepository();

            renameBranchToolStripMenuItem.DropDown = renameDropDown;
            renameBranchToolStripMenuItem.Enabled = renameDropDown.Items.Count > 0;

            checkoutRevisionToolStripMenuItem.Enabled = !bareRepositoryOrArtificial;
            revertCommitToolStripMenuItem.Enabled = !bareRepositoryOrArtificial;
            cherryPickCommitToolStripMenuItem.Enabled = !bareRepositoryOrArtificial;
            manipulateCommitToolStripMenuItem.Enabled = !bareRepositoryOrArtificial;

            copyToClipboardToolStripMenuItem.Enabled = !revision.IsArtificial;
            createNewBranchToolStripMenuItem.Enabled = !bareRepositoryOrArtificial;
            resetCurrentBranchToHereToolStripMenuItem.Enabled = !bareRepositoryOrArtificial;
            archiveRevisionToolStripMenuItem.Enabled = !revision.IsArtificial;
            createTagToolStripMenuItem.Enabled = !revision.IsArtificial;

            openBuildReportToolStripMenuItem.Visible = !string.IsNullOrWhiteSpace(revision.BuildStatus?.Url);

            RefreshOwnScripts();
        }

        private ISet<string> _ambiguousRefs;
        private ISet<string> AmbiguousRefs
        {
            get
            {
                if (_ambiguousRefs == null)
                {
                    _ambiguousRefs = GitRef.GetAmbiguousRefNames(LatestRefs);
                }

                return _ambiguousRefs;
            }
            set
            {
                _ambiguousRefs = value;
            }
        }

        private string GetRefUnambiguousName(IGitRef gitRef)
        {
            if (AmbiguousRefs.Contains(gitRef.Name))
            {
                return gitRef.CompleteName;
            }

            return gitRef.Name;
        }

        private void ToolStripItemClickRebaseBranch(object sender, EventArgs e)
        {
            if (_rebaseOnTopOf != null)
            {
                UICommands.StartRebase(this, _rebaseOnTopOf);
            }
        }

        private void OnRebaseInteractivelyClicked(object sender, EventArgs e)
        {
            if (_rebaseOnTopOf != null)
            {
                UICommands.StartInteractiveRebase(this, _rebaseOnTopOf);
            }
        }

        private void OnRebaseWithAdvOptionsClicked(object sender, EventArgs e)
        {
            if (_rebaseOnTopOf != null)
            {
                UICommands.StartRebaseDialogWithAdvOptions(this, _rebaseOnTopOf);
            }
        }

        private void CheckoutRevisionToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (LatestSelectedRevision != null)
            {
                UICommands.StartCheckoutRevisionDialog(this, LatestSelectedRevision.Guid);
            }
        }

        private void ArchiveRevisionToolStripMenuItemClick(object sender, EventArgs e)
        {
            var selectedRevisions = GetSelectedRevisions();
            if (selectedRevisions.Count > 2)
            {
                MessageBox.Show(this, "Select only one or two revisions. Abort.", "Archive revision");
                return;
            }

            GitRevision mainRevision = selectedRevisions.First();
            GitRevision diffRevision = null;
            if (selectedRevisions.Count == 2)
            {
                diffRevision = selectedRevisions.Last();
            }

            UICommands.StartArchiveDialog(this, mainRevision, diffRevision);
        }

        internal void ToggleShowAuthorDate()
        {
            AppSettings.ShowAuthorDate = !AppSettings.ShowAuthorDate;
            Refresh();
        }

        internal void ToggleOrderRevisionByDate()
        {
            AppSettings.OrderRevisionByDate = !AppSettings.OrderRevisionByDate;
            ForceRefreshRevisions();
        }

        internal void ToggleShowRemoteBranches()
        {
            AppSettings.ShowRemoteBranches = !AppSettings.ShowRemoteBranches;
            Graph.Invalidate();
        }

        internal void ToggleShowReflogReferences()
        {
            AppSettings.ShowReflogReferences = !AppSettings.ShowReflogReferences;
            ForceRefreshRevisions();
        }

        internal void ToggleShowSuperprojectTags()
        {
            AppSettings.ShowSuperprojectTags = !AppSettings.ShowSuperprojectTags;
            ForceRefreshRevisions();
        }

        internal void ShowSuperprojectBranches_ToolStripMenuItemClick()
        {
            AppSettings.ShowSuperprojectBranches = !AppSettings.ShowSuperprojectBranches;
            ForceRefreshRevisions();
        }

        internal void ShowSuperprojectRemoteBranches_ToolStripMenuItemClick()
        {
            AppSettings.ShowSuperprojectRemoteBranches = !AppSettings.ShowSuperprojectRemoteBranches;
            ForceRefreshRevisions();
        }

        private void RevertCommitToolStripMenuItemClick(object sender, EventArgs e)
        {
            var revisions = GetSelectedRevisions(SortDirection.Ascending);
            foreach (var rev in revisions)
            {
                UICommands.StartRevertCommitDialog(this, rev);
            }
        }

        private void CherryPickCommitToolStripMenuItemClick(object sender, EventArgs e)
        {
            var revisions = GetSelectedRevisions(SortDirection.Descending);
            UICommands.StartCherryPickDialog(this, revisions);
        }

        private void FixupCommitToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (LatestSelectedRevision == null)
            {
                return;
            }

            UICommands.StartFixupCommitDialog(this, LatestSelectedRevision);
        }

        private void SquashCommitToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (LatestSelectedRevision == null)
            {
                return;
            }

            UICommands.StartSquashCommitDialog(this, LatestSelectedRevision);
        }

        internal void ToggleShowRelativeDate(EventArgs e)
        {
            AppSettings.RelativeDate = !AppSettings.RelativeDate;
            Refresh();
        }

        private bool ShowUncommittedChanges()
        {
            return ShowUncommittedChangesIfPossible && AppSettings.RevisionGraphShowWorkingDirChanges;
        }

        private void OnRevisionRead([CanBeNull] GitRevision rev = null)
        {
            if (rev == null)
            {
                // Prune the graph and make sure the row count matches reality
                Graph.Prune();
                return;
            }

            if (_filteredCurrentCheckout == null)
            {
                if (rev.Guid == CurrentCheckout)
                {
                    _filteredCurrentCheckout = CurrentCheckout;
                }
                else
                {
                    if (_currentCheckoutParents == null)
                    {
                        _currentCheckoutParents = GetAllParents(CurrentCheckout);
                    }

                    _filteredCurrentCheckout = _currentCheckoutParents.FirstOrDefault(parent => parent == rev.Guid);
                }
            }

            if (_filteredCurrentCheckout == rev.Guid && ShowUncommittedChanges() && !Module.IsBareRepository())
            {
                CheckUncommittedChanged(_filteredCurrentCheckout);
            }

            DvcsGraph.DataTypes dataTypes;
            if (rev.Guid == _filteredCurrentCheckout)
            {
                dataTypes = DvcsGraph.DataTypes.Active;
            }
            else if (rev.Refs.Count != 0)
            {
                dataTypes = DvcsGraph.DataTypes.Special;
            }
            else
            {
                dataTypes = DvcsGraph.DataTypes.Normal;
            }

            Graph.Add(rev, dataTypes);
        }

        public void UpdateArtificialCommitCount(IReadOnlyList<GitItemStatus> status)
        {
            GitRevision unstagedRev = GetRevision(GitRevision.UnstagedGuid);
            GitRevision stagedRev = GetRevision(GitRevision.IndexGuid);
            UpdateArtificialCommitCount(status, unstagedRev, stagedRev);
        }

        private void UpdateArtificialCommitCount([CanBeNull] IReadOnlyList<GitItemStatus> status, GitRevision unstagedRev, GitRevision stagedRev)
        {
            if (status == null)
            {
                return;
            }

            int staged = status.Count(item => item.IsStaged);
            int unstaged = status.Count - staged;

            if (unstagedRev != null)
            {
                unstagedRev.SubjectCount = Strings.GetUnstagedCountText(unstaged);
            }

            if (stagedRev != null)
            {
                stagedRev.SubjectCount = Strings.GetStagedCountText(staged);
            }

            // cache the status, if commits do not exist or for a refresh
            _artificialStatus = status;

            Graph.Invalidate();
        }

        private void CheckUncommittedChanged(string filteredCurrentCheckout)
        {
            var userName = Module.GetEffectiveSetting(SettingKeyString.UserName);
            var userEmail = Module.GetEffectiveSetting(SettingKeyString.UserEmail);

            // Add working directory as virtual commit
            var unstagedRev = new GitRevision(GitRevision.UnstagedGuid)
            {
                Author = userName,
                AuthorDate = DateTime.MaxValue,
                AuthorEmail = userEmail,
                Committer = userName,
                CommitDate = DateTime.MaxValue,
                CommitterEmail = userEmail,
                Subject = Strings.GetWorkspaceText(),
                ParentGuids = new[] { GitRevision.IndexGuid }
            };
            Graph.Add(unstagedRev, DvcsGraph.DataTypes.Normal);

            // Add index as virtual commit
            var stagedRev = new GitRevision(GitRevision.IndexGuid)
            {
                Author = userName,
                AuthorDate = DateTime.MaxValue,
                AuthorEmail = userEmail,
                Committer = userName,
                CommitDate = DateTime.MaxValue,
                CommitterEmail = userEmail,
                Subject = Strings.GetIndexText(),
                ParentGuids = new[] { filteredCurrentCheckout }
            };
            Graph.Add(stagedRev, DvcsGraph.DataTypes.Normal);

            UpdateArtificialCommitCount(_artificialStatus, unstagedRev, stagedRev);
        }

        internal void ToggleDrawNonRelativesGray()
        {
            AppSettings.RevisionGraphDrawNonRelativesGray = !AppSettings.RevisionGraphDrawNonRelativesGray;
            MenuCommands.TriggerMenuChanged();
            Graph.Refresh();
        }

        #region Bisect

        private void MarkRevisionAsBadToolStripMenuItemClick(object sender, EventArgs e)
        {
            ContinueBisect(GitBisectOption.Bad);
        }

        private void MarkRevisionAsGoodToolStripMenuItemClick(object sender, EventArgs e)
        {
            ContinueBisect(GitBisectOption.Good);
        }

        private void BisectSkipRevisionToolStripMenuItemClick(object sender, EventArgs e)
        {
            ContinueBisect(GitBisectOption.Skip);
        }

        private void ContinueBisect(GitBisectOption bisectOption)
        {
            if (LatestSelectedRevision == null)
            {
                return;
            }

            FormProcess.ShowDialog(this, Module, GitCommandHelpers.ContinueBisectCmd(bisectOption, LatestSelectedRevision.Guid), false);
            RefreshRevisions();
        }

        private void StopBisectToolStripMenuItemClick(object sender, EventArgs e)
        {
            FormProcess.ShowDialog(this, Module, GitCommandHelpers.StopBisectCmd());
            RefreshRevisions();
        }

        #endregion

        private void RefreshOwnScripts()
        {
            RemoveOwnScripts();
            AddOwnScripts();
            return;

            void RemoveOwnScripts()
            {
                runScriptToolStripMenuItem.DropDown.Items.Clear();

                var list = mainContextMenu.Items.Cast<ToolStripItem>().ToList();

                foreach (var item in list)
                {
                    if (item.Name.Contains("_ownScript"))
                    {
                        mainContextMenu.Items.RemoveByKey(item.Name);
                    }
                }

                if (mainContextMenu.Items[mainContextMenu.Items.Count - 1] is ToolStripSeparator)
                {
                    mainContextMenu.Items.RemoveAt(mainContextMenu.Items.Count - 1);
                }
            }

            void AddOwnScripts()
            {
                var scripts = ScriptManager.GetScripts();

                if (scripts == null)
                {
                    return;
                }

                var lastIndex = mainContextMenu.Items.Count;

                foreach (var script in scripts)
                {
                    if (script.Enabled)
                    {
                        var item = new ToolStripMenuItem
                        {
                            Text = script.Name,
                            Name = script.Name + "_ownScript",
                            Image = script.GetIcon()
                        };
                        item.Click += RunScript;

                        if (script.AddToRevisionGridContextMenu)
                        {
                            mainContextMenu.Items.Add(item);
                        }
                        else
                        {
                            runScriptToolStripMenuItem.DropDown.Items.Add(item);
                        }
                    }
                }

                if (lastIndex != mainContextMenu.Items.Count)
                {
                    mainContextMenu.Items.Insert(lastIndex, new ToolStripSeparator());
                }

                bool showScriptsMenu = runScriptToolStripMenuItem.DropDown.Items.Count > 0;
                runScriptToolStripMenuItem.Visible = showScriptsMenu;

                return;

                void RunScript(object sender, EventArgs e)
                {
                    if (_settingsLoaded == false)
                    {
                        new FormSettings(UICommands).LoadSettings();
                        _settingsLoaded = true;
                    }

                    if (ScriptRunner.RunScript(this, Module, sender.ToString(), this))
                    {
                        RefreshRevisions();
                    }
                }
            }
        }

        internal void ToggleShowGitNotes()
        {
            AppSettings.ShowGitNotes = !AppSettings.ShowGitNotes;
            ForceRefreshRevisions();
        }

        internal void ToggleShowMergeCommits()
        {
            AppSettings.ShowMergeCommits = !AppSettings.ShowMergeCommits;

            // hide revision graph when hiding merge commits, reasons:
            // 1, revision graph is no longer relevant, as we are not showing all commits
            // 2, performance hit when both revision graph and no merge commits are enabled
            if (AppSettings.ShowRevisionGridGraphColumn && !AppSettings.ShowMergeCommits)
            {
                AppSettings.ShowRevisionGridGraphColumn = !AppSettings.ShowRevisionGridGraphColumn;
            }

            ForceRefreshRevisions();
        }

        internal void ShowFirstParent()
        {
            AppSettings.ShowFirstParent = !AppSettings.ShowFirstParent;

            ShowFirstParentsToggled?.Invoke(this, EventArgs.Empty);

            ForceRefreshRevisions();
        }

        internal void ToggleObjectIdColumn()
        {
            AppSettings.ShowObjectIdColumn = !AppSettings.ShowObjectIdColumn;
            MenuCommands.TriggerMenuChanged();
            Refresh();
        }

        internal void ToggleRevisionGraphColumn()
        {
            AppSettings.ShowRevisionGridGraphColumn = !AppSettings.ShowRevisionGridGraphColumn;

            // must show MergeCommits when showing revision graph
            if (!AppSettings.ShowMergeCommits && AppSettings.ShowRevisionGridGraphColumn)
            {
                AppSettings.ShowMergeCommits = true;
                ForceRefreshRevisions();
            }
            else
            {
                Refresh();
            }

            MenuCommands.TriggerMenuChanged();
        }

        internal void ToggleShowTags()
        {
            AppSettings.ShowTags = !AppSettings.ShowTags;
            MenuCommands.TriggerMenuChanged();
            Refresh();
        }

        internal bool ExecuteCommand(Commands cmd)
        {
            return ExecuteCommand((int)cmd);
        }

        private void ToggleHighlightSelectedBranch()
        {
            if (_revisionReader != null)
            {
                MessageBox.Show(_cannotHighlightSelectedBranch.Text);
                return;
            }

            var revision = GetSelectedRevisions().FirstOrDefault();

            if (revision != null)
            {
                HighlightBranch(revision.Guid);
                Refresh();
            }
        }

        private void deleteBranchTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;

            if (item.DropDown != null && item.DropDown.Items.Count == 1)
            {
                item.DropDown.Items[0].PerformClick();
            }
        }

        private void goToParentToolStripMenuItem_Click()
        {
            var r = LatestSelectedRevision;
            if (r != null)
            {
                if (_parentChildNavigationHistory.HasPreviousParent)
                {
                    _parentChildNavigationHistory.NavigateToPreviousParent(r.Guid);
                }
                else if (r.HasParent)
                {
                    _parentChildNavigationHistory.NavigateToParent(r.Guid, r.FirstParentGuid);
                }
            }
        }

        private void goToChildToolStripMenuItem_Click()
        {
            var r = LatestSelectedRevision;
            if (r != null)
            {
                var children = GetRevisionChildren(r.Guid);

                if (_parentChildNavigationHistory.HasPreviousChild)
                {
                    _parentChildNavigationHistory.NavigateToPreviousChild(r.Guid);
                }
                else if (children.Any())
                {
                    _parentChildNavigationHistory.NavigateToChild(r.Guid, children[0]);
                }
            }
        }

        public void GoToRef(string refName, bool showNoRevisionMsg)
        {
            if (DetachedHeadParser.TryParse(refName, out var sha1))
            {
                refName = sha1;
            }

            var revisionGuid = Module.RevParse(refName);
            if (revisionGuid != null)
            {
                if (_isLoading || !SetSelectedRevision(new GitRevision(revisionGuid.ToString())))
                {
                    _initialSelectedRevision = revisionGuid.ToString();
                    Graph.SelectedIds = null;
                    _lastSelectedRows = null;
                }
            }
            else if (showNoRevisionMsg)
            {
                MessageBox.Show(ParentForm as IWin32Window ?? this, _noRevisionFoundError.Text);
            }
        }

        internal Keys GetShortcutKeys(Commands cmd)
        {
            return GetShortcutKeys((int)cmd);
        }

        private void CompareToBranchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var headCommit = GetSelectedRevisions().First();
            using (var form = new FormCompareToBranch(UICommands, headCommit.Guid))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    var baseCommit = Module.RevParse(form.BranchName);
                    UICommands.ShowFormDiff(IsFirstParentValid(), baseCommit, headCommit.ObjectId,
                        form.BranchName, headCommit.Subject);
                }
            }
        }

        private void CompareWithCurrentBranchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var baseCommit = GetSelectedRevisions().First();
            var headBranch = Module.GetSelectedBranch();
            var headBranchName = Module.RevParse(headBranch);
            UICommands.ShowFormDiff(IsFirstParentValid(), baseCommit.ObjectId, headBranchName,
                baseCommit.Subject, headBranch);
        }

        private void selectAsBaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _baseCommitToCompare = GetSelectedRevisions().First();
            compareToBaseToolStripMenuItem.Enabled = true;
        }

        private void compareToBaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_baseCommitToCompare == null)
            {
                MessageBox.Show(this, _baseForCompareNotSelectedError.Text, _strError.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var headCommit = GetSelectedRevisions().First();
            UICommands.ShowFormDiff(IsFirstParentValid(), _baseCommitToCompare.ObjectId, headCommit.ObjectId,
                _baseCommitToCompare.Subject, headCommit.Subject);
        }

        private void getHelpOnHowToUseTheseFeaturesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OsShellUtil.OpenUrlInDefaultBrowser(
                UserManual.UserManual.UrlFor("modify_history", "using-autosquash-rebase-feature"));
        }

        private void openBuildReportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var revision = GetSelectedRevisions().First();

            if (!string.IsNullOrWhiteSpace(revision.BuildStatus?.Url))
            {
                Process.Start(revision.BuildStatus.Url);
            }
        }

        private void editCommitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LaunchRebase("e");
        }

        private void rewordCommitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LaunchRebase("r");
        }

        private void LaunchRebase(string command)
        {
            if (LatestSelectedRevision == null)
            {
                return;
            }

            string rebaseCmd = GitCommandHelpers.RebaseCmd(LatestSelectedRevision.FirstParentGuid,
                interactive: true, preserveMerges: false, autosquash: false, autostash: true);

            using (var formProcess = new FormProcess(null, rebaseCmd, Module.WorkingDir, null, true))
            {
                formProcess.ProcessEnvVariables.Add("GIT_SEQUENCE_EDITOR", string.Format("sed -i -re '0,/pick/s//{0}/'", command));
                formProcess.ShowDialog(this);
            }

            RefreshRevisions();
        }

        #region Nested type: RevisionGridInMemFilter

        private sealed class RevisionGridInMemFilter
        {
            private readonly string _authorFilter;
            private readonly Regex _authorFilterRegex;
            private readonly string _committerFilter;
            private readonly Regex _committerFilterRegex;
            private readonly string _messageFilter;
            private readonly Regex _messageFilterRegex;
            private readonly string _shaFilter;
            private readonly Regex _shaFilterRegex;

            private RevisionGridInMemFilter(string authorFilter, string committerFilter, string messageFilter, bool ignoreCase)
            {
                (_authorFilter, _authorFilterRegex) = SetUpVars(authorFilter, ignoreCase);
                (_committerFilter, _committerFilterRegex) = SetUpVars(committerFilter, ignoreCase);
                (_messageFilter, _messageFilterRegex) = SetUpVars(messageFilter, ignoreCase);

                if (!string.IsNullOrEmpty(_messageFilter) && ObjectId.IsValidPartial(_messageFilter, minLength: 5))
                {
                    (_shaFilter, _shaFilterRegex) = SetUpVars(messageFilter, false);
                }

                (string filterStr, Regex filterRegex) SetUpVars(string filterValue, bool ignoreKase)
                {
                    var filterStr = filterValue?.Trim() ?? string.Empty;

                    try
                    {
                        var options = ignoreKase ? RegexOptions.IgnoreCase : RegexOptions.None;
                        return (filterStr, new Regex(filterStr, options));
                    }
                    catch (ArgumentException)
                    {
                        return (filterStr, null);
                    }
                }
            }

            public bool Predicate(GitRevision rev)
            {
                return CheckCondition(_authorFilter, _authorFilterRegex, rev.Author) &&
                       CheckCondition(_committerFilter, _committerFilterRegex, rev.Committer) &&
                       (CheckCondition(_messageFilter, _messageFilterRegex, rev.Body) ||
                        (_shaFilter != null && CheckCondition(_shaFilter, _shaFilterRegex, rev.Guid)));

                bool CheckCondition(string filter, Regex regex, string value)
                {
                    return string.IsNullOrEmpty(filter) ||
                           (regex != null && value != null && regex.IsMatch(value));
                }
            }

            [CanBeNull]
            public static RevisionGridInMemFilter CreateIfNeeded([CanBeNull] string authorFilter,
                [CanBeNull] string committerFilter,
                [CanBeNull] string messageFilter,
                bool ignoreCase)
            {
                if (string.IsNullOrEmpty(authorFilter) &&
                    string.IsNullOrEmpty(committerFilter) &&
                    string.IsNullOrEmpty(messageFilter) &&
                    !ObjectId.IsValidPartial(messageFilter, minLength: 5))
                {
                    return null;
                }

                return new RevisionGridInMemFilter(
                    authorFilter,
                    committerFilter,
                    messageFilter,
                    ignoreCase);
            }
        }

        #endregion

        #region Nested type: SuperProjectInfo

        #endregion

        #region Drag/drop patch files on revision grid

        private void OnGraphDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is Array fileNameArray)
            {
                if (fileNameArray.Length > 10)
                {
                    // Some users need to be protected against themselves!
                    MessageBox.Show(this, _droppingFilesBlocked.Text);
                    return;
                }

                foreach (object fileNameObject in fileNameArray)
                {
                    var fileName = fileNameObject as string;

                    if (!string.IsNullOrEmpty(fileName) && fileName.EndsWith(".patch", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Start apply patch dialog for each dropped patch file...
                        UICommands.StartApplyPatchDialog(this, fileName);
                    }
                }
            }
        }

        private static void OnGraphDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is Array fileNameArray)
            {
                foreach (object fileNameObject in fileNameArray)
                {
                    var fileName = fileNameObject as string;

                    if (!string.IsNullOrEmpty(fileName) && fileName.EndsWith(".patch", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Allow drop (copy, not move) patch files
                        e.Effect = DragDropEffects.Copy;
                    }
                    else
                    {
                        // When a non-patch file is dragged, do not allow it
                        e.Effect = DragDropEffects.None;
                        return;
                    }
                }
            }
        }
        #endregion

        #region Hotkey commands

        internal enum Commands
        {
            ToggleRevisionGraph = 0,
            RevisionFilter = 1,
            ToggleAuthorDateCommitDate = 2,
            ToggleOrderRevisionsByDate = 3,
            ToggleShowRelativeDate = 4,
            ToggleDrawNonRelativesGray = 5,
            ToggleShowGitNotes = 6,
            //// <snip>
            ToggleShowMergeCommits = 8,
            ShowAllBranches = 9,
            ShowCurrentBranchOnly = 10,
            ShowFilteredBranches = 11,
            ShowRemoteBranches = 12,
            ShowFirstParent = 13,
            GoToParent = 14,
            GoToChild = 15,
            ToggleHighlightSelectedBranch = 16,
            NextQuickSearch = 17,
            PrevQuickSearch = 18,
            SelectCurrentRevision = 19,
            GoToCommit = 20,
            NavigateBackward = 21,
            NavigateForward = 22,
            SelectAsBaseToCompare = 23,
            CompareToBase = 24,
            CreateFixupCommit = 25,
            ToggleShowTags = 26,
            ToggleBranchTreePanel = 27
        }

        protected override bool ExecuteCommand(int cmd)
        {
            var command = (Commands)cmd;

            switch (command)
            {
                case Commands.ToggleRevisionGraph: ToggleRevisionGraphColumn(); break;
                case Commands.RevisionFilter: ShowRevisionFilterDialog(); break;
                case Commands.ToggleAuthorDateCommitDate: ToggleShowAuthorDate(); break;
                case Commands.ToggleOrderRevisionsByDate: ToggleOrderRevisionByDate(); break;
                case Commands.ToggleShowRelativeDate: ToggleShowRelativeDate(null); break;
                case Commands.ToggleDrawNonRelativesGray: ToggleDrawNonRelativesGray(); break;
                case Commands.ToggleShowGitNotes: ToggleShowGitNotes(); break;
                case Commands.ToggleShowMergeCommits: ToggleShowMergeCommits(); break;
                case Commands.ToggleShowTags: ToggleShowTags(); break;
                case Commands.ShowAllBranches: ShowAllBranches(); break;
                case Commands.ShowCurrentBranchOnly: ShowCurrentBranchOnly(); break;
                case Commands.ShowFilteredBranches: ShowFilteredBranches(); break;
                case Commands.ShowRemoteBranches: ToggleShowRemoteBranches(); break;
                case Commands.ShowFirstParent: ShowFirstParent(); break;
                case Commands.SelectCurrentRevision: SetSelectedRevision(new GitRevision(CurrentCheckout)); break;
                case Commands.GoToCommit: MenuCommands.GotoCommitExcecute(); break;
                case Commands.GoToParent: goToParentToolStripMenuItem_Click(); break;
                case Commands.GoToChild: goToChildToolStripMenuItem_Click(); break;
                case Commands.ToggleHighlightSelectedBranch: ToggleHighlightSelectedBranch(); break;
                case Commands.NextQuickSearch: _quickSearchProvider.NextResult(down: true); break;
                case Commands.PrevQuickSearch: _quickSearchProvider.NextResult(down: false); break;
                case Commands.NavigateBackward: NavigateBackward(); break;
                case Commands.NavigateForward: NavigateForward(); break;
                case Commands.SelectAsBaseToCompare: selectAsBaseToolStripMenuItem_Click(null, null); break;
                case Commands.CompareToBase: compareToBaseToolStripMenuItem_Click(null, null); break;
                case Commands.CreateFixupCommit: FixupCommitToolStripMenuItemClick(null, null); break;
                case Commands.ToggleBranchTreePanel: ToggleBranchTreePanel(); break;
                default:
                {
                    bool result = base.ExecuteCommand(cmd);
                    RefreshRevisions();
                    return result;
                }
            }

            return true;
        }

        #endregion
    }
}
