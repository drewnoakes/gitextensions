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

    private const string GitHubSvgIcon = "<svg class=\"link-icon\" viewBox=\"0 0 16 16\" fill=\"currentColor\"><path d=\"M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z\"/></svg>";

    private const string CopilotIcon = "\U0001F916";

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
        bool renderMarkdown,
        string? remoteUrl = null)
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
                width: 90px;
                height: 90px;
                border-radius: 6px;
                background: {{Css(headerBg)}};
            }
            .inline-avatar {
                width: 16px;
                height: 16px;
                border-radius: 3px;
                vertical-align: text-bottom;
            }
            .header-details { flex: 1; min-width: 0; }
            .header-row {
                display: grid;
                grid-template-columns: 90px 24px 1fr;
                line-height: 1.6;
                white-space: nowrap;
                align-items: baseline;
            }
            .header-label {
                color: {{Css(foreground)}};
                font-weight: 600;
            }
            .header-icon {
                text-align: center;
                line-height: 1.6;
            }
            .header-value {
                overflow: hidden;
                text-overflow: ellipsis;
            }
            .date-relative {
                color: {{Css(mutedFg)}};
                margin-left: 0.5em;
            }
            .hash {
                font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace;
                font-size: 11px;
            }
            .hash-row {
                display: inline-flex;
                align-items: center;
                min-width: 200px;
            }
            .copy-btn {
                visibility: hidden;
                cursor: pointer;
                margin-left: 3px;
                color: {{Css(mutedFg)}};
                font-size: 11px;
                line-height: 1;
                user-select: none;
            }
            .copy-btn:hover { color: {{Css(foreground)}}; }
            .hash-row:hover .copy-btn { visibility: visible; }
            .copied-feedback {
                color: {{Css(mutedFg)}};
                font-size: 11px;
                margin-left: 6px;
            }
            .author-email {
                color: {{Css(mutedFg)}};
                text-decoration: none;
                margin-left: 0.5em;
            }
            .author-email:hover { cursor: pointer; text-decoration: underline; }

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
            .link-icon {
                width: 14px;
                height: 14px;
                vertical-align: text-bottom;
                margin-right: 3px;
                opacity: 0.7;
            }
            .footer-label { color: {{Css(foreground)}}; font-weight: 600; }
            .footer u { text-decoration: none; font-weight: 600; color: {{Css(foreground)}}; }

            /* Tooltip for links */
            a[title]:hover { cursor: pointer; }
            </style>
            <script>
            function copyId(btn, fullHash) {
                event.stopPropagation();
                window.chrome.webview.postMessage(JSON.stringify({ type: 'copy', text: fullHash }));
                var orig = btn.innerHTML;
                btn.innerHTML = 'Copied';
                btn.classList.add('copied-feedback');
                btn.classList.remove('copy-btn');
                setTimeout(function() {
                    btn.innerHTML = orig;
                    btn.classList.remove('copied-feedback');
                    btn.classList.add('copy-btn');
                }, 1200);
            }
            document.addEventListener('click', function(e) {
                if (e.target.closest('.copy-btn') || e.target.closest('.copied-feedback')) return;
                var a = e.target.closest('a');
                if (a && a.href) {
                    e.preventDefault();
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'link', url: a.href }));
                } else {
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'dismiss' }));
                }
            });
            document.addEventListener('contextmenu', function(e) {
                e.preventDefault();
                var hasSelection = window.getSelection().toString().length > 0;
                window.chrome.webview.postMessage(JSON.stringify({ type: 'contextmenu', hasSelection: hasSelection }));
            });
            </script>
            </head>
            <body>
            """);

        // Header
        sb.Append("<div id=\"header\" class=\"header\">");
        if (commitData is not null)
        {
            sb.Append(BuildHeaderInner(commitData, avatarUrl, showRevisionsAsLinks, commitMessageBody, remoteUrl));
        }

        sb.Append("</div>");

        // Message body
        sb.Append("<div id=\"message\" class=\"message\">");
        if (!string.IsNullOrWhiteSpace(commitMessageBody))
        {
            sb.Append(BuildMessageInner(commitMessageBody, renderMarkdown));
        }

        sb.Append("</div>");

        // Footer
        sb.Append("<div id=\"footer\" class=\"footer\">");
        string footerContent = BuildFooter(annotatedTagsInfo, linksInfo, branchInfo, tagInfo, gitDescribeInfo);
        sb.Append(footerContent);
        sb.Append("</div>");

        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>
    ///  Builds the inner HTML for the header section (for incremental updates).
    /// </summary>
    public string BuildHeaderInner(CommitData commitData, string? avatarUrl, bool showRevisionsAsLinks, string? commitBody = null, string? remoteUrl = null)
    {
        bool isArtificial = commitData.ObjectId.IsArtificial;
        bool authorIsCommitter = string.Equals(commitData.Author, commitData.Committer, StringComparison.CurrentCulture);
        bool datesEqual = commitData.AuthorDate.EqualsExact(commitData.CommitDate);
        string authorEmail = GetEmail(commitData.Author);

        StringBuilder sb = new();

        if (!string.IsNullOrEmpty(avatarUrl))
        {
            sb.Append("<img class=\"avatar\" src=\"").Append(avatarUrl).Append("\" />");
        }

        sb.Append("<div class=\"header-details\">");

        // --- People section ---
        AppendHeaderRow(sb, ResourceManager.TranslatedStrings.Author, FormatPersonHtml(commitData.Author));

        (IReadOnlyList<string> additionalAuthors, IReadOnlyList<string> signedOffBy) = ExtractTrailers(commitBody);

        foreach (string additionalAuthor in additionalAuthors)
        {
            string email = GetEmail(additionalAuthor);
            string avatarImg = GetPersonIcon(email);
            AppendHeaderRow(sb, "Co-author", FormatPersonHtml(additionalAuthor), avatarImg);
        }

        if (!authorIsCommitter)
        {
            AppendHeaderRow(sb, ResourceManager.TranslatedStrings.Committer, FormatPersonHtml(commitData.Committer));
        }

        foreach (string signer in signedOffBy)
        {
            string email = GetEmail(signer);
            string avatarImg = GetPersonIcon(email);
            AppendHeaderRow(sb, "Signed-off-by", FormatPersonHtml(signer), avatarImg);
        }

        // --- Dates section ---
        if (!isArtificial)
        {
            string dateLabel = datesEqual ? ResourceManager.TranslatedStrings.Date : ResourceManager.TranslatedStrings.AuthorDate;
            AppendHeaderRow(sb, dateLabel, FormatDateHtml(commitData.AuthorDate));

            if (!authorIsCommitter && !datesEqual)
            {
                AppendHeaderRow(sb, ResourceManager.TranslatedStrings.CommitDate, FormatDateHtml(commitData.CommitDate));
            }
        }

        // --- Metadata section ---

        if (!isArtificial)
        {
            string hashShort = commitData.ObjectId.ToShortString();
            string hashFull = commitData.ObjectId.ToString();
            string? gitHubBaseUrl = GetGitHubBaseUrl(remoteUrl);

            string hashHtml;
            string iconHtml = "";
            if (gitHubBaseUrl is not null)
            {
                // Hash is a link to GitHub, with GitHub icon in the icon column
                iconHtml = GitHubSvgIcon;
                hashHtml = $"<span class=\"hash-row\"><a href=\"{gitHubBaseUrl}/commit/{hashFull}\" title=\"View commit on GitHub\"><span class=\"hash\">{WebUtility.HtmlEncode(hashShort)}</span></a>" +
                    $"<span class=\"copy-btn\" onclick=\"copyId(this, '{hashFull}')\" title=\"Copy full commit ID\">&#x1F4CB;</span></span>";
            }
            else
            {
                hashHtml = $"<span class=\"hash-row\"><span class=\"hash\">{WebUtility.HtmlEncode(hashShort)}</span>" +
                    $"<span class=\"copy-btn\" onclick=\"copyId(this, '{hashFull}')\" title=\"Copy full commit ID\">&#x1F4CB;</span></span>";
            }

            AppendHeaderRow(sb, ResourceManager.TranslatedStrings.CommitHash, hashHtml, iconHtml);

            // GitHub PR reference from the commit title
            if (gitHubBaseUrl is not null)
            {
                System.Text.RegularExpressions.Match titleMatch =
                    System.Text.RegularExpressions.Regex.Match(commitBody ?? "", @"\(#(\d+)\)");
                if (titleMatch.Success)
                {
                    string number = titleMatch.Groups[1].Value;
                    string itemUrl = $"{gitHubBaseUrl}/pull/{number}";
                    AppendHeaderRow(sb, "Pull request",
                        $"<a href=\"{WebUtility.HtmlEncode(itemUrl)}\" title=\"View #{number} on GitHub\">#{number}</a>",
                        GitHubSvgIcon);
                }
            }
        }

        if (commitData.ChildIds is { Count: > 0 })
        {
            string rendered = RenderObjectIds(commitData.ChildIds, showRevisionsAsLinks);
            if (rendered.Length > 0)
            {
                AppendHeaderRow(sb, ResourceManager.TranslatedStrings.GetChildren(commitData.ChildIds.Count), rendered);
            }
        }

        if (commitData.ParentIds is { Count: > 0 })
        {
            string rendered = RenderObjectIds(commitData.ParentIds, showRevisionsAsLinks);
            if (rendered.Length > 0)
            {
                AppendHeaderRow(sb, ResourceManager.TranslatedStrings.GetParents(commitData.ParentIds.Count), rendered);
            }
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>
    ///  Formats a person as "Name &lt;email&gt;" where name is plain text
    ///  and email is a muted mailto link with tooltip.
    /// </summary>
    private static string FormatPersonHtml(string? person)
    {
        if (string.IsNullOrEmpty(person))
        {
            return string.Empty;
        }

        string name = GetName(person);
        string email = GetEmail(person);

        StringBuilder sb = new();
        sb.Append(WebUtility.HtmlEncode(name));

        if (!string.IsNullOrEmpty(email) && !IsNoReplyEmail(email))
        {
            sb.Append($"<a class=\"author-email\" href=\"mailto:{WebUtility.HtmlEncode(email)}\" title=\"Send email to {WebUtility.HtmlEncode(email)}\">{WebUtility.HtmlEncode(email)}</a>");
        }

        return sb.ToString();
    }

    /// <summary>
    ///  Extracts co-authors and sign-off trailers from the commit body.
    /// </summary>
    internal static (IReadOnlyList<string> additionalAuthors, IReadOnlyList<string> signedOffBy) ExtractTrailers(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return ([], []);
        }

        HashSet<string> additionalAuthors = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> signedOff = new(StringComparer.OrdinalIgnoreCase);

        foreach (string line in body.Split('\n'))
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("Co-authored-by:", StringComparison.OrdinalIgnoreCase))
            {
                string value = trimmed["Co-authored-by:".Length..].Trim();
                if (value.Length > 0)
                {
                    additionalAuthors.Add(value);
                }
            }
            else if (trimmed.StartsWith("Signed-off-by:", StringComparison.OrdinalIgnoreCase))
            {
                string value = trimmed["Signed-off-by:".Length..].Trim();
                if (value.Length > 0)
                {
                    signedOff.Add(value);
                }
            }
        }

        return ([.. additionalAuthors], [.. signedOff]);
    }

    private static string GetName(string? person)
    {
        if (string.IsNullOrEmpty(person))
        {
            return string.Empty;
        }

        int angleStart = person.IndexOf('<');
        return angleStart > 0 ? person[..angleStart].TrimEnd() : person;
    }

    /// <summary>
    ///  Returns a Gravatar URL for the given email.
    /// </summary>
    private static string GravatarUrl(string email, int size)
    {
        string hash = ComputeMd5Hash(email.Trim().ToLowerInvariant());
        return $"https://www.gravatar.com/avatar/{hash}?r=g&amp;d=identicon&amp;s={size}";

        static string ComputeMd5Hash(string input)
        {
            byte[] hashBytes = System.Security.Cryptography.MD5.HashData(Encoding.ASCII.GetBytes(input));
            return Convert.ToHexStringLower(hashBytes);
        }
    }

    /// <summary>
    ///  Removes Co-authored-by and Signed-off-by trailer lines from the message body.
    /// </summary>
    internal static string StripTrailers(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return body;
        }

        StringBuilder sb = new();
        foreach (string line in body.Split('\n'))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("Co-authored-by:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Signed-off-by:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            sb.AppendLine(line.TrimEnd('\r'));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    ///  Builds the inner HTML for the message section (for incremental updates).
    /// </summary>
    public static string BuildMessageInner(string commitMessageBody, bool renderMarkdown)
    {
        if (string.IsNullOrWhiteSpace(commitMessageBody))
        {
            return string.Empty;
        }

        // Remove trailer lines (Co-authored-by, Signed-off-by) from the message
        // since they are displayed in the header section.
        string body = StripTrailers(commitMessageBody);

        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        if (renderMarkdown)
        {
            string markdown = body;
            if (markdown.Length > 0 && markdown[0] == '\uFEFF')
            {
                markdown = markdown[1..];
            }

            return Markdig.Markdown.ToHtml(markdown, Editor.MarkdownToHtmlConverter.Pipeline);
        }

        return $"<pre><code>{WebUtility.HtmlEncode(body)}</code></pre>";
    }

    private static void AppendHeaderRow(StringBuilder sb, string label, string value, string icon = "")
    {
        sb.Append("<div class=\"header-row\"><span class=\"header-label\">")
          .Append(WebUtility.HtmlEncode(label))
          .Append("</span><span class=\"header-icon\">")
          .Append(icon)
          .Append("</span><span class=\"header-value\">")
          .Append(value)
          .Append("</span></div>");
    }

    private string RenderObjectIds(IReadOnlyList<ObjectId> objectIds, bool showAsLinks)
    {
        IEnumerable<ObjectId> filtered = objectIds.Where(id => !id.IsArtificial);

        if (showAsLinks)
        {
            return string.Join(" ", filtered.Select(id =>
                $"<span class=\"hash-row\"><a href=\"gitext://gotocommit/{id}\" title=\"Navigate to commit {id.ToShortString()}\">" +
                $"<span class=\"hash\">{WebUtility.HtmlEncode(id.ToShortString())}</span></a>" +
                $"<span class=\"copy-btn\" onclick=\"copyId(this, '{id}')\" title=\"Copy full commit ID\">&#x1F4CB;</span></span>"));
        }

        return string.Join(" ", filtered.Select(id =>
            $"<span class=\"hash-row\"><span class=\"hash\">{WebUtility.HtmlEncode(id.ToShortString())}</span>" +
            $"<span class=\"copy-btn\" onclick=\"copyId(this, '{id}')\" title=\"Copy full commit ID\">&#x1F4CB;</span></span>"));
    }

    private static string FormatDateHtml(DateTimeOffset date)
    {
        string fullDate = LocalizationHelpers.GetFullDateString(date);
        string relative = WebUtility.HtmlEncode(LocalizationHelpers.GetRelativeDateString(DateTime.UtcNow, date.UtcDateTime));

        // Colour date separators (/, :, spaces between date parts) in muted colour
        string styledDate = System.Text.RegularExpressions.Regex.Replace(
            WebUtility.HtmlEncode(fullDate),
            @"([/:\-])",
            "<span class=\"date-relative\">$1</span>");

        return $"{styledDate}<span class=\"date-relative\"> {relative}</span>";
    }

    /// <summary>
    ///  Builds only the footer HTML content for incremental updates.
    /// </summary>
    public static string BuildFooterHtml(
        string annotatedTagsInfo,
        string linksInfo,
        string branchInfo,
        string tagInfo,
        string gitDescribeInfo)
    {
        return BuildFooter(annotatedTagsInfo, linksInfo, branchInfo, tagInfo, gitDescribeInfo);
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

            // Skip "Contained in no tag/branch" — absence is self-explanatory
            if (!section.Contains("<a ", StringComparison.OrdinalIgnoreCase)
                && !section.Contains("<u>", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string html = section.Replace("\r\n", "<br>").Replace("\n", "<br>");

            // Strip redundant "Related links:" prefix — the icons make it self-explanatory
            html = System.Text.RegularExpressions.Regex.Replace(
                html,
                @"^[^<]*Related links[^<]*:<br>",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            html = AddTitleToLinks(html);

            sb.Append("<div class=\"footer-section\">")
              .Append(html)
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

    /// <summary>
    ///  Adds <c>title</c> attributes to <c>&lt;a href='...'&gt;</c> tags
    ///  so that hovering shows the URL as a tooltip. Skips internal links.
    /// </summary>
    private static string AddTitleToLinks(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            html,
            @"<a href=(['""])([^'""]+)\1>",
            match =>
            {
                string url = match.Groups[2].Value;
                string quote = match.Groups[1].Value;

                if (url.StartsWith("gitext://gotobranch/", StringComparison.OrdinalIgnoreCase))
                {
                    string branch = url["gitext://gotobranch/".Length..];
                    return $"<a href={quote}{url}{quote} title=\"Navigate to branch {WebUtility.HtmlEncode(branch)}\">";
                }

                if (url.StartsWith("gitext://gototag/", StringComparison.OrdinalIgnoreCase))
                {
                    string tag = url["gitext://gototag/".Length..];
                    return $"<a href={quote}{url}{quote} title=\"Navigate to tag {WebUtility.HtmlEncode(tag)}\">";
                }

                if (url.StartsWith("gitext://", StringComparison.OrdinalIgnoreCase))
                {
                    return match.Value;
                }

                string escaped = WebUtility.HtmlEncode(url);

                // Add a small icon for GitHub links
                bool isGitHub = url.Contains("github.com", StringComparison.OrdinalIgnoreCase);
                string icon = isGitHub
                    ? "<svg class=\"link-icon\" viewBox=\"0 0 16 16\" fill=\"currentColor\"><path d=\"M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z\"/></svg>"
                    : "";

                return $"<a href={quote}{url}{quote} title=\"{escaped}\">{icon}";
            });
    }

    private static string GetPersonIcon(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return "";
        }

        if (email.Contains("Copilot", StringComparison.OrdinalIgnoreCase)
            && email.Contains("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return CopilotIcon;
        }

        if (email.Contains("noreply@github.com", StringComparison.OrdinalIgnoreCase))
        {
            return GitHubSvgIcon;
        }

        return $"<img class=\"inline-avatar\" src=\"{GravatarUrl(email, 32)}\" />";
    }

    private static bool IsNoReplyEmail(string email)
    {
        return email.Contains("noreply", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///  Extracts a GitHub repo base URL (e.g. https://github.com/owner/repo)
    ///  from a remote URL, or returns null if not a GitHub remote.
    /// </summary>
    private static string? GetGitHubBaseUrl(string? remoteUrl)
    {
        if (string.IsNullOrEmpty(remoteUrl))
        {
            return null;
        }

        // SSH: git@github.com:owner/repo.git
        System.Text.RegularExpressions.Match sshMatch =
            System.Text.RegularExpressions.Regex.Match(remoteUrl, @"github\.com[:/]([^/]+/[^/]+?)(?:\.git)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (sshMatch.Success)
        {
            return $"https://github.com/{sshMatch.Groups[1].Value}";
        }

        // HTTPS: https://github.com/owner/repo.git
        System.Text.RegularExpressions.Match httpsMatch =
            System.Text.RegularExpressions.Regex.Match(remoteUrl, @"https?://github\.com/([^/]+/[^/]+?)(?:\.git)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (httpsMatch.Success)
        {
            return $"https://github.com/{httpsMatch.Groups[1].Value}";
        }

        return null;
    }

    private static string Css(Color c) => $"rgb({c.R},{c.G},{c.B})";
}
