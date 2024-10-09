/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using HaRepacker.GUI.Panels;
using System.Diagnostics;
using MapleLib.WzLib.Serializer;

namespace HaRepacker.GUI
{
    public partial class OptionsForm : Form
    {
        public OptionsForm()
        {
            InitializeComponent();

            sortBox.Checked = Program.ConfigurationManager.UserSettings.Sort;
            loadRelated.Checked = Program.ConfigurationManager.UserSettings.AutoloadRelatedWzFiles;
            apngIncompEnable.Checked = Program.ConfigurationManager.UserSettings.UseApngIncompatibilityFrame;
            autoAssociateBox.Checked = Program.ConfigurationManager.UserSettings.AutoAssociate;
            if (Program.ConfigurationManager.UserSettings.DefaultXmlFolder != "") 
            { 
                defXmlFolderEnable.Checked = true; 
                defXmlFolderBox.Text = Program.ConfigurationManager.UserSettings.DefaultXmlFolder; 
            }
            indentBox.Value = Program.ConfigurationManager.UserSettings.Indentation;
            lineBreakBox.SelectedIndex = (int)Program.ConfigurationManager.UserSettings.LineBreakType;

            // Theme color
            themeColor__comboBox.SelectedIndex = Program.ConfigurationManager.UserSettings.ThemeColor;
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

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            if (indentBox.Value < 0)
            {
                Warning.Error(HaRepacker.Properties.Resources.OptionsIndentError);
                return;
            }
            
            Program.ConfigurationManager.UserSettings.Sort = sortBox.Checked;
            Program.ConfigurationManager.UserSettings.AutoloadRelatedWzFiles = loadRelated.Checked;
            Program.ConfigurationManager.UserSettings.UseApngIncompatibilityFrame = apngIncompEnable.Checked;
            Program.ConfigurationManager.UserSettings.AutoAssociate = autoAssociateBox.Checked;
            if (defXmlFolderEnable.Checked)
                Program.ConfigurationManager.UserSettings.DefaultXmlFolder = defXmlFolderBox.Text;
            else
                Program.ConfigurationManager.UserSettings.DefaultXmlFolder = "";
            Program.ConfigurationManager.UserSettings.Indentation = indentBox.Value;
            Program.ConfigurationManager.UserSettings.LineBreakType = (LineBreak)lineBreakBox.SelectedIndex;
            Program.ConfigurationManager.UserSettings.ThemeColor = themeColor__comboBox.SelectedIndex;

            Program.ConfigurationManager.Save();
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

        /// <summary>
        /// OpenAI API hyperlink url
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process myProcess = new Process();

            try {
                // true is the default, but it is important not to set it to false
                myProcess.StartInfo.UseShellExecute = true;
                myProcess.StartInfo.FileName = ((LinkLabel)sender).Text;
                myProcess.Start();
            }
            catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
