/* Copyright (C) 2018 LastBattle
https://github.com/eaxvac/Harepacker-resurrected
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;

namespace HaRepacker.GUI.Input
{
    public partial class RenameInputBox : Form
    {
        public static bool Show(string title, string previousItemName, out string newName)
        {
            RenameInputBox form = new RenameInputBox(title);
            form.nameBox.Text = previousItemName; // set previous name

            bool result = form.ShowDialog() == DialogResult.OK;

            newName = form.newNameResult;
            return result;
        }

        private string newNameResult = null;

        public RenameInputBox(string title)
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
                newNameResult = nameBox.Text;

                DialogResult = DialogResult.OK;
                Close();
            }
            else MessageBox.Show(Properties.Resources.EnterValidInput, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
