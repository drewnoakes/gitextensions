﻿using GitUIPluginInterfaces;
using ResourceManager;

[assembly: PluginType(typeof(GitImpact.GitImpactPlugin))]

namespace GitImpact
{
    public sealed class GitImpactPlugin : GitPluginBase, IGitPluginForRepository
    {
        public GitImpactPlugin()
            : base("Impact Graph")
        {
            Translate();
        }

        public override bool Execute(GitUIBaseEventArgs gitUIEventArgs)
        {
            if (string.IsNullOrEmpty(gitUIEventArgs.GitModule.WorkingDir))
            {
                return false;
            }

            using (var form = new FormImpact(gitUIEventArgs.GitModule))
            {
                form.ShowDialog(gitUIEventArgs.OwnerForm);
            }

            return false;
        }
    }
}
