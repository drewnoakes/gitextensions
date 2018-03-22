using System.Collections.Generic;
using GitUIPluginInterfaces;

namespace GitUI.Plugin
{
    public static class LoadedPlugins
    {
        public static IList<IGitPlugin> Plugins { get; } = new List<IGitPlugin>();
    }
}
