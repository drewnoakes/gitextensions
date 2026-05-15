using GitExtensions.Extensibility.Git;
using GitUI.Editor;
using GitUI.Properties;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Lightweight panel that shows the staged/unstaged status of a worktree
///  and provides a button to open the full commit dialog.
/// </summary>
internal sealed class WorktreeCommitPanel : UserControl
{
    private readonly ListBox _fileList;
    private readonly FileViewer _diffViewer;
    private readonly Button _btnCommit;
    private readonly Label _lblHeader;
    private readonly Label _lblSummary;
    private readonly SplitContainer _mainSplit;

    private IGitUICommands? _uiCommands;
    private IReadOnlyList<GitItemStatus> _allFiles = [];

    public WorktreeCommitPanel()
    {
        _lblHeader = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(4, 4, 4, 0),
            Font = new Font(Font, FontStyle.Bold),
            Text = "Worktree"
        };

        _lblSummary = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(4, 2, 4, 4),
            Text = ""
        };

        _btnCommit = new Button
        {
            Text = "Commit...",
            Dock = DockStyle.Bottom,
            Height = 30,
            Image = Images.RepoStateDirty,
            TextImageRelation = TextImageRelation.ImageBeforeText,
        };
        _btnCommit.Click += OnCommitClick;

        _fileList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
        };
        _fileList.SelectedIndexChanged += OnFileSelected;

        _diffViewer = new FileViewer
        {
            Dock = DockStyle.Fill,
        };

        Panel leftPanel = new()
        {
            Dock = DockStyle.Fill,
        };
        leftPanel.Controls.Add(_fileList);
        leftPanel.Controls.Add(_btnCommit);
        leftPanel.Controls.Add(_lblSummary);
        leftPanel.Controls.Add(_lblHeader);

        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 250,
        };
        _mainSplit.Panel1.Controls.Add(leftPanel);
        _mainSplit.Panel2.Controls.Add(_diffViewer);

        Controls.Add(_mainSplit);
    }

    /// <summary>
    ///  Binds the panel to a specific worktree via its <see cref="IGitUICommands"/>.
    /// </summary>
    public void Bind(IGitUICommands uiCommands, string displayName)
    {
        _uiCommands = uiCommands;
        _lblHeader.Text = displayName;
        RefreshStatus();
    }

    /// <summary>
    ///  Unbinds the panel, clearing the file list and diff viewer.
    /// </summary>
    public void Unbind()
    {
        _uiCommands = null;
        _allFiles = [];
        _lblHeader.Text = "Worktree";
        _lblSummary.Text = "";
        _fileList.Items.Clear();
        _diffViewer.Clear();
    }

    /// <summary>
    ///  Refreshes the staged/unstaged file list from the bound worktree.
    /// </summary>
    public void RefreshStatus()
    {
        if (_uiCommands is null)
        {
            return;
        }

        IGitModule module = _uiCommands.Module;

        ThreadHelper.FileAndForget(async () =>
        {
            IReadOnlyList<GitItemStatus> allFiles = module.GetAllChangedFiles();

            await this.SwitchToMainThreadAsync();

            _allFiles = allFiles;
            _fileList.Items.Clear();

            int unstaged = 0;
            int staged = 0;

            foreach (GitItemStatus file in allFiles)
            {
                string prefix = file.Staged == StagedStatus.Index ? "[staged] " : "";

                if (file.Staged == StagedStatus.Index)
                {
                    staged++;
                }
                else
                {
                    unstaged++;
                }

                _fileList.Items.Add($"{prefix}{file.Name}");
            }

            _lblSummary.Text = $"{unstaged} unstaged, {staged} staged";
        });
    }

    private void OnFileSelected(object? sender, EventArgs e)
    {
        if (_uiCommands is null || _fileList.SelectedIndex < 0 || _fileList.SelectedIndex >= _allFiles.Count)
        {
            _diffViewer.Clear();
            return;
        }

        GitItemStatus item = _allFiles[_fileList.SelectedIndex];
        bool staged = item.Staged == StagedStatus.Index;
        string extraArgs = staged ? "--cached" : "";

        ThreadHelper.FileAndForget(async () =>
        {
            Patch? patch = await _uiCommands.Module.GetCurrentChangesAsync(
                item.Name,
                item.OldName,
                staged,
                extraArgs);

            await this.SwitchToMainThreadAsync();
            await _diffViewer.ViewFixedPatchAsync(item.Name, patch?.Text ?? "");
        });
    }

    private void OnCommitClick(object? sender, EventArgs e)
    {
        if (_uiCommands is null)
        {
            return;
        }

        _uiCommands.StartCommitDialog(FindForm());

        // Refresh after the commit dialog closes to reflect any changes.
        RefreshStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _btnCommit.Click -= OnCommitClick;
            _fileList.SelectedIndexChanged -= OnFileSelected;
            _mainSplit.Dispose();
            _diffViewer.Dispose();
        }

        base.Dispose(disposing);
    }
}
