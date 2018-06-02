namespace HaCreator.GUI.EditorPanels
{
    partial class BackgroundPanel
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
            this.splitContainer6 = new System.Windows.Forms.SplitContainer();
            this.splitContainer7 = new System.Windows.Forms.SplitContainer();
            this.bgSetListBox = new System.Windows.Forms.ListBox();
            this.bgBack = new System.Windows.Forms.RadioButton();
            this.aniBg = new System.Windows.Forms.RadioButton();
            this.bgImageContainer = new HaCreator.ThirdParty.ThumbnailFlowLayoutPanel();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer6)).BeginInit();
            this.splitContainer6.Panel1.SuspendLayout();
            this.splitContainer6.Panel2.SuspendLayout();
            this.splitContainer6.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer7)).BeginInit();
            this.splitContainer7.Panel1.SuspendLayout();
            this.splitContainer7.Panel2.SuspendLayout();
            this.splitContainer7.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer6
            // 
            this.splitContainer6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer6.Location = new System.Drawing.Point(0, 0);
            this.splitContainer6.Name = "splitContainer6";
            this.splitContainer6.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer6.Panel1
            // 
            this.splitContainer6.Panel1.Controls.Add(this.splitContainer7);
            // 
            // splitContainer6.Panel2
            // 
            this.splitContainer6.Panel2.Controls.Add(this.bgImageContainer);
            this.splitContainer6.Size = new System.Drawing.Size(284, 435);
            this.splitContainer6.SplitterDistance = 113;
            this.splitContainer6.TabIndex = 2;
            // 
            // splitContainer7
            // 
            this.splitContainer7.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer7.Location = new System.Drawing.Point(0, 0);
            this.splitContainer7.Name = "splitContainer7";
            this.splitContainer7.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer7.Panel1
            // 
            this.splitContainer7.Panel1.Controls.Add(this.bgSetListBox);
            // 
            // splitContainer7.Panel2
            // 
            this.splitContainer7.Panel2.Controls.Add(this.bgBack);
            this.splitContainer7.Panel2.Controls.Add(this.aniBg);
            this.splitContainer7.Panel2MinSize = 20;
            this.splitContainer7.Size = new System.Drawing.Size(284, 113);
            this.splitContainer7.SplitterDistance = 81;
            this.splitContainer7.TabIndex = 1;
            // 
            // bgSetListBox
            // 
            this.bgSetListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bgSetListBox.FormattingEnabled = true;
            this.bgSetListBox.Location = new System.Drawing.Point(0, 0);
            this.bgSetListBox.Name = "bgSetListBox";
            this.bgSetListBox.Size = new System.Drawing.Size(284, 81);
            this.bgSetListBox.TabIndex = 0;
            this.bgSetListBox.SelectedIndexChanged += new System.EventHandler(this.bgSetListBox_SelectedIndexChanged);
            // 
            // bgBack
            // 
            this.bgBack.Checked = true;
            this.bgBack.Location = new System.Drawing.Point(120, 3);
            this.bgBack.Name = "bgBack";
            this.bgBack.Size = new System.Drawing.Size(68, 18);
            this.bgBack.TabIndex = 1;
            this.bgBack.TabStop = true;
            this.bgBack.Text = "Static";
            this.bgBack.CheckedChanged += new System.EventHandler(this.bgSetListBox_SelectedIndexChanged);
            // 
            // aniBg
            // 
            this.aniBg.Location = new System.Drawing.Point(3, 3);
            this.aniBg.Name = "aniBg";
            this.aniBg.Size = new System.Drawing.Size(67, 18);
            this.aniBg.TabIndex = 0;
            this.aniBg.Text = "Animated";
            this.aniBg.CheckedChanged += new System.EventHandler(this.bgSetListBox_SelectedIndexChanged);
            // 
            // bgImageContainer
            // 
            this.bgImageContainer.AutoScroll = true;
            this.bgImageContainer.BackColor = System.Drawing.Color.White;
            this.bgImageContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bgImageContainer.Location = new System.Drawing.Point(0, 0);
            this.bgImageContainer.Name = "bgImageContainer";
            this.bgImageContainer.Size = new System.Drawing.Size(284, 318);
            this.bgImageContainer.TabIndex = 0;
            // 
            // BackgroundPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 435);
            this.CloseButton = false;
            this.CloseButtonVisible = false;
            this.Controls.Add(this.splitContainer6);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(177)));
            this.Name = "BackgroundPanel";
            this.ShowIcon = false;
            this.Text = "Background";
            this.splitContainer6.Panel1.ResumeLayout(false);
            this.splitContainer6.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer6)).EndInit();
            this.splitContainer6.ResumeLayout(false);
            this.splitContainer7.Panel1.ResumeLayout(false);
            this.splitContainer7.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer7)).EndInit();
            this.splitContainer7.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer6;
        private System.Windows.Forms.SplitContainer splitContainer7;
        private System.Windows.Forms.ListBox bgSetListBox;
        private System.Windows.Forms.RadioButton bgBack;
        private System.Windows.Forms.RadioButton aniBg;
        private ThirdParty.ThumbnailFlowLayoutPanel bgImageContainer;
    }
}