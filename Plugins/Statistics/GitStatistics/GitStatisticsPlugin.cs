using System.Collections.Generic;
using GitUIPluginInterfaces;
using ResourceManager;

[assembly: PluginType(typeof(GitStatistics.GitStatisticsPlugin))]

namespace GitStatistics
{
    public sealed class GitStatisticsPlugin : GitPluginBase, IGitPluginForRepository
    {
        public GitStatisticsPlugin()
            : base("Statistics")
        {
            Translate();
        }

        private readonly StringSetting _codeFiles = new StringSetting("Code files",
                                "*.c;*.cpp;*.cc;*.cxx;*.h;*.hpp;*.hxx;*.inl;*.idl;*.asm;*.inc;*.cs;*.xsd;*.wsdl;*.xml;*.htm;*.html;*.css;" +
                                "*.vbs;*.vb;*.sql;*.aspx;*.asp;*.php;*.nav;*.pas;*.py;*.rb;*.js;*.mk;*.java");
        private readonly StringSetting _ignoreDirectories = new StringSetting("Directories to ignore (EndsWith)", "\\Debug;\\Release;\\obj;\\bin;\\lib");
        private readonly BoolSetting _ignoreSubmodules = new BoolSetting("Ignore submodules", true);

        public override IEnumerable<ISetting> GetSettings()
        {
            yield return _codeFiles;
            yield return _ignoreDirectories;
            yield return _ignoreSubmodules;
        }

        public override bool Execute(GitUIBaseEventArgs e)
        {
            if (string.IsNullOrEmpty(e.GitModule.WorkingDir))
            {
                return false;
            }

            bool includeSubmodules = !_ignoreSubmodules.ValueOrDefault(Settings);

            var form = new FormGitStatistics(e.GitModule, _codeFiles.ValueOrDefault(Settings), includeSubmodules)
            {
                DirectoriesToIgnore = _ignoreDirectories.ValueOrDefault(Settings)
            };

            using (form)
            {
                form.DirectoriesToIgnore = form.DirectoriesToIgnore.Replace("/", "\\");

                form.ShowDialog(e.OwnerForm);
            }

            return false;
        }
    }
}
