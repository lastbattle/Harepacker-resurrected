namespace HaCreator.GUI
{
    partial class Repack
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
            this.button_repack = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label_repackState = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.checkedListBox_changedFiles = new System.Windows.Forms.CheckedListBox();
            this.SuspendLayout();
            // 
            // button_repack
            // 
            this.button_repack.Location = new System.Drawing.Point(2, 527);
            this.button_repack.Name = "button_repack";
            this.button_repack.Size = new System.Drawing.Size(345, 51);
            this.button_repack.TabIndex = 1;
            this.button_repack.Text = "Repack";
            this.button_repack.UseVisualStyleBackColor = true;
            this.button_repack.Click += new System.EventHandler(this.button_repack_Click);
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(-1, 440);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(348, 44);
            this.label1.TabIndex = 2;
            this.label1.Text = "The original files will be saved in the same directory where the WZ files are sto" +
    "red. If anything goes wrong, you will still have a backup.";
            // 
            // label2
            // 
            this.label2.ForeColor = System.Drawing.Color.Red;
            this.label2.Location = new System.Drawing.Point(-1, 408);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(326, 32);
            this.label2.TabIndex = 3;
            this.label2.Text = "Repacking will cause HaCreator to restart.\r\nMake sure ALL your maps are saved bef" +
    "ore proceeding.";
            // 
            // label_repackState
            // 
            this.label_repackState.Location = new System.Drawing.Point(-1, 484);
            this.label_repackState.Name = "label_repackState";
            this.label_repackState.Size = new System.Drawing.Size(348, 40);
            this.label_repackState.TabIndex = 6;
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(-1, -1);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(348, 15);
            this.label3.TabIndex = 7;
            this.label3.Text = "Files to repack:";
            // 
            // checkedListBox_changedFiles
            // 
            this.checkedListBox_changedFiles.FormattingEnabled = true;
            this.checkedListBox_changedFiles.Location = new System.Drawing.Point(2, 12);
            this.checkedListBox_changedFiles.Name = "checkedListBox_changedFiles";
            this.checkedListBox_changedFiles.Size = new System.Drawing.Size(345, 395);
            this.checkedListBox_changedFiles.TabIndex = 8;
            // 
            // Repack
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(350, 581);
            this.Controls.Add(this.checkedListBox_changedFiles);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label_repackState);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.button_repack);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.Name = "Repack";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Repack";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Repack_FormClosing);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Repack_KeyDown);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button button_repack;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label_repackState;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckedListBox checkedListBox_changedFiles;
    }
}