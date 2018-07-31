using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GitCommands;
using JetBrains.Annotations;
using Microsoft.VisualStudio.Threading;

namespace GitUI.AutoCompletion
{
    public class CommitAutoCompleteProvider : IAutoCompleteProvider
    {
#pragma warning disable VSTHRD012 // Provide JoinableTaskFactory where allowed
        private static readonly AsyncLazy<Dictionary<string, Regex>> _regexByExtension = new AsyncLazy<Dictionary<string, Regex>>(ParseRegexesAsync);
#pragma warning restore VSTHRD012 // Provide JoinableTaskFactory where allowed

        private readonly GitModule _module;

        public CommitAutoCompleteProvider(GitModule module)
        {
            _module = module;
        }

        public async Task<IEnumerable<AutoCompleteWord>> GetAutoCompleteWordsAsync(CancellationToken cancellationToken)
        {
            await TaskScheduler.Default.SwitchTo(alwaysYield: true);

            cancellationToken.ThrowIfCancellationRequested();

            var autoCompleteWords = new HashSet<string>();

            foreach (var file in _module.GetAllChangedFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var extension = Path.GetExtension(file.Name);

                var regexByExtension = await _regexByExtension.GetValueAsync(cancellationToken);

                if (regexByExtension.TryGetValue(extension, out var regex))
                {
                    var text = GetChangedFileText(_module, file);

                    foreach (Match match in regex.Matches(text))
                    {
                        // Skip first group since it always contains the entire matched string (regardless of capture groups)
                        foreach (Group group in match.Groups.OfType<Group>().Skip(1))
                        {
                            foreach (Capture capture in group.Captures)
                            {
                                autoCompleteWords.Add(capture.Value);
                            }
                        }
                    }
                }

                autoCompleteWords.Add(Path.GetFileNameWithoutExtension(file.Name));
                autoCompleteWords.Add(Path.GetFileName(file.Name));

                if (!string.IsNullOrWhiteSpace(file.OldName))
                {
                    autoCompleteWords.Add(Path.GetFileNameWithoutExtension(file.OldName));
                    autoCompleteWords.Add(Path.GetFileName(file.OldName));
                }
            }

            return autoCompleteWords.Select(w => new AutoCompleteWord(w));
        }

        private static async Task<Dictionary<string, Regex>> ParseRegexesAsync()
        {
            var stream = OpenStream();

            if (stream == null)
            {
                throw new NotImplementedException("Unable to open AutoCompleteRegexes.txt");
            }

            var regexes = new Dictionary<string, Regex>();

            using (stream)
            using (var reader = new StreamReader(stream))
            {
                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        break;
                    }

                    var index = line.IndexOf('=');

                    if (index == -1)
                    {
                        continue;
                    }

                    var extensionStr = line.Substring(0, index);
                    var regexStr = line.Substring(index + 1).Trim();

                    var regex = new Regex(regexStr, RegexOptions.Compiled);

                    foreach (var extension in extensionStr.Split(',').Select(s => s.Trim()).Distinct())
                    {
                        regexes.Add(extension, regex);
                    }
                }
            }

            return regexes;

            Stream OpenStream()
            {
                var path = Path.Combine(AppSettings.ApplicationDataPath.Value, "AutoCompleteRegexes.txt");

                if (File.Exists(path))
                {
                    return File.OpenRead(path);
                }

                return Assembly.GetEntryAssembly().GetManifestResourceStream("GitExtensions.AutoCompleteRegexes.txt");
            }
        }

        [CanBeNull]
        private static string GetChangedFileText(GitModule module, GitItemStatus file)
        {
            var changes = module.GetCurrentChanges(file.Name, file.OldName, file.Staged == StagedStatus.Index, "-U1000000", module.FilesEncoding);

            if (changes != null)
            {
                return changes.Text;
            }

            var content = module.GetFileContents(file);

            if (content != null)
            {
                return content;
            }

            // Try to read the contents of the file: if it cannot be read, skip the operation silently.
            try
            {
                return File.ReadAllText(Path.Combine(module.WorkingDir, file.Name));
            }
            catch
            {
                return "";
            }
        }
    }
}