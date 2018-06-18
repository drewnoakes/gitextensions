﻿using System;
using System.Windows.Forms;

namespace GitUI.UserControls.RevisionGrid
{
    /// <summary>
    /// Specialized menu item that has a title and a value which will be copied to the clipboard on click.
    /// </summary>
    public class CopyToClipboardToolStripMenuItem : ToolStripMenuItem
    {
        private readonly string _value;

        public CopyToClipboardToolStripMenuItem(string text, string value, Keys shortcutKeys)
            : base(text)
        {
            _value = value;
            ShortcutKeys = shortcutKeys;
            Click += CopyToClipboard;
        }

        private void CopyToClipboard(object sender, EventArgs e)
        {
            Clipboard.SetText(_value);
        }
    }
}