namespace HaCreator.GUI
{
    partial class DataSourceSettings
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            groupBox_mode = new System.Windows.Forms.GroupBox();
            label_modeDescription = new System.Windows.Forms.Label();
            radioButton_hybridMode = new System.Windows.Forms.RadioButton();
            radioButton_wzMode = new System.Windows.Forms.RadioButton();
            radioButton_imgMode = new System.Windows.Forms.RadioButton();
            groupBox_paths = new System.Windows.Forms.GroupBox();
            button_browseWz = new System.Windows.Forms.Button();
            textBox_wzPath = new System.Windows.Forms.TextBox();
            label_wzPath = new System.Windows.Forms.Label();
            groupBox_cache = new System.Windows.Forms.GroupBox();
            checkBox_memoryMappedFiles = new System.Windows.Forms.CheckBox();
            numericUpDown_maxImages = new System.Windows.Forms.NumericUpDown();
            label_maxImages = new System.Windows.Forms.Label();
            numericUpDown_maxMemory = new System.Windows.Forms.NumericUpDown();
            label_maxMemory = new System.Windows.Forms.Label();
            groupBox_legacy = new System.Windows.Forms.GroupBox();
            checkBox_autoConvert = new System.Windows.Forms.CheckBox();
            checkBox_allowWzFallback = new System.Windows.Forms.CheckBox();
            groupBox_extraction = new System.Windows.Forms.GroupBox();
            checkBox_validateAfterExtract = new System.Windows.Forms.CheckBox();
            checkBox_generateIndex = new System.Windows.Forms.CheckBox();
            numericUpDown_parallelThreads = new System.Windows.Forms.NumericUpDown();
            label_parallelThreads = new System.Windows.Forms.Label();
            button_ok = new System.Windows.Forms.Button();
            button_cancel = new System.Windows.Forms.Button();
            button_resetDefaults = new System.Windows.Forms.Button();
            groupBox_mode.SuspendLayout();
            groupBox_paths.SuspendLayout();
            groupBox_cache.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_maxImages).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_maxMemory).BeginInit();
            groupBox_legacy.SuspendLayout();
            groupBox_extraction.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_parallelThreads).BeginInit();
            SuspendLayout();
            //
            // groupBox_mode
            //
            groupBox_mode.Controls.Add(label_modeDescription);
            groupBox_mode.Controls.Add(radioButton_hybridMode);
            groupBox_mode.Controls.Add(radioButton_wzMode);
            groupBox_mode.Controls.Add(radioButton_imgMode);
            groupBox_mode.Location = new System.Drawing.Point(12, 12);
            groupBox_mode.Name = "groupBox_mode";
            groupBox_mode.Size = new System.Drawing.Size(460, 115);
            groupBox_mode.TabIndex = 0;
            groupBox_mode.TabStop = false;
            groupBox_mode.Text = "Data Source Mode";
            //
            // label_modeDescription
            //
            label_modeDescription.ForeColor = System.Drawing.SystemColors.GrayText;
            label_modeDescription.Location = new System.Drawing.Point(140, 22);
            label_modeDescription.Name = "label_modeDescription";
            label_modeDescription.Size = new System.Drawing.Size(310, 80);
            label_modeDescription.TabIndex = 3;
            label_modeDescription.Text = "IMG Filesystem Mode (Recommended)";
            //
            // radioButton_hybridMode
            //
            radioButton_hybridMode.AutoSize = true;
            radioButton_hybridMode.Location = new System.Drawing.Point(15, 78);
            radioButton_hybridMode.Name = "radioButton_hybridMode";
            radioButton_hybridMode.Size = new System.Drawing.Size(100, 19);
            radioButton_hybridMode.TabIndex = 2;
            radioButton_hybridMode.Text = "Hybrid Mode";
            radioButton_hybridMode.UseVisualStyleBackColor = true;
            radioButton_hybridMode.CheckedChanged += radioButton_mode_CheckedChanged;
            //
            // radioButton_wzMode
            //
            radioButton_wzMode.AutoSize = true;
            radioButton_wzMode.Location = new System.Drawing.Point(15, 53);
            radioButton_wzMode.Name = "radioButton_wzMode";
            radioButton_wzMode.Size = new System.Drawing.Size(115, 19);
            radioButton_wzMode.TabIndex = 1;
            radioButton_wzMode.Text = "WZ Files (Legacy)";
            radioButton_wzMode.UseVisualStyleBackColor = true;
            radioButton_wzMode.CheckedChanged += radioButton_mode_CheckedChanged;
            //
            // radioButton_imgMode
            //
            radioButton_imgMode.AutoSize = true;
            radioButton_imgMode.Checked = true;
            radioButton_imgMode.Location = new System.Drawing.Point(15, 28);
            radioButton_imgMode.Name = "radioButton_imgMode";
            radioButton_imgMode.Size = new System.Drawing.Size(119, 19);
            radioButton_imgMode.TabIndex = 0;
            radioButton_imgMode.TabStop = true;
            radioButton_imgMode.Text = "IMG Filesystem";
            radioButton_imgMode.UseVisualStyleBackColor = true;
            radioButton_imgMode.CheckedChanged += radioButton_mode_CheckedChanged;
            //
            // groupBox_paths
            //
            groupBox_paths.Controls.Add(button_browseWz);
            groupBox_paths.Controls.Add(textBox_wzPath);
            groupBox_paths.Controls.Add(label_wzPath);
            groupBox_paths.Location = new System.Drawing.Point(12, 133);
            groupBox_paths.Name = "groupBox_paths";
            groupBox_paths.Size = new System.Drawing.Size(460, 60);
            groupBox_paths.TabIndex = 1;
            groupBox_paths.TabStop = false;
            groupBox_paths.Text = "Paths";
            //
            // button_browseWz
            //
            button_browseWz.Location = new System.Drawing.Point(379, 26);
            button_browseWz.Name = "button_browseWz";
            button_browseWz.Size = new System.Drawing.Size(75, 23);
            button_browseWz.TabIndex = 2;
            button_browseWz.Text = "Browse...";
            button_browseWz.UseVisualStyleBackColor = true;
            button_browseWz.Click += button_browseWz_Click;
            //
            // textBox_wzPath
            //
            textBox_wzPath.Location = new System.Drawing.Point(110, 26);
            textBox_wzPath.Name = "textBox_wzPath";
            textBox_wzPath.Size = new System.Drawing.Size(263, 23);
            textBox_wzPath.TabIndex = 1;
            //
            // label_wzPath
            //
            label_wzPath.AutoSize = true;
            label_wzPath.Location = new System.Drawing.Point(15, 29);
            label_wzPath.Name = "label_wzPath";
            label_wzPath.Size = new System.Drawing.Size(89, 15);
            label_wzPath.TabIndex = 0;
            label_wzPath.Text = "WZ Files Path:";
            //
            // groupBox_cache
            //
            groupBox_cache.Controls.Add(checkBox_memoryMappedFiles);
            groupBox_cache.Controls.Add(numericUpDown_maxImages);
            groupBox_cache.Controls.Add(label_maxImages);
            groupBox_cache.Controls.Add(numericUpDown_maxMemory);
            groupBox_cache.Controls.Add(label_maxMemory);
            groupBox_cache.Location = new System.Drawing.Point(12, 199);
            groupBox_cache.Name = "groupBox_cache";
            groupBox_cache.Size = new System.Drawing.Size(225, 100);
            groupBox_cache.TabIndex = 2;
            groupBox_cache.TabStop = false;
            groupBox_cache.Text = "Cache Settings";
            //
            // checkBox_memoryMappedFiles
            //
            checkBox_memoryMappedFiles.AutoSize = true;
            checkBox_memoryMappedFiles.Location = new System.Drawing.Point(15, 72);
            checkBox_memoryMappedFiles.Name = "checkBox_memoryMappedFiles";
            checkBox_memoryMappedFiles.Size = new System.Drawing.Size(172, 19);
            checkBox_memoryMappedFiles.TabIndex = 4;
            checkBox_memoryMappedFiles.Text = "Use memory-mapped files";
            checkBox_memoryMappedFiles.UseVisualStyleBackColor = true;
            //
            // numericUpDown_maxImages
            //
            numericUpDown_maxImages.Location = new System.Drawing.Point(135, 43);
            numericUpDown_maxImages.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            numericUpDown_maxImages.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
            numericUpDown_maxImages.Name = "numericUpDown_maxImages";
            numericUpDown_maxImages.Size = new System.Drawing.Size(80, 23);
            numericUpDown_maxImages.TabIndex = 3;
            numericUpDown_maxImages.Value = new decimal(new int[] { 1000, 0, 0, 0 });
            //
            // label_maxImages
            //
            label_maxImages.AutoSize = true;
            label_maxImages.Location = new System.Drawing.Point(15, 45);
            label_maxImages.Name = "label_maxImages";
            label_maxImages.Size = new System.Drawing.Size(114, 15);
            label_maxImages.TabIndex = 2;
            label_maxImages.Text = "Max Cached Images:";
            //
            // numericUpDown_maxMemory
            //
            numericUpDown_maxMemory.Location = new System.Drawing.Point(135, 18);
            numericUpDown_maxMemory.Maximum = new decimal(new int[] { 4096, 0, 0, 0 });
            numericUpDown_maxMemory.Minimum = new decimal(new int[] { 64, 0, 0, 0 });
            numericUpDown_maxMemory.Name = "numericUpDown_maxMemory";
            numericUpDown_maxMemory.Size = new System.Drawing.Size(80, 23);
            numericUpDown_maxMemory.TabIndex = 1;
            numericUpDown_maxMemory.Value = new decimal(new int[] { 512, 0, 0, 0 });
            //
            // label_maxMemory
            //
            label_maxMemory.AutoSize = true;
            label_maxMemory.Location = new System.Drawing.Point(15, 20);
            label_maxMemory.Name = "label_maxMemory";
            label_maxMemory.Size = new System.Drawing.Size(109, 15);
            label_maxMemory.TabIndex = 0;
            label_maxMemory.Text = "Max Memory (MB):";
            //
            // groupBox_legacy
            //
            groupBox_legacy.Controls.Add(checkBox_autoConvert);
            groupBox_legacy.Controls.Add(checkBox_allowWzFallback);
            groupBox_legacy.Location = new System.Drawing.Point(247, 199);
            groupBox_legacy.Name = "groupBox_legacy";
            groupBox_legacy.Size = new System.Drawing.Size(225, 72);
            groupBox_legacy.TabIndex = 3;
            groupBox_legacy.TabStop = false;
            groupBox_legacy.Text = "Legacy Options";
            //
            // checkBox_autoConvert
            //
            checkBox_autoConvert.AutoSize = true;
            checkBox_autoConvert.Location = new System.Drawing.Point(15, 47);
            checkBox_autoConvert.Name = "checkBox_autoConvert";
            checkBox_autoConvert.Size = new System.Drawing.Size(180, 19);
            checkBox_autoConvert.TabIndex = 1;
            checkBox_autoConvert.Text = "Auto-convert WZ to IMG";
            checkBox_autoConvert.UseVisualStyleBackColor = true;
            //
            // checkBox_allowWzFallback
            //
            checkBox_allowWzFallback.AutoSize = true;
            checkBox_allowWzFallback.Location = new System.Drawing.Point(15, 22);
            checkBox_allowWzFallback.Name = "checkBox_allowWzFallback";
            checkBox_allowWzFallback.Size = new System.Drawing.Size(168, 19);
            checkBox_allowWzFallback.TabIndex = 0;
            checkBox_allowWzFallback.Text = "Allow WZ fallback (Hybrid)";
            checkBox_allowWzFallback.UseVisualStyleBackColor = true;
            //
            // groupBox_extraction
            //
            groupBox_extraction.Controls.Add(checkBox_validateAfterExtract);
            groupBox_extraction.Controls.Add(checkBox_generateIndex);
            groupBox_extraction.Controls.Add(numericUpDown_parallelThreads);
            groupBox_extraction.Controls.Add(label_parallelThreads);
            groupBox_extraction.Location = new System.Drawing.Point(247, 277);
            groupBox_extraction.Name = "groupBox_extraction";
            groupBox_extraction.Size = new System.Drawing.Size(225, 100);
            groupBox_extraction.TabIndex = 4;
            groupBox_extraction.TabStop = false;
            groupBox_extraction.Text = "Extraction Settings";
            //
            // checkBox_validateAfterExtract
            //
            checkBox_validateAfterExtract.AutoSize = true;
            checkBox_validateAfterExtract.Location = new System.Drawing.Point(15, 72);
            checkBox_validateAfterExtract.Name = "checkBox_validateAfterExtract";
            checkBox_validateAfterExtract.Size = new System.Drawing.Size(139, 19);
            checkBox_validateAfterExtract.TabIndex = 3;
            checkBox_validateAfterExtract.Text = "Validate after extract";
            checkBox_validateAfterExtract.UseVisualStyleBackColor = true;
            //
            // checkBox_generateIndex
            //
            checkBox_generateIndex.AutoSize = true;
            checkBox_generateIndex.Location = new System.Drawing.Point(15, 47);
            checkBox_generateIndex.Name = "checkBox_generateIndex";
            checkBox_generateIndex.Size = new System.Drawing.Size(139, 19);
            checkBox_generateIndex.TabIndex = 2;
            checkBox_generateIndex.Text = "Generate index files";
            checkBox_generateIndex.UseVisualStyleBackColor = true;
            //
            // numericUpDown_parallelThreads
            //
            numericUpDown_parallelThreads.Location = new System.Drawing.Point(110, 18);
            numericUpDown_parallelThreads.Maximum = new decimal(new int[] { 16, 0, 0, 0 });
            numericUpDown_parallelThreads.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericUpDown_parallelThreads.Name = "numericUpDown_parallelThreads";
            numericUpDown_parallelThreads.Size = new System.Drawing.Size(60, 23);
            numericUpDown_parallelThreads.TabIndex = 1;
            numericUpDown_parallelThreads.Value = new decimal(new int[] { 4, 0, 0, 0 });
            //
            // label_parallelThreads
            //
            label_parallelThreads.AutoSize = true;
            label_parallelThreads.Location = new System.Drawing.Point(15, 20);
            label_parallelThreads.Name = "label_parallelThreads";
            label_parallelThreads.Size = new System.Drawing.Size(89, 15);
            label_parallelThreads.TabIndex = 0;
            label_parallelThreads.Text = "Parallel Threads:";
            //
            // button_ok
            //
            button_ok.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            button_ok.Location = new System.Drawing.Point(290, 390);
            button_ok.Name = "button_ok";
            button_ok.Size = new System.Drawing.Size(90, 28);
            button_ok.TabIndex = 5;
            button_ok.Text = "Save";
            button_ok.UseVisualStyleBackColor = true;
            button_ok.Click += button_ok_Click;
            //
            // button_cancel
            //
            button_cancel.Location = new System.Drawing.Point(386, 390);
            button_cancel.Name = "button_cancel";
            button_cancel.Size = new System.Drawing.Size(90, 28);
            button_cancel.TabIndex = 6;
            button_cancel.Text = "Cancel";
            button_cancel.UseVisualStyleBackColor = true;
            button_cancel.Click += button_cancel_Click;
            //
            // button_resetDefaults
            //
            button_resetDefaults.Location = new System.Drawing.Point(12, 390);
            button_resetDefaults.Name = "button_resetDefaults";
            button_resetDefaults.Size = new System.Drawing.Size(100, 28);
            button_resetDefaults.TabIndex = 7;
            button_resetDefaults.Text = "Reset Defaults";
            button_resetDefaults.UseVisualStyleBackColor = true;
            button_resetDefaults.Click += button_resetDefaults_Click;
            //
            // DataSourceSettings
            //
            AcceptButton = button_ok;
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            CancelButton = button_cancel;
            ClientSize = new System.Drawing.Size(484, 430);
            Controls.Add(groupBox_mode);
            Controls.Add(groupBox_paths);
            Controls.Add(groupBox_cache);
            Controls.Add(groupBox_legacy);
            Controls.Add(groupBox_extraction);
            Controls.Add(button_ok);
            Controls.Add(button_cancel);
            Controls.Add(button_resetDefaults);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "DataSourceSettings";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Data Source Settings";
            Load += DataSourceSettings_Load;
            groupBox_mode.ResumeLayout(false);
            groupBox_mode.PerformLayout();
            groupBox_paths.ResumeLayout(false);
            groupBox_paths.PerformLayout();
            groupBox_cache.ResumeLayout(false);
            groupBox_cache.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_maxImages).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_maxMemory).EndInit();
            groupBox_legacy.ResumeLayout(false);
            groupBox_legacy.PerformLayout();
            groupBox_extraction.ResumeLayout(false);
            groupBox_extraction.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_parallelThreads).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox_mode;
        private System.Windows.Forms.Label label_modeDescription;
        private System.Windows.Forms.RadioButton radioButton_hybridMode;
        private System.Windows.Forms.RadioButton radioButton_wzMode;
        private System.Windows.Forms.RadioButton radioButton_imgMode;
        private System.Windows.Forms.GroupBox groupBox_paths;
        private System.Windows.Forms.Button button_browseWz;
        private System.Windows.Forms.TextBox textBox_wzPath;
        private System.Windows.Forms.Label label_wzPath;
        private System.Windows.Forms.GroupBox groupBox_cache;
        private System.Windows.Forms.CheckBox checkBox_memoryMappedFiles;
        private System.Windows.Forms.NumericUpDown numericUpDown_maxImages;
        private System.Windows.Forms.Label label_maxImages;
        private System.Windows.Forms.NumericUpDown numericUpDown_maxMemory;
        private System.Windows.Forms.Label label_maxMemory;
        private System.Windows.Forms.GroupBox groupBox_legacy;
        private System.Windows.Forms.CheckBox checkBox_autoConvert;
        private System.Windows.Forms.CheckBox checkBox_allowWzFallback;
        private System.Windows.Forms.GroupBox groupBox_extraction;
        private System.Windows.Forms.CheckBox checkBox_validateAfterExtract;
        private System.Windows.Forms.CheckBox checkBox_generateIndex;
        private System.Windows.Forms.NumericUpDown numericUpDown_parallelThreads;
        private System.Windows.Forms.Label label_parallelThreads;
        private System.Windows.Forms.Button button_ok;
        private System.Windows.Forms.Button button_cancel;
        private System.Windows.Forms.Button button_resetDefaults;
    }
}
