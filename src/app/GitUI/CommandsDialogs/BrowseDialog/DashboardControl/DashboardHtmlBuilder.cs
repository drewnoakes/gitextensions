using System.Net;
using System.Text;
using GitCommands.UserRepositoryHistory;
using GitExtUtils.GitUI.Theming;

namespace GitUI.CommandsDialogs.BrowseDialog.DashboardControl;

/// <summary>
///  Builds HTML for the WebView2-based dashboard repository list,
///  reusing the same visual language as the CommitInfo panel.
/// </summary>
internal static class DashboardHtmlBuilder
{
    public static string Build(
        IReadOnlyList<RecentRepoInfo> recentRepositories,
        IReadOnlyList<RecentRepoInfo> favouriteRepositories,
        Func<string, string?> getBranchName,
        Color? themeBackground = null,
        Color? themeForeground = null)
    {
        bool isDark = Application.IsDarkModeEnabled;

        Color background = themeBackground ?? SystemColors.Window;
        Color foreground = themeForeground ?? SystemColors.WindowText;
        Color mutedFg = isDark
            ? Color.FromArgb(145, 152, 161)
            : Color.FromArgb(101, 109, 118);
        Color borderColor = background.MakeBackgroundDarkerBy(isDark ? -0.10 : 0.10);
        Color linkColor = isDark
            ? Color.FromArgb(88, 166, 255)
            : Color.FromArgb(3, 102, 214);
        Color hoverBg = background.MakeBackgroundDarkerBy(isDark ? -0.06 : 0.04);
        Color branchColor = isDark
            ? Color.FromArgb(110, 180, 255)
            : Color.FromArgb(50, 120, 200);
        Color categoryColor = isDark
            ? Color.FromArgb(200, 170, 80)
            : Color.FromArgb(160, 120, 20);

        StringBuilder sb = new(8192);
        sb.Append($$"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <style>
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body {
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "Noto Sans", Helvetica, Arial, sans-serif;
                font-size: 13px;
                color: {{Css(foreground)}};
                background: {{Css(background)}};
                overflow-y: auto;
            }
            ::-webkit-scrollbar { width: 14px; }
            ::-webkit-scrollbar-track { background: {{Css(background)}}; }
            ::-webkit-scrollbar-thumb {
                background: {{Css(isDark ? Color.FromArgb(80, 80, 80) : Color.FromArgb(190, 190, 190))}};
                border-radius: 7px;
                border: 3px solid {{Css(background)}};
            }
            ::-webkit-scrollbar-thumb:hover {
                background: {{Css(isDark ? Color.FromArgb(110, 110, 110) : Color.FromArgb(160, 160, 160))}};
            }
            a { color: {{Css(linkColor)}}; text-decoration: none; }
            a:hover { text-decoration: underline; }

            #search-box {
                display: block;
                width: 100%;
                padding: 8px 12px;
                font-size: 13px;
                color: {{Css(foreground)}};
                background: {{Css(background)}};
                border: 1px solid {{Css(borderColor)}};
                border-radius: 6px;
                outline: none;
                margin-bottom: 8px;
            }
            #search-box:focus {
                border-color: {{Css(linkColor)}};
            }

            .group-header {
                font-size: 11px;
                font-weight: 600;
                text-transform: uppercase;
                letter-spacing: 0.5px;
                color: {{Css(mutedFg)}};
                padding: 12px 12px 4px 12px;
            }

            .repo-item {
                display: flex;
                align-items: center;
                padding: 6px 12px;
                border-radius: 6px;
                cursor: pointer;
                gap: 10px;
                text-decoration: none;
                color: inherit;
            }
            .repo-item:hover {
                background: {{Css(hoverBg)}};
                text-decoration: none;
            }

            .repo-icon {
                flex-shrink: 0;
                width: 20px;
                height: 20px;
                display: flex;
                align-items: center;
                justify-content: center;
            }
            .repo-icon svg { width: 16px; height: 16px; fill: {{Css(mutedFg)}}; }

            .repo-details {
                flex: 1;
                min-width: 0;
                display: flex;
                align-items: baseline;
                gap: 6px;
            }
            .repo-name {
                font-weight: 600;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
                flex-shrink: 0;
            }
            .repo-path {
                font-size: 11px;
                color: {{Css(mutedFg)}};
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
                flex-shrink: 1;
            }

            .repo-branch {
                flex-shrink: 0;
                font-size: 11px;
                color: {{Css(branchColor)}};
                padding: 1px 6px;
                border: 1px solid {{Css(borderColor)}};
                border-radius: 10px;
                white-space: nowrap;
            }

