/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using HaRepackerLib;

namespace HaRepacker.GUI
{
    public partial class FirstRunForm : Form
    {
        public FirstRunForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            UserSettings.AutoUpdate = autoUpdate.Checked;
            UserSettings.AutoAssociate = autoAssociateBox.Checked;
            FormClosing -= FirstRunForm_FormClosing;
            Close();
        }

        private void FirstRunForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
        }
    }
}
