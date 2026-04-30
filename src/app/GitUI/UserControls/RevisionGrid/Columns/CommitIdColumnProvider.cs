using System.Diagnostics.CodeAnalysis;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.Properties;
using GitUIPluginInterfaces;

namespace GitUI.UserControls.RevisionGrid.Columns;

internal sealed class CommitIdColumnProvider : ColumnProvider
{
    private static readonly int _copyButtonMargin = DpiUtil.Scale(1);
    private static readonly int _copyButtonPadding = DpiUtil.Scale(2);
    private static readonly int _copyButtonSize = DpiUtil.Scale(16);

    private readonly Dictionary<Font, int[]> _widthByLengthByFont = new(capacity: 4);
    private readonly RevisionGridControl _grid;
    private readonly Image _copyImage = DpiUtil.Scale(Images.CopyToClipboard.AdaptLightness());
    private int? _charCount = null;
    private int _copyButtonRowIndex = -1;
    private bool _copyButtonPressed;
    private readonly int _maxWidth = TextRenderer.MeasureText(GitRevision.WorkTreeGuid, AppSettings.MonospaceFont).Width;

    public CommitIdColumnProvider(RevisionGridControl grid)
        : base("Commit ID")
    {
        _grid = grid;

        Column = new DataGridViewTextBoxColumn
        {
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            HeaderText = "Commit ID",
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.True,
            Width = DpiUtil.Scale(60),
            MinimumWidth = DpiUtil.Scale(32),
            Visible = AppSettings.ShowObjectIdColumn
        };
    }

    public override void ApplySettings()
    {
        Column.Visible = AppSettings.ShowObjectIdColumn;
    }

    private int GetCharLengthForColumnWidth(int width)
    {
        Font monospaceFont = AppSettings.MonospaceFont;
        if (!_widthByLengthByFont.TryGetValue(monospaceFont, out int[]? widthByLength))
        {
            widthByLength = [.. Enumerable.Range(0, ObjectId.Sha1CharCount + 1).Select(c => TextRenderer.MeasureText(new string('8', c), monospaceFont).Width)];

            _widthByLengthByFont[monospaceFont] = widthByLength;
        }

        int i = Array.FindIndex(widthByLength, w => w > width);

        if (i == -1 && width >= widthByLength[^1])
        {
            return ObjectId.Sha1CharCount;
        }
        else if (i > 1)
        {
            return i - 1;
        }

        return 0;
    }

    public override void OnCellPainting(DataGridViewCellPaintingEventArgs e, GitRevision revision, int rowHeight, in CellStyle style)
    {
        if (string.IsNullOrWhiteSpace(e.FormattedValue as string))
        {
            return;
        }

        _grid.DrawColumnText(e, (string)e.FormattedValue, style.MonospaceFont, style.ForeColor, e.CellBounds, useEllipsis: false);

        if (!revision.IsArtificial && e.RowIndex == _copyButtonRowIndex)
        {
            DrawCopyButton(e.Graphics!, GetCopyButtonBounds(e.CellBounds));
        }
    }

    public int ClearCopyButtonRow()
    {
        return SetCopyButtonRow(-1).OldRowIndex;
    }

    public int ResetCopyButtonPressed()
    {
        if (!_copyButtonPressed)
        {
            return -1;
        }

        _copyButtonPressed = false;
        return _copyButtonRowIndex;
    }

    public (int OldRowIndex, int NewRowIndex) SetCopyButtonRow(int rowIndex)
    {
        int oldRowIndex = _copyButtonRowIndex;
        if (oldRowIndex == rowIndex)
        {
            return (OldRowIndex: oldRowIndex, NewRowIndex: rowIndex);
        }

        _copyButtonRowIndex = rowIndex;
        if (rowIndex < 0)
        {
            _copyButtonPressed = false;
        }

        return (OldRowIndex: oldRowIndex, NewRowIndex: rowIndex);
    }

    public bool SetCopyButtonPressed(bool pressed)
    {
        if (_copyButtonPressed == pressed)
        {
            return false;
        }

        _copyButtonPressed = pressed;
        return true;
    }

    public bool IsCopyButtonHit(int rowIndex, Point gridClientPoint)
    {
        if (Column.DataGridView is null ||
            rowIndex < 0 ||
            rowIndex >= Column.DataGridView.RowCount ||
            !Column.Visible)
        {
            return false;
        }

        Rectangle cellBounds = Column.DataGridView.GetCellDisplayRectangle(Column.Index, rowIndex, cutOverflow: false);
        return !cellBounds.IsEmpty && GetCopyButtonBounds(cellBounds).Contains(gridClientPoint);
    }

    public override void OnColumnWidthChanged(DataGridViewColumnEventArgs e)
    {
        _charCount = GetCharLengthForColumnWidth(e.Column.Width);
        if (e.Column.Width > _maxWidth && Column.DataGridView != null)
        {
            // Enforce from outside the current method because it is not allowed (exception thrown...)
            Task.Run(async () =>
            {
                await Column.DataGridView.SwitchToMainThreadAsync();
                e.Column.Width = _maxWidth;
            });
        }
    }

    public override void OnCellFormatting(DataGridViewCellFormattingEventArgs e, GitRevision revision)
    {
        // Set the grid cell's accessibility text
        if (!revision.IsArtificial)
        {
            if (!_charCount.HasValue)
            {
                _charCount = GetCharLengthForColumnWidth(Column.Width);
            }

            if (_charCount > 0)
            {
                e.Value = revision.ObjectId.ToShortString(_charCount.Value);
            }
        }
    }

    public override bool TryGetToolTip(DataGridViewCellMouseEventArgs e, GitRevision revision, [NotNullWhen(returnValue: true)] out string? toolTip)
    {
        if (revision.ObjectId.IsArtificial)
        {
            toolTip = default;
            return false;
        }

        toolTip = revision.Guid;
        return true;
    }

    private void DrawCopyButton(Graphics graphics, Rectangle buttonBounds)
    {
        ControlPaint.DrawButton(graphics, buttonBounds, _copyButtonPressed ? ButtonState.Pushed : ButtonState.Flat);

        Rectangle imageBounds = Rectangle.Inflate(buttonBounds, -_copyButtonPadding, -_copyButtonPadding);
        if (imageBounds.Width > 0 && imageBounds.Height > 0)
        {
            graphics.DrawImage(_copyImage, imageBounds);
        }
    }

    private static Rectangle GetCopyButtonBounds(Rectangle cellBounds)
    {
        int size = Math.Min(_copyButtonSize, Math.Max(0, cellBounds.Height - (2 * _copyButtonMargin)));
        int x = Math.Max(cellBounds.X, cellBounds.Right - size - _copyButtonMargin);
        int y = cellBounds.Y + ((cellBounds.Height - size) / 2);

        return new Rectangle(x, y, size, size);
    }
}