            .repo-category {
                flex-shrink: 0;
                font-size: 10px;
                color: {{Css(categoryColor)}};
                white-space: nowrap;
            }
            </style>
            </head>
            <body>
            <input type="text" id="search-box" placeholder="Search repositories..." oninput="filterRepos(this.value)" autofocus />
            <div id="repo-list">
            """);

        if (favouriteRepositories.Count > 0)
        {
            sb.Append("<div class=\"group-header\">Favourites</div>");
            AppendRepoItems(sb, favouriteRepositories, getBranchName);
        }

        if (recentRepositories.Count > 0)
        {
            sb.Append("<div class=\"group-header\">Recent</div>");
            AppendRepoItems(sb, recentRepositories, getBranchName);
        }

        sb.Append($$"""
            </div>
            <script>
            function openRepo(path) {
                window.chrome.webview.postMessage(JSON.stringify({ type: 'open-repo', path: path }));
            }
            function filterRepos(query) {
                query = query.toLowerCase();
                document.querySelectorAll('.repo-item').forEach(item => {
                    let text = item.dataset.searchtext;
                    item.style.display = text.includes(query) ? '' : 'none';
                });
                document.querySelectorAll('.group-header').forEach(header => {
                    let next = header.nextElementSibling;
                    let hasVisible = false;
                    while (next && !next.classList.contains('group-header')) {
                        if (next.style.display !== 'none') hasVisible = true;
                        next = next.nextElementSibling;
                    }
                    header.style.display = hasVisible ? '' : 'none';
                });
            }
            document.getElementById('search-box').addEventListener('keydown', function(e) {
                if (e.key === 'Enter') {
                    let visible = document.querySelector('.repo-item:not([style*="display: none"])');
                    if (visible) visible.click();
                }
            });
            </script>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    private static void AppendRepoItems(
        StringBuilder sb,
        IReadOnlyList<RecentRepoInfo> repos,
        Func<string, string?> getBranchName)
    {
        const string repoSvg = """<svg viewBox="0 0 16 16"><path d="M2 2.5A2.5 2.5 0 014.5 0h8.75a.75.75 0 01.75.75v12.5a.75.75 0 01-.75.75h-2.5a.75.75 0 110-1.5h1.75v-2h-8a1 1 0 00-.714 1.7.75.75 0 01-1.072 1.05A2.495 2.495 0 012 11.5v-9zm10.5-1h-8a1 1 0 00-1 1v6.708A2.486 2.486 0 014.5 9h8.5V1.5zM5 12.25v3.25a.25.25 0 00.4.2l1.45-1.087a.25.25 0 01.3 0L8.6 15.7a.25.25 0 00.4-.2v-3.25a.25.25 0 00-.25-.25h-3.5a.25.25 0 00-.25.25z"/></svg>""";

        foreach (RecentRepoInfo repo in repos)
        {
            string path = repo.Repo.Path;
            string name = repo.ShortName ?? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string parentDir = repo.DirName;
            string escapedPath = path.Replace("\\", "\\\\").Replace("'", "\\'");
            string branch = getBranchName(path) ?? "";
            string category = repo.Repo.Category ?? "";
            string searchText = WebUtility.HtmlEncode($"{name} {path} {branch} {category}".ToLowerInvariant());

            sb.Append($"""<a class="repo-item" data-searchtext="{searchText}" onclick="openRepo('{escapedPath}')" title="{WebUtility.HtmlEncode(path)}">""");
            sb.Append($"""<span class="repo-icon">{repoSvg}</span>""");
            sb.Append($"""<span class="repo-details"><span class="repo-name">{WebUtility.HtmlEncode(name)}</span>""");

            if (!string.IsNullOrEmpty(parentDir))
            {
                sb.Append($"""<span class="repo-path">{WebUtility.HtmlEncode(parentDir)}</span>""");
            }

            sb.Append("</span>");

            if (!string.IsNullOrEmpty(branch))
            {
                sb.Append($"""<span class="repo-branch">{WebUtility.HtmlEncode(branch)}</span>""");
            }

            if (!string.IsNullOrEmpty(category))
            {
                sb.Append($"""<span class="repo-category">★ {WebUtility.HtmlEncode(category)}</span>""");
            }

            sb.Append("</a>");
        }
    }

    private static string Css(Color c) => $"rgb({c.R},{c.G},{c.B})";
}
