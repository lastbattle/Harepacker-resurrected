namespace HaCreator.GUI.EditorPanels
{
    partial class LifePanel
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
            splitContainer8 = new System.Windows.Forms.SplitContainer();
            splitContainer9 = new System.Windows.Forms.SplitContainer();
            label1 = new System.Windows.Forms.Label();
            lifeSearchBox = new System.Windows.Forms.TextBox();
            mobRButton = new System.Windows.Forms.RadioButton();
            npcRButton = new System.Windows.Forms.RadioButton();
            reactorRButton = new System.Windows.Forms.RadioButton();
            lifeListBox = new System.Windows.Forms.ListBox();
            lifePictureBox = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)splitContainer8).BeginInit();
            splitContainer8.Panel1.SuspendLayout();
            splitContainer8.Panel2.SuspendLayout();
            splitContainer8.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer9).BeginInit();
            splitContainer9.Panel1.SuspendLayout();
            splitContainer9.Panel2.SuspendLayout();
            splitContainer9.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)lifePictureBox).BeginInit();
            SuspendLayout();
            // 
            // splitContainer8
            // 
            splitContainer8.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer8.Location = new System.Drawing.Point(0, 0);
            splitContainer8.Name = "splitContainer8";
            splitContainer8.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer8.Panel1
            // 
            splitContainer8.Panel1.Controls.Add(splitContainer9);
            // 
            // splitContainer8.Panel2
            // 
            splitContainer8.Panel2.AutoScroll = true;
            splitContainer8.Panel2.Controls.Add(lifePictureBox);
            splitContainer8.Size = new System.Drawing.Size(269, 658);
            splitContainer8.SplitterDistance = 429;
            splitContainer8.TabIndex = 2;
            // 
            // splitContainer9
            // 
            splitContainer9.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer9.Location = new System.Drawing.Point(0, 0);
            splitContainer9.Name = "splitContainer9";
            splitContainer9.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer9.Panel1
            // 
            splitContainer9.Panel1.Controls.Add(label1);
            splitContainer9.Panel1.Controls.Add(lifeSearchBox);
            splitContainer9.Panel1.Controls.Add(mobRButton);
            splitContainer9.Panel1.Controls.Add(npcRButton);
            splitContainer9.Panel1.Controls.Add(reactorRButton);
            // 
            // splitContainer9.Panel2
            // 
            splitContainer9.Panel2.Controls.Add(lifeListBox);
            splitContainer9.Size = new System.Drawing.Size(269, 429);
            splitContainer9.SplitterDistance = 60;
            splitContainer9.TabIndex = 3;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(3, 30);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(41, 13);
            label1.TabIndex = 4;
            label1.Text = "Search";
            // 
            // lifeSearchBox
            // 
            lifeSearchBox.Location = new System.Drawing.Point(44, 28);
            lifeSearchBox.Name = "lifeSearchBox";
            lifeSearchBox.Size = new System.Drawing.Size(222, 22);
            lifeSearchBox.TabIndex = 3;
            lifeSearchBox.TextChanged += lifeModeChanged;
            // 
            // mobRButton
            // 
            mobRButton.Checked = true;
            mobRButton.Location = new System.Drawing.Point(3, 3);
            mobRButton.Name = "mobRButton";
            mobRButton.Size = new System.Drawing.Size(52, 19);
            mobRButton.TabIndex = 1;
            mobRButton.TabStop = true;
            mobRButton.Text = "Mob";
            mobRButton.CheckedChanged += lifeModeChanged;
            // 
            // npcRButton
            // 
            npcRButton.Location = new System.Drawing.Point(61, 3);
            npcRButton.Name = "npcRButton";
            npcRButton.Size = new System.Drawing.Size(56, 19);
            npcRButton.TabIndex = 2;
            npcRButton.Text = "NPC";
            npcRButton.CheckedChanged += lifeModeChanged;
            // 
            // reactorRButton
            // 
            reactorRButton.Location = new System.Drawing.Point(123, 3);
            reactorRButton.Name = "reactorRButton";
            reactorRButton.Size = new System.Drawing.Size(67, 19);
            reactorRButton.TabIndex = 0;
            reactorRButton.Text = "Reactor";
            reactorRButton.CheckedChanged += lifeModeChanged;
            // 
            // lifeListBox
            // 
            lifeListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            lifeListBox.FormattingEnabled = true;
            lifeListBox.ItemHeight = 13;
            lifeListBox.Location = new System.Drawing.Point(0, 0);
            lifeListBox.Name = "lifeListBox";
            lifeListBox.Size = new System.Drawing.Size(269, 365);
            lifeListBox.TabIndex = 0;
            lifeListBox.SelectedIndexChanged += lifeListBox_SelectedValueChanged;
            // 
            // lifePictureBox
            // 
            lifePictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            lifePictureBox.Location = new System.Drawing.Point(0, 0);
            lifePictureBox.Name = "lifePictureBox";
            lifePictureBox.Size = new System.Drawing.Size(269, 225);
            lifePictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            lifePictureBox.TabIndex = 0;
            lifePictureBox.TabStop = false;
            // 
            // LifePanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            Controls.Add(splitContainer8);
            Font = new System.Drawing.Font("Segoe UI", 8.25F);
            Name = "LifePanel";
            Size = new System.Drawing.Size(269, 658);
            splitContainer8.Panel1.ResumeLayout(false);
            splitContainer8.Panel2.ResumeLayout(false);
            splitContainer8.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer8).EndInit();
            splitContainer8.ResumeLayout(false);
            splitContainer9.Panel1.ResumeLayout(false);
            splitContainer9.Panel1.PerformLayout();
            splitContainer9.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer9).EndInit();
            splitContainer9.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)lifePictureBox).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer8;
        private System.Windows.Forms.SplitContainer splitContainer9;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox lifeSearchBox;
        private System.Windows.Forms.RadioButton mobRButton;
        private System.Windows.Forms.RadioButton npcRButton;
        private System.Windows.Forms.RadioButton reactorRButton;
        private System.Windows.Forms.ListBox lifeListBox;
        private System.Windows.Forms.PictureBox lifePictureBox;
    }
}