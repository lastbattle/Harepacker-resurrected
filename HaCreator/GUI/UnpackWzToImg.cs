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

            // Set default version name
            textBox_versionName.Text = "v" + DateTime.Now.ToString("yyyyMMdd");
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

                WzMapleVersion mapleVer = (WzMapleVersion)ApplicationSettings.MapleVersionIndex;
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

                    string linksInfo = $"Links resolved: {extractionResult.TotalLinksResolved}" +
                        (extractionResult.TotalLinksFailed > 0 ? $" (missing in original WZ: {extractionResult.TotalLinksFailed})" : "") + "\n";

                    MessageBox.Show(
                        $"Extraction complete!\n\n" +
                        $"Images extracted: {extractionResult.TotalImagesExtracted}\n" +
                        $"Total size: {FormatBytes(extractionResult.TotalSize)}\n" +
                        linksInfo +
                        $"Duration: {extractionResult.Duration.TotalSeconds:F1} seconds\n" +
                        $"Output: {versionOutputPath}",
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
                Filter = "MapleStory|Base.wz|All files (*.*)|*.*",
                Title = "Select Base.wz file from MapleStory installation",
                CheckFileExists = true,
                CheckPathExists = true
            })
            {
                if (baseWzSelect.ShowDialog() != DialogResult.OK)
                    return;

                string wzFullPath = Path.GetFullPath(baseWzSelect.FileName);
                string baseWzFileName = Path.GetFileName(wzFullPath);

                if (!baseWzFileName.Equals("Base.wz", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Please select the Base.wz file.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _mapleStoryPath = Path.GetDirectoryName(wzFullPath);
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

            // Get all WZ files
            var wzFiles = new List<WzFileInfo>();

            if (is64Bit)
            {
                // 64-bit: Look in Data folder
                string dataPath = Path.Combine(_mapleStoryPath, "Data");
                if (Directory.Exists(dataPath))
                {
                    foreach (var dir in Directory.EnumerateDirectories(dataPath))
                    {
                        string dirName = Path.GetFileName(dir);
                        // Check if it has .wz files inside
                        if (Directory.EnumerateFiles(dir, "*.wz").Any())
                        {
                            int wzCount = Directory.EnumerateFiles(dir, "*.wz", SearchOption.AllDirectories).Count();
                            wzFiles.Add(new WzFileInfo(dirName, $"{dirName} ({wzCount} files)", true));
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
