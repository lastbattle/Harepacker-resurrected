namespace HaCreator.CustomControls
{
    partial class MapBrowser
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.mapNamesBox = new System.Windows.Forms.ListBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.minimapBox = new System.Windows.Forms.PictureBox();
            this.panel_linkWarning = new System.Windows.Forms.Panel();
            this.label_linkMapId = new System.Windows.Forms.Label();
            this.panel_mapExistWarning = new System.Windows.Forms.Panel();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.minimapBox)).BeginInit();
            this.panel_linkWarning.SuspendLayout();
            this.panel_mapExistWarning.SuspendLayout();
            this.SuspendLayout();
            // 
            // mapNamesBox
            // 
            this.mapNamesBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mapNamesBox.FormattingEnabled = true;
            this.mapNamesBox.Location = new System.Drawing.Point(0, 0);
            this.mapNamesBox.Name = "mapNamesBox";
            this.mapNamesBox.Size = new System.Drawing.Size(616, 501);
            this.mapNamesBox.TabIndex = 19;
            this.mapNamesBox.SelectedIndexChanged += new System.EventHandler(this.mapNamesBox_SelectedIndexChanged);
            // 
            // panel1
            // 
            this.panel1.AutoScroll = true;
            this.panel1.Controls.Add(this.panel_mapExistWarning);
            this.panel1.Controls.Add(this.minimapBox);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel1.Location = new System.Drawing.Point(616, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(349, 501);
            this.panel1.TabIndex = 18;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.Color.Red;
            this.label1.Location = new System.Drawing.Point(14, 10);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(119, 13);
            this.label1.TabIndex = 20;
            this.label1.Text = "This map is linked to: ";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.Color.Red;
            this.label2.Location = new System.Drawing.Point(14, 12);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(147, 13);
            this.label2.TabIndex = 19;
            this.label2.Text = "Map does not actually exist";
            // 
            // minimapBox
            // 
            this.minimapBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.minimapBox.Location = new System.Drawing.Point(0, 0);
            this.minimapBox.Name = "minimapBox";
            this.minimapBox.Size = new System.Drawing.Size(349, 501);
            this.minimapBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.minimapBox.TabIndex = 6;
            this.minimapBox.TabStop = false;
            // 
            // panel_linkWarning
            // 
            this.panel_linkWarning.BackColor = System.Drawing.Color.Transparent;
            this.panel_linkWarning.Controls.Add(this.label_linkMapId);
            this.panel_linkWarning.Controls.Add(this.label1);
            this.panel_linkWarning.Location = new System.Drawing.Point(0, 0);
            this.panel_linkWarning.Name = "panel_linkWarning";
            this.panel_linkWarning.Size = new System.Drawing.Size(275, 49);
            this.panel_linkWarning.TabIndex = 21;
            this.panel_linkWarning.Visible = false;
            // 
            // label_linkMapId
            // 
            this.label_linkMapId.AutoSize = true;
            this.label_linkMapId.ForeColor = System.Drawing.Color.Black;
            this.label_linkMapId.Location = new System.Drawing.Point(139, 10);
            this.label_linkMapId.Name = "label_linkMapId";
            this.label_linkMapId.Size = new System.Drawing.Size(13, 13);
            this.label_linkMapId.TabIndex = 21;
            this.label_linkMapId.Text = "0";
            // 
            // panel_mapExistWarning
            // 
            this.panel_mapExistWarning.BackColor = System.Drawing.Color.Transparent;
            this.panel_mapExistWarning.Controls.Add(this.panel_linkWarning);
            this.panel_mapExistWarning.Controls.Add(this.label2);
            this.panel_mapExistWarning.Location = new System.Drawing.Point(46, 206);
            this.panel_mapExistWarning.Name = "panel_mapExistWarning";
            this.panel_mapExistWarning.Size = new System.Drawing.Size(275, 49);
            this.panel_mapExistWarning.TabIndex = 22;
            this.panel_mapExistWarning.Visible = false;
            // 
            // MapBrowser
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            this.Controls.Add(this.mapNamesBox);
            this.Controls.Add(this.panel1);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.Name = "MapBrowser";
            this.Size = new System.Drawing.Size(965, 501);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.minimapBox)).EndInit();
            this.panel_linkWarning.ResumeLayout(false);
            this.panel_linkWarning.PerformLayout();
            this.panel_mapExistWarning.ResumeLayout(false);
            this.panel_mapExistWarning.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox mapNamesBox;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.PictureBox minimapBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel_linkWarning;
        private System.Windows.Forms.Label label_linkMapId;
        private System.Windows.Forms.Panel panel_mapExistWarning;
    }
}
