﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NetIde.Util.Forms
{
    public partial class FileBrowser : FileBrowserBase
    {
        public FileBrowser()
        {
            InitializeComponent();

            UpdateAutoCompleteSource();
        }

        protected override void OnAllowFilesChanged(EventArgs e)
        {
            UpdateAutoCompleteSource();

            base.OnAllowFilesChanged(e);
        }

        private void UpdateAutoCompleteSource()
        {
            _path.AutoCompleteSource = AllowFiles
                ? AutoCompleteSource.FileSystem
                : AutoCompleteSource.FileSystemDirectories;
        }

        protected override void OnReadOnlyChanged(EventArgs e)
        {
            _path.ReadOnly = ReadOnly;

            base.OnReadOnlyChanged(e);
        }

        protected override void OnPathChanged(EventArgs e)
        {
            _path.Text = Path;

            base.OnPathChanged(e);
        }

        private void _path_TextChanged(object sender, EventArgs e)
        {
            Path = _path.Text;
        }
    }
}
