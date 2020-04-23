/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using MapleLib.WzLib;
using HaRepacker.GUI.Panels;
using HaRepacker.Configuration;

namespace HaRepacker.GUI
{
    public partial class NewForm : Form
    {
        private MainPanel panel;

        private bool bIsLoading;
        public NewForm(MainPanel panel)
        {
            this.panel = panel;
            InitializeComponent();

            Load += NewForm_Load;
        }

        private void NewForm_Load(object sender, EventArgs e)
        {
            bIsLoading = true;
            try
            {
                MainForm.AddWzEncryptionTypesToComboBox(encryptionBox);

                encryptionBox.SelectedIndex = MainForm.GetIndexByWzMapleVersion(Program.ConfigurationManager.ApplicationSettings.MapleVersion, true);
                versionBox.Value = 1;
            } finally
            {
                bIsLoading = false;
            }
        }

        /// <summary>
        /// On combobox selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void encryptionBox_SelectionChanged(object sender, EventArgs e)
        {
            if (bIsLoading)
                return;

            int selectedIndex = encryptionBox.SelectedIndex;
            WzMapleVersion wzMapleVersion = MainForm.GetWzMapleVersionByWzEncryptionBoxSelection(selectedIndex);
            if (wzMapleVersion == WzMapleVersion.CUSTOM)
            {
                CustomWZEncryptionInputBox customWzInputBox = new CustomWZEncryptionInputBox();
                customWzInputBox.ShowDialog();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listwz_CheckedChanged(object sender, EventArgs e)
        {
            copyrightBox.Enabled = true;
            versionBox.Enabled = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataWZ_CheckedChanged(object sender, EventArgs e)
        {
            copyrightBox.Enabled = false;
            versionBox.Enabled = false;
        }

        /// <summary>
        /// Selecting regular WZ checkbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void regBox_CheckedChanged(object sender, EventArgs e)
        {
            copyrightBox.Enabled = regBox.Checked;
            versionBox.Enabled = regBox.Checked;

            copyrightBox.Enabled = true;
            versionBox.Enabled = true;
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            string name = nameBox.Text;

            if (regBox.Checked)
            {
                WzFile file = new WzFile((short)versionBox.Value, (WzMapleVersion)encryptionBox.SelectedIndex);
                file.Header.Copyright = copyrightBox.Text;
                file.Header.RecalculateFileStart();
                file.Name = name + ".wz";
                file.WzDirectory.Name = name + ".wz";
                panel.DataTree.Nodes.Add(new WzNode(file));
            }
            else if (listBox.Checked == true) 
            {
                new ListEditor(null, (WzMapleVersion)encryptionBox.SelectedIndex).Show();
            }
            else if (radioButton_hotfix.Checked == true)
            {
                WzImage img = new WzImage(name + ".wz");
                img.MarkWzImageAsParsed();
     
                WzNode node = new WzNode(img);
                panel.DataTree.Nodes.Add(node);
            }
            Close();
        }
    }
}
