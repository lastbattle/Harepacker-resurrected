namespace HaCreator.GUI.InstanceEditor
{
    partial class LifeInstanceEditor
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
            this.pathLabel = new System.Windows.Forms.Label();
            this.xInput = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.yInput = new System.Windows.Forms.NumericUpDown();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.rx0Box = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.rx1Box = new System.Windows.Forms.NumericUpDown();
            this.mobTimeBox = new System.Windows.Forms.NumericUpDown();
            this.mobTimeEnable = new System.Windows.Forms.CheckBox();
            this.limitedNameEnable = new System.Windows.Forms.CheckBox();
            this.infoEnable = new System.Windows.Forms.CheckBox();
            this.teamEnable = new System.Windows.Forms.CheckBox();
            this.hideBox = new System.Windows.Forms.CheckBox();
            this.infoBox = new System.Windows.Forms.NumericUpDown();
            this.teamBox = new System.Windows.Forms.NumericUpDown();
            this.limitedNameBox = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.yShiftBox = new System.Windows.Forms.NumericUpDown();
            this.flipBox = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.xInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.yInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rx0Box)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rx1Box)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.mobTimeBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.infoBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.teamBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.yShiftBox)).BeginInit();
            this.SuspendLayout();
            // 
            // pathLabel
            // 
            this.pathLabel.Location = new System.Drawing.Point(0, -1);
            this.pathLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.pathLabel.Name = "pathLabel";
            this.pathLabel.Size = new System.Drawing.Size(436, 114);
            this.pathLabel.TabIndex = 0;
            this.pathLabel.Text = "label1";
            this.pathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // xInput
            // 
            this.xInput.Location = new System.Drawing.Point(73, 122);
            this.xInput.Margin = new System.Windows.Forms.Padding(4);
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
            this.xInput.Size = new System.Drawing.Size(75, 29);
            this.xInput.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(32, 124);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(20, 23);
            this.label1.TabIndex = 2;
            this.label1.Text = "X";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(32, 164);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(19, 23);
            this.label2.TabIndex = 4;
            this.label2.Text = "Y";
            // 
            // yInput
            // 
            this.yInput.Location = new System.Drawing.Point(73, 160);
            this.yInput.Margin = new System.Windows.Forms.Padding(4);
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
            this.yInput.Size = new System.Drawing.Size(75, 29);
            this.yInput.TabIndex = 1;
            // 
            // okButton
            // 
            this.okButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.okButton.Location = new System.Drawing.Point(2, 342);
            this.okButton.Margin = new System.Windows.Forms.Padding(4);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(216, 69);
            this.okButton.TabIndex = 9;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.cancelButton.Location = new System.Drawing.Point(215, 342);
            this.cancelButton.Margin = new System.Windows.Forms.Padding(4);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(224, 69);
            this.cancelButton.TabIndex = 8;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(19, 202);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(39, 23);
            this.label3.TabIndex = 10;
            this.label3.Text = "RX0";
            // 
            // rx0Box
            // 
            this.rx0Box.Location = new System.Drawing.Point(72, 200);
            this.rx0Box.Margin = new System.Windows.Forms.Padding(4);
            this.rx0Box.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.rx0Box.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.rx0Box.Name = "rx0Box";
            this.rx0Box.Size = new System.Drawing.Size(75, 29);
            this.rx0Box.TabIndex = 2;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(20, 242);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(39, 23);
            this.label4.TabIndex = 12;
            this.label4.Text = "RX1";
            // 
            // rx1Box
            // 
            this.rx1Box.Location = new System.Drawing.Point(72, 238);
            this.rx1Box.Margin = new System.Windows.Forms.Padding(4);
            this.rx1Box.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.rx1Box.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.rx1Box.Name = "rx1Box";
            this.rx1Box.Size = new System.Drawing.Size(75, 29);
            this.rx1Box.TabIndex = 3;
            // 
            // mobTimeBox
            // 
            this.mobTimeBox.Enabled = false;
            this.mobTimeBox.Location = new System.Drawing.Point(338, 117);
            this.mobTimeBox.Margin = new System.Windows.Forms.Padding(4);
            this.mobTimeBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.mobTimeBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.mobTimeBox.Name = "mobTimeBox";
            this.mobTimeBox.Size = new System.Drawing.Size(75, 29);
            this.mobTimeBox.TabIndex = 5;
            // 
            // mobTimeEnable
            // 
            this.mobTimeEnable.AutoSize = true;
            this.mobTimeEnable.Location = new System.Drawing.Point(194, 120);
            this.mobTimeEnable.Margin = new System.Windows.Forms.Padding(4);
            this.mobTimeEnable.Name = "mobTimeEnable";
            this.mobTimeEnable.Size = new System.Drawing.Size(113, 27);
            this.mobTimeEnable.TabIndex = 15;
            this.mobTimeEnable.Text = "Mob Time";
            this.mobTimeEnable.CheckedChanged += new System.EventHandler(this.enablingCheckBoxCheckChanged);
            // 
            // limitedNameEnable
            // 
            this.limitedNameEnable.AutoSize = true;
            this.limitedNameEnable.Location = new System.Drawing.Point(194, 231);
            this.limitedNameEnable.Margin = new System.Windows.Forms.Padding(4);
            this.limitedNameEnable.Name = "limitedNameEnable";
            this.limitedNameEnable.Size = new System.Drawing.Size(143, 27);
            this.limitedNameEnable.TabIndex = 16;
            this.limitedNameEnable.Text = "Limited Name";
            this.limitedNameEnable.CheckedChanged += new System.EventHandler(this.enablingCheckBoxCheckChanged);
            // 
            // infoEnable
            // 
            this.infoEnable.AutoSize = true;
            this.infoEnable.Location = new System.Drawing.Point(194, 154);
            this.infoEnable.Margin = new System.Windows.Forms.Padding(4);
            this.infoEnable.Name = "infoEnable";
            this.infoEnable.Size = new System.Drawing.Size(66, 27);
            this.infoEnable.TabIndex = 18;
            this.infoEnable.Text = "Info";
            this.infoEnable.CheckedChanged += new System.EventHandler(this.enablingCheckBoxCheckChanged);
            // 
            // teamEnable
            // 
            this.teamEnable.AutoSize = true;
            this.teamEnable.Location = new System.Drawing.Point(194, 194);
            this.teamEnable.Margin = new System.Windows.Forms.Padding(4);
            this.teamEnable.Name = "teamEnable";
            this.teamEnable.Size = new System.Drawing.Size(76, 27);
            this.teamEnable.TabIndex = 19;
            this.teamEnable.Text = "Team";
            this.teamEnable.CheckedChanged += new System.EventHandler(this.enablingCheckBoxCheckChanged);
            // 
            // hideBox
            // 
            this.hideBox.AutoSize = true;
            this.hideBox.Location = new System.Drawing.Point(194, 266);
            this.hideBox.Margin = new System.Windows.Forms.Padding(4);
            this.hideBox.Name = "hideBox";
            this.hideBox.Size = new System.Drawing.Size(71, 27);
            this.hideBox.TabIndex = 4;
            this.hideBox.Text = "Hide";
            // 
            // infoBox
            // 
            this.infoBox.Enabled = false;
            this.infoBox.Location = new System.Drawing.Point(338, 150);
            this.infoBox.Margin = new System.Windows.Forms.Padding(4);
            this.infoBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.infoBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.infoBox.Name = "infoBox";
            this.infoBox.Size = new System.Drawing.Size(75, 29);
            this.infoBox.TabIndex = 6;
            // 
            // teamBox
            // 
            this.teamBox.Enabled = false;
            this.teamBox.Location = new System.Drawing.Point(338, 189);
            this.teamBox.Margin = new System.Windows.Forms.Padding(4);
            this.teamBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.teamBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.teamBox.Name = "teamBox";
            this.teamBox.Size = new System.Drawing.Size(75, 29);
            this.teamBox.TabIndex = 7;
            // 
            // limitedNameBox
            // 
            this.limitedNameBox.Enabled = false;
            this.limitedNameBox.Location = new System.Drawing.Point(338, 228);
            this.limitedNameBox.Margin = new System.Windows.Forms.Padding(4);
            this.limitedNameBox.Name = "limitedNameBox";
            this.limitedNameBox.Size = new System.Drawing.Size(92, 29);
            this.limitedNameBox.TabIndex = 8;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(4, 280);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(61, 23);
            this.label5.TabIndex = 21;
            this.label5.Text = "Height";
            // 
            // yShiftBox
            // 
            this.yShiftBox.Location = new System.Drawing.Point(72, 278);
            this.yShiftBox.Margin = new System.Windows.Forms.Padding(4);
            this.yShiftBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.yShiftBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.yShiftBox.Name = "yShiftBox";
            this.yShiftBox.Size = new System.Drawing.Size(75, 29);
            this.yShiftBox.TabIndex = 20;
            // 
            // flipBox
            // 
            this.flipBox.AutoSize = true;
            this.flipBox.Location = new System.Drawing.Point(194, 300);
            this.flipBox.Margin = new System.Windows.Forms.Padding(4);
            this.flipBox.Name = "flipBox";
            this.flipBox.Size = new System.Drawing.Size(62, 27);
            this.flipBox.TabIndex = 22;
            this.flipBox.Text = "Flip";
            // 
            // LifeInstanceEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(440, 412);
            this.Controls.Add(this.flipBox);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.yShiftBox);
            this.Controls.Add(this.limitedNameBox);
            this.Controls.Add(this.teamBox);
            this.Controls.Add(this.infoBox);
            this.Controls.Add(this.hideBox);
            this.Controls.Add(this.teamEnable);
            this.Controls.Add(this.infoEnable);
            this.Controls.Add(this.limitedNameEnable);
            this.Controls.Add(this.mobTimeEnable);
            this.Controls.Add(this.mobTimeBox);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.rx1Box);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.rx0Box);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.yInput);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.xInput);
            this.Controls.Add(this.pathLabel);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Margin = new System.Windows.Forms.Padding(6);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LifeInstanceEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Life";
            ((System.ComponentModel.ISupportInitialize)(this.xInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.yInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rx0Box)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rx1Box)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.mobTimeBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.infoBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.teamBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.yShiftBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label pathLabel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown xInput;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown yInput;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown rx0Box;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.NumericUpDown rx1Box;
        private System.Windows.Forms.NumericUpDown mobTimeBox;
        private System.Windows.Forms.CheckBox mobTimeEnable;
        private System.Windows.Forms.CheckBox limitedNameEnable;
        private System.Windows.Forms.CheckBox infoEnable;
        private System.Windows.Forms.CheckBox teamEnable;
        private System.Windows.Forms.CheckBox hideBox;
        private System.Windows.Forms.NumericUpDown infoBox;
        private System.Windows.Forms.NumericUpDown teamBox;
        private System.Windows.Forms.TextBox limitedNameBox;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown yShiftBox;
        private System.Windows.Forms.CheckBox flipBox;
    }
}