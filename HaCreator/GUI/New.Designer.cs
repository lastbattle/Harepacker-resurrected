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
            ((System.ComponentModel.ISupportInitialize)(this.newHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.newWidth)).BeginInit();
            this.SuspendLayout();
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(158, 15);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(38, 13);
            this.label2.TabIndex = 17;
            this.label2.Text = "Height";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(57, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(54, 13);
            this.label1.TabIndex = 16;
            this.label1.Text = "Width    X";
            // 
            // newHeight
            // 
            this.newHeight.Location = new System.Drawing.Point(117, 12);
            this.newHeight.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.newHeight.Name = "newHeight";
            this.newHeight.Size = new System.Drawing.Size(41, 20);
            this.newHeight.TabIndex = 1;
            this.newHeight.Value = new decimal(new int[] {
            600,
            0,
            0,
            0});
            // 
            // newWidth
            // 
            this.newWidth.Location = new System.Drawing.Point(12, 12);
            this.newWidth.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.newWidth.Name = "newWidth";
            this.newWidth.Size = new System.Drawing.Size(45, 20);
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
            this.newButton.Location = new System.Drawing.Point(12, 38);
            this.newButton.Name = "newButton";
            this.newButton.Size = new System.Drawing.Size(194, 30);
            this.newButton.TabIndex = 2;
            this.newButton.Text = "Create";
            this.newButton.Click += new System.EventHandler(this.newButton_Click);
            // 
            // New
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(218, 80);
            this.Controls.Add(this.newButton);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.newHeight);
            this.Controls.Add(this.newWidth);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.MinimizeBox = false;
            this.Name = "New";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "New";
            this.Load += new System.EventHandler(this.New_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.New_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.newHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.newWidth)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown newHeight;
        private System.Windows.Forms.NumericUpDown newWidth;
        private System.Windows.Forms.Button newButton;
    }
}