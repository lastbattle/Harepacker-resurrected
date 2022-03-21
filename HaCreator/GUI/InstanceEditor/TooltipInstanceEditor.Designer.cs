namespace HaCreator.GUI.InstanceEditor
{
    partial class TooltipInstanceEditor
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
            this.titleBox = new System.Windows.Forms.TextBox();
            this.useTitleBox = new System.Windows.Forms.CheckBox();
            this.useDescBox = new System.Windows.Forms.CheckBox();
            this.descBox = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.xInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.yInput)).BeginInit();
            this.SuspendLayout();
            // 
            // pathLabel
            // 
            this.pathLabel.Location = new System.Drawing.Point(0, 1);
            this.pathLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.pathLabel.Name = "pathLabel";
            this.pathLabel.Size = new System.Drawing.Size(527, 73);
            this.pathLabel.TabIndex = 0;
            this.pathLabel.Text = "label1";
            this.pathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // xInput
            // 
            this.xInput.Location = new System.Drawing.Point(139, 70);
            this.xInput.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
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
            this.xInput.Size = new System.Drawing.Size(100, 29);
            this.xInput.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(115, 74);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(20, 23);
            this.label1.TabIndex = 2;
            this.label1.Text = "X";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(269, 74);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(19, 23);
            this.label2.TabIndex = 4;
            this.label2.Text = "Y";
            // 
            // yInput
            // 
            this.yInput.Location = new System.Drawing.Point(293, 70);
            this.yInput.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
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
            this.yInput.Size = new System.Drawing.Size(100, 29);
            this.yInput.TabIndex = 1;
            // 
            // okButton
            // 
            this.okButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.okButton.Location = new System.Drawing.Point(4, 420);
            this.okButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(260, 69);
            this.okButton.TabIndex = 3;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.cancelButton.Location = new System.Drawing.Point(267, 420);
            this.cancelButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(260, 69);
            this.cancelButton.TabIndex = 4;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // titleBox
            // 
            this.titleBox.Enabled = false;
            this.titleBox.Location = new System.Drawing.Point(89, 117);
            this.titleBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.titleBox.Multiline = true;
            this.titleBox.Name = "titleBox";
            this.titleBox.Size = new System.Drawing.Size(438, 140);
            this.titleBox.TabIndex = 7;
            // 
            // useTitleBox
            // 
            this.useTitleBox.AutoSize = true;
            this.useTitleBox.Location = new System.Drawing.Point(13, 174);
            this.useTitleBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.useTitleBox.Name = "useTitleBox";
            this.useTitleBox.Size = new System.Drawing.Size(68, 27);
            this.useTitleBox.TabIndex = 8;
            this.useTitleBox.Text = "Title";
            this.useTitleBox.UseVisualStyleBackColor = true;
            this.useTitleBox.CheckedChanged += new System.EventHandler(this.useTitleBox_CheckedChanged);
            // 
            // useDescBox
            // 
            this.useDescBox.AutoSize = true;
            this.useDescBox.Location = new System.Drawing.Point(13, 323);
            this.useDescBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.useDescBox.Name = "useDescBox";
            this.useDescBox.Size = new System.Drawing.Size(72, 27);
            this.useDescBox.TabIndex = 10;
            this.useDescBox.Text = "Desc";
            this.useDescBox.UseVisualStyleBackColor = true;
            this.useDescBox.CheckedChanged += new System.EventHandler(this.useDescBox_CheckedChanged);
            // 
            // descBox
            // 
            this.descBox.Enabled = false;
            this.descBox.Location = new System.Drawing.Point(89, 268);
            this.descBox.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.descBox.Multiline = true;
            this.descBox.Name = "descBox";
            this.descBox.Size = new System.Drawing.Size(438, 140);
            this.descBox.TabIndex = 9;
            // 
            // TooltipInstanceEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(531, 493);
            this.Controls.Add(this.useDescBox);
            this.Controls.Add(this.descBox);
            this.Controls.Add(this.useTitleBox);
            this.Controls.Add(this.titleBox);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.yInput);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.xInput);
            this.Controls.Add(this.pathLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TooltipInstanceEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "General";
            ((System.ComponentModel.ISupportInitialize)(this.xInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.yInput)).EndInit();
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
        private System.Windows.Forms.TextBox titleBox;
        private System.Windows.Forms.CheckBox useTitleBox;
        private System.Windows.Forms.CheckBox useDescBox;
        private System.Windows.Forms.TextBox descBox;
    }
}