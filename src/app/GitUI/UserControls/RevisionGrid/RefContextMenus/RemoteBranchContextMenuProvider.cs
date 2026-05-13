using GitCommands;
using GitCommands.Config;
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

            if (!isAtCurrentHead)
            {
                string refUnambiguousName = context.GetRefUnambiguousName(gitRef);
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

        ToolStripMenuItem delete = new(_deleteBranch.Text, Images.BranchDelete);
        delete.Click += (_, _) => context.UICommands.StartDeleteRemoteBranchDialog(context.ParentForm, gitRef.Name);
        menu.Items.Add(delete);

        AddViewOnRemoteItem(menu, context.UICommands.Module, gitRef.Remote, gitRef.LocalName);
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
