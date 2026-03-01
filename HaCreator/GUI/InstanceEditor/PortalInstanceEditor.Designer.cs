namespace HaCreator.GUI.InstanceEditor
{
    partial class PortalInstanceEditor
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
            xInput = new System.Windows.Forms.NumericUpDown();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            yInput = new System.Windows.Forms.NumericUpDown();
            okButton = new System.Windows.Forms.Button();
            cancelButton = new System.Windows.Forms.Button();
            ptComboBox = new System.Windows.Forms.ComboBox();
            ptLabel = new System.Windows.Forms.Label();
            pnLabel = new System.Windows.Forms.Label();
            pnBox = new System.Windows.Forms.TextBox();
            tmBox = new System.Windows.Forms.NumericUpDown();
            tmLabel = new System.Windows.Forms.Label();
            btnBrowseMap = new System.Windows.Forms.Button();
            thisMap = new System.Windows.Forms.CheckBox();
            tnBox = new System.Windows.Forms.TextBox();
            tnLabel = new System.Windows.Forms.Label();
            btnBrowseTn = new System.Windows.Forms.Button();
            scriptBox = new System.Windows.Forms.TextBox();
            delayBox = new System.Windows.Forms.NumericUpDown();
            delayEnable = new System.Windows.Forms.CheckBox();
            hRangeBox = new System.Windows.Forms.NumericUpDown();
            vRangeBox = new System.Windows.Forms.NumericUpDown();
            vImpactEnable = new System.Windows.Forms.CheckBox();
            vImpactBox = new System.Windows.Forms.NumericUpDown();
            impactLabel = new System.Windows.Forms.Label();
            hImpactEnable = new System.Windows.Forms.CheckBox();
            hImpactBox = new System.Windows.Forms.NumericUpDown();
            hideTooltip = new System.Windows.Forms.CheckBox();
            onlyOnce = new System.Windows.Forms.CheckBox();
            imageLabel = new System.Windows.Forms.Label();
            portalImageList = new System.Windows.Forms.ListBox();
            scriptLabel = new System.Windows.Forms.Label();
            rangeEnable = new System.Windows.Forms.CheckBox();
            xRangeLabel = new System.Windows.Forms.Label();
            yRangeLabel = new System.Windows.Forms.Label();
            leftBlankLabel = new System.Windows.Forms.Label();
            portalImageBox = new HaCreator.CustomControls.ScrollablePictureBox();
            ((System.ComponentModel.ISupportInitialize)xInput).BeginInit();
            ((System.ComponentModel.ISupportInitialize)yInput).BeginInit();
            ((System.ComponentModel.ISupportInitialize)tmBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)delayBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)hRangeBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)vRangeBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)vImpactBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)hImpactBox).BeginInit();
            SuspendLayout();
            // 
            // xInput
            // 
            xInput.Location = new System.Drawing.Point(42, 32);
            xInput.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            xInput.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            xInput.Name = "xInput";
            xInput.Size = new System.Drawing.Size(50, 22);
            xInput.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(3, 34);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(13, 13);
            label1.TabIndex = 2;
            label1.Text = "X";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(125, 34);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(12, 13);
            label2.TabIndex = 4;
            label2.Text = "Y";
            // 
            // yInput
            // 
            yInput.Location = new System.Drawing.Point(141, 31);
            yInput.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            yInput.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            yInput.Name = "yInput";
            yInput.Size = new System.Drawing.Size(50, 22);
            yInput.TabIndex = 1;
            // 
            // okButton
            // 
            okButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            okButton.Location = new System.Drawing.Point(3, 519);
            okButton.Name = "okButton";
            okButton.Size = new System.Drawing.Size(200, 46);
            okButton.TabIndex = 22;
            okButton.Text = "OK";
            okButton.Click += okButton_Click;
            // 
            // cancelButton
            // 
            cancelButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            cancelButton.Location = new System.Drawing.Point(208, 519);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new System.Drawing.Size(200, 46);
            cancelButton.TabIndex = 23;
            cancelButton.Text = "Cancel";
            cancelButton.Click += cancelButton_Click;
            // 
            // ptComboBox
            // 
            ptComboBox.DisplayMember = "Text";
            ptComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            ptComboBox.FormattingEnabled = true;
            ptComboBox.ItemHeight = 13;
            ptComboBox.Location = new System.Drawing.Point(43, 6);
            ptComboBox.Name = "ptComboBox";
            ptComboBox.Size = new System.Drawing.Size(365, 21);
            ptComboBox.TabIndex = 2;
            ptComboBox.SelectedIndexChanged += ptComboBox_SelectedIndexChanged;
            // 
            // ptLabel
            // 
            ptLabel.AutoSize = true;
            ptLabel.Location = new System.Drawing.Point(3, 9);
            ptLabel.Name = "ptLabel";
            ptLabel.Size = new System.Drawing.Size(32, 13);
            ptLabel.TabIndex = 10;
            ptLabel.Text = "Type:";
            // 
            // pnLabel
            // 
            pnLabel.AutoSize = true;
            pnLabel.Location = new System.Drawing.Point(3, 66);
            pnLabel.Name = "pnLabel";
            pnLabel.Size = new System.Drawing.Size(72, 13);
            pnLabel.TabIndex = 11;
            pnLabel.Text = "Portal Name:";
            // 
            // pnBox
            // 
            pnBox.Location = new System.Drawing.Point(80, 63);
            pnBox.Name = "pnBox";
            pnBox.Size = new System.Drawing.Size(328, 22);
            pnBox.TabIndex = 3;
            // 
            // tmBox
            // 
            tmBox.Location = new System.Drawing.Point(80, 89);
            tmBox.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            tmBox.Name = "tmBox";
            tmBox.Size = new System.Drawing.Size(139, 22);
            tmBox.TabIndex = 4;
            // 
            // tmLabel
            // 
            tmLabel.AutoSize = true;
            tmLabel.Location = new System.Drawing.Point(3, 91);
            tmLabel.Name = "tmLabel";
            tmLabel.Size = new System.Drawing.Size(47, 13);
            tmLabel.TabIndex = 14;
            tmLabel.Text = "Map ID:";
            // 
            // btnBrowseMap
            // 
            btnBrowseMap.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            btnBrowseMap.Location = new System.Drawing.Point(225, 89);
            btnBrowseMap.Name = "btnBrowseMap";
            btnBrowseMap.Size = new System.Drawing.Size(73, 20);
            btnBrowseMap.TabIndex = 5;
            btnBrowseMap.Text = "Browse";
            btnBrowseMap.Click += btnBrowseMap_Click;
            // 
            // thisMap
            // 
            thisMap.AutoSize = true;
            thisMap.Location = new System.Drawing.Point(304, 92);
            thisMap.Name = "thisMap";
            thisMap.Size = new System.Drawing.Size(72, 17);
            thisMap.TabIndex = 6;
            thisMap.Text = "This Map";
            thisMap.CheckedChanged += thisMap_CheckedChanged;
            // 
            // tnBox
            // 
            tnBox.Location = new System.Drawing.Point(80, 115);
            tnBox.Name = "tnBox";
            tnBox.Size = new System.Drawing.Size(139, 22);
            tnBox.TabIndex = 7;
            // 
            // tnLabel
            // 
            tnLabel.AutoSize = true;
            tnLabel.Location = new System.Drawing.Point(3, 118);
            tnLabel.Name = "tnLabel";
            tnLabel.Size = new System.Drawing.Size(73, 13);
            tnLabel.TabIndex = 17;
            tnLabel.Text = "Target Name:";
            // 
            // btnBrowseTn
            // 
            btnBrowseTn.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            btnBrowseTn.Enabled = false;
            btnBrowseTn.Location = new System.Drawing.Point(225, 115);
            btnBrowseTn.Name = "btnBrowseTn";
            btnBrowseTn.Size = new System.Drawing.Size(73, 20);
            btnBrowseTn.TabIndex = 8;
            btnBrowseTn.Text = "Browse";
            btnBrowseTn.Click += btnBrowseTn_Click;
            // 
            // scriptBox
            // 
            scriptBox.Location = new System.Drawing.Point(80, 141);
            scriptBox.Name = "scriptBox";
            scriptBox.Size = new System.Drawing.Size(328, 22);
            scriptBox.TabIndex = 9;
            // 
            // delayBox
            // 
            delayBox.Enabled = false;
            delayBox.Location = new System.Drawing.Point(80, 168);
            delayBox.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            delayBox.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            delayBox.Name = "delayBox";
            delayBox.Size = new System.Drawing.Size(328, 22);
            delayBox.TabIndex = 11;
            // 
            // delayEnable
            // 
            delayEnable.AutoSize = true;
            delayEnable.Location = new System.Drawing.Point(3, 168);
            delayEnable.Name = "delayEnable";
            delayEnable.Size = new System.Drawing.Size(57, 17);
            delayEnable.TabIndex = 10;
            delayEnable.Text = "Delay:";
            delayEnable.CheckedChanged += EnablingCheckBoxCheckChanged;
            // 
            // hRangeBox
            // 
            hRangeBox.Enabled = false;
            hRangeBox.Location = new System.Drawing.Point(93, 193);
            hRangeBox.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            hRangeBox.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            hRangeBox.Name = "hRangeBox";
            hRangeBox.Size = new System.Drawing.Size(62, 22);
            hRangeBox.TabIndex = 13;
            // 
            // vRangeBox
            // 
            vRangeBox.Enabled = false;
            vRangeBox.Location = new System.Drawing.Point(211, 193);
            vRangeBox.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            vRangeBox.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            vRangeBox.Name = "vRangeBox";
            vRangeBox.Size = new System.Drawing.Size(62, 22);
            vRangeBox.TabIndex = 14;
            // 
            // vImpactEnable
            // 
            vImpactEnable.AutoSize = true;
            vImpactEnable.Checked = true;
            vImpactEnable.CheckState = System.Windows.Forms.CheckState.Checked;
            vImpactEnable.Enabled = false;
            vImpactEnable.Location = new System.Drawing.Point(175, 221);
            vImpactEnable.Name = "vImpactEnable";
            vImpactEnable.Size = new System.Drawing.Size(31, 17);
            vImpactEnable.TabIndex = 33;
            vImpactEnable.Text = "Y";
            // 
            // vImpactBox
            // 
            vImpactBox.Enabled = false;
            vImpactBox.Location = new System.Drawing.Point(211, 219);
            vImpactBox.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            vImpactBox.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            vImpactBox.Name = "vImpactBox";
            vImpactBox.Size = new System.Drawing.Size(62, 22);
            vImpactBox.TabIndex = 17;
            // 
            // impactLabel
            // 
            impactLabel.AutoSize = true;
            impactLabel.Location = new System.Drawing.Point(3, 222);
            impactLabel.Name = "impactLabel";
            impactLabel.Size = new System.Drawing.Size(44, 13);
            impactLabel.TabIndex = 15;
            impactLabel.Text = "Impact:";
            // 
            // hImpactEnable
            // 
            hImpactEnable.AutoSize = true;
            hImpactEnable.Location = new System.Drawing.Point(57, 221);
            hImpactEnable.Name = "hImpactEnable";
            hImpactEnable.Size = new System.Drawing.Size(32, 17);
            hImpactEnable.TabIndex = 30;
            hImpactEnable.Text = "X";
            hImpactEnable.CheckedChanged += EnablingCheckBoxCheckChanged;
            // 
            // hImpactBox
            // 
            hImpactBox.Enabled = false;
            hImpactBox.Location = new System.Drawing.Point(93, 219);
            hImpactBox.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            hImpactBox.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            hImpactBox.Name = "hImpactBox";
            hImpactBox.Size = new System.Drawing.Size(62, 22);
            hImpactBox.TabIndex = 16;
            // 
            // hideTooltip
            // 
            hideTooltip.AutoSize = true;
            hideTooltip.Location = new System.Drawing.Point(57, 244);
            hideTooltip.Name = "hideTooltip";
            hideTooltip.Size = new System.Drawing.Size(88, 17);
            hideTooltip.TabIndex = 18;
            hideTooltip.Text = "Hide Tooltip";
            // 
            // onlyOnce
            // 
            onlyOnce.AutoSize = true;
            onlyOnce.Location = new System.Drawing.Point(193, 244);
            onlyOnce.Name = "onlyOnce";
            onlyOnce.Size = new System.Drawing.Size(80, 17);
            onlyOnce.TabIndex = 19;
            onlyOnce.Text = "Only Once";
            // 
            // imageLabel
            // 
            imageLabel.AutoSize = true;
            imageLabel.Location = new System.Drawing.Point(3, 286);
            imageLabel.Name = "imageLabel";
            imageLabel.Size = new System.Drawing.Size(74, 13);
            imageLabel.TabIndex = 36;
            imageLabel.Text = "Portal Image:";
            // 
            // portalImageList
            // 
            portalImageList.FormattingEnabled = true;
            portalImageList.ItemHeight = 13;
            portalImageList.Location = new System.Drawing.Point(83, 286);
            portalImageList.Name = "portalImageList";
            portalImageList.Size = new System.Drawing.Size(325, 56);
            portalImageList.TabIndex = 20;
            portalImageList.SelectedIndexChanged += portalImageList_SelectedIndexChanged;
            // 
            // scriptLabel
            // 
            scriptLabel.AutoSize = true;
            scriptLabel.Location = new System.Drawing.Point(3, 144);
            scriptLabel.Name = "scriptLabel";
            scriptLabel.Size = new System.Drawing.Size(39, 13);
            scriptLabel.TabIndex = 40;
            scriptLabel.Text = "Script:";
            // 
            // rangeEnable
            // 
            rangeEnable.AutoSize = true;
            rangeEnable.Location = new System.Drawing.Point(3, 193);
            rangeEnable.Name = "rangeEnable";
            rangeEnable.Size = new System.Drawing.Size(62, 17);
            rangeEnable.TabIndex = 12;
            rangeEnable.Text = "Range:";
            rangeEnable.CheckedChanged += rangeEnable_CheckedChanged;
            // 
            // xRangeLabel
            // 
            xRangeLabel.AutoSize = true;
            xRangeLabel.Location = new System.Drawing.Point(77, 195);
            xRangeLabel.Name = "xRangeLabel";
            xRangeLabel.Size = new System.Drawing.Size(13, 13);
            xRangeLabel.TabIndex = 42;
            xRangeLabel.Text = "X";
            // 
            // yRangeLabel
            // 
            yRangeLabel.AutoSize = true;
            yRangeLabel.Location = new System.Drawing.Point(195, 195);
            yRangeLabel.Name = "yRangeLabel";
            yRangeLabel.Size = new System.Drawing.Size(12, 13);
            yRangeLabel.TabIndex = 43;
            yRangeLabel.Text = "Y";
            // 
            // leftBlankLabel
            // 
            leftBlankLabel.AutoSize = true;
            leftBlankLabel.Location = new System.Drawing.Point(304, 119);
            leftBlankLabel.Name = "leftBlankLabel";
            leftBlankLabel.Size = new System.Drawing.Size(95, 13);
            leftBlankLabel.TabIndex = 9;
            leftBlankLabel.Text = "Can be left blank";
            // 
            // portalImageBox
            // 
            portalImageBox.AutoScroll = true;
            portalImageBox.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            portalImageBox.Image = null;
            portalImageBox.Location = new System.Drawing.Point(77, 348);
            portalImageBox.Name = "portalImageBox";
            portalImageBox.Size = new System.Drawing.Size(330, 165);
            portalImageBox.TabIndex = 21;
            // 
            // PortalInstanceEditor
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(412, 568);
            Controls.Add(leftBlankLabel);
            Controls.Add(yRangeLabel);
            Controls.Add(xRangeLabel);
            Controls.Add(rangeEnable);
            Controls.Add(scriptLabel);
            Controls.Add(portalImageBox);
            Controls.Add(portalImageList);
            Controls.Add(imageLabel);
            Controls.Add(onlyOnce);
            Controls.Add(hideTooltip);
            Controls.Add(vImpactEnable);
            Controls.Add(vImpactBox);
            Controls.Add(impactLabel);
            Controls.Add(hImpactEnable);
            Controls.Add(hImpactBox);
            Controls.Add(vRangeBox);
            Controls.Add(hRangeBox);
            Controls.Add(delayEnable);
            Controls.Add(delayBox);
            Controls.Add(scriptBox);
            Controls.Add(btnBrowseTn);
            Controls.Add(tnBox);
            Controls.Add(tnLabel);
            Controls.Add(thisMap);
            Controls.Add(btnBrowseMap);
            Controls.Add(tmLabel);
            Controls.Add(tmBox);
            Controls.Add(pnBox);
            Controls.Add(pnLabel);
            Controls.Add(ptLabel);
            Controls.Add(ptComboBox);
            Controls.Add(cancelButton);
            Controls.Add(okButton);
            Controls.Add(label2);
            Controls.Add(yInput);
            Controls.Add(label1);
            Controls.Add(xInput);
            DoubleBuffered = true;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "PortalInstanceEditor";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Portal";
            ((System.ComponentModel.ISupportInitialize)xInput).EndInit();
            ((System.ComponentModel.ISupportInitialize)yInput).EndInit();
            ((System.ComponentModel.ISupportInitialize)tmBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)delayBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)hRangeBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)vRangeBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)vImpactBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)hImpactBox).EndInit();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown xInput;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown yInput;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.ComboBox ptComboBox;
        private System.Windows.Forms.Label ptLabel;
        private System.Windows.Forms.Label pnLabel;
        private System.Windows.Forms.TextBox pnBox;
        private System.Windows.Forms.NumericUpDown tmBox;
        private System.Windows.Forms.Label tmLabel;
        private System.Windows.Forms.Button btnBrowseMap;
        private System.Windows.Forms.CheckBox thisMap;
        private System.Windows.Forms.TextBox tnBox;
        private System.Windows.Forms.Label tnLabel;
        private System.Windows.Forms.Button btnBrowseTn;
        private System.Windows.Forms.TextBox scriptBox;
        private System.Windows.Forms.NumericUpDown delayBox;
        private System.Windows.Forms.CheckBox delayEnable;
        private System.Windows.Forms.NumericUpDown hRangeBox;
        private System.Windows.Forms.NumericUpDown vRangeBox;
        private System.Windows.Forms.CheckBox vImpactEnable;
        private System.Windows.Forms.NumericUpDown vImpactBox;
        private System.Windows.Forms.Label impactLabel;
        private System.Windows.Forms.CheckBox hImpactEnable;
        private System.Windows.Forms.NumericUpDown hImpactBox;
        private System.Windows.Forms.CheckBox hideTooltip;
        private System.Windows.Forms.CheckBox onlyOnce;
        private System.Windows.Forms.Label imageLabel;
        private System.Windows.Forms.ListBox portalImageList;
        private CustomControls.ScrollablePictureBox portalImageBox;
        private System.Windows.Forms.Label scriptLabel;
        private System.Windows.Forms.CheckBox rangeEnable;
        private System.Windows.Forms.Label xRangeLabel;
        private System.Windows.Forms.Label yRangeLabel;
        private System.Windows.Forms.Label leftBlankLabel;
    }
}