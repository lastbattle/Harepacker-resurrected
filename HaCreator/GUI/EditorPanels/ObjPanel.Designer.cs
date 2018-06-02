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
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.splitContainer4 = new System.Windows.Forms.SplitContainer();
            this.objSetListBox = new System.Windows.Forms.ListBox();
            this.objL0ListBox = new System.Windows.Forms.ListBox();
            this.splitContainer5 = new System.Windows.Forms.SplitContainer();
            this.objL1ListBox = new System.Windows.Forms.ListBox();
            this.objImagesContainer = new HaCreator.ThirdParty.ThumbnailFlowLayoutPanel();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer4)).BeginInit();
            this.splitContainer4.Panel1.SuspendLayout();
            this.splitContainer4.Panel2.SuspendLayout();
            this.splitContainer4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer5)).BeginInit();
            this.splitContainer5.Panel1.SuspendLayout();
            this.splitContainer5.Panel2.SuspendLayout();
            this.splitContainer5.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Name = "splitContainer3";
            this.splitContainer3.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.splitContainer4);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.splitContainer5);
            this.splitContainer3.Size = new System.Drawing.Size(284, 515);
            this.splitContainer3.SplitterDistance = 148;
            this.splitContainer3.TabIndex = 2;
            // 
            // splitContainer4
            // 
            this.splitContainer4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer4.Location = new System.Drawing.Point(0, 0);
            this.splitContainer4.Name = "splitContainer4";
            this.splitContainer4.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer4.Panel1
            // 
            this.splitContainer4.Panel1.Controls.Add(this.objSetListBox);
            // 
            // splitContainer4.Panel2
            // 
            this.splitContainer4.Panel2.Controls.Add(this.objL0ListBox);
            this.splitContainer4.Size = new System.Drawing.Size(284, 148);
            this.splitContainer4.SplitterDistance = 89;
            this.splitContainer4.TabIndex = 0;
            // 
            // objSetListBox
            // 
            this.objSetListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.objSetListBox.FormattingEnabled = true;
            this.objSetListBox.Location = new System.Drawing.Point(0, 0);
            this.objSetListBox.Name = "objSetListBox";
            this.objSetListBox.Size = new System.Drawing.Size(284, 89);
            this.objSetListBox.TabIndex = 0;
            this.objSetListBox.SelectedIndexChanged += new System.EventHandler(this.objSetListBox_SelectedIndexChanged);
            // 
            // objL0ListBox
            // 
            this.objL0ListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.objL0ListBox.FormattingEnabled = true;
            this.objL0ListBox.Location = new System.Drawing.Point(0, 0);
            this.objL0ListBox.Name = "objL0ListBox";
            this.objL0ListBox.Size = new System.Drawing.Size(284, 55);
            this.objL0ListBox.TabIndex = 0;
            this.objL0ListBox.SelectedIndexChanged += new System.EventHandler(this.objL0ListBox_SelectedIndexChanged);
            // 
            // splitContainer5
            // 
            this.splitContainer5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer5.Location = new System.Drawing.Point(0, 0);
            this.splitContainer5.Name = "splitContainer5";
            this.splitContainer5.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer5.Panel1
            // 
            this.splitContainer5.Panel1.Controls.Add(this.objL1ListBox);
            // 
            // splitContainer5.Panel2
            // 
            this.splitContainer5.Panel2.Controls.Add(this.objImagesContainer);
            this.splitContainer5.Size = new System.Drawing.Size(284, 363);
            this.splitContainer5.SplitterDistance = 53;
            this.splitContainer5.TabIndex = 0;
            // 
            // objL1ListBox
            // 
            this.objL1ListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.objL1ListBox.FormattingEnabled = true;
            this.objL1ListBox.Location = new System.Drawing.Point(0, 0);
            this.objL1ListBox.Name = "objL1ListBox";
            this.objL1ListBox.Size = new System.Drawing.Size(284, 53);
            this.objL1ListBox.TabIndex = 0;
            this.objL1ListBox.SelectedIndexChanged += new System.EventHandler(this.objL1ListBox_SelectedIndexChanged);
            // 
            // objImagesContainer
            // 
            this.objImagesContainer.AutoScroll = true;
            this.objImagesContainer.BackColor = System.Drawing.Color.White;
            this.objImagesContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.objImagesContainer.Location = new System.Drawing.Point(0, 0);
            this.objImagesContainer.Name = "objImagesContainer";
            this.objImagesContainer.Size = new System.Drawing.Size(284, 306);
            this.objImagesContainer.TabIndex = 0;
            // 
            // ObjPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 515);
            this.CloseButton = false;
            this.CloseButtonVisible = false;
            this.Controls.Add(this.splitContainer3);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(177)));
            this.Name = "ObjPanel";
            this.ShowIcon = false;
            this.Text = "Objects";
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.splitContainer4.Panel1.ResumeLayout(false);
            this.splitContainer4.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer4)).EndInit();
            this.splitContainer4.ResumeLayout(false);
            this.splitContainer5.Panel1.ResumeLayout(false);
            this.splitContainer5.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer5)).EndInit();
            this.splitContainer5.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.SplitContainer splitContainer4;
        private System.Windows.Forms.ListBox objSetListBox;
        private System.Windows.Forms.ListBox objL0ListBox;
        private System.Windows.Forms.SplitContainer splitContainer5;
        private System.Windows.Forms.ListBox objL1ListBox;
        private ThirdParty.ThumbnailFlowLayoutPanel objImagesContainer;
    }
}