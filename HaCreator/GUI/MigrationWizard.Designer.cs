namespace HaCreator.GUI
{
    partial class MigrationWizard
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panel_welcome = new System.Windows.Forms.Panel();
            this.label_welcomeDescription = new System.Windows.Forms.Label();
            this.label_welcomeTitle = new System.Windows.Forms.Label();
            this.panel_selectSource = new System.Windows.Forms.Panel();
            this.groupBox_detected = new System.Windows.Forms.GroupBox();
            this.listBox_detected = new System.Windows.Forms.ListBox();
            this.button_scanFolder = new System.Windows.Forms.Button();
            this.groupBox_manual = new System.Windows.Forms.GroupBox();
            this.button_browse = new System.Windows.Forms.Button();
            this.textBox_wzPath = new System.Windows.Forms.TextBox();
            this.label_wzPath = new System.Windows.Forms.Label();
            this.panel_configure = new System.Windows.Forms.Panel();
            this.label_versionError = new System.Windows.Forms.Label();
            this.comboBox_encryption = new System.Windows.Forms.ComboBox();
            this.label_encryption = new System.Windows.Forms.Label();
            this.textBox_displayName = new System.Windows.Forms.TextBox();
            this.label_displayName = new System.Windows.Forms.Label();
            this.textBox_versionName = new System.Windows.Forms.TextBox();
            this.label_versionName = new System.Windows.Forms.Label();
            this.label_configureTitle = new System.Windows.Forms.Label();
            this.panel_progress = new System.Windows.Forms.Panel();
            this.listBox_log = new System.Windows.Forms.ListBox();
            this.label_progress = new System.Windows.Forms.Label();
            this.progressBar_extraction = new System.Windows.Forms.ProgressBar();
            this.label_progressTitle = new System.Windows.Forms.Label();
            this.panel_buttons = new System.Windows.Forms.Panel();
            this.button_cancel = new System.Windows.Forms.Button();
            this.button_next = new System.Windows.Forms.Button();
            this.button_back = new System.Windows.Forms.Button();
            this.label_stepInfo = new System.Windows.Forms.Label();
            this.panel_welcome.SuspendLayout();
            this.panel_selectSource.SuspendLayout();
            this.groupBox_detected.SuspendLayout();
            this.groupBox_manual.SuspendLayout();
            this.panel_configure.SuspendLayout();
            this.panel_progress.SuspendLayout();
            this.panel_buttons.SuspendLayout();
            this.SuspendLayout();
            //
            // panel_welcome
            //
            this.panel_welcome.Controls.Add(this.label_welcomeDescription);
            this.panel_welcome.Controls.Add(this.label_welcomeTitle);
            this.panel_welcome.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel_welcome.Location = new System.Drawing.Point(0, 0);
            this.panel_welcome.Name = "panel_welcome";
            this.panel_welcome.Padding = new System.Windows.Forms.Padding(20);
            this.panel_welcome.Size = new System.Drawing.Size(584, 361);
            this.panel_welcome.TabIndex = 0;
            //
            // label_welcomeDescription
            //
            this.label_welcomeDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label_welcomeDescription.Location = new System.Drawing.Point(20, 53);
            this.label_welcomeDescription.Name = "label_welcomeDescription";
            this.label_welcomeDescription.Size = new System.Drawing.Size(544, 288);
            this.label_welcomeDescription.TabIndex = 1;
            this.label_welcomeDescription.Text = @"This wizard will help you convert MapleStory WZ files to the new IMG filesystem format.

The IMG filesystem format provides several advantages:
  - No need for the original MapleStory client after conversion
  - Support for multiple game versions simultaneously
  - Faster loading times with optimized caching
  - Easy modification and version control with Git
  - Direct editing of extracted IMG files

Before you begin:
  - Locate your MapleStory installation folder (containing WZ files)
  - Choose a name for this version (e.g., 'v83_gms', 'kmst_latest')
  - Ensure you have enough disk space (approximately 2-3x the WZ file sizes)

Click 'Next' to continue.";
            //
            // label_welcomeTitle
            //
            this.label_welcomeTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.label_welcomeTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.label_welcomeTitle.Location = new System.Drawing.Point(20, 20);
            this.label_welcomeTitle.Name = "label_welcomeTitle";
            this.label_welcomeTitle.Size = new System.Drawing.Size(544, 33);
            this.label_welcomeTitle.TabIndex = 0;
            this.label_welcomeTitle.Text = "Welcome to the Migration Wizard";
            //
            // panel_selectSource
            //
            this.panel_selectSource.Controls.Add(this.groupBox_detected);
            this.panel_selectSource.Controls.Add(this.groupBox_manual);
            this.panel_selectSource.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel_selectSource.Location = new System.Drawing.Point(0, 0);
            this.panel_selectSource.Name = "panel_selectSource";
            this.panel_selectSource.Padding = new System.Windows.Forms.Padding(20);
            this.panel_selectSource.Size = new System.Drawing.Size(584, 361);
            this.panel_selectSource.TabIndex = 1;
            this.panel_selectSource.Visible = false;
            //
            // groupBox_detected
            //
            this.groupBox_detected.Controls.Add(this.listBox_detected);
            this.groupBox_detected.Controls.Add(this.button_scanFolder);
            this.groupBox_detected.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox_detected.Location = new System.Drawing.Point(20, 100);
            this.groupBox_detected.Name = "groupBox_detected";
            this.groupBox_detected.Size = new System.Drawing.Size(544, 241);
            this.groupBox_detected.TabIndex = 1;
            this.groupBox_detected.TabStop = false;
            this.groupBox_detected.Text = "Auto-Detect Installations";
            //
            // listBox_detected
            //
            this.listBox_detected.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox_detected.FormattingEnabled = true;
            this.listBox_detected.ItemHeight = 15;
            this.listBox_detected.Location = new System.Drawing.Point(3, 49);
            this.listBox_detected.Name = "listBox_detected";
            this.listBox_detected.Size = new System.Drawing.Size(538, 189);
            this.listBox_detected.TabIndex = 1;
            this.listBox_detected.SelectedIndexChanged += new System.EventHandler(this.listBox_detected_SelectedIndexChanged);
            //
            // button_scanFolder
            //
            this.button_scanFolder.Dock = System.Windows.Forms.DockStyle.Top;
            this.button_scanFolder.Location = new System.Drawing.Point(3, 19);
            this.button_scanFolder.Name = "button_scanFolder";
            this.button_scanFolder.Size = new System.Drawing.Size(538, 30);
            this.button_scanFolder.TabIndex = 0;
            this.button_scanFolder.Text = "Scan Folder for MapleStory Installations...";
            this.button_scanFolder.UseVisualStyleBackColor = true;
            this.button_scanFolder.Click += new System.EventHandler(this.button_scanFolder_Click);
            //
            // groupBox_manual
            //
            this.groupBox_manual.Controls.Add(this.button_browse);
            this.groupBox_manual.Controls.Add(this.textBox_wzPath);
            this.groupBox_manual.Controls.Add(this.label_wzPath);
            this.groupBox_manual.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox_manual.Location = new System.Drawing.Point(20, 20);
            this.groupBox_manual.Name = "groupBox_manual";
            this.groupBox_manual.Size = new System.Drawing.Size(544, 80);
            this.groupBox_manual.TabIndex = 0;
            this.groupBox_manual.TabStop = false;
            this.groupBox_manual.Text = "Manual Selection";
            //
            // button_browse
            //
            this.button_browse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button_browse.Location = new System.Drawing.Point(456, 44);
            this.button_browse.Name = "button_browse";
            this.button_browse.Size = new System.Drawing.Size(80, 25);
            this.button_browse.TabIndex = 2;
            this.button_browse.Text = "Browse...";
            this.button_browse.UseVisualStyleBackColor = true;
            this.button_browse.Click += new System.EventHandler(this.button_browse_Click);
            //
            // textBox_wzPath
            //
            this.textBox_wzPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_wzPath.Location = new System.Drawing.Point(9, 45);
            this.textBox_wzPath.Name = "textBox_wzPath";
            this.textBox_wzPath.ReadOnly = true;
            this.textBox_wzPath.Size = new System.Drawing.Size(441, 23);
            this.textBox_wzPath.TabIndex = 1;
            //
            // label_wzPath
            //
            this.label_wzPath.AutoSize = true;
            this.label_wzPath.Location = new System.Drawing.Point(6, 25);
            this.label_wzPath.Name = "label_wzPath";
            this.label_wzPath.Size = new System.Drawing.Size(222, 15);
            this.label_wzPath.TabIndex = 0;
            this.label_wzPath.Text = "MapleStory Folder (containing WZ files):";
            //
            // panel_configure
            //
            this.panel_configure.Controls.Add(this.label_versionError);
            this.panel_configure.Controls.Add(this.comboBox_encryption);
            this.panel_configure.Controls.Add(this.label_encryption);
            this.panel_configure.Controls.Add(this.textBox_displayName);
            this.panel_configure.Controls.Add(this.label_displayName);
            this.panel_configure.Controls.Add(this.textBox_versionName);
            this.panel_configure.Controls.Add(this.label_versionName);
            this.panel_configure.Controls.Add(this.label_configureTitle);
            this.panel_configure.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel_configure.Location = new System.Drawing.Point(0, 0);
            this.panel_configure.Name = "panel_configure";
            this.panel_configure.Padding = new System.Windows.Forms.Padding(20);
            this.panel_configure.Size = new System.Drawing.Size(584, 361);
            this.panel_configure.TabIndex = 2;
            this.panel_configure.Visible = false;
            //
            // label_versionError
            //
            this.label_versionError.AutoSize = true;
            this.label_versionError.ForeColor = System.Drawing.Color.Red;
            this.label_versionError.Location = new System.Drawing.Point(20, 115);
            this.label_versionError.Name = "label_versionError";
            this.label_versionError.Size = new System.Drawing.Size(0, 15);
            this.label_versionError.TabIndex = 7;
            this.label_versionError.Visible = false;
            //
            // comboBox_encryption
            //
            this.comboBox_encryption.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_encryption.FormattingEnabled = true;
            this.comboBox_encryption.Items.AddRange(new object[] {
            MapleLib.WzLib.WzMapleVersion.GMS,
            MapleLib.WzLib.WzMapleVersion.EMS,
            MapleLib.WzLib.WzMapleVersion.BMS,
            MapleLib.WzLib.WzMapleVersion.CLASSIC});
            this.comboBox_encryption.Location = new System.Drawing.Point(23, 230);
            this.comboBox_encryption.Name = "comboBox_encryption";
            this.comboBox_encryption.Size = new System.Drawing.Size(300, 23);
            this.comboBox_encryption.TabIndex = 6;
            this.comboBox_encryption.SelectedIndexChanged += new System.EventHandler(this.comboBox_encryption_SelectedIndexChanged);
            //
            // label_encryption
            //
            this.label_encryption.AutoSize = true;
            this.label_encryption.Location = new System.Drawing.Point(20, 212);
            this.label_encryption.Name = "label_encryption";
            this.label_encryption.Size = new System.Drawing.Size(273, 15);
            this.label_encryption.TabIndex = 5;
            this.label_encryption.Text = "WZ Encryption (select based on game region):";
            //
            // textBox_displayName
            //
            this.textBox_displayName.Location = new System.Drawing.Point(23, 170);
            this.textBox_displayName.Name = "textBox_displayName";
            this.textBox_displayName.Size = new System.Drawing.Size(300, 23);
            this.textBox_displayName.TabIndex = 4;
            this.textBox_displayName.TextChanged += new System.EventHandler(this.textBox_displayName_TextChanged);
            //
            // label_displayName
            //
            this.label_displayName.AutoSize = true;
            this.label_displayName.Location = new System.Drawing.Point(20, 152);
            this.label_displayName.Name = "label_displayName";
            this.label_displayName.Size = new System.Drawing.Size(259, 15);
            this.label_displayName.TabIndex = 3;
            this.label_displayName.Text = "Display Name (optional, e.g., \"GMS v83 Pre-BB\"):";
            //
            // textBox_versionName
            //
            this.textBox_versionName.Location = new System.Drawing.Point(23, 90);
            this.textBox_versionName.Name = "textBox_versionName";
            this.textBox_versionName.Size = new System.Drawing.Size(300, 23);
            this.textBox_versionName.TabIndex = 2;
            this.textBox_versionName.TextChanged += new System.EventHandler(this.textBox_versionName_TextChanged);
            //
            // label_versionName
            //
            this.label_versionName.AutoSize = true;
            this.label_versionName.Location = new System.Drawing.Point(20, 72);
            this.label_versionName.Name = "label_versionName";
            this.label_versionName.Size = new System.Drawing.Size(318, 15);
            this.label_versionName.TabIndex = 1;
            this.label_versionName.Text = "Version Name (used for folder name, e.g., \"v83_gms\"):";
            //
            // label_configureTitle
            //
            this.label_configureTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.label_configureTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.label_configureTitle.Location = new System.Drawing.Point(20, 20);
            this.label_configureTitle.Name = "label_configureTitle";
            this.label_configureTitle.Size = new System.Drawing.Size(544, 33);
            this.label_configureTitle.TabIndex = 0;
            this.label_configureTitle.Text = "Configure Version";
            //
            // panel_progress
            //
            this.panel_progress.Controls.Add(this.listBox_log);
            this.panel_progress.Controls.Add(this.label_progress);
            this.panel_progress.Controls.Add(this.progressBar_extraction);
            this.panel_progress.Controls.Add(this.label_progressTitle);
            this.panel_progress.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel_progress.Location = new System.Drawing.Point(0, 0);
            this.panel_progress.Name = "panel_progress";
            this.panel_progress.Padding = new System.Windows.Forms.Padding(20);
            this.panel_progress.Size = new System.Drawing.Size(584, 361);
            this.panel_progress.TabIndex = 3;
            this.panel_progress.Visible = false;
            //
            // listBox_log
            //
            this.listBox_log.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox_log.Font = new System.Drawing.Font("Consolas", 9F);
            this.listBox_log.FormattingEnabled = true;
            this.listBox_log.HorizontalScrollbar = true;
            this.listBox_log.ItemHeight = 14;
            this.listBox_log.Location = new System.Drawing.Point(20, 113);
            this.listBox_log.Name = "listBox_log";
            this.listBox_log.Size = new System.Drawing.Size(544, 228);
            this.listBox_log.TabIndex = 3;
            //
            // label_progress
            //
            this.label_progress.Dock = System.Windows.Forms.DockStyle.Top;
            this.label_progress.Location = new System.Drawing.Point(20, 83);
            this.label_progress.Name = "label_progress";
            this.label_progress.Size = new System.Drawing.Size(544, 30);
            this.label_progress.TabIndex = 2;
            this.label_progress.Text = "Preparing...";
            this.label_progress.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // progressBar_extraction
            //
            this.progressBar_extraction.Dock = System.Windows.Forms.DockStyle.Top;
            this.progressBar_extraction.Location = new System.Drawing.Point(20, 53);
            this.progressBar_extraction.Name = "progressBar_extraction";
            this.progressBar_extraction.Size = new System.Drawing.Size(544, 30);
            this.progressBar_extraction.TabIndex = 1;
            //
            // label_progressTitle
            //
            this.label_progressTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.label_progressTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.label_progressTitle.Location = new System.Drawing.Point(20, 20);
            this.label_progressTitle.Name = "label_progressTitle";
            this.label_progressTitle.Size = new System.Drawing.Size(544, 33);
            this.label_progressTitle.TabIndex = 0;
            this.label_progressTitle.Text = "Extracting...";
            //
            // panel_buttons
            //
            this.panel_buttons.Controls.Add(this.label_stepInfo);
            this.panel_buttons.Controls.Add(this.button_cancel);
            this.panel_buttons.Controls.Add(this.button_next);
            this.panel_buttons.Controls.Add(this.button_back);
            this.panel_buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel_buttons.Location = new System.Drawing.Point(0, 361);
            this.panel_buttons.Name = "panel_buttons";
            this.panel_buttons.Size = new System.Drawing.Size(584, 50);
            this.panel_buttons.TabIndex = 4;
            //
            // button_cancel
            //
            this.button_cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button_cancel.Location = new System.Drawing.Point(497, 12);
            this.button_cancel.Name = "button_cancel";
            this.button_cancel.Size = new System.Drawing.Size(75, 26);
            this.button_cancel.TabIndex = 2;
            this.button_cancel.Text = "Cancel";
            this.button_cancel.UseVisualStyleBackColor = true;
            this.button_cancel.Click += new System.EventHandler(this.button_cancel_Click);
            //
            // button_next
            //
            this.button_next.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button_next.Location = new System.Drawing.Point(416, 12);
            this.button_next.Name = "button_next";
            this.button_next.Size = new System.Drawing.Size(75, 26);
            this.button_next.TabIndex = 1;
            this.button_next.Text = "Next >";
            this.button_next.UseVisualStyleBackColor = true;
            this.button_next.Click += new System.EventHandler(this.button_next_Click);
            //
            // button_back
            //
            this.button_back.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button_back.Enabled = false;
            this.button_back.Location = new System.Drawing.Point(335, 12);
            this.button_back.Name = "button_back";
            this.button_back.Size = new System.Drawing.Size(75, 26);
            this.button_back.TabIndex = 0;
            this.button_back.Text = "< Back";
            this.button_back.UseVisualStyleBackColor = true;
            this.button_back.Click += new System.EventHandler(this.button_back_Click);
            //
            // label_stepInfo
            //
            this.label_stepInfo.AutoSize = true;
            this.label_stepInfo.Location = new System.Drawing.Point(12, 17);
            this.label_stepInfo.Name = "label_stepInfo";
            this.label_stepInfo.Size = new System.Drawing.Size(68, 15);
            this.label_stepInfo.TabIndex = 3;
            this.label_stepInfo.Text = "Step 1 of 4";
            //
            // MigrationWizard
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 411);
            this.Controls.Add(this.panel_progress);
            this.Controls.Add(this.panel_configure);
            this.Controls.Add(this.panel_selectSource);
            this.Controls.Add(this.panel_welcome);
            this.Controls.Add(this.panel_buttons);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MigrationWizard";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "WZ to IMG Migration Wizard";
            this.Load += new System.EventHandler(this.MigrationWizard_Load);
            this.panel_welcome.ResumeLayout(false);
            this.panel_selectSource.ResumeLayout(false);
            this.groupBox_detected.ResumeLayout(false);
            this.groupBox_manual.ResumeLayout(false);
            this.groupBox_manual.PerformLayout();
            this.panel_configure.ResumeLayout(false);
            this.panel_configure.PerformLayout();
            this.panel_progress.ResumeLayout(false);
            this.panel_buttons.ResumeLayout(false);
            this.panel_buttons.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel_welcome;
        private System.Windows.Forms.Label label_welcomeDescription;
        private System.Windows.Forms.Label label_welcomeTitle;
        private System.Windows.Forms.Panel panel_selectSource;
        private System.Windows.Forms.GroupBox groupBox_detected;
        private System.Windows.Forms.ListBox listBox_detected;
        private System.Windows.Forms.Button button_scanFolder;
        private System.Windows.Forms.GroupBox groupBox_manual;
        private System.Windows.Forms.Button button_browse;
        private System.Windows.Forms.TextBox textBox_wzPath;
        private System.Windows.Forms.Label label_wzPath;
        private System.Windows.Forms.Panel panel_configure;
        private System.Windows.Forms.ComboBox comboBox_encryption;
        private System.Windows.Forms.Label label_encryption;
        private System.Windows.Forms.TextBox textBox_displayName;
        private System.Windows.Forms.Label label_displayName;
        private System.Windows.Forms.TextBox textBox_versionName;
        private System.Windows.Forms.Label label_versionName;
        private System.Windows.Forms.Label label_configureTitle;
        private System.Windows.Forms.Panel panel_progress;
        private System.Windows.Forms.ListBox listBox_log;
        private System.Windows.Forms.Label label_progress;
        private System.Windows.Forms.ProgressBar progressBar_extraction;
        private System.Windows.Forms.Label label_progressTitle;
        private System.Windows.Forms.Panel panel_buttons;
        private System.Windows.Forms.Button button_cancel;
        private System.Windows.Forms.Button button_next;
        private System.Windows.Forms.Button button_back;
        private System.Windows.Forms.Label label_stepInfo;
        private System.Windows.Forms.Label label_versionError;
    }
}
