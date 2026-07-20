using MapleLib.Img;
using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.ComponentModel;
using System.Windows.Media;

namespace HaRepacker.GUI
{
    public partial class PackToWzForm : ThemedDialogWindow
    {
        private static string T(string text) => UiLocalization.Translate(text);
        private static string TF(string text, params object[] args) => string.Format(T(text), args);
        private readonly string _versionPath;
        private readonly VersionInfo _versionInfo;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _isPacking = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="versionPath">Path to the IMG filesystem version directory</param>
        public PackToWzForm(string versionPath)
        {
            InitializeComponent();

            _versionPath = versionPath;

            // Load version info
            string manifestPath = Path.Combine(versionPath, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    _versionInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<VersionInfo>(json);
                }
                catch
                {
                    _versionInfo = null;
                }
            }

            // Populate categories list
            PopulateCategoriesList();

            // Set default output path
            textBox_outputPath.Text = Path.Combine(Path.GetDirectoryName(versionPath), "WZ_Output");

            // Update UI based on version info
            if (_versionInfo != null)
            {
                label_versionInfo.Text = TF("Version: {0}", _versionInfo.DisplayName ?? _versionInfo.Version);
                checkBox_64bit.IsChecked = _versionInfo.Is64Bit;

                // Set format label based on detected format and pre-select beta checkbox if applicable
                if (_versionInfo.IsBetaMs)
                {
                    label_format.Text = T("Source: Beta (single Data.wz)");
                    label_format.Foreground = Brushes.DarkGreen;
                    // Pre-select beta format checkbox (user can uncheck if desired)
                    checkBox_betaFormat.IsChecked = true;
                }
                else if (_versionInfo.Is64Bit)
                {
                    label_format.Text = T("Source: 64-bit (Data folder)");
                    label_format.Foreground = Brushes.DarkBlue;
                }
                else if (_versionInfo.IsPreBB)
                {
                    label_format.Text = T("Source: Pre-Big Bang");
                    label_format.Foreground = Brushes.DarkOrange;
                }
                else
                {
                    label_format.Text = T("Source: Standard");
                    label_format.Foreground = Brushes.Black;
                }

                // Respect manifest patchVersion verbatim so "0 = auto" persists across sessions.
                int patchVersion = _versionInfo.PatchVersion;
                if (patchVersion >= numericUpDown_patchVersion.Minimum &&
                    patchVersion <= numericUpDown_patchVersion.Maximum)
                {
                    numericUpDown_patchVersion.Value = patchVersion;
                }
            }
            else
            {
                label_versionInfo.Text = T("Version: Unknown (no manifest.json)");
                label_format.Text = T("Source: Unknown");
                label_format.Foreground = Brushes.Gray;
            }

            // Populate encryption dropdown
            PopulateEncryptionDropdown();

            // Update UI state based on initial checkbox values
            UpdateFormatOptionsState();
        }

        /// <summary>
        /// Populates the encryption dropdown with available options.
        /// The encryption from manifest is marked as recommended.
        /// </summary>
        private void PopulateEncryptionDropdown()
        {
            comboBox_encryption.Items.Clear();

            WzMapleVersion manifestEncryption = GetRecommendedEncryption();

            // Add encryption options with recommended marker
            var encryptionOptions = new[]
            {
                WzMapleVersion.BMS,
                WzMapleVersion.GMS,
                WzMapleVersion.EMS,
                WzMapleVersion.CLASSIC
            };

            int selectedIndex = 0;
            for (int i = 0; i < encryptionOptions.Length; i++)
            {
                var enc = encryptionOptions[i];
                string displayName = enc.ToString();

                // Mark the manifest encryption as recommended
                if (enc == manifestEncryption)
                {
                    displayName += " (Recommended)";
                    selectedIndex = i;
                }

                comboBox_encryption.Items.Add(new EncryptionItem(enc, displayName));
            }

            comboBox_encryption.SelectedIndex = selectedIndex;
        }

        /// <summary>
        /// Gets the currently selected encryption from the dropdown.
        /// </summary>
        private WzMapleVersion GetSelectedEncryption()
        {
            if (comboBox_encryption.SelectedItem is EncryptionItem item)
            {
                return item.Encryption;
            }
            return WzMapleVersion.BMS;
        }

        /// <summary>
        /// Resolves recommended encryption from manifest metadata.
        /// Falls back to SourceRegion when Encryption is missing.
        /// </summary>
        private WzMapleVersion GetRecommendedEncryption()
        {
            if (_versionInfo != null)
            {
                if (!string.IsNullOrEmpty(_versionInfo.Encryption) &&
                    Enum.TryParse<WzMapleVersion>(_versionInfo.Encryption, true, out var parsed))
                {
                    return parsed;
                }

                if (!string.IsNullOrEmpty(_versionInfo.SourceRegion))
                {
                    switch (_versionInfo.SourceRegion.Trim().ToUpperInvariant())
                    {
                        case "GMS":
                            return WzMapleVersion.GMS;
                        case "EMS":
                            return WzMapleVersion.EMS;
                        case "CLASSIC":
                            return WzMapleVersion.CLASSIC;
                    }
                }
            }

            return WzMapleVersion.BMS;
        }

