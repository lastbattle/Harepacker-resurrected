using HaCreator.CustomControls;

namespace HaCreator.GUI.InstanceEditor
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
            this.searchBox = new HaCreator.CustomControls.WatermarkTextBox();
            this.fontDialog1 = new System.Windows.Forms.FontDialog();
            this.mapBrowserHost = new System.Windows.Forms.Integration.ElementHost();
            this.mapBrowser = new HaCreator.CustomControls.MapBrowserWpf();
            this.SuspendLayout();
            // 
            // loadButton
            // 
            this.loadButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.loadButton.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.loadButton.Location = new System.Drawing.Point(0, 497);
            this.loadButton.Name = "loadButton";
            this.loadButton.Size = new System.Drawing.Size(813, 26);
            this.loadButton.TabIndex = 9;
            this.loadButton.Text = "Select";
            this.loadButton.Click += new System.EventHandler(this.loadButton_Click);
            // 
            // searchBox
            //
            this.searchBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.searchBox.ForeColor = System.Drawing.Color.Gray;
            this.searchBox.Location = new System.Drawing.Point(0, 0);
            this.searchBox.Name = "searchBox";
            this.searchBox.Size = new System.Drawing.Size(813, 22);
            this.searchBox.TabIndex = 7;
            this.searchBox.Text = "Type here to search";
            this.searchBox.WatermarkActive = true;
            this.searchBox.WatermarkText = "Type here";
            //
            // mapBrowserHost
            //
            this.mapBrowserHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mapBrowserHost.Location = new System.Drawing.Point(0, 22);
            this.mapBrowserHost.Name = "mapBrowserHost";
            this.mapBrowserHost.Size = new System.Drawing.Size(813, 475);
            this.mapBrowserHost.TabIndex = 8;
            this.mapBrowserHost.Text = "mapBrowserHost";
            this.mapBrowserHost.Child = this.mapBrowser;
            //
            // mapBrowser
            //
            this.mapBrowser.SelectionChanged += new HaCreator.CustomControls.MapBrowserWpf.MapSelectChangedDelegate(this.mapBrowser_SelectionChanged);
            //
            // LoadMapSelector
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(813, 523);
            this.Controls.Add(this.mapBrowserHost);
            this.Controls.Add(this.searchBox);
            this.Controls.Add(this.loadButton);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
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
        private System.Windows.Forms.Integration.ElementHost mapBrowserHost;
        private CustomControls.MapBrowserWpf mapBrowser;
        private System.Windows.Forms.FontDialog fontDialog1;
    }
}
