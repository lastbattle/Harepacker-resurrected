/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;

namespace HaRepacker.GUI.Input
{
    public partial class NameInputBox : Form
    {
        public static bool Show(string title, int maxInputLength, out string name)
        {
            NameInputBox form = new NameInputBox(title);
            if (maxInputLength != 0) // 0 = not set a max length
                form.nameBox.MaxLength = maxInputLength;

            bool result = form.ShowDialog() == DialogResult.OK;
            name = form.nameResult;
            return result;
        }

        private string nameResult = null;

        public NameInputBox(string title)
        {
            InitializeComponent();
            DialogResult = DialogResult.Cancel;
            Text = title;
        }

        /// <summary>
        /// On key press
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nameBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                okButton_Click(null, null);
            }
        }

        /// <summary>
        /// On key up
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nameBox_KeyUp(object sender, KeyEventArgs e)
        {
            //if (e.KeyCode == Keys.Escape) Close();
            //if (e.KeyCode == Keys.Enter) done();
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            if (nameBox.Text != "" && nameBox.Text != null)
            {
                nameResult = nameBox.Text;
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
