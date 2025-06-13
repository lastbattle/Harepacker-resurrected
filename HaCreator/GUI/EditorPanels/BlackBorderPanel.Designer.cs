using HaCreator.CustomControls;

namespace HaCreator.GUI.EditorPanels
{
    partial class BlackBorderPanel
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
            checkBox_side = new System.Windows.Forms.CheckBox();
            checkBox_bottom = new System.Windows.Forms.CheckBox();
            checkBox_top = new System.Windows.Forms.CheckBox();
            groupBox2 = new System.Windows.Forms.GroupBox();
            label3 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            numericUpDown_side = new System.Windows.Forms.NumericUpDown();
            numericUpDown_bottom = new System.Windows.Forms.NumericUpDown();
            numericUpDown_top = new System.Windows.Forms.NumericUpDown();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_side).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_bottom).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_top).BeginInit();
            SuspendLayout();
            // 
            // checkBox_side
            // 
            checkBox_side.AutoSize = true;
            checkBox_side.Location = new System.Drawing.Point(6, 94);
            checkBox_side.Name = "checkBox_side";
            checkBox_side.Size = new System.Drawing.Size(48, 17);
            checkBox_side.TabIndex = 2;
            checkBox_side.Text = "Side";
            checkBox_side.UseVisualStyleBackColor = true;
            checkBox_side.CheckedChanged += checkBox_side_CheckedChanged;
            // 
            // checkBox_bottom
            // 
            checkBox_bottom.AutoSize = true;
            checkBox_bottom.Location = new System.Drawing.Point(6, 71);
            checkBox_bottom.Name = "checkBox_bottom";
            checkBox_bottom.Size = new System.Drawing.Size(64, 17);
            checkBox_bottom.TabIndex = 1;
            checkBox_bottom.Text = "Bottom";
            checkBox_bottom.UseVisualStyleBackColor = true;
            checkBox_bottom.CheckedChanged += checkBox_bottom_CheckedChanged;
            // 
            // checkBox_top
            // 
            checkBox_top.AutoSize = true;
            checkBox_top.Location = new System.Drawing.Point(6, 48);
            checkBox_top.Name = "checkBox_top";
            checkBox_top.Size = new System.Drawing.Size(44, 17);
            checkBox_top.TabIndex = 0;
            checkBox_top.Text = "Top";
            checkBox_top.UseVisualStyleBackColor = true;
            checkBox_top.CheckedChanged += checkBox_top_CheckedChanged;
            // 
            // groupBox2
            // 
            groupBox2.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            groupBox2.Controls.Add(label3);
            groupBox2.Controls.Add(label2);
            groupBox2.Controls.Add(label1);
            groupBox2.Controls.Add(numericUpDown_side);
            groupBox2.Controls.Add(numericUpDown_bottom);
            groupBox2.Controls.Add(numericUpDown_top);
            groupBox2.Controls.Add(checkBox_side);
            groupBox2.Controls.Add(checkBox_bottom);
            groupBox2.Controls.Add(checkBox_top);
            groupBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            groupBox2.Location = new System.Drawing.Point(0, 0);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new System.Drawing.Size(284, 658);
            groupBox2.TabIndex = 1;
            groupBox2.TabStop = false;
            groupBox2.Text = "Set black borders";
            // 
            // label3
            // 
            label3.Font = new System.Drawing.Font("Segoe UI", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label3.Location = new System.Drawing.Point(3, 133);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(272, 25);
            label3.TabIndex = 8;
            label3.Text = "Preview it in the simulator after changes.";
            // 
            // label2
            // 
            label2.Font = new System.Drawing.Font("Segoe UI", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label2.Location = new System.Drawing.Point(117, 118);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(161, 15);
            label2.TabIndex = 7;
            label2.Text = "Height or width in px";
            // 
            // label1
            // 
            label1.Location = new System.Drawing.Point(6, 18);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(272, 27);
            label1.TabIndex = 6;
            label1.Text = "Resolves issues with pre-Big Bang maps and ensures compatibility with higher resolutions.";
            // 
            // numericUpDown_side
            // 
            numericUpDown_side.Enabled = false;
            numericUpDown_side.Location = new System.Drawing.Point(117, 93);
            numericUpDown_side.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            numericUpDown_side.Name = "numericUpDown_side";
            numericUpDown_side.Size = new System.Drawing.Size(161, 22);
            numericUpDown_side.TabIndex = 5;
            numericUpDown_side.ValueChanged += numericUpDown_side_ValueChanged;
            // 
            // numericUpDown_bottom
            // 
            numericUpDown_bottom.Enabled = false;
            numericUpDown_bottom.Location = new System.Drawing.Point(117, 70);
            numericUpDown_bottom.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            numericUpDown_bottom.Name = "numericUpDown_bottom";
            numericUpDown_bottom.Size = new System.Drawing.Size(161, 22);
            numericUpDown_bottom.TabIndex = 4;
            numericUpDown_bottom.ValueChanged += numericUpDown_bottom_ValueChanged;
            // 
            // numericUpDown_top
            // 
            numericUpDown_top.Enabled = false;
            numericUpDown_top.Location = new System.Drawing.Point(117, 47);
            numericUpDown_top.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            numericUpDown_top.Name = "numericUpDown_top";
            numericUpDown_top.Size = new System.Drawing.Size(161, 22);
            numericUpDown_top.TabIndex = 3;
            numericUpDown_top.ValueChanged += numericUpDown_top_ValueChanged;
            // 
            // BlackBorderPanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            Controls.Add(groupBox2);
            Font = new System.Drawing.Font("Segoe UI", 8.25F);
            Name = "BlackBorderPanel";
            Size = new System.Drawing.Size(284, 658);
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_side).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_bottom).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_top).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private global::System.Resources.ResourceManager resourceMan;
        private System.Windows.Forms.CheckBox checkBox_side;
        private System.Windows.Forms.CheckBox checkBox_bottom;
        private System.Windows.Forms.CheckBox checkBox_top;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.NumericUpDown numericUpDown_side;
        private System.Windows.Forms.NumericUpDown numericUpDown_bottom;
        private System.Windows.Forms.NumericUpDown numericUpDown_top;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;

        public global::System.Resources.ResourceManager ResourceManager
        {
            get
            {
                if (object.ReferenceEquals(resourceMan, null))
                {
                    string baseName = this.GetType().Namespace + "." + this.GetType().Name;
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager(baseName, this.GetType().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
    }
}