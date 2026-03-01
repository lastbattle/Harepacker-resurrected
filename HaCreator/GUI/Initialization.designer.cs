namespace HaCreator.GUI
{
    partial class Initialization
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Initialization));
            tabControl_dataSource = new System.Windows.Forms.TabControl();
            tabPage_wzFiles = new System.Windows.Forms.TabPage();
            label3 = new System.Windows.Forms.Label();
            pathBox = new System.Windows.Forms.ComboBox();
            button_browse = new System.Windows.Forms.Button();
            label2 = new System.Windows.Forms.Label();
            versionBox = new System.Windows.Forms.ComboBox();
            label1 = new System.Windows.Forms.Label();
            comboBox_localisation = new System.Windows.Forms.ComboBox();
            label4 = new System.Windows.Forms.Label();
            tabPage_imgVersions = new System.Windows.Forms.TabPage();
            listBox_imgVersions = new System.Windows.Forms.ListBox();
            label_noVersions = new System.Windows.Forms.Label();
            panel_versionDetails = new System.Windows.Forms.Panel();
            label_versionName = new System.Windows.Forms.Label();
            label_extractedDate = new System.Windows.Forms.Label();
            label_encryptionInfo = new System.Windows.Forms.Label();
            label_format = new System.Windows.Forms.Label();
            label_imageCount = new System.Windows.Forms.Label();
            label_categoryCount = new System.Windows.Forms.Label();
            label_features = new System.Windows.Forms.Label();
            label_validationStatus = new System.Windows.Forms.Label();
            button_extractNew = new System.Windows.Forms.Button();
            button_browseVersion = new System.Windows.Forms.Button();
            button_refreshVersions = new System.Windows.Forms.Button();
            button_deleteVersion = new System.Windows.Forms.Button();
            button_initialise = new System.Windows.Forms.Button();
            toolStripProgressBar1 = new System.Windows.Forms.ToolStripProgressBar();
            textBox2 = new System.Windows.Forms.TextBox();
            button_checkMapErrors = new System.Windows.Forms.Button();
            button_settings = new System.Windows.Forms.Button();
            toolTip = new System.Windows.Forms.ToolTip(components);
            tabControl_dataSource.SuspendLayout();
            tabPage_wzFiles.SuspendLayout();
            tabPage_imgVersions.SuspendLayout();
            panel_versionDetails.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl_dataSource
            // 
            tabControl_dataSource.Controls.Add(tabPage_wzFiles);
            tabControl_dataSource.Controls.Add(tabPage_imgVersions);
            tabControl_dataSource.Location = new System.Drawing.Point(4, 4);
            tabControl_dataSource.Name = "tabControl_dataSource";
            tabControl_dataSource.SelectedIndex = 0;
            tabControl_dataSource.Size = new System.Drawing.Size(582, 330);
            tabControl_dataSource.TabIndex = 0;
            tabControl_dataSource.SelectedIndexChanged += tabControl_dataSource_SelectedIndexChanged;
            // 
            // tabPage_wzFiles
            // 
            tabPage_wzFiles.Controls.Add(label3);
            tabPage_wzFiles.Controls.Add(pathBox);
            tabPage_wzFiles.Controls.Add(button_browse);
            tabPage_wzFiles.Controls.Add(label2);
            tabPage_wzFiles.Controls.Add(versionBox);
            tabPage_wzFiles.Controls.Add(label1);
            tabPage_wzFiles.Controls.Add(comboBox_localisation);
            tabPage_wzFiles.Controls.Add(label4);
            tabPage_wzFiles.Location = new System.Drawing.Point(4, 22);
            tabPage_wzFiles.Name = "tabPage_wzFiles";
            tabPage_wzFiles.Padding = new System.Windows.Forms.Padding(3);
            tabPage_wzFiles.Size = new System.Drawing.Size(574, 304);
            tabPage_wzFiles.TabIndex = 0;
            tabPage_wzFiles.Text = "WZ Files";
            tabPage_wzFiles.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(6, 12);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(33, 13);
            label3.TabIndex = 9;
            label3.Text = "Path:";
            // 
            // pathBox
            // 
            pathBox.FormattingEnabled = true;
            pathBox.Location = new System.Drawing.Point(74, 9);
            pathBox.Name = "pathBox";
            pathBox.Size = new System.Drawing.Size(430, 21);
            pathBox.TabIndex = 13;
            // 
            // button_browse
            // 
            button_browse.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            button_browse.Location = new System.Drawing.Point(510, 8);
            button_browse.Name = "button_browse";
            button_browse.Size = new System.Drawing.Size(54, 21);
            button_browse.TabIndex = 14;
            button_browse.Text = "...";
            button_browse.Click += button_browseWz_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(6, 42);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(85, 13);
            label2.TabIndex = 8;
            label2.Text = "WZ encryption:";
            // 
            // versionBox
            // 
            versionBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            versionBox.FormattingEnabled = true;
            versionBox.Items.AddRange(new object[] { "GMS", "EMS , MSEA , KMS", "BMS , JMS", "Auto-Detect" });
            versionBox.Location = new System.Drawing.Point(142, 39);
            versionBox.Name = "versionBox";
            versionBox.Size = new System.Drawing.Size(362, 21);
            versionBox.TabIndex = 3;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Enabled = false;
            label1.Location = new System.Drawing.Point(6, 72);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(102, 13);
            label1.TabIndex = 17;
            label1.Text = "Client localisation:";
            // 
            // comboBox_localisation
            // 
            comboBox_localisation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBox_localisation.Enabled = false;
            comboBox_localisation.FormattingEnabled = true;
            comboBox_localisation.Items.AddRange(new object[] { "GMS", "EMS , MSEA , KMS", "BMS , JMS", "Auto-Detect" });
            comboBox_localisation.Location = new System.Drawing.Point(142, 69);
            comboBox_localisation.Name = "comboBox_localisation";
            comboBox_localisation.Size = new System.Drawing.Size(362, 21);
            comboBox_localisation.TabIndex = 16;
            // 
            // label4
            // 
            label4.Enabled = false;
            label4.Font = new System.Drawing.Font("Segoe UI", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label4.Location = new System.Drawing.Point(6, 96);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(500, 33);
            label4.TabIndex = 18;
            label4.Text = "Please select the right localisation, as the saved .wz data parameters might be different.";
            // 
            // tabPage_imgVersions
            // 
            tabPage_imgVersions.Controls.Add(listBox_imgVersions);
            tabPage_imgVersions.Controls.Add(label_noVersions);
            tabPage_imgVersions.Controls.Add(panel_versionDetails);
            tabPage_imgVersions.Controls.Add(button_extractNew);
            tabPage_imgVersions.Controls.Add(button_browseVersion);
            tabPage_imgVersions.Controls.Add(button_refreshVersions);
            tabPage_imgVersions.Controls.Add(button_deleteVersion);
            tabPage_imgVersions.Location = new System.Drawing.Point(4, 22);
            tabPage_imgVersions.Name = "tabPage_imgVersions";
            tabPage_imgVersions.Padding = new System.Windows.Forms.Padding(3);
            tabPage_imgVersions.Size = new System.Drawing.Size(574, 304);
            tabPage_imgVersions.TabIndex = 1;
            tabPage_imgVersions.Text = "IMG Versions";
            tabPage_imgVersions.UseVisualStyleBackColor = true;
            // 
            // listBox_imgVersions
            // 
            listBox_imgVersions.Font = new System.Drawing.Font("Segoe UI", 10F);
            listBox_imgVersions.FormattingEnabled = true;
            listBox_imgVersions.Location = new System.Drawing.Point(6, 6);
            listBox_imgVersions.Name = "listBox_imgVersions";
            listBox_imgVersions.Size = new System.Drawing.Size(280, 242);
            listBox_imgVersions.TabIndex = 1;
            listBox_imgVersions.SelectedIndexChanged += listBox_imgVersions_SelectedIndexChanged;
            listBox_imgVersions.DoubleClick += listBox_imgVersions_DoubleClick;
            // 
            // label_noVersions
            // 
            label_noVersions.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);
            label_noVersions.ForeColor = System.Drawing.SystemColors.GrayText;
            label_noVersions.Location = new System.Drawing.Point(6, 80);
            label_noVersions.Name = "label_noVersions";
            label_noVersions.Size = new System.Drawing.Size(280, 80);
            label_noVersions.TabIndex = 100;
            label_noVersions.Text = "No versions found.\n\nClick 'Extract New...' to extract WZ files to IMG format,\nor 'Browse...' to add an existing folder.";
            label_noVersions.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            label_noVersions.Visible = false;
            // 
            // panel_versionDetails
            // 
            panel_versionDetails.AutoScroll = true;
            panel_versionDetails.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panel_versionDetails.Controls.Add(label_versionName);
            panel_versionDetails.Controls.Add(label_extractedDate);
            panel_versionDetails.Controls.Add(label_encryptionInfo);
            panel_versionDetails.Controls.Add(label_format);
            panel_versionDetails.Controls.Add(label_imageCount);
            panel_versionDetails.Controls.Add(label_categoryCount);
            panel_versionDetails.Controls.Add(label_features);
            panel_versionDetails.Controls.Add(label_validationStatus);
            panel_versionDetails.Location = new System.Drawing.Point(292, 6);
            panel_versionDetails.Name = "panel_versionDetails";
            panel_versionDetails.Size = new System.Drawing.Size(273, 242);
            panel_versionDetails.TabIndex = 2;
            panel_versionDetails.Visible = false;
            // 
            // label_versionName
            // 
            label_versionName.AutoSize = true;
            label_versionName.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            label_versionName.Location = new System.Drawing.Point(6, 6);
            label_versionName.Name = "label_versionName";
            label_versionName.Size = new System.Drawing.Size(102, 19);
            label_versionName.TabIndex = 0;
            label_versionName.Text = "Version Name";
            // 
            // label_extractedDate
            // 
            label_extractedDate.AutoSize = true;
            label_extractedDate.Font = new System.Drawing.Font("Segoe UI", 8F);
            label_extractedDate.ForeColor = System.Drawing.SystemColors.GrayText;
            label_extractedDate.Location = new System.Drawing.Point(6, 28);
            label_extractedDate.Name = "label_extractedDate";
            label_extractedDate.Size = new System.Drawing.Size(116, 13);
            label_extractedDate.TabIndex = 1;
            label_extractedDate.Text = "Extracted: 2025-01-01";
            // 
            // label_encryptionInfo
            // 
            label_encryptionInfo.AutoSize = true;
            label_encryptionInfo.Location = new System.Drawing.Point(6, 50);
            label_encryptionInfo.Name = "label_encryptionInfo";
            label_encryptionInfo.Size = new System.Drawing.Size(92, 13);
            label_encryptionInfo.TabIndex = 2;
            label_encryptionInfo.Text = "Encryption: GMS";
            // 
            // label_format
            // 
            label_format.AutoSize = true;
            label_format.Location = new System.Drawing.Point(6, 66);
            label_format.Name = "label_format";
            label_format.Size = new System.Drawing.Size(96, 13);
            label_format.TabIndex = 3;
            label_format.Text = "Format: Standard";
            // 
            // label_imageCount
            // 
            label_imageCount.AutoSize = true;
            label_imageCount.Location = new System.Drawing.Point(6, 86);
            label_imageCount.Name = "label_imageCount";
            label_imageCount.Size = new System.Drawing.Size(82, 13);
            label_imageCount.TabIndex = 4;
            label_imageCount.Text = "Total Images: 0";
            // 
            // label_categoryCount
            // 
            label_categoryCount.AutoSize = true;
            label_categoryCount.Location = new System.Drawing.Point(6, 102);
            label_categoryCount.Name = "label_categoryCount";
            label_categoryCount.Size = new System.Drawing.Size(74, 13);
            label_categoryCount.TabIndex = 5;
            label_categoryCount.Text = "Categories: 0";
            // 
            // label_features
            // 
            label_features.AutoSize = true;
            label_features.Location = new System.Drawing.Point(6, 118);
            label_features.Name = "label_features";
            label_features.Size = new System.Drawing.Size(83, 13);
            label_features.TabIndex = 6;
            label_features.Text = "Features: Basic";
            // 
            // label_validationStatus
            // 
            label_validationStatus.AutoSize = true;
            label_validationStatus.ForeColor = System.Drawing.Color.Green;
            label_validationStatus.Location = new System.Drawing.Point(6, 134);
            label_validationStatus.Name = "label_validationStatus";
            label_validationStatus.Size = new System.Drawing.Size(70, 13);
            label_validationStatus.TabIndex = 7;
            label_validationStatus.Text = "Status: Valid";
            // 
            // button_extractNew
            // 
            button_extractNew.Location = new System.Drawing.Point(6, 256);
            button_extractNew.Name = "button_extractNew";
            button_extractNew.Size = new System.Drawing.Size(100, 40);
            button_extractNew.TabIndex = 2;
            button_extractNew.Text = "Extract New...";
            toolTip.SetToolTip(button_extractNew, "Extract WZ files from a MapleStory installation to create a new version");
            button_extractNew.UseVisualStyleBackColor = true;
            button_extractNew.Click += button_extractNew_Click;
            // 
            // button_browseVersion
            // 
            button_browseVersion.Location = new System.Drawing.Point(112, 256);
            button_browseVersion.Name = "button_browseVersion";
            button_browseVersion.Size = new System.Drawing.Size(86, 40);
            button_browseVersion.TabIndex = 3;
            button_browseVersion.Text = "Browse...";
            toolTip.SetToolTip(button_browseVersion, "Add an existing IMG version folder to the list");
            button_browseVersion.UseVisualStyleBackColor = true;
            button_browseVersion.Click += button_browseVersion_Click;
            // 
            // button_refreshVersions
            // 
            button_refreshVersions.Location = new System.Drawing.Point(204, 256);
            button_refreshVersions.Name = "button_refreshVersions";
            button_refreshVersions.Size = new System.Drawing.Size(82, 40);
            button_refreshVersions.TabIndex = 4;
            button_refreshVersions.Text = "Refresh";
            toolTip.SetToolTip(button_refreshVersions, "Scan for new or updated versions");
            button_refreshVersions.UseVisualStyleBackColor = true;
            button_refreshVersions.Click += button_refreshVersions_Click;
            // 
            // button_deleteVersion
            // 
            button_deleteVersion.Enabled = false;
            button_deleteVersion.Location = new System.Drawing.Point(483, 256);
            button_deleteVersion.Name = "button_deleteVersion";
            button_deleteVersion.Size = new System.Drawing.Size(82, 40);
            button_deleteVersion.TabIndex = 5;
            button_deleteVersion.Text = "Delete";
            toolTip.SetToolTip(button_deleteVersion, "Delete the selected version from disk");
            button_deleteVersion.UseVisualStyleBackColor = true;
            button_deleteVersion.Click += button_deleteVersion_Click;
            // 
            // button_initialise
            // 
            button_initialise.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            button_initialise.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold);
            button_initialise.Location = new System.Drawing.Point(4, 336);
            button_initialise.Name = "button_initialise";
            button_initialise.Size = new System.Drawing.Size(140, 38);
            button_initialise.TabIndex = 1;
            button_initialise.Text = "Initialize";
            button_initialise.Click += button_initialise_Click;
            // 
            // toolStripProgressBar1
            // 
            toolStripProgressBar1.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            toolStripProgressBar1.Name = "toolStripProgressBar1";
            toolStripProgressBar1.Size = new System.Drawing.Size(150, 16);
            // 
            // textBox2
            // 
            textBox2.Location = new System.Drawing.Point(4, 376);
            textBox2.Name = "textBox2";
            textBox2.ReadOnly = true;
            textBox2.Size = new System.Drawing.Size(582, 22);
            textBox2.TabIndex = 2;
            // 
            // button_checkMapErrors
            // 
            button_checkMapErrors.Location = new System.Drawing.Point(489, 348);
            button_checkMapErrors.Name = "button_checkMapErrors";
            button_checkMapErrors.Size = new System.Drawing.Size(97, 22);
            button_checkMapErrors.TabIndex = 15;
            button_checkMapErrors.Text = "Check map errors";
            button_checkMapErrors.UseVisualStyleBackColor = true;
            button_checkMapErrors.Click += debugButton_Click;
            // 
            // button_settings
            // 
            button_settings.Location = new System.Drawing.Point(388, 348);
            button_settings.Name = "button_settings";
            button_settings.Size = new System.Drawing.Size(95, 22);
            button_settings.TabIndex = 21;
            button_settings.Text = "Settings";
            button_settings.Click += button_settings_Click;
            // 
            // Initialization
            // 
            AccessibleRole = System.Windows.Forms.AccessibleRole.ScrollBar;
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(590, 402);
            Controls.Add(tabControl_dataSource);
            Controls.Add(button_initialise);
            Controls.Add(button_settings);
            Controls.Add(button_checkMapErrors);
            Controls.Add(textBox2);
            Font = new System.Drawing.Font("Segoe UI", 8.25F);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            MaximizeBox = false;
            Name = "Initialization";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "HaCreator - Initialization";
            Load += Initialization_Load;
            KeyDown += Initialization_KeyDown;
            tabControl_dataSource.ResumeLayout(false);
            tabPage_wzFiles.ResumeLayout(false);
            tabPage_wzFiles.PerformLayout();
            tabPage_imgVersions.ResumeLayout(false);
            panel_versionDetails.ResumeLayout(false);
            panel_versionDetails.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        // Tab Control
        private System.Windows.Forms.TabControl tabControl_dataSource;
        private System.Windows.Forms.TabPage tabPage_wzFiles;
        private System.Windows.Forms.TabPage tabPage_imgVersions;

        // WZ Tab Controls
        private System.Windows.Forms.Button button_initialise;
        private System.Windows.Forms.ComboBox versionBox;
        private System.Windows.Forms.ToolStripProgressBar toolStripProgressBar1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.ComboBox pathBox;
        private System.Windows.Forms.Button button_browse;
        private System.Windows.Forms.Button button_checkMapErrors;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBox_localisation;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button button_settings;

        // IMG Versions Tab Controls
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.ListBox listBox_imgVersions;
        private System.Windows.Forms.Panel panel_versionDetails;
        private System.Windows.Forms.Label label_versionName;
        private System.Windows.Forms.Label label_extractedDate;
        private System.Windows.Forms.Label label_encryptionInfo;
        private System.Windows.Forms.Label label_format;
        private System.Windows.Forms.Label label_imageCount;
        private System.Windows.Forms.Label label_categoryCount;
        private System.Windows.Forms.Label label_features;
        private System.Windows.Forms.Label label_validationStatus;
        private System.Windows.Forms.Button button_extractNew;
        private System.Windows.Forms.Button button_browseVersion;
        private System.Windows.Forms.Button button_refreshVersions;
        private System.Windows.Forms.Button button_deleteVersion;
        private System.Windows.Forms.Label label_noVersions;
    }
}
