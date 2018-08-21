using HaRepacker.GUI.Panels;

namespace HaRepacker.GUI
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.mainMenu = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.unloadAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reloadAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzImageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.wzByteFloatPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzCanvasPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzLongPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzCompressedIntPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzConvexPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzDoublePropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzNullPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzSoundPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzStringPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzSubPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzUnsignedShortPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzUolPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.wzVectorPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.undoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.redoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.expandAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.collapseAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportFilesToXMLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.xMLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rawDataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.imgToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportDataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.xMLToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.privateServerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.classicToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.pNGsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.imgToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.importToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.xMLToolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.iMGToolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.searchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extrasToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fHMappingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.renderMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.zoomTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.animateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem_searchWzStrings = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem_WzEncryption = new System.Windows.Forms.ToolStripMenuItem();
            this.encryptionBox = new System.Windows.Forms.ToolStripComboBox();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewHelpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debugToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.AbortButton = new System.Windows.Forms.Button();
            this.tabControl_MainPanels = new System.Windows.Forms.TabControl();
            this.button_addTab = new System.Windows.Forms.Button();
            this.mainMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainMenu
            // 
            this.mainMenu.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.mainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.toolsToolStripMenuItem,
            this.extrasToolStripMenuItem,
            this.encryptionBox,
            this.helpToolStripMenuItem,
            this.debugToolStripMenuItem});
            resources.ApplyResources(this.mainMenu, "mainMenu");
            this.mainMenu.Name = "mainMenu";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolStripMenuItem,
            this.openToolStripMenuItem,
            this.saveToolStripMenuItem,
            this.unloadAllToolStripMenuItem,
            this.reloadAllToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            resources.ApplyResources(this.fileToolStripMenuItem, "fileToolStripMenuItem");
            // 
            // newToolStripMenuItem
            // 
            this.newToolStripMenuItem.Image = global::HaRepacker.Properties.Resources.page_white;
            this.newToolStripMenuItem.Name = "newToolStripMenuItem";
            resources.ApplyResources(this.newToolStripMenuItem, "newToolStripMenuItem");
            this.newToolStripMenuItem.Click += new System.EventHandler(this.newToolStripMenuItem_Click);
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Image = global::HaRepacker.Properties.Resources.folder;
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            resources.ApplyResources(this.openToolStripMenuItem, "openToolStripMenuItem");
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // saveToolStripMenuItem
            // 
            resources.ApplyResources(this.saveToolStripMenuItem, "saveToolStripMenuItem");
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // unloadAllToolStripMenuItem
            // 
            resources.ApplyResources(this.unloadAllToolStripMenuItem, "unloadAllToolStripMenuItem");
            this.unloadAllToolStripMenuItem.Name = "unloadAllToolStripMenuItem";
            this.unloadAllToolStripMenuItem.Click += new System.EventHandler(this.unloadAllToolStripMenuItem_Click);
            // 
            // reloadAllToolStripMenuItem
            // 
            resources.ApplyResources(this.reloadAllToolStripMenuItem, "reloadAllToolStripMenuItem");
            this.reloadAllToolStripMenuItem.Name = "reloadAllToolStripMenuItem";
            this.reloadAllToolStripMenuItem.Click += new System.EventHandler(this.reloadAllToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addToolStripMenuItem,
            this.removeToolStripMenuItem,
            this.undoToolStripMenuItem,
            this.redoToolStripMenuItem,
            this.expandAllToolStripMenuItem,
            this.collapseAllToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            resources.ApplyResources(this.editToolStripMenuItem, "editToolStripMenuItem");
            // 
            // addToolStripMenuItem
            // 
            this.addToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.wzDirectoryToolStripMenuItem,
            this.wzImageToolStripMenuItem,
            this.toolStripSeparator1,
            this.wzByteFloatPropertyToolStripMenuItem,
            this.wzCanvasPropertyToolStripMenuItem,
            this.wzLongPropertyToolStripMenuItem,
            this.wzCompressedIntPropertyToolStripMenuItem,
            this.wzConvexPropertyToolStripMenuItem,
            this.wzDoublePropertyToolStripMenuItem,
            this.wzNullPropertyToolStripMenuItem,
            this.wzSoundPropertyToolStripMenuItem,
            this.wzStringPropertyToolStripMenuItem,
            this.wzSubPropertyToolStripMenuItem,
            this.wzUnsignedShortPropertyToolStripMenuItem,
            this.wzUolPropertyToolStripMenuItem,
            this.wzVectorPropertyToolStripMenuItem});
            resources.ApplyResources(this.addToolStripMenuItem, "addToolStripMenuItem");
            this.addToolStripMenuItem.Name = "addToolStripMenuItem";
            // 
            // wzDirectoryToolStripMenuItem
            // 
            this.wzDirectoryToolStripMenuItem.Name = "wzDirectoryToolStripMenuItem";
            resources.ApplyResources(this.wzDirectoryToolStripMenuItem, "wzDirectoryToolStripMenuItem");
            this.wzDirectoryToolStripMenuItem.Click += new System.EventHandler(this.wzDirectoryToolStripMenuItem_Click);
            // 
            // wzImageToolStripMenuItem
            // 
            this.wzImageToolStripMenuItem.Name = "wzImageToolStripMenuItem";
            resources.ApplyResources(this.wzImageToolStripMenuItem, "wzImageToolStripMenuItem");
            this.wzImageToolStripMenuItem.Click += new System.EventHandler(this.wzImageToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            // 
            // wzByteFloatPropertyToolStripMenuItem
            // 
            this.wzByteFloatPropertyToolStripMenuItem.Name = "wzByteFloatPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzByteFloatPropertyToolStripMenuItem, "wzByteFloatPropertyToolStripMenuItem");
            this.wzByteFloatPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzByteFloatPropertyToolStripMenuItem_Click);
            // 
            // wzCanvasPropertyToolStripMenuItem
            // 
            this.wzCanvasPropertyToolStripMenuItem.Name = "wzCanvasPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzCanvasPropertyToolStripMenuItem, "wzCanvasPropertyToolStripMenuItem");
            this.wzCanvasPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzCanvasPropertyToolStripMenuItem_Click);
            // 
            // wzLongPropertyToolStripMenuItem
            // 
            this.wzLongPropertyToolStripMenuItem.Name = "wzLongPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzLongPropertyToolStripMenuItem, "wzLongPropertyToolStripMenuItem");
            this.wzLongPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzLongPropertyToolStripMenuItem_Click);
            // 
            // wzCompressedIntPropertyToolStripMenuItem
            // 
            this.wzCompressedIntPropertyToolStripMenuItem.Name = "wzCompressedIntPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzCompressedIntPropertyToolStripMenuItem, "wzCompressedIntPropertyToolStripMenuItem");
            this.wzCompressedIntPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzCompressedIntPropertyToolStripMenuItem_Click);
            // 
            // wzConvexPropertyToolStripMenuItem
            // 
            this.wzConvexPropertyToolStripMenuItem.Name = "wzConvexPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzConvexPropertyToolStripMenuItem, "wzConvexPropertyToolStripMenuItem");
            this.wzConvexPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzConvexPropertyToolStripMenuItem_Click);
            // 
            // wzDoublePropertyToolStripMenuItem
            // 
            this.wzDoublePropertyToolStripMenuItem.Name = "wzDoublePropertyToolStripMenuItem";
            resources.ApplyResources(this.wzDoublePropertyToolStripMenuItem, "wzDoublePropertyToolStripMenuItem");
            this.wzDoublePropertyToolStripMenuItem.Click += new System.EventHandler(this.wzDoublePropertyToolStripMenuItem_Click);
            // 
            // wzNullPropertyToolStripMenuItem
            // 
            this.wzNullPropertyToolStripMenuItem.Name = "wzNullPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzNullPropertyToolStripMenuItem, "wzNullPropertyToolStripMenuItem");
            this.wzNullPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzNullPropertyToolStripMenuItem_Click);
            // 
            // wzSoundPropertyToolStripMenuItem
            // 
            this.wzSoundPropertyToolStripMenuItem.Name = "wzSoundPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzSoundPropertyToolStripMenuItem, "wzSoundPropertyToolStripMenuItem");
            this.wzSoundPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzSoundPropertyToolStripMenuItem_Click);
            // 
            // wzStringPropertyToolStripMenuItem
            // 
            this.wzStringPropertyToolStripMenuItem.Name = "wzStringPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzStringPropertyToolStripMenuItem, "wzStringPropertyToolStripMenuItem");
            this.wzStringPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzStringPropertyToolStripMenuItem_Click);
            // 
            // wzSubPropertyToolStripMenuItem
            // 
            this.wzSubPropertyToolStripMenuItem.Name = "wzSubPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzSubPropertyToolStripMenuItem, "wzSubPropertyToolStripMenuItem");
            this.wzSubPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzSubPropertyToolStripMenuItem_Click);
            // 
            // wzUnsignedShortPropertyToolStripMenuItem
            // 
            this.wzUnsignedShortPropertyToolStripMenuItem.Name = "wzUnsignedShortPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzUnsignedShortPropertyToolStripMenuItem, "wzUnsignedShortPropertyToolStripMenuItem");
            this.wzUnsignedShortPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzUnsignedShortPropertyToolStripMenuItem_Click);
            // 
            // wzUolPropertyToolStripMenuItem
            // 
            this.wzUolPropertyToolStripMenuItem.Name = "wzUolPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzUolPropertyToolStripMenuItem, "wzUolPropertyToolStripMenuItem");
            this.wzUolPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzUolPropertyToolStripMenuItem_Click);
            // 
            // wzVectorPropertyToolStripMenuItem
            // 
            this.wzVectorPropertyToolStripMenuItem.Name = "wzVectorPropertyToolStripMenuItem";
            resources.ApplyResources(this.wzVectorPropertyToolStripMenuItem, "wzVectorPropertyToolStripMenuItem");
            this.wzVectorPropertyToolStripMenuItem.Click += new System.EventHandler(this.wzVectorPropertyToolStripMenuItem_Click);
            // 
            // removeToolStripMenuItem
            // 
            resources.ApplyResources(this.removeToolStripMenuItem, "removeToolStripMenuItem");
            this.removeToolStripMenuItem.Name = "removeToolStripMenuItem";
            this.removeToolStripMenuItem.Click += new System.EventHandler(this.removeToolStripMenuItem_Click);
            // 
            // undoToolStripMenuItem
            // 
            this.undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            resources.ApplyResources(this.undoToolStripMenuItem, "undoToolStripMenuItem");
            // 
            // redoToolStripMenuItem
            // 
            this.redoToolStripMenuItem.Name = "redoToolStripMenuItem";
            resources.ApplyResources(this.redoToolStripMenuItem, "redoToolStripMenuItem");
            // 
            // expandAllToolStripMenuItem
            // 
            this.expandAllToolStripMenuItem.Name = "expandAllToolStripMenuItem";
            resources.ApplyResources(this.expandAllToolStripMenuItem, "expandAllToolStripMenuItem");
            this.expandAllToolStripMenuItem.Click += new System.EventHandler(this.expandAllToolStripMenuItem_Click);
            // 
            // collapseAllToolStripMenuItem
            // 
            this.collapseAllToolStripMenuItem.Name = "collapseAllToolStripMenuItem";
            resources.ApplyResources(this.collapseAllToolStripMenuItem, "collapseAllToolStripMenuItem");
            this.collapseAllToolStripMenuItem.Click += new System.EventHandler(this.collapseAllToolStripMenuItem_Click);
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportFilesToXMLToolStripMenuItem,
            this.exportDataToolStripMenuItem,
            this.importToolStripMenuItem,
            this.optionsToolStripMenuItem,
            this.searchToolStripMenuItem,
            this.copyToolStripMenuItem,
            this.pasteToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            resources.ApplyResources(this.toolsToolStripMenuItem, "toolsToolStripMenuItem");
            // 
            // exportFilesToXMLToolStripMenuItem
            // 
            this.exportFilesToXMLToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.xMLToolStripMenuItem,
            this.rawDataToolStripMenuItem,
            this.imgToolStripMenuItem});
            this.exportFilesToXMLToolStripMenuItem.Image = global::HaRepacker.Properties.Resources.folder_go;
            this.exportFilesToXMLToolStripMenuItem.Name = "exportFilesToXMLToolStripMenuItem";
            resources.ApplyResources(this.exportFilesToXMLToolStripMenuItem, "exportFilesToXMLToolStripMenuItem");
            // 
            // xMLToolStripMenuItem
            // 
            this.xMLToolStripMenuItem.Name = "xMLToolStripMenuItem";
            resources.ApplyResources(this.xMLToolStripMenuItem, "xMLToolStripMenuItem");
            this.xMLToolStripMenuItem.Click += new System.EventHandler(this.xMLToolStripMenuItem_Click);
            // 
            // rawDataToolStripMenuItem
            // 
            this.rawDataToolStripMenuItem.Name = "rawDataToolStripMenuItem";
            resources.ApplyResources(this.rawDataToolStripMenuItem, "rawDataToolStripMenuItem");
            this.rawDataToolStripMenuItem.Click += new System.EventHandler(this.rawDataToolStripMenuItem_Click);
            // 
            // imgToolStripMenuItem
            // 
            this.imgToolStripMenuItem.Name = "imgToolStripMenuItem";
            resources.ApplyResources(this.imgToolStripMenuItem, "imgToolStripMenuItem");
            this.imgToolStripMenuItem.Click += new System.EventHandler(this.imgToolStripMenuItem_Click);
            // 
            // exportDataToolStripMenuItem
            // 
            this.exportDataToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.xMLToolStripMenuItem1,
            this.pNGsToolStripMenuItem,
            this.imgToolStripMenuItem1});
            this.exportDataToolStripMenuItem.Image = global::HaRepacker.Properties.Resources.page_go;
            this.exportDataToolStripMenuItem.Name = "exportDataToolStripMenuItem";
            resources.ApplyResources(this.exportDataToolStripMenuItem, "exportDataToolStripMenuItem");
            // 
            // xMLToolStripMenuItem1
            // 
            this.xMLToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.privateServerToolStripMenuItem,
            this.classicToolStripMenuItem,
            this.newToolStripMenuItem1});
            this.xMLToolStripMenuItem1.Name = "xMLToolStripMenuItem1";
            resources.ApplyResources(this.xMLToolStripMenuItem1, "xMLToolStripMenuItem1");
            // 
            // privateServerToolStripMenuItem
            // 
            this.privateServerToolStripMenuItem.Name = "privateServerToolStripMenuItem";
            resources.ApplyResources(this.privateServerToolStripMenuItem, "privateServerToolStripMenuItem");
            this.privateServerToolStripMenuItem.Click += new System.EventHandler(this.privateServerToolStripMenuItem_Click);
            // 
            // classicToolStripMenuItem
            // 
            this.classicToolStripMenuItem.Name = "classicToolStripMenuItem";
            resources.ApplyResources(this.classicToolStripMenuItem, "classicToolStripMenuItem");
            this.classicToolStripMenuItem.Click += new System.EventHandler(this.classicToolStripMenuItem_Click);
            // 
            // newToolStripMenuItem1
            // 
            this.newToolStripMenuItem1.Name = "newToolStripMenuItem1";
            resources.ApplyResources(this.newToolStripMenuItem1, "newToolStripMenuItem1");
            this.newToolStripMenuItem1.Click += new System.EventHandler(this.newToolStripMenuItem1_Click);
            // 
            // pNGsToolStripMenuItem
            // 
            this.pNGsToolStripMenuItem.Name = "pNGsToolStripMenuItem";
            resources.ApplyResources(this.pNGsToolStripMenuItem, "pNGsToolStripMenuItem");
            this.pNGsToolStripMenuItem.Click += new System.EventHandler(this.pNGsToolStripMenuItem_Click);
            // 
            // imgToolStripMenuItem1
            // 
            this.imgToolStripMenuItem1.Name = "imgToolStripMenuItem1";
            resources.ApplyResources(this.imgToolStripMenuItem1, "imgToolStripMenuItem1");
            this.imgToolStripMenuItem1.Click += new System.EventHandler(this.imgToolStripMenuItem1_Click);
            // 
            // importToolStripMenuItem
            // 
            this.importToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.xMLToolStripMenuItem2,
            this.iMGToolStripMenuItem2});
            this.importToolStripMenuItem.Image = global::HaRepacker.Properties.Resources.page_add;
            this.importToolStripMenuItem.Name = "importToolStripMenuItem";
            resources.ApplyResources(this.importToolStripMenuItem, "importToolStripMenuItem");
            // 
            // xMLToolStripMenuItem2
            // 
            this.xMLToolStripMenuItem2.Name = "xMLToolStripMenuItem2";
            resources.ApplyResources(this.xMLToolStripMenuItem2, "xMLToolStripMenuItem2");
            this.xMLToolStripMenuItem2.Click += new System.EventHandler(this.xMLToolStripMenuItem2_Click);
            // 
            // iMGToolStripMenuItem2
            // 
            this.iMGToolStripMenuItem2.Name = "iMGToolStripMenuItem2";
            resources.ApplyResources(this.iMGToolStripMenuItem2, "iMGToolStripMenuItem2");
            this.iMGToolStripMenuItem2.Click += new System.EventHandler(this.iMGToolStripMenuItem2_Click);
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.Image = global::HaRepacker.Properties.Resources.cog;
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            resources.ApplyResources(this.optionsToolStripMenuItem, "optionsToolStripMenuItem");
            this.optionsToolStripMenuItem.Click += new System.EventHandler(this.optionsToolStripMenuItem_Click);
            // 
            // searchToolStripMenuItem
            // 
            resources.ApplyResources(this.searchToolStripMenuItem, "searchToolStripMenuItem");
            this.searchToolStripMenuItem.Name = "searchToolStripMenuItem";
            this.searchToolStripMenuItem.Click += new System.EventHandler(this.searchToolStripMenuItem_Click);
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            resources.ApplyResources(this.copyToolStripMenuItem, "copyToolStripMenuItem");
            this.copyToolStripMenuItem.Click += new System.EventHandler(this.copyToolStripMenuItem_Click);
            // 
            // pasteToolStripMenuItem
            // 
            this.pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            resources.ApplyResources(this.pasteToolStripMenuItem, "pasteToolStripMenuItem");
            this.pasteToolStripMenuItem.Click += new System.EventHandler(this.pasteToolStripMenuItem_Click);
            // 
            // extrasToolStripMenuItem
            // 
            this.extrasToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fHMappingToolStripMenuItem,
            this.animateToolStripMenuItem,
            this.toolStripMenuItem_searchWzStrings,
            this.toolStripMenuItem_WzEncryption});
            this.extrasToolStripMenuItem.Name = "extrasToolStripMenuItem";
            resources.ApplyResources(this.extrasToolStripMenuItem, "extrasToolStripMenuItem");
            // 
            // fHMappingToolStripMenuItem
            // 
            this.fHMappingToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.renderMapToolStripMenuItem,
            this.settingsToolStripMenuItem,
            this.zoomTextBox});
            this.fHMappingToolStripMenuItem.Name = "fHMappingToolStripMenuItem";
            resources.ApplyResources(this.fHMappingToolStripMenuItem, "fHMappingToolStripMenuItem");
            // 
            // renderMapToolStripMenuItem
            // 
            this.renderMapToolStripMenuItem.Image = global::HaRepacker.Properties.Resources.map;
            this.renderMapToolStripMenuItem.Name = "renderMapToolStripMenuItem";
            resources.ApplyResources(this.renderMapToolStripMenuItem, "renderMapToolStripMenuItem");
            this.renderMapToolStripMenuItem.Click += new System.EventHandler(this.renderMapToolStripMenuItem_Click);
            // 
            // settingsToolStripMenuItem
            // 
            this.settingsToolStripMenuItem.Image = global::HaRepacker.Properties.Resources.cog;
            this.settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            resources.ApplyResources(this.settingsToolStripMenuItem, "settingsToolStripMenuItem");
            this.settingsToolStripMenuItem.Click += new System.EventHandler(this.settingsToolStripMenuItem_Click);
            // 
            // zoomTextBox
            // 
            resources.ApplyResources(this.zoomTextBox, "zoomTextBox");
            this.zoomTextBox.Name = "zoomTextBox";
            // 
            // animateToolStripMenuItem
            // 
            this.animateToolStripMenuItem.Image = global::HaRepacker.Properties.Resources.lightning;
            this.animateToolStripMenuItem.Name = "animateToolStripMenuItem";
            resources.ApplyResources(this.animateToolStripMenuItem, "animateToolStripMenuItem");
            this.animateToolStripMenuItem.Click += new System.EventHandler(this.aPNGToolStripMenuItem_Click);
            // 
            // toolStripMenuItem_searchWzStrings
            // 
            this.toolStripMenuItem_searchWzStrings.Name = "toolStripMenuItem_searchWzStrings";
            resources.ApplyResources(this.toolStripMenuItem_searchWzStrings, "toolStripMenuItem_searchWzStrings");
            this.toolStripMenuItem_searchWzStrings.Click += new System.EventHandler(this.toolStripMenuItem_searchWzStrings_Click);
            // 
            // toolStripMenuItem_WzEncryption
            // 
            this.toolStripMenuItem_WzEncryption.Name = "toolStripMenuItem_WzEncryption";
            resources.ApplyResources(this.toolStripMenuItem_WzEncryption, "toolStripMenuItem_WzEncryption");
            this.toolStripMenuItem_WzEncryption.Click += new System.EventHandler(this.toolStripMenuItem_WzEncryption_Click);
            // 
            // encryptionBox
            // 
            this.encryptionBox.DropDownWidth = 400;
            resources.ApplyResources(this.encryptionBox, "encryptionBox");
            this.encryptionBox.Name = "encryptionBox";
            this.encryptionBox.SelectedIndexChanged += new System.EventHandler(this.encryptionBox_SelectedIndexChanged);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.viewHelpToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            resources.ApplyResources(this.helpToolStripMenuItem, "helpToolStripMenuItem");
            // 
            // viewHelpToolStripMenuItem
            // 
            this.viewHelpToolStripMenuItem.Image = global::HaRepacker.Properties.Resources.help;
            this.viewHelpToolStripMenuItem.Name = "viewHelpToolStripMenuItem";
            resources.ApplyResources(this.viewHelpToolStripMenuItem, "viewHelpToolStripMenuItem");
            this.viewHelpToolStripMenuItem.Click += new System.EventHandler(this.viewHelpToolStripMenuItem_Click);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Image = global::HaRepacker.Properties.Resources.information;
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            resources.ApplyResources(this.aboutToolStripMenuItem, "aboutToolStripMenuItem");
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // debugToolStripMenuItem
            // 
            this.debugToolStripMenuItem.Name = "debugToolStripMenuItem";
            resources.ApplyResources(this.debugToolStripMenuItem, "debugToolStripMenuItem");
            // 
            // AbortButton
            // 
            resources.ApplyResources(this.AbortButton, "AbortButton");
            this.AbortButton.Name = "AbortButton";
            this.AbortButton.UseVisualStyleBackColor = true;
            this.AbortButton.Click += new System.EventHandler(this.AbortButton_Click);
            // 
            // tabControl_MainPanels
            // 
            resources.ApplyResources(this.tabControl_MainPanels, "tabControl_MainPanels");
            this.tabControl_MainPanels.Name = "tabControl_MainPanels";
            this.tabControl_MainPanels.SelectedIndex = 0;
            this.tabControl_MainPanels.SelectedIndexChanged += new System.EventHandler(this.tabControl_MainPanels_TabIndexChanged);
            this.tabControl_MainPanels.KeyUp += new System.Windows.Forms.KeyEventHandler(this.tabControl_MainPanels_KeyUp);
            // 
            // button_addTab
            // 
            resources.ApplyResources(this.button_addTab, "button_addTab");
            this.button_addTab.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button_addTab.Name = "button_addTab";
            this.button_addTab.UseVisualStyleBackColor = true;
            this.button_addTab.Click += new System.EventHandler(this.button_addTab_Click);
            // 
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.button_addTab);
            this.Controls.Add(this.tabControl_MainPanels);
            this.Controls.Add(this.AbortButton);
            this.Controls.Add(this.mainMenu);
            this.MainMenuStrip = this.mainMenu;
            this.Name = "MainForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.SizeChanged += new System.EventHandler(this.MainForm_SizeChanged);
            this.mainMenu.ResumeLayout(false);
            this.mainMenu.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.MenuStrip mainMenu;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportFilesToXMLToolStripMenuItem;
        private System.Windows.Forms.ToolStripComboBox encryptionBox;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewHelpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem xMLToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem rawDataToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem imgToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportDataToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem xMLToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem pNGsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem imgToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzDirectoryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzImageToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem wzByteFloatPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzCanvasPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzCompressedIntPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzConvexPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzDoublePropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzNullPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzSoundPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzStringPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzSubPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzUnsignedShortPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzUolPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzVectorPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem xMLToolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem iMGToolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem privateServerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem classicToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newToolStripMenuItem1;
        private System.Windows.Forms.Button AbortButton;
        private System.Windows.Forms.ToolStripMenuItem extrasToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem animateToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fHMappingToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renderMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem settingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem redoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem expandAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem collapseAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem searchToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem debugToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem unloadAllToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem reloadAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
        private System.Windows.Forms.ToolStripTextBox zoomTextBox;
        private System.Windows.Forms.TabControl tabControl_MainPanels;
        private System.Windows.Forms.Button button_addTab;
        private System.Windows.Forms.ToolStripMenuItem wzLongPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem_searchWzStrings;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem_WzEncryption;
    }
}

