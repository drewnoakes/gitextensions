using GitUIPluginInterfaces;
using ResourceManager;

[assembly: PluginType(typeof(CreateLocalBranches.CreateLocalBranchesPlugin))]

namespace CreateLocalBranches
{
    public sealed class CreateLocalBranchesPlugin : GitPluginBase, IGitPluginForRepository
    {
        public CreateLocalBranchesPlugin()
            : base("Create local tracking branches")
        {
            Translate();
        }

        public override bool Execute(GitUIBaseEventArgs gitUiCommands)
        {
            using (var frm = new CreateLocalBranchesForm(gitUiCommands))
            {
                frm.ShowDialog(gitUiCommands.OwnerForm);
            }

            return true;
        }
    }
}
