using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using HaCreator.GUI.Localization;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HaCreator.GUI
{
    public partial class Repack : Window
    {
        private readonly List<WzFile> _toRepack;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// Constructor
        /// </summary>
        public Repack()
        {
            InitializeComponent();
            if (Program.HaEditorWindow?.IsVisible == true) Owner = Program.HaEditorWindow;

            // Check if we're using IMG filesystem mode (no WzManager)
            if (Program.WzManager == null)
            {
                // In IMG filesystem mode, changes are saved directly to disk
                // No repacking is needed
                MessageBox.Show(this,
                    DialogTextExtension.Get("Dialog_RepackNotNeededImg"),
                    DialogTextExtension.Get("Dialog_ImgFilesystemMode"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                _toRepack = new List<WzFile>();
                return;
            }

            _toRepack = Program.WzManager.GetUpdatedWzFiles();

            foreach (WzFile wzf in _toRepack)
            {
                filesList.Items.Add(new CheckBox { Content = wzf.Name, IsChecked = true });
            }
        }

        /// <summary>
        /// On closing form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Repack_Closing(object sender, CancelEventArgs e)
        {
            if (!repackButton.IsEnabled && !Program.Restarting)
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
        /// <summary>
        /// Repack button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Repack_Click(object sender, RoutedEventArgs e)
        {
            repackButton.IsEnabled = false;

            await Task.Run(RepackerThread, _cancellationTokenSource.Token);
        }

        private void ShowErrorMessage(string message) =>
             MessageBox.Show(this, message, DialogTextExtension.Get("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        private void ChangeRepackState(string state) =>
            stateText.Text = state;


        /// <summary>
        /// On repacking completed
        /// </summary>
        /// <param name="saveFileInHaCreatorDirectory"></param>
        private void FinishSuccess(bool saveFileInHaCreatorDirectory)
        {
            var message = saveFileInHaCreatorDirectory
                ? DialogTextExtension.Get("Dialog_RepackSuccessReplace")
                : DialogTextExtension.Get("Dialog_RepackSuccess");

            MessageBox.Show(this, message);

            if (!saveFileInHaCreatorDirectory)
            {
                Program.Restarting = true;
            }
            else
            {
                repackButton.IsEnabled = true;
            }
            Close();
        }

        private void ShowErrorMessageThreadSafe(Exception ex, string saveStage)
        {
            if (Dispatcher.CheckAccess())
            {
                HandleError();
                return;
            }

            Dispatcher.Invoke(HandleError);

            void HandleError()
            {
            ChangeRepackState(DialogTextExtension.Format("Dialog_RepackSaveAborted", saveStage));
                repackButton.IsEnabled = true;
            ShowErrorMessage(DialogTextExtension.Get("Dialog_RepackPermissionError"));
                if (ex != null)
                    ShowErrorMessage($"{ex.Message}\r\n{ex.StackTrace}");
            }
        }

        private async Task UpdateUIAsync(string message)
        {
            await Dispatcher.InvokeAsync(() => ChangeRepackState(message));
        }

        private async Task RepackerThread()
        {
            await UpdateUIAsync(DialogTextExtension.Get("Dialog_DeletingBackups"));

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
                var selectedFiles = await Dispatcher.InvokeAsync(() => filesList.Items.OfType<CheckBox>()
                    .Where(item => item.IsChecked == true).Select(item => item.Content?.ToString()).ToHashSet());
                foreach (var wzFile in _toRepack.Where(wzFile => selectedFiles.Contains(wzFile.Name)))
                {
                await UpdateUIAsync(DialogTextExtension.Format("Dialog_SavingFile", wzFile.Name));

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

            await UpdateUIAsync(DialogTextExtension.Get("Dialog_Finished"));
                await Dispatcher.InvokeAsync(() => FinishSuccess(saveFileInHaCreatorDirectory));
            }
            catch (Exception ex)
            {
                ShowErrorMessageThreadSafe(ex, "processing files");
            }
        }

        private (string rootDir, bool saveInHaCreator) GetRootDirectoryAsync()
        {
            // Handle IMG filesystem mode where WzManager is null
            if (Program.WzManager == null)
            {
                return (Path.Combine(Directory.GetCurrentDirectory(), Program.APP_NAME), true);
            }

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
            await UpdateUIAsync(DialogTextExtension.Get("Dialog_SavingXml"));

            // Skip if WzManager is null (IMG filesystem mode)
            if (Program.WzManager == null)
            {
                return;
            }

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
