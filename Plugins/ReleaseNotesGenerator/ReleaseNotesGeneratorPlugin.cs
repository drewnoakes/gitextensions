using System.Windows.Forms;
using GitUIPluginInterfaces;
using ResourceManager;

[assembly: PluginType(typeof(ReleaseNotesGenerator.ReleaseNotesGeneratorPlugin))]

namespace ReleaseNotesGenerator
{
    public sealed class ReleaseNotesGeneratorPlugin : GitPluginBase
    {
        public ReleaseNotesGeneratorPlugin()
            : base("Release Notes Generator")
        {
            Translate();
        }

        public override bool Execute(GitUIBaseEventArgs gitUiCommands)
        {
            using (var form = new ReleaseNotesGeneratorForm(gitUiCommands))
            {
                if (form.ShowDialog(gitUiCommands.OwnerForm) == DialogResult.OK)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
