using System.Diagnostics;
using GitExtensions.Extensibility.Git;
using GitUI.Properties;

namespace GitUI.LeftPanel;

[DebuggerDisplay("(Worktree) Path = {Worktree.Path}, Branch = {Worktree.Branch}")]
internal sealed class WorktreeNode(Tree tree, GitWorktree worktree, bool isCurrent) : Node(tree)
{
    public GitWorktree Worktree { get; } = worktree;
    public bool IsCurrent { get; } = isCurrent;

    internal override void OnSelected()
    {
        if (Tree.IgnoreSelectionChangedEvent)
        {
            return;
        }

        SelectRevision();
    }

    internal override void OnDoubleClick()
    {
        if (!IsCurrent && !Worktree.IsDeleted)
        {
            OpenWorktree();
        }
    }

    public void OpenWorktree()
    {
        if (Worktree.IsDeleted)
        {
            return;
        }

        UICommands.WorktreeSwitch(ParentWindow(), Worktree.Path);
    }

    public void DeleteWorktree()
    {
        if (UICommands.WorktreeDelete(ParentWindow(), Worktree.Path))
        {
            ((WorktreeTree)Tree).Refresh();
        }
    }

    private void SelectRevision()
    {
        if (Worktree.Sha1 is null)
        {
            return;
        }

        TreeViewNode.TreeView?.BeginInvoke(() =>
        {
            UICommands.BrowseRepo?.GoToRef(Worktree.Sha1, showNoRevisionMsg: true, toggleSelection: Control.ModifierKeys.HasFlag(Keys.Control));
            TreeViewNode.TreeView?.Focus();
        });
    }

    protected override FontStyle GetFontStyle()
        => base.GetFontStyle() | (IsCurrent ? FontStyle.Bold : FontStyle.Regular);

    public override void ApplyStyle()
    {
        base.ApplyStyle();

        if (Worktree.IsDeleted)
        {
            TreeViewNode.ForeColor = SystemColors.GrayText;
        }

        TreeViewNode.ImageKey = TreeViewNode.SelectedImageKey = nameof(Images.WorkTree);
        TreeViewNode.ToolTipText = GetToolTipText();

        return;

        string GetToolTipText()
        {
            string? shortSha = Worktree.Sha1?.Length >= 7 ? Worktree.Sha1[..7] : Worktree.Sha1;

            string status = IsCurrent ? " (current)"
                : Worktree.IsDeleted ? " (deleted)"
                : "";

            string branchLine = Worktree.HeadType is GitWorktreeHeadType.Bare ? "bare"
                : Worktree.HeadType is GitWorktreeHeadType.Detached ? $"detached at {shortSha}"
                : Worktree.Branch ?? "unknown";

            return shortSha is not null
                ? $"{Worktree.Path}{status}\nBranch: {branchLine}\nHEAD: {shortSha}"
                : $"{Worktree.Path}{status}\nBranch: {branchLine}";
        }
    }

    protected override string DisplayText()
    {
        return Worktree.GetDisplayName(GetRelativePath());

        string GetRelativePath()
        {
            string workingDir = Tree.UICommands.Module.WorkingDir.TrimEnd(Path.DirectorySeparatorChar);
            string parentDir = Path.GetDirectoryName(workingDir) ?? workingDir;

            try
            {
                return Path.GetRelativePath(parentDir, Worktree.Path);
            }
            catch
            {
                return Worktree.Path;
            }
        }
    }
}
