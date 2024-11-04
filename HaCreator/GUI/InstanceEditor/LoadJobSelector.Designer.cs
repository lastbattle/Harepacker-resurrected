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
            splitContainer1 = new System.Windows.Forms.SplitContainer();
            listBox_npcList = new System.Windows.Forms.ListBox();
            splitContainer2 = new System.Windows.Forms.SplitContainer();
            label_itemDesc = new System.Windows.Forms.Label();
            pictureBox_IconPreview = new System.Windows.Forms.PictureBox();
            button_select = new System.Windows.Forms.Button();
            searchBox = new CustomControls.WatermarkTextBox();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.Panel1.SuspendLayout();
            splitContainer2.Panel2.SuspendLayout();
            splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox_IconPreview).BeginInit();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer1.Location = new System.Drawing.Point(0, 23);
            splitContainer1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(listBox_npcList);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(splitContainer2);
            splitContainer1.Size = new System.Drawing.Size(467, 740);
            splitContainer1.SplitterDistance = 640;
            splitContainer1.SplitterWidth = 1;
            splitContainer1.TabIndex = 9;
            // 
            // listBox_npcList
            // 
            listBox_npcList.Dock = System.Windows.Forms.DockStyle.Fill;
            listBox_npcList.FormattingEnabled = true;
            listBox_npcList.ItemHeight = 15;
            listBox_npcList.Location = new System.Drawing.Point(0, 0);
            listBox_npcList.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            listBox_npcList.Name = "listBox_npcList";
            listBox_npcList.Size = new System.Drawing.Size(467, 640);
            listBox_npcList.TabIndex = 0;
            listBox_npcList.DrawItem += listBox_itemList_drawItem;
            listBox_npcList.MeasureItem += listBox_itemList_measureItem;
            listBox_npcList.SelectedIndexChanged += listBox_itemList_SelectedIndexChanged;
            // 
            // splitContainer2
            // 
            splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer2.Location = new System.Drawing.Point(0, 0);
            splitContainer2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(label_itemDesc);
            splitContainer2.Panel1.Controls.Add(pictureBox_IconPreview);
            // 
            // splitContainer2.Panel2
            // 
            splitContainer2.Panel2.Controls.Add(button_select);
            splitContainer2.Size = new System.Drawing.Size(467, 99);
            splitContainer2.SplitterDistance = 326;
            splitContainer2.SplitterWidth = 1;
            splitContainer2.TabIndex = 0;
            // 
            // label_itemDesc
            // 
            label_itemDesc.Dock = System.Windows.Forms.DockStyle.Fill;
            label_itemDesc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            label_itemDesc.Location = new System.Drawing.Point(112, 0);
            label_itemDesc.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label_itemDesc.Name = "label_itemDesc";
            label_itemDesc.Size = new System.Drawing.Size(214, 99);
            label_itemDesc.TabIndex = 1;
            label_itemDesc.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pictureBox_IconPreview
            // 
            pictureBox_IconPreview.Dock = System.Windows.Forms.DockStyle.Left;
            pictureBox_IconPreview.Location = new System.Drawing.Point(0, 0);
            pictureBox_IconPreview.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            pictureBox_IconPreview.Name = "pictureBox_IconPreview";
            pictureBox_IconPreview.Size = new System.Drawing.Size(112, 99);
            pictureBox_IconPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            pictureBox_IconPreview.TabIndex = 0;
            pictureBox_IconPreview.TabStop = false;
            // 
            // button_select
            // 
            button_select.Enabled = false;
            button_select.Location = new System.Drawing.Point(4, 0);
            button_select.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            button_select.Name = "button_select";
            button_select.Size = new System.Drawing.Size(132, 95);
            button_select.TabIndex = 0;
            button_select.Text = "Select";
            button_select.UseVisualStyleBackColor = true;
            button_select.Click += button_select_Click;
            // 
            // searchBox
            // 
            searchBox.Dock = System.Windows.Forms.DockStyle.Top;
            searchBox.ForeColor = System.Drawing.Color.Gray;
            searchBox.Location = new System.Drawing.Point(0, 0);
            searchBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            searchBox.Name = "searchBox";
            searchBox.Size = new System.Drawing.Size(467, 23);
            searchBox.TabIndex = 8;
            searchBox.Text = "Type here to search";
            searchBox.WatermarkActive = true;
            searchBox.WatermarkText = "Type here";
            // 
            // LoadJobSelector
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(467, 763);
            Controls.Add(splitContainer1);
            Controls.Add(searchBox);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "LoadJobSelector";
            Text = "Select a job";
            KeyDown += Load_KeyDown;
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            splitContainer2.Panel1.ResumeLayout(false);
            splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox_IconPreview).EndInit();
            ResumeLayout(false);
            PerformLayout();
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