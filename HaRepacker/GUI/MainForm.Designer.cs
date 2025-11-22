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
            mainMenu = new System.Windows.Forms.MenuStrip();
            fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            newToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItem_newWzFormat = new System.Windows.Forms.ToolStripMenuItem();
            saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            reloadAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            unloadAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            addToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzImageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzSubPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzUolPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            wzCanvasPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            wzStringPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzByteFloatPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzLongPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzDoublePropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzCompressedIntPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzUnsignedShortPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            wzConvexPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzNullPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzSoundPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzVectorPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            wzLuaPropertyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            removeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            undoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            redoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            expandAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            collapseAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportFilesToXMLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            xMLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            rawDataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            imgToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            nXFormatToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exportDataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            xMLToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            privateServerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            classicToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            newToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            jSONToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            bSONToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            pNGsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            imgToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            importToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            xMLToolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            iMGToolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator9 = new System.Windows.Forms.ToolStripSeparator();
            optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator8 = new System.Windows.Forms.ToolStripSeparator();
            searchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            fHMappingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            renderMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            zoomTextBox = new System.Windows.Forms.ToolStripTextBox();
            toolStripMenuItem_searchWzStrings = new System.Windows.Forms.ToolStripMenuItem();
            helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            viewHelpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            encryptionBox = new System.Windows.Forms.ToolStripComboBox();
            AbortButton = new System.Windows.Forms.Button();
            tabControl_MainPanels = new System.Windows.Forms.TabControl();
            button_addTab = new System.Windows.Forms.Button();
            toolsToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            mainMenu.SuspendLayout();
            SuspendLayout();
            // 
            // mainMenu
            // 
            mainMenu.ImageScalingSize = new System.Drawing.Size(24, 24);
            mainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, toolsToolStripMenuItem, helpToolStripMenuItem, encryptionBox });
            resources.ApplyResources(mainMenu, "mainMenu");
            mainMenu.Name = "mainMenu";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { newToolStripMenuItem, openToolStripMenuItem, toolStripMenuItem_newWzFormat, saveToolStripMenuItem, toolStripSeparator5, copyToolStripMenuItem, pasteToolStripMenuItem, toolStripSeparator4, reloadAllToolStripMenuItem, unloadAllToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            resources.ApplyResources(fileToolStripMenuItem, "fileToolStripMenuItem");
            // 
            // newToolStripMenuItem
            // 
            newToolStripMenuItem.Image = Properties.Resources.page_white;
            newToolStripMenuItem.Name = "newToolStripMenuItem";
            resources.ApplyResources(newToolStripMenuItem, "newToolStripMenuItem");
            newToolStripMenuItem.Click += newToolStripMenuItem_Click;
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.Image = Properties.Resources.folder;
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            resources.ApplyResources(openToolStripMenuItem, "openToolStripMenuItem");
            openToolStripMenuItem.Click += openToolStripMenuItem_Click;
            // 
            // toolStripMenuItem_newWzFormat
            // 
            toolStripMenuItem_newWzFormat.Image = Properties.Resources.folder;
            toolStripMenuItem_newWzFormat.Name = "toolStripMenuItem_newWzFormat";
            resources.ApplyResources(toolStripMenuItem_newWzFormat, "toolStripMenuItem_newWzFormat");
            toolStripMenuItem_newWzFormat.Click += toolStripMenuItem_newWzFormat_Click;
            // 
            // saveToolStripMenuItem
            // 
            resources.ApplyResources(saveToolStripMenuItem, "saveToolStripMenuItem");
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.Click += SaveToolStripMenuItem_Click;
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            resources.ApplyResources(toolStripSeparator5, "toolStripSeparator5");
            // 
            // copyToolStripMenuItem
            // 
            copyToolStripMenuItem.Image = Properties.Resources.copyFile;
            copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            resources.ApplyResources(copyToolStripMenuItem, "copyToolStripMenuItem");
            // 
            // pasteToolStripMenuItem
            // 
            pasteToolStripMenuItem.Image = Properties.Resources.pasteFile;
            pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            resources.ApplyResources(pasteToolStripMenuItem, "pasteToolStripMenuItem");
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            resources.ApplyResources(toolStripSeparator4, "toolStripSeparator4");
            // 
            // reloadAllToolStripMenuItem
            // 
            resources.ApplyResources(reloadAllToolStripMenuItem, "reloadAllToolStripMenuItem");
            reloadAllToolStripMenuItem.Name = "reloadAllToolStripMenuItem";
            reloadAllToolStripMenuItem.Click += reloadAllToolStripMenuItem_Click;
            // 
            // unloadAllToolStripMenuItem
            // 
            resources.ApplyResources(unloadAllToolStripMenuItem, "unloadAllToolStripMenuItem");
            unloadAllToolStripMenuItem.Name = "unloadAllToolStripMenuItem";
            unloadAllToolStripMenuItem.Click += unloadAllToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { addToolStripMenuItem, removeToolStripMenuItem, toolStripSeparator7, undoToolStripMenuItem, redoToolStripMenuItem, toolStripSeparator6, expandAllToolStripMenuItem, collapseAllToolStripMenuItem });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            resources.ApplyResources(editToolStripMenuItem, "editToolStripMenuItem");
            // 
            // addToolStripMenuItem
            // 
            addToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { wzDirectoryToolStripMenuItem, wzImageToolStripMenuItem, wzSubPropertyToolStripMenuItem, wzUolPropertyToolStripMenuItem, toolStripSeparator2, wzCanvasPropertyToolStripMenuItem, toolStripSeparator1, wzStringPropertyToolStripMenuItem, wzByteFloatPropertyToolStripMenuItem, wzLongPropertyToolStripMenuItem, wzDoublePropertyToolStripMenuItem, wzCompressedIntPropertyToolStripMenuItem, wzUnsignedShortPropertyToolStripMenuItem, toolStripSeparator3, wzConvexPropertyToolStripMenuItem, wzNullPropertyToolStripMenuItem, wzSoundPropertyToolStripMenuItem, wzVectorPropertyToolStripMenuItem, wzLuaPropertyToolStripMenuItem });
            resources.ApplyResources(addToolStripMenuItem, "addToolStripMenuItem");
            addToolStripMenuItem.Name = "addToolStripMenuItem";
            // 
            // wzDirectoryToolStripMenuItem
            // 
            wzDirectoryToolStripMenuItem.Name = "wzDirectoryToolStripMenuItem";
            resources.ApplyResources(wzDirectoryToolStripMenuItem, "wzDirectoryToolStripMenuItem");
            wzDirectoryToolStripMenuItem.Click += WzDirectoryToolStripMenuItem_Click;
            // 
            // wzImageToolStripMenuItem
            // 
            wzImageToolStripMenuItem.Name = "wzImageToolStripMenuItem";
            resources.ApplyResources(wzImageToolStripMenuItem, "wzImageToolStripMenuItem");
            wzImageToolStripMenuItem.Click += WzImageToolStripMenuItem_Click;
            // 
            // wzSubPropertyToolStripMenuItem
            // 
            wzSubPropertyToolStripMenuItem.Name = "wzSubPropertyToolStripMenuItem";
            resources.ApplyResources(wzSubPropertyToolStripMenuItem, "wzSubPropertyToolStripMenuItem");
            wzSubPropertyToolStripMenuItem.Click += WzSubPropertyToolStripMenuItem_Click;
            // 
            // wzUolPropertyToolStripMenuItem
            // 
            wzUolPropertyToolStripMenuItem.Name = "wzUolPropertyToolStripMenuItem";
            resources.ApplyResources(wzUolPropertyToolStripMenuItem, "wzUolPropertyToolStripMenuItem");
            wzUolPropertyToolStripMenuItem.Click += WzUolPropertyToolStripMenuItem_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            resources.ApplyResources(toolStripSeparator2, "toolStripSeparator2");
            // 
            // wzCanvasPropertyToolStripMenuItem
            // 
            wzCanvasPropertyToolStripMenuItem.Name = "wzCanvasPropertyToolStripMenuItem";
            resources.ApplyResources(wzCanvasPropertyToolStripMenuItem, "wzCanvasPropertyToolStripMenuItem");
            wzCanvasPropertyToolStripMenuItem.Click += WzCanvasPropertyToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(toolStripSeparator1, "toolStripSeparator1");
            // 
            // wzStringPropertyToolStripMenuItem
            // 
            wzStringPropertyToolStripMenuItem.Name = "wzStringPropertyToolStripMenuItem";
            resources.ApplyResources(wzStringPropertyToolStripMenuItem, "wzStringPropertyToolStripMenuItem");
            wzStringPropertyToolStripMenuItem.Click += WzStringPropertyToolStripMenuItem_Click;
            // 
            // wzByteFloatPropertyToolStripMenuItem
            // 
            wzByteFloatPropertyToolStripMenuItem.Name = "wzByteFloatPropertyToolStripMenuItem";
            resources.ApplyResources(wzByteFloatPropertyToolStripMenuItem, "wzByteFloatPropertyToolStripMenuItem");
            wzByteFloatPropertyToolStripMenuItem.Click += WzByteFloatPropertyToolStripMenuItem_Click;
            // 
            // wzLongPropertyToolStripMenuItem
            // 
            wzLongPropertyToolStripMenuItem.Name = "wzLongPropertyToolStripMenuItem";
            resources.ApplyResources(wzLongPropertyToolStripMenuItem, "wzLongPropertyToolStripMenuItem");
            wzLongPropertyToolStripMenuItem.Click += WzLongPropertyToolStripMenuItem_Click;
            // 
            // wzDoublePropertyToolStripMenuItem
            // 
            wzDoublePropertyToolStripMenuItem.Name = "wzDoublePropertyToolStripMenuItem";
            resources.ApplyResources(wzDoublePropertyToolStripMenuItem, "wzDoublePropertyToolStripMenuItem");
            wzDoublePropertyToolStripMenuItem.Click += WzDoublePropertyToolStripMenuItem_Click;
            // 
            // wzCompressedIntPropertyToolStripMenuItem
            // 
            wzCompressedIntPropertyToolStripMenuItem.Name = "wzCompressedIntPropertyToolStripMenuItem";
            resources.ApplyResources(wzCompressedIntPropertyToolStripMenuItem, "wzCompressedIntPropertyToolStripMenuItem");
            wzCompressedIntPropertyToolStripMenuItem.Click += WzCompressedIntPropertyToolStripMenuItem_Click;
            // 
            // wzUnsignedShortPropertyToolStripMenuItem
            // 
            wzUnsignedShortPropertyToolStripMenuItem.Name = "wzUnsignedShortPropertyToolStripMenuItem";
            resources.ApplyResources(wzUnsignedShortPropertyToolStripMenuItem, "wzUnsignedShortPropertyToolStripMenuItem");
            wzUnsignedShortPropertyToolStripMenuItem.Click += WzUnsignedShortPropertyToolStripMenuItem_Click;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            resources.ApplyResources(toolStripSeparator3, "toolStripSeparator3");
            // 
            // wzConvexPropertyToolStripMenuItem
            // 
            wzConvexPropertyToolStripMenuItem.Name = "wzConvexPropertyToolStripMenuItem";
            resources.ApplyResources(wzConvexPropertyToolStripMenuItem, "wzConvexPropertyToolStripMenuItem");
            wzConvexPropertyToolStripMenuItem.Click += WzConvexPropertyToolStripMenuItem_Click;
            // 
            // wzNullPropertyToolStripMenuItem
            // 
            wzNullPropertyToolStripMenuItem.Name = "wzNullPropertyToolStripMenuItem";
            resources.ApplyResources(wzNullPropertyToolStripMenuItem, "wzNullPropertyToolStripMenuItem");
            wzNullPropertyToolStripMenuItem.Click += WzNullPropertyToolStripMenuItem_Click;
            // 
            // wzSoundPropertyToolStripMenuItem
            // 
            wzSoundPropertyToolStripMenuItem.Name = "wzSoundPropertyToolStripMenuItem";
            resources.ApplyResources(wzSoundPropertyToolStripMenuItem, "wzSoundPropertyToolStripMenuItem");
            wzSoundPropertyToolStripMenuItem.Click += WzSoundPropertyToolStripMenuItem_Click;
            // 
            // wzVectorPropertyToolStripMenuItem
            // 
            wzVectorPropertyToolStripMenuItem.Name = "wzVectorPropertyToolStripMenuItem";
            resources.ApplyResources(wzVectorPropertyToolStripMenuItem, "wzVectorPropertyToolStripMenuItem");
            wzVectorPropertyToolStripMenuItem.Click += WzVectorPropertyToolStripMenuItem_Click;
            // 
            // wzLuaPropertyToolStripMenuItem
            // 
            wzLuaPropertyToolStripMenuItem.Name = "wzLuaPropertyToolStripMenuItem";
            resources.ApplyResources(wzLuaPropertyToolStripMenuItem, "wzLuaPropertyToolStripMenuItem");
            wzLuaPropertyToolStripMenuItem.Click += wzLuaPropertyToolStripMenuItem_Click;
            // 
            // removeToolStripMenuItem
            // 
            resources.ApplyResources(removeToolStripMenuItem, "removeToolStripMenuItem");
            removeToolStripMenuItem.Name = "removeToolStripMenuItem";
            removeToolStripMenuItem.Click += RemoveToolStripMenuItem_Click;
            // 
            // toolStripSeparator7
            // 
            toolStripSeparator7.Name = "toolStripSeparator7";
            resources.ApplyResources(toolStripSeparator7, "toolStripSeparator7");
            // 
            // undoToolStripMenuItem
            // 
            undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            resources.ApplyResources(undoToolStripMenuItem, "undoToolStripMenuItem");
            // 
            // redoToolStripMenuItem
            // 
            redoToolStripMenuItem.Name = "redoToolStripMenuItem";
            resources.ApplyResources(redoToolStripMenuItem, "redoToolStripMenuItem");
            // 
            // toolStripSeparator6
            // 
            toolStripSeparator6.Name = "toolStripSeparator6";
            resources.ApplyResources(toolStripSeparator6, "toolStripSeparator6");
            // 
            // expandAllToolStripMenuItem
            // 
            expandAllToolStripMenuItem.Name = "expandAllToolStripMenuItem";
            resources.ApplyResources(expandAllToolStripMenuItem, "expandAllToolStripMenuItem");
            expandAllToolStripMenuItem.Click += expandAllToolStripMenuItem_Click;
            // 
            // collapseAllToolStripMenuItem
            // 
            collapseAllToolStripMenuItem.Name = "collapseAllToolStripMenuItem";
            resources.ApplyResources(collapseAllToolStripMenuItem, "collapseAllToolStripMenuItem");
            collapseAllToolStripMenuItem.Click += collapseAllToolStripMenuItem_Click;
            // 
            // exportFilesToXMLToolStripMenuItem
            // 
            exportFilesToXMLToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { xMLToolStripMenuItem, rawDataToolStripMenuItem, imgToolStripMenuItem, nXFormatToolStripMenuItem });
            exportFilesToXMLToolStripMenuItem.Image = Properties.Resources.folder_go;
            exportFilesToXMLToolStripMenuItem.Name = "exportFilesToXMLToolStripMenuItem";
            resources.ApplyResources(exportFilesToXMLToolStripMenuItem, "exportFilesToXMLToolStripMenuItem");
            // 
            // toolsToolStripMenuItem
            // 
            toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { exportFilesToXMLToolStripMenuItem, exportDataToolStripMenuItem, importToolStripMenuItem, toolStripSeparator9, optionsToolStripMenuItem, toolStripSeparator8, searchToolStripMenuItem, fHMappingToolStripMenuItem, toolStripMenuItem_searchWzStrings, toolsToolStripMenuItem1 });
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            resources.ApplyResources(toolsToolStripMenuItem, "toolsToolStripMenuItem");
            // 
            // xMLToolStripMenuItem
            // 
            xMLToolStripMenuItem.Name = "xMLToolStripMenuItem";
            resources.ApplyResources(xMLToolStripMenuItem, "xMLToolStripMenuItem");
            xMLToolStripMenuItem.Click += xMLToolStripMenuItem_Click;
            // 
            // rawDataToolStripMenuItem
            // 
            rawDataToolStripMenuItem.Name = "rawDataToolStripMenuItem";
            resources.ApplyResources(rawDataToolStripMenuItem, "rawDataToolStripMenuItem");
            rawDataToolStripMenuItem.Click += rawDataToolStripMenuItem_Click;
            // 
            // imgToolStripMenuItem
            // 
            imgToolStripMenuItem.Name = "imgToolStripMenuItem";
            resources.ApplyResources(imgToolStripMenuItem, "imgToolStripMenuItem");
            imgToolStripMenuItem.Click += imgToolStripMenuItem_Click;
            // 
            // nXFormatToolStripMenuItem
            // 
            nXFormatToolStripMenuItem.Name = "nXFormatToolStripMenuItem";
            resources.ApplyResources(nXFormatToolStripMenuItem, "nXFormatToolStripMenuItem");
            nXFormatToolStripMenuItem.Click += nXFormatToolStripMenuItem_Click;
            // 
            // exportDataToolStripMenuItem
            // 
            exportDataToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { xMLToolStripMenuItem1, jSONToolStripMenuItem, bSONToolStripMenuItem, pNGsToolStripMenuItem, imgToolStripMenuItem1 });
            exportDataToolStripMenuItem.Image = Properties.Resources.page_go;
            exportDataToolStripMenuItem.Name = "exportDataToolStripMenuItem";
            resources.ApplyResources(exportDataToolStripMenuItem, "exportDataToolStripMenuItem");
            // 
            // xMLToolStripMenuItem1
            // 
            xMLToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { privateServerToolStripMenuItem, classicToolStripMenuItem, newToolStripMenuItem1 });
            xMLToolStripMenuItem1.Name = "xMLToolStripMenuItem1";
            resources.ApplyResources(xMLToolStripMenuItem1, "xMLToolStripMenuItem1");
            // 
            // privateServerToolStripMenuItem
            // 
            privateServerToolStripMenuItem.Name = "privateServerToolStripMenuItem";
            resources.ApplyResources(privateServerToolStripMenuItem, "privateServerToolStripMenuItem");
            privateServerToolStripMenuItem.Click += privateServerToolStripMenuItem_Click;
            // 
            // classicToolStripMenuItem
            // 
            classicToolStripMenuItem.Name = "classicToolStripMenuItem";
            resources.ApplyResources(classicToolStripMenuItem, "classicToolStripMenuItem");
            classicToolStripMenuItem.Click += classicToolStripMenuItem_Click;
            // 
            // newToolStripMenuItem1
            // 
            newToolStripMenuItem1.Name = "newToolStripMenuItem1";
            resources.ApplyResources(newToolStripMenuItem1, "newToolStripMenuItem1");
            newToolStripMenuItem1.Click += newToolStripMenuItem1_Click;
            // 
            // jSONToolStripMenuItem
            // 
            jSONToolStripMenuItem.Name = "jSONToolStripMenuItem";
            resources.ApplyResources(jSONToolStripMenuItem, "jSONToolStripMenuItem");
            jSONToolStripMenuItem.Click += jSONToolStripMenuItem_Click;
            // 
            // bSONToolStripMenuItem
            // 
            bSONToolStripMenuItem.Name = "bSONToolStripMenuItem";
            resources.ApplyResources(bSONToolStripMenuItem, "bSONToolStripMenuItem");
            bSONToolStripMenuItem.Click += bSONToolStripMenuItem_Click;
            // 
            // pNGsToolStripMenuItem
            // 
            pNGsToolStripMenuItem.Name = "pNGsToolStripMenuItem";
            resources.ApplyResources(pNGsToolStripMenuItem, "pNGsToolStripMenuItem");
            pNGsToolStripMenuItem.Click += pNGsToolStripMenuItem_Click;
            // 
            // imgToolStripMenuItem1
            // 
            imgToolStripMenuItem1.Name = "imgToolStripMenuItem1";
            resources.ApplyResources(imgToolStripMenuItem1, "imgToolStripMenuItem1");
            imgToolStripMenuItem1.Click += imgToolStripMenuItem1_Click;
            // 
            // importToolStripMenuItem
            // 
            importToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { xMLToolStripMenuItem2, iMGToolStripMenuItem2 });
            importToolStripMenuItem.Image = Properties.Resources.page_add;
            importToolStripMenuItem.Name = "importToolStripMenuItem";
            resources.ApplyResources(importToolStripMenuItem, "importToolStripMenuItem");
            // 
            // xMLToolStripMenuItem2
            // 
            xMLToolStripMenuItem2.Name = "xMLToolStripMenuItem2";
            resources.ApplyResources(xMLToolStripMenuItem2, "xMLToolStripMenuItem2");
            xMLToolStripMenuItem2.Click += xMLToolStripMenuItem2_Click;
            // 
            // iMGToolStripMenuItem2
            // 
            iMGToolStripMenuItem2.Name = "iMGToolStripMenuItem2";
            resources.ApplyResources(iMGToolStripMenuItem2, "iMGToolStripMenuItem2");
            iMGToolStripMenuItem2.Click += iMGToolStripMenuItem2_Click;
            // 
            // toolStripSeparator9
            // 
            toolStripSeparator9.Name = "toolStripSeparator9";
            resources.ApplyResources(toolStripSeparator9, "toolStripSeparator9");
            // 
            // optionsToolStripMenuItem
            // 
            optionsToolStripMenuItem.Image = Properties.Resources.cog;
            optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            resources.ApplyResources(optionsToolStripMenuItem, "optionsToolStripMenuItem");
            optionsToolStripMenuItem.Click += optionsToolStripMenuItem_Click;
            // 
            // toolStripSeparator8
            // 
            toolStripSeparator8.Name = "toolStripSeparator8";
            resources.ApplyResources(toolStripSeparator8, "toolStripSeparator8");
            // 
            // searchToolStripMenuItem
            // 
            resources.ApplyResources(searchToolStripMenuItem, "searchToolStripMenuItem");
            searchToolStripMenuItem.Name = "searchToolStripMenuItem";
            searchToolStripMenuItem.Click += searchToolStripMenuItem_Click;
            // 
            // fHMappingToolStripMenuItem
            // 
            fHMappingToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { renderMapToolStripMenuItem, settingsToolStripMenuItem, zoomTextBox });
            fHMappingToolStripMenuItem.Name = "fHMappingToolStripMenuItem";
            resources.ApplyResources(fHMappingToolStripMenuItem, "fHMappingToolStripMenuItem");
            // 
            // renderMapToolStripMenuItem
            // 
            renderMapToolStripMenuItem.Image = Properties.Resources.map;
            renderMapToolStripMenuItem.Name = "renderMapToolStripMenuItem";
            resources.ApplyResources(renderMapToolStripMenuItem, "renderMapToolStripMenuItem");
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.Image = Properties.Resources.cog;
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            resources.ApplyResources(settingsToolStripMenuItem, "settingsToolStripMenuItem");
            // 
            // zoomTextBox
            // 
            resources.ApplyResources(zoomTextBox, "zoomTextBox");
            zoomTextBox.Name = "zoomTextBox";
            // 
            // toolStripMenuItem_searchWzStrings
            // 
            toolStripMenuItem_searchWzStrings.Name = "toolStripMenuItem_searchWzStrings";
            resources.ApplyResources(toolStripMenuItem_searchWzStrings, "toolStripMenuItem_searchWzStrings");
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { viewHelpToolStripMenuItem, aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            resources.ApplyResources(helpToolStripMenuItem, "helpToolStripMenuItem");
            // 
            // viewHelpToolStripMenuItem
            // 
            viewHelpToolStripMenuItem.Image = Properties.Resources.help;
            viewHelpToolStripMenuItem.Name = "viewHelpToolStripMenuItem";
            resources.ApplyResources(viewHelpToolStripMenuItem, "viewHelpToolStripMenuItem");
            viewHelpToolStripMenuItem.Click += ViewHelpToolStripMenuItem_Click;
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Image = Properties.Resources.information;
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            resources.ApplyResources(aboutToolStripMenuItem, "aboutToolStripMenuItem");
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // encryptionBox
            // 
            encryptionBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            encryptionBox.DropDownWidth = 400;
            resources.ApplyResources(encryptionBox, "encryptionBox");
            encryptionBox.Name = "encryptionBox";
            encryptionBox.SelectedIndexChanged += EncryptionBox_SelectedIndexChanged;
            // 
            // AbortButton
            // 
            resources.ApplyResources(AbortButton, "AbortButton");
            AbortButton.Name = "AbortButton";
            AbortButton.UseVisualStyleBackColor = true;
            AbortButton.Click += AbortButton_Click;
            // 
            // tabControl_MainPanels
            // 
            resources.ApplyResources(tabControl_MainPanels, "tabControl_MainPanels");
            tabControl_MainPanels.Name = "tabControl_MainPanels";
            tabControl_MainPanels.SelectedIndex = 0;
            tabControl_MainPanels.SelectedIndexChanged += tabControl_MainPanels_TabIndexChanged;
            tabControl_MainPanels.KeyUp += tabControl_MainPanels_KeyUp;
            // 
            // button_addTab
            // 
            resources.ApplyResources(button_addTab, "button_addTab");
            button_addTab.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            button_addTab.Name = "button_addTab";
            button_addTab.UseVisualStyleBackColor = true;
            button_addTab.Click += Button_addTab_Click;
            // 
            // toolsToolStripMenuItem1
            // 
            toolsToolStripMenuItem1.Name = "toolsToolStripMenuItem1";
            resources.ApplyResources(toolsToolStripMenuItem1, "toolsToolStripMenuItem1");
            // 
            // MainForm
            // 
            AllowDrop = true;
            resources.ApplyResources(this, "$this");
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(button_addTab);
            Controls.Add(tabControl_MainPanels);
            Controls.Add(AbortButton);
            Controls.Add(mainMenu);
            MainMenuStrip = mainMenu;
            Name = "MainForm";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            SizeChanged += MainForm_SizeChanged;
            mainMenu.ResumeLayout(false);
            mainMenu.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

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
        private System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem redoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem expandAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem collapseAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem searchToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem unloadAllToolStripMenuItem;
        public System.Windows.Forms.ToolStripMenuItem reloadAllToolStripMenuItem;
        private System.Windows.Forms.TabControl tabControl_MainPanels;
        private System.Windows.Forms.Button button_addTab;
        private System.Windows.Forms.ToolStripMenuItem wzLongPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem jSONToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bSONToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem wzLuaPropertyToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator9;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator8;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripMenuItem nXFormatToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem_newWzFormat;
        private System.Windows.Forms.ToolStripMenuItem fHMappingToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renderMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem settingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripTextBox zoomTextBox;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem_searchWzStrings;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem1;
    }
}

