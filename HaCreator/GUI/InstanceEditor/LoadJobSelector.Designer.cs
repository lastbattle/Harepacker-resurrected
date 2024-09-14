namespace HaCreator.GUI.InstanceEditor
{
    partial class LoadJobSelector
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.listBox_npcList = new System.Windows.Forms.ListBox();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.label_itemDesc = new System.Windows.Forms.Label();
            this.pictureBox_IconPreview = new System.Windows.Forms.PictureBox();
            this.button_select = new System.Windows.Forms.Button();
            this.searchBox = new HaCreator.CustomControls.WatermarkTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox_IconPreview)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 20);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.listBox_npcList);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(400, 641);
            this.splitContainer1.SplitterDistance = 555;
            this.splitContainer1.SplitterWidth = 1;
            this.splitContainer1.TabIndex = 9;
            // 
            // listBox_npcList
            // 
            this.listBox_npcList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox_npcList.FormattingEnabled = true;
            this.listBox_npcList.Location = new System.Drawing.Point(0, 0);
            this.listBox_npcList.Name = "listBox_npcList";
            this.listBox_npcList.Size = new System.Drawing.Size(400, 555);
            this.listBox_npcList.TabIndex = 0;
            this.listBox_npcList.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.listBox_itemList_drawItem);
            this.listBox_npcList.MeasureItem += new System.Windows.Forms.MeasureItemEventHandler(this.listBox_itemList_measureItem);
            this.listBox_npcList.SelectedIndexChanged += new System.EventHandler(this.listBox_itemList_SelectedIndexChanged);
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.label_itemDesc);
            this.splitContainer2.Panel1.Controls.Add(this.pictureBox_IconPreview);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.button_select);
            this.splitContainer2.Size = new System.Drawing.Size(400, 85);
            this.splitContainer2.SplitterDistance = 280;
            this.splitContainer2.SplitterWidth = 1;
            this.splitContainer2.TabIndex = 0;
            // 
            // label_itemDesc
            // 
            this.label_itemDesc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label_itemDesc.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_itemDesc.Location = new System.Drawing.Point(96, 0);
            this.label_itemDesc.Name = "label_itemDesc";
            this.label_itemDesc.Size = new System.Drawing.Size(184, 85);
            this.label_itemDesc.TabIndex = 1;
            this.label_itemDesc.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pictureBox_IconPreview
            // 
            this.pictureBox_IconPreview.Dock = System.Windows.Forms.DockStyle.Left;
            this.pictureBox_IconPreview.Location = new System.Drawing.Point(0, 0);
            this.pictureBox_IconPreview.Name = "pictureBox_IconPreview";
            this.pictureBox_IconPreview.Size = new System.Drawing.Size(96, 85);
            this.pictureBox_IconPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox_IconPreview.TabIndex = 0;
            this.pictureBox_IconPreview.TabStop = false;
            // 
            // button_select
            // 
            this.button_select.Enabled = false;
            this.button_select.Location = new System.Drawing.Point(3, 0);
            this.button_select.Name = "button_select";
            this.button_select.Size = new System.Drawing.Size(113, 82);
            this.button_select.TabIndex = 0;
            this.button_select.Text = "Select";
            this.button_select.UseVisualStyleBackColor = true;
            this.button_select.Click += new System.EventHandler(this.button_select_Click);
            // 
            // searchBox
            // 
            this.searchBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.searchBox.ForeColor = System.Drawing.Color.Gray;
            this.searchBox.Location = new System.Drawing.Point(0, 0);
            this.searchBox.Name = "searchBox";
            this.searchBox.Size = new System.Drawing.Size(400, 20);
            this.searchBox.TabIndex = 8;
            this.searchBox.Text = "Type here to search";
            this.searchBox.WatermarkActive = true;
            this.searchBox.WatermarkText = "Type here";
            this.searchBox.TextChanged += new System.EventHandler(this.searchBox_TextChanged);
            // 
            // LoadJobSelector
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 661);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.searchBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "LoadJobSelector";
            this.Text = "Select a job";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Load_KeyDown);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox_IconPreview)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private CustomControls.WatermarkTextBox searchBox;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox listBox_npcList;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.Button button_select;
        private System.Windows.Forms.PictureBox pictureBox_IconPreview;
        private System.Windows.Forms.Label label_itemDesc;
    }
}