/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;

namespace HaRepacker.GUI.Input
{
    public enum ReplaceResult
    {
        Yes,
        No,
        YesToAll,
        NoToAll,
        NoneSelectedYet
    }

    public partial class ReplaceBox : Form
    {
        public ReplaceResult result = ReplaceResult.No;

        private ReplaceBox()
        {
            InitializeComponent();
        }

        public static bool Show(string name, out ReplaceResult result)
        {
            ReplaceBox box = new ReplaceBox();

            box.label1.Text = string.Format(Properties.Resources.ReplaceConfirm, name);

            box.ShowDialog();
            result = box.result;

            return true;
        }


        private void btnYes_Click(object sender, EventArgs e)
        {
            result = ReplaceResult.Yes;
            Close();
        }

        private void btnNo_Click(object sender, EventArgs e)
        {
            result = ReplaceResult.No;

            Close();
        }

        private void btnYestoall_Click(object sender, EventArgs e)
        {
            result = ReplaceResult.YesToAll;

            Close();
        }

        private void btnNotoall_Click(object sender, EventArgs e)
        {
            result = ReplaceResult.NoToAll;

            Close();
        }
    }
}
