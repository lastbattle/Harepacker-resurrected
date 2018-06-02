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
            this.xInput = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.yInput = new System.Windows.Forms.NumericUpDown();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.ptComboBox = new System.Windows.Forms.ComboBox();
            this.ptLabel = new System.Windows.Forms.Label();
            this.pnLabel = new System.Windows.Forms.Label();
            this.pnBox = new System.Windows.Forms.TextBox();
            this.tmBox = new System.Windows.Forms.NumericUpDown();
            this.tmLabel = new System.Windows.Forms.Label();
            this.btnBrowseMap = new System.Windows.Forms.Button();
            this.thisMap = new System.Windows.Forms.CheckBox();
            this.tnBox = new System.Windows.Forms.TextBox();
            this.tnLabel = new System.Windows.Forms.Label();
            this.btnBrowseTn = new System.Windows.Forms.Button();
            this.scriptBox = new System.Windows.Forms.TextBox();
            this.delayBox = new System.Windows.Forms.NumericUpDown();
            this.delayEnable = new System.Windows.Forms.CheckBox();
            this.hRangeBox = new System.Windows.Forms.NumericUpDown();
            this.vRangeBox = new System.Windows.Forms.NumericUpDown();
            this.vImpactEnable = new System.Windows.Forms.CheckBox();
            this.vImpactBox = new System.Windows.Forms.NumericUpDown();
            this.impactLabel = new System.Windows.Forms.Label();
            this.hImpactEnable = new System.Windows.Forms.CheckBox();
            this.hImpactBox = new System.Windows.Forms.NumericUpDown();
            this.hideTooltip = new System.Windows.Forms.CheckBox();
            this.onlyOnce = new System.Windows.Forms.CheckBox();
            this.imageLabel = new System.Windows.Forms.Label();
            this.portalImageList = new System.Windows.Forms.ListBox();
            this.scriptLabel = new System.Windows.Forms.Label();
            this.rangeEnable = new System.Windows.Forms.CheckBox();
            this.xRangeLabel = new System.Windows.Forms.Label();
            this.yRangeLabel = new System.Windows.Forms.Label();
            this.leftBlankLabel = new System.Windows.Forms.Label();
            this.portalImageBox = new HaCreator.CustomControls.ScrollablePictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.xInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.yInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tmBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.delayBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.hRangeBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.vRangeBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.vImpactBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.hImpactBox)).BeginInit();
            this.SuspendLayout();
            // 
            // xInput
            // 
            this.xInput.Location = new System.Drawing.Point(89, 12);
            this.xInput.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.xInput.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.xInput.Name = "xInput";
            this.xInput.Size = new System.Drawing.Size(50, 20);
            this.xInput.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(73, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(14, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "X";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(162, 15);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(14, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Y";
            // 
            // yInput
            // 
            this.yInput.Location = new System.Drawing.Point(178, 12);
            this.yInput.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.yInput.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.yInput.Name = "yInput";
            this.yInput.Size = new System.Drawing.Size(50, 20);
            this.yInput.TabIndex = 1;
            // 
            // okButton
            // 
            this.okButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.okButton.Location = new System.Drawing.Point(110, 491);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(76, 28);
            this.okButton.TabIndex = 22;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.cancelButton.Location = new System.Drawing.Point(192, 491);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(73, 28);
            this.cancelButton.TabIndex = 23;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // ptComboBox
            // 
            this.ptComboBox.DisplayMember = "Text";
            this.ptComboBox.FormattingEnabled = true;
            this.ptComboBox.ItemHeight = 13;
            this.ptComboBox.Location = new System.Drawing.Point(89, 37);
            this.ptComboBox.Name = "ptComboBox";
            this.ptComboBox.Size = new System.Drawing.Size(139, 21);
            this.ptComboBox.TabIndex = 2;
            this.ptComboBox.SelectedIndexChanged += new System.EventHandler(this.ptComboBox_SelectedIndexChanged);
            // 
            // ptLabel
            // 
            this.ptLabel.AutoSize = true;
            this.ptLabel.Location = new System.Drawing.Point(20, 39);
            this.ptLabel.Name = "ptLabel";
            this.ptLabel.Size = new System.Drawing.Size(34, 13);
            this.ptLabel.TabIndex = 10;
            this.ptLabel.Text = "Type:";
            // 
            // pnLabel
            // 
            this.pnLabel.AutoSize = true;
            this.pnLabel.Location = new System.Drawing.Point(20, 86);
            this.pnLabel.Name = "pnLabel";
            this.pnLabel.Size = new System.Drawing.Size(68, 13);
            this.pnLabel.TabIndex = 11;
            this.pnLabel.Text = "Portal Name:";
            // 
            // pnBox
            // 
            this.pnBox.Location = new System.Drawing.Point(89, 83);
            this.pnBox.Name = "pnBox";
            this.pnBox.Size = new System.Drawing.Size(139, 20);
            this.pnBox.TabIndex = 3;
            // 
            // tmBox
            // 
            this.tmBox.Location = new System.Drawing.Point(89, 109);
            this.tmBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.tmBox.Name = "tmBox";
            this.tmBox.Size = new System.Drawing.Size(139, 20);
            this.tmBox.TabIndex = 4;
            // 
            // tmLabel
            // 
            this.tmLabel.AutoSize = true;
            this.tmLabel.Location = new System.Drawing.Point(20, 111);
            this.tmLabel.Name = "tmLabel";
            this.tmLabel.Size = new System.Drawing.Size(45, 13);
            this.tmLabel.TabIndex = 14;
            this.tmLabel.Text = "Map ID:";
            // 
            // btnBrowseMap
            // 
            this.btnBrowseMap.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.btnBrowseMap.Location = new System.Drawing.Point(234, 109);
            this.btnBrowseMap.Name = "btnBrowseMap";
            this.btnBrowseMap.Size = new System.Drawing.Size(56, 20);
            this.btnBrowseMap.TabIndex = 5;
            this.btnBrowseMap.Text = "Browse";
            this.btnBrowseMap.Click += new System.EventHandler(this.btnBrowseMap_Click);
            // 
            // thisMap
            // 
            this.thisMap.AutoSize = true;
            this.thisMap.Location = new System.Drawing.Point(296, 111);
            this.thisMap.Name = "thisMap";
            this.thisMap.Size = new System.Drawing.Size(70, 17);
            this.thisMap.TabIndex = 6;
            this.thisMap.Text = "This Map";
            this.thisMap.CheckedChanged += new System.EventHandler(this.thisMap_CheckedChanged);
            // 
            // tnBox
            // 
            this.tnBox.Location = new System.Drawing.Point(89, 135);
            this.tnBox.Name = "tnBox";
            this.tnBox.Size = new System.Drawing.Size(139, 20);
            this.tnBox.TabIndex = 7;
            // 
            // tnLabel
            // 
            this.tnLabel.AutoSize = true;
            this.tnLabel.Location = new System.Drawing.Point(20, 138);
            this.tnLabel.Name = "tnLabel";
            this.tnLabel.Size = new System.Drawing.Size(72, 13);
            this.tnLabel.TabIndex = 17;
            this.tnLabel.Text = "Target Name:";
            // 
            // btnBrowseTn
            // 
            this.btnBrowseTn.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.btnBrowseTn.Enabled = false;
            this.btnBrowseTn.Location = new System.Drawing.Point(234, 135);
            this.btnBrowseTn.Name = "btnBrowseTn";
            this.btnBrowseTn.Size = new System.Drawing.Size(56, 20);
            this.btnBrowseTn.TabIndex = 8;
            this.btnBrowseTn.Text = "Browse";
            this.btnBrowseTn.Click += new System.EventHandler(this.btnBrowseTn_Click);
            // 
            // scriptBox
            // 
            this.scriptBox.Location = new System.Drawing.Point(89, 161);
            this.scriptBox.Name = "scriptBox";
            this.scriptBox.Size = new System.Drawing.Size(139, 20);
            this.scriptBox.TabIndex = 9;
            // 
            // delayBox
            // 
            this.delayBox.Enabled = false;
            this.delayBox.Location = new System.Drawing.Point(89, 187);
            this.delayBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.delayBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.delayBox.Name = "delayBox";
            this.delayBox.Size = new System.Drawing.Size(139, 20);
            this.delayBox.TabIndex = 11;
            // 
            // delayEnable
            // 
            this.delayEnable.AutoSize = true;
            this.delayEnable.Location = new System.Drawing.Point(20, 189);
            this.delayEnable.Name = "delayEnable";
            this.delayEnable.Size = new System.Drawing.Size(56, 17);
            this.delayEnable.TabIndex = 10;
            this.delayEnable.Text = "Delay:";
            this.delayEnable.CheckedChanged += new System.EventHandler(this.EnablingCheckBoxCheckChanged);
            // 
            // hRangeBox
            // 
            this.hRangeBox.Enabled = false;
            this.hRangeBox.Location = new System.Drawing.Point(110, 213);
            this.hRangeBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.hRangeBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.hRangeBox.Name = "hRangeBox";
            this.hRangeBox.Size = new System.Drawing.Size(62, 20);
            this.hRangeBox.TabIndex = 13;
            // 
            // vRangeBox
            // 
            this.vRangeBox.Enabled = false;
            this.vRangeBox.Location = new System.Drawing.Point(228, 213);
            this.vRangeBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.vRangeBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.vRangeBox.Name = "vRangeBox";
            this.vRangeBox.Size = new System.Drawing.Size(62, 20);
            this.vRangeBox.TabIndex = 14;
            // 
            // vImpactEnable
            // 
            this.vImpactEnable.AutoSize = true;
            this.vImpactEnable.Checked = true;
            this.vImpactEnable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.vImpactEnable.Enabled = false;
            this.vImpactEnable.Location = new System.Drawing.Point(192, 241);
            this.vImpactEnable.Name = "vImpactEnable";
            this.vImpactEnable.Size = new System.Drawing.Size(33, 17);
            this.vImpactEnable.TabIndex = 33;
            this.vImpactEnable.Text = "Y";
            // 
            // vImpactBox
            // 
            this.vImpactBox.Enabled = false;
            this.vImpactBox.Location = new System.Drawing.Point(228, 239);
            this.vImpactBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.vImpactBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.vImpactBox.Name = "vImpactBox";
            this.vImpactBox.Size = new System.Drawing.Size(62, 20);
            this.vImpactBox.TabIndex = 17;
            // 
            // impactLabel
            // 
            this.impactLabel.AutoSize = true;
            this.impactLabel.Location = new System.Drawing.Point(20, 241);
            this.impactLabel.Name = "impactLabel";
            this.impactLabel.Size = new System.Drawing.Size(42, 13);
            this.impactLabel.TabIndex = 15;
            this.impactLabel.Text = "Impact:";
            // 
            // hImpactEnable
            // 
            this.hImpactEnable.AutoSize = true;
            this.hImpactEnable.Location = new System.Drawing.Point(74, 241);
            this.hImpactEnable.Name = "hImpactEnable";
            this.hImpactEnable.Size = new System.Drawing.Size(33, 17);
            this.hImpactEnable.TabIndex = 30;
            this.hImpactEnable.Text = "X";
            this.hImpactEnable.CheckedChanged += new System.EventHandler(this.EnablingCheckBoxCheckChanged);
            // 
            // hImpactBox
            // 
            this.hImpactBox.Enabled = false;
            this.hImpactBox.Location = new System.Drawing.Point(110, 239);
            this.hImpactBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.hImpactBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.hImpactBox.Name = "hImpactBox";
            this.hImpactBox.Size = new System.Drawing.Size(62, 20);
            this.hImpactBox.TabIndex = 16;
            // 
            // hideTooltip
            // 
            this.hideTooltip.AutoSize = true;
            this.hideTooltip.Location = new System.Drawing.Point(77, 265);
            this.hideTooltip.Name = "hideTooltip";
            this.hideTooltip.Size = new System.Drawing.Size(83, 17);
            this.hideTooltip.TabIndex = 18;
            this.hideTooltip.Text = "Hide Tooltip";
            // 
            // onlyOnce
            // 
            this.onlyOnce.AutoSize = true;
            this.onlyOnce.Location = new System.Drawing.Point(183, 265);
            this.onlyOnce.Name = "onlyOnce";
            this.onlyOnce.Size = new System.Drawing.Size(76, 17);
            this.onlyOnce.TabIndex = 19;
            this.onlyOnce.Text = "Only Once";
            // 
            // imageLabel
            // 
            this.imageLabel.AutoSize = true;
            this.imageLabel.Location = new System.Drawing.Point(20, 286);
            this.imageLabel.Name = "imageLabel";
            this.imageLabel.Size = new System.Drawing.Size(39, 13);
            this.imageLabel.TabIndex = 36;
            this.imageLabel.Text = "Image:";
            // 
            // portalImageList
            // 
            this.portalImageList.FormattingEnabled = true;
            this.portalImageList.Location = new System.Drawing.Point(77, 286);
            this.portalImageList.Name = "portalImageList";
            this.portalImageList.Size = new System.Drawing.Size(189, 69);
            this.portalImageList.TabIndex = 20;
            this.portalImageList.SelectedIndexChanged += new System.EventHandler(this.portalImageList_SelectedIndexChanged);
            // 
            // scriptLabel
            // 
            this.scriptLabel.AutoSize = true;
            this.scriptLabel.Location = new System.Drawing.Point(20, 163);
            this.scriptLabel.Name = "scriptLabel";
            this.scriptLabel.Size = new System.Drawing.Size(37, 13);
            this.scriptLabel.TabIndex = 40;
            this.scriptLabel.Text = "Script:";
            // 
            // rangeEnable
            // 
            this.rangeEnable.AutoSize = true;
            this.rangeEnable.Location = new System.Drawing.Point(20, 214);
            this.rangeEnable.Name = "rangeEnable";
            this.rangeEnable.Size = new System.Drawing.Size(61, 17);
            this.rangeEnable.TabIndex = 12;
            this.rangeEnable.Text = "Range:";
            this.rangeEnable.CheckedChanged += new System.EventHandler(this.rangeEnable_CheckedChanged);
            // 
            // xRangeLabel
            // 
            this.xRangeLabel.AutoSize = true;
            this.xRangeLabel.Location = new System.Drawing.Point(94, 215);
            this.xRangeLabel.Name = "xRangeLabel";
            this.xRangeLabel.Size = new System.Drawing.Size(14, 13);
            this.xRangeLabel.TabIndex = 42;
            this.xRangeLabel.Text = "X";
            // 
            // yRangeLabel
            // 
            this.yRangeLabel.AutoSize = true;
            this.yRangeLabel.Location = new System.Drawing.Point(212, 215);
            this.yRangeLabel.Name = "yRangeLabel";
            this.yRangeLabel.Size = new System.Drawing.Size(14, 13);
            this.yRangeLabel.TabIndex = 43;
            this.yRangeLabel.Text = "Y";
            // 
            // leftBlankLabel
            // 
            this.leftBlankLabel.AutoSize = true;
            this.leftBlankLabel.Location = new System.Drawing.Point(296, 138);
            this.leftBlankLabel.Name = "leftBlankLabel";
            this.leftBlankLabel.Size = new System.Drawing.Size(87, 13);
            this.leftBlankLabel.TabIndex = 9;
            this.leftBlankLabel.Text = "Can be left blank";
            // 
            // portalImageBox
            // 
            this.portalImageBox.AutoScroll = true;
            this.portalImageBox.Image = null;
            this.portalImageBox.Location = new System.Drawing.Point(77, 361);
            this.portalImageBox.Name = "portalImageBox";
            this.portalImageBox.Size = new System.Drawing.Size(189, 124);
            this.portalImageBox.TabIndex = 21;
            // 
            // PortalInstanceEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(412, 528);
            this.Controls.Add(this.leftBlankLabel);
            this.Controls.Add(this.yRangeLabel);
            this.Controls.Add(this.xRangeLabel);
            this.Controls.Add(this.rangeEnable);
            this.Controls.Add(this.scriptLabel);
            this.Controls.Add(this.portalImageBox);
            this.Controls.Add(this.portalImageList);
            this.Controls.Add(this.imageLabel);
            this.Controls.Add(this.onlyOnce);
            this.Controls.Add(this.hideTooltip);
            this.Controls.Add(this.vImpactEnable);
            this.Controls.Add(this.vImpactBox);
            this.Controls.Add(this.impactLabel);
            this.Controls.Add(this.hImpactEnable);
            this.Controls.Add(this.hImpactBox);
            this.Controls.Add(this.vRangeBox);
            this.Controls.Add(this.hRangeBox);
            this.Controls.Add(this.delayEnable);
            this.Controls.Add(this.delayBox);
            this.Controls.Add(this.scriptBox);
            this.Controls.Add(this.btnBrowseTn);
            this.Controls.Add(this.tnBox);
            this.Controls.Add(this.tnLabel);
            this.Controls.Add(this.thisMap);
            this.Controls.Add(this.btnBrowseMap);
            this.Controls.Add(this.tmLabel);
            this.Controls.Add(this.tmBox);
            this.Controls.Add(this.pnBox);
            this.Controls.Add(this.pnLabel);
            this.Controls.Add(this.ptLabel);
            this.Controls.Add(this.ptComboBox);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.yInput);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.xInput);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PortalInstanceEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Portal";
            ((System.ComponentModel.ISupportInitialize)(this.xInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.yInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tmBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.delayBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.hRangeBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.vRangeBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.vImpactBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.hImpactBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

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