using HaRepacker.GUI.Panels;
using MapleLib.Configuration;
using MapleLib.MapleCryptoLib;
using MapleLib.WzLib;
using MapleLib.WzLib.MSFile;
using MapleLib.WzLib.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Windows.Forms;

namespace HaRepacker.GUI
{
    public partial class SaveForm : ThemedDialogWindow
    {
        private readonly WzNode wzNode;

        private readonly WzFile wzf; // it can either be a WzImage or a WzFile only.
        private readonly WzImage wzImg; // it can either be a WzImage or a WzFile only.

        private readonly bool IsRegularWzFile = false; // or data.wz

        public string path;
        private readonly MainPanel _mainPanel;
        private int defaultVersionIndex;

        private bool bIsLoaded = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="panel"></param>
        /// <param name="wzNode"></param>
        public SaveForm(MainPanel panel, WzNode wzNode)
        {
            InitializeComponent();
            ApplyLocalizedText();

            WzEncryptionUiShared.Populate(encryptionBox);

            this.wzNode = wzNode;
            if (wzNode.Tag is WzImage image) // Data.wz hotfix file
            {
                this.wzImg = image;
                this.IsRegularWzFile = false;

                // Data.wz uses BMS encryption... no sepcific version indicated
                SetWzEncryptionBoxSelectionByWzMapleVersion(WzMapleVersion.BMS);

                versionBox.IsEnabled = false; // disable, not necessary
                checkBox_64BitFile.IsEnabled = false; // disable, not necessary
            }
            else
            {
                this.wzf = (WzFile)wzNode.Tag;
                this.IsRegularWzFile = true;

                SetWzEncryptionBoxSelectionByWzMapleVersion(wzf.MapleVersion);

                versionBox.Text = wzf.Version.ToString();
                versionBox.IsEnabled = !wzf.Is64BitWzFile;
                checkBox_64BitFile.IsChecked = wzf.Is64BitWzFile;
            }
            this._mainPanel = panel;

            defaultVersionIndex = encryptionBox.SelectedIndex;
            Closed += (sender, args) => encryptionBox.SelectedIndex = defaultVersionIndex;

            bIsLoaded = true;
        }

        private string LocalizedText(string key, string fallback) => WpfDialogSupport.Text(typeof(SaveForm), key, fallback);

        private void ApplyLocalizedText()
        {
            Title = LocalizedText("$this.Text", "Save");
            formatHeader.Text = LocalizedText("groupBox1.Text", "File format selection:");
            radioButton_wzFile.Content = LocalizedText("radioButton_wzFile.Text", "Save as .wz file");
            radioButton1.Content = LocalizedText("radioButton1.Text", "Save as .ms file (encrypted. v220++)");
            versionLabel.Text = LocalizedText("label1.Text", "Version");
            encryptionLabel.Text = LocalizedText("label2.Text", "Encryption");
            checkBox_64BitFile.Content = LocalizedText("checkBox_64BitFile.Text", "No version number");
            saveButton.Content = LocalizedText("saveButton.Text", "Save");
        }

        /// <summary>
        /// --- Helper function to keep UI synchronized ---
        /// </summary>
        private void UpdateUIState()
        {
            if (groupBox_wzSaveSelection == null || versionBox == null)
                return;

            // The WZ Options group box is only enabled if the WZ Radio button is checked.
            groupBox_wzSaveSelection.IsEnabled = radioButton_wzFile.IsChecked == true;

            versionBox.IsEnabled = checkBox_64BitFile.IsChecked != true;
        }


        /// <summary>
        /// On encryption box selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void encryptionBox_SelectedIndexChanged(object sender, EventArgs e)
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

        private void SetWzEncryptionBoxSelectionByWzMapleVersion(WzMapleVersion versionSelected)
        {
            encryptionBox.SelectedIndex = MainForm.GetIndexByWzMapleVersion(versionSelected);
            if (versionSelected == WzMapleVersion.CUSTOM)
            {
                Program.ConfigurationManager.SetCustomWzUserKeyFromConfig();
            }
        }

        /// <summary>
        /// On save button clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveButton_Click(object sender, EventArgs e)
        {
            int version = WpfDialogSupport.ParseInteger(versionBox.Text, -1);
            if (version < 0)
            {
                Warning.Error(Properties.Resources.SaveVersionError);
                return;
            }

            bool bSaveAsWzFile = radioButton_wzFile.IsChecked == true;

            if (bSaveAsWzFile)
            {
                using (SaveFileDialog dialog = new()
                {
                    Title = Properties.Resources.SelectOutWz,
                    FileName = wzNode.Text,
                    Filter = string.Format("{0}|*.wz",
                    Properties.Resources.WzFilter)
                })
                {
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;

                    bool bSaveAs64BitWzFile = checkBox_64BitFile.IsChecked == true; // no version number
                    WzMapleVersion wzMapleVersionSelected = ((EncryptionKey)encryptionBox.SelectedItem).MapleVersion; // new encryption selected
                    if (this.IsRegularWzFile)
                    {

                        if (wzf.MapleVersion != wzMapleVersionSelected)
                        {
                            PrepareAllImgs(wzf.WzDirectory);
                        }
                        wzf.Version = (short)version;
                        wzf.MapleVersion = wzMapleVersionSelected;

                        if (string.Equals(wzf.FilePath, dialog.FileName, StringComparison.OrdinalIgnoreCase))
                        {
                            wzf.SaveToDisk(dialog.FileName + "$tmp", bSaveAs64BitWzFile, wzMapleVersionSelected);
                            _mainPanel.MainForm.UnloadWzFile(wzf);
                            try
                            {
                                File.Delete(dialog.FileName); // delete existing file
                                File.Move(dialog.FileName + "$tmp", dialog.FileName);
                            }
                            catch (IOException ex)
                            {
                                MessageBox.Show(string.Format(UiLocalization.Translate("Error overwriting WZ file: {0}"), ex.Message), Properties.Resources.Error);
                            }
                        }
                        else
                        {
                            wzf.SaveToDisk(dialog.FileName, bSaveAs64BitWzFile, wzMapleVersionSelected);
                            _mainPanel.MainForm.UnloadWzFile(wzf);
                        }

                        // Reload the new file
                        var loadedFiles = Program.WzFileManager.WzFileList;
                        WzFile loadedWzFile = Program.WzFileManager.LoadWzFile(dialog.FileName, wzMapleVersionSelected);
                        if (loadedWzFile != null)
                        {
                            _mainPanel.MainForm.AddLoadedWzObjectToMainPanel(loadedWzFile);
                        }
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
                            wzNode.DeleteWzNode(); // this is a WzImage, and cannot be unloaded by _mainPanel.MainForm.UnloadWzFile
                        }
                        catch (UnauthorizedAccessException)
                        {
                            error_noAdminPriviledge = true;
                        }

                        // Reload the new file
                        WzImage img = Program.WzFileManager.LoadDataWzHotfixFile(dialog.FileName, wzMapleVersionSelected);
                        if (img == null || error_noAdminPriviledge)
                        {
                            MessageBox.Show(Properties.Resources.MainFileOpenFail, HaRepacker.Properties.Resources.Error);
                        }
                        _mainPanel.MainForm.AddLoadedWzObjectToMainPanel(img);
                    }
                }
            } else
            {
                // save as .ms file
                using (SaveFileDialog dialog = new()
                {
                    Title = Properties.Resources.SelectOutWz,
                    FileName = wzNode.Text.Replace("wz", "ms"),
                    Filter = string.Format("{0}|*.ms",
                    Properties.Resources.MsFilter)
                })
                {
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;

                    using (var memoryStream = new MemoryStream())
                    {
                        var msFile = new WzMsFile(memoryStream, Path.GetFileName(dialog.FileName), dialog.FileName, true, isSavingFile: true);
                        var savedStream = msFile.Save(wzf);

                        // Now savedStream (which is the same as memoryStream) contains the saved .ms data
                        // You can write it to a file or use it as needed
                        using (var fileStream = new FileStream(dialog.FileName, FileMode.OpenOrCreate))
                        {
                            savedStream.CopyTo(fileStream);
                        }
                    }
                }
            }

            Close();
        }


        private void PrepareAllImgs(WzDirectory dir)
        {
            foreach (WzImage img in dir.WzImages)
            {
                img.Changed = true;
            }
            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                PrepareAllImgs(subdir);
            }
        }

        /// <summary>
        /// On checkBox_64BitFile checked changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox_64BitFile_CheckedChanged(object sender, EventArgs e)
        {
            if (!bIsLoaded)
                return;

            UpdateUIState();
        }

        /// <summary>
        /// Selection between saving as .wz or .ms
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileFormat_CheckedChanged(object sender, EventArgs e)
        {
            // When either radio button changes state, update the UI.
            UpdateUIState();
        }
    }
}
