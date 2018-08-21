/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaRepacker.Configuration;
using MapleLib.WzLib;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace HaRepacker.GUI.Interaction
{
    public partial class WzMapleVersionInputBox : Form
    {
        public static bool Show(string title, out WzMapleVersion MapleVersionEncryptionSelected)
        {
            WzMapleVersionInputBox form = new WzMapleVersionInputBox(title);
            bool result = form.ShowDialog() == DialogResult.OK;

            if (result)
                MapleVersionEncryptionSelected = (WzMapleVersion)form.comboBox_wzEncryptionType.SelectedIndex;
            else
                MapleVersionEncryptionSelected = WzMapleVersion.BMS; // default

            return result;
        }

        public WzMapleVersionInputBox(string title)
        {
            InitializeComponent();
            DialogResult = DialogResult.Cancel;
            Text = title;

            MainForm.AddWzEncryptionTypesToComboBox(comboBox_wzEncryptionType);
            comboBox_wzEncryptionType.SelectedIndex = (int)Program.ConfigurationManager.ApplicationSettings.MapleVersion;

            // Localization
            label_wzEncrytionType.Text = HaRepacker.Properties.Resources.InteractionWzMapleVersionInfo;
        }

        private void keyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
                okButton_Click(null, null);
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            if (comboBox_wzEncryptionType.SelectedIndex == -1)
            {
                MessageBox.Show(HaRepacker.Properties.Resources.EnterValidInput, HaRepacker.Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            } else
            {
                DialogResult = DialogResult.OK;
            }
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// When the selected index changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox_Encryption_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
