using GitExtensions.Extensibility.Git.Operations;

namespace GitUI.Operations;

/// <summary>
///  An <see cref="IProgress{T}"/> implementation that marshals progress reports
///  to a UI callback on the UI thread. Used by progress dialogs to display
///  operation output in real-time.
/// </summary>
public sealed class UIProgress : IProgress<string>
{
    private readonly Control _control;
    private readonly Action<string> _onReport;

    /// <summary>
    ///  Initializes a new instance of the <see cref="UIProgress"/> class.
    /// </summary>
    /// <param name="control">A control used to marshal callbacks to the UI thread.</param>
    /// <param name="onReport">The callback invoked for each progress message on the UI thread.</param>
    public UIProgress(Control control, Action<string> onReport)
    {
        _control = control;
        _onReport = onReport;
    }

    void IProgress<string>.Report(string value)
    {
        if (_control.InvokeRequired)
        {
            _control.BeginInvoke(() => _onReport(value));
        }
        else
        {
            _onReport(value);
        }
    }
}
