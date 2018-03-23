using GitUIPluginInterfaces;
using ResourceManager;

[assembly: PluginType(typeof(GitFlow.GitFlowPlugin))]

namespace GitFlow
{
    public sealed class GitFlowPlugin : GitPluginBase, IGitPluginForRepository
    {
        public GitFlowPlugin()
            : base("GitFlow")
        {
            Translate();
        }

        public override bool Execute(GitUIBaseEventArgs gitUiCommands)
        {
            using (var frm = new GitFlowForm(gitUiCommands))
            {
                frm.ShowDialog(gitUiCommands.OwnerForm);
                return frm.IsRefreshNeeded;
            }
        }
    }
}
