using HaCreator.CustomControls;

namespace HaCreator.GUI.EditorPanels
{
    partial class ObjPanel
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
            splitContainer_topPanel = new System.Windows.Forms.SplitContainer();
            objSetListBox = new System.Windows.Forms.ListBox();
            objL0ListBox = new System.Windows.Forms.ListBox();
            splitContainer_bottomPanel = new System.Windows.Forms.SplitContainer();
            objL1ListBox = new System.Windows.Forms.ListBox();
            objImagesContainer = new ThumbnailFlowLayoutPanel();
            button_addImage = new System.Windows.Forms.Button();
            splitContainer_whole = new System.Windows.Forms.SplitContainer();
            ((System.ComponentModel.ISupportInitialize)splitContainer_all).BeginInit();
            splitContainer_all.Panel1.SuspendLayout();
            splitContainer_all.Panel2.SuspendLayout();
            splitContainer_all.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer_topPanel).BeginInit();
            splitContainer_topPanel.Panel1.SuspendLayout();
            splitContainer_topPanel.Panel2.SuspendLayout();
            splitContainer_topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer_bottomPanel).BeginInit();
            splitContainer_bottomPanel.Panel1.SuspendLayout();
            splitContainer_bottomPanel.Panel2.SuspendLayout();
            splitContainer_bottomPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer_whole).BeginInit();
            splitContainer_whole.Panel1.SuspendLayout();
            splitContainer_whole.Panel2.SuspendLayout();
            splitContainer_whole.SuspendLayout();
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
            splitContainer_all.Panel1.Controls.Add(splitContainer_topPanel);
            // 
            // splitContainer_all.Panel2
            // 
            splitContainer_all.Panel2.Controls.Add(splitContainer_bottomPanel);
            splitContainer_all.Size = new System.Drawing.Size(284, 629);
            splitContainer_all.SplitterDistance = 180;
            splitContainer_all.TabIndex = 2;
            // 
            // splitContainer_topPanel
            // 
            splitContainer_topPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer_topPanel.Location = new System.Drawing.Point(0, 0);
            splitContainer_topPanel.Name = "splitContainer_topPanel";
            splitContainer_topPanel.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer_topPanel.Panel1
            // 
            splitContainer_topPanel.Panel1.Controls.Add(objSetListBox);
            // 
            // splitContainer_topPanel.Panel2
            // 
            splitContainer_topPanel.Panel2.Controls.Add(objL0ListBox);
            splitContainer_topPanel.Size = new System.Drawing.Size(284, 180);
            splitContainer_topPanel.SplitterDistance = 107;
            splitContainer_topPanel.TabIndex = 0;
            // 
            // objSetListBox
            // 
            objSetListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            objSetListBox.FormattingEnabled = true;
            objSetListBox.ItemHeight = 13;
            objSetListBox.Location = new System.Drawing.Point(0, 0);
            objSetListBox.Name = "objSetListBox";
            objSetListBox.Size = new System.Drawing.Size(284, 107);
            objSetListBox.TabIndex = 0;
            objSetListBox.SelectedIndexChanged += objSetListBox_SelectedIndexChanged;
            // 
            // objL0ListBox
            // 
            objL0ListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            objL0ListBox.FormattingEnabled = true;
            objL0ListBox.ItemHeight = 13;
            objL0ListBox.Location = new System.Drawing.Point(0, 0);
            objL0ListBox.Name = "objL0ListBox";
            objL0ListBox.Size = new System.Drawing.Size(284, 69);
            objL0ListBox.TabIndex = 0;
            objL0ListBox.SelectedIndexChanged += objL0ListBox_SelectedIndexChanged;
            // 
            // splitContainer_bottomPanel
            // 
            splitContainer_bottomPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer_bottomPanel.Location = new System.Drawing.Point(0, 0);
            splitContainer_bottomPanel.Name = "splitContainer_bottomPanel";
            splitContainer_bottomPanel.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer_bottomPanel.Panel1
            // 
            splitContainer_bottomPanel.Panel1.Controls.Add(objL1ListBox);
            // 
            // splitContainer_bottomPanel.Panel2
            // 
            splitContainer_bottomPanel.Panel2.Controls.Add(objImagesContainer);
            splitContainer_bottomPanel.Size = new System.Drawing.Size(284, 445);
            splitContainer_bottomPanel.SplitterDistance = 64;
            splitContainer_bottomPanel.TabIndex = 0;
            // 
            // objL1ListBox
            // 
            objL1ListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            objL1ListBox.FormattingEnabled = true;
            objL1ListBox.ItemHeight = 13;
            objL1ListBox.Location = new System.Drawing.Point(0, 0);
            objL1ListBox.Name = "objL1ListBox";
            objL1ListBox.Size = new System.Drawing.Size(284, 64);
            objL1ListBox.TabIndex = 0;
            objL1ListBox.SelectedIndexChanged += objL1ListBox_SelectedIndexChanged;
            // 
            // objImagesContainer
            // 
            objImagesContainer.AutoScroll = true;
            objImagesContainer.BackColor = System.Drawing.Color.White;
            objImagesContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            objImagesContainer.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            objImagesContainer.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            objImagesContainer.Location = new System.Drawing.Point(0, 0);
            objImagesContainer.Name = "objImagesContainer";
            objImagesContainer.Size = new System.Drawing.Size(284, 377);
            objImagesContainer.TabIndex = 0;
            objImagesContainer.WrapContents = false;
            // 
            // button_addImage
            // 
            button_addImage.Dock = System.Windows.Forms.DockStyle.Fill;
            button_addImage.Enabled = false;
            button_addImage.Location = new System.Drawing.Point(0, 0);
            button_addImage.Name = "button_addImage";
            button_addImage.Size = new System.Drawing.Size(284, 25);
            button_addImage.TabIndex = 0;
            button_addImage.Text = "Add Image";
            button_addImage.UseVisualStyleBackColor = true;
            button_addImage.Click += button_addImage_Click;
            // 
            // splitContainer_whole
            // 
            splitContainer_whole.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer_whole.Location = new System.Drawing.Point(0, 0);
            splitContainer_whole.Name = "splitContainer_whole";
            splitContainer_whole.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer_whole.Panel1
            // 
            splitContainer_whole.Panel1.Controls.Add(splitContainer_all);
            // 
            // splitContainer_whole.Panel2
            // 
            splitContainer_whole.Panel2.Controls.Add(button_addImage);
            splitContainer_whole.Size = new System.Drawing.Size(284, 658);
            splitContainer_whole.SplitterDistance = 629;
            splitContainer_whole.TabIndex = 1;
            // 
            // ObjPanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            Controls.Add(splitContainer_whole);
            Font = new System.Drawing.Font("Segoe UI", 8.25F);
            Name = "ObjPanel";
            Size = new System.Drawing.Size(284, 658);
            splitContainer_all.Panel1.ResumeLayout(false);
            splitContainer_all.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer_all).EndInit();
            splitContainer_all.ResumeLayout(false);
            splitContainer_topPanel.Panel1.ResumeLayout(false);
            splitContainer_topPanel.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer_topPanel).EndInit();
            splitContainer_topPanel.ResumeLayout(false);
            splitContainer_bottomPanel.Panel1.ResumeLayout(false);
            splitContainer_bottomPanel.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer_bottomPanel).EndInit();
            splitContainer_bottomPanel.ResumeLayout(false);
            splitContainer_whole.Panel1.ResumeLayout(false);
            splitContainer_whole.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer_whole).EndInit();
            splitContainer_whole.ResumeLayout(false);
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
        private System.Windows.Forms.SplitContainer splitContainer_topPanel;
        private System.Windows.Forms.ListBox objSetListBox;
        private System.Windows.Forms.ListBox objL0ListBox;
        private System.Windows.Forms.SplitContainer splitContainer_bottomPanel;
        private System.Windows.Forms.ListBox objL1ListBox;
        private ThumbnailFlowLayoutPanel objImagesContainer;
        private System.Windows.Forms.Button button_addImage;
        private System.Windows.Forms.SplitContainer splitContainer_whole;
    }
}