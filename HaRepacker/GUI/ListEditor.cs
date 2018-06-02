/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MapleLib.WzLib;
using HaRepackerLib;

namespace HaRepacker.GUI
{
    public partial class ListEditor : Form
    {
        WzMapleVersion version;

        public ListEditor(string path, WzMapleVersion version)
        {
            this.version = version;
            InitializeComponent();
            string text = "";
            if (path != null)
            {
                List<string> listEntries = ListFileParser.ParseListFile(path, version);
                foreach (string entry in listEntries) text += entry + "\n";
                text = text.Substring(0, text.Length - 1);
            }
            textBox.Text = text.Replace("\n", "\r\n");
        }

        private void ListEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = !Warning.Warn("Are you sure you want to close this file?");
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog() { Title = "Select where to save the file", Filter = "List WZ File (*.wz)|*.wz" };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            List<string> listEntries = textBox.Text.Replace("\r\n", "\n").Split("\n".ToCharArray()).ToList<string>();
            ListFileParser.SaveToDisk(dialog.FileName, version, listEntries);
        }
    }
}
