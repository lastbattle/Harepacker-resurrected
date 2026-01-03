using MapleLib.Img;
using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class PackToWz : Form
    {
        private readonly string _versionPath;
        private readonly VersionInfo _versionInfo;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private GroupBox groupBox1;
        private bool _isPacking = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="versionPath">Path to the IMG filesystem version directory</param>
        public PackToWz(string versionPath)
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
                label_versionInfo.Text = $"{_versionInfo.DisplayName ?? _versionInfo.Version}";
                checkBox_64bit.Checked = _versionInfo.Is64Bit;

                // Auto-fill patch version from manifest
                int patchVersion = _versionInfo.PatchVersion;

                // If patchVersion is not set, try to extract from version string (e.g., "v83", "gms_v230")
                if (patchVersion <= 0 && !string.IsNullOrEmpty(_versionInfo.Version))
                {
                    patchVersion = ExtractVersionNumber(_versionInfo.Version);
                }

                // If still not found, try from display name
                if (patchVersion <= 0 && !string.IsNullOrEmpty(_versionInfo.DisplayName))
                {
                    patchVersion = ExtractVersionNumber(_versionInfo.DisplayName);
                }

                if (patchVersion > 0 && patchVersion <= numericUpDown_patchVersion.Maximum)
                {
                    numericUpDown_patchVersion.Value = patchVersion;
                }
            }
            else
            {
                label_versionInfo.Text = "Version: Unknown (no manifest.json)";
            }
        }

        /// <summary>
        /// Extracts version number from strings like "v83", "gms_v230", "GMS v83 (Pre-Big Bang)"
        /// </summary>
        private int ExtractVersionNumber(string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
                return 0;

            // Look for patterns like "v83", "v230", etc.
            var match = System.Text.RegularExpressions.Regex.Match(versionString, @"v(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int version))
            {
                return version;
            }

            // Try to find any number sequence
            match = System.Text.RegularExpressions.Regex.Match(versionString, @"(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out version))
            {
                return version;
            }

            return 0;
        }

        private void InitializeComponent()
        {
            checkedListBox_categories = new CheckedListBox();
            label_outputPath = new Label();
            textBox_outputPath = new TextBox();
            button_browse = new Button();
            button_pack = new Button();
            button_cancel = new Button();
            progressBar = new ProgressBar();
            label_status = new Label();
            checkBox_64bit = new CheckBox();
            checkBox_separateCanvas = new CheckBox();
            label_versionInfo = new Label();
            button_selectAll = new Button();
            button_selectNone = new Button();
            label_patchVersion = new Label();
            numericUpDown_patchVersion = new NumericUpDown();
            groupBox1 = new GroupBox();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_patchVersion).BeginInit();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // checkedListBox_categories
            // 
            checkedListBox_categories.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            checkedListBox_categories.CheckOnClick = true;
            checkedListBox_categories.FormattingEnabled = true;
            checkedListBox_categories.Location = new System.Drawing.Point(14, 22);
            checkedListBox_categories.Name = "checkedListBox_categories";
            checkedListBox_categories.Size = new System.Drawing.Size(349, 220);
            checkedListBox_categories.TabIndex = 2;
            // 
            // label_outputPath
            // 
            label_outputPath.AutoSize = true;
            label_outputPath.Location = new System.Drawing.Point(12, 275);
            label_outputPath.Name = "label_outputPath";
            label_outputPath.Size = new System.Drawing.Size(75, 15);
            label_outputPath.TabIndex = 5;
            label_outputPath.Text = "Output path:";
            // 
            // textBox_outputPath
            // 
            textBox_outputPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBox_outputPath.Location = new System.Drawing.Point(12, 293);
            textBox_outputPath.Name = "textBox_outputPath";
            textBox_outputPath.Size = new System.Drawing.Size(279, 23);
            textBox_outputPath.TabIndex = 6;
            // 
            // button_browse
            // 
            button_browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_browse.Location = new System.Drawing.Point(297, 293);
            button_browse.Name = "button_browse";
            button_browse.Size = new System.Drawing.Size(75, 23);
            button_browse.TabIndex = 7;
            button_browse.Text = "Browse...";
            button_browse.UseVisualStyleBackColor = true;
            button_browse.Click += button_browse_Click;
            // 
            // button_pack
            // 
            button_pack.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            button_pack.Location = new System.Drawing.Point(216, 452);
            button_pack.Name = "button_pack";
            button_pack.Size = new System.Drawing.Size(75, 23);
            button_pack.TabIndex = 11;
            button_pack.Text = "Pack";
            button_pack.UseVisualStyleBackColor = true;
            button_pack.Click += button_pack_Click;
            // 
            // button_cancel
            // 
            button_cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            button_cancel.Location = new System.Drawing.Point(297, 452);
            button_cancel.Name = "button_cancel";
            button_cancel.Size = new System.Drawing.Size(75, 23);
            button_cancel.TabIndex = 12;
            button_cancel.Text = "Close";
            button_cancel.UseVisualStyleBackColor = true;
            button_cancel.Click += button_cancel_Click;
            // 
            // progressBar
            // 
            progressBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            progressBar.Location = new System.Drawing.Point(12, 402);
            progressBar.Name = "progressBar";
            progressBar.Size = new System.Drawing.Size(360, 23);
            progressBar.TabIndex = 9;
            // 
            // label_status
            // 
            label_status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label_status.Location = new System.Drawing.Point(12, 428);
            label_status.Name = "label_status";
            label_status.Size = new System.Drawing.Size(360, 15);
            label_status.TabIndex = 10;
            label_status.Text = "Ready";
            // 
            // checkBox_64bit
            // 
            checkBox_64bit.AutoSize = true;
            checkBox_64bit.Location = new System.Drawing.Point(12, 322);
            checkBox_64bit.Name = "checkBox_64bit";
            checkBox_64bit.Size = new System.Drawing.Size(158, 19);
            checkBox_64bit.TabIndex = 8;
            checkBox_64bit.Text = "Save as 64-bit WZ format";
            checkBox_64bit.UseVisualStyleBackColor = true;
            checkBox_64bit.CheckedChanged += checkBox_64bit_CheckedChanged;
            // 
            // checkBox_separateCanvas
            // 
            checkBox_separateCanvas.AutoSize = true;
            checkBox_separateCanvas.Enabled = false;
            checkBox_separateCanvas.Location = new System.Drawing.Point(30, 344);
            checkBox_separateCanvas.Name = "checkBox_separateCanvas";
            checkBox_separateCanvas.Size = new System.Drawing.Size(231, 19);
            checkBox_separateCanvas.TabIndex = 15;
            checkBox_separateCanvas.Text = "Save images in separate _Canvas folder";
            checkBox_separateCanvas.UseVisualStyleBackColor = true;
            // 
            // label_versionInfo
            // 
            label_versionInfo.AutoSize = true;
            label_versionInfo.Location = new System.Drawing.Point(189, 372);
            label_versionInfo.Name = "label_versionInfo";
            label_versionInfo.Size = new System.Drawing.Size(58, 15);
            label_versionInfo.TabIndex = 0;
            label_versionInfo.Text = "Unknown";
            // 
            // button_selectAll
            // 
            button_selectAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_selectAll.Location = new System.Drawing.Point(214, 242);
            button_selectAll.Name = "button_selectAll";
            button_selectAll.Size = new System.Drawing.Size(75, 23);
            button_selectAll.TabIndex = 3;
            button_selectAll.Text = "Select All";
            button_selectAll.UseVisualStyleBackColor = true;
            button_selectAll.Click += button_selectAll_Click;
            // 
            // button_selectNone
            // 
            button_selectNone.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_selectNone.Location = new System.Drawing.Point(288, 242);
            button_selectNone.Name = "button_selectNone";
            button_selectNone.Size = new System.Drawing.Size(75, 23);
            button_selectNone.TabIndex = 4;
            button_selectNone.Text = "Select None";
            button_selectNone.UseVisualStyleBackColor = true;
            button_selectNone.Click += button_selectNone_Click;
            // 
            // label_patchVersion
            // 
            label_patchVersion.AutoSize = true;
            label_patchVersion.Location = new System.Drawing.Point(12, 372);
            label_patchVersion.Name = "label_patchVersion";
            label_patchVersion.Size = new System.Drawing.Size(81, 15);
            label_patchVersion.TabIndex = 13;
            label_patchVersion.Text = "Patch Version:";
            // 
            // numericUpDown_patchVersion
            // 
            numericUpDown_patchVersion.Location = new System.Drawing.Point(103, 370);
            numericUpDown_patchVersion.Maximum = new decimal(new int[] { 32767, 0, 0, 0 });
            numericUpDown_patchVersion.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericUpDown_patchVersion.Name = "numericUpDown_patchVersion";
            numericUpDown_patchVersion.Size = new System.Drawing.Size(80, 23);
            numericUpDown_patchVersion.TabIndex = 14;
            numericUpDown_patchVersion.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(checkedListBox_categories);
            groupBox1.Controls.Add(button_selectAll);
            groupBox1.Controls.Add(button_selectNone);
            groupBox1.Location = new System.Drawing.Point(2, 1);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(378, 271);
            groupBox1.TabIndex = 16;
            groupBox1.TabStop = false;
            groupBox1.Text = "Categories to pack:";
            // 
            // PackToWz
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(384, 485);
            Controls.Add(groupBox1);
            Controls.Add(numericUpDown_patchVersion);
            Controls.Add(label_patchVersion);
            Controls.Add(button_cancel);
            Controls.Add(button_pack);
            Controls.Add(label_status);
            Controls.Add(progressBar);
            Controls.Add(checkBox_separateCanvas);
            Controls.Add(checkBox_64bit);
            Controls.Add(button_browse);
            Controls.Add(textBox_outputPath);
            Controls.Add(label_outputPath);
            Controls.Add(label_versionInfo);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "PackToWz";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Pack IMG Files to WZ";
            FormClosing += PackToWz_FormClosing;
            ((System.ComponentModel.ISupportInitialize)numericUpDown_patchVersion).EndInit();
            groupBox1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        private System.Windows.Forms.CheckedListBox checkedListBox_categories;
        private System.Windows.Forms.Label label_outputPath;
        private System.Windows.Forms.TextBox textBox_outputPath;
        private System.Windows.Forms.Button button_browse;
        private System.Windows.Forms.Button button_pack;
        private System.Windows.Forms.Button button_cancel;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label label_status;
        private System.Windows.Forms.CheckBox checkBox_64bit;
        private System.Windows.Forms.CheckBox checkBox_separateCanvas;
        private System.Windows.Forms.Label label_versionInfo;
        private System.Windows.Forms.Button button_selectAll;
        private System.Windows.Forms.Button button_selectNone;
        private System.Windows.Forms.Label label_patchVersion;
        private System.Windows.Forms.NumericUpDown numericUpDown_patchVersion;

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

                    // Add if it has .img files OR subdirectories (for structures like Base.wz)
                    if (imgCount > 0 || subDirCount > 0)
                    {
                        string displayName = imgCount > 0
                            ? $"{category} ({imgCount} images)"
                            : $"{category} (directory structure)";
                        checkedListBox_categories.Items.Add(displayName, true);
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

                if (imgCount > 0 || subDirCount > 0)
                {
                    string displayName = imgCount > 0
                        ? $"{dirName} ({imgCount} images)"
                        : $"{dirName} (directory structure)";
                    checkedListBox_categories.Items.Add(displayName, true);
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

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    textBox_outputPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void checkBox_64bit_CheckedChanged(object sender, EventArgs e)
        {
            // Only enable separate canvas option when 64-bit is checked
            checkBox_separateCanvas.Enabled = checkBox_64bit.Checked;
            if (!checkBox_64bit.Checked)
            {
                checkBox_separateCanvas.Checked = false;
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
                MessageBox.Show("Please select at least one category to pack.", "No Categories Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputPath = textBox_outputPath.Text;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                MessageBox.Show("Please specify an output path.", "Output Path Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Start packing
            _isPacking = true;
            button_pack.Text = "Cancel";
            checkedListBox_categories.Enabled = false;
            textBox_outputPath.Enabled = false;
            button_browse.Enabled = false;
            checkBox_64bit.Enabled = false;
            checkBox_separateCanvas.Enabled = false;
            numericUpDown_patchVersion.Enabled = false;

            try
            {
                var packingService = new WzPackingService();

                var progress = new Progress<PackingProgress>(p =>
                {
                    if (InvokeRequired)
                    {
                        Invoke(() => UpdateProgress(p));
                    }
                    else
                    {
                        UpdateProgress(p);
                    }
                });

                var result = await packingService.PackCategoriesAsync(
                    _versionPath,
                    outputPath,
                    selectedCategories,
                    checkBox_64bit.Checked,
                    _cancellationTokenSource.Token,
                    progress,
                    (short)numericUpDown_patchVersion.Value,
                    checkBox_separateCanvas.Checked);

                if (result.Success)
                {
                    label_status.Text = $"Completed! Packed {result.TotalImagesPacked} images.";
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
                    label_status.Text = $"Failed: {result.ErrorMessage}";
                    MessageBox.Show(
                        $"Packing failed: {result.ErrorMessage}",
                        "Packing Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                label_status.Text = "Cancelled";
                MessageBox.Show("Packing was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                label_status.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Error during packing: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isPacking = false;
                button_pack.Text = "Pack";
                checkedListBox_categories.Enabled = true;
                textBox_outputPath.Enabled = true;
                button_browse.Enabled = true;
                checkBox_64bit.Enabled = true;
                checkBox_separateCanvas.Enabled = checkBox_64bit.Checked;
                numericUpDown_patchVersion.Enabled = true;
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

        private void PackToWz_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isPacking)
            {
                e.Cancel = true;
                MessageBox.Show("Please wait for packing to complete or cancel it first.",
                    "Packing in Progress", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
}
