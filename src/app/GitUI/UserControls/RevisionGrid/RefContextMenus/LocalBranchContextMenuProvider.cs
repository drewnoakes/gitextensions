using GitCommands;
using GitCommands.Config;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.CommandsDialogs.SettingsDialog.RevisionLinks;
using GitUI.Properties;
using ResourceManager;

namespace GitUI.UserControls.RevisionGrid.RefContextMenus;

/// <summary>
///  Provides context menu items for local branch (head) refs.
/// </summary>
internal sealed class LocalBranchContextMenuProvider : Translate, IRefContextMenuProvider
{
    private readonly TranslationString _checkoutBranch = new("Chec&kout this branch");
    private readonly TranslationString _openBranchWorktree = new("Open branch's &worktree");
    private readonly TranslationString _goToRemoteBranch = new("Go to remote &branch ({0})");
    private readonly TranslationString _fastForwardToThis = new("Fast-&forward to this branch");
    private readonly TranslationString _mergeIntoCurrent = new("&Merge into current branch");
    private readonly TranslationString _rebaseOnto = new("&Rebase current branch onto this");
    private readonly TranslationString _diffCurrentToThis = new("Diff &current → this");
    private readonly TranslationString _diffThisToCurrent = new("Diff this → cu&rrent");
    private readonly TranslationString _renameBranch = new("R&ename this branch");
    private readonly TranslationString _deleteBranch = new("&Delete this branch");
    private readonly TranslationString _deleteBranchAndWorktree = new("&Delete branch and worktree...");
    private readonly TranslationString _pushBranch = new("Pus&h this branch");
    private readonly TranslationString _viewOnRemote = new("View branch on {0}");

    public bool Handles(IGitRef? gitRef, string? stashReflogSelector) => gitRef?.IsHead is true;

    public void Populate(ContextMenuStrip menu, IGitRef? gitRef, string? stashReflogSelector, RefContextMenuContext context)
    {
        if (gitRef is null)
        {
            return;
        }

        bool isCurrentBranch = gitRef.CompleteName == context.CurrentBranchRef;
        bool isAtCurrentHead = gitRef.ObjectId == context.CurrentCheckout;

        if (!context.IsBareRepository && !isCurrentBranch)
        {
            string? worktreePath = context.GetWorktreePathForBranch(gitRef.Name);
            if (worktreePath is not null)
            {
                // Branch is checked out in another worktree — offer to switch to that worktree
                // rather than checking it out again (which git would reject).
                ToolStripMenuItem openWorktree = new(_openBranchWorktree.Text, Images.WorkTree);
                openWorktree.Click += (_, _) => context.UICommands.WorktreeSwitch(context.ParentForm, worktreePath, skipConfirmation: true);
                menu.Items.Add(openWorktree);
            }
            else
            {
                ToolStripMenuItem checkout = new(_checkoutBranch.Text, Images.BranchCheckout);
                checkout.Click += (_, _) => context.UICommands.StartCheckoutBranch(context.ParentForm, gitRef.Name);
                menu.Items.Add(checkout);
            }
        }

        if (!context.IsBareRepository && !isAtCurrentHead)
        {
            string refUnambiguousName = context.GetRefUnambiguousName(gitRef);

            if (context.CurrentCheckout is ObjectId headId
                && gitRef.ObjectId is ObjectId branchObjectId
                && context.IsAncestorOf(headId, branchObjectId))
            {
                ToolStripMenuItem fastForward = new(_fastForwardToThis.Text, Images.Merge);
                fastForward.Click += (_, _) => context.UICommands.StartCommandLineProcessDialog(
                    context.ParentForm,
                    Commands.MergeFastForwardOnly(gitRef.Name));
                menu.Items.Add(fastForward);
            }

            ToolStripMenuItem merge = new(_mergeIntoCurrent.Text, Images.Merge);
            merge.Click += (_, _) => context.UICommands.StartMergeBranchDialog(context.ParentForm, refUnambiguousName);
            menu.Items.Add(merge);

            ToolStripMenuItem rebase = new(_rebaseOnto.Text, Images.Rebase);
            rebase.Click += (_, _) => context.UICommands.StartRebase(context.ParentForm, refUnambiguousName);
            menu.Items.Add(rebase);
        }

        if (!isAtCurrentHead && gitRef.ObjectId is ObjectId gitRefObjectId && context.CurrentCheckout is ObjectId currentCheckoutId)
        {
            ToolStripMenuItem diffCurrentToThis = new(_diffCurrentToThis.Text, Images.Diff);
            diffCurrentToThis.Click += (_, _) => context.ShowFormDiff(currentCheckoutId, gitRefObjectId, context.CurrentBranchName, gitRef.Name);
            menu.Items.Add(diffCurrentToThis);

            ToolStripMenuItem diffThisToCurrent = new(_diffThisToCurrent.Text, Images.Diff);
            diffThisToCurrent.Click += (_, _) => context.ShowFormDiff(gitRefObjectId, currentCheckoutId, gitRef.Name, context.CurrentBranchName);
            menu.Items.Add(diffThisToCurrent);
        }

        AddGoToRemoteBranchItem(menu, gitRef, context);

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
        }

