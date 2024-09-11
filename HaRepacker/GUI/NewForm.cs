/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using MapleLib.WzLib;
using HaRepacker.GUI.Panels;
using MapleLib.Configuration;
using MapleLib.MapleCryptoLib;
using System.Linq;

namespace HaRepacker.GUI
{
    public partial class NewForm : Form
    {
        private readonly MainPanel _mainPanel;

        private bool bIsLoaded = false;

        private int defaultVersionIndex;

        public NewForm(MainPanel panel)
        {
            this._mainPanel = panel;
            InitializeComponent();

            MainForm.AddWzEncryptionTypesToComboBox(encryptionBox);
            SetWzEncryptionBoxSelectionByWzMapleVersion();
            defaultVersionIndex = encryptionBox.SelectedIndex;

            versionBox.Value = 1;
            
            // change back to default
            Closed += (sender, args) => encryptionBox.SelectedIndex = defaultVersionIndex;

            bIsLoaded = true;
        }

        /// <summary>
        /// Process command key on the form
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // ...
            if (keyData == (Keys.Escape))
            {
                Close(); // exit window
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// On combobox selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EncryptionBox_SelectionChanged(object sender, EventArgs e)
        {
            if (!bIsLoaded)
                return;

            EncryptionKey selectedEncryption = (EncryptionKey)encryptionBox.SelectedItem;
            if (selectedEncryption.MapleVersion == WzMapleVersion.CUSTOM)
            {
                Program.ConfigurationManager.SetCustomWzUserKeyFromConfig();
            }
            else
            {
                MapleCryptoConstants.UserKey_WzLib = MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.ToArray();
            }
        }

        private void SetWzEncryptionBoxSelectionByWzMapleVersion()
        {
            var wzMapleVersion = Program.ConfigurationManager.ApplicationSettings.MapleVersion;
            encryptionBox.SelectedIndex = MainForm.GetIndexByWzMapleVersion(wzMapleVersion, true);
            if (wzMapleVersion == WzMapleVersion.CUSTOM)
            {
                Program.ConfigurationManager.SetCustomWzUserKeyFromConfig();
            }
        }

        /// <summary>
        /// Selecting list WZ checkbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Listwz_CheckedChanged(object sender, EventArgs e)
        {
            if (listBox.Checked)
            {
                copyrightBox.Enabled = true;
                versionBox.Enabled = true;
            }
        }

        /// <summary>
        /// Selecting hotfix data WZ checkbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataWZ_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton_hotfix.Checked)
            {
                copyrightBox.Enabled = false;
                versionBox.Enabled = false;
            }
        }

        /// <summary>
        /// Selecting regular WZ checkbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void regBox_CheckedChanged(object sender, EventArgs e)
        {
            if (regBox.Checked)
            {
                copyrightBox.Enabled = true;
                versionBox.Enabled = true;
            }
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            string name = nameBox.Text.Trim();
            WzMapleVersion wzMapleVersionSelected = ((EncryptionKey)encryptionBox.SelectedItem).MapleVersion; // new encryption selected

            if (regBox.Checked)
            {
                WzFile file = new WzFile((short)versionBox.Value, wzMapleVersionSelected);
                file.Header.Copyright = copyrightBox.Text;
                file.Header.RecalculateFileStart();
                file.Name = name + ".wz";
                file.WzDirectory.Name = name + ".wz";
                _mainPanel.DataTree.Nodes.Add(new WzNode(file));
            }
            else if (listBox.Checked == true)
            {
                new ListEditor(null, wzMapleVersionSelected).Show();
            }
            else if (radioButton_hotfix.Checked == true)
            {
                WzImage img = new WzImage(name + ".wz");
                img.MarkWzImageAsParsed();

                WzNode node = new WzNode(img);
                _mainPanel.DataTree.Nodes.Add(node);
            }

            Close();
        }
    }
}