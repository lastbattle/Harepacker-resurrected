namespace HaCreator.GUI
{
    partial class UnpackWzToImg
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label4 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            comboBox_localisation = new System.Windows.Forms.ComboBox();
            button_pathSelect = new System.Windows.Forms.Button();
            label3 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            versionBox = new System.Windows.Forms.ComboBox();
            button_unpack = new System.Windows.Forms.Button();
            textBox_status = new System.Windows.Forms.TextBox();
            textBox_path = new System.Windows.Forms.TextBox();
            progressBar = new System.Windows.Forms.ProgressBar();
            listBox_log = new System.Windows.Forms.ListBox();
            label5 = new System.Windows.Forms.Label();
            textBox_versionName = new System.Windows.Forms.TextBox();
            label6 = new System.Windows.Forms.Label();
            label_wzFiles = new System.Windows.Forms.Label();
            checkedListBox_wzFiles = new System.Windows.Forms.CheckedListBox();
            button_selectAll = new System.Windows.Forms.Button();
            button_selectNone = new System.Windows.Forms.Button();
            button_scanWzFiles = new System.Windows.Forms.Button();
            SuspendLayout();
            //
            // label4
            //
            label4.Enabled = false;
            label4.Font = new System.Drawing.Font("Segoe UI", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label4.Location = new System.Drawing.Point(1, 115);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(365, 33);
            label4.TabIndex = 26;
            label4.Text = "Please select the right localisation, as the saved .wz data parameters might be different.";
            //
            // label1
            //
            label1.AutoSize = true;
            label1.Enabled = false;
            label1.Location = new System.Drawing.Point(2, 93);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(104, 15);
            label1.TabIndex = 25;
            label1.Text = "Client localisation:";
            //
            // comboBox_localisation
            //
            comboBox_localisation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBox_localisation.Enabled = false;
            comboBox_localisation.FormattingEnabled = true;
            comboBox_localisation.Items.AddRange(new object[] { "GMS", "EMS , MSEA , KMS", "BMS , JMS", "Auto-Detect" });
            comboBox_localisation.Location = new System.Drawing.Point(138, 91);
            comboBox_localisation.Name = "comboBox_localisation";
            comboBox_localisation.Size = new System.Drawing.Size(328, 23);
            comboBox_localisation.TabIndex = 24;
            //
            // button_pathSelect
            //
            button_pathSelect.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            button_pathSelect.Location = new System.Drawing.Point(412, 5);
            button_pathSelect.Name = "button_pathSelect";
            button_pathSelect.Size = new System.Drawing.Size(54, 21);
            button_pathSelect.TabIndex = 23;
            button_pathSelect.Text = "...";
            button_pathSelect.Click += button_pathSelect_Click;
            //
            // label3
            //
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(3, 9);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(70, 15);
            label3.TabIndex = 21;
            label3.Text = "Export Path:";
            //
            // label2
            //
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(3, 64);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(88, 15);
            label2.TabIndex = 20;
            label2.Text = "WZ encryption:";
            //
            // versionBox
            //
            versionBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            versionBox.FormattingEnabled = true;
            versionBox.Items.AddRange(new object[] { "GMS", "EMS , MSEA , KMS", "BMS , JMS", "Auto-Detect" });
            versionBox.Location = new System.Drawing.Point(139, 64);
            versionBox.Name = "versionBox";
            versionBox.Size = new System.Drawing.Size(327, 23);
            versionBox.TabIndex = 19;
            //
            // label_wzFiles
            //
            label_wzFiles.AutoSize = true;
            label_wzFiles.Location = new System.Drawing.Point(3, 148);
            label_wzFiles.Name = "label_wzFiles";
            label_wzFiles.Size = new System.Drawing.Size(111, 15);
            label_wzFiles.TabIndex = 35;
            label_wzFiles.Text = "WZ Files to Extract:";
            //
            // checkedListBox_wzFiles
            //
            checkedListBox_wzFiles.CheckOnClick = true;
            checkedListBox_wzFiles.FormattingEnabled = true;
            checkedListBox_wzFiles.Location = new System.Drawing.Point(3, 166);
            checkedListBox_wzFiles.Name = "checkedListBox_wzFiles";
            checkedListBox_wzFiles.Size = new System.Drawing.Size(462, 130);
            checkedListBox_wzFiles.TabIndex = 36;
            //
            // button_selectAll
            //
            button_selectAll.Location = new System.Drawing.Point(3, 300);
            button_selectAll.Name = "button_selectAll";
            button_selectAll.Size = new System.Drawing.Size(75, 23);
            button_selectAll.TabIndex = 37;
            button_selectAll.Text = "Select All";
            button_selectAll.UseVisualStyleBackColor = true;
            button_selectAll.Click += button_selectAll_Click;
            //
            // button_selectNone
            //
            button_selectNone.Location = new System.Drawing.Point(84, 300);
            button_selectNone.Name = "button_selectNone";
            button_selectNone.Size = new System.Drawing.Size(85, 23);
            button_selectNone.TabIndex = 38;
            button_selectNone.Text = "Select None";
            button_selectNone.UseVisualStyleBackColor = true;
            button_selectNone.Click += button_selectNone_Click;
            //
            // button_scanWzFiles
            //
            button_scanWzFiles.Location = new System.Drawing.Point(370, 300);
            button_scanWzFiles.Name = "button_scanWzFiles";
            button_scanWzFiles.Size = new System.Drawing.Size(95, 23);
            button_scanWzFiles.TabIndex = 39;
            button_scanWzFiles.Text = "Scan WZ Files";
            button_scanWzFiles.UseVisualStyleBackColor = true;
            button_scanWzFiles.Click += button_scanWzFiles_Click;
            //
            // button_unpack
            //
            button_unpack.Location = new System.Drawing.Point(1, 330);
            button_unpack.Name = "button_unpack";
            button_unpack.Size = new System.Drawing.Size(465, 38);
            button_unpack.TabIndex = 27;
            button_unpack.Text = "Extract";
            button_unpack.UseVisualStyleBackColor = true;
            button_unpack.Click += button_unpack_Click;
            //
            // textBox_status
            //
            textBox_status.Location = new System.Drawing.Point(1, 403);
            textBox_status.Name = "textBox_status";
            textBox_status.ReadOnly = true;
            textBox_status.Size = new System.Drawing.Size(464, 23);
            textBox_status.TabIndex = 28;
            //
            // textBox_path
            //
            textBox_path.Location = new System.Drawing.Point(79, 3);
            textBox_path.Name = "textBox_path";
            textBox_path.Size = new System.Drawing.Size(327, 23);
            textBox_path.TabIndex = 29;
            textBox_path.TextChanged += textBox_path_TextChanged;
            //
            // progressBar
            //
            progressBar.Location = new System.Drawing.Point(1, 374);
            progressBar.Name = "progressBar";
            progressBar.Size = new System.Drawing.Size(464, 23);
            progressBar.TabIndex = 30;
            //
            // listBox_log
            //
            listBox_log.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            listBox_log.FormattingEnabled = true;
            listBox_log.HorizontalScrollbar = true;
            listBox_log.Location = new System.Drawing.Point(1, 432);
            listBox_log.Name = "listBox_log";
            listBox_log.Size = new System.Drawing.Size(464, 160);
            listBox_log.TabIndex = 31;
            //
            // label5
            //
            label5.AutoSize = true;
            label5.Location = new System.Drawing.Point(3, 35);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(84, 15);
            label5.TabIndex = 32;
            label5.Text = "Version Name:";
            //
            // textBox_versionName
            //
            textBox_versionName.Location = new System.Drawing.Point(139, 32);
            textBox_versionName.Name = "textBox_versionName";
            textBox_versionName.Size = new System.Drawing.Size(327, 23);
            textBox_versionName.TabIndex = 33;
            textBox_versionName.TextChanged += textBox_versionName_TextChanged;
            //
            // label6
            //
            label6.AutoSize = true;
            label6.Font = new System.Drawing.Font("Segoe UI", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label6.ForeColor = System.Drawing.SystemColors.GrayText;
            label6.Location = new System.Drawing.Point(1, 598);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(282, 12);
            label6.TabIndex = 34;
            label6.Text = "Extracted IMG files can be used with HaCreator's IMG filesystem mode.";
            //
            // UnpackWzToImg
            //
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(469, 618);
            Controls.Add(button_scanWzFiles);
            Controls.Add(button_selectNone);
            Controls.Add(button_selectAll);
            Controls.Add(checkedListBox_wzFiles);
            Controls.Add(label_wzFiles);
            Controls.Add(label6);
            Controls.Add(textBox_versionName);
            Controls.Add(label5);
            Controls.Add(listBox_log);
            Controls.Add(progressBar);
            Controls.Add(textBox_path);
            Controls.Add(textBox_status);
            Controls.Add(button_unpack);
            Controls.Add(label4);
            Controls.Add(label1);
            Controls.Add(comboBox_localisation);
            Controls.Add(button_pathSelect);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(versionBox);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            MaximizeBox = false;
            Name = "UnpackWzToImg";
            Text = "Extract WZ to IMG Files";
            Load += Initialization_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBox_localisation;
        private System.Windows.Forms.Button button_pathSelect;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox versionBox;
        private System.Windows.Forms.Button button_unpack;
        private System.Windows.Forms.TextBox textBox_status;
        private System.Windows.Forms.TextBox textBox_path;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.ListBox listBox_log;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBox_versionName;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label_wzFiles;
        private System.Windows.Forms.CheckedListBox checkedListBox_wzFiles;
        private System.Windows.Forms.Button button_selectAll;
        private System.Windows.Forms.Button button_selectNone;
        private System.Windows.Forms.Button button_scanWzFiles;
    }
}
