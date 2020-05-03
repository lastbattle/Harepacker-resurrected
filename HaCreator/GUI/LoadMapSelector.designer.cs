using HaCreator.CustomControls;

namespace HaCreator.GUI
{
    partial class LoadMapSelector
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoadMapSelector));
            this.loadButton = new System.Windows.Forms.Button();
            this.mapBrowser = new HaCreator.CustomControls.MapBrowser();
            this.searchBox = new HaCreator.CustomControls.WatermarkTextBox();
            this.SuspendLayout();
            // 
            // loadButton
            // 
            this.loadButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.loadButton.Enabled = false;
            this.loadButton.Location = new System.Drawing.Point(11, 752);
            this.loadButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.loadButton.Name = "loadButton";
            this.loadButton.Size = new System.Drawing.Size(812, 46);
            this.loadButton.TabIndex = 9;
            this.loadButton.Text = "Select";
            this.loadButton.Click += new System.EventHandler(this.loadButton_Click);
            // 
            // mapBrowser
            // 
            this.mapBrowser.Location = new System.Drawing.Point(13, 47);
            this.mapBrowser.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.mapBrowser.Name = "mapBrowser";
            this.mapBrowser.Size = new System.Drawing.Size(810, 692);
            this.mapBrowser.TabIndex = 8;
            this.mapBrowser.SelectionChanged += new HaCreator.CustomControls.MapBrowser.MapSelectChangedDelegate(this.mapBrowser_SelectionChanged);
            // 
            // searchBox
            // 
            this.searchBox.ForeColor = System.Drawing.Color.Gray;
            this.searchBox.Location = new System.Drawing.Point(13, 10);
            this.searchBox.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.searchBox.Name = "searchBox";
            this.searchBox.Size = new System.Drawing.Size(810, 26);
            this.searchBox.TabIndex = 7;
            this.searchBox.Text = "Type here to search";
            this.searchBox.WatermarkActive = true;
            this.searchBox.WatermarkText = "Type here";
            // 
            // LoadMapSelector
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(838, 804);
            this.Controls.Add(this.mapBrowser);
            this.Controls.Add(this.searchBox);
            this.Controls.Add(this.loadButton);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MaximizeBox = false;
            this.Name = "LoadMapSelector";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Select a field";
            this.Load += new System.EventHandler(this.Load_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Load_KeyDown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button loadButton;
        private WatermarkTextBox searchBox;
        private CustomControls.MapBrowser mapBrowser;
    }
}