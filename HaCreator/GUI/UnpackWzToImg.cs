using Footholds;
using HaSharedLibrary.GUI;
using HaCreator.GUI.Localization;
using HaSharedLibrary.Util;
using MapleLib;
using MapleLib.Configuration;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class UnpackWzToImg : Window
    {
        private const int AutoDetectEncryptionSelection = -1;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isExtracting;
        private bool _isUpdatingEncryptionSelections;
        private readonly WzExtractionService _extractionService;

        /// <summary>
        /// Constructor for the UnpackWzToImg form
        /// </summary>
        public UnpackWzToImg()
        {
            InitializeComponent();
            _extractionService = new WzExtractionService();

            // Wire up extraction events
            _extractionService.ProgressChanged += OnExtractionProgressChanged;
            _extractionService.CategoryStarted += OnCategoryStarted;
            _extractionService.CategoryCompleted += OnCategoryCompleted;
            _extractionService.ErrorOccurred += OnExtractionError;

            // Setup log listbox for copy functionality
            SetupLogListBoxCopySupport();

            versionBox.SelectionChanged += VersionBox_SelectedIndexChanged;
            comboBox_writeEncryption.SelectionChanged += ComboBox_writeEncryption_SelectedIndexChanged;
            Initialization_Load(this, EventArgs.Empty);
        }

        public bool? ShowDialog(Forms.IWin32Window owner)
        {
            if (owner != null)
            {
                new System.Windows.Interop.WindowInteropHelper(this).Owner = owner.Handle;
            }
            return ShowDialog();
        }

        /// <summary>
        /// Sets up copy/paste functionality for the log listbox
        /// </summary>
        private void SetupLogListBoxCopySupport()
        {
            // Enable multi-select for copying multiple lines
            listBox_log.SelectionMode = SelectionMode.Extended;

            // Add keyboard shortcuts (Ctrl+C, Ctrl+A)
            listBox_log.KeyDown += ListBox_log_KeyDown;

            // Add context menu for copy
            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem { Header = DialogTextExtension.Get("Dialog_Copy"), Command = ApplicationCommands.Copy });
            contextMenu.Items.Add(new MenuItem { Header = DialogTextExtension.Get("Dialog_SelectAll"), Command = ApplicationCommands.SelectAll });
            contextMenu.Items.Add(new Separator());
            var copyAllItem = new MenuItem { Header = DialogTextExtension.Get("Dialog_CopyAll") };
            copyAllItem.Click += (_, _) => CopyAllLogItems();
            contextMenu.Items.Add(copyAllItem);
            listBox_log.ContextMenu = contextMenu;
            listBox_log.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (_, _) => CopySelectedLogItems()));
            listBox_log.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (_, _) => SelectAllLogItems()));
        }

        private void ListBox_log_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.C)
            {
                CopySelectedLogItems();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.A)
            {
                SelectAllLogItems();
                e.Handled = true;
            }
        }

        private void CopySelectedLogItems()
        {
            if (listBox_log.SelectedItems.Count > 0)
            {
                var selectedText = string.Join(Environment.NewLine,
                    listBox_log.SelectedItems.Cast<object>().Select(item => item.ToString()));
                if (!string.IsNullOrEmpty(selectedText))
                {
                    Clipboard.SetText(selectedText);
                }
            }
        }

        private void SelectAllLogItems()
        {
            for (int i = 0; i < listBox_log.Items.Count; i++)
            {
                listBox_log.SelectedItems.Add(listBox_log.Items[i]);
            }
        }

        private void CopyAllLogItems()
        {
            if (listBox_log.Items.Count > 0)
            {
                var allText = string.Join(Environment.NewLine,
                    listBox_log.Items.Cast<object>().Select(item => item.ToString()));
                if (!string.IsNullOrEmpty(allText))
                {
                    Clipboard.SetText(allText);
                }
            }
        }

        #region Initialization
        /// <summary>
        /// On init
        /// </summary>
        private void Initialization_Load(object sender, EventArgs e)
        {
            _isUpdatingEncryptionSelections = true;
            PopulateReadEncryptionOptions();
            PopulateWriteEncryptionOptions();

            int savedIndex = ApplicationSettings.MapleVersionIndex;
            if (savedIndex < 0 || savedIndex >= versionBox.Items.Count)
            {
                // Default to BMS (IV {0,0,0,0}) for consistent import/export data across versions/localizations.
                savedIndex = 2;
            }
            versionBox.SelectedIndex = savedIndex;
            comboBox_writeEncryption.SelectedIndex = 2;
            _isUpdatingEncryptionSelections = false;

            // Leave path empty - user must select export location
            textBox_path.Text = string.Empty;

            // Populate the MapleStory localisation box
            var values = Enum.GetValues(typeof(MapleLib.ClientLib.MapleStoryLocalisation))
                    .Cast<MapleLib.ClientLib.MapleStoryLocalisation>()
                    .Select(v => new
                    {
                        Text = v.ToString().Replace("MapleStory", "MapleStory "),
                        Value = (int)v
                    })
                    .ToList();

            comboBox_localisation.ItemsSource = values;
            comboBox_localisation.DisplayMemberPath = "Text";
            comboBox_localisation.SelectedValuePath = "Value";

            var savedLocaliation = values.Where(x => x.Value == ApplicationSettings.MapleStoryClientLocalisation).FirstOrDefault();
            comboBox_localisation.SelectedItem = savedLocaliation ?? values[0];

            // Set default version name (include hour and minute for multiple extractions per day)
            textBox_versionName.Text = "v" + DateTime.Now.ToString("yyyyMMdd_HHmm");
        }
        #endregion

        #region Events
        /// <summary>
        /// On select path button click
        /// </summary>
        private void button_pathSelect_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = DialogTextExtension.Get("Dialog_SelectExportFolder");
                dialog.ShowNewFolderButton = true;
                dialog.RootFolder = Environment.SpecialFolder.ProgramFiles;

                if (dialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;
                    textBox_path.Text = selectedPath;
                    ApplicationSettings.MapleStoryDataBasePath = selectedPath;
                }
            }
        }

        /// <summary>
        /// On path text changed
        /// </summary>
        private void textBox_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateButtonState();
        }

        private void textBox_versionName_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            if (_isExtracting)
            {
            button_unpack.Content = DialogTextExtension.Get("Dialog_Cancel");
                button_unpack.IsEnabled = true;
                button_scanWzFiles.IsEnabled = false;
                button_selectAll.IsEnabled = false;
                button_selectNone.IsEnabled = false;
                return;
            }

            button_unpack.Content = DialogTextExtension.Get("Dialog_Extract");
            button_scanWzFiles.IsEnabled = true;
            button_selectAll.IsEnabled = checkedListBox_wzFiles.Items.Count > 0;
            button_selectNone.IsEnabled = checkedListBox_wzFiles.Items.Count > 0;

            bool pathValid = !string.IsNullOrEmpty(textBox_path.Text) && Directory.Exists(textBox_path.Text);
            bool versionValid = !string.IsNullOrEmpty(textBox_versionName.Text);
            bool hasSelectedFiles = GetCheckedFiles().Any();
            bool hasMapleStoryPath = !string.IsNullOrEmpty(_mapleStoryPath) && Directory.Exists(_mapleStoryPath);

            button_unpack.IsEnabled = pathValid && versionValid && hasSelectedFiles && hasMapleStoryPath;
        }

        private static List<EncryptionSelectionItem> BuildEncryptionOptions(bool includeAutoDetect)
        {
            var configManager = new ConfigurationManager();
            configManager.Load();
            string customName = configManager.ApplicationSettings?.MapleVersion_CustomEncryptionName ?? "Default";

            var keys = WzEncryptionOptionsFactory.CreateEncryptionKeys(customName);
            var options = keys.Select(key => new EncryptionSelectionItem(key.Name, key.MapleVersion)).ToList();

            if (includeAutoDetect)
            {
            options.Add(new EncryptionSelectionItem(DialogTextExtension.Get("Dialog_AutoDetect"), AutoDetectEncryptionSelection));
            }

            return options;
        }

        private void PopulateReadEncryptionOptions()
        {
            versionBox.Items.Clear();
            foreach (var option in BuildEncryptionOptions(includeAutoDetect: true)) versionBox.Items.Add(option);
        }

        private void PopulateWriteEncryptionOptions()
        {
            comboBox_writeEncryption.Items.Clear();
            foreach (var option in BuildEncryptionOptions(includeAutoDetect: false)) comboBox_writeEncryption.Items.Add(option);
        }

        private void VersionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingEncryptionSelections || versionBox.SelectedItem is not EncryptionSelectionItem item)
            {
                return;
            }

            if (item.Encryption == WzMapleVersion.CUSTOM && !item.IsAutoDetect)
            {
                ShowCustomEncryptionEditor();
                RefreshEncryptionSelections(versionBox, comboBox_writeEncryption);
            }
        }

        private void ComboBox_writeEncryption_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingEncryptionSelections || comboBox_writeEncryption.SelectedItem is not EncryptionSelectionItem item)
            {
                return;
            }

            if (item.Encryption == WzMapleVersion.CUSTOM)
            {
                ShowCustomEncryptionEditor();
                RefreshEncryptionSelections(versionBox, comboBox_writeEncryption);
            }
        }

        private void ShowCustomEncryptionEditor()
        {
            using var customWzInputBox = new SharedCustomWzEncryptionInputBox();
            customWzInputBox.ShowDialog();
            ConfigureCustomEncryptionFromSettings();
        }

        private void RefreshEncryptionSelections(object readSelectionSource, object writeSelectionSource)
        {
            int readSelection = (readSelectionSource as ComboBox)?.SelectedIndex ?? versionBox.SelectedIndex;
            int writeSelection = (writeSelectionSource as ComboBox)?.SelectedIndex ?? comboBox_writeEncryption.SelectedIndex;

            _isUpdatingEncryptionSelections = true;
            PopulateReadEncryptionOptions();
            PopulateWriteEncryptionOptions();

            versionBox.SelectedIndex = ResolveSelectionIndex(versionBox, readSelection, includeAutoDetect: true);
            comboBox_writeEncryption.SelectedIndex = ResolveSelectionIndex(comboBox_writeEncryption, writeSelection, includeAutoDetect: false);
            _isUpdatingEncryptionSelections = false;
        }

        private static int ResolveSelectionIndex(ComboBox comboBox, int previousIndex, bool includeAutoDetect)
        {
            if (previousIndex >= 0 && previousIndex < comboBox.Items.Count)
            {
                return previousIndex;
            }

            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is EncryptionSelectionItem item &&
                    item.Encryption == WzMapleVersion.CUSTOM &&
                    (!item.IsAutoDetect || includeAutoDetect))
                {
                    return i;
                }
            }

            return includeAutoDetect ? 0 : 2;
        }

        private bool TryGetSelectedReadEncryption(out WzMapleVersion encryption)
        {
            encryption = WzMapleVersion.BMS;
            if (versionBox.SelectedItem is not EncryptionSelectionItem item)
            {
                return true;
            }

            if (item.IsAutoDetect)
            {
                return false;
            }

            encryption = item.Encryption;
            return true;
        }

        private WzMapleVersion GetSelectedWriteEncryption()
        {
            if (comboBox_writeEncryption.SelectedItem is EncryptionSelectionItem item)
            {
                return item.Encryption;
            }

            // Default output encryption stays BMS (IV {0,0,0,0}) for consistency.
            return WzMapleVersion.BMS;
        }

        private static void ConfigureCustomEncryptionFromSettings()
        {
            var configManager = new ConfigurationManager();
            configManager.Load();
            configManager.SetCustomWzUserKeyFromConfig();
        }

        /// <summary>
        /// On unpack/cancel button click
        /// </summary>
        private async void button_unpack_Click(object sender, RoutedEventArgs e)
        {
            if (_isExtracting)
            {
                // Cancel the extraction
                _cancellationTokenSource?.Cancel();
                return;
            }

            await StartExtractionAsync();
        }

        private async Task StartExtractionAsync()
        {
            _isExtracting = true;
            _cancellationTokenSource = new CancellationTokenSource();
            UpdateButtonState();

            try
            {
                ApplicationSettings.MapleVersionIndex = versionBox.SelectedIndex;
                ApplicationSettings.MapleStoryClientLocalisation = (int)comboBox_localisation.SelectedValue;

                WzMapleVersion mapleVer;
                bool useAutoDetect = !TryGetSelectedReadEncryption(out mapleVer);
                WzMapleVersion writeEncryption = GetSelectedWriteEncryption();

                // Detect if 64-bit format
                bool is64Bit = WzFileManager.Detect64BitDirectoryWzFileFormat(_mapleStoryPath);

                if (useAutoDetect)
                {
                    string baseWzPath = null;

                    if (is64Bit)
                    {
                        // 64-bit format: Base.wz is in Data/Base/Base_000.wz
                        string dataBasePath = Path.Combine(_mapleStoryPath, "Data", "Base");
                        if (Directory.Exists(dataBasePath))
                        {
                            var wzFiles = Directory.GetFiles(dataBasePath, "*.wz");
                            baseWzPath = wzFiles.FirstOrDefault();
                        }

                        // If not found in Data/Base, try any .wz file in Data folder
                        if (string.IsNullOrEmpty(baseWzPath))
                        {
                            string dataPath = Path.Combine(_mapleStoryPath, "Data");
                            if (Directory.Exists(dataPath))
                            {
                                var wzFiles = Directory.GetFiles(dataPath, "*.wz", SearchOption.AllDirectories);
                                baseWzPath = wzFiles.FirstOrDefault();
                            }
                        }
                    }
                    else
                    {
                        // Standard format: Base.wz is in root
                        baseWzPath = Path.Combine(_mapleStoryPath, "Base.wz");
                        if (!File.Exists(baseWzPath))
                        {
                            // Try to find any .wz file for detection
                            var wzFiles = Directory.GetFiles(_mapleStoryPath, "*.wz");
                            baseWzPath = wzFiles.FirstOrDefault();
                        }
                    }

                    if (!string.IsNullOrEmpty(baseWzPath) && File.Exists(baseWzPath))
                    {
                        mapleVer = MapleLib.WzLib.Util.WzTool.DetectMapleVersion(baseWzPath, out _);
                    listBox_log.Items.Add(DialogTextExtension.Format("Dialog_AutoDetectedEncryption", mapleVer, is64Bit));
                    }
                    else
                    {
                        // Default to BMS (IV {0,0,0,0}) for consistent cross-version IMG filesystem data.
                        mapleVer = WzMapleVersion.BMS;
                    listBox_log.Items.Add(DialogTextExtension.Format("Dialog_AutoDetectDefault", mapleVer));
                    }
                }
                else
                {
                    if (mapleVer == WzMapleVersion.CUSTOM)
                    {
                        ConfigureCustomEncryptionFromSettings();
                    }
                }

                if (writeEncryption == WzMapleVersion.CUSTOM)
                {
                    ConfigureCustomEncryptionFromSettings();
                }

                string outputFolder = textBox_path.Text;
                string versionName = textBox_versionName.Text;

                if (string.IsNullOrEmpty(outputFolder) || !Directory.Exists(outputFolder))
                {
                MessageBox.Show(this, DialogTextExtension.Get("Dialog_SelectValidOutputFolder"), DialogTextExtension.Get("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_mapleStoryPath) || !Directory.Exists(_mapleStoryPath))
                {
                MessageBox.Show(this, DialogTextExtension.Get("Dialog_ScanWzFirst"), DialogTextExtension.Get("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var selectedCategories = GetSelectedCategories();
                if (selectedCategories.Count == 0)
                {
                MessageBox.Show(this, DialogTextExtension.Get("Dialog_SelectWzToExtract"), DialogTextExtension.Get("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string versionOutputPath = Path.Combine(outputFolder, versionName);

                // Check if version already exists
                if (Directory.Exists(versionOutputPath))
                {
                    var result = MessageBox.Show(this,
                DialogTextExtension.Format("Dialog_VersionFolderExists", versionName),
                DialogTextExtension.Get("Dialog_ConfirmOverwrite"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                        return;

                    // Delete existing folder
                    Directory.Delete(versionOutputPath, true);
                }

                // Reset progress
                progressBar.Value = 0;
                progressBar.IsIndeterminate = false;
                textBox_status.Text = DialogTextExtension.Format("Dialog_StartingWzExtraction", selectedCategories.Count);
                listBox_log.Items.Clear();
                listBox_log.Items.Add(DialogTextExtension.Format("Dialog_SelectedCategories", selectedCategories.Count,
                    string.Join(", ", selectedCategories.Take(5)) + (selectedCategories.Count > 5 ? "..." : string.Empty)));

                // Create progress reporter
                var progress = new Progress<ExtractionProgress>(p =>
                {
                    UpdateProgress(p);
                });

                // Run extraction with selected categories
                var extractionResult = await _extractionService.ExtractAsync(
                    _mapleStoryPath,
                    versionOutputPath,
                    versionName,
                    versionName,
                    mapleVer,
                    selectedCategories,
                    resolveLinks: true,
                    _cancellationTokenSource.Token,
                    progress,
                    writeEncryption);

                // Show result
                if (extractionResult.Success)
                {
                    progressBar.Value = 100;
                    textBox_status.Text = DialogTextExtension.Format("Dialog_ExtractionCompleteStatus", extractionResult.TotalImagesExtracted);
                    listBox_log.Items.Add(DialogTextExtension.Get("Dialog_ExtractionCompleteLog"));
                    listBox_log.Items.Add(DialogTextExtension.Format("Dialog_TotalImages", extractionResult.TotalImagesExtracted));
                    listBox_log.Items.Add(DialogTextExtension.Format("Dialog_TotalSize", FormatBytes(extractionResult.TotalSize)));
                    listBox_log.Items.Add(DialogTextExtension.Format("Dialog_LinksResolved", extractionResult.TotalLinksResolved));
                    if (extractionResult.TotalLinksFailed > 0)
                    {
                        listBox_log.Items.Add(DialogTextExtension.Format("Dialog_LinksFailed", extractionResult.TotalLinksFailed));
                    }
                    listBox_log.Items.Add(DialogTextExtension.Format("Dialog_DurationSeconds", extractionResult.Duration.TotalSeconds));
                    listBox_log.Items.Add(DialogTextExtension.Format("Dialog_OutputPath", versionOutputPath));

                    // Automatically add the extracted version to the version selector
                    AddExtractedVersionToSelector(versionOutputPath);

                    MessageBox.Show(this,
                        DialogTextExtension.Format("Dialog_UnpackSuccessSummary", extractionResult.TotalImagesExtracted,
                            FormatBytes(extractionResult.TotalSize), extractionResult.TotalLinksResolved,
                            extractionResult.TotalLinksFailed, extractionResult.Duration.TotalSeconds, versionOutputPath),
                        DialogTextExtension.Get("Dialog_Success"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    textBox_status.Text = DialogTextExtension.Format("Dialog_ExtractionFailedStatus", extractionResult.ErrorMessage);
                    listBox_log.Items.Add(DialogTextExtension.Format("Dialog_ErrorLog", extractionResult.ErrorMessage));

                    MessageBox.Show(this,
                        DialogTextExtension.Format("Dialog_ExtractionFailedMessage", extractionResult.ErrorMessage),
                        DialogTextExtension.Get("Dialog_Error"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                textBox_status.Text = DialogTextExtension.Get("Dialog_ExtractionCancelled");
                listBox_log.Items.Add(DialogTextExtension.Get("Dialog_ExtractionCancelledByUser"));
            }
            catch (Exception ex)
            {
                textBox_status.Text = DialogTextExtension.Format("Dialog_ErrorWithMessage", ex.Message);
                listBox_log.Items.Add(DialogTextExtension.Format("Dialog_ErrorLog", ex.Message));
                MessageBox.Show(this, DialogTextExtension.Format("Dialog_ErrorOccurred", ex.Message), DialogTextExtension.Get("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isExtracting = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                UpdateButtonState();
            }
        }
        #endregion

        #region Progress Updates
        private void UpdateProgress(ExtractionProgress progress)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateProgress(progress));
                return;
            }

            textBox_status.Text = DialogTextExtension.Format("Dialog_ProgressPhaseFile", progress.CurrentPhase, progress.CurrentFile);

            if (progress.TotalFiles > 0)
            {
                int percentage = (int)((double)progress.ProcessedFiles / progress.TotalFiles * 100);
                progressBar.Value = Math.Min(percentage, 100);
            }
        }

        private void OnExtractionProgressChanged(object sender, ExtractionProgressEventArgs e)
        {
            // Already handled by Progress<T>
        }

        private void OnCategoryStarted(object sender, CategoryExtractionEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnCategoryStarted(sender, e));
                return;
            }

                listBox_log.Items.Add(DialogTextExtension.Format("Dialog_StartingCategory", e.Category));
            listBox_log.ScrollIntoView(listBox_log.Items[^1]);
        }

        private void OnCategoryCompleted(object sender, CategoryExtractionEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnCategoryCompleted(sender, e));
                return;
            }

            if (e.Result != null)
            {
                string linksInfo = e.Result.LinksResolved > 0
                    ? DialogTextExtension.Format("Dialog_LinksResolvedSuffix", e.Result.LinksResolved)
                    : "";
                listBox_log.Items.Add(DialogTextExtension.Format("Dialog_CategoryCompleted", e.Category,
                    e.Result.ImagesExtracted, FormatBytes(e.Result.TotalSize), linksInfo));

                if (e.Result.Errors.Count > 0)
                {
                    foreach (var error in e.Result.Errors.Take(3))
                    {
                        listBox_log.Items.Add(DialogTextExtension.Format("Dialog_WarningLog", error));
                    }
                    if (e.Result.Errors.Count > 3)
                    {
                        listBox_log.Items.Add(DialogTextExtension.Format("Dialog_MoreWarnings", e.Result.Errors.Count - 3));
                    }
                }
            }
            if (listBox_log.Items.Count > 0) listBox_log.ScrollIntoView(listBox_log.Items[^1]);
        }

        private void OnExtractionError(object sender, ExtractionErrorEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnExtractionError(sender, e));
                return;
            }

                listBox_log.Items.Add(DialogTextExtension.Format("Dialog_ErrorLog", e.Exception.Message));
            listBox_log.ScrollIntoView(listBox_log.Items[^1]);
        }
        #endregion

        #region WZ File Selection
        private string _mapleStoryPath;

        private void button_scanWzFiles_Click(object sender, RoutedEventArgs e)
        {
            using (Forms.OpenFileDialog baseWzSelect = new()
            {
                Filter = DialogTextExtension.Get("Dialog_WzFileFilter"),
                Title = DialogTextExtension.Get("Dialog_SelectBaseWzTitle"),
                CheckFileExists = true,
                CheckPathExists = true
            })
            {
                if (baseWzSelect.ShowDialog() != Forms.DialogResult.OK)
                    return;

                string wzFullPath = Path.GetFullPath(baseWzSelect.FileName);
                string baseWzFileName = Path.GetFileName(wzFullPath);

                // Check for beta Data.wz (single file containing all categories)
                if (baseWzFileName.Equals("Data.wz", StringComparison.OrdinalIgnoreCase))
                {
                    _mapleStoryPath = Path.GetDirectoryName(wzFullPath);

                    // Verify this is actually a beta format (no separate category WZ files)
                    string skillWzPath = Path.Combine(_mapleStoryPath, "Skill.wz");
                    string stringWzPath = Path.Combine(_mapleStoryPath, "String.wz");
                    string characterWzPath = Path.Combine(_mapleStoryPath, "Character.wz");

                    if (File.Exists(skillWzPath) || File.Exists(stringWzPath) || File.Exists(characterWzPath))
                    {
                        // This is likely a hotfix Data.wz, not beta - treat as standard installation
                MessageBox.Show(this, DialogTextExtension.Get("Dialog_HotfixDataWzSelected"),
                    DialogTextExtension.Get("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                // Check for standard Base.wz
                else if (baseWzFileName.Equals("Base.wz", StringComparison.OrdinalIgnoreCase))
                {
                    _mapleStoryPath = Path.GetDirectoryName(wzFullPath);
                }
                // Check for 64-bit Base_000.wz (in Data/Base/ folder)
                else if (baseWzFileName.StartsWith("Base_", StringComparison.OrdinalIgnoreCase) &&
                         baseWzFileName.EndsWith(".wz", StringComparison.OrdinalIgnoreCase))
                {
                    // Navigate up from Data/Base/Base_000.wz to MapleStory root
                    string baseFolderPath = Path.GetDirectoryName(wzFullPath); // Data/Base
                    string dataFolderPath = Path.GetDirectoryName(baseFolderPath); // Data
                    _mapleStoryPath = Path.GetDirectoryName(dataFolderPath); // MapleStory root

                    // Verify this looks like a valid 64-bit installation
                    if (!Directory.Exists(Path.Combine(_mapleStoryPath, "Data")))
                    {
                    MessageBox.Show(this, DialogTextExtension.Get("Dialog_CannotDetectFromSelectedWz"),
                        DialogTextExtension.Get("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                // Check for any WZ file in Data folder (64-bit) - try to navigate to root
                else if (wzFullPath.Contains(Path.Combine("Data", "")))
                {
                    // Try to find the MapleStory root by looking for the Data folder
                    string currentPath = Path.GetDirectoryName(wzFullPath);
                    while (!string.IsNullOrEmpty(currentPath))
                    {
                        string parentPath = Path.GetDirectoryName(currentPath);
                        if (parentPath != null && Path.GetFileName(currentPath).Equals("Data", StringComparison.OrdinalIgnoreCase))
                        {
                            _mapleStoryPath = parentPath;
                            break;
                        }
                        currentPath = parentPath;
                    }

                    if (string.IsNullOrEmpty(_mapleStoryPath) || !Directory.Exists(Path.Combine(_mapleStoryPath, "Data")))
                    {
                    MessageBox.Show(this, DialogTextExtension.Get("Dialog_CannotDetectMapleDirectory"),
                        DialogTextExtension.Get("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                MessageBox.Show(this, DialogTextExtension.Get("Dialog_SelectSupportedBaseWz"),
                    DialogTextExtension.Get("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                PopulateWzFileList();
            }
        }

        private void PopulateWzFileList()
        {
            checkedListBox_wzFiles.Items.Clear();

            if (string.IsNullOrEmpty(_mapleStoryPath) || !Directory.Exists(_mapleStoryPath))
                return;

            // Detect format
            bool is64Bit = WzFileManager.Detect64BitDirectoryWzFileFormat(_mapleStoryPath);
            bool isBetaDataWz = WzFileManager.DetectBetaDataWzFormat(_mapleStoryPath);

            // Get all WZ files
            var wzFiles = new List<WzFileInfo>();

            if (isBetaDataWz)
            {
                // Beta format: Read Data.wz and list subdirectories as categories
                string dataWzPath = Path.Combine(_mapleStoryPath, "Data.wz");
                long dataWzSize = new FileInfo(dataWzPath).Length;

                try
                {
                    WzMapleVersion encryption;
                    bool useAutoDetect = !TryGetSelectedReadEncryption(out encryption);
                    if (useAutoDetect)
                    {
                        // Try auto-detect
                        encryption = MapleLib.WzLib.Util.WzTool.DetectMapleVersion(dataWzPath, out _);
                    }
                    else if (encryption == WzMapleVersion.CUSTOM)
                    {
                        ConfigureCustomEncryptionFromSettings();
                    }

                    using (var wzFile = new WzFile(dataWzPath, encryption))
                    {
                        var parseStatus = wzFile.ParseWzFile();
                        if (parseStatus == WzFileParseStatus.Success && wzFile.WzDirectory != null)
                        {
                            // List each top-level directory as a category
                            foreach (var dir in wzFile.WzDirectory.WzDirectories)
                            {
                                string dirName = dir.Name;
                                int imageCount = dir.CountImages();

                                bool isStandard = WzExtractionService.STANDARD_WZ_FILES.Contains(dirName, StringComparer.OrdinalIgnoreCase);
                    string displayText = DialogTextExtension.Format("Dialog_DataWzCategoryDisplay", dirName, imageCount);
                                wzFiles.Add(new WzFileInfo(dirName, displayText, isStandard));
                            }

                            // Also list any images directly in root of Data.wz
                            if (wzFile.WzDirectory.WzImages != null && wzFile.WzDirectory.WzImages.Count > 0)
                            {
                                int rootImageCount = wzFile.WzDirectory.WzImages.Count;
                wzFiles.Add(new WzFileInfo("_Root", DialogTextExtension.Format("Dialog_DataWzCategoryDisplay", "_Root", rootImageCount), false));
                            }

                listBox_log.Items.Add(DialogTextExtension.Format("Dialog_DetectedBetaDataWz", FormatBytes(dataWzSize), encryption));
                        }
                        else
                        {
                    listBox_log.Items.Add(DialogTextExtension.Format("Dialog_DataWzParseFailed", parseStatus));
                    MessageBox.Show(this, DialogTextExtension.Format("Dialog_DataWzParseFailedTryEncryption", parseStatus),
                        DialogTextExtension.Get("Dialog_ParseError"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                listBox_log.Items.Add(DialogTextExtension.Format("Dialog_DataWzReadError", ex.Message));
                MessageBox.Show(this, DialogTextExtension.Format("Dialog_DataWzReadErrorTryEncryption", ex.Message),
                    DialogTextExtension.Get("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (is64Bit)
            {
                // 64-bit: Look in Data folder
                string dataPath = Path.Combine(_mapleStoryPath, "Data");
                if (Directory.Exists(dataPath))
                {
                    foreach (var dir in Directory.EnumerateDirectories(dataPath))
                    {
                        string dirName = Path.GetFileName(dir);

                        // Skip Packs folder (handled separately)
                        if (dirName.Equals("Packs", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Get all .wz files in this category folder (including subfolders)
                        var allWzFiles = Directory.EnumerateFiles(dir, "*.wz", SearchOption.AllDirectories).ToList();

                        if (allWzFiles.Count > 0)
                        {
                            // Calculate total file size for all WZ files in the folder
                            long totalSize = 0;
                            int mainWzCount = 0;
                            int canvasWzCount = 0;

                            foreach (var wzFile in allWzFiles)
                            {
                                totalSize += new FileInfo(wzFile).Length;

                                // Check if it's a _Canvas file
                                if (wzFile.Contains("_Canvas", StringComparison.OrdinalIgnoreCase))
                                {
                                    canvasWzCount++;
                                }
                                else
                                {
                                    mainWzCount++;
                                }
                            }

                            // Build display string with total size (like older MapleStory format)
                            // Note: _Canvas files are used for link resolution and embedded into main .img files
                            string displayText;
                            if (canvasWzCount > 0)
                            {
                    displayText = DialogTextExtension.Format("Dialog_WzCategoryCanvasDisplay", dirName,
                        FormatBytes(totalSize), mainWzCount, canvasWzCount);
                            }
                            else
                            {
                    displayText = DialogTextExtension.Format("Dialog_WzCategoryDisplay", dirName, FormatBytes(totalSize), mainWzCount);
                            }

                            bool isStandard = WzExtractionService.STANDARD_WZ_FILES.Contains(dirName, StringComparer.OrdinalIgnoreCase);
                            wzFiles.Add(new WzFileInfo(dirName, displayText, isStandard));
                        }
                    }

                    // Check for Packs folder with .ms files
                    string packsPath = Path.Combine(dataPath, "Packs");
                    if (Directory.Exists(packsPath))
                    {
                        var allMsFiles = Directory.EnumerateFiles(packsPath, "*.ms", SearchOption.AllDirectories).ToList();
                        if (allMsFiles.Count > 0)
                        {
                            long totalSize = allMsFiles.Sum(f => new FileInfo(f).Length);
                string displayText = DialogTextExtension.Format("Dialog_PacksDisplay", FormatBytes(totalSize), allMsFiles.Count);
                            // Mark as standard so it's checked by default
                            wzFiles.Add(new WzFileInfo("Packs", displayText, true));
                        }
                    }
                }
            }
            else
            {
                // Standard: Look for .wz files in root
                foreach (var wzFile in Directory.EnumerateFiles(_mapleStoryPath, "*.wz"))
                {
                    string fileName = Path.GetFileName(wzFile);
                    string category = Path.GetFileNameWithoutExtension(wzFile);
                    long fileSize = new FileInfo(wzFile).Length;

                    // Skip backup files and non-standard names
                    bool isStandard = WzExtractionService.STANDARD_WZ_FILES.Contains(category, StringComparer.OrdinalIgnoreCase) ||
                                      IsNumberedVariant(category);

                    wzFiles.Add(new WzFileInfo(category, $"{fileName} ({FormatBytes(fileSize)})", isStandard));
                }
            }

            // Sort: standard files first, then others
            var sorted = wzFiles.OrderByDescending(f => f.IsStandard)
                                .ThenBy(f => f.Category)
                                .ToList();

            foreach (var file in sorted)
            {
                var checkBox = new CheckBox { Content = file, IsChecked = file.IsStandard, Margin = new Thickness(2) };
                checkBox.Checked += (_, _) => UpdateButtonState();
                checkBox.Unchecked += (_, _) => UpdateButtonState();
                checkedListBox_wzFiles.Items.Add(checkBox);
            }

            UpdateButtonState();
        }

        private bool IsNumberedVariant(string category)
        {
            // Check if it's a numbered variant like "Mob001", "Map2", etc.
            foreach (var standard in WzExtractionService.STANDARD_WZ_FILES)
            {
                if (category.StartsWith(standard, StringComparison.OrdinalIgnoreCase) &&
                    category.Length > standard.Length &&
                    char.IsDigit(category[standard.Length]))
                {
                    return true;
                }
            }
            return false;
        }

        private void button_selectAll_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < checkedListBox_wzFiles.Items.Count; i++)
            {
                if (checkedListBox_wzFiles.Items[i] is CheckBox checkBox) checkBox.IsChecked = true;
            }
        }

        private void button_selectNone_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < checkedListBox_wzFiles.Items.Count; i++)
            {
                if (checkedListBox_wzFiles.Items[i] is CheckBox checkBox) checkBox.IsChecked = false;
            }
        }

        private List<string> GetSelectedCategories()
        {
            var selected = new List<string>();
            foreach (var item in GetCheckedFiles())
            {
                selected.Add(item.Category);
            }
            return selected;
        }

        private IEnumerable<WzFileInfo> GetCheckedFiles() => checkedListBox_wzFiles.Items
            .OfType<CheckBox>()
            .Where(checkBox => checkBox.IsChecked == true)
            .Select(checkBox => checkBox.Content)
            .OfType<WzFileInfo>();

        /// <summary>
        /// Helper class to store WZ file info in the checkedListBox
        /// </summary>
        private class WzFileInfo
        {
            public string Category { get; }
            public string DisplayName { get; }
            public bool IsStandard { get; }

            public WzFileInfo(string category, string displayName, bool isStandard)
            {
                Category = category;
                DisplayName = displayName;
                IsStandard = isStandard;
            }

            public override string ToString() => DisplayName;
        }

        private class EncryptionSelectionItem
        {
            public string DisplayName { get; }
            public WzMapleVersion Encryption { get; }
            public int Sentinel { get; }
            public bool IsAutoDetect => Sentinel == AutoDetectEncryptionSelection;

            public EncryptionSelectionItem(string displayName, WzMapleVersion encryption)
            {
                DisplayName = displayName;
                Encryption = encryption;
                Sentinel = 0;
            }

            public EncryptionSelectionItem(string displayName, int sentinel)
            {
                DisplayName = displayName;
                Encryption = WzMapleVersion.BMS;
                Sentinel = sentinel;
            }

            public override string ToString() => DisplayName;
        }
        #endregion

        #region Helpers
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Adds the extracted version to the HaCreator version selector
        /// </summary>
        /// <param name="versionPath">The path to the extracted version folder</param>
        private void AddExtractedVersionToSelector(string versionPath)
        {
            try
            {
                // Add to VersionManager
                var versionInfo = Program.StartupManager.VersionManager.AddExternalVersion(versionPath);
                if (versionInfo != null)
                {
                listBox_log.Items.Add(DialogTextExtension.Format("Dialog_VersionAdded", versionInfo.DisplayName));
                }
                else
                {
                listBox_log.Items.Add(DialogTextExtension.Get("Dialog_VersionAlreadyInSelector"));
                }

                // Add to recent version paths in config for persistence
                var config = MapleLib.Img.HaCreatorConfig.Load();
                config.AddToRecentVersionPaths(versionPath);
                config.Save();
            }
            catch (Exception ex)
            {
                listBox_log.Items.Add(DialogTextExtension.Format("Dialog_VersionAddWarning", ex.Message));
            }
        }
        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _extractionService.ProgressChanged -= OnExtractionProgressChanged;
            _extractionService.CategoryStarted -= OnCategoryStarted;
            _extractionService.CategoryCompleted -= OnCategoryCompleted;
            _extractionService.ErrorOccurred -= OnExtractionError;
            base.OnClosed(e);
        }
    }
}

