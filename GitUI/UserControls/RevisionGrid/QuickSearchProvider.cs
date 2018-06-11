using System.Drawing;
using System.Windows.Forms;
using GitCommands;
using GitUI.UserControls.RevisionGrid;

namespace GitUI
{
    /// <summary>
    /// Provides a 'quick search' capability to <see cref="RevisionGridControl"/> whereby the user may type directly
    /// into the control in order to search for the typed word.
    /// </summary>
    internal sealed class QuickSearchProvider
    {
        private readonly Label _label;
        private readonly RevisionGridControl _grid;
        private readonly RevisionDataGridView _gridView;
        private readonly Timer _quickSearchTimer;

        private string _lastQuickSearchString = "";
        private string _quickSearchString = "";

        public QuickSearchProvider(RevisionGridControl grid, RevisionDataGridView gridView)
        {
            _grid = grid;
            _gridView = gridView;

            _label = new Label
            {
                Location = new Point(10, 10),
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = SystemColors.InfoText,
                BackColor = SystemColors.Info
            };

            _quickSearchTimer = new Timer { Interval = AppSettings.RevisionGridQuickSearchTimeout };
            _quickSearchTimer.Tick += (sender, e) =>
            {
                _quickSearchTimer.Stop();
                _quickSearchString = "";
                HideQuickSearchString();
            };

            _grid.Controls.Add(_label);
        }

        public void OnKeyPress(KeyPressEventArgs e)
        {
            var curIndex = _gridView.SelectedRows.Count > 0
                ? _gridView.SelectedRows[0].Index
                : -1;

            curIndex = curIndex >= 0 ? curIndex : 0;

            if (e.KeyChar == 8 && _quickSearchString.Length > 1)
            {
                // backspace
                RestartQuickSearchTimer();

                _quickSearchString = _quickSearchString.Substring(0, _quickSearchString.Length - 1);

                _grid.FindNextMatch(curIndex, _quickSearchString, false);
                _lastQuickSearchString = _quickSearchString;

                e.Handled = true;
                ShowQuickSearchString();
            }
            else if (!char.IsControl(e.KeyChar))
            {
                RestartQuickSearchTimer();

                // The code below is meant to fix the weird key values when pressing keys e.g. ".".
                _quickSearchString = string.Concat(_quickSearchString, char.ToLower(e.KeyChar));

                _grid.FindNextMatch(curIndex, _quickSearchString, false);
                _lastQuickSearchString = _quickSearchString;

                e.Handled = true;
                ShowQuickSearchString();
            }
            else
            {
                _quickSearchString = "";
                HideQuickSearchString();
                e.Handled = false;
            }
        }

        public void NextResult(bool down)
        {
            var curIndex = -1;
            if (_gridView.SelectedRows.Count > 0)
            {
                curIndex = _gridView.SelectedRows[0].Index;
            }

            RestartQuickSearchTimer();

            bool reverse = !down;
            var nextIndex = 0;
            if (curIndex >= 0)
            {
                nextIndex = reverse ? curIndex - 1 : curIndex + 1;
            }

            _quickSearchString = _lastQuickSearchString;
            _grid.FindNextMatch(nextIndex, _quickSearchString, reverse);
            ShowQuickSearchString();
        }

        private void ShowQuickSearchString()
        {
            _label.Visible = true;
            _label.BringToFront();
            _label.Text = _quickSearchString;
            _label.AutoSize = true;
        }

        private void HideQuickSearchString()
        {
            _label.Visible = false;
        }

        private void RestartQuickSearchTimer()
        {
            _quickSearchTimer.Stop();
            _quickSearchTimer.Interval = AppSettings.RevisionGridQuickSearchTimeout;
            _quickSearchTimer.Start();
        }
    }
}