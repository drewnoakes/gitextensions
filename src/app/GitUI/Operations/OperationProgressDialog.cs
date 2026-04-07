using GitExtensions.Extensibility.Git.Operations;

namespace GitUI.Operations;

/// <summary>
///  A dialog that shows progress output while an operation runs.
///  Unlike <c>FormProcess</c>, this dialog does not own process execution —
///  the operation owns its own process via <see cref="IOperation.ExecuteAsync"/>,
///  and this dialog observes output via <see cref="IProgress{T}"/>.
/// </summary>
public sealed class OperationProgressDialog : Form
{
    private readonly RichTextBox _outputBox;
    private readonly Button _closeButton;
    private readonly ProgressBar _progressBar;

    private bool _completed;

    /// <summary>
    ///  Initializes a new instance of the <see cref="OperationProgressDialog"/> class.
    /// </summary>
    public OperationProgressDialog()
    {
        Text = "Operation Progress";
        Size = new Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Style = ProgressBarStyle.Marquee,
            Height = 8,
            MarqueeAnimationSpeed = 30,
        };

        _outputBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 9f),
            BackColor = SystemColors.Window,
            WordWrap = false,
        };

        _closeButton = new Button
        {
            Text = "Close",
            Dock = DockStyle.Bottom,
            Height = 30,
            Enabled = false,
            DialogResult = DialogResult.OK,
        };
        _closeButton.Click += (_, _) => Close();

        Controls.Add(_outputBox);
        Controls.Add(_progressBar);
        Controls.Add(_closeButton);

        AcceptButton = _closeButton;
    }

    /// <summary>
    ///  Runs an operation and shows this dialog while it executes.
    ///  The dialog closes automatically on success if <paramref name="autoClose"/> is true.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="runner">The operation runner.</param>
    /// <param name="operation">The operation to run.</param>
    /// <param name="autoClose">Whether to close automatically on success.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the operation completed successfully.</returns>
    public static async Task<bool> RunAsync(
        IWin32Window? owner,
        IOperationRunner runner,
        IOperation operation,
        bool autoClose = true,
        CancellationToken cancellationToken = default)
    {
        using OperationProgressDialog dialog = new();
        dialog.Text = operation.Title;

        UIProgress progress = new(dialog._outputBox, line =>
        {
            dialog._outputBox.AppendText(line + Environment.NewLine);
            dialog._outputBox.ScrollToCaret();
        });

        // Create a runner that injects our progress into the context
        ProgressInjectingRunner progressRunner = new(runner, progress, owner as IWin32Window);

        bool success = false;

        // Start the operation when the dialog is shown.
        // FileAndForget handles exceptions and avoids the async void lambda issue.
        dialog.Shown += (_, _) =>
        {
            ThreadHelper.FileAndForget(async () =>
            {
                try
                {
                    await progressRunner.RunAsync(operation, cancellationToken);
                    success = true;
                }
                catch (OperationCanceledException)
                {
                    dialog.InvokeAndForget(() =>
                        dialog._outputBox.AppendText(Environment.NewLine + "Cancelled." + Environment.NewLine));
                }
                catch (Exception ex)
                {
                    dialog.InvokeAndForget(() =>
                        dialog._outputBox.AppendText(Environment.NewLine + ex.Message + Environment.NewLine));
                }
                finally
                {
                    dialog.InvokeAndForget(() =>
                    {
                        dialog._completed = true;
                        dialog._progressBar.Style = ProgressBarStyle.Continuous;
                        dialog._progressBar.Value = success ? 100 : 0;
                        dialog._closeButton.Enabled = true;

                        if (success && autoClose)
                        {
                            dialog.DialogResult = DialogResult.OK;
                            dialog.Close();
                        }
                    });
                }
            });
        };

#pragma warning disable VSTHRD103 // ShowDialog is the correct call here — the dialog runs a message loop
        dialog.ShowDialog(owner);
#pragma warning restore VSTHRD103

        return success;
    }

    /// <inheritdoc />
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_completed && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
        }

        base.OnFormClosing(e);
    }
}
