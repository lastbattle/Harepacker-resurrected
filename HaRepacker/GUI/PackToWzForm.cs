using MapleLib.Img;
using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaRepacker.GUI
{
    public partial class PackToWzForm : Form
    {
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
                label_versionInfo.Text = $"Version: {_versionInfo.DisplayName ?? _versionInfo.Version}";
                checkBox_64bit.Checked = _versionInfo.Is64Bit;

                // Set format label based on detected format and pre-select beta checkbox if applicable
                if (_versionInfo.IsBetaMs)
                {
                    label_format.Text = "Source: Beta (Single Data.wz)";
                    label_format.ForeColor = System.Drawing.Color.DarkGreen;
                    // Pre-select beta format checkbox (user can uncheck if desired)
                    checkBox_betaFormat.Checked = true;
                }
                else if (_versionInfo.Is64Bit)
                {
                    label_format.Text = "Source: 64-bit (Data folder)";
                    label_format.ForeColor = System.Drawing.Color.DarkBlue;
                }
                else if (_versionInfo.IsPreBB)
                {
                    label_format.Text = "Source: Pre-Big Bang";
                    label_format.ForeColor = System.Drawing.Color.DarkOrange;
                }
                else
                {
                    label_format.Text = "Source: Standard";
                    label_format.ForeColor = System.Drawing.Color.Black;
                }

                // Auto-fill patch version from manifest
                int patchVersion = _versionInfo.PatchVersion;
                if (patchVersion > 0 && patchVersion <= numericUpDown_patchVersion.Maximum)
                {
                    numericUpDown_patchVersion.Value = patchVersion;
                }
            }
            else
            {
                label_versionInfo.Text = "Version: Unknown (no manifest.json)";
                label_format.Text = "Source: Unknown";
                label_format.ForeColor = System.Drawing.Color.Gray;
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

            // Get the encryption from manifest (if available)
            WzMapleVersion manifestEncryption = WzMapleVersion.BMS;
            if (_versionInfo != null && !string.IsNullOrEmpty(_versionInfo.Encryption))
            {
                Enum.TryParse<WzMapleVersion>(_versionInfo.Encryption, out manifestEncryption);
            }

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
            this.label_format = new System.Windows.Forms.Label();
            this.label_patchVersion = new System.Windows.Forms.Label();
            this.numericUpDown_patchVersion = new System.Windows.Forms.NumericUpDown();
            this.checkBox_betaFormat = new System.Windows.Forms.CheckBox();
            this.label_encryption = new System.Windows.Forms.Label();
            this.comboBox_encryption = new System.Windows.Forms.ComboBox();
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
            this.checkBox_64bit.CheckedChanged += new System.EventHandler(this.checkBox_64bit_CheckedChanged);
            //
            // checkBox_betaFormat
            //
            this.checkBox_betaFormat.AutoSize = true;
            this.checkBox_betaFormat.Location = new System.Drawing.Point(200, 322);
            this.checkBox_betaFormat.Name = "checkBox_betaFormat";
            this.checkBox_betaFormat.Size = new System.Drawing.Size(172, 19);
            this.checkBox_betaFormat.TabIndex = 16;
            this.checkBox_betaFormat.Text = "Pack as Beta Data.wz";
            this.checkBox_betaFormat.UseVisualStyleBackColor = true;
            this.checkBox_betaFormat.CheckedChanged += new System.EventHandler(this.checkBox_betaFormat_CheckedChanged);
            //
            // label_patchVersion
            //
            this.label_patchVersion.AutoSize = true;
            this.label_patchVersion.Location = new System.Drawing.Point(12, 350);
            this.label_patchVersion.Name = "label_patchVersion";
            this.label_patchVersion.Size = new System.Drawing.Size(81, 15);
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
            // label_format
            //
            this.label_format.AutoSize = true;
            this.label_format.ForeColor = System.Drawing.Color.DarkBlue;
            this.label_format.Location = new System.Drawing.Point(12, 375);
            this.label_format.Name = "label_format";
            this.label_format.Size = new System.Drawing.Size(180, 15);
            this.label_format.TabIndex = 15;
            this.label_format.Text = "Source: Standard";
            //
            // label_encryption
            //
            this.label_encryption.AutoSize = true;
            this.label_encryption.Location = new System.Drawing.Point(200, 327);
            this.label_encryption.Name = "label_encryption";
            this.label_encryption.Size = new System.Drawing.Size(67, 15);
            this.label_encryption.TabIndex = 17;
            this.label_encryption.Text = "Encryption:";
            //
            // comboBox_encryption
            //
            this.comboBox_encryption.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_encryption.FormattingEnabled = true;
            this.comboBox_encryption.Location = new System.Drawing.Point(200, 348);
            this.comboBox_encryption.Name = "comboBox_encryption";
            this.comboBox_encryption.Size = new System.Drawing.Size(170, 23);
            this.comboBox_encryption.TabIndex = 18;
            //
            // progressBar
            //
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(12, 400);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(360, 23);
            this.progressBar.TabIndex = 9;
            //
            // label_status
            //
            this.label_status.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label_status.Location = new System.Drawing.Point(12, 426);
            this.label_status.Name = "label_status";
            this.label_status.Size = new System.Drawing.Size(360, 15);
            this.label_status.TabIndex = 10;
            this.label_status.Text = "Ready";
            //
            // button_pack
            //
            this.button_pack.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button_pack.Location = new System.Drawing.Point(216, 450);
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
            this.button_cancel.Location = new System.Drawing.Point(297, 450);
            this.button_cancel.Name = "button_cancel";
            this.button_cancel.Size = new System.Drawing.Size(75, 23);
            this.button_cancel.TabIndex = 12;
            this.button_cancel.Text = "Close";
            this.button_cancel.UseVisualStyleBackColor = true;
            this.button_cancel.Click += new System.EventHandler(this.button_cancel_Click);
            //
            // PackToWzForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 481);
            this.Controls.Add(this.button_cancel);
            this.Controls.Add(this.button_pack);
            this.Controls.Add(this.label_status);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.comboBox_encryption);
            this.Controls.Add(this.label_encryption);
            this.Controls.Add(this.label_format);
            this.Controls.Add(this.numericUpDown_patchVersion);
            this.Controls.Add(this.label_patchVersion);
            this.Controls.Add(this.checkBox_betaFormat);
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
            this.Name = "PackToWzForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Pack IMG Files to WZ";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PackToWzForm_FormClosing);
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
        private System.Windows.Forms.Label label_format;
        private System.Windows.Forms.Label label_patchVersion;
        private System.Windows.Forms.NumericUpDown numericUpDown_patchVersion;
        private System.Windows.Forms.CheckBox checkBox_betaFormat;
        private System.Windows.Forms.Label label_encryption;
        private System.Windows.Forms.ComboBox comboBox_encryption;

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
            if (checkBox_betaFormat.Checked)
            {
                // Beta format - disable 64-bit option
                checkBox_64bit.Checked = false;
                checkBox_64bit.Enabled = false;
            }
            else
            {
                // Standard/64-bit format
                checkBox_64bit.Enabled = true;
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
            checkBox_betaFormat.Enabled = false;
            numericUpDown_patchVersion.Enabled = false;
            comboBox_encryption.Enabled = false;

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

                // Get selected encryption
                WzMapleVersion selectedEncryption = GetSelectedEncryption();

                PackingResult result;
                if (checkBox_betaFormat.Checked)
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
                        checkBox_64bit.Checked,
                        _cancellationTokenSource.Token,
                        progress,
                        (short)numericUpDown_patchVersion.Value,
                        false, // separateCanvas
                        selectedEncryption);
                }

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
                checkBox_betaFormat.Enabled = true;
                numericUpDown_patchVersion.Enabled = true;
                comboBox_encryption.Enabled = true;
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

        private void PackToWzForm_FormClosing(object sender, FormClosingEventArgs e)
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
