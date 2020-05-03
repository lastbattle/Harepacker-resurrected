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
            this.label2.Location = new System.Drawing.Point(13, 50);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(56, 20);
            this.label2.TabIndex = 17;
            this.label2.Text = "Height";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 18);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(54, 20);
            this.label1.TabIndex = 16;
            this.label1.Text = "Width ";
            // 
            // newHeight
            // 
            this.newHeight.Location = new System.Drawing.Point(100, 48);
            this.newHeight.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.newHeight.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.newHeight.Name = "newHeight";
            this.newHeight.Size = new System.Drawing.Size(191, 26);
            this.newHeight.TabIndex = 1;
            this.newHeight.Value = new decimal(new int[] {
            600,
            0,
            0,
            0});
            // 
            // newWidth
            // 
            this.newWidth.Location = new System.Drawing.Point(100, 14);
            this.newWidth.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.newWidth.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.newWidth.Name = "newWidth";
            this.newWidth.Size = new System.Drawing.Size(191, 26);
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
            this.newButton.Location = new System.Drawing.Point(13, 84);
            this.newButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.newButton.Name = "newButton";
            this.newButton.Size = new System.Drawing.Size(278, 48);
            this.newButton.TabIndex = 2;
            this.newButton.Text = "Create";
            this.newButton.Click += new System.EventHandler(this.newButton_Click);
            // 
            // New
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(304, 146);
            this.Controls.Add(this.newButton);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.newHeight);
            this.Controls.Add(this.newWidth);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
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