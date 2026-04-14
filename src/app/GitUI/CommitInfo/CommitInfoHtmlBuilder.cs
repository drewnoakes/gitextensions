using System.Net;
using System.Text;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI.Theming;
using GitUIPluginInterfaces;
using ResourceManager;
using ResourceManager.CommitDataRenders;

namespace GitUI.CommitInfo;

/// <summary>
///  Builds a complete HTML document for the CommitInfo panel, combining the
///  header (author, dates, hash, parents), commit message body, and footer
///  (branches, tags, links, git-describe) into a single WebView2 page.
/// </summary>
internal sealed class CommitInfoHtmlBuilder
{
    private readonly ILinkFactory _linkFactory;
    private readonly IDateFormatter _dateFormatter;

    public CommitInfoHtmlBuilder(ILinkFactory linkFactory, IDateFormatter dateFormatter)
    {
        _linkFactory = linkFactory;
        _dateFormatter = dateFormatter;
    }

    /// <summary>
    ///  Builds the full HTML document for the CommitInfo panel.
    /// </summary>
    /// <param name="commitData"> The commit metadata (author, dates, hash, parents, children).</param>
    /// <param name="commitMessageBody"> The raw commit message body text (may contain markdown).</param>
    /// <param name="avatarUrl"> Optional data: URI for the author avatar image.</param>
    /// <param name="annotatedTagsInfo"> Pre-formatted annotated tag info (XHTML fragment).</param>
    /// <param name="linksInfo"> Pre-formatted external links (XHTML fragment).</param>
    /// <param name="branchInfo"> Pre-formatted branch info (XHTML fragment).</param>
    /// <param name="tagInfo"> Pre-formatted tag info (XHTML fragment).</param>
    /// <param name="gitDescribeInfo"> Pre-formatted git-describe info (XHTML fragment).</param>
    /// <param name="showRevisionsAsLinks"> Whether commit hashes should be rendered as clickable links.</param>
    /// <param name="renderMarkdown"> Whether to render the body as markdown.</param>
    public string Build(
        CommitData? commitData,
        string commitMessageBody,
        string? avatarUrl,
        string annotatedTagsInfo,
        string linksInfo,
        string branchInfo,
        string tagInfo,
        string gitDescribeInfo,
        bool showRevisionsAsLinks,
        bool renderMarkdown)
    {
        bool isDark = Application.IsDarkModeEnabled;

        Color background = SystemColors.Window;
        Color foreground = SystemColors.WindowText;
        Color mutedFg = isDark
            ? Color.FromArgb(145, 152, 161)
            : Color.FromArgb(101, 109, 118);
        Color borderColor = background.MakeBackgroundDarkerBy(isDark ? -0.15 : 0.15);
        Color linkColor = isDark
            ? Color.FromArgb(68, 147, 248)
            : Color.FromArgb(3, 102, 214);
        Color headerBg = background.MakeBackgroundDarkerBy(isDark ? -0.03 : 0.03);
        Color codeBg = background.MakeBackgroundDarkerBy(isDark ? -0.08 : 0.05);

        StringBuilder sb = new(4096);
        sb.Append($$"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <style>
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body {
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "Noto Sans", Helvetica, Arial, sans-serif;
                font-size: 12px;
                color: {{Css(foreground)}};
                background: {{Css(background)}};
                overflow: hidden;
            }
            a { color: {{Css(linkColor)}}; text-decoration: none; }
            a:hover { text-decoration: underline; }

            /* Header section */
            .header {
                display: flex;
                gap: 10px;
                padding: 8px 12px;
                background: {{Css(headerBg)}};
                border-bottom: 1px solid {{Css(borderColor)}};
            }
            .avatar {
                flex-shrink: 0;
                width: 60px;
                height: 60px;
                border-radius: 6px;
            }
            .header-details { flex: 1; min-width: 0; }
            .header-row {
                display: flex;
                line-height: 1.6;
                white-space: nowrap;
            }
            .header-label {
                color: {{Css(mutedFg)}};
                min-width: 90px;
                flex-shrink: 0;
            }
            .header-value {
                overflow: hidden;
                text-overflow: ellipsis;
            }
            .hash {
                font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace;
                font-size: 11px;
            }

            /* Message section */
            .message {
                padding: 8px 12px;
                line-height: 1.5;
                word-wrap: break-word;
            }
            .message h1, .message h2, .message h3, .message h4, .message h5, .message h6 {
                margin-top: 1em; margin-bottom: 0.5em; font-weight: 600; line-height: 1.25;
            }
            .message h1 { font-size: 1.6em; padding-bottom: 0.3em; border-bottom: 1px solid {{Css(borderColor)}}; }
            .message h2 { font-size: 1.3em; padding-bottom: 0.3em; border-bottom: 1px solid {{Css(borderColor)}}; }
            .message h3 { font-size: 1.15em; }
            .message h4 { font-size: 1em; }
            .message p { margin-bottom: 0.6em; }
            .message ul, .message ol { padding-left: 2em; margin-bottom: 0.6em; }
            .message li + li { margin-top: 0.15em; }
            .message code, .message tt {
                font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace;
                font-size: 85%;
                padding: 0.2em 0.4em;
                background: {{Css(codeBg)}};
                border-radius: 6px;
            }
            .message pre {
                padding: 0.8em 1em;
                overflow: auto;
                font-size: 85%;
                line-height: 1.45;
                background: {{Css(codeBg)}};
                border-radius: 6px;
                margin-bottom: 0.6em;
            }
            .message pre code { padding: 0; background: none; font-size: 100%; }
            .message blockquote {
                padding: 0 1em;
                color: {{Css(mutedFg)}};
                border-left: 0.25em solid {{Css(borderColor)}};
                margin-bottom: 0.6em;
            }
            .message hr { height: 0.25em; background: {{Css(borderColor)}}; border: 0; margin: 1em 0; }
            .message a { color: {{Css(linkColor)}}; }
            .message table { border-collapse: collapse; margin-bottom: 0.6em; }
            .message th, .message td { border: 1px solid {{Css(borderColor)}}; padding: 4px 8px; }
            .message img { max-width: 100%; }

            /* Footer section */
            .footer {
                padding: 6px 12px;
                border-top: 1px solid {{Css(borderColor)}};
                color: {{Css(mutedFg)}};
                font-size: 11.5px;
                line-height: 1.5;
            }
            .footer a { color: {{Css(linkColor)}}; }
            .footer-section { margin-bottom: 0.5em; }
            .footer-section:last-child { margin-bottom: 0; }
            .footer-label { color: {{Css(foreground)}}; font-weight: 600; }
            .footer u { text-decoration: none; font-weight: 600; color: {{Css(foreground)}}; }

            /* Wheel forwarding script */
            </style>
            <script>
            document.addEventListener('wheel', function(e) {
                e.preventDefault();
                window.chrome.webview.postMessage(JSON.stringify({ type: 'wheel', delta: Math.round(e.deltaY) }));
            }, { passive: false });
            document.addEventListener('click', function(e) {
                var a = e.target.closest('a');
                if (a && a.href) {
                    e.preventDefault();
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'link', url: a.href }));
                }
            });
            </script>
            </head>
            <body>
            """);

        // Header
        if (commitData is not null)
        {
            BuildHeader(sb, commitData, avatarUrl, showRevisionsAsLinks);
        }

        // Message body
        if (!string.IsNullOrWhiteSpace(commitMessageBody))
        {
            sb.Append("<div class=\"message\">");

            if (renderMarkdown)
            {
                string markdown = commitMessageBody;
                if (markdown.Length > 0 && markdown[0] == '\uFEFF')
                {
                    markdown = markdown[1..];
                }

                sb.Append(Markdig.Markdown.ToHtml(markdown, Editor.MarkdownToHtmlConverter.Pipeline));
            }
            else
            {
                sb.Append("<pre>").Append(WebUtility.HtmlEncode(commitMessageBody)).Append("</pre>");
            }

            sb.Append("</div>");
        }

        // Footer
        string footerContent = BuildFooter(annotatedTagsInfo, linksInfo, branchInfo, tagInfo, gitDescribeInfo);
        if (footerContent.Length > 0)
        {
            sb.Append("<div class=\"footer\">").Append(footerContent).Append("</div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private void BuildHeader(StringBuilder sb, CommitData commitData, string? avatarUrl, bool showRevisionsAsLinks)
    {
        bool isArtificial = commitData.ObjectId.IsArtificial;
        bool authorIsCommitter = string.Equals(commitData.Author, commitData.Committer, StringComparison.CurrentCulture);
        bool datesEqual = commitData.AuthorDate.EqualsExact(commitData.CommitDate);
        string authorEmail = GetEmail(commitData.Author);

        sb.Append("<div class=\"header\">");

        if (!string.IsNullOrEmpty(avatarUrl))
        {
            sb.Append("<img class=\"avatar\" src=\"").Append(avatarUrl).Append("\" />");
        }

        sb.Append("<div class=\"header-details\">");

        // Author
        AppendHeaderRow(sb, ResourceManager.TranslatedStrings.Author,
            $"<a href=\"mailto:{WebUtility.HtmlEncode(authorEmail)}\">{WebUtility.HtmlEncode(commitData.Author)}</a>");

        // Date(s)
        if (!isArtificial)
        {
            string dateLabel = datesEqual ? ResourceManager.TranslatedStrings.Date : ResourceManager.TranslatedStrings.AuthorDate;
            AppendHeaderRow(sb, dateLabel, WebUtility.HtmlEncode(_dateFormatter.FormatDateAsRelativeLocal(commitData.AuthorDate)));
        }

        // Committer (if different)
        if (!authorIsCommitter)
        {
            string committerEmail = GetEmail(commitData.Committer);
            AppendHeaderRow(sb, ResourceManager.TranslatedStrings.Committer,
                $"<a href=\"mailto:{WebUtility.HtmlEncode(committerEmail)}\">{WebUtility.HtmlEncode(commitData.Committer)}</a>");

            if (!isArtificial && !datesEqual)
            {
                AppendHeaderRow(sb, ResourceManager.TranslatedStrings.CommitDate,
                    WebUtility.HtmlEncode(_dateFormatter.FormatDateAsRelativeLocal(commitData.CommitDate)));
            }
        }

        // Commit hash
        if (!isArtificial)
        {
            AppendHeaderRow(sb, ResourceManager.TranslatedStrings.CommitHash,
                $"<span class=\"hash\">{WebUtility.HtmlEncode(commitData.ObjectId.ToString())}</span>");
        }

        // Children
        if (commitData.ChildIds is { Count: > 0 })
        {
            AppendHeaderRow(sb, ResourceManager.TranslatedStrings.GetChildren(commitData.ChildIds.Count),
                RenderObjectIds(commitData.ChildIds, showRevisionsAsLinks));
        }

        // Parents
        if (commitData.ParentIds is { Count: > 0 })
        {
            AppendHeaderRow(sb, ResourceManager.TranslatedStrings.GetParents(commitData.ParentIds.Count),
                RenderObjectIds(commitData.ParentIds, showRevisionsAsLinks));
        }

        sb.Append("</div></div>");
    }

    private static void AppendHeaderRow(StringBuilder sb, string label, string value)
    {
        sb.Append("<div class=\"header-row\"><span class=\"header-label\">")
          .Append(WebUtility.HtmlEncode(label))
          .Append(":</span><span class=\"header-value\">")
          .Append(value)
          .Append("</span></div>");
    }

    private string RenderObjectIds(IReadOnlyList<ObjectId> objectIds, bool showAsLinks)
    {
        if (showAsLinks)
        {
            return string.Join(" ", objectIds.Select(id =>
                $"<a href=\"gitext://gotocommit/{id}\"><span class=\"hash\">{WebUtility.HtmlEncode(id.ToShortString())}</span></a>"));
        }

        return string.Join(" ", objectIds.Select(id =>
            $"<span class=\"hash\">{WebUtility.HtmlEncode(id.ToShortString())}</span>"));
    }

    private static string BuildFooter(
        string annotatedTagsInfo,
        string linksInfo,
        string branchInfo,
        string tagInfo,
        string gitDescribeInfo)
    {
        StringBuilder sb = new();

        // The footer sections arrive as pre-formatted XHTML fragments from
        // RefsFormatter et al. Convert the simple XHTML to HTML by replacing
        // newlines with <br> and preserving the existing <a>, <u> tags.
        foreach (string section in new[] { annotatedTagsInfo, linksInfo, branchInfo, tagInfo, gitDescribeInfo })
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                continue;
            }

            sb.Append("<div class=\"footer-section\">")
              .Append(section.Replace("\r\n", "<br>").Replace("\n", "<br>"))
              .Append("</div>");
        }

        return sb.ToString();
    }

    private static string GetEmail(string? author)
    {
        if (string.IsNullOrEmpty(author))
        {
            return string.Empty;
        }

        int start = author.IndexOf('<');
        if (start == -1)
        {
            return string.Empty;
        }

        return author[(start + 1)..author.LastIndexOf('>')];
    }

    private static string Css(Color c) => $"rgb({c.R},{c.G},{c.B})";
}
