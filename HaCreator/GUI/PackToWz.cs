/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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
                label_versionInfo.Text = $"Version: {_versionInfo.DisplayName ?? _versionInfo.Version}";
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
            this.checkedListBox_categories = new System.Windows.Forms.CheckedListBox();
            this.label_categories = new System.Windows.Forms.Label();
            this.label_outputPath = new System.Windows.Forms.Label();
            this.textBox_outputPath = new System.Windows.Forms.TextBox();
            this.button_browse = new System.Windows.Forms.Button();
            this.button_pack = new System.Windows.Forms.Button();
            this.button_cancel = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.label_status = new System.Windows.Forms.Label();
            this.checkBox_64bit = new System.Windows.Forms.CheckBox();
            this.label_versionInfo = new System.Windows.Forms.Label();
            this.button_selectAll = new System.Windows.Forms.Button();
            this.button_selectNone = new System.Windows.Forms.Button();
            this.label_patchVersion = new System.Windows.Forms.Label();
            this.numericUpDown_patchVersion = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_patchVersion)).BeginInit();
            this.SuspendLayout();
            //
            // label_versionInfo
            //
            this.label_versionInfo.AutoSize = true;
            this.label_versionInfo.Location = new System.Drawing.Point(12, 9);
            this.label_versionInfo.Name = "label_versionInfo";
            this.label_versionInfo.Size = new System.Drawing.Size(100, 15);
            this.label_versionInfo.TabIndex = 0;
            this.label_versionInfo.Text = "Version: Unknown";
            //
            // label_categories
            //
            this.label_categories.AutoSize = true;
            this.label_categories.Location = new System.Drawing.Point(12, 35);
            this.label_categories.Name = "label_categories";
            this.label_categories.Size = new System.Drawing.Size(120, 15);
            this.label_categories.TabIndex = 1;
            this.label_categories.Text = "Categories to pack:";
            //
            // checkedListBox_categories
            //
            this.checkedListBox_categories.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.checkedListBox_categories.CheckOnClick = true;
            this.checkedListBox_categories.FormattingEnabled = true;
            this.checkedListBox_categories.Location = new System.Drawing.Point(12, 53);
            this.checkedListBox_categories.Name = "checkedListBox_categories";
            this.checkedListBox_categories.Size = new System.Drawing.Size(360, 184);
            this.checkedListBox_categories.TabIndex = 2;
            //
            // button_selectAll
            //
            this.button_selectAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button_selectAll.Location = new System.Drawing.Point(216, 243);
            this.button_selectAll.Name = "button_selectAll";
            this.button_selectAll.Size = new System.Drawing.Size(75, 23);
            this.button_selectAll.TabIndex = 3;
            this.button_selectAll.Text = "Select All";
            this.button_selectAll.UseVisualStyleBackColor = true;
            this.button_selectAll.Click += new System.EventHandler(this.button_selectAll_Click);
            //
            // button_selectNone
            //
            this.button_selectNone.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button_selectNone.Location = new System.Drawing.Point(297, 243);
            this.button_selectNone.Name = "button_selectNone";
            this.button_selectNone.Size = new System.Drawing.Size(75, 23);
            this.button_selectNone.TabIndex = 4;
            this.button_selectNone.Text = "Select None";
            this.button_selectNone.UseVisualStyleBackColor = true;
            this.button_selectNone.Click += new System.EventHandler(this.button_selectNone_Click);
            //
            // label_outputPath
            //
            this.label_outputPath.AutoSize = true;
            this.label_outputPath.Location = new System.Drawing.Point(12, 275);
            this.label_outputPath.Name = "label_outputPath";
            this.label_outputPath.Size = new System.Drawing.Size(75, 15);
            this.label_outputPath.TabIndex = 5;
            this.label_outputPath.Text = "Output path:";
            //
            // textBox_outputPath
            //
            this.textBox_outputPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_outputPath.Location = new System.Drawing.Point(12, 293);
            this.textBox_outputPath.Name = "textBox_outputPath";
            this.textBox_outputPath.Size = new System.Drawing.Size(279, 23);
            this.textBox_outputPath.TabIndex = 6;
            //
            // button_browse
            //
            this.button_browse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button_browse.Location = new System.Drawing.Point(297, 293);
            this.button_browse.Name = "button_browse";
            this.button_browse.Size = new System.Drawing.Size(75, 23);
            this.button_browse.TabIndex = 7;
            this.button_browse.Text = "Browse...";
            this.button_browse.UseVisualStyleBackColor = true;
            this.button_browse.Click += new System.EventHandler(this.button_browse_Click);
            //
            // checkBox_64bit
            //
            this.checkBox_64bit.AutoSize = true;
            this.checkBox_64bit.Location = new System.Drawing.Point(12, 322);
            this.checkBox_64bit.Name = "checkBox_64bit";
            this.checkBox_64bit.Size = new System.Drawing.Size(170, 19);
            this.checkBox_64bit.TabIndex = 8;
            this.checkBox_64bit.Text = "Save as 64-bit WZ format";
            this.checkBox_64bit.UseVisualStyleBackColor = true;
            //
            // label_patchVersion
            //
            this.label_patchVersion.AutoSize = true;
            this.label_patchVersion.Location = new System.Drawing.Point(12, 350);
            this.label_patchVersion.Name = "label_patchVersion";
            this.label_patchVersion.Size = new System.Drawing.Size(85, 15);
            this.label_patchVersion.TabIndex = 13;
            this.label_patchVersion.Text = "Patch Version:";
            //
            // numericUpDown_patchVersion
            //
            this.numericUpDown_patchVersion.Location = new System.Drawing.Point(103, 348);
            this.numericUpDown_patchVersion.Maximum = new decimal(new int[] { 32767, 0, 0, 0 });
            this.numericUpDown_patchVersion.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numericUpDown_patchVersion.Name = "numericUpDown_patchVersion";
            this.numericUpDown_patchVersion.Size = new System.Drawing.Size(80, 23);
            this.numericUpDown_patchVersion.TabIndex = 14;
            this.numericUpDown_patchVersion.Value = new decimal(new int[] { 1, 0, 0, 0 });
            //
            // progressBar
            //
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(12, 380);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(360, 23);
            this.progressBar.TabIndex = 9;
            //
            // label_status
            //
            this.label_status.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label_status.Location = new System.Drawing.Point(12, 406);
            this.label_status.Name = "label_status";
            this.label_status.Size = new System.Drawing.Size(360, 15);
            this.label_status.TabIndex = 10;
            this.label_status.Text = "Ready";
            //
            // button_pack
            //
            this.button_pack.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button_pack.Location = new System.Drawing.Point(216, 430);
            this.button_pack.Name = "button_pack";
            this.button_pack.Size = new System.Drawing.Size(75, 23);
            this.button_pack.TabIndex = 11;
            this.button_pack.Text = "Pack";
            this.button_pack.UseVisualStyleBackColor = true;
            this.button_pack.Click += new System.EventHandler(this.button_pack_Click);
            //
            // button_cancel
            //
            this.button_cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button_cancel.Location = new System.Drawing.Point(297, 430);
            this.button_cancel.Name = "button_cancel";
            this.button_cancel.Size = new System.Drawing.Size(75, 23);
            this.button_cancel.TabIndex = 12;
            this.button_cancel.Text = "Close";
            this.button_cancel.UseVisualStyleBackColor = true;
            this.button_cancel.Click += new System.EventHandler(this.button_cancel_Click);
            //
            // PackToWz
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 461);
            this.Controls.Add(this.numericUpDown_patchVersion);
            this.Controls.Add(this.label_patchVersion);
            this.Controls.Add(this.button_cancel);
            this.Controls.Add(this.button_pack);
            this.Controls.Add(this.label_status);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.checkBox_64bit);
            this.Controls.Add(this.button_browse);
            this.Controls.Add(this.textBox_outputPath);
            this.Controls.Add(this.label_outputPath);
            this.Controls.Add(this.button_selectNone);
            this.Controls.Add(this.button_selectAll);
            this.Controls.Add(this.checkedListBox_categories);
            this.Controls.Add(this.label_categories);
            this.Controls.Add(this.label_versionInfo);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PackToWz";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Pack IMG Files to WZ";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PackToWz_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_patchVersion)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.CheckedListBox checkedListBox_categories;
        private System.Windows.Forms.Label label_categories;
        private System.Windows.Forms.Label label_outputPath;
        private System.Windows.Forms.TextBox textBox_outputPath;
        private System.Windows.Forms.Button button_browse;
        private System.Windows.Forms.Button button_pack;
        private System.Windows.Forms.Button button_cancel;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label label_status;
        private System.Windows.Forms.CheckBox checkBox_64bit;
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
                    (short)numericUpDown_patchVersion.Value);

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
