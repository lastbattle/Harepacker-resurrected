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
            this.splitContainer8 = new System.Windows.Forms.SplitContainer();
            this.splitContainer9 = new System.Windows.Forms.SplitContainer();
            this.label1 = new System.Windows.Forms.Label();
            this.lifeSearchBox = new System.Windows.Forms.TextBox();
            this.mobRButton = new System.Windows.Forms.RadioButton();
            this.npcRButton = new System.Windows.Forms.RadioButton();
            this.reactorRButton = new System.Windows.Forms.RadioButton();
            this.lifeListBox = new System.Windows.Forms.ListBox();
            this.lifePictureBox = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer8)).BeginInit();
            this.splitContainer8.Panel1.SuspendLayout();
            this.splitContainer8.Panel2.SuspendLayout();
            this.splitContainer8.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer9)).BeginInit();
            this.splitContainer9.Panel1.SuspendLayout();
            this.splitContainer9.Panel2.SuspendLayout();
            this.splitContainer9.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.lifePictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer8
            // 
            this.splitContainer8.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer8.Location = new System.Drawing.Point(0, 0);
            this.splitContainer8.Name = "splitContainer8";
            this.splitContainer8.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer8.Panel1
            // 
            this.splitContainer8.Panel1.Controls.Add(this.splitContainer9);
            // 
            // splitContainer8.Panel2
            // 
            this.splitContainer8.Panel2.AutoScroll = true;
            this.splitContainer8.Panel2.Controls.Add(this.lifePictureBox);
            this.splitContainer8.Size = new System.Drawing.Size(269, 537);
            this.splitContainer8.SplitterDistance = 182;
            this.splitContainer8.TabIndex = 2;
            // 
            // splitContainer9
            // 
            this.splitContainer9.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer9.Location = new System.Drawing.Point(0, 0);
            this.splitContainer9.Name = "splitContainer9";
            this.splitContainer9.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer9.Panel1
            // 
            this.splitContainer9.Panel1.Controls.Add(this.label1);
            this.splitContainer9.Panel1.Controls.Add(this.lifeSearchBox);
            this.splitContainer9.Panel1.Controls.Add(this.mobRButton);
            this.splitContainer9.Panel1.Controls.Add(this.npcRButton);
            this.splitContainer9.Panel1.Controls.Add(this.reactorRButton);
            // 
            // splitContainer9.Panel2
            // 
            this.splitContainer9.Panel2.Controls.Add(this.lifeListBox);
            this.splitContainer9.Size = new System.Drawing.Size(269, 182);
            this.splitContainer9.SplitterDistance = 65;
            this.splitContainer9.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(41, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "Search";
            // 
            // lifeSearchBox
            // 
            this.lifeSearchBox.Location = new System.Drawing.Point(44, 28);
            this.lifeSearchBox.Name = "lifeSearchBox";
            this.lifeSearchBox.Size = new System.Drawing.Size(146, 20);
            this.lifeSearchBox.TabIndex = 3;
            this.lifeSearchBox.TextChanged += new System.EventHandler(this.lifeModeChanged);
            // 
            // mobRButton
            // 
            this.mobRButton.Checked = true;
            this.mobRButton.Location = new System.Drawing.Point(3, 3);
            this.mobRButton.Name = "mobRButton";
            this.mobRButton.Size = new System.Drawing.Size(52, 19);
            this.mobRButton.TabIndex = 1;
            this.mobRButton.TabStop = true;
            this.mobRButton.Text = "Mob";
            this.mobRButton.CheckedChanged += new System.EventHandler(this.lifeModeChanged);
            // 
            // npcRButton
            // 
            this.npcRButton.Location = new System.Drawing.Point(61, 3);
            this.npcRButton.Name = "npcRButton";
            this.npcRButton.Size = new System.Drawing.Size(56, 19);
            this.npcRButton.TabIndex = 2;
            this.npcRButton.Text = "NPC";
            this.npcRButton.CheckedChanged += new System.EventHandler(this.lifeModeChanged);
            // 
            // reactorRButton
            // 
            this.reactorRButton.Location = new System.Drawing.Point(123, 3);
            this.reactorRButton.Name = "reactorRButton";
            this.reactorRButton.Size = new System.Drawing.Size(67, 19);
            this.reactorRButton.TabIndex = 0;
            this.reactorRButton.Text = "Reactor";
            this.reactorRButton.CheckedChanged += new System.EventHandler(this.lifeModeChanged);
            // 
            // lifeListBox
            // 
            this.lifeListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lifeListBox.FormattingEnabled = true;
            this.lifeListBox.Location = new System.Drawing.Point(0, 0);
            this.lifeListBox.Name = "lifeListBox";
            this.lifeListBox.Size = new System.Drawing.Size(269, 113);
            this.lifeListBox.TabIndex = 0;
            this.lifeListBox.SelectedIndexChanged += new System.EventHandler(this.lifeListBox_SelectedValueChanged);
            // 
            // lifePictureBox
            // 
            this.lifePictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lifePictureBox.Location = new System.Drawing.Point(0, 0);
            this.lifePictureBox.Name = "lifePictureBox";
            this.lifePictureBox.Size = new System.Drawing.Size(269, 351);
            this.lifePictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.lifePictureBox.TabIndex = 0;
            this.lifePictureBox.TabStop = false;
            // 
            // LifePanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(269, 537);
            this.CloseButton = false;
            this.CloseButtonVisible = false;
            this.Controls.Add(this.splitContainer8);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(177)));
            this.Name = "LifePanel";
            this.ShowIcon = false;
            this.Text = "Life";
            this.splitContainer8.Panel1.ResumeLayout(false);
            this.splitContainer8.Panel2.ResumeLayout(false);
            this.splitContainer8.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer8)).EndInit();
            this.splitContainer8.ResumeLayout(false);
            this.splitContainer9.Panel1.ResumeLayout(false);
            this.splitContainer9.Panel1.PerformLayout();
            this.splitContainer9.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer9)).EndInit();
            this.splitContainer9.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.lifePictureBox)).EndInit();
            this.ResumeLayout(false);

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