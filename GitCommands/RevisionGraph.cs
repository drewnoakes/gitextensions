﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GitUI;
using GitUIPluginInterfaces;
using JetBrains.Annotations;
using Microsoft.VisualStudio.Threading;

namespace GitCommands
{
    [Flags]
    public enum RefFilterOptions
    {
        Branches = 1,              // --branches
        Remotes = 2,               // --remotes
        Tags = 4,                  // --tags
        Stashes = 8,               //
        All = 15,                  // --all
        Boundary = 16,             // --boundary
        ShowGitNotes = 32,         // --not --glob=notes --not
        NoMerges = 64,             // --no-merges
        FirstParent = 128,         // --first-parent
        SimplifyByDecoration = 256 // --simplify-by-decoration
    }

    public sealed class RevisionGraph : IDisposable
    {
        private static readonly char[] _shellGlobCharacters = { '?', '*', '[' };

        private static readonly Regex _commitRegex = new Regex(@"
                ^
                ([^\n]+)\n   # 1 authorname
                ([^\n]+)\n   # 2 authoremail
                (\d+)\n      # 3 authordate
                ([^\n]+)\n   # 4 committername
                ([^\n]+)\n   # 5 committeremail
                (\d+)\n      # 6 commitdate
                ([^\n]*)\n   # 7 encoding
                (.+)         # 8 subject
                (\n+
                  ((.|\n)*)  # 10 body
                )?
                $
            ",
            RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public event EventHandler Exited;
        public event Action<GitRevision> Updated;
        public event EventHandler<AsyncErrorEventArgs> Error;

        private readonly CancellationTokenSequence _cancellationTokenSequence = new CancellationTokenSequence();
        private readonly GitModule _module;
        private readonly RefFilterOptions _refFilterOptions;
        private readonly string _branchFilter;
        private readonly string _revisionFilter;
        private readonly string _pathFilter;
        [CanBeNull] private readonly Func<GitRevision, bool> _revisionPredicate;

        [CanBeNull] private Dictionary<string, List<IGitRef>> _refs;
        private string _selectedBranchName;

        public int RevisionCount { get; private set; }

        public RevisionGraph(
            GitModule module,
            RefFilterOptions refFilterOptions,
            string branchFilter,
            string revisionFilter,
            string pathFilter,
            [CanBeNull] Func<GitRevision, bool> revisionPredicate)
        {
            _module = module;
            _refFilterOptions = refFilterOptions;
            _branchFilter = branchFilter;
            _revisionFilter = revisionFilter;
            _pathFilter = pathFilter;
            _revisionPredicate = revisionPredicate;
        }

        /// <value>Refs loaded during the last call to <see cref="Execute"/>.</value>
        public IEnumerable<IGitRef> LatestRefs => _refs?.SelectMany(p => p.Value) ?? Enumerable.Empty<IGitRef>();

        public void Execute()
        {
            ThreadHelper.JoinableTaskFactory
                .RunAsync(ExecuteAsync)
                .FileAndForget(
                    ex =>
                    {
                        var args = new AsyncErrorEventArgs(ex);
                        Error?.Invoke(this, args);
                        return !args.Handled;
                    });
        }

        private async Task ExecuteAsync()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var token = _cancellationTokenSequence.Next();

            RevisionCount = 0;
            Updated?.Invoke(null);

            await TaskScheduler.Default;

            if (token.IsCancellationRequested)
            {
                return;
            }

            _refs = GetRefs().ToDictionaryOfList(head => head.Guid);

            if (token.IsCancellationRequested)
            {
                return;
            }

            const string fullFormat =
                /* Hash                    */ "%H" +
                /* Tree                    */ "%T" +
                /* Parents                 */ "%P%n" +
                /* Author Name             */ "%aN%n" +
                /* Author Email            */ "%aE%n" +
                /* Author Date             */ "%at%n" +
                /* Committer Name          */ "%cN%n" +
                /* Committer Email         */ "%cE%n" +
                /* Commit Date             */ "%ct%n" +
                /* Commit message encoding */ "%e%n" + // there is a bug: git does not recode commit message when format is given
                /* Commit Body             */ "%B";

            var arguments = new ArgumentBuilder
            {
                "log",
                "-z",
                $"--pretty=format:\"{fullFormat}\"",
                { AppSettings.OrderRevisionByDate, "--date-order", "--topo-order" },
                { AppSettings.ShowReflogReferences, "--reflog" },
                {
                    _refFilterOptions.HasFlag(RefFilterOptions.All),
                    "--all",
                    new ArgumentBuilder
                    {
                        {
                            _refFilterOptions.HasFlag(RefFilterOptions.Branches) && !string.IsNullOrWhiteSpace(_branchFilter) && _branchFilter.IndexOfAny(_shellGlobCharacters) != -1,
                            "--branches=" + _branchFilter
                        },
                        { _refFilterOptions.HasFlag(RefFilterOptions.Remotes), "--remotes" },
                        { _refFilterOptions.HasFlag(RefFilterOptions.Tags), "--tags" },
                    }.ToString()
                },
                { _refFilterOptions.HasFlag(RefFilterOptions.Boundary), "--boundary" },
                { _refFilterOptions.HasFlag(RefFilterOptions.ShowGitNotes), "--not --glob=notes --not" },
                { _refFilterOptions.HasFlag(RefFilterOptions.NoMerges), "--no-merges" },
                { _refFilterOptions.HasFlag(RefFilterOptions.FirstParent), "--first-parent" },
                { _refFilterOptions.HasFlag(RefFilterOptions.SimplifyByDecoration), "--simplify-by-decoration" },
                _revisionFilter,
                "--",
                _pathFilter
            };

            var sw = Stopwatch.StartNew();
            var revisionCount = 0;

            using (var process = _module.RunGitCmdDetached(arguments.ToString(), GitModule.LosslessEncoding))
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                // Pool string values likely to form a small set: encoding, authorname, authoremail, committername, committeremail
                var stringPool = new StringPool();

                var buffer = new byte[4096];

                foreach (var logItemBytes in process.StandardOutput.BaseStream.ReadNullTerminatedChunks(ref buffer))
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    revisionCount++;

                    ProcessLogItem(logItemBytes, stringPool);
                }

