using System;
using GitCommands;
using JetBrains.Annotations;

namespace GitUI.CommandsDialogs
{
    public partial class FormBlame : GitModuleForm
    {
        private readonly string _fileName;

        [Obsolete("For VS designer and translation test only. Do not remove.")]
        private FormBlame()
        {
            InitializeComponent();
        }

        private FormBlame(GitUICommands commands)
            : base(commands)
        {
            InitializeComponent();
            InitializeComplete();
        }

        public FormBlame(GitUICommands commands, string fileName, [CanBeNull] GitRevision revision, int? initialLine = null)
            : this(commands)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            _fileName = fileName;

            blameControl1.LoadBlame(revision ?? Module.GetRevision(), null, fileName, null, null, Module.FilesEncoding, initialLine);
        }

        private void FormBlameLoad(object sender, EventArgs e)
        {
            Text = $"Blame ({_fileName})";
        }
    }
}