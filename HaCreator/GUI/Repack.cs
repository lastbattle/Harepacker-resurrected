/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using System;
using System.CodeDom;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace HaCreator.GUI
{
    public partial class Repack : Form
    {
        private readonly List<WzFile> _toRepack;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// Constructor
        /// </summary>
        public Repack()
        {
            InitializeComponent();
            _toRepack = Program.WzManager.GetUpdatedWzFiles();

            foreach (WzFile wzf in _toRepack)
            {
                checkedListBox_changedFiles.Items.Add(wzf.Name, CheckState.Checked);
            }
        }

        /// <summary>
        /// On closing form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Repack_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!button_repack.Enabled && !Program.Restarting)
            {
                //Do not let the user close the form while saving
                e.Cancel = true;
            }
            else
            {
                _cancellationTokenSource.Dispose();
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
        private async void button_repack_Click(object sender, EventArgs e)
        {
            button_repack.Enabled = false;

            await Task.Run(RepackerThread, _cancellationTokenSource.Token);
        }

        private void ShowErrorMessage(string message) =>
             MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        private void ChangeRepackState(string state) =>
            label_repackState.Text = state;


        /// <summary>
        /// On repacking completed
        /// </summary>
        /// <param name="saveFileInHaCreatorDirectory"></param>
        private void FinishSuccess(bool saveFileInHaCreatorDirectory)
        {
            var message = "Repacked successfully. " +
                (!saveFileInHaCreatorDirectory ? string.Empty : "Please replace the files in HaCreator\\Output.");

            MessageBox.Show(message);

            if (!saveFileInHaCreatorDirectory)
            {
                Program.Restarting = true;
            }
            else
            {
                button_repack.Enabled = true;
            }
            Close();
        }

        private void ShowErrorMessageThreadSafe(Exception ex, string saveStage)
        {
            if (!InvokeRequired)
            {
                HandleError();
                return;
            }

            Invoke(HandleError);

            void HandleError()
            {
                ChangeRepackState($"ERROR While saving {saveStage}, aborted.");
                button_repack.Enabled = true;
                ShowErrorMessage("There has been an error while saving, it is likely because you do not have permissions to the destination folder or the files are in use.\r\n\r\nPress OK to see the error details.");
                if (ex != null)
                    ShowErrorMessage($"{ex.Message}\r\n{ex.StackTrace}");
            }
        }

        private async Task UpdateUIAsync(string message)
        {
            if (InvokeRequired)
            {
                await Task.Run(() => Invoke(() => ChangeRepackState(message)));
            }
            else
            {
                ChangeRepackState(message);
            }
        }

        private async Task RepackerThread()
        {
            if (InvokeRequired)
            {
                await UpdateUIAsync("Deleting old backups...");
            }

            // Check file access for all files first
            /*foreach (var wzFile in _toRepack)
            {
                var (inUse, details) = GetFileAccessStatusAsync(wzFile.FilePath);
                if (inUse)
                {
                    ShowErrorMessageThreadSafe(null, details);
                    return;
                }
            }*/

            var (rootDir, saveFileInHaCreatorDirectory) = GetRootDirectoryAsync();
            var directories = new DirectoryStructure(rootDir);

            try
            {
                PrepareDirectoriesAsync(directories);
                SaveXMLFilesAsync(directories);

                // save selected wz files
                foreach (var wzFile in _toRepack.Where(wzFile => checkedListBox_changedFiles.CheckedItems.Cast<string>().Contains(wzFile.Name)))
                {
                    await UpdateUIAsync($"Saving {wzFile.Name}...");

                    var orgFile = wzFile.FilePath;
                    var tmpFile = GetTemporaryFilePath(wzFile, directories, saveFileInHaCreatorDirectory);

                    wzFile.SaveToDisk(tmpFile, wzFile.Is64BitWzFile);
                    wzFile.Dispose();

                    if (!saveFileInHaCreatorDirectory)
                    {
                        string backupName = $"{orgFile}_BAK_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.wz";

                        try
                        {
                            File.Move(orgFile, backupName);
                            File.Move(tmpFile, orgFile);
                        }
                        catch (Exception exp)
                        {
                            ShowErrorMessageThreadSafe(exp, "");

                            // delete the temporary saved wz file if moving is not successful
                            File.Delete(tmpFile);
                            return;
                        }
                    }
                }

                await UpdateUIAsync("Finished");
                await Task.Run(() => Invoke(() => FinishSuccess(saveFileInHaCreatorDirectory)));
            }
            catch (Exception ex)
            {
                ShowErrorMessageThreadSafe(ex, "processing files");
            }
        }

        private (string rootDir, bool saveInHaCreator) GetRootDirectoryAsync()
        {
            var baseDir = Path.Combine(Program.WzManager.WzBaseDirectory, Program.APP_NAME);
            var testDir = Path.Combine(baseDir, "Test");

            try
            {
                if (!Directory.Exists(testDir))
                {
                    Directory.CreateDirectory(testDir);
                    Directory.Delete(testDir);
                }
                return (baseDir, false);
            }
            catch (UnauthorizedAccessException)
            {
                return (Path.Combine(Directory.GetCurrentDirectory(), Program.APP_NAME), true);
            }
        }

        private record DirectoryStructure(string RootDir)
        {
            public string BackupDir => Path.Combine(RootDir, "Backup");
            public string OriginalDir => Path.Combine(RootDir, "Original");
            public string XMLDir => Path.Combine(RootDir, "XML");
            public string OutputDir => Path.Combine(RootDir, "Output");
        }

        private void PrepareDirectoriesAsync(DirectoryStructure dirs)
        {
            Directory.CreateDirectory(dirs.BackupDir);
            Directory.CreateDirectory(dirs.OriginalDir);
            Directory.CreateDirectory(dirs.XMLDir);

            foreach (var file in new DirectoryInfo(dirs.BackupDir).GetFiles())
            {
                file.Delete();
            }
        }

        private async void SaveXMLFilesAsync(DirectoryStructure dirs)
        {
            await UpdateUIAsync("Saving XMLs...");

            foreach (var img in Program.WzManager.WzUpdatedImageList)
            {
                var xmlPath = Path.Combine(dirs.XMLDir, img.FullPath);
                var xmlPathDir = Path.GetDirectoryName(xmlPath);

                Directory.CreateDirectory(xmlPathDir!);
                var xmlSerializer = new WzClassicXmlSerializer(0, LineBreak.None, false);
                xmlSerializer.SerializeImage(img, xmlPath);
            }
        }

        private string GetTemporaryFilePath(WzFile wzFile, DirectoryStructure dirs, bool saveInHaCreator)
        {
            if (!saveInHaCreator)
            {
                return $"{wzFile.FilePath}$tmp";
            }

            Directory.CreateDirectory(dirs.OutputDir);
            var tmpFile = Path.Combine(dirs.OutputDir, wzFile.Name);

            if (!File.Exists(tmpFile))
            {
                File.Create(tmpFile).Dispose();
            }

            return tmpFile;
        }


        /// <summary>
        /// Alternative method that provides more detailed file access information
        /// </summary>
        /// <param name="filePath">Full path to the file</param>
        /// <returns>Tuple containing whether file is in use and access details</returns>
        public static (bool isInUse, string details) GetFileAccessStatusAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return (false, "File does not exist");

            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return (false, "File is available for exclusive access");
            }
            catch (IOException ex)
            {
                return ex.Message switch
                {
                    var msg when msg.Contains("being used by another process")
                        => (true, "File is being used by another process"),
                    var msg when msg.Contains("access denied")
                        => (true, "Access denied - file may be locked or you lack permissions"),
                    _ => (true, $"File is locked: {ex.Message}")
                };
            }
            catch (UnauthorizedAccessException)
            {
                return (true, "Unauthorized access - check file permissions");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error checking file access: {ex.Message}", ex);
            }
        }
    }
}
