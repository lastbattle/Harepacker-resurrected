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
            pathLabel = new System.Windows.Forms.Label();
            xInput = new System.Windows.Forms.NumericUpDown();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            yInput = new System.Windows.Forms.NumericUpDown();
            label3 = new System.Windows.Forms.Label();
            zInput = new System.Windows.Forms.NumericUpDown();
            okButton = new System.Windows.Forms.Button();
            cancelButton = new System.Windows.Forms.Button();
            label4 = new System.Windows.Forms.Label();
            typeBox = new System.Windows.Forms.ComboBox();
            label5 = new System.Windows.Forms.Label();
            alphaBox = new System.Windows.Forms.NumericUpDown();
            front = new System.Windows.Forms.CheckBox();
            label6 = new System.Windows.Forms.Label();
            ryBox = new System.Windows.Forms.NumericUpDown();
            label7 = new System.Windows.Forms.Label();
            rxBox = new System.Windows.Forms.NumericUpDown();
            copyLabel = new System.Windows.Forms.Label();
            cyLabel = new System.Windows.Forms.Label();
            cyBox = new System.Windows.Forms.NumericUpDown();
            cxLabel = new System.Windows.Forms.Label();
            cxBox = new System.Windows.Forms.NumericUpDown();
            label9 = new System.Windows.Forms.Label();
            comboBox_screenMode = new System.Windows.Forms.ComboBox();
            checkBox_spineRandomStart = new System.Windows.Forms.CheckBox();
            groupBox_spine = new System.Windows.Forms.GroupBox();
            label11 = new System.Windows.Forms.Label();
            comboBox_spineAnimation = new System.Windows.Forms.ComboBox();
            groupBox1 = new System.Windows.Forms.GroupBox();
            label8 = new System.Windows.Forms.Label();
            trackBar_parallaxY = new System.Windows.Forms.TrackBar();
            trackBar_parallaxX = new System.Windows.Forms.TrackBar();
            groupBox2 = new System.Windows.Forms.GroupBox();
            groupBox3 = new System.Windows.Forms.GroupBox();
            labelTypeDescription = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)xInput).BeginInit();
            ((System.ComponentModel.ISupportInitialize)yInput).BeginInit();
            ((System.ComponentModel.ISupportInitialize)zInput).BeginInit();
            ((System.ComponentModel.ISupportInitialize)alphaBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)ryBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)rxBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)cyBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)cxBox).BeginInit();
            groupBox_spine.SuspendLayout();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trackBar_parallaxY).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackBar_parallaxX).BeginInit();
            groupBox2.SuspendLayout();
            groupBox3.SuspendLayout();
            SuspendLayout();
            // 
            // pathLabel
            // 
            pathLabel.Location = new System.Drawing.Point(2, 0);
            pathLabel.Name = "pathLabel";
            pathLabel.Size = new System.Drawing.Size(501, 41);
            pathLabel.TabIndex = 0;
            pathLabel.Text = "label1";
            pathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // xInput
            // 
            xInput.Location = new System.Drawing.Point(26, 21);
            xInput.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            xInput.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            xInput.Name = "xInput";
            xInput.Size = new System.Drawing.Size(50, 22);
            xInput.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(10, 24);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(13, 13);
            label1.TabIndex = 2;
            label1.Text = "X";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(87, 24);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(12, 13);
            label2.TabIndex = 4;
            label2.Text = "Y";
            // 
            // yInput
            // 
            yInput.Location = new System.Drawing.Point(103, 21);
            yInput.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            yInput.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            yInput.Name = "yInput";
            yInput.Size = new System.Drawing.Size(50, 22);
            yInput.TabIndex = 1;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(10, 52);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(13, 13);
            label3.TabIndex = 6;
            label3.Text = "Z";
            // 
            // zInput
            // 
            zInput.Location = new System.Drawing.Point(26, 49);
            zInput.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            zInput.Name = "zInput";
            zInput.Size = new System.Drawing.Size(50, 22);
            zInput.TabIndex = 2;
            // 
            // okButton
            // 
            okButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            okButton.Location = new System.Drawing.Point(9, 439);
            okButton.Name = "okButton";
            okButton.Size = new System.Drawing.Size(228, 28);
            okButton.TabIndex = 10;
            okButton.Text = "OK";
            okButton.Click += okButton_Click;
            // 
            // cancelButton
            // 
            cancelButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            cancelButton.Location = new System.Drawing.Point(254, 439);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new System.Drawing.Size(242, 28);
            cancelButton.TabIndex = 11;
            cancelButton.Text = "Cancel";
            cancelButton.Click += cancelButton_Click;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(6, 21);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(32, 13);
            label4.TabIndex = 9;
            label4.Text = "Type:";
            // 
            // typeBox
            // 
            typeBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            typeBox.FormattingEnabled = true;
            typeBox.ItemHeight = 13;
            typeBox.Location = new System.Drawing.Point(45, 21);
            typeBox.Name = "typeBox";
            typeBox.Size = new System.Drawing.Size(184, 21);
            typeBox.TabIndex = 3;
            typeBox.SelectedIndexChanged += typeBox_SelectedIndexChanged;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new System.Drawing.Point(6, 86);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(115, 13);
            label5.TabIndex = 11;
            label5.Text = "Alpha: (Transparency)";
            // 
            // alphaBox
            // 
            alphaBox.Location = new System.Drawing.Point(128, 84);
            alphaBox.Maximum = new decimal(new int[] { 255, 0, 0, 0 });
            alphaBox.Name = "alphaBox";
            alphaBox.Size = new System.Drawing.Size(100, 22);
            alphaBox.TabIndex = 4;
            // 
            // front
            // 
            front.AutoSize = true;
            front.Location = new System.Drawing.Point(9, 139);
            front.Name = "front";
            front.Size = new System.Drawing.Size(120, 17);
            front.TabIndex = 5;
            front.Text = "Front Background";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new System.Drawing.Point(11, 73);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(12, 13);
            label6.TabIndex = 17;
            label6.Text = "Y";
            // 
            // ryBox
            // 
            ryBox.Location = new System.Drawing.Point(27, 70);
            ryBox.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            ryBox.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            ryBox.Name = "ryBox";
            ryBox.Size = new System.Drawing.Size(50, 22);
            ryBox.TabIndex = 7;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new System.Drawing.Point(10, 18);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(13, 13);
            label7.TabIndex = 15;
            label7.Text = "X";
            // 
            // rxBox
            // 
            rxBox.Location = new System.Drawing.Point(26, 15);
            rxBox.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            rxBox.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            rxBox.Name = "rxBox";
            rxBox.Size = new System.Drawing.Size(50, 22);
            rxBox.TabIndex = 6;
            // 
            // copyLabel
            // 
            copyLabel.AutoSize = true;
            copyLabel.Location = new System.Drawing.Point(6, 49);
            copyLabel.Name = "copyLabel";
            copyLabel.Size = new System.Drawing.Size(36, 13);
            copyLabel.TabIndex = 23;
            copyLabel.Text = "Copy:";
            // 
            // cyLabel
            // 
            cyLabel.AutoSize = true;
            cyLabel.Location = new System.Drawing.Point(134, 49);
            cyLabel.Name = "cyLabel";
            cyLabel.Size = new System.Drawing.Size(12, 13);
            cyLabel.TabIndex = 22;
            cyLabel.Text = "Y";
            // 
            // cyBox
            // 
            cyBox.Location = new System.Drawing.Point(150, 46);
            cyBox.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            cyBox.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            cyBox.Name = "cyBox";
            cyBox.Size = new System.Drawing.Size(50, 22);
            cyBox.TabIndex = 9;
            cyBox.ValueChanged += cyBox_ValueChanged;
            // 
            // cxLabel
            // 
            cxLabel.AutoSize = true;
            cxLabel.Location = new System.Drawing.Point(57, 49);
            cxLabel.Name = "cxLabel";
            cxLabel.Size = new System.Drawing.Size(13, 13);
            cxLabel.TabIndex = 20;
            cxLabel.Text = "X";
            // 
            // cxBox
            // 
            cxBox.Location = new System.Drawing.Point(73, 46);
            cxBox.Maximum = new decimal(new int[] { int.MaxValue, 0, 0, 0 });
            cxBox.Minimum = new decimal(new int[] { int.MinValue, 0, 0, int.MinValue });
            cxBox.Name = "cxBox";
            cxBox.Size = new System.Drawing.Size(50, 22);
            cxBox.TabIndex = 8;
            cxBox.ValueChanged += cxBox_ValueChanged;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new System.Drawing.Point(6, 114);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(77, 13);
            label9.TabIndex = 24;
            label9.Text = "Screen Mode:";
            // 
            // comboBox_screenMode
            // 
            comboBox_screenMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBox_screenMode.FormattingEnabled = true;
            comboBox_screenMode.ItemHeight = 13;
            comboBox_screenMode.Location = new System.Drawing.Point(86, 111);
            comboBox_screenMode.Name = "comboBox_screenMode";
            comboBox_screenMode.Size = new System.Drawing.Size(142, 21);
            comboBox_screenMode.TabIndex = 25;
            // 
            // checkBox_spineRandomStart
            // 
            checkBox_spineRandomStart.AutoSize = true;
            checkBox_spineRandomStart.Location = new System.Drawing.Point(7, 21);
            checkBox_spineRandomStart.Name = "checkBox_spineRandomStart";
            checkBox_spineRandomStart.Size = new System.Drawing.Size(96, 17);
            checkBox_spineRandomStart.TabIndex = 27;
            checkBox_spineRandomStart.Text = "Random Start";
            // 
            // groupBox_spine
            // 
            groupBox_spine.Controls.Add(label11);
            groupBox_spine.Controls.Add(comboBox_spineAnimation);
            groupBox_spine.Controls.Add(checkBox_spineRandomStart);
            groupBox_spine.Location = new System.Drawing.Point(9, 354);
            groupBox_spine.Name = "groupBox_spine";
            groupBox_spine.Size = new System.Drawing.Size(487, 79);
            groupBox_spine.TabIndex = 31;
            groupBox_spine.TabStop = false;
            groupBox_spine.Text = "Spine";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new System.Drawing.Point(6, 45);
            label11.Name = "label11";
            label11.Size = new System.Drawing.Size(63, 13);
            label11.TabIndex = 32;
            label11.Text = "Animation:";
            // 
            // comboBox_spineAnimation
            // 
            comboBox_spineAnimation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBox_spineAnimation.FormattingEnabled = true;
            comboBox_spineAnimation.Location = new System.Drawing.Point(75, 42);
            comboBox_spineAnimation.Name = "comboBox_spineAnimation";
            comboBox_spineAnimation.Size = new System.Drawing.Size(153, 21);
            comboBox_spineAnimation.TabIndex = 31;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(label8);
            groupBox1.Controls.Add(trackBar_parallaxY);
            groupBox1.Controls.Add(trackBar_parallaxX);
            groupBox1.Controls.Add(ryBox);
            groupBox1.Controls.Add(rxBox);
            groupBox1.Controls.Add(label7);
            groupBox1.Controls.Add(label6);
            groupBox1.Location = new System.Drawing.Point(9, 213);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(487, 134);
            groupBox1.TabIndex = 32;
            groupBox1.TabStop = false;
            groupBox1.Text = "Parallax";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new System.Drawing.Point(164, 114);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(281, 13);
            label8.TabIndex = 20;
            label8.Text = "Further <<<<<< Parallax distance  >>>>>>> Closer";
            // 
            // trackBar_parallaxY
            // 
            trackBar_parallaxY.LargeChange = 1;
            trackBar_parallaxY.Location = new System.Drawing.Point(95, 66);
            trackBar_parallaxY.Maximum = 200;
            trackBar_parallaxY.Minimum = -200;
            trackBar_parallaxY.Name = "trackBar_parallaxY";
            trackBar_parallaxY.Size = new System.Drawing.Size(378, 45);
            trackBar_parallaxY.TabIndex = 19;
            trackBar_parallaxY.Scroll += trackBar_parallaxY_Scroll;
            // 
            // trackBar_parallaxX
            // 
            trackBar_parallaxX.LargeChange = 1;
            trackBar_parallaxX.Location = new System.Drawing.Point(95, 15);
            trackBar_parallaxX.Maximum = 200;
            trackBar_parallaxX.Minimum = -200;
            trackBar_parallaxX.Name = "trackBar_parallaxX";
            trackBar_parallaxX.Size = new System.Drawing.Size(378, 45);
            trackBar_parallaxX.TabIndex = 18;
            trackBar_parallaxX.Scroll += trackBar_parallaxX_Scroll;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(zInput);
            groupBox2.Controls.Add(xInput);
            groupBox2.Controls.Add(label1);
            groupBox2.Controls.Add(yInput);
            groupBox2.Controls.Add(label2);
            groupBox2.Controls.Add(label3);
            groupBox2.Location = new System.Drawing.Point(9, 41);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new System.Drawing.Size(228, 165);
            groupBox2.TabIndex = 33;
            groupBox2.TabStop = false;
            groupBox2.Text = "Position";
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(comboBox_screenMode);
            groupBox3.Controls.Add(label4);
            groupBox3.Controls.Add(typeBox);
            groupBox3.Controls.Add(labelTypeDescription);
            groupBox3.Controls.Add(label5);
            groupBox3.Controls.Add(alphaBox);
            groupBox3.Controls.Add(front);
            groupBox3.Controls.Add(cxBox);
            groupBox3.Controls.Add(cxLabel);
            groupBox3.Controls.Add(label9);
            groupBox3.Controls.Add(cyBox);
            groupBox3.Controls.Add(copyLabel);
            groupBox3.Controls.Add(cyLabel);
            groupBox3.Location = new System.Drawing.Point(254, 41);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new System.Drawing.Size(242, 165);
            groupBox3.TabIndex = 34;
            groupBox3.TabStop = false;
            groupBox3.Text = "Etc";
            // 
            // labelTypeDescription
            // 
            labelTypeDescription.ForeColor = System.Drawing.Color.DimGray;
            labelTypeDescription.Location = new System.Drawing.Point(6, 45);
            labelTypeDescription.Name = "labelTypeDescription";
            labelTypeDescription.Size = new System.Drawing.Size(229, 31);
            labelTypeDescription.TabIndex = 26;
            labelTypeDescription.Text = "Static background, no tiling or movement.";
            // 
            // BackgroundInstanceEditor
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(503, 474);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(groupBox_spine);
            Controls.Add(cancelButton);
            Controls.Add(okButton);
            Controls.Add(pathLabel);
            DoubleBuffered = true;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "BackgroundInstanceEditor";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Background";
            ((System.ComponentModel.ISupportInitialize)xInput).EndInit();
            ((System.ComponentModel.ISupportInitialize)yInput).EndInit();
            ((System.ComponentModel.ISupportInitialize)zInput).EndInit();
            ((System.ComponentModel.ISupportInitialize)alphaBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)ryBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)rxBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)cyBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)cxBox).EndInit();
            groupBox_spine.ResumeLayout(false);
            groupBox_spine.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trackBar_parallaxY).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackBar_parallaxX).EndInit();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            ResumeLayout(false);

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
        private System.Windows.Forms.Label copyLabel;
        private System.Windows.Forms.Label cyLabel;
        private System.Windows.Forms.NumericUpDown cyBox;
        private System.Windows.Forms.Label cxLabel;
        private System.Windows.Forms.NumericUpDown cxBox;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox comboBox_screenMode;
        private System.Windows.Forms.CheckBox checkBox_spineRandomStart;
        private System.Windows.Forms.GroupBox groupBox_spine;
        private System.Windows.Forms.ComboBox comboBox_spineAnimation;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TrackBar trackBar_parallaxY;
        private System.Windows.Forms.TrackBar trackBar_parallaxX;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label labelTypeDescription;
    }
}