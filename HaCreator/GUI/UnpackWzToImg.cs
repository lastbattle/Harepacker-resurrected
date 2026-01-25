using Footholds;
using MapleLib;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class UnpackWzToImg : Form
    {
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isExtracting;
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
        }

        /// <summary>
        /// Sets up copy/paste functionality for the log listbox
        /// </summary>
        private void SetupLogListBoxCopySupport()
        {
            // Enable multi-select for copying multiple lines
            listBox_log.SelectionMode = SelectionMode.MultiExtended;

            // Add keyboard shortcuts (Ctrl+C, Ctrl+A)
            listBox_log.KeyDown += ListBox_log_KeyDown;

            // Add context menu for copy
            var contextMenu = new ContextMenuStrip();
            var copyItem = new ToolStripMenuItem("Copy", null, (s, e) => CopySelectedLogItems());
            copyItem.ShortcutKeys = Keys.Control | Keys.C;
            var selectAllItem = new ToolStripMenuItem("Select All", null, (s, e) => SelectAllLogItems());
            selectAllItem.ShortcutKeys = Keys.Control | Keys.A;
            var copyAllItem = new ToolStripMenuItem("Copy All", null, (s, e) => CopyAllLogItems());

            contextMenu.Items.Add(copyItem);
            contextMenu.Items.Add(selectAllItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(copyAllItem);

            listBox_log.ContextMenuStrip = contextMenu;
        }

        private void ListBox_log_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectedLogItems();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.A)
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
                listBox_log.SetSelected(i, true);
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
            versionBox.SelectedIndex = ApplicationSettings.MapleVersionIndex;

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

            comboBox_localisation.DataSource = values;
            comboBox_localisation.DisplayMember = "Text";
            comboBox_localisation.ValueMember = "Value";

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
        private void button_pathSelect_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select export folder";
                dialog.ShowNewFolderButton = true;
                dialog.RootFolder = Environment.SpecialFolder.ProgramFiles;

                if (dialog.ShowDialog() == DialogResult.OK)
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
        private void textBox_path_TextChanged(object sender, EventArgs e)
        {
            UpdateButtonState();
        }

        private void textBox_versionName_TextChanged(object sender, EventArgs e)
        {
            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            if (_isExtracting)
            {
                button_unpack.Text = "Cancel";
                button_unpack.Enabled = true;
                button_scanWzFiles.Enabled = false;
                button_selectAll.Enabled = false;
                button_selectNone.Enabled = false;
                return;
            }

            button_unpack.Text = "Extract";
            button_scanWzFiles.Enabled = true;
            button_selectAll.Enabled = checkedListBox_wzFiles.Items.Count > 0;
            button_selectNone.Enabled = checkedListBox_wzFiles.Items.Count > 0;

            bool pathValid = !string.IsNullOrEmpty(textBox_path.Text) && Directory.Exists(textBox_path.Text);
            bool versionValid = !string.IsNullOrEmpty(textBox_versionName.Text);
            bool hasSelectedFiles = checkedListBox_wzFiles.CheckedItems.Count > 0;
            bool hasMapleStoryPath = !string.IsNullOrEmpty(_mapleStoryPath) && Directory.Exists(_mapleStoryPath);

            button_unpack.Enabled = pathValid && versionValid && hasSelectedFiles && hasMapleStoryPath;
        }

        /// <summary>
        /// On unpack/cancel button click
        /// </summary>
        private async void button_unpack_Click(object sender, EventArgs e)
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
                int selectedIndex = versionBox.SelectedIndex;

                // Detect if 64-bit format
                bool is64Bit = WzFileManager.Detect64BitDirectoryWzFileFormat(_mapleStoryPath);

                // Map dropdown index to WzMapleVersion
                // Index 0: GMS, 1: EMS/MSEA/KMS, 2: BMS/JMS, 3: Auto-Detect
                if (selectedIndex == 3) // Auto-Detect
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
                        listBox_log.Items.Add($"Auto-detected encryption: {mapleVer} (64-bit: {is64Bit})");
                    }
                    else
                    {
                        mapleVer = WzMapleVersion.BMS; // Default to BMS if detection fails
                        listBox_log.Items.Add($"Could not auto-detect, defaulting to: {mapleVer}");
                    }
                }
                else
                {
                    // Direct mapping: 0=GMS, 1=EMS, 2=BMS
                    mapleVer = (WzMapleVersion)selectedIndex;
                }

                string outputFolder = textBox_path.Text;
                string versionName = textBox_versionName.Text;

                if (string.IsNullOrEmpty(outputFolder) || !Directory.Exists(outputFolder))
                {
                    MessageBox.Show("Please select a valid output folder.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_mapleStoryPath) || !Directory.Exists(_mapleStoryPath))
                {
                    MessageBox.Show("Please scan WZ files first.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var selectedCategories = GetSelectedCategories();
                if (selectedCategories.Count == 0)
                {
                    MessageBox.Show("Please select at least one WZ file to extract.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string versionOutputPath = Path.Combine(outputFolder, versionName);

                // Check if version already exists
                if (Directory.Exists(versionOutputPath))
                {
                    var result = MessageBox.Show(
                        $"A version folder '{versionName}' already exists. Do you want to overwrite it?",
                        "Confirm Overwrite",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result != DialogResult.Yes)
                        return;

                    // Delete existing folder
                    Directory.Delete(versionOutputPath, true);
                }

                // Reset progress
                progressBar.Value = 0;
                progressBar.Style = ProgressBarStyle.Continuous;
                textBox_status.Text = $"Starting extraction of {selectedCategories.Count} WZ files...";
                listBox_log.Items.Clear();
                listBox_log.Items.Add($"Selected {selectedCategories.Count} categories: {string.Join(", ", selectedCategories.Take(5))}{(selectedCategories.Count > 5 ? "..." : "")}");
                Application.DoEvents();

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
                    $"{versionName} (Extracted {DateTime.Now:yyyy-MM-dd})",
                    mapleVer,
                    selectedCategories,
                    resolveLinks: true,
                    _cancellationTokenSource.Token,
                    progress);

                // Show result
                if (extractionResult.Success)
                {
                    progressBar.Value = 100;
                    textBox_status.Text = $"Extraction complete! {extractionResult.TotalImagesExtracted} images extracted.";
                    listBox_log.Items.Add($"=== Extraction Complete ===");
                    listBox_log.Items.Add($"Total images: {extractionResult.TotalImagesExtracted}");
                    listBox_log.Items.Add($"Total size: {FormatBytes(extractionResult.TotalSize)}");
                    listBox_log.Items.Add($"Links resolved: {extractionResult.TotalLinksResolved}");
                    if (extractionResult.TotalLinksFailed > 0)
                    {
                        listBox_log.Items.Add($"Links failed: {extractionResult.TotalLinksFailed} (missing in original WZ)");
                    }
                    listBox_log.Items.Add($"Duration: {extractionResult.Duration.TotalSeconds:F1}s");
                    listBox_log.Items.Add($"Output: {versionOutputPath}");

                    // Automatically add the extracted version to the version selector
                    AddExtractedVersionToSelector(versionOutputPath);

                    string linksInfo = $"Links resolved: {extractionResult.TotalLinksResolved}" +
                        (extractionResult.TotalLinksFailed > 0 ? $" (missing in original WZ: {extractionResult.TotalLinksFailed})" : "") + "\n";

                    MessageBox.Show(
                        $"Extraction complete!\n\n" +
                        $"Images extracted: {extractionResult.TotalImagesExtracted}\n" +
                        $"Total size: {FormatBytes(extractionResult.TotalSize)}\n" +
                        linksInfo +
                        $"Duration: {extractionResult.Duration.TotalSeconds:F1} seconds\n" +
                        $"Output: {versionOutputPath}\n\n" +
                        $"The version has been added to HaCreator's version selector.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    textBox_status.Text = $"Extraction failed: {extractionResult.ErrorMessage}";
                    listBox_log.Items.Add($"ERROR: {extractionResult.ErrorMessage}");

                    MessageBox.Show(
                        $"Extraction failed:\n{extractionResult.ErrorMessage}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                textBox_status.Text = "Extraction cancelled.";
                listBox_log.Items.Add("Extraction was cancelled by user.");
            }
            catch (Exception ex)
            {
                textBox_status.Text = $"Error: {ex.Message}";
                listBox_log.Items.Add($"ERROR: {ex.Message}");
                MessageBox.Show($"An error occurred:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgress(progress)));
                return;
            }

            textBox_status.Text = $"{progress.CurrentPhase}: {progress.CurrentFile}";

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
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnCategoryStarted(sender, e)));
                return;
            }

            listBox_log.Items.Add($"Starting: {e.Category}");
            listBox_log.TopIndex = listBox_log.Items.Count - 1;
        }

        private void OnCategoryCompleted(object sender, CategoryExtractionEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnCategoryCompleted(sender, e)));
                return;
            }

            if (e.Result != null)
            {
                string linksInfo = e.Result.LinksResolved > 0
                    ? $", {e.Result.LinksResolved} links resolved"
                    : "";
                listBox_log.Items.Add($"  Completed: {e.Category} - {e.Result.ImagesExtracted} images ({FormatBytes(e.Result.TotalSize)}{linksInfo})");

                if (e.Result.Errors.Count > 0)
                {
                    foreach (var error in e.Result.Errors.Take(3))
                    {
                        listBox_log.Items.Add($"    Warning: {error}");
                    }
                    if (e.Result.Errors.Count > 3)
                    {
                        listBox_log.Items.Add($"    ... and {e.Result.Errors.Count - 3} more warnings");
                    }
                }
            }
            listBox_log.TopIndex = listBox_log.Items.Count - 1;
        }

        private void OnExtractionError(object sender, ExtractionErrorEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnExtractionError(sender, e)));
                return;
            }

            listBox_log.Items.Add($"ERROR: {e.Exception.Message}");
            listBox_log.TopIndex = listBox_log.Items.Count - 1;
        }
        #endregion

        #region WZ File Selection
        private string _mapleStoryPath;

        private void button_scanWzFiles_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog baseWzSelect = new()
            {
                Filter = "MapleStory WZ|Base.wz;Base_000.wz;Data.wz|All WZ files (*.wz)|*.wz|All files (*.*)|*.*",
                Title = "Select Base.wz, Data.wz (beta), or Base_000.wz (64-bit) from MapleStory installation",
                CheckFileExists = true,
                CheckPathExists = true
            })
            {
                if (baseWzSelect.ShowDialog() != DialogResult.OK)
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
                        MessageBox.Show("This appears to be a hotfix Data.wz file, not a beta MapleStory installation.\n" +
                            "Please select Base.wz for standard installations.",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        MessageBox.Show("Could not detect MapleStory installation directory from the selected file.\n" +
                            "Please select Base.wz from a standard installation or Base_000.wz from a 64-bit installation (in Data/Base folder).",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        MessageBox.Show("Could not detect MapleStory installation directory.\n" +
                            "Please select Base.wz from a standard installation or any .wz file from a 64-bit installation's Data folder.",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("Please select Base.wz (standard), Data.wz (beta), or Base_000.wz (64-bit from Data/Base folder).",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    // Auto-detect encryption for beta Data.wz
                    WzMapleVersion encryption = WzMapleVersion.BMS;
                    int selectedIndex = versionBox.SelectedIndex;
                    if (selectedIndex >= 0 && selectedIndex < 3)
                    {
                        encryption = (WzMapleVersion)selectedIndex;
                    }
                    else
                    {
                        // Try auto-detect
                        encryption = MapleLib.WzLib.Util.WzTool.DetectMapleVersion(dataWzPath, out _);
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
                                string displayText = $"{dirName} ({imageCount} images) [from Data.wz]";
                                wzFiles.Add(new WzFileInfo(dirName, displayText, isStandard));
                            }

                            // Also list any images directly in root of Data.wz
                            if (wzFile.WzDirectory.WzImages != null && wzFile.WzDirectory.WzImages.Count > 0)
                            {
                                int rootImageCount = wzFile.WzDirectory.WzImages.Count;
                                wzFiles.Add(new WzFileInfo("_Root", $"_Root ({rootImageCount} images) [from Data.wz]", false));
                            }

                            listBox_log.Items.Add($"Detected beta Data.wz format ({FormatBytes(dataWzSize)}, encryption: {encryption})");
                        }
                        else
                        {
                            listBox_log.Items.Add($"Failed to parse Data.wz: {parseStatus}");
                            MessageBox.Show($"Failed to parse Data.wz: {parseStatus}\n\nTry selecting a different encryption version.",
                                "Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    listBox_log.Items.Add($"Error reading Data.wz: {ex.Message}");
                    MessageBox.Show($"Error reading Data.wz: {ex.Message}\n\nTry selecting a different encryption version.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                                displayText = $"{dirName} ({FormatBytes(totalSize)}, {mainWzCount} + {canvasWzCount} canvas files)";
                            }
                            else
                            {
                                displayText = $"{dirName} ({FormatBytes(totalSize)}, {mainWzCount} files)";
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
                            string displayText = $"Packs ({FormatBytes(totalSize)}, {allMsFiles.Count} .ms files)";
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
                checkedListBox_wzFiles.Items.Add(file);
                // Auto-check standard files
                if (file.IsStandard)
                {
                    checkedListBox_wzFiles.SetItemChecked(checkedListBox_wzFiles.Items.Count - 1, true);
                }
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

        private void button_selectAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox_wzFiles.Items.Count; i++)
            {
                checkedListBox_wzFiles.SetItemChecked(i, true);
            }
        }

        private void button_selectNone_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox_wzFiles.Items.Count; i++)
            {
                checkedListBox_wzFiles.SetItemChecked(i, false);
            }
        }

        private List<string> GetSelectedCategories()
        {
            var selected = new List<string>();
            foreach (var item in checkedListBox_wzFiles.CheckedItems)
            {
                if (item is WzFileInfo fileInfo)
                {
                    selected.Add(fileInfo.Category);
                }
            }
            return selected;
        }

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
                    listBox_log.Items.Add($"Added version '{versionInfo.DisplayName}' to version selector");
                }
                else
                {
                    listBox_log.Items.Add($"Version already exists in selector");
                }

                // Add to recent version paths in config for persistence
                var config = MapleLib.Img.HaCreatorConfig.Load();
                config.AddToRecentVersionPaths(versionPath);
                config.Save();
            }
            catch (Exception ex)
            {
                listBox_log.Items.Add($"Warning: Could not add to version selector: {ex.Message}");
            }
        }
        #endregion

        #region Cleanup
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
        #endregion
    }
}
