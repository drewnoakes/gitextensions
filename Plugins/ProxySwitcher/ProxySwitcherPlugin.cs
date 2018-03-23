using GitUIPluginInterfaces;
using ResourceManager;

[assembly: PluginType(typeof(ProxySwitcher.ProxySwitcherPlugin))]

namespace ProxySwitcher
{
    public class ProxySwitcherPlugin : GitPluginBase
    {
        public readonly StringSetting Username = new StringSetting("Username", string.Empty);
        public readonly StringSetting Password = new StringSetting("Password", string.Empty);
        public readonly StringSetting HttpProxy = new StringSetting("HTTP proxy", string.Empty);
        public readonly StringSetting HttpProxyPort = new StringSetting("HTTP proxy port", "8080");

        public ProxySwitcherPlugin()
            : base("Proxy Switcher")
        {
            Translate();
        }

        public override System.Collections.Generic.IEnumerable<ISetting> GetSettings()
        {
            yield return Username;
            yield return Password;
            yield return HttpProxy;
            yield return HttpProxyPort;
        }

        public override bool Execute(GitUIBaseEventArgs gitUiCommands)
        {
            using (var form = new ProxySwitcherForm(this, Settings, gitUiCommands))
            {
                form.ShowDialog(gitUiCommands.OwnerForm);
            }

            return false;
        }
    }
}
