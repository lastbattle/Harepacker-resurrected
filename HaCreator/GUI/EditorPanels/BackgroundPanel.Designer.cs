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
            this.radioButton_spine = new System.Windows.Forms.RadioButton();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer6)).BeginInit();
            this.splitContainer6.Panel1.SuspendLayout();
            this.splitContainer6.Panel2.SuspendLayout();
            this.splitContainer6.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer7)).BeginInit();
            this.splitContainer7.Panel1.SuspendLayout();
            this.splitContainer7.Panel2.SuspendLayout();
            this.splitContainer7.SuspendLayout();
            this.groupBox1.SuspendLayout();
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
            this.splitContainer6.Size = new System.Drawing.Size(284, 658);
            this.splitContainer6.SplitterDistance = 170;
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
            this.splitContainer7.Panel2.Controls.Add(this.groupBox1);
            this.splitContainer7.Panel2MinSize = 20;
            this.splitContainer7.Size = new System.Drawing.Size(284, 170);
            this.splitContainer7.SplitterDistance = 121;
            this.splitContainer7.TabIndex = 1;
            // 
            // bgSetListBox
            // 
            this.bgSetListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bgSetListBox.FormattingEnabled = true;
            this.bgSetListBox.Location = new System.Drawing.Point(0, 0);
            this.bgSetListBox.Name = "bgSetListBox";
            this.bgSetListBox.Size = new System.Drawing.Size(284, 121);
            this.bgSetListBox.TabIndex = 0;
            this.bgSetListBox.SelectedIndexChanged += new System.EventHandler(this.bgSetListBox_SelectedIndexChanged);
            // 
            // bgBack
            // 
            this.bgBack.Checked = true;
            this.bgBack.Location = new System.Drawing.Point(103, 15);
            this.bgBack.Name = "bgBack";
            this.bgBack.Size = new System.Drawing.Size(68, 18);
            this.bgBack.TabIndex = 1;
            this.bgBack.Text = "Static";
            this.bgBack.CheckedChanged += new System.EventHandler(this.bgSetListBox_SelectedIndexChanged);
            // 
            // aniBg
            // 
            this.aniBg.Location = new System.Drawing.Point(6, 15);
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
            this.bgImageContainer.Size = new System.Drawing.Size(284, 484);
            this.bgImageContainer.TabIndex = 0;
            // 
            // radioButton_spine
            // 
            this.radioButton_spine.Location = new System.Drawing.Point(200, 15);
            this.radioButton_spine.Name = "radioButton_spine";
            this.radioButton_spine.Size = new System.Drawing.Size(68, 18);
            this.radioButton_spine.TabIndex = 2;
            this.radioButton_spine.Text = "Spine";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.aniBg);
            this.groupBox1.Controls.Add(this.radioButton_spine);
            this.groupBox1.Controls.Add(this.bgBack);
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(278, 39);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Select";
            // 
            // BackgroundPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Controls.Add(this.splitContainer6);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.Name = "BackgroundPanel";
            this.Size = new System.Drawing.Size(284, 658);
            this.splitContainer6.Panel1.ResumeLayout(false);
            this.splitContainer6.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer6)).EndInit();
            this.splitContainer6.ResumeLayout(false);
            this.splitContainer7.Panel1.ResumeLayout(false);
            this.splitContainer7.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer7)).EndInit();
            this.splitContainer7.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer6;
        private System.Windows.Forms.SplitContainer splitContainer7;
        private System.Windows.Forms.ListBox bgSetListBox;
        private System.Windows.Forms.RadioButton bgBack;
        private System.Windows.Forms.RadioButton aniBg;
        private ThirdParty.ThumbnailFlowLayoutPanel bgImageContainer;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton radioButton_spine;
    }
}