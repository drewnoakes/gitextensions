using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace GitUI
{
    public static class PluginLoader
    {
        public static void Load()
        {
            lock (Plugin.LoadedPlugins.Plugins)
            {
                if (Plugin.LoadedPlugins.Plugins.Count > 0)
                {
                    return;
                }

                var file = new FileInfo(Application.ExecutablePath);

                if (file.Directory == null)
                {
                    return;
                }

                var pluginsPath = Path.Combine(file.Directory.FullName, "Plugins");

                var plugins = Directory.Exists(pluginsPath)
                    ? new DirectoryInfo(pluginsPath).GetFiles("*.dll")
                    : Array.Empty<FileInfo>();

                var pluginFiles = plugins.Where(pluginFile =>
                    !pluginFile.Name.StartsWith("System.") &&
                    !pluginFile.Name.StartsWith("ICSharpCode.") &&
                    !pluginFile.Name.StartsWith("Microsoft."));

                foreach (var pluginFile in pluginFiles)
                {
                    try
                    {
                        Debug.WriteLine("Loading plugin...", pluginFile.Name);
                        var types = Assembly.LoadFile(pluginFile.FullName).GetTypes();
                        PluginExtraction.ExtractPluginTypes(types);
                    }
                    catch (SystemException ex)
                    {
                        var msg = new StringBuilder();
                        msg.AppendLine($"Failed to load plugin \"{pluginFile}\".");
                        msg.AppendLine();
                        msg.AppendLine("Exception info:");

                        if (ex is ReflectionTypeLoadException rtle)
                        {
                            msg.AppendLine(ex.Message);

                            foreach (var el in rtle.LoaderExceptions)
                            {
                                msg.AppendLine(el.Message);
                            }
                        }
                        else
                        {
                            // Walk inner exceptions
                            Exception e = ex;
                            while (true)
                            {
                                msg.AppendLine(e.Message);

                                if (e.InnerException == null)
                                {
                                    break;
                                }

                                e = e.InnerException;
                            }
                        }

                        MessageBox.Show(msg.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Trace.WriteLine(ex.Message);
                    }
                }
            }
        }
    }
}