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
///  Provides context menu items for remote branch refs.
/// </summary>
internal sealed class RemoteBranchContextMenuProvider : Translate, IRefContextMenuProvider
{
    private readonly TranslationString _checkoutBranch = new("Chec&kout this branch");
    private readonly TranslationString _createWorktree = new("Create &worktree for this branch");
    private readonly TranslationString _fastForwardToThis = new("Fast-&forward current branch to here");
    private readonly TranslationString _goToLocalBranch = new("Go to &local branch");
    private readonly TranslationString _mergeIntoCurrent = new("&Merge into current branch");
    private readonly TranslationString _rebaseOnto = new("&Rebase current branch onto this");
    private readonly TranslationString _diffCurrentToThis = new("Diff &current → this");
    private readonly TranslationString _diffThisToCurrent = new("Diff this → cu&rrent");
    private readonly TranslationString _deleteBranch = new("&Delete this branch");
    private readonly TranslationString _viewOnRemote = new("View branch on {0}");

    public bool Handles(IGitRef? gitRef, string? stashReflogSelector) => gitRef?.IsRemote is true;

    public void Populate(ContextMenuStrip menu, IGitRef? gitRef, string? stashReflogSelector, RefContextMenuContext context)
    {
        if (gitRef is null)
        {
            return;
        }

        bool isAtCurrentHead = gitRef.ObjectId == context.CurrentCheckout;

        if (!context.IsBareRepository)
        {
            ToolStripMenuItem checkout = new(_checkoutBranch.Text, Images.BranchCheckout);
            checkout.Click += (_, _) => context.UICommands.StartCheckoutRemoteBranch(context.ParentForm, gitRef.Name);
            menu.Items.Add(checkout);

            // Offer to create a worktree when no local branch tracks this remote branch.
            if (context.FindLocalBranchTrackingRemote(gitRef) is null)
            {
                ToolStripMenuItem createWorktree = new(_createWorktree.Text, Images.WorkTree);
                createWorktree.Click += (_, _) => context.CreateWorktreeForBranch(gitRef.LocalName, gitRef.Name);
                menu.Items.Add(createWorktree);
            }

            if (!isAtCurrentHead)
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

            menu.Items.Add(new ToolStripSeparator());
        }

        if (!isAtCurrentHead && gitRef.ObjectId is ObjectId gitRefObjectId && context.CurrentCheckout is ObjectId currentCheckoutId)
        {
            ToolStripMenuItem diffCurrentToThis = new(_diffCurrentToThis.Text, Images.Diff);
            diffCurrentToThis.Click += (_, _) => context.ShowFormDiff(currentCheckoutId, gitRefObjectId, context.CurrentBranchName, gitRef.Name);
            menu.Items.Add(diffCurrentToThis);

            ToolStripMenuItem diffThisToCurrent = new(_diffThisToCurrent.Text, Images.Diff);
            diffThisToCurrent.Click += (_, _) => context.ShowFormDiff(gitRefObjectId, currentCheckoutId, gitRef.Name, context.CurrentBranchName);
            menu.Items.Add(diffThisToCurrent);

            menu.Items.Add(new ToolStripSeparator());
        }

        AddGoToLocalBranchItem(menu, gitRef, context);

        ToolStripMenuItem delete = new(_deleteBranch.Text, Images.BranchDelete);
        delete.Click += (_, _) => context.UICommands.StartDeleteRemoteBranchDialog(context.ParentForm, gitRef.Name);
        menu.Items.Add(delete);

        AddViewOnRemoteItem(menu, context.UICommands.Module, gitRef.Remote, gitRef.LocalName);
    }

    private void AddGoToLocalBranchItem(ContextMenuStrip menu, IGitRef remoteRef, RefContextMenuContext context)
    {
        (string name, ObjectId objectId)? localBranch = context.FindLocalBranchTrackingRemote(remoteRef);

        if (localBranch is not { } local || local.objectId == remoteRef.ObjectId)
        {
            return;
        }

        ToolStripMenuItem goToLocal = new(_goToLocalBranch.Text, Images.BranchLocal);
        goToLocal.Click += (_, _) => context.GoToRevision(local.objectId);
        menu.Items.Add(goToLocal);
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