        /// <summary>
        /// Extracts version number from strings like "v83", "gms_v230", "GMS v83 (Pre-Big Bang)".
        /// </summary>
        private int ExtractVersionNumber(string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
                return 0;

            var match = System.Text.RegularExpressions.Regex.Match(
                versionString, @"v(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int version))
            {
                return version;
            }

            match = System.Text.RegularExpressions.Regex.Match(versionString, @"(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out version))
            {
                return version;
            }

            return 0;
        }


        private void PopulateCategoriesList()
        {
            checkedListBox_categories.Items.Clear();
            var addedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // First add standard categories
            foreach (var category in WzPackingService.STANDARD_CATEGORIES)
            {
                string categoryPath = Path.Combine(_versionPath, category);
                if (Directory.Exists(categoryPath))
                {
                    int imgCount = Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.AllDirectories).Count();
                    int subDirCount = Directory.EnumerateDirectories(categoryPath, "*", SearchOption.AllDirectories).Count();
                    bool hasListJson = category.Equals("List", StringComparison.OrdinalIgnoreCase) &&
                                       File.Exists(Path.Combine(categoryPath, "List.json"));

                    // Add if it has .img files, subdirectories, or List.json (pre-BB List category).
                    if (imgCount > 0 || subDirCount > 0 || hasListJson)
                    {
                        string displayName;
                        if (imgCount > 0)
                        {
                            displayName = $"{category} ({imgCount} images)";
                        }
                        else if (hasListJson)
                        {
                            displayName = $"{category} (List.json)";
                        }
                        else
                        {
                            displayName = $"{category} (directory structure)";
                        }

                        checkedListBox_categories.AddItem(displayName, true);
                        addedCategories.Add(category);
                    }
                }
            }

            // Then add any non-standard categories found in the version directory
            foreach (var dirPath in Directory.EnumerateDirectories(_versionPath))
            {
                string dirName = Path.GetFileName(dirPath);

                // Skip if already added or is a system/metadata folder
                if (addedCategories.Contains(dirName) ||
                    dirName.StartsWith(".") ||
                    dirName.Equals("manifest", StringComparison.OrdinalIgnoreCase))
                    continue;

                int imgCount = Directory.EnumerateFiles(dirPath, "*.img", SearchOption.AllDirectories).Count();
                int subDirCount = Directory.EnumerateDirectories(dirPath, "*", SearchOption.AllDirectories).Count();
                bool hasListJson = dirName.Equals("List", StringComparison.OrdinalIgnoreCase) &&
                                   File.Exists(Path.Combine(dirPath, "List.json"));

                if (imgCount > 0 || subDirCount > 0 || hasListJson)
                {
                    string displayName;
                    if (imgCount > 0)
                    {
                        displayName = $"{dirName} ({imgCount} images)";
                    }
                    else if (hasListJson)
                    {
                        displayName = $"{dirName} (List.json)";
                    }
                    else
                    {
                        displayName = $"{dirName} (directory structure)";
                    }

                    checkedListBox_categories.AddItem(displayName, true);
                }
            }
        }

        private void button_selectAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox_categories.Items.Count; i++)
            {
                checkedListBox_categories.SetItemChecked(i, true);
            }
        }

        private void button_selectNone_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox_categories.Items.Count; i++)
            {
                checkedListBox_categories.SetItemChecked(i, false);
            }
        }

        private void button_browse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select output folder for WZ files";
                dialog.SelectedPath = textBox_outputPath.Text;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    textBox_outputPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void checkBox_64bit_CheckedChanged(object sender, EventArgs e)
        {
            UpdateFormatOptionsState();
        }

        private void checkBox_betaFormat_CheckedChanged(object sender, EventArgs e)
        {
            UpdateFormatOptionsState();
        }

        /// <summary>
        /// Updates the enabled state of format options based on checkbox selections.
        /// Beta format and 64-bit format are mutually exclusive.
        /// </summary>
        private void UpdateFormatOptionsState()
        {
            if (checkBox_betaFormat == null || checkBox_64bit == null)
                return;

            if (checkBox_betaFormat.IsChecked == true)
            {
                // Beta format - disable 64-bit option
                checkBox_64bit.IsChecked = false;
                checkBox_64bit.IsEnabled = false;
            }
            else
            {
                // Standard/64-bit format
                checkBox_64bit.IsEnabled = true;
            }
        }

        /// <summary>
        /// Persists selected packing settings to manifest.json so future pack sessions
        /// reuse the same effective encryption/format defaults.
        /// </summary>
        private void SavePackingSettingsToManifest()
        {
            if (_versionInfo == null)
            {
                return;
            }

            string manifestPath = Path.Combine(_versionPath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                return;
            }

            try
            {
                _versionInfo.Encryption = GetSelectedEncryption().ToString();
                _versionInfo.Is64Bit = checkBox_64bit.IsChecked == true;
                _versionInfo.IsBetaMs = checkBox_betaFormat.IsChecked == true;

                _versionInfo.PatchVersion = (short)numericUpDown_patchVersion.Value;

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(_versionInfo, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(manifestPath, json);
            }
            catch
            {
                // Best-effort persistence only; packing should continue even if manifest write fails.
            }
        }

        private async void button_pack_Click(object sender, EventArgs e)
        {
            if (_isPacking)
            {
                // Cancel current operation
                _cancellationTokenSource.Cancel();
                return;
            }

            // Get selected categories
            var selectedCategories = new List<string>();
            foreach (var item in checkedListBox_categories.CheckedItems)
            {
                // Extract category name from display string (e.g., "String (15 images)" -> "String")
                string displayName = item.ToString();
                string categoryName = displayName.Split(' ')[0];
                selectedCategories.Add(categoryName);
            }

            if (selectedCategories.Count == 0)
            {
                MessageBox.Show(T("Please select at least one category to pack."), T("No Categories Selected"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputPath = textBox_outputPath.Text;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                MessageBox.Show(T("Please specify an output path."), T("Output Path Required"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SavePackingSettingsToManifest();

            // Start packing
            _isPacking = true;
            button_pack.Content = UiLocalization.Translate("Cancel");
            checkedListBox_categories.IsEnabled = false;
            textBox_outputPath.IsEnabled = false;
            button_browse.IsEnabled = false;
            checkBox_64bit.IsEnabled = false;
            checkBox_betaFormat.IsEnabled = false;
            numericUpDown_patchVersion.IsEnabled = false;
            comboBox_encryption.IsEnabled = false;

            try
            {
                var packingService = new WzPackingService();

                var progress = new Progress<PackingProgress>(p =>
                {
                    Dispatcher.Invoke(() => UpdateProgress(p));
                });

                // Get selected encryption
                WzMapleVersion selectedEncryption = GetSelectedEncryption();

                PackingResult result;
                if (checkBox_betaFormat.IsChecked == true)
                {
                    // Use beta packing for single Data.wz format
                    result = await packingService.PackBetaDataWzAsync(
                        _versionPath,
                        outputPath,
                        selectedCategories,
                        _cancellationTokenSource.Token,
                        progress,
                        (short)numericUpDown_patchVersion.Value,
                        selectedEncryption);
                }
                else
                {
                    // Use standard packing for separate category WZ files
                    result = await packingService.PackCategoriesAsync(
                        _versionPath,
                        outputPath,
                        selectedCategories,
                        checkBox_64bit.IsChecked == true,
                        _cancellationTokenSource.Token,
                        progress,
                        (short)numericUpDown_patchVersion.Value,
                        false, // separateCanvas
                        selectedEncryption);
                }

                if (result.Success)
                {
                    label_status.Text = TF("Completed! Packed {0} images.", result.TotalImagesPacked);
                    MessageBox.Show(
                        $"Successfully packed {result.TotalImagesPacked} images into {result.CategoriesPacked.Count} WZ files.\n\n" +
                        $"Output: {outputPath}\n" +
                        $"Total size: {FormatFileSize(result.TotalOutputSize)}\n" +
                        $"Duration: {result.Duration.TotalSeconds:F1} seconds",
                        "Packing Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    label_status.Text = TF("Failed: {0}", result.ErrorMessage);
                    MessageBox.Show(
                        $"Packing failed: {result.ErrorMessage}",
                        "Packing Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                label_status.Text = T("Cancelled");
                MessageBox.Show(T("Packing was cancelled."), T("Cancelled"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                label_status.Text = TF("Error: {0}", ex.Message);
                MessageBox.Show(TF("Error during packing: {0}", ex.Message), T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isPacking = false;
            button_pack.Content = UiLocalization.Translate("Pack");
                checkedListBox_categories.IsEnabled = true;
                textBox_outputPath.IsEnabled = true;
                button_browse.IsEnabled = true;
                checkBox_betaFormat.IsEnabled = true;
                numericUpDown_patchVersion.IsEnabled = true;
                comboBox_encryption.IsEnabled = true;
                // Re-enable format options based on current checkbox state
                UpdateFormatOptionsState();
            }
        }

        private void UpdateProgress(PackingProgress progress)
        {
            progressBar.Value = (int)Math.Min(progress.ProgressPercentage, 100);
            label_status.Text = $"{progress.CurrentPhase}: {progress.CurrentFile ?? ""} ({progress.ProcessedFiles}/{progress.TotalFiles})";
        }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            if (_isPacking)
            {
                _cancellationTokenSource.Cancel();
            }
            else
            {
                Close();
            }
        }

        private void PackToWzForm_FormClosing(object sender, CancelEventArgs e)
        {
            if (_isPacking)
            {
                e.Cancel = true;
                MessageBox.Show(T("Please wait for packing to complete or cancel it first."),
                    T("Packing in Progress"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                _cancellationTokenSource.Dispose();
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:F2} {sizes[order]}";
        }
    }

    /// <summary>
    /// Helper class for encryption dropdown items.
    /// </summary>
    internal class EncryptionItem
    {
        public WzMapleVersion Encryption { get; }
        public string DisplayName { get; }

        public EncryptionItem(WzMapleVersion encryption, string displayName)
        {
            Encryption = encryption;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }
}
