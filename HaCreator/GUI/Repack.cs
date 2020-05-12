/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using MapleLib.WzLib;
using MapleLib.WzLib.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class Repack : Form
    {
        List<WzFile> toRepack = new List<WzFile>();

        public Repack()
        {
            InitializeComponent();

            StringBuilder repackTxt = new StringBuilder("Files to repack:");
            repackTxt.Append(Environment.NewLine);

            foreach (WzFile wzf in Program.WzManager.wzFiles.Values)
            {
                if (Program.WzManager.wzFilesUpdated[wzf])
                {
                    toRepack.Add(wzf);

                    repackTxt.Append(wzf.Name);
                    repackTxt.Append(Environment.NewLine);
                }
            }
            infoLabel.Text = repackTxt.ToString();
        }

        /// <summary>
        /// On closing form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Repack_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!button_repack.Enabled && !Program.Restarting)
            { //Do not let the user close the form while saving
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Keydown
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Repack_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                button_repack_Click(null, null);
            }
        }

        /// <summary>
        /// Repack button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_repack_Click(object sender, EventArgs e)
        {
            button_repack.Enabled = false;

            Thread t = new Thread(new ThreadStart(RepackerThread));
            t.Start();
        }

        private void ShowErrorMessage(string data)
        {
            MessageBox.Show(data, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ChangeRepackState(string state)
        {
            stateLabel.Text = state;
        }

        /// <summary>
        /// On repacking completed
        /// </summary>
        /// <param name="bSaveFileInHaCreatorDirectory"></param>
        private void FinishSuccess(bool bSaveFileInHaCreatorDirectory)
        {
            MessageBox.Show("Repacked successfully. " + (!bSaveFileInHaCreatorDirectory ? "press OK to restart." : "Please replace the files in HaCreator\\Output."));

            if (!bSaveFileInHaCreatorDirectory)
            {
                Program.Restarting = true;
            }
            else
            {
                button_repack.Enabled = true;
            }
            Close();
        }

        private void ShowErrorMessageThreadSafe(Exception e, string saveStage)
        {
            Invoke((Action)delegate
            {
                ChangeRepackState("ERROR While saving " + saveStage + ", aborted.");
                button_repack.Enabled = true;
                ShowErrorMessage("There has been an error while saving, it is likely because you do not have permissions to the destination folder or the files are in use.\r\n\r\nPress OK to see the error details.");
                ShowErrorMessage(e.Message + "\r\n" + e.StackTrace);
            });
        }

        private void RepackerThread()
        {
            Invoke((Action)delegate 
            { 
                ChangeRepackState("Deleting old backups..."); 
            });

            // Test for write access
            string rootDir = Path.Combine(Program.WzManager.BaseDir, Program.APP_NAME);
            string testDir = Path.Combine(rootDir, "Test");

            bool bSaveFileInHaCreatorDirectory = false;
            try
            {
                if (!Directory.Exists(testDir))
                {
                    Directory.CreateDirectory(testDir);
                    Directory.Delete(testDir);
                }
            }
            catch (Exception e)
            {
                if (e is UnauthorizedAccessException)
                {
                    bSaveFileInHaCreatorDirectory = true;
                }
            }
            if (bSaveFileInHaCreatorDirectory)
                rootDir = Path.Combine(Directory.GetCurrentDirectory(), Program.APP_NAME); 

            // Prepare directories
            string backupDir = Path.Combine(rootDir, "Backup");
            string orgBackupDir = Path.Combine(rootDir, "Original");
            string XMLDir = Path.Combine(rootDir, "XML");

            try
            {
                if (!Directory.Exists(backupDir))
                    Directory.CreateDirectory(backupDir);
                
                if (!Directory.Exists(orgBackupDir))
                    Directory.CreateDirectory(orgBackupDir);
                
                if (!Directory.Exists(XMLDir))
                    Directory.CreateDirectory(XMLDir);

                foreach (FileInfo fi in new DirectoryInfo(backupDir).GetFiles())
                {
                    fi.Delete();
                }
            }
            catch (Exception e)
            {
                ShowErrorMessageThreadSafe(e, "backup files");
                return;
            }

            // Save XMLs
            // We have to save XMLs first, otherwise the WzImages will already be disposed when we reach this code
            Invoke((Action)delegate 
            { 
                ChangeRepackState("Saving XMLs..."); 
            });
            foreach (WzImage img in Program.WzManager.updatedImages)
            {
                try
                {
                    string xmlPath = Path.Combine(XMLDir, img.FullPath);
                    string xmlPathDir = Path.GetDirectoryName(xmlPath);
                    if (!Directory.Exists(xmlPathDir))
                        Directory.CreateDirectory(xmlPathDir);
                    WzClassicXmlSerializer xmlSer = new WzClassicXmlSerializer(0, LineBreak.None, false);
                    xmlSer.SerializeImage(img, xmlPath);
                }
                catch (Exception e)
                {
                    ShowErrorMessageThreadSafe(e, "XMLs");
                    return;
                }
            }

            // Save WZ Files
            foreach (WzFile wzf in toRepack)
            {
                Invoke((Action)delegate 
                { 
                    ChangeRepackState("Saving " + wzf.Name + "..."); 
                });
                string orgFile = wzf.FilePath;

                string tmpFile;
                if (!bSaveFileInHaCreatorDirectory)
                    tmpFile = orgFile + "$tmp";
                else
                {
                    string folderPath = Path.Combine(rootDir, "Output");
                    tmpFile = Path.Combine(folderPath, wzf.Name);

                    try
                    {
                        if (!Directory.Exists(folderPath))
                            Directory.CreateDirectory(folderPath);

                        if (!File.Exists(tmpFile))
                            File.Create(tmpFile).Close();
                    }
                    catch (Exception e)
                    {
                        ShowErrorMessageThreadSafe(e, wzf.Name);
                        return;
                    }
                }

                try
                {
                    wzf.SaveToDisk(tmpFile);
                    wzf.Dispose();

                    if (!bSaveFileInHaCreatorDirectory) // only replace the original file if its saving in the maplestory folder
                    {
                        string buPath = Path.Combine(orgBackupDir, Path.GetFileName(orgFile));
                        // Try to backup to /Originals/ First, if there is already a file there, we are not original, so just backup to /Backup/
                        if (File.Exists(buPath))
                        {
                            buPath = Path.Combine(backupDir, Path.GetFileName(orgFile));
                        }
                        File.Move(orgFile, buPath);
                        File.Move(tmpFile, orgFile);
                    }
                }
                catch (Exception e)
                {
                    ShowErrorMessageThreadSafe(e, wzf.Name);
                    return;
                }
            }

            Invoke((Action)delegate 
            { 
                ChangeRepackState("Finished"); 
                FinishSuccess(bSaveFileInHaCreatorDirectory);
            });
        }
    }
}
