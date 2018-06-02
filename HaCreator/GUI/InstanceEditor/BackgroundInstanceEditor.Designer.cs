namespace HaCreator.GUI.InstanceEditor
{
    partial class BackgroundInstanceEditor
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
            this.label3 = new System.Windows.Forms.Label();
            this.zInput = new System.Windows.Forms.NumericUpDown();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.typeBox = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.alphaBox = new System.Windows.Forms.NumericUpDown();
            this.front = new System.Windows.Forms.CheckBox();
            this.label6 = new System.Windows.Forms.Label();
            this.ryBox = new System.Windows.Forms.NumericUpDown();
            this.label7 = new System.Windows.Forms.Label();
            this.rxBox = new System.Windows.Forms.NumericUpDown();
            this.label8 = new System.Windows.Forms.Label();
            this.copyLabel = new System.Windows.Forms.Label();
            this.cyLabel = new System.Windows.Forms.Label();
            this.cyBox = new System.Windows.Forms.NumericUpDown();
            this.cxLabel = new System.Windows.Forms.Label();
            this.cxBox = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.xInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.yInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.zInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.alphaBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ryBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rxBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cyBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cxBox)).BeginInit();
            this.SuspendLayout();
            // 
            // pathLabel
            // 
            this.pathLabel.Location = new System.Drawing.Point(12, 12);
            this.pathLabel.Name = "pathLabel";
            this.pathLabel.Size = new System.Drawing.Size(226, 35);
            this.pathLabel.TabIndex = 0;
            this.pathLabel.Text = "label1";
            this.pathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // xInput
            // 
            this.xInput.Location = new System.Drawing.Point(28, 53);
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
            this.label1.Location = new System.Drawing.Point(12, 56);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(14, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "X";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(89, 56);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(14, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Y";
            // 
            // yInput
            // 
            this.yInput.Location = new System.Drawing.Point(105, 53);
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
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(168, 56);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(14, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Z";
            // 
            // zInput
            // 
            this.zInput.Location = new System.Drawing.Point(184, 53);
            this.zInput.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.zInput.Name = "zInput";
            this.zInput.Size = new System.Drawing.Size(50, 20);
            this.zInput.TabIndex = 2;
            // 
            // okButton
            // 
            this.okButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.okButton.Location = new System.Drawing.Point(48, 183);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(76, 28);
            this.okButton.TabIndex = 10;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.cancelButton.Location = new System.Drawing.Point(130, 183);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(73, 28);
            this.cancelButton.TabIndex = 11;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(12, 81);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(34, 13);
            this.label4.TabIndex = 9;
            this.label4.Text = "Type:";
            // 
            // typeBox
            // 
            this.typeBox.FormattingEnabled = true;
            this.typeBox.ItemHeight = 13;
            this.typeBox.Location = new System.Drawing.Point(48, 79);
            this.typeBox.Name = "typeBox";
            this.typeBox.Size = new System.Drawing.Size(186, 21);
            this.typeBox.TabIndex = 3;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(12, 108);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(37, 13);
            this.label5.TabIndex = 11;
            this.label5.Text = "Alpha:";
            // 
            // alphaBox
            // 
            this.alphaBox.Location = new System.Drawing.Point(52, 105);
            this.alphaBox.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.alphaBox.Name = "alphaBox";
            this.alphaBox.Size = new System.Drawing.Size(50, 20);
            this.alphaBox.TabIndex = 4;
            // 
            // front
            // 
            this.front.AutoSize = true;
            this.front.Location = new System.Drawing.Point(121, 108);
            this.front.Name = "front";
            this.front.Size = new System.Drawing.Size(111, 17);
            this.front.TabIndex = 5;
            this.front.Text = "Front Background";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(140, 134);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(14, 13);
            this.label6.TabIndex = 17;
            this.label6.Text = "Y";
            // 
            // ryBox
            // 
            this.ryBox.Location = new System.Drawing.Point(156, 131);
            this.ryBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.ryBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.ryBox.Name = "ryBox";
            this.ryBox.Size = new System.Drawing.Size(50, 20);
            this.ryBox.TabIndex = 7;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(63, 134);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(14, 13);
            this.label7.TabIndex = 15;
            this.label7.Text = "X";
            // 
            // rxBox
            // 
            this.rxBox.Location = new System.Drawing.Point(79, 131);
            this.rxBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.rxBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.rxBox.Name = "rxBox";
            this.rxBox.Size = new System.Drawing.Size(50, 20);
            this.rxBox.TabIndex = 6;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(12, 134);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(47, 13);
            this.label8.TabIndex = 18;
            this.label8.Text = "Parallax:";
            // 
            // copyLabel
            // 
            this.copyLabel.AutoSize = true;
            this.copyLabel.Location = new System.Drawing.Point(12, 160);
            this.copyLabel.Name = "copyLabel";
            this.copyLabel.Size = new System.Drawing.Size(34, 13);
            this.copyLabel.TabIndex = 23;
            this.copyLabel.Text = "Copy:";
            // 
            // cyLabel
            // 
            this.cyLabel.AutoSize = true;
            this.cyLabel.Location = new System.Drawing.Point(140, 160);
            this.cyLabel.Name = "cyLabel";
            this.cyLabel.Size = new System.Drawing.Size(14, 13);
            this.cyLabel.TabIndex = 22;
            this.cyLabel.Text = "Y";
            // 
            // cyBox
            // 
            this.cyBox.Location = new System.Drawing.Point(156, 157);
            this.cyBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.cyBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.cyBox.Name = "cyBox";
            this.cyBox.Size = new System.Drawing.Size(50, 20);
            this.cyBox.TabIndex = 9;
            // 
            // cxLabel
            // 
            this.cxLabel.AutoSize = true;
            this.cxLabel.Location = new System.Drawing.Point(63, 160);
            this.cxLabel.Name = "cxLabel";
            this.cxLabel.Size = new System.Drawing.Size(14, 13);
            this.cxLabel.TabIndex = 20;
            this.cxLabel.Text = "X";
            // 
            // cxBox
            // 
            this.cxBox.Location = new System.Drawing.Point(79, 157);
            this.cxBox.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.cxBox.Minimum = new decimal(new int[] {
            -2147483648,
            0,
            0,
            -2147483648});
            this.cxBox.Name = "cxBox";
            this.cxBox.Size = new System.Drawing.Size(50, 20);
            this.cxBox.TabIndex = 8;
            // 
            // BackgroundInstanceEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(246, 218);
            this.Controls.Add(this.copyLabel);
            this.Controls.Add(this.cyLabel);
            this.Controls.Add(this.cyBox);
            this.Controls.Add(this.cxLabel);
            this.Controls.Add(this.cxBox);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.ryBox);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.rxBox);
            this.Controls.Add(this.front);
            this.Controls.Add(this.alphaBox);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.typeBox);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.zInput);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.yInput);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.xInput);
            this.Controls.Add(this.pathLabel);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BackgroundInstanceEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Background";
            ((System.ComponentModel.ISupportInitialize)(this.xInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.yInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.zInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.alphaBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ryBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rxBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cyBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cxBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label pathLabel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown xInput;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown yInput;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown zInput;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox typeBox;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown alphaBox;
        private System.Windows.Forms.CheckBox front;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.NumericUpDown ryBox;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.NumericUpDown rxBox;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label copyLabel;
        private System.Windows.Forms.Label cyLabel;
        private System.Windows.Forms.NumericUpDown cyBox;
        private System.Windows.Forms.Label cxLabel;
        private System.Windows.Forms.NumericUpDown cxBox;
    }
}