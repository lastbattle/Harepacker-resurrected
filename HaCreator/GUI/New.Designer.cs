namespace HaCreator.GUI
{
    partial class New
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
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.newHeight = new System.Windows.Forms.NumericUpDown();
            this.newWidth = new System.Windows.Forms.NumericUpDown();
            this.newButton = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.label3 = new System.Windows.Forms.Label();
            this.numericUpDown1 = new System.Windows.Forms.NumericUpDown();
            this.buttonCreateFrmClone = new System.Windows.Forms.Button();
            this.button_SelectCloneMap = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.newHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.newWidth)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).BeginInit();
            this.SuspendLayout();
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(7, 33);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(42, 13);
            this.label2.TabIndex = 17;
            this.label2.Text = "Height";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(42, 13);
            this.label1.TabIndex = 16;
            this.label1.Text = "Width ";
            // 
            // newHeight
            // 
            this.newHeight.Location = new System.Drawing.Point(65, 32);
            this.newHeight.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.newHeight.Name = "newHeight";
            this.newHeight.Size = new System.Drawing.Size(260, 22);
            this.newHeight.TabIndex = 1;
            this.newHeight.Value = new decimal(new int[] {
            600,
            0,
            0,
            0});
            // 
            // newWidth
            // 
            this.newWidth.Location = new System.Drawing.Point(65, 10);
            this.newWidth.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.newWidth.Name = "newWidth";
            this.newWidth.Size = new System.Drawing.Size(260, 22);
            this.newWidth.TabIndex = 0;
            this.newWidth.Value = new decimal(new int[] {
            800,
            0,
            0,
            0});
            // 
            // newButton
            // 
            this.newButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.newButton.Location = new System.Drawing.Point(7, 121);
            this.newButton.Name = "newButton";
            this.newButton.Size = new System.Drawing.Size(318, 31);
            this.newButton.TabIndex = 2;
            this.newButton.Text = "Create";
            this.newButton.Click += new System.EventHandler(this.newButton_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(1, 2);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(344, 187);
            this.tabControl1.TabIndex = 18;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Controls.Add(this.newButton);
            this.tabPage1.Controls.Add(this.newWidth);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.newHeight);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(336, 161);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "New";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.button_SelectCloneMap);
            this.tabPage2.Controls.Add(this.buttonCreateFrmClone);
            this.tabPage2.Controls.Add(this.numericUpDown1);
            this.tabPage2.Controls.Add(this.label3);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(336, 161);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Clone";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(7, 14);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(47, 13);
            this.label3.TabIndex = 17;
            this.label3.Text = "Map ID:";
            // 
            // numericUpDown1
            // 
            this.numericUpDown1.Enabled = false;
            this.numericUpDown1.Location = new System.Drawing.Point(60, 12);
            this.numericUpDown1.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.numericUpDown1.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            this.numericUpDown1.Name = "numericUpDown1";
            this.numericUpDown1.Size = new System.Drawing.Size(265, 22);
            this.numericUpDown1.TabIndex = 18;
            this.numericUpDown1.Value = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            this.numericUpDown1.ValueChanged += new System.EventHandler(this.numericUpDown1_ValueChanged);
            // 
            // buttonCreateFrmClone
            // 
            this.buttonCreateFrmClone.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.buttonCreateFrmClone.Enabled = false;
            this.buttonCreateFrmClone.Location = new System.Drawing.Point(10, 121);
            this.buttonCreateFrmClone.Name = "buttonCreateFrmClone";
            this.buttonCreateFrmClone.Size = new System.Drawing.Size(315, 31);
            this.buttonCreateFrmClone.TabIndex = 19;
            this.buttonCreateFrmClone.Text = "Create";
            this.buttonCreateFrmClone.Click += new System.EventHandler(this.buttonCreateFrmClone_Click);
            // 
            // button_SelectCloneMap
            // 
            this.button_SelectCloneMap.Location = new System.Drawing.Point(240, 40);
            this.button_SelectCloneMap.Name = "button_SelectCloneMap";
            this.button_SelectCloneMap.Size = new System.Drawing.Size(85, 23);
            this.button_SelectCloneMap.TabIndex = 20;
            this.button_SelectCloneMap.Text = "Select map";
            this.button_SelectCloneMap.UseVisualStyleBackColor = true;
            this.button_SelectCloneMap.Click += new System.EventHandler(this.button_SelectCloneMap_Click);
            // 
            // New
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(342, 188);
            this.Controls.Add(this.tabControl1);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.MinimizeBox = false;
            this.Name = "New";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Create a new map";
            this.Load += new System.EventHandler(this.New_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.New_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.newHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.newWidth)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown newHeight;
        private System.Windows.Forms.NumericUpDown newWidth;
        private System.Windows.Forms.Button newButton;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button buttonCreateFrmClone;
        private System.Windows.Forms.NumericUpDown numericUpDown1;
        private System.Windows.Forms.Button button_SelectCloneMap;
    }
}