namespace HaCreator.GUI
{
    partial class VersionSelector
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
            components = new System.ComponentModel.Container();
            toolTip = new System.Windows.Forms.ToolTip(components);
            button_select = new System.Windows.Forms.Button();
            button_delete = new System.Windows.Forms.Button();
            button_extract = new System.Windows.Forms.Button();
            button_browse = new System.Windows.Forms.Button();
            button_refresh = new System.Windows.Forms.Button();
            listBox_versions = new System.Windows.Forms.ListBox();
            panel_details = new System.Windows.Forms.Panel();
            label_versionName = new System.Windows.Forms.Label();
            label_extractedDate = new System.Windows.Forms.Label();
            label_encryption = new System.Windows.Forms.Label();
            label_format = new System.Windows.Forms.Label();
            label_imageCount = new System.Windows.Forms.Label();
            label_categoryCount = new System.Windows.Forms.Label();
            label_features = new System.Windows.Forms.Label();
            label_validationStatus = new System.Windows.Forms.Label();
            label_noVersions = new System.Windows.Forms.Label();
            panel_details.SuspendLayout();
            SuspendLayout();
            // 
            // button_select
            // 
            button_select.Enabled = false;
            button_select.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            button_select.Location = new System.Drawing.Point(300, 264);
            button_select.Name = "button_select";
            button_select.Size = new System.Drawing.Size(135, 34);
            button_select.TabIndex = 3;
            button_select.Text = "Select";
            toolTip.SetToolTip(button_select, "Load the selected version and start HaCreator");
            button_select.UseVisualStyleBackColor = true;
            button_select.Click += button_select_Click;
            // 
            // button_delete
            // 
            button_delete.Enabled = false;
            button_delete.Location = new System.Drawing.Point(441, 264);
            button_delete.Name = "button_delete";
            button_delete.Size = new System.Drawing.Size(132, 33);
            button_delete.TabIndex = 4;
            button_delete.Text = "Delete";
            toolTip.SetToolTip(button_delete, "Delete the selected version from disk");
            button_delete.UseVisualStyleBackColor = true;
            button_delete.Click += button_delete_Click;
            // 
            // button_extract
            // 
            button_extract.Location = new System.Drawing.Point(6, 303);
            button_extract.Name = "button_extract";
            button_extract.Size = new System.Drawing.Size(145, 50);
            button_extract.TabIndex = 5;
            button_extract.Text = "Extract New...";
            toolTip.SetToolTip(button_extract, "Extract WZ files from a MapleStory installation to create a new version");
            button_extract.UseVisualStyleBackColor = true;
            button_extract.Click += button_extract_Click;
            // 
            // button_browse
            // 
            button_browse.Location = new System.Drawing.Point(157, 303);
            button_browse.Name = "button_browse";
            button_browse.Size = new System.Drawing.Size(135, 50);
            button_browse.TabIndex = 8;
            button_browse.Text = "Browse...";
            toolTip.SetToolTip(button_browse, "Add an existing IMG version folder to the list");
            button_browse.UseVisualStyleBackColor = true;
            button_browse.Click += button_browse_Click;
            // 
            // button_refresh
            // 
            button_refresh.Location = new System.Drawing.Point(441, 317);
            button_refresh.Name = "button_refresh";
            button_refresh.Size = new System.Drawing.Size(132, 36);
            button_refresh.TabIndex = 7;
            button_refresh.Text = "Refresh";
            toolTip.SetToolTip(button_refresh, "Scan for new or updated versions");
            button_refresh.UseVisualStyleBackColor = true;
            button_refresh.Click += button_refresh_Click;
            // 
            // listBox_versions
            // 
            listBox_versions.Font = new System.Drawing.Font("Segoe UI", 10F);
            listBox_versions.FormattingEnabled = true;
            listBox_versions.Location = new System.Drawing.Point(6, 4);
            listBox_versions.Name = "listBox_versions";
            listBox_versions.Size = new System.Drawing.Size(286, 293);
            listBox_versions.TabIndex = 1;
            listBox_versions.SelectedIndexChanged += listBox_versions_SelectedIndexChanged;
            listBox_versions.DoubleClick += listBox_versions_DoubleClick;
            // 
            // panel_details
            // 
            panel_details.AutoScroll = true;
            panel_details.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panel_details.Controls.Add(label_versionName);
            panel_details.Controls.Add(label_extractedDate);
            panel_details.Controls.Add(label_encryption);
            panel_details.Controls.Add(label_format);
            panel_details.Controls.Add(label_imageCount);
            panel_details.Controls.Add(label_categoryCount);
            panel_details.Controls.Add(label_features);
            panel_details.Controls.Add(label_validationStatus);
            panel_details.Location = new System.Drawing.Point(300, 4);
            panel_details.Name = "panel_details";
            panel_details.Size = new System.Drawing.Size(273, 254);
            panel_details.TabIndex = 2;
            panel_details.Visible = false;
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
            // label_encryption
            // 
            label_encryption.AutoSize = true;
            label_encryption.Location = new System.Drawing.Point(6, 50);
            label_encryption.Name = "label_encryption";
            label_encryption.Size = new System.Drawing.Size(92, 13);
            label_encryption.TabIndex = 2;
            label_encryption.Text = "Encryption: GMS";
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
            // label_noVersions
            // 
            label_noVersions.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);
            label_noVersions.ForeColor = System.Drawing.SystemColors.GrayText;
            label_noVersions.Location = new System.Drawing.Point(12, 120);
            label_noVersions.Name = "label_noVersions";
            label_noVersions.Size = new System.Drawing.Size(280, 80);
            label_noVersions.TabIndex = 100;
            label_noVersions.Text = "No versions found.\n\nClick 'Extract New...' to extract WZ files to IMG format,\nor 'Browse...' to add an existing folder.";
            label_noVersions.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            label_noVersions.Visible = false;
            // 
            // VersionSelector
            // 
            AcceptButton = button_select;
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(575, 355);
            Controls.Add(listBox_versions);
            Controls.Add(label_noVersions);
            Controls.Add(panel_details);
            Controls.Add(button_select);
            Controls.Add(button_delete);
            Controls.Add(button_extract);
            Controls.Add(button_browse);
            Controls.Add(button_refresh);
            Font = new System.Drawing.Font("Segoe UI", 8.25F);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "VersionSelector";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "HaCreator - Select MapleStory Version";
            Load += VersionSelector_Load;
            panel_details.ResumeLayout(false);
            panel_details.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.ListBox listBox_versions;
        private System.Windows.Forms.Panel panel_details;
        private System.Windows.Forms.Label label_versionName;
        private System.Windows.Forms.Label label_extractedDate;
        private System.Windows.Forms.Label label_encryption;
        private System.Windows.Forms.Label label_format;
        private System.Windows.Forms.Label label_imageCount;
        private System.Windows.Forms.Label label_categoryCount;
        private System.Windows.Forms.Label label_features;
        private System.Windows.Forms.Label label_validationStatus;
        private System.Windows.Forms.Button button_select;
        private System.Windows.Forms.Button button_delete;
        private System.Windows.Forms.Button button_extract;
        private System.Windows.Forms.Button button_browse;
        private System.Windows.Forms.Button button_refresh;
        private System.Windows.Forms.Label label_noVersions;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
