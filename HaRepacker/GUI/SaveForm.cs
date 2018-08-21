/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.IO;
using MapleLib.WzLib.Util;
using System.Diagnostics;
using HaRepacker.GUI.Panels;

namespace HaRepacker.GUI
{
    public partial class SaveForm : Form
    {
        private WzNode wzNode;

        private WzFile wzf; // it can either be a WzImage or a WzFile only.
        private WzImage wzImg; // it can either be a WzImage or a WzFile only.

        private bool IsRegularWzFile = false; // or data.wz

        public string path;
        private MainPanel panel;

        public SaveForm(MainPanel panel, WzNode wzNode)
        {
            InitializeComponent();

            MainForm.AddWzEncryptionTypesToComboBox(encryptionBox);

            this.wzNode = wzNode;
            if (wzNode.Tag is WzImage)
            {
                this.wzImg = (WzImage)wzNode.Tag;
                this.IsRegularWzFile = false;

                versionBox.Enabled = false;

            }
            else
            {
                this.wzf = (WzFile)wzNode.Tag;
                this.IsRegularWzFile = true;
            }
            this.panel = panel;
        }

        public void PrepareAllImgs(WzDirectory dir)
        {
            foreach (WzImage img in dir.WzImages)
                img.Changed = true;
            foreach (WzDirectory subdir in dir.WzDirectories)
                PrepareAllImgs(subdir);
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            if (versionBox.Value < 0)
            {
                Warning.Error(HaRepacker.Properties.Resources.SaveVersionError);
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog()
            {
                Title = HaRepacker.Properties.Resources.SelectOutWz,
                FileName = wzNode.Text,
                Filter = string.Format("{0}|*.wz",
                HaRepacker.Properties.Resources.WzFilter)
            })
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                WzMapleVersion wzMapleVersionSelected = (WzMapleVersion)encryptionBox.SelectedIndex;
                if (this.IsRegularWzFile)
                {
                    if (wzf is WzFile && wzf.MapleVersion != wzMapleVersionSelected)
                        PrepareAllImgs(((WzFile)wzf).WzDirectory);

                    wzf.MapleVersion = (WzMapleVersion)encryptionBox.SelectedIndex;
                    if (wzf is WzFile)
                        ((WzFile)wzf).Version = (short)versionBox.Value;
                    if (wzf.FilePath != null && wzf.FilePath.ToLower() == dialog.FileName.ToLower())
                    {
                        wzf.SaveToDisk(dialog.FileName + "$tmp");
                        wzNode.Delete();
                        File.Delete(dialog.FileName);
                        File.Move(dialog.FileName + "$tmp", dialog.FileName);
                    }
                    else
                    {
                        wzf.SaveToDisk(dialog.FileName);
                        wzNode.Delete();
                    }

                    // Reload the new file
                    Program.WzMan.LoadWzFile(dialog.FileName, (WzMapleVersion)encryptionBox.SelectedIndex, panel);
                }
                else
                {
                    byte[] WzIv = WzTool.GetIvByMapleVersion(wzMapleVersionSelected);

                    // Save file
                    string tmpFilePath = dialog.FileName + ".tmp";
                    string targetFilePath = dialog.FileName;

                    bool error_noAdminPriviledge = false;
                    try
                    {
                        using (FileStream oldfs = File.Open(tmpFilePath, FileMode.OpenOrCreate))
                        {
                            using (WzBinaryWriter wzWriter = new WzBinaryWriter(oldfs, WzIv))
                            {
                                wzImg.SaveImage(wzWriter, true); // Write to temp folder
                            }
                        }
                        try
                        {
                            File.Copy(tmpFilePath, targetFilePath, true);
                            File.Delete(tmpFilePath);
                        }
                        catch (Exception exp)
                        {
                            Debug.WriteLine(exp); // nvm, dont show to user
                        }
                        wzNode.Delete();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        error_noAdminPriviledge = true;
                    }

                    // Reload the new file
                    WzImage img = Program.WzMan.LoadDataWzHotfixFile(dialog.FileName, wzMapleVersionSelected, panel);
                    if (img == null || error_noAdminPriviledge)
                    {
                        MessageBox.Show(HaRepacker.Properties.Resources.MainFileOpenFail, HaRepacker.Properties.Resources.Error);
                    }
                }
            }
            Close();
        }

        private void SaveForm_Load(object sender, EventArgs e)
        {
            if (this.IsRegularWzFile)
            {
                encryptionBox.SelectedIndex = (int)wzf.MapleVersion;
                versionBox.Value = wzf.Version;
            }
            else
            { // Data.wz uses BMS encryption... no sepcific version indicated
                encryptionBox.SelectedIndex = 2;
            }
        }
    }
}
