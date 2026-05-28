using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.Properties;
using ResourceManager;

namespace GitUI.UserControls.RevisionGrid.RefContextMenus;

/// <summary>
///  Composes a <see cref="ContextMenuStrip"/> for a right-clicked ref label by delegating
///  to the first matching <see cref="IRefContextMenuProvider"/>.
/// </summary>
internal sealed class RefContextMenuComposer : Translate
{
    private readonly TranslationString _copyBranchName = new("Copy &branch name");
    private readonly TranslationString _copyTagName = new("Copy &tag name");
    private readonly TranslationString _copyName = new("&Copy name");
    private readonly TranslationString _copyWorktreePath = new("Copy worktree &path");

    private readonly IReadOnlyList<IRefContextMenuProvider> _providers;

    public RefContextMenuComposer(IReadOnlyList<IRefContextMenuProvider> providers)
    {
        _providers = providers;
    }

    /// <summary>
    ///  Builds a <see cref="ContextMenuStrip"/> for the given ref or stash selector.
    ///  Returns <see langword="null"/> when no provider produced any items.
    /// </summary>
    public ContextMenuStrip? Build(IGitRef? gitRef, string? stashReflogSelector, RefContextMenuContext context)
    {
        IRefContextMenuProvider? provider = null;
        foreach (IRefContextMenuProvider p in _providers)
        {
            if (p.Handles(gitRef, stashReflogSelector))
            {
                provider = p;
                break;
            }
        }

        if (provider is null)
        {
            return null;
        }

        ContextMenuStrip menu = new();
        provider.Populate(menu, gitRef, stashReflogSelector, context);

        if (menu.Items.Count == 0)
        {
            menu.Dispose();
            return null;
        }

        string copyText = gitRef?.Name ?? stashReflogSelector ?? "";
        string copyLabel = GetCopyLabel(gitRef);

        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem copy = new(copyLabel, Images.CopyToClipboard);
        copy.Click += (_, _) => ClipboardUtil.TrySetText(copyText);
        menu.Items.Add(copy);

        if (gitRef?.IsHead is true)
        {
            string? worktreePath = context.GetWorktreePathForBranch(gitRef.Name);
            if (worktreePath is not null)
            {
                ToolStripMenuItem copyPath = new(_copyWorktreePath.Text, Images.CopyToClipboard);
                copyPath.Click += (_, _) => ClipboardUtil.TrySetText(worktreePath);
                menu.Items.Add(copyPath);
            }
        }

        return menu;

        string GetCopyLabel(IGitRef? gitRef)
        {
            if (gitRef?.IsHead is true || gitRef?.IsRemote is true)
            {
                return _copyBranchName.Text;
            }

            if (gitRef?.IsTag is true)
            {
                return _copyTagName.Text;
            }

            return _copyName.Text;
        }
    }
}
