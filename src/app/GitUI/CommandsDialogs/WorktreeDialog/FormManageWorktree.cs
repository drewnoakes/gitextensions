using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;

namespace GitUI.CommandsDialogs.WorktreeDialog;

public partial class FormManageWorktree : GitExtensionsDialog
{
    private SortableWorktreeList? _worktrees;
    private string? _mainWorktreePath;

    public bool ShouldRefreshRevisionGrid { get; private set; }

    public FormManageWorktree(IGitUICommands commands)
        : base(commands, enablePositionRestore: false)
    {
        InitializeComponent();

        Sha1.Width = DpiUtil.Scale(53);
        Worktrees.AutoGenerateColumns = false;

        Path.DataPropertyName = nameof(GitWorktree.Path);
        Type.DataPropertyName = nameof(GitWorktree.HeadType);
        Branch.DataPropertyName = nameof(GitWorktree.Branch);
        Sha1.DataPropertyName = nameof(GitWorktree.Sha1);
        LastCommit.DataPropertyName = nameof(GitWorktree.LastCommitDate);

        Worktrees.Columns[3].DefaultCellStyle.Font = AppSettings.MonospaceFont;
        Worktrees.Columns[3].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        Worktrees.Select();

        InitializeComplete();
    }

    /// <summary>
    /// If this is not null before showing the dialog the given
    /// remote name will be preselected in the listbox.
    /// </summary>
    public string? PreselectRemoteOnLoad { get; set; }

    protected override void OnRuntimeLoad(EventArgs e)
    {
        base.OnRuntimeLoad(e);

        Initialize();
        RestoreSortOrder();
    }

    private void Initialize()
    {
        IReadOnlyList<GitWorktree> worktreeData = Module.GetWorktrees(includeCommitDates: true);

        // The main worktree is always listed first by git
        _mainWorktreePath = worktreeData.Count > 0 ? worktreeData[0].Path : null;

        _worktrees = new SortableWorktreeList();
        _worktrees.AddRange(worktreeData);

        Worktrees.DataSource = _worktrees;

        Font? font = Worktrees.DefaultCellStyle.Font;
        Font deletedFont = new(font?.FontFamily ?? FontFamily.GenericSansSerif, font?.Size ?? 8.25f, (font?.Style ?? FontStyle.Regular) | FontStyle.Strikeout);

        for (int i = 0; i < Worktrees.Rows.Count; i++)
        {
            if (_worktrees[i].IsDeleted)
            {
                Worktrees.Rows[i].DefaultCellStyle.Font = deletedFont;
            }
        }

        buttonPruneWorktrees.Enabled = worktreeData.Skip(1).Any(w => w.IsDeleted);
    }

    private void RestoreSortOrder()
    {
        string columnName = AppSettings.ManageWorktreeSortColumn;
        if (string.IsNullOrEmpty(columnName))
        {
            return;
        }

        DataGridViewColumn? column = Worktrees.Columns[columnName];
        if (column is null)
        {
            return;
        }

        ListSortDirection direction = AppSettings.ManageWorktreeSortAscending
            ? ListSortDirection.Ascending
            : ListSortDirection.Descending;

        Worktrees.Sort(column, direction);
    }

    private void Worktrees_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (Worktrees.SortedColumn is not null)
        {
            AppSettings.ManageWorktreeSortColumn = Worktrees.SortedColumn.Name;
            AppSettings.ManageWorktreeSortAscending = Worktrees.SortOrder == SortOrder.Ascending;
        }
    }

    private void buttonPruneWorktrees_Click(object sender, EventArgs e) => PruneWorktrees();

    private void PruneWorktrees()
    {
        UICommands.StartCommandLineProcessDialog(this, command: null, "worktree prune");
        Initialize();
    }

    private void buttonDeleteSelectedWorktree_Click(object sender, EventArgs e)
    {
        if (!CanActOnSelectedWorkspace(out GitWorktree? workTree))
        {
            return;
        }

        if (UICommands.WorktreeDelete(this, workTree.Path))
        {
            Initialize();
        }
    }

    private void buttonOpenSelectedWorktree_Click(object sender, EventArgs e)
    {
        OpenSelectedWorktree();
    }

    private void WorktreesOnCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        OpenSelectedWorktree();
    }

    private void Worktrees_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            OpenSelectedWorktree();
        }
    }

    private void OpenSelectedWorktree()
    {
        if (!CanActOnSelectedWorkspace(out GitWorktree? workTree))
        {
            return;
        }

        if (UICommands.WorktreeSwitch(this, workTree.Path))
        {
            Close();
        }
    }

    private void Worktrees_SelectionChanged(object sender, EventArgs e)
    {
        buttonDeleteSelectedWorktree.Enabled = CanDeleteSelectedWorkspace();
        buttonOpenSelectedWorktree.Enabled = CanActOnSelectedWorkspace(out _);
    }

    private bool CanDeleteSelectedWorkspace()
        => CanActOnSelectedWorkspace(out GitWorktree? workTree) && workTree.Path != _mainWorktreePath;

    private bool CanActOnSelectedWorkspace([NotNullWhen(true)] out GitWorktree? workTree)
    {
        workTree = null;

        if (_worktrees is null or { Count: <= 1 } || Worktrees.SelectedRows.Count == 0)
        {
            return false;
        }

        workTree = _worktrees[Worktrees.SelectedRows[0].Index];

        if (workTree.IsDeleted)
        {
            return false;
        }

        return !IsCurrentlyOpenedWorktree(workTree);
    }

    private bool IsCurrentlyOpenedWorktree(GitWorktree workTree)
        => new DirectoryInfo(UICommands.Module.WorkingDir).FullName.TrimEnd('\\') == new DirectoryInfo(workTree.Path).FullName.TrimEnd('\\');

    private void buttonCreateNewWorktree_Click(object sender, EventArgs e)
    {
        string basePath = _mainWorktreePath ?? UICommands.Module.WorkingDir;

        if (UICommands.WorktreeCreate(this, basePath))
        {
            ShouldRefreshRevisionGrid = true;
            Initialize();
        }
    }

    private sealed class SortableWorktreeList : SortableBindingList<GitWorktree>
    {
        static SortableWorktreeList()
        {
            AddSortableProperty(w => w.Path, (x, y) => string.Compare(x.Path, y.Path, StringComparison.OrdinalIgnoreCase));
            AddSortableProperty(w => w.HeadType, (x, y) => x.HeadType.CompareTo(y.HeadType));
            AddSortableProperty(w => w.Branch, (x, y) => string.Compare(x.Branch, y.Branch, StringComparison.OrdinalIgnoreCase));
            AddSortableProperty(w => w.Sha1, (x, y) => string.Compare(x.Sha1, y.Sha1, StringComparison.Ordinal));
            AddSortableProperty(w => w.LastCommitDate, (x, y) => Nullable.Compare(x.LastCommitDate, y.LastCommitDate));
        }
    }
}
