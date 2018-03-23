﻿using GitUIPluginInterfaces;
using ResourceManager;

[assembly: PluginType(typeof(Bitbucket.BitbucketPlugin))]

namespace Bitbucket
{
    public class BitbucketPlugin : GitPluginBase
    {
        public readonly StringSetting BitbucketUsername = new StringSetting("Bitbucket Username", string.Empty);
        public readonly PasswordSetting BitbucketPassword = new PasswordSetting("Bitbucket Password", string.Empty);
        public readonly StringSetting BitbucketBaseUrl = new StringSetting("Specify the base URL to Bitbucket", "https://example.bitbucket.com");
        public readonly BoolSetting BitbucketDisableSsl = new BoolSetting("Disable SSL verification", false);

        public BitbucketPlugin()
            : base("Bitbucket Server")
        {
            Translate();
        }

        public override bool Execute(GitUIBaseEventArgs gitUiCommands)
        {
            using (var frm = new BitbucketPullRequestForm(this, Settings, gitUiCommands))
            {
                frm.ShowDialog(gitUiCommands.OwnerForm);
            }

            return true;
        }

        public override System.Collections.Generic.IEnumerable<ISetting> GetSettings()
        {
            yield return BitbucketUsername;
            yield return BitbucketPassword;
            yield return BitbucketBaseUrl;
            yield return BitbucketDisableSsl;
        }
    }
}
