using HaCreator.CustomControls;

namespace HaCreator.GUI
{
    partial class FieldSelector
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FieldSelector));
            this.loadButton = new System.Windows.Forms.Button();
            this.WZSelect = new System.Windows.Forms.RadioButton();
            this.XMLSelect = new System.Windows.Forms.RadioButton();
            this.XMLBox = new System.Windows.Forms.TextBox();
            this.HAMBox = new System.Windows.Forms.TextBox();
            this.HAMSelect = new System.Windows.Forms.RadioButton();
            this.tabControl_maps = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.checkBox_townOnly = new System.Windows.Forms.CheckBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.button_clearHistory = new System.Windows.Forms.Button();
            this.button_deleteSelected = new System.Windows.Forms.Button();
            this.button_loadHistory = new System.Windows.Forms.Button();
            this.mapBrowser = new HaCreator.CustomControls.MapBrowser();
            this.mapBrowser_history = new HaCreator.CustomControls.MapBrowser();
            this.searchBox = new HaCreator.CustomControls.WatermarkTextBox();
            this.tabControl_maps.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.SuspendLayout();
            // 
            // loadButton
            // 
            this.loadButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.loadButton.Enabled = false;
            this.loadButton.Location = new System.Drawing.Point(6, 506);
            this.loadButton.Name = "loadButton";
            this.loadButton.Size = new System.Drawing.Size(753, 30);
            this.loadButton.TabIndex = 9;
            this.loadButton.Text = "Load";
            this.loadButton.Click += new System.EventHandler(this.LoadButton_Click);
            // 
            // WZSelect
            // 
            this.WZSelect.AutoSize = true;
            this.WZSelect.Checked = true;
            this.WZSelect.Location = new System.Drawing.Point(11, 63);
            this.WZSelect.Name = "WZSelect";
            this.WZSelect.Size = new System.Drawing.Size(42, 17);
            this.WZSelect.TabIndex = 6;
            this.WZSelect.TabStop = true;
            this.WZSelect.Text = "WZ";
            this.WZSelect.UseVisualStyleBackColor = true;
            this.WZSelect.CheckedChanged += new System.EventHandler(this.SelectionChanged);
            // 
            // XMLSelect
            // 
            this.XMLSelect.AutoSize = true;
            this.XMLSelect.Location = new System.Drawing.Point(11, 39);
            this.XMLSelect.Name = "XMLSelect";
            this.XMLSelect.Size = new System.Drawing.Size(46, 17);
            this.XMLSelect.TabIndex = 3;
            this.XMLSelect.Text = "XML";
            this.XMLSelect.UseVisualStyleBackColor = true;
            this.XMLSelect.CheckedChanged += new System.EventHandler(this.SelectionChanged);
            // 
            // XMLBox
            // 
            this.XMLBox.Enabled = false;
            this.XMLBox.Location = new System.Drawing.Point(64, 38);
            this.XMLBox.Name = "XMLBox";
            this.XMLBox.Size = new System.Drawing.Size(692, 22);
            this.XMLBox.TabIndex = 4;
            this.XMLBox.Click += new System.EventHandler(this.BrowseXML_Click);
            this.XMLBox.TextChanged += new System.EventHandler(this.XMLBox_TextChanged);
            // 
            // HAMBox
            // 
            this.HAMBox.Enabled = false;
            this.HAMBox.Location = new System.Drawing.Point(64, 12);
            this.HAMBox.Name = "HAMBox";
            this.HAMBox.Size = new System.Drawing.Size(692, 22);
            this.HAMBox.TabIndex = 1;
            this.HAMBox.Click += new System.EventHandler(this.BrowseHAM_Click);
            this.HAMBox.TextChanged += new System.EventHandler(this.HAMBox_TextChanged);
            // 
            // HAMSelect
            // 
            this.HAMSelect.AutoSize = true;
            this.HAMSelect.Location = new System.Drawing.Point(11, 13);
            this.HAMSelect.Name = "HAMSelect";
            this.HAMSelect.Size = new System.Drawing.Size(50, 17);
            this.HAMSelect.TabIndex = 0;
            this.HAMSelect.Text = "HAM";
            this.HAMSelect.UseVisualStyleBackColor = true;
            // 
            // tabControl_maps
            // 
            this.tabControl_maps.Controls.Add(this.tabPage1);
            this.tabControl_maps.Controls.Add(this.tabPage2);
            this.tabControl_maps.Location = new System.Drawing.Point(8, 86);
            this.tabControl_maps.Margin = new System.Windows.Forms.Padding(2);
            this.tabControl_maps.Name = "tabControl_maps";
            this.tabControl_maps.SelectedIndex = 0;
            this.tabControl_maps.Size = new System.Drawing.Size(769, 568);
            this.tabControl_maps.TabIndex = 10;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.checkBox_townOnly);
            this.tabPage1.Controls.Add(this.mapBrowser);
            this.tabPage1.Controls.Add(this.loadButton);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(2);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(2);
            this.tabPage1.Size = new System.Drawing.Size(761, 542);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Maps";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // checkBox_townOnly
            // 
            this.checkBox_townOnly.AutoSize = true;
            this.checkBox_townOnly.Location = new System.Drawing.Point(6, 485);
            this.checkBox_townOnly.Name = "checkBox_townOnly";
            this.checkBox_townOnly.Size = new System.Drawing.Size(79, 17);
            this.checkBox_townOnly.TabIndex = 11;
            this.checkBox_townOnly.Text = "Town only";
            this.checkBox_townOnly.UseVisualStyleBackColor = true;
            this.checkBox_townOnly.CheckedChanged += new System.EventHandler(this.checkBox_townOnly_CheckedChanged);
            //
            // tabPage2
            //
            this.tabPage2.Controls.Add(this.button_loadHistory);
            this.tabPage2.Controls.Add(this.mapBrowser_history);
            this.tabPage2.Controls.Add(this.button_deleteSelected);
            this.tabPage2.Controls.Add(this.button_clearHistory);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Margin = new System.Windows.Forms.Padding(2);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(2);
            this.tabPage2.Size = new System.Drawing.Size(761, 542);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "History";
            this.tabPage2.UseVisualStyleBackColor = true;
            //
            // button_clearHistory
            //
            this.button_clearHistory.Location = new System.Drawing.Point(6, 484);
            this.button_clearHistory.Name = "button_clearHistory";
            this.button_clearHistory.Size = new System.Drawing.Size(89, 23);
            this.button_clearHistory.TabIndex = 0;
            this.button_clearHistory.Text = "Clear history";
            this.button_clearHistory.UseVisualStyleBackColor = true;
            this.button_clearHistory.Click += new System.EventHandler(this.button_clearHistory_Click);
            //
            // button_deleteSelected
            //
            this.button_deleteSelected.Enabled = false;
            this.button_deleteSelected.Location = new System.Drawing.Point(101, 484);
            this.button_deleteSelected.Name = "button_deleteSelected";
            this.button_deleteSelected.Size = new System.Drawing.Size(100, 23);
            this.button_deleteSelected.TabIndex = 11;
            this.button_deleteSelected.Text = "Delete Selected";
            this.button_deleteSelected.UseVisualStyleBackColor = true;
            this.button_deleteSelected.Click += new System.EventHandler(this.button_deleteSelected_Click);
            //
            // button_loadHistory
            // 
            this.button_loadHistory.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.button_loadHistory.Enabled = false;
            this.button_loadHistory.Location = new System.Drawing.Point(6, 507);
            this.button_loadHistory.Name = "button_loadHistory";
            this.button_loadHistory.Size = new System.Drawing.Size(753, 30);
            this.button_loadHistory.TabIndex = 10;
            this.button_loadHistory.Text = "Load";
            this.button_loadHistory.Click += new System.EventHandler(this.button_loadHistory_Click);
            // 
            // mapBrowser
            // 
            this.mapBrowser.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.mapBrowser.Location = new System.Drawing.Point(6, 5);
            this.mapBrowser.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.mapBrowser.Name = "mapBrowser";
            this.mapBrowser.Size = new System.Drawing.Size(746, 479);
            this.mapBrowser.TabIndex = 8;
            this.mapBrowser.TownOnlyFilter = false;
            this.mapBrowser.SelectionChanged += new HaCreator.CustomControls.MapBrowser.MapSelectChangedDelegate(this.MapBrowser_SelectionChanged);
            // 
            // mapBrowser_history
            // 
            this.mapBrowser_history.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.mapBrowser_history.Location = new System.Drawing.Point(6, 5);
            this.mapBrowser_history.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.mapBrowser_history.Name = "mapBrowser_history";
            this.mapBrowser_history.Size = new System.Drawing.Size(746, 479);
            this.mapBrowser_history.TabIndex = 9;
            this.mapBrowser_history.TownOnlyFilter = false;
            this.mapBrowser_history.SelectionChanged += new HaCreator.CustomControls.MapBrowser.MapSelectChangedDelegate(this.mapBrowserHistory_OnSelectionChanged);
            // 
            // searchBox
            // 
            this.searchBox.ForeColor = System.Drawing.Color.Gray;
            this.searchBox.Location = new System.Drawing.Point(64, 62);
            this.searchBox.Name = "searchBox";
            this.searchBox.Size = new System.Drawing.Size(692, 22);
            this.searchBox.TabIndex = 7;
            this.searchBox.Text = "Type here";
            this.searchBox.WatermarkActive = true;
            this.searchBox.WatermarkText = "Type here";
            // 
            // FieldSelector
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(783, 656);
            this.Controls.Add(this.tabControl_maps);
            this.Controls.Add(this.HAMBox);
            this.Controls.Add(this.HAMSelect);
            this.Controls.Add(this.searchBox);
            this.Controls.Add(this.XMLBox);
            this.Controls.Add(this.XMLSelect);
            this.Controls.Add(this.WZSelect);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.Name = "FieldSelector";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Load";
            this.Load += new System.EventHandler(this.Load_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Load_KeyDown);
            this.tabControl_maps.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button loadButton;
        private System.Windows.Forms.RadioButton WZSelect;
        private System.Windows.Forms.RadioButton XMLSelect;
        private System.Windows.Forms.TextBox XMLBox;
        private WatermarkTextBox searchBox;
        private CustomControls.MapBrowser mapBrowser;
        private System.Windows.Forms.TextBox HAMBox;
        private System.Windows.Forms.RadioButton HAMSelect;
        private System.Windows.Forms.TabControl tabControl_maps;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.CheckBox checkBox_townOnly;
        private System.Windows.Forms.Button button_clearHistory;
        private System.Windows.Forms.Button button_deleteSelected;
        private MapBrowser mapBrowser_history;
        private System.Windows.Forms.Button button_loadHistory;
    }
}