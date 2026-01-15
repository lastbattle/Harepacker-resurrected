namespace HaCreator.GUI.EditorPanels
{
    partial class ObjectViewerPanel
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
            if (disposing)
            {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
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
            components = new System.ComponentModel.Container();
            mainSplitContainer = new System.Windows.Forms.SplitContainer();
            toolbarPanel = new System.Windows.Forms.TableLayoutPanel();
            searchBox = new System.Windows.Forms.TextBox();
            toolbarButtonPanel = new System.Windows.Forms.FlowLayoutPanel();
            btnRefresh = new System.Windows.Forms.Button();
            btnExpandAll = new System.Windows.Forms.Button();
            btnCollapseAll = new System.Windows.Forms.Button();
            objectTreeView = new System.Windows.Forms.TreeView();
            statusStrip = new System.Windows.Forms.StatusStrip();
            lblTotalCount = new System.Windows.Forms.ToolStripStatusLabel();
            lblSelectedCount = new System.Windows.Forms.ToolStripStatusLabel();
            contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(components);
            selectOnBoardMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            jumpToObjectMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            editPropertiesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            deleteMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            selectAllOfTypeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            selectAllInLayerMenuItem = new System.Windows.Forms.ToolStripMenuItem();

            ((System.ComponentModel.ISupportInitialize)mainSplitContainer).BeginInit();
            mainSplitContainer.Panel1.SuspendLayout();
            mainSplitContainer.Panel2.SuspendLayout();
            mainSplitContainer.SuspendLayout();
            toolbarPanel.SuspendLayout();
            toolbarButtonPanel.SuspendLayout();
            statusStrip.SuspendLayout();
            contextMenuStrip.SuspendLayout();
            SuspendLayout();

            //
            // mainSplitContainer
            //
            mainSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            mainSplitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            mainSplitContainer.Location = new System.Drawing.Point(0, 0);
            mainSplitContainer.Name = "mainSplitContainer";
            mainSplitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // mainSplitContainer.Panel1
            //
            mainSplitContainer.Panel1.Controls.Add(toolbarPanel);
            mainSplitContainer.Panel1MinSize = 50;
            //
            // mainSplitContainer.Panel2
            //
            mainSplitContainer.Panel2.Controls.Add(objectTreeView);
            mainSplitContainer.Panel2.Controls.Add(statusStrip);
            mainSplitContainer.Size = new System.Drawing.Size(269, 658);
            mainSplitContainer.SplitterDistance = 50;
            mainSplitContainer.TabIndex = 0;

            //
            // toolbarPanel
            //
            toolbarPanel.ColumnCount = 1;
            toolbarPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            toolbarPanel.Controls.Add(searchBox, 0, 0);
            toolbarPanel.Controls.Add(toolbarButtonPanel, 0, 1);
            toolbarPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            toolbarPanel.Location = new System.Drawing.Point(0, 0);
            toolbarPanel.Name = "toolbarPanel";
            toolbarPanel.RowCount = 2;
            toolbarPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            toolbarPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            toolbarPanel.Size = new System.Drawing.Size(269, 50);
            toolbarPanel.TabIndex = 0;

            //
            // searchBox
            //
            searchBox.Dock = System.Windows.Forms.DockStyle.Fill;
            searchBox.Location = new System.Drawing.Point(3, 1);
            searchBox.Margin = new System.Windows.Forms.Padding(3, 1, 3, 1);
            searchBox.Name = "searchBox";
            searchBox.PlaceholderText = "Search...";
            searchBox.Size = new System.Drawing.Size(263, 22);
            searchBox.TabIndex = 0;
            searchBox.TextChanged += SearchBox_TextChanged;

            //
            // toolbarButtonPanel
            //
            toolbarButtonPanel.Controls.Add(btnRefresh);
            toolbarButtonPanel.Controls.Add(btnExpandAll);
            toolbarButtonPanel.Controls.Add(btnCollapseAll);
            toolbarButtonPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            toolbarButtonPanel.Location = new System.Drawing.Point(0, 48);
            toolbarButtonPanel.Margin = new System.Windows.Forms.Padding(0);
            toolbarButtonPanel.Name = "toolbarButtonPanel";
            toolbarButtonPanel.Size = new System.Drawing.Size(269, 12);
            toolbarButtonPanel.TabIndex = 2;

            //
            // btnRefresh
            //
            btnRefresh.Location = new System.Drawing.Point(3, 0);
            btnRefresh.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new System.Drawing.Size(60, 20);
            btnRefresh.TabIndex = 0;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += BtnRefresh_Click;

            //
            // btnExpandAll
            //
            btnExpandAll.Location = new System.Drawing.Point(69, 0);
            btnExpandAll.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            btnExpandAll.Name = "btnExpandAll";
            btnExpandAll.Size = new System.Drawing.Size(75, 20);
            btnExpandAll.TabIndex = 1;
            btnExpandAll.Text = "Expand All";
            btnExpandAll.UseVisualStyleBackColor = true;
            btnExpandAll.Click += BtnExpandAll_Click;

            //
            // btnCollapseAll
            //
            btnCollapseAll.Location = new System.Drawing.Point(150, 0);
            btnCollapseAll.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            btnCollapseAll.Name = "btnCollapseAll";
            btnCollapseAll.Size = new System.Drawing.Size(80, 20);
            btnCollapseAll.TabIndex = 2;
            btnCollapseAll.Text = "Collapse All";
            btnCollapseAll.UseVisualStyleBackColor = true;
            btnCollapseAll.Click += BtnCollapseAll_Click;

            //
            // objectTreeView
            //
            objectTreeView.ContextMenuStrip = contextMenuStrip;
            objectTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            objectTreeView.FullRowSelect = true;
            objectTreeView.HideSelection = false;
            objectTreeView.Location = new System.Drawing.Point(0, 0);
            objectTreeView.Name = "objectTreeView";
            objectTreeView.ShowLines = true;
            objectTreeView.Size = new System.Drawing.Size(269, 572);
            objectTreeView.TabIndex = 0;
            objectTreeView.AfterSelect += ObjectTreeView_AfterSelect;
            objectTreeView.NodeMouseDoubleClick += ObjectTreeView_NodeMouseDoubleClick;
            objectTreeView.KeyDown += ObjectTreeView_KeyDown;

            //
            // statusStrip
            //
            statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                lblTotalCount,
                lblSelectedCount
            });
            statusStrip.Location = new System.Drawing.Point(0, 572);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new System.Drawing.Size(269, 22);
            statusStrip.TabIndex = 1;

            //
            // lblTotalCount
            //
            lblTotalCount.Name = "lblTotalCount";
            lblTotalCount.Size = new System.Drawing.Size(46, 17);
            lblTotalCount.Text = "Total: 0";

            //
            // lblSelectedCount
            //
            lblSelectedCount.Name = "lblSelectedCount";
            lblSelectedCount.Size = new System.Drawing.Size(65, 17);
            lblSelectedCount.Text = "| Selected: 0";

            //
            // contextMenuStrip
            //
            contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                selectOnBoardMenuItem,
                jumpToObjectMenuItem,
                toolStripSeparator1,
                editPropertiesMenuItem,
                deleteMenuItem,
                toolStripSeparator2,
                selectAllOfTypeMenuItem,
                selectAllInLayerMenuItem
            });
            contextMenuStrip.Name = "contextMenuStrip";
            contextMenuStrip.Size = new System.Drawing.Size(170, 148);
            contextMenuStrip.Opening += ContextMenuStrip_Opening;

            //
            // selectOnBoardMenuItem
            //
            selectOnBoardMenuItem.Name = "selectOnBoardMenuItem";
            selectOnBoardMenuItem.Size = new System.Drawing.Size(169, 22);
            selectOnBoardMenuItem.Text = "Select on Board";
            selectOnBoardMenuItem.Click += SelectOnBoardMenuItem_Click;

            //
            // jumpToObjectMenuItem
            //
            jumpToObjectMenuItem.Name = "jumpToObjectMenuItem";
            jumpToObjectMenuItem.Size = new System.Drawing.Size(169, 22);
            jumpToObjectMenuItem.Text = "Jump to Object";
            jumpToObjectMenuItem.Click += JumpToObjectMenuItem_Click;

            //
            // toolStripSeparator1
            //
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(166, 6);

            //
            // editPropertiesMenuItem
            //
            editPropertiesMenuItem.Name = "editPropertiesMenuItem";
            editPropertiesMenuItem.Size = new System.Drawing.Size(169, 22);
            editPropertiesMenuItem.Text = "Edit Properties...";
            editPropertiesMenuItem.Click += EditPropertiesMenuItem_Click;

            //
            // deleteMenuItem
            //
            deleteMenuItem.Name = "deleteMenuItem";
            deleteMenuItem.Size = new System.Drawing.Size(169, 22);
            deleteMenuItem.Text = "Delete";
            deleteMenuItem.Click += DeleteMenuItem_Click;

            //
            // toolStripSeparator2
            //
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new System.Drawing.Size(166, 6);

            //
            // selectAllOfTypeMenuItem
            //
            selectAllOfTypeMenuItem.Name = "selectAllOfTypeMenuItem";
            selectAllOfTypeMenuItem.Size = new System.Drawing.Size(169, 22);
            selectAllOfTypeMenuItem.Text = "Select All of Type";
            selectAllOfTypeMenuItem.Click += SelectAllOfTypeMenuItem_Click;

            //
            // selectAllInLayerMenuItem
            //
            selectAllInLayerMenuItem.Name = "selectAllInLayerMenuItem";
            selectAllInLayerMenuItem.Size = new System.Drawing.Size(169, 22);
            selectAllInLayerMenuItem.Text = "Select All in Layer";
            selectAllInLayerMenuItem.Click += SelectAllInLayerMenuItem_Click;

            //
            // ObjectViewerPanel
            //
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            Controls.Add(mainSplitContainer);
            Font = new System.Drawing.Font("Segoe UI", 8.25F);
            Name = "ObjectViewerPanel";
            Size = new System.Drawing.Size(269, 658);

            mainSplitContainer.Panel1.ResumeLayout(false);
            mainSplitContainer.Panel2.ResumeLayout(false);
            mainSplitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)mainSplitContainer).EndInit();
            mainSplitContainer.ResumeLayout(false);
            toolbarPanel.ResumeLayout(false);
            toolbarPanel.PerformLayout();
            toolbarButtonPanel.ResumeLayout(false);
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            contextMenuStrip.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.SplitContainer mainSplitContainer;
        private System.Windows.Forms.TableLayoutPanel toolbarPanel;
        private System.Windows.Forms.TextBox searchBox;
        private System.Windows.Forms.FlowLayoutPanel toolbarButtonPanel;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnExpandAll;
        private System.Windows.Forms.Button btnCollapseAll;
        private System.Windows.Forms.TreeView objectTreeView;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblTotalCount;
        private System.Windows.Forms.ToolStripStatusLabel lblSelectedCount;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem selectOnBoardMenuItem;
        private System.Windows.Forms.ToolStripMenuItem jumpToObjectMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem editPropertiesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem selectAllOfTypeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectAllInLayerMenuItem;
    }
}