        ToolStripMenuItem rename = new(_renameBranch.Text, Images.EditFile);
        rename.Click += (_, _) => context.UICommands.StartRenameDialog(context.ParentForm, gitRef.Name);
        menu.Items.Add(rename);

        if (!isCurrentBranch)
        {
            string? worktreePath = context.GetWorktreePathForBranch(gitRef.Name);
            if (worktreePath is not null)
            {
                ToolStripMenuItem deleteWithWorktree = new(_deleteBranchAndWorktree.Text, Images.BranchDelete);
                deleteWithWorktree.Click += (_, _) =>
                {
                    if (context.UICommands.WorktreeDelete(context.ParentForm, worktreePath))
                    {
                        // The user already confirmed deletion of the worktree and branch.
                        // Delete the branch directly without a second confirmation dialog.
                        IGitCommand cmd = Commands.DeleteBranch([gitRef], force: true);
                        context.UICommands.StartCommandLineProcessDialog(context.ParentForm, cmd);
                        context.PerformRefreshRevisions();
                    }
                };
                menu.Items.Add(deleteWithWorktree);
            }
            else
            {
                ToolStripMenuItem delete = new(_deleteBranch.Text, Images.BranchDelete);
                delete.Click += (_, _) => context.UICommands.StartDeleteBranchDialog(context.ParentForm, gitRef.Name);
                menu.Items.Add(delete);
            }
        }

        if (!context.IsBareRepository)
        {
            ToolStripMenuItem push = new(_pushBranch.Text, Images.Push);
            push.Click += (_, _) => context.UICommands.StartPushDialog(context.ParentForm, pushOnShow: false, forceWithLease: false, out _, gitRef.Name);
            menu.Items.Add(push);
        }

        if (!string.IsNullOrEmpty(gitRef.TrackingRemote) && !string.IsNullOrEmpty(gitRef.MergeWith))
        {
            AddViewOnRemoteItem(menu, context.UICommands.Module, gitRef.TrackingRemote, gitRef.MergeWith);
        }
    }

    private void AddGoToRemoteBranchItem(ContextMenuStrip menu, IGitRef gitRef, RefContextMenuContext context)
    {
        if (string.IsNullOrEmpty(gitRef.TrackingRemote) || string.IsNullOrEmpty(gitRef.MergeWith))
        {
            return;
        }

        string remoteBranchName = $"{gitRef.TrackingRemote}/{gitRef.MergeWith}";

        if (context.UICommands.Module.RevParse($"{GitRefName.RefsRemotesPrefix}{remoteBranchName}") is not ObjectId remoteObjectId
            || remoteObjectId == gitRef.ObjectId)
        {
            return;
        }

        ToolStripMenuItem goToRemote = new(string.Format(_goToRemoteBranch.Text, remoteBranchName), Images.BranchRemote);
        goToRemote.Click += (_, _) => context.GoToRevision(remoteObjectId);
        menu.Items.Add(goToRemote);
    }

    private void AddViewOnRemoteItem(ContextMenuStrip menu, IGitModule module, string remoteName, string branchName)
    {
        string remoteUrl = module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remoteName));
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return;
        }

        foreach (ICloudProviderExternalLinkDefinitionExtractor extractor in new CloudProviderExternalLinkDefinitionExtractorFactory().GetAllExtractor())
        {
            if (extractor.TryBuildBranchUrl(remoteUrl, branchName, out string? url))
            {
                menu.Items.Add(new ToolStripSeparator());
                ToolStripMenuItem viewOnRemote = new(string.Format(_viewOnRemote.Text, extractor.ServiceName), extractor.Icon);
                viewOnRemote.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(url);
                menu.Items.Add(viewOnRemote);
                return;
            }
        }
    }
}
