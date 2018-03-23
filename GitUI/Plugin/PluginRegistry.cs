using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using GitCommands;
using GitUIPluginInterfaces;
using GitUIPluginInterfaces.RepositoryHosts;
using JetBrains.Annotations;

namespace GitUI
{
    public static class PluginRegistry
    {
        private static int _initialised;
        private static IReadOnlyList<IGitPlugin> _plugins;
        private static IReadOnlyList<IRepositoryHostPlugin> _repoHostPlugins;

        public static bool IsInitialised => _plugins != null && _repoHostPlugins != null;

        [NotNull]
        public static IReadOnlyList<IGitPlugin> Plugins => _plugins ?? throw new InvalidOperationException($"Plugins not yet loaded. Call {nameof(Initialise)} first.");

        [NotNull]
        public static IReadOnlyList<IRepositoryHostPlugin> RepoHostPlugins => _repoHostPlugins ?? throw new InvalidOperationException($"Plugins not yet loaded. Call {nameof(Initialise)} first.");

        private static readonly IReadOnlyList<string> _ignorePrefixes = new[]
        {
            "System.",
            "ICSharpCode.",
            "Microsoft.",
            "TfsInterop."
        };

        private static readonly HashSet<string> _blackList = new HashSet<string>(
            new[]
            {
                "AppVeyorIntegration.dll",
                "Atlassian.Jira.dll",
                "Git.hub.dll",
                "GitCommands.dll",
                "GitExtUtils.dll",
                "GitUIPluginInterfaces.dll",
                "JenkinsIntegration.dll",
                "JetBrains.Annotations.dll",
                "netstandard.dll",
                "Newtonsoft.Json.dll",
                "NString.dll",
                "RestSharp.dll",
                "SmartFormat.dll",
                "TeamCityIntegration.dll",
                "TfsIntegration.dll",
                "TfsInterop.Vs2012.dll",
                "TfsInterop.Vs2013.dll",
                "TfsInterop.Vs2015.dll"
            });

        public static void Initialise()
        {
            // Prevent multiple calls to initialise (non-locking, thread safe)
            if (Interlocked.CompareExchange(ref _initialised, 1, 0) != 0)
            {
                throw new InvalidOperationException($"{nameof(PluginRegistry)}.{nameof(Initialise)} already called.");
            }

            var sw = Stopwatch.StartNew();

            var basePath = Path.GetDirectoryName(Application.ExecutablePath);

            Trace.Assert(basePath != null, "Application's base path could not be determined");

            var pluginsPath = Path.Combine(basePath, "Plugins");

            // Find all the DLL files in the "Plugins" folder
            var dllFiles = Directory.Exists(pluginsPath)
                ? new DirectoryInfo(pluginsPath).GetFiles("*.dll")
                : Array.Empty<FileInfo>();

            // Allocate to locals, so readers of properties cannot see partially initialised data during initialisation
            var plugins = new List<IGitPlugin>();
            var repoHostPlugins = new List<IRepositoryHostPlugin>();

            foreach (var dllFile in dllFiles)
            {
                if (_blackList.Contains(dllFile.Name))
                {
                    continue;
                }

                // Ignore some file name patterns we know do not contain plugins
                if (_ignorePrefixes.Any(prefix => dllFile.Name.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    continue;
                }

                try
                {
                    Debug.WriteLine($"Scanning file \"{dllFile.Name}\" for plugins");

                    var assembly = Assembly.LoadFile(dllFile.FullName);

                    ////var types = assembly.GetExportedTypes().Where(type => typeof(IGitPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
                    var types = assembly.GetCustomAttributes<PluginTypeAttribute>().Select(attribute => attribute.PluginType);

                    foreach (var type in types)
                    {
                        var plugin = (IGitPlugin)Activator.CreateInstance(type);

                        Debug.WriteLine("Found plugin: " + plugin.Name);

                        plugin.SettingsContainer = new GitPluginSettingsContainer(plugin.Name);

                        if (plugin is IRepositoryHostPlugin repoHostPlugin)
                        {
                            repoHostPlugins.Add(repoHostPlugin);
                        }

                        plugins.Add(plugin);
                    }
                }
                catch (SystemException ex)
                {
                    var errorMessage = BuildErrorMessage(dllFile, ex);

                    MessageBox.Show(errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Trace.WriteLine(ex.Message);
                }
            }

            Debug.WriteLine($"{plugins.Count} plugins loaded in {sw.Elapsed.TotalMilliseconds:#,##0.##} ms");

            // Make the discovered plugins publicly visible
            _plugins = plugins;
            _repoHostPlugins = repoHostPlugins;

            return;

            string BuildErrorMessage(FileInfo pluginFile, Exception ex)
            {
                var msg = new StringBuilder();
                msg.AppendLine($"Failed to load plugin \"{pluginFile}\".");
                msg.AppendLine();
                msg.AppendLine("Exception info:");

                if (ex is ReflectionTypeLoadException rtle)
                {
                    msg.AppendLine(rtle.Message);

                    foreach (var le in rtle.LoaderExceptions)
                    {
                        msg.AppendLine(le.Message);
                    }
                }
                else
                {
                    // Walk inner exceptions
                    while (true)
                    {
                        msg.AppendLine(ex.Message);

                        if (ex.InnerException == null)
                        {
                            break;
                        }

                        ex = ex.InnerException;
                    }
                }

                return msg.ToString();
            }
        }

        [CanBeNull]
        public static IRepositoryHostPlugin TryGetGitHosterForModule(GitModule module)
        {
            if (!module.IsValidGitWorkingDir())
            {
                return null;
            }

            return RepoHostPlugins.FirstOrDefault(gitHoster => gitHoster.GitModuleIsRelevantToMe(module));
        }
    }
}