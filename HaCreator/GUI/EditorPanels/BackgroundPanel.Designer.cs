using HaCreator.CustomControls;

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
            splitContainer_all = new System.Windows.Forms.SplitContainer();
            splitContainer7 = new System.Windows.Forms.SplitContainer();
            bgSetListBox = new System.Windows.Forms.ListBox();
            groupBox1 = new System.Windows.Forms.GroupBox();
            aniBg = new System.Windows.Forms.RadioButton();
            radioButton_spine = new System.Windows.Forms.RadioButton();
            bgBack = new System.Windows.Forms.RadioButton();
            bgImageContainer = new ThumbnailFlowLayoutPanel();
            button_addImage = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)splitContainer_all).BeginInit();
            splitContainer_all.Panel1.SuspendLayout();
            splitContainer_all.Panel2.SuspendLayout();
            splitContainer_all.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer7).BeginInit();
            splitContainer7.Panel1.SuspendLayout();
            splitContainer7.Panel2.SuspendLayout();
            splitContainer7.SuspendLayout();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer_all
            // 
            splitContainer_all.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer_all.Location = new System.Drawing.Point(0, 0);
            splitContainer_all.Name = "splitContainer_all";
            splitContainer_all.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer_all.Panel1
            // 
            splitContainer_all.Panel1.Controls.Add(splitContainer7);
            // 
            // splitContainer_all.Panel2
            // 
            splitContainer_all.Panel2.Controls.Add(bgImageContainer);
            splitContainer_all.Panel2.Controls.Add(button_addImage);
            splitContainer_all.Size = new System.Drawing.Size(284, 658);
            splitContainer_all.SplitterDistance = 170;
            splitContainer_all.TabIndex = 2;
            // 
            // splitContainer7
            // 
            splitContainer7.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer7.Location = new System.Drawing.Point(0, 0);
            splitContainer7.Name = "splitContainer7";
            splitContainer7.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer7.Panel1
            // 
            splitContainer7.Panel1.Controls.Add(bgSetListBox);
            // 
            // splitContainer7.Panel2
            // 
            splitContainer7.Panel2.Controls.Add(groupBox1);
            splitContainer7.Panel2MinSize = 20;
            splitContainer7.Size = new System.Drawing.Size(284, 170);
            splitContainer7.SplitterDistance = 121;
            splitContainer7.TabIndex = 1;
            // 
            // bgSetListBox
            // 
            bgSetListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            bgSetListBox.FormattingEnabled = true;
            bgSetListBox.ItemHeight = 13;
            bgSetListBox.Location = new System.Drawing.Point(0, 0);
            bgSetListBox.Name = "bgSetListBox";
            bgSetListBox.Size = new System.Drawing.Size(284, 121);
            bgSetListBox.TabIndex = 0;
            bgSetListBox.SelectedIndexChanged += bgSetListBox_SelectedIndexChanged;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(aniBg);
            groupBox1.Controls.Add(radioButton_spine);
            groupBox1.Controls.Add(bgBack);
            groupBox1.Location = new System.Drawing.Point(3, 3);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(278, 39);
            groupBox1.TabIndex = 3;
            groupBox1.TabStop = false;
            groupBox1.Text = "Select";
            // 
            // aniBg
            // 
            aniBg.Location = new System.Drawing.Point(6, 15);
            aniBg.Name = "aniBg";
            aniBg.Size = new System.Drawing.Size(67, 18);
            aniBg.TabIndex = 0;
            aniBg.Text = "Animated";
            aniBg.CheckedChanged += bgSetListBox_SelectedIndexChanged;
            // 
            // radioButton_spine
            // 
            radioButton_spine.Location = new System.Drawing.Point(200, 15);
            radioButton_spine.Name = "radioButton_spine";
            radioButton_spine.Size = new System.Drawing.Size(68, 18);
            radioButton_spine.TabIndex = 2;
            radioButton_spine.Text = "Spine";
            // 
            // bgBack
            // 
            bgBack.Checked = true;
            bgBack.Location = new System.Drawing.Point(103, 15);
            bgBack.Name = "bgBack";
            bgBack.Size = new System.Drawing.Size(68, 18);
            bgBack.TabIndex = 1;
            bgBack.TabStop = true;
            bgBack.Text = "Static";
            bgBack.CheckedChanged += bgSetListBox_SelectedIndexChanged;
            // 
            // bgImageContainer
            // 
            bgImageContainer.AutoScroll = true;
            bgImageContainer.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            bgImageContainer.BackColor = System.Drawing.Color.White;
            bgImageContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            bgImageContainer.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            bgImageContainer.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            bgImageContainer.Location = new System.Drawing.Point(0, 0);
            bgImageContainer.Name = "bgImageContainer";
            bgImageContainer.Size = new System.Drawing.Size(284, 461);
            bgImageContainer.TabIndex = 0;
            bgImageContainer.WrapContents = false;
            // 
            // button_addImage
            // 
            button_addImage.Dock = System.Windows.Forms.DockStyle.Bottom;
            button_addImage.Location = new System.Drawing.Point(0, 461);
            button_addImage.Name = "button_addImage";
            button_addImage.Size = new System.Drawing.Size(284, 23);
            button_addImage.TabIndex = 1;
            button_addImage.Text = "Add image";
            button_addImage.UseVisualStyleBackColor = true;
            button_addImage.Click += button_addImage_Click;
            // 
            // BackgroundPanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            Controls.Add(splitContainer_all);
            Font = new System.Drawing.Font("Segoe UI", 8.25F);
            Name = "BackgroundPanel";
            Size = new System.Drawing.Size(284, 658);
            splitContainer_all.Panel1.ResumeLayout(false);
            splitContainer_all.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer_all).EndInit();
            splitContainer_all.ResumeLayout(false);
            splitContainer7.Panel1.ResumeLayout(false);
            splitContainer7.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer7).EndInit();
            splitContainer7.ResumeLayout(false);
            groupBox1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private global::System.Resources.ResourceManager resourceMan;
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

        private System.Windows.Forms.SplitContainer splitContainer_all;
        private System.Windows.Forms.SplitContainer splitContainer7;
        private System.Windows.Forms.ListBox bgSetListBox;
        private System.Windows.Forms.RadioButton bgBack;
        private System.Windows.Forms.RadioButton aniBg;
        private ThumbnailFlowLayoutPanel bgImageContainer;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton radioButton_spine;
        private System.Windows.Forms.Button button_addImage;
    }
}