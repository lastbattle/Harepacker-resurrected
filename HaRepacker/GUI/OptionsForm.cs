/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using HaRepackerLib;
using MapleLib.WzLib.Serialization;
using HaRepackerLib.Controls;
using HaRepackerLib.Controls.HaRepackerMainPanels;

namespace HaRepacker.GUI
{
    public partial class OptionsForm : Form
    {
        private HaRepackerMainPanel panel;

        public OptionsForm(HaRepackerMainPanel panel)
        {
            this.panel = panel;
            InitializeComponent();
            sortBox.Checked = UserSettings.Sort;
            apngIncompEnable.Checked = UserSettings.UseApngIncompatibilityFrame;
            autoAssociateBox.Checked = UserSettings.AutoAssociate;
            devImgSequences.Checked = UserSettings.devImgSequences;
            if (UserSettings.DefaultXmlFolder != "") 
            { 
                defXmlFolderEnable.Checked = true; 
                defXmlFolderBox.Text = UserSettings.DefaultXmlFolder; 
            }
            indentBox.Value = UserSettings.Indentation;
            lineBreakBox.SelectedIndex = (int)UserSettings.LineBreakType;
            autoUpdate.Checked = UserSettings.AutoUpdate;
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            if (indentBox.Value < 0) { Warning.Error(HaRepacker.Properties.Resources.OptionsIndentError); return; }
            UserSettings.Sort = sortBox.Checked;
            UserSettings.UseApngIncompatibilityFrame = apngIncompEnable.Checked;
            UserSettings.AutoAssociate = autoAssociateBox.Checked;
            if (defXmlFolderEnable.Checked)
                UserSettings.DefaultXmlFolder = defXmlFolderBox.Text;
            else
                UserSettings.DefaultXmlFolder = "";
            UserSettings.Indentation = indentBox.Value;
            UserSettings.LineBreakType = (LineBreak)lineBreakBox.SelectedIndex;
            UserSettings.AutoUpdate = autoUpdate.Checked;
            UserSettings.devImgSequences = devImgSequences.Checked;
            Program.SettingsManager.Save();
            Close();
        }

        private void browse_Click(object sender, EventArgs e)
        {
            defXmlFolderBox.Text = SavedFolderBrowser.Show(HaRepacker.Properties.Resources.SelectDefaultXmlFolder);
        }

        private void defXmlFolderEnable_CheckedChanged(object sender, EventArgs e)
        {
            browse.Enabled = defXmlFolderEnable.Checked;
            defXmlFolderBox.Enabled = defXmlFolderEnable.Checked;
        }
    }
}
