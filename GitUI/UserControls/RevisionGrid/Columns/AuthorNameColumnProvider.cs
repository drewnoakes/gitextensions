﻿using System.Drawing;
using System.Windows.Forms;
using GitCommands;
using GitExtUtils.GitUI;

namespace GitUI.UserControls.RevisionGrid.Columns
{
    internal sealed class AuthorNameColumnProvider : ColumnProvider
    {
        private readonly RevisionGridControl _grid;
        private readonly AuthorRevisionHighlighting _authorHighlighting;

        public AuthorNameColumnProvider(RevisionGridControl grid, AuthorRevisionHighlighting authorHighlighting)
            : base("Author Name")
        {
            _grid = grid;
            _authorHighlighting = authorHighlighting;

            Column = new DataGridViewTextBoxColumn
            {
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                HeaderText = "Author Name",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                FillWeight = 80,
                Width = DpiUtil.Scale(150)
            };
        }

        public override void Refresh() => Column.Visible = AppSettings.ShowAuthorNameColumn;

        public override void OnCellPainting(DataGridViewCellPaintingEventArgs e, GitRevision revision, (Brush backBrush, Color backColor, Color foreColor, Font normalFont, Font boldFont) style)
        {
            if (!revision.IsArtificial)
            {
                var font = _authorHighlighting.IsHighlighted(revision) ? style.boldFont : style.normalFont;

                _grid.DrawColumnText(e, (string)e.FormattedValue, font, style.foreColor, e.CellBounds.ReduceLeft(TextCellLeftMargin));
            }
        }

        public override void OnCellFormatting(DataGridViewCellFormattingEventArgs e, GitRevision revision)
        {
            e.Value = revision.Author ?? "";
            e.FormattingApplied = true;
        }

        public override bool TryGetToolTip(GitRevision revision, out string toolTip)
        {
            if (revision.ObjectId?.IsArtificial == true)
            {
                toolTip = default;
                return false;
            }

            if (revision.Author == revision.Committer && revision.AuthorEmail == revision.CommitterEmail)
            {
                toolTip = $"{revision.Author} <{revision.AuthorEmail}> authored and committed";
            }
            else
            {
                toolTip =
                    $"{revision.Author} <{revision.AuthorEmail}> authored\n" +
                    $"{revision.Committer} <{revision.CommitterEmail}> committed";
            }

            return true;
        }
    }
}