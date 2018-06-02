/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;

namespace HaRepacker.GUI.Interaction
{
    public partial class IntInputBox : Form
    {
        public static bool Show(string title, out string name, out int? integer)
        {
            IntInputBox form = new IntInputBox(title);
            bool result = form.ShowDialog() == DialogResult.OK;
            name = form.nameResult;
            integer = form.intResult;
            return result;
        }

        private string nameResult = null;
        private int? intResult = null;

        public IntInputBox(string title)
        {
            InitializeComponent();
            DialogResult = DialogResult.Cancel;
            Text = title;
        }

        private void nameBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
                okButton_Click(null, null);
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            if (nameBox.Text != "" && nameBox.Text != null)
            {
                nameResult = nameBox.Text;
                intResult = valueBox.Value;
                DialogResult = DialogResult.OK;
                Close();
            }
            else MessageBox.Show(HaRepacker.Properties.Resources.EnterValidInput, HaRepacker.Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
