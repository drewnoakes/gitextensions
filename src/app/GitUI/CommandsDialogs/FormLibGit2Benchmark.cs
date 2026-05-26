using System.Diagnostics;
using System.Text;
using GitCommands;
using GitExtensions.Extensibility.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.Threading;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Benchmarks git.exe process spawning vs LibGit2Sharp in-process API
///  for various common git operations. This form is temporary and intended
///  for evaluating whether LibGit2Sharp could improve performance in Git Extensions.
/// </summary>
internal sealed partial class FormLibGit2Benchmark : Form
{
    private const int DefaultIterations = 10;

    private readonly IGitModule _module;
    private readonly DataGridView _resultsGrid;
    private readonly NumericUpDown _nudIterations;
    private readonly Button _btnRun;
    private readonly Button _btnCopy;
    private readonly Label _lblStatus;
    private readonly ProgressBar _progressBar;

    public FormLibGit2Benchmark(IGitModule module)
    {
        _module = module;

        Text = "LibGit2Sharp Benchmark — git.exe vs in-process API";
        Size = new Size(900, 520);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(700, 400);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        FlowLayoutPanel topPanel = new()
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
        };

        Label lblIterations = new()
        {
            Text = "Iterations:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 4, 0),
        };

        _nudIterations = new()
        {
            Minimum = 1,
            Maximum = 100,
            Value = DefaultIterations,
            Width = 60,
        };

        _btnRun = new()
        {
            Text = "Run Benchmark",
            AutoSize = true,
            Margin = new Padding(12, 0, 0, 0),
        };
        _btnRun.Click += RunBenchmarkButton_Click;

        _btnCopy = new()
        {
            Text = "Copy Results",
            AutoSize = true,
            Enabled = false,
            Margin = new Padding(6, 0, 0, 0),
        };
        _btnCopy.Click += (_, _) => CopyResults();

        topPanel.Controls.AddRange([lblIterations, _nudIterations, _btnRun, _btnCopy]);

        _resultsGrid = new()
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = SystemColors.Window,
        };

        _resultsGrid.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { Name = "Operation", HeaderText = "Operation", Width = 200, SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { Name = "GitExeAvg", HeaderText = "git.exe avg (ms)", Width = 120, DefaultCellStyle = new() { Alignment = DataGridViewContentAlignment.MiddleRight }, SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { Name = "LibGit2Avg", HeaderText = "LibGit2Sharp avg (ms)", Width = 140, DefaultCellStyle = new() { Alignment = DataGridViewContentAlignment.MiddleRight }, SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { Name = "Speedup", HeaderText = "Speedup", Width = 100, DefaultCellStyle = new() { Alignment = DataGridViewContentAlignment.MiddleRight }, SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { Name = "ResultMatch", HeaderText = "Results Match?", Width = 110, SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable },
        ]);

        FlowLayoutPanel bottomPanel = new()
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
        };

        _progressBar = new()
        {
            Width = 300,
            Height = 20,
            Margin = new Padding(0, 4, 12, 0),
        };

        _lblStatus = new()
        {
            Text = "Ready. Click 'Run Benchmark' to start.",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 0, 0),
        };

        bottomPanel.Controls.AddRange([_progressBar, _lblStatus]);

        layout.Controls.Add(topPanel, 0, 0);
        layout.Controls.Add(_resultsGrid, 0, 1);
        layout.Controls.Add(bottomPanel, 0, 2);
        Controls.Add(layout);
    }

    private void RunBenchmarkButton_Click(object? sender, EventArgs e)
    {
        ThreadHelper.JoinableTaskFactory.RunAsync(RunBenchmarkAsync).FileAndForget();
    }

    private async Task RunBenchmarkAsync()
    {
        _btnRun.Enabled = false;
        _btnCopy.Enabled = false;
        _resultsGrid.Rows.Clear();

        int iterations = (int)_nudIterations.Value;
        string workingDir = _module.WorkingDir;

        BenchmarkOperation[] operations =
        [
            new("rev-parse HEAD", "Resolve HEAD to a commit SHA",
                () => RunGitExe("rev-parse HEAD"),
                repo => repo.Head.Tip?.Sha ?? ""),

            new("branch --list", "List all local branch names",
                () =>
                {
                    string output = RunGitExe("branch --list --no-color");
                    return string.Join(",", output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(b => b.TrimStart('*', ' ')).Order());
                },
                repo =>
                {
                    return string.Join(",", repo.Branches
                        .Where(b => !b.IsRemote)
                        .Select(b => b.FriendlyName).Order());
                }),

            new("tag --list", "List all tag names",
                () =>
                {
                    string output = RunGitExe("tag --list");
                    return string.Join(",", output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Order());
                },
                repo =>
                {
                    return string.Join(",", repo.Tags
                        .Select(t => t.FriendlyName).Order());
                }),

            new("log -100 --format=%H", "Get last 100 commit SHAs",
                () => RunGitExe("log -100 --format=%H"),
                repo =>
                {
                    StringBuilder sb = new();
                    int count = 0;
                    foreach (Commit commit in repo.Commits)
                    {
                        if (count++ >= 100)
                        {
                            break;
                        }

                        sb.AppendLine(commit.Sha);
                    }

                    return sb.ToString().TrimEnd();
                }),

            new("log -500 --format=%H", "Get last 500 commit SHAs",
                () => RunGitExe("log -500 --format=%H"),
                repo =>
                {
                    StringBuilder sb = new();
                    int count = 0;
                    foreach (Commit commit in repo.Commits)
                    {
                        if (count++ >= 500)
                        {
                            break;
                        }

                        sb.AppendLine(commit.Sha);
                    }

                    return sb.ToString().TrimEnd();
                }),

            new("log -100 (full metadata)", "Get last 100 commits with author/date/message",
                () => RunGitExe("log -100 --format=%H%n%aN%n%aI%n%s"),
                repo =>
                {
                    StringBuilder sb = new();
                    int count = 0;
                    foreach (Commit commit in repo.Commits)
                    {
                        if (count++ >= 100)
                        {
                            break;
                        }

                        sb.AppendLine(commit.Sha);
                        sb.AppendLine(commit.Author.Name);
                        sb.AppendLine(commit.Author.When.ToString("o"));
                        sb.AppendLine(commit.MessageShort);
                    }

                    return sb.ToString().TrimEnd();
                }),

            new("status --porcelain", "Get working directory status",
                () => RunGitExe("status --porcelain -uall"),
                repo =>
                {
                    RepositoryStatus status = repo.RetrieveStatus(new StatusOptions { IncludeUntracked = true });
                    StringBuilder sb = new();
                    foreach (StatusEntry entry in status.OrderBy(e => e.FilePath))
                    {
                        sb.AppendLine($"{entry.State} {entry.FilePath}");
                    }

                    return sb.ToString().TrimEnd();
                }),

            new("rev-list --count HEAD", "Count all commits reachable from HEAD",
                () => RunGitExe("rev-list --count HEAD"),
                repo =>
                {
                    int count = 0;
                    foreach (Commit commit in repo.Commits)
                    {
                        _ = commit;
                        count++;
                    }

                    return count.ToString();
                }),

            new("for-each-ref (all refs)", "Enumerate all refs (branches + tags + remotes)",
                () => RunGitExe("for-each-ref --format=%(refname)"),
                repo =>
                {
                    StringBuilder sb = new();
                    foreach (Reference r in repo.Refs.OrderBy(r => r.CanonicalName))
                    {
                        sb.AppendLine(r.CanonicalName);
                    }

                    return sb.ToString().TrimEnd();
                }),

            new("show HEAD:README.md (read blob)", "Read a file from HEAD commit",
                () =>
                {
                    // Find first file in HEAD tree to read
                    string? fileName = FindFirstFile(workingDir);
                    return fileName is not null ? RunGitExe($"show HEAD:{fileName}") : "(no files)";
                },
                repo =>
                {
                    string? fileName = FindFirstFile(workingDir);
                    if (fileName is null)
                    {
                        return "(no files)";
                    }

                    TreeEntry? entry = repo.Head.Tip?.Tree[fileName];
                    if (entry?.Target is Blob blob)
                    {
                        return blob.GetContentText();
                    }

                    return "(not found)";
                }),
        ];

        _progressBar.Maximum = operations.Length * 2;
        _progressBar.Value = 0;

        // LibGit2Sharp 0.31 doesn't support extensions.relativeworktrees.
        // Temporarily unset it so we can open the repo for benchmarking.
        bool removedRelativeWorktrees = await Task.Run(() => TryRemoveUnsupportedExtensions(workingDir));

        try
        {
            foreach (BenchmarkOperation op in operations)
            {
                _lblStatus.Text = $"Benchmarking: {op.Name} (git.exe)...";

                BenchmarkResult gitResult = await Task.Run(() => MeasureOperation(op.GitExeAction, iterations));
                _progressBar.Value++;

                _lblStatus.Text = $"Benchmarking: {op.Name} (LibGit2Sharp)...";

                BenchmarkResult lib2Result = await Task.Run(() =>
                {
                    using Repository repo = new(workingDir);
                    return MeasureOperation(() => op.LibGit2Action(repo), iterations);
                });
                _progressBar.Value++;

                double speedup = gitResult.AverageMs > 0
                    ? gitResult.AverageMs / lib2Result.AverageMs
                    : 0;

                bool resultsMatch = NormalizeForComparison(gitResult.LastResult) == NormalizeForComparison(lib2Result.LastResult);

                _resultsGrid.Rows.Add(
                    op.Name,
                    gitResult.AverageMs.ToString("F2"),
                    lib2Result.AverageMs.ToString("F2"),
                    speedup.ToString("F2") + "x",
                    resultsMatch ? "✓ Yes" : "✗ No",
                    op.Description);
            }
        }
        finally
        {
            if (removedRelativeWorktrees)
            {
                await Task.Run(() => RestoreUnsupportedExtensions(workingDir));
            }
        }

        // Color code speedup column
        foreach (DataGridViewRow row in _resultsGrid.Rows)
        {
            string speedupText = (row.Cells["Speedup"].Value?.ToString() ?? "").Replace("x", "");
            if (double.TryParse(speedupText, out double s))
            {
                row.Cells["Speedup"].Style.ForeColor = s >= 1.0 ? Color.Green : Color.Red;
                row.Cells["LibGit2Avg"].Style.ForeColor = s >= 1.0 ? Color.Green : Color.Red;
            }

            string match = row.Cells["ResultMatch"].Value?.ToString() ?? "";
            row.Cells["ResultMatch"].Style.ForeColor = match.StartsWith('✓') ? Color.Green : Color.Red;
        }

        _lblStatus.Text = $"Complete. {operations.Length} operations benchmarked with {iterations} iterations each.";
        _btnRun.Enabled = true;
        _btnCopy.Enabled = true;

        return;

        string RunGitExe(string arguments)
        {
            ProcessStartInfo psi = new()
            {
                FileName = AppSettings.GitCommand,
                Arguments = arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using Process process = Process.Start(psi)!;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.TrimEnd();
        }

        static string? FindFirstFile(string workingDir)
        {
            foreach (string candidate in new[] { "README.md", "README", "LICENSE", ".gitignore" })
            {
                if (File.Exists(Path.Combine(workingDir, candidate)))
                {
                    return candidate;
                }
            }

            // Fall back to any file
            string? firstFile = Directory.EnumerateFiles(workingDir).FirstOrDefault();
            return firstFile is not null ? Path.GetFileName(firstFile) : null;
        }
    }

    private static BenchmarkResult MeasureOperation(Func<string> action, int iterations)
    {
        // Warm up
        string result = action();

        Stopwatch sw = new();
        long totalMs = 0;

        for (int i = 0; i < iterations; i++)
        {
            sw.Restart();
            result = action();
            sw.Stop();
            totalMs += sw.ElapsedMilliseconds;
        }

        return new BenchmarkResult(totalMs / (double)iterations, result);
    }

    private static string NormalizeForComparison(string value)
    {
        // Normalize line endings and trim for comparison
        return value.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
    }

    private void CopyResults()
    {
        StringBuilder sb = new();
        sb.AppendLine($"LibGit2Sharp Benchmark Results — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Repository: {_module.WorkingDir}");
        sb.AppendLine($"Iterations: {_nudIterations.Value}");
        sb.AppendLine();

        const string headerFormat = "{0,-35} {1,14} {2,14} {3,10} {4,8}";
        sb.AppendLine(string.Format(headerFormat, "Operation", "git.exe (ms)", "LibGit2# (ms)", "Speedup", "Match"));
        sb.AppendLine(new string('-', 85));

        foreach (DataGridViewRow row in _resultsGrid.Rows)
        {
            sb.AppendLine(string.Format(
                headerFormat,
                row.Cells["Operation"].Value,
                row.Cells["GitExeAvg"].Value,
                row.Cells["LibGit2Avg"].Value,
                row.Cells["Speedup"].Value,
                row.Cells["ResultMatch"].Value));
        }

        Clipboard.SetText(sb.ToString());
        _lblStatus.Text = "Results copied to clipboard.";
    }

    private sealed record BenchmarkOperation(
        string Name,
        string Description,
        Func<string> GitExeAction,
        Func<Repository, string> LibGit2Action);

    private sealed record BenchmarkResult(double AverageMs, string LastResult);

    /// <summary>
    ///  LibGit2Sharp 0.31 does not support extensions.relativeworktrees.
    ///  Temporarily unset it so the repo can be opened for benchmarking.
    /// </summary>
    private static bool TryRemoveUnsupportedExtensions(string workingDir)
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = AppSettings.GitCommand,
                Arguments = "config --local --get extensions.relativeworktrees",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            using Process process = Process.Start(psi)!;
            string value = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode != 0 || string.IsNullOrEmpty(value))
            {
                return false;
            }

            ProcessStartInfo unsetPsi = new()
            {
                FileName = AppSettings.GitCommand,
                Arguments = "config --local --unset extensions.relativeworktrees",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using Process unsetProcess = Process.Start(unsetPsi)!;
            unsetProcess.WaitForExit();
            return unsetProcess.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void RestoreUnsupportedExtensions(string workingDir)
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = AppSettings.GitCommand,
                Arguments = "config --local extensions.relativeworktrees true",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using Process process = Process.Start(psi)!;
            process.WaitForExit();
        }
        catch
        {
            // Best effort restore
        }
    }
}