                Trace.WriteLine($"**** PROCESSED {revisionCount} ALL REVISIONS IN {sw.Elapsed.TotalMilliseconds:#,##0.#} ms. Pool count {stringPool.Count}");
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);

            if (!token.IsCancellationRequested)
            {
                Exited?.Invoke(this, EventArgs.Empty);
            }
        }

        private IReadOnlyList<IGitRef> GetRefs()
        {
            var refs = _module.GetRefs(true);

            _selectedBranchName = _module.IsValidGitWorkingDir()
                ? _module.GetSelectedBranch()
                : "";

            var selectedRef = refs.FirstOrDefault(head => head.Name == _selectedBranchName);

            if (selectedRef != null)
            {
                selectedRef.Selected = true;

                var localConfigFile = _module.LocalConfigFile;

                var selectedHeadMergeSource = refs.FirstOrDefault(
                    head => head.IsRemote
                         && selectedRef.GetTrackingRemote(localConfigFile) == head.Remote
                         && selectedRef.GetMergeWith(localConfigFile) == head.LocalName);

                if (selectedHeadMergeSource != null)
                {
                    selectedHeadMergeSource.SelectedHeadMergeSource = true;
                }
            }

            return refs;
        }

        private void ProcessLogItem(ArraySegment<byte> logItemBytes, StringPool stringPool)
        {
            if (!ObjectId.TryParseAsciiHexBytes(logItemBytes, 0, out var objectId) ||
                !ObjectId.TryParseAsciiHexBytes(logItemBytes, ObjectId.Sha1CharCount, out var treeId))
            {
                return;
            }

            var parentIds = new List<ObjectId>(capacity: 1);
            var parentIdOffset = ObjectId.Sha1CharCount * 2;

            while (parentIdOffset < logItemBytes.Count - 1)
            {
                var b = logItemBytes.Array[logItemBytes.Offset + parentIdOffset];

                if (b == '\n')
                {
                    parentIdOffset++;
                    break;
                }

                if (b == ' ')
                {
                    parentIdOffset++;
                }

                if (!ObjectId.TryParseAsciiHexBytes(logItemBytes, parentIdOffset, out var parentId))
                {
                    return;
                }

                parentIds.Add(parentId);
                parentIdOffset += ObjectId.Sha1CharCount;
            }

            var s = _module.LogOutputEncoding.GetString(logItemBytes.Array, logItemBytes.Offset + parentIdOffset, logItemBytes.Count - parentIdOffset);

            var match = _commitRegex.Match(s);

            if (!match.Success || match.Index != 0)
            {
                Debug.Fail("Commit regex did not match");
                return;
            }

            var encoding = stringPool.Intern(s, match.Groups[7 /*encoding*/]);

            var revision = new GitRevision(null)
            {
                // TODO are we really sure we can't make Revision.Guid an ObjectId?
                Guid = objectId.ToString(),

                // TODO take IReadOnlyList<ObjectId> instead
                ParentGuids = parentIds.Select(p => p.ToString()).ToArray(),

                TreeGuid = treeId,

                Author = stringPool.Intern(s, match.Groups[1 /*authorname*/]),
                AuthorEmail = stringPool.Intern(s, match.Groups[2 /*authoremail*/]),
                AuthorDate = DateTimeUtils.ParseUnixTime(s, match.Groups[3 /*authordate*/]),
                Committer = stringPool.Intern(s, match.Groups[4 /*committername*/]),
                CommitterEmail = stringPool.Intern(s, match.Groups[5 /*committeremail*/]),
                CommitDate = DateTimeUtils.ParseUnixTime(s, match.Groups[6 /*commitdate*/]),
                MessageEncoding = encoding,
                Subject = _module.ReEncodeCommitMessage(match.Groups[8 /*subject*/].Value, encoding),
                Body = _module.ReEncodeCommitMessage(match.Groups[10 /*body*/].Value, encoding)
            };

            revision.HasMultiLineMessage = !string.IsNullOrWhiteSpace(revision.Body);

            if (_refs.TryGetValue(revision.Guid, out var gitRefs))
            {
                revision.Refs = gitRefs;
            }

            if (_revisionPredicate == null || _revisionPredicate(revision))
            {
                // Remove full commit message to reduce memory consumption (28% for a repo with 69K commits)
                // Full commit message is used in InMemFilter but later it's not needed
                revision.Body = null;

                RevisionCount++;
                Updated?.Invoke(revision);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSequence.Dispose();
        }
    }
}
