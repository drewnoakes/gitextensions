using System.Diagnostics;
using System.Runtime.InteropServices;
using GitCommands;
using GitExtUtils;
using GitExtUtils.GitUI;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GitUI.UserControls;

/// <summary>
///  A read-only control that renders Markdown text in a WebView2 control
///  using <see cref="Editor.MarkdownToHtmlConverter"/>.
/// </summary>
public class MarkdownViewer : UserControl
{
    private const uint WebView2ControllerInvalidState = 0x8007139F;

    private readonly WebView2 _webView;
    private string _markdownText = string.Empty;
    private string? _pendingHtml;
    private bool _isWebViewReady;
    private bool _isInitializing;

    public MarkdownViewer()
    {
        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
        };

        _webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
        _webView.HandleCreated += WebView_HandleCreated;
        _webView.NavigationStarting += WebView_NavigationStarting;

        // Bubble mouse events so the floating toolbar in FileViewer works.
        _webView.MouseMove += (s, e) => OnMouseMove(e);
        _webView.MouseLeave += (s, e) => OnMouseLeave(e);

        Controls.Add(_webView);
    }

    /// <summary>
    ///  Gets or sets the Markdown text to render.
    /// </summary>
    public string MarkdownText
    {
        get => _markdownText;
        set
        {
            _markdownText = value ?? string.Empty;
            RenderMarkdown();
        }
    }

    /// <summary>
    ///  Sets pre-built HTML content directly, bypassing Markdown conversion.
    /// </summary>
    public void SetHtml(string html)
    {
        if (_isWebViewReady)
        {
            _webView.NavigateToString(html);
        }
        else
        {
            _pendingHtml = html;
            RequestInitialization();
        }
    }

    /// <summary>
    ///  Executes JavaScript in the WebView2 content.
    /// </summary>
    public async Task ExecuteScriptAsync(string script)
    {
        if (_isWebViewReady && _webView.CoreWebView2 is not null)
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }

    /// <summary>
    ///  Updates specific elements in the current page by ID, avoiding a full navigation.
    ///  Falls back to full navigation if the WebView2 isn't ready.
    /// </summary>
    public async Task UpdateElementAsync(string elementId, string innerHtml)
    {
        if (_isWebViewReady && _webView.CoreWebView2 is not null)
        {
            string escaped = innerHtml
                .Replace("\\", "\\\\")
                .Replace("`", "\\`")
                .Replace("$", "\\$");
            await _webView.CoreWebView2.ExecuteScriptAsync(
                $"document.getElementById('{elementId}').innerHTML = `{escaped}`;");
        }
    }

    /// <summary>
    ///  Copies the current selection in the WebView2 to the clipboard.
    /// </summary>
    public async Task ExecuteCopyAsync()
    {
        if (_isWebViewReady && _webView.CoreWebView2 is not null)
        {
            await _webView.CoreWebView2.ExecuteScriptAsync("document.execCommand('copy')");
        }
    }

    /// <summary>
    ///  Returns whether any text is currently selected in the WebView2.
    /// </summary>
    public async Task<bool> HasSelectionAsync()
    {
        if (_isWebViewReady && _webView.CoreWebView2 is not null)
        {
            string result = await _webView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString().length > 0");
            return result == "true";
        }

        return false;
    }

    /// <summary>
    ///  Raised when a web message is received from the WebView2 content.
    /// </summary>
    public event EventHandler<string>? WebMessageReceived;

    /// <summary>
    ///  Raised when the user right-clicks in the WebView2 content.
    ///  The bool indicates whether text is currently selected.
    /// </summary>
    public event EventHandler<bool>? ContextMenuRequested;

    /// <summary>
    ///  Raised when the user clicks in the WebView2 content (to dismiss menus).
    /// </summary>
    public event EventHandler? DismissRequested;

    /// <summary>
    ///  When <see langword="true"/>, the WebView2 does not scroll internally and
    ///  forwards wheel events to the nearest scrollable WinForms parent.
    ///  Use for embedded panels like CommitInfo. Default is <see langword="false"/>.
    /// </summary>
    public bool DisableScrolling { get; set; }

    private async Task EnsureInitializedAsync()
    {
        if (_isWebViewReady || _isInitializing || !CanInitializeWebView())
        {
            return;
        }

        _isInitializing = true;

        try
        {
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(Path.GetTempPath(), "GitExtensions_WebView2"));

            if (!CanInitializeWebView())
            {
                return;
            }

            await _webView.EnsureCoreWebView2Async(environment);
        }
        catch (COMException exception) when ((uint)exception.HResult == WebView2ControllerInvalidState)
        {
            Trace.WriteLine(exception);
        }
        finally
        {
            if (!_isWebViewReady)
            {
                _isInitializing = false;
            }
        }
    }

    private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            _isInitializing = false;
            Trace.WriteLine(e.InitializationException);

            return;
        }

        _isWebViewReady = true;

        // Set the WebView2 background to match the themed SystemColors.Window.
        // This must happen after theme initialization (not in the constructor).
        _webView.DefaultBackgroundColor = SystemColors.Window;

        // Map the avatar cache directory so file:// images can be loaded via a virtual host
        string avatarCachePath = GitCommands.AppSettings.AvatarImageCachePath;
        if (Directory.Exists(avatarCachePath))
        {
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "gitextensions.avatars",
                avatarCachePath,
                CoreWebView2HostResourceAccessKind.Allow);
        }

        CoreWebView2Settings settings = _webView.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
