namespace HaCreator.GUI.InstanceEditor
{
    partial class MirrorFieldEditor
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
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.comboBox_objectForOverlay = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.numericUpDown_yOffsetValue = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.numericUpDown_xOffsetValue = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.label_gradient = new System.Windows.Forms.Label();
            this.label_alphaValue = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.trackBar_alpha = new System.Windows.Forms.TrackBar();
            this.label1 = new System.Windows.Forms.Label();
            this.trackBar_gradient = new System.Windows.Forms.TrackBar();
            this.checkBox_alphaTest = new System.Windows.Forms.CheckBox();
            this.checkBox_reflection = new System.Windows.Forms.CheckBox();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_yOffsetValue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_xOffsetValue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar_alpha)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar_gradient)).BeginInit();
            this.SuspendLayout();
            // 
            // pathLabel
            // 
            this.pathLabel.Location = new System.Drawing.Point(0, 0);
            this.pathLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.pathLabel.Name = "pathLabel";
            this.pathLabel.Size = new System.Drawing.Size(469, 107);
            this.pathLabel.TabIndex = 0;
            this.pathLabel.Text = "label1";
            this.pathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // okButton
            // 
            this.okButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.okButton.Location = new System.Drawing.Point(4, 494);
            this.okButton.Margin = new System.Windows.Forms.Padding(4);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(232, 62);
            this.okButton.TabIndex = 23;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.cancelButton.Location = new System.Drawing.Point(237, 494);
            this.cancelButton.Margin = new System.Windows.Forms.Padding(4);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(232, 62);
            this.cancelButton.TabIndex = 24;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // comboBox_objectForOverlay
            // 
            this.comboBox_objectForOverlay.FormattingEnabled = true;
            this.comboBox_objectForOverlay.Items.AddRange(new object[] {
            "mirror"});
            this.comboBox_objectForOverlay.Location = new System.Drawing.Point(182, 59);
            this.comboBox_objectForOverlay.Name = "comboBox_objectForOverlay";
            this.comboBox_objectForOverlay.Size = new System.Drawing.Size(275, 31);
            this.comboBox_objectForOverlay.TabIndex = 27;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(10, 62);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(149, 23);
            this.label4.TabIndex = 28;
            this.label4.Text = "Object for overlay:";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.numericUpDown_yOffsetValue);
            this.panel1.Controls.Add(this.label3);
            this.panel1.Controls.Add(this.numericUpDown_xOffsetValue);
            this.panel1.Controls.Add(this.label5);
            this.panel1.Controls.Add(this.label_gradient);
            this.panel1.Controls.Add(this.label_alphaValue);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.trackBar_alpha);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.trackBar_gradient);
            this.panel1.Controls.Add(this.checkBox_alphaTest);
            this.panel1.Controls.Add(this.checkBox_reflection);
            this.panel1.Controls.Add(this.label4);
            this.panel1.Controls.Add(this.comboBox_objectForOverlay);
            this.panel1.Location = new System.Drawing.Point(3, 121);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(466, 366);
            this.panel1.TabIndex = 29;
            // 
            // numericUpDown_yOffsetValue
            // 
            this.numericUpDown_yOffsetValue.Location = new System.Drawing.Point(319, 10);
            this.numericUpDown_yOffsetValue.Margin = new System.Windows.Forms.Padding(4);
            this.numericUpDown_yOffsetValue.Maximum = new decimal(new int[] {
            15000,
            0,
            0,
            0});
            this.numericUpDown_yOffsetValue.Minimum = new decimal(new int[] {
            15000,
            0,
            0,
            -2147483648});
            this.numericUpDown_yOffsetValue.Name = "numericUpDown_yOffsetValue";
            this.numericUpDown_yOffsetValue.Size = new System.Drawing.Size(122, 29);
            this.numericUpDown_yOffsetValue.TabIndex = 38;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(10, 12);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(70, 23);
            this.label3.TabIndex = 40;
            this.label3.Text = "Offset X";
            // 
            // numericUpDown_xOffsetValue
            // 
            this.numericUpDown_xOffsetValue.Location = new System.Drawing.Point(88, 10);
            this.numericUpDown_xOffsetValue.Margin = new System.Windows.Forms.Padding(4);
            this.numericUpDown_xOffsetValue.Maximum = new decimal(new int[] {
            15000,
            0,
            0,
            0});
            this.numericUpDown_xOffsetValue.Minimum = new decimal(new int[] {
            15000,
            0,
            0,
            -2147483648});
            this.numericUpDown_xOffsetValue.Name = "numericUpDown_xOffsetValue";
            this.numericUpDown_xOffsetValue.Size = new System.Drawing.Size(122, 29);
            this.numericUpDown_xOffsetValue.TabIndex = 39;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(238, 12);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(69, 23);
            this.label5.TabIndex = 41;
            this.label5.Text = "Offset Y";
            // 
            // label_gradient
            // 
            this.label_gradient.AutoSize = true;
            this.label_gradient.Location = new System.Drawing.Point(45, 212);
            this.label_gradient.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label_gradient.Name = "label_gradient";
            this.label_gradient.Size = new System.Drawing.Size(19, 23);
            this.label_gradient.TabIndex = 37;
            this.label_gradient.Text = "0";
            // 
            // label_alphaValue
            // 
            this.label_alphaValue.AutoSize = true;
            this.label_alphaValue.Location = new System.Drawing.Point(45, 287);
            this.label_alphaValue.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label_alphaValue.Name = "label_alphaValue";
            this.label_alphaValue.Size = new System.Drawing.Size(19, 23);
            this.label_alphaValue.TabIndex = 36;
            this.label_alphaValue.Text = "0";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 264);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(54, 23);
            this.label2.TabIndex = 35;
            this.label2.Text = "Alpha";
            // 
            // trackBar_alpha
            // 
            this.trackBar_alpha.Location = new System.Drawing.Point(107, 264);
            this.trackBar_alpha.Maximum = 255;
            this.trackBar_alpha.Name = "trackBar_alpha";
            this.trackBar_alpha.Size = new System.Drawing.Size(350, 69);
            this.trackBar_alpha.TabIndex = 34;
            this.trackBar_alpha.ValueChanged += new System.EventHandler(this.trackBar_alpha_ValueChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 189);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(76, 23);
            this.label1.TabIndex = 33;
            this.label1.Text = "Gradient";
            // 
            // trackBar_gradient
            // 
            this.trackBar_gradient.Location = new System.Drawing.Point(107, 189);
            this.trackBar_gradient.Maximum = 255;
            this.trackBar_gradient.Name = "trackBar_gradient";
            this.trackBar_gradient.Size = new System.Drawing.Size(350, 69);
            this.trackBar_gradient.TabIndex = 32;
            this.trackBar_gradient.ValueChanged += new System.EventHandler(this.trackBar_gradient_ValueChanged);
            // 
            // checkBox_alphaTest
            // 
            this.checkBox_alphaTest.AutoSize = true;
            this.checkBox_alphaTest.Location = new System.Drawing.Point(14, 138);
            this.checkBox_alphaTest.Margin = new System.Windows.Forms.Padding(4);
            this.checkBox_alphaTest.Name = "checkBox_alphaTest";
            this.checkBox_alphaTest.Size = new System.Drawing.Size(114, 27);
            this.checkBox_alphaTest.TabIndex = 31;
            this.checkBox_alphaTest.Text = "Alpha Test";
            // 
            // checkBox_reflection
            // 
            this.checkBox_reflection.AutoSize = true;
            this.checkBox_reflection.Location = new System.Drawing.Point(14, 103);
            this.checkBox_reflection.Margin = new System.Windows.Forms.Padding(4);
            this.checkBox_reflection.Name = "checkBox_reflection";
            this.checkBox_reflection.Size = new System.Drawing.Size(111, 27);
            this.checkBox_reflection.TabIndex = 30;
            this.checkBox_reflection.Text = "Reflection";
            // 
            // MirrorFieldEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(472, 561);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.pathLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Margin = new System.Windows.Forms.Padding(6);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MirrorFieldEditor";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Object";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_yOffsetValue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_xOffsetValue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar_alpha)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar_gradient)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label pathLabel;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.ComboBox comboBox_objectForOverlay;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.CheckBox checkBox_alphaTest;
        private System.Windows.Forms.CheckBox checkBox_reflection;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TrackBar trackBar_alpha;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TrackBar trackBar_gradient;
        private System.Windows.Forms.Label label_gradient;
        private System.Windows.Forms.Label label_alphaValue;
        private System.Windows.Forms.NumericUpDown numericUpDown_yOffsetValue;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown numericUpDown_xOffsetValue;
        private System.Windows.Forms.Label label5;
    }
}