/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;

namespace HaRepackerLib
{
    public enum ReplaceResult
    {
        Yes,
        No,
        YesToAll,
        NoToAll
    }

    public partial class ReplaceBox : Form
    {
        public ReplaceResult result = ReplaceResult.No;

        public ReplaceBox(string name)
        {
            InitializeComponent();
            label1.Text = string.Format(Properties.Resources.ReplaceConfirm, name);
        }

        private void ReplaceBox_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
        }

        private void btnYes_Click(object sender, EventArgs e)
        {
            result = ReplaceResult.Yes;
            FormClosing -= ReplaceBox_FormClosing;
            Close();
        }

        private void btnNo_Click(object sender, EventArgs e)
        {
            result = ReplaceResult.No;
            FormClosing -= ReplaceBox_FormClosing;
            Close();
        }

        private void btnYestoall_Click(object sender, EventArgs e)
        {
            result = ReplaceResult.YesToAll;
            FormClosing -= ReplaceBox_FormClosing;
            Close();
        }

        private void btnNotoall_Click(object sender, EventArgs e)
        {
            result = ReplaceResult.NoToAll;
            FormClosing -= ReplaceBox_FormClosing;
            Close();
        }
    }
}