#if DEBUG
        settings.AreDevToolsEnabled = true;
#else
        settings.AreDevToolsEnabled = false;
#endif
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;

        // Handle messages from JavaScript (wheel events, link clicks, etc.)
        _webView.CoreWebView2.WebMessageReceived += (s, args) =>
        {
            string message = args.TryGetWebMessageAsString() ?? string.Empty;

            if (message.Contains("\"type\""))
            {
                if (message.Contains("\"wheel\""))
                {
                    int deltaStart = message.IndexOf("\"delta\":") + 8;
                    int deltaEnd = message.IndexOf('}', deltaStart);
                    if (deltaStart > 7 && deltaEnd > deltaStart
                        && int.TryParse(message[deltaStart..deltaEnd], out int wheelDelta)
                        && DisableScrolling)
                    {
                        ScrollableControl? scrollParent = FindScrollParent();
                        if (scrollParent is not null)
                        {
                            Point pos = scrollParent.AutoScrollPosition;
                            scrollParent.AutoScrollPosition = new Point(-pos.X, -pos.Y + wheelDelta);
                        }
                    }
                }
                else if (message.Contains("\"link\""))
                {
                    int urlStart = message.IndexOf("\"url\":\"") + 7;
                    int urlEnd = message.LastIndexOf('"');
                    if (urlStart > 6 && urlEnd > urlStart)
                    {
                        string url = message[urlStart..urlEnd];

                        if (WebMessageReceived is not null)
                        {
                            WebMessageReceived.Invoke(this, url);
                        }
                        else if (!url.StartsWith("gitext://", StringComparison.OrdinalIgnoreCase))
                        {
                            OsShellUtil.OpenUrlInDefaultBrowser(url);
                        }
                    }
                }
                else if (message.Contains("\"copy\""))
                {
                    int textStart = message.IndexOf("\"text\":\"") + 8;
                    int textEnd = message.LastIndexOf('"');
                    if (textStart > 7 && textEnd > textStart)
                    {
                        string text = message[textStart..textEnd];
                        ClipboardUtil.TrySetText(text);
                    }
                }
                else if (message.Contains("\"contextmenu\""))
                {
                    bool hasSelection = message.Contains("\"hasSelection\":true");
                    ContextMenuRequested?.Invoke(this, hasSelection);
                }
                else if (message.Contains("\"dismiss\""))
                {
                    DismissRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (int.TryParse(message, out int delta) && DisableScrolling)
            {
                ScrollableControl? scrollParent = FindScrollParent();
                if (scrollParent is not null)
                {
                    Point pos = scrollParent.AutoScrollPosition;
                    scrollParent.AutoScrollPosition = new Point(-pos.X, -pos.Y + delta);
                }
            }
        };

        if (_pendingHtml is not null)
        {
            _webView.NavigateToString(_pendingHtml);
            _pendingHtml = null;
        }
    }

    private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // Allow initial content load and data URIs. All other navigation
        // (including gitext:// internal links and http:// external links)
        // is handled by the JavaScript click handler via postMessage.
        if (e.Uri is not null
            && !e.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
            && !e.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
        }
    }

    private void RenderMarkdown()
    {
        string html = Editor.MarkdownToHtmlConverter.Convert(_markdownText, DisableScrolling);

        if (_isWebViewReady)
        {
            _webView.NavigateToString(html);
        }
        else
        {
            _pendingHtml = html;

            // EnsureCoreWebView2Async must run on the UI thread (STA).
            // RenderMarkdown is called from property setters on the UI thread,
            // so we can safely fire-and-forget here.
            RequestInitialization();
        }
    }

    private bool CanInitializeWebView()
        => !IsDisposed
            && !Disposing
            && !_webView.IsDisposed
            && !_webView.Disposing
            && _webView.IsHandleCreated;

    private ScrollableControl? FindScrollParent()
    {
        for (Control? current = Parent; current is not null; current = current.Parent)
        {
            if (current is ScrollableControl { AutoScroll: true } scrollable)
            {
                return scrollable;
            }
        }

        return null;
    }

    private void RequestInitialization()
    {
        if (_pendingHtml is not null && !_isWebViewReady && !_isInitializing && CanInitializeWebView())
        {
            EnsureInitializedAsync().FileAndForget();
        }
    }

    private void WebView_HandleCreated(object? sender, EventArgs e)
    {
        RequestInitialization();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
            _webView.HandleCreated -= WebView_HandleCreated;
            _webView.NavigationStarting -= WebView_NavigationStarting;
            _webView.Dispose();
        }

        base.Dispose(disposing);
    }
}
