using HaRepacker.GUI;
using HaRepacker.GUI.Input;
using HaRepacker.GUI.Panels;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace HaRepacker
{
    public class ContextMenuManager
    {
        private MainPanel parentPanel;

        private ToolStripMenuItem SaveFile;
        private ToolStripMenuItem SaveImg;
        private ToolStripMenuItem CreateNewImgFile;
        private ToolStripMenuItem DeleteImgFile;
        private ToolStripMenuItem Remove;
        private ToolStripMenuItem Unload;
        private ToolStripMenuItem Reload;
        private ToolStripMenuItem CollapseAllChildNode;
        private ToolStripMenuItem ExpandAllChildNode;
        private ToolStripMenuItem SortAllChildViewNode, SortAllChildViewNode2;
        private ToolStripMenuItem SortPropertiesByName;

        private ToolStripMenuItem AddPropsSubMenu;
        private ToolStripMenuItem AddDirsSubMenu;
        private ToolStripMenuItem AddBatchMenu;
        private ToolStripMenuItem AddSortMenu;
        private ToolStripMenuItem AddSortMenu_WithoutPropSort;
        private ToolStripMenuItem AddImage;
        private ToolStripMenuItem AddDirectory;
        private ToolStripMenuItem AddByteFloat;
        private ToolStripMenuItem AddCanvas;
        private ToolStripMenuItem AddLong;
        private ToolStripMenuItem AddInt;
        private ToolStripMenuItem AddConvex;
        private ToolStripMenuItem AddDouble;
        private ToolStripMenuItem AddNull;
        private ToolStripMenuItem AddSound;
        private ToolStripMenuItem AddString;
        private ToolStripMenuItem AddSub;
        private ToolStripMenuItem AddUshort;
        private ToolStripMenuItem AddUOL;
        private ToolStripMenuItem AddVector;
        private ToolStripMenuItem Rename;
        private ToolStripMenuItem Animate;
        private ToolStripMenuItem SaveAnimation;
        private ToolStripMenuItem FixInlink, AiUpscaleImage, AiUpscaleImageSubMenu_QualityOnly, AiUpscaleImageSubMenu_1_5x, AiUpscaleImageSubMenu_2x, AiUpscaleImageSubMenu_4x;

        /*private ToolStripMenuItem ExportPropertySubMenu;
        private ToolStripMenuItem ExportAnimationSubMenu;
        private ToolStripMenuItem ExportDirectorySubMenu;
        private ToolStripMenuItem ExportPServerXML;
        private ToolStripMenuItem ExportDataXML;
        private ToolStripMenuItem ExportImgData;
        private ToolStripMenuItem ExportRawData;
        private ToolStripMenuItem ExportGIF;
        private ToolStripMenuItem ExportAPNG;

        private ToolStripMenuItem ImportSubMenu;
        private ToolStripMenuItem ImportXML;
        private ToolStripMenuItem ImportImgData;*/

        public ContextMenuManager(MainPanel haRepackerMainPanel, UndoRedoManager undoMan)
        {
            this.parentPanel = haRepackerMainPanel;

            SaveFile = new ToolStripMenuItem("Save", Properties.Resources.disk, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    foreach (WzNode node in GetNodes(sender))
                    {
                        new SaveForm(parentPanel, node).ShowDialog();
                    }
                }));
            SaveImg = new ToolStripMenuItem("Save to IMG", Properties.Resources.disk, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    foreach (WzNode node in GetNodes(sender))
                    {
                        SaveImgNode(node);
                    }
                }));
            CreateNewImgFile = new ToolStripMenuItem("Create New IMG File", Properties.Resources.add, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }
                    CreateNewImgFileInDirectory(nodes[0]);
                }));
            DeleteImgFile = new ToolStripMenuItem("Delete IMG File", Properties.Resources.delete, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }
                    DeleteImgFileFromDirectory(nodes[0]);
                }));
            Rename = new ToolStripMenuItem("Rename", Properties.Resources.rename, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode currentNode = currNode;

                    haRepackerMainPanel.PromptRenameWzTreeNode(currentNode);
                }));
            Remove = new ToolStripMenuItem("Remove", Properties.Resources.delete, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    haRepackerMainPanel.PromptRemoveSelectedTreeNodes();
                }));

            Unload = new ToolStripMenuItem("Unload", Properties.Resources.delete, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    if (!Warning.Warn("Are you sure you want to unload this?"))
                        return;

                    var nodesSelected = GetNodes(sender);
                    foreach (WzNode node in nodesSelected)
                    {
                        if (node.Tag is VirtualWzDirectory virtualDir)
                        {
                            // For VirtualWzDirectory, just remove from tree and dispose
                            virtualDir.Dispose();
                            node.Remove();
                        }
                        else if (node.Tag is WzFile)
                        {
                            parentPanel.MainForm.UnloadWzFile(node.Tag as WzFile);
                        }
                        else if (node.Tag is WzImage)
                        {
                            parentPanel.MainForm.UnloadWzImageFile(node.Tag as WzImage);
                        }
                    }
                }));
            Reload = new ToolStripMenuItem("Reload", Properties.Resources.arrow_refresh, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    if (!Warning.Warn("Are you sure you want to reload this file?"))
                        return;

                    var nodesSelected = GetNodes(sender);
                    foreach (WzNode node in nodesSelected) // selected nodes
                    {
                        parentPanel.MainForm.ReloadWzFile(node.Tag as WzFile);
                    }
                }));
            CollapseAllChildNode = new ToolStripMenuItem("Collapse All", Properties.Resources.collapse, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    foreach (WzNode node in GetNodes(sender))
                    {
                        node.Collapse();
                    }
                }));
            ExpandAllChildNode = new ToolStripMenuItem("Expand all", Properties.Resources.expand, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    foreach (WzNode node in GetNodes(sender))
                    {
                        node.ExpandAll();
                    }
                }));

            // This only sorts the view, does not affect the actual order of the 
            // wz properties
            SortAllChildViewNode = new ToolStripMenuItem("Sort child nodes view", null, new EventHandler( // SortAllChildViewNode cant be in 2 place at once, gotta make copies
                delegate (object sender, EventArgs e)
                {
                    foreach (WzNode node in GetNodes(sender)) {
                        parentPanel.MainForm.SortNodesRecursively(node, true);
                    }
                }));
            SortAllChildViewNode2 = new ToolStripMenuItem("Sort child nodes view", null, new EventHandler( // SortAllChildViewNode cant be in 2 place at once, gotta make copies
                delegate (object sender, EventArgs e) {
                    foreach (WzNode node in GetNodes(sender)) {
                        parentPanel.MainForm.SortNodesRecursively(node, true);
                    }
                }));
            SortPropertiesByName = new ToolStripMenuItem("Sort properties by name", null, new EventHandler(
                delegate (object sender, EventArgs e) {
                    foreach (WzNode node in GetNodes(sender)) {
                        parentPanel.MainForm.SortNodeProperties(node);
                    }
                }));

            AddImage = new ToolStripMenuItem("Image", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name;
                    if (NameInputBox.Show("Add Image", 0, out name))
                        nodes[0].AddObject(new WzImage(name) { Changed = true }, undoMan);
                }));
            AddDirectory = new ToolStripMenuItem("Directory", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }
                    haRepackerMainPanel.AddWzDirectoryToSelectedNode(nodes[0]);

                }));
            AddByteFloat = new ToolStripMenuItem("Float", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    haRepackerMainPanel.AddWzByteFloatToSelectedNode(nodes[0]);
                }));
            AddCanvas = new ToolStripMenuItem("Canvas", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    haRepackerMainPanel.AddWzCanvasToSelectedNode(nodes[0]);
                }));
            AddLong = new ToolStripMenuItem("Long", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }
                    haRepackerMainPanel.AddWzLongToSelectedNode(nodes[0]);
                }));
            AddInt = new ToolStripMenuItem("Int", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }
                    haRepackerMainPanel.AddWzCompressedIntToSelectedNode(nodes[0]);

                }));
            AddConvex = new ToolStripMenuItem("Convex", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    haRepackerMainPanel.AddWzConvexPropertyToSelectedNode(nodes[0]);
                }));
            AddDouble = new ToolStripMenuItem("Double", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }
                    haRepackerMainPanel.AddWzDoublePropertyToSelectedNode(nodes[0]);
                }));
            AddNull = new ToolStripMenuItem("Null", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    haRepackerMainPanel.AddWzNullPropertyToSelectedNode(nodes[0]);
                }));
            AddSound = new ToolStripMenuItem("Sound", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    haRepackerMainPanel.AddWzSoundPropertyToSelectedNode(nodes[0]);
                }));
            AddString = new ToolStripMenuItem("String", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    haRepackerMainPanel.AddWzStringPropertyToSelectedIndex(nodes[0]);
                }));
            AddSub = new ToolStripMenuItem("Sub", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    haRepackerMainPanel.AddWzSubPropertyToSelectedIndex(nodes[0]);
                }));
            AddUshort = new ToolStripMenuItem("Short", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    haRepackerMainPanel.AddWzUnsignedShortPropertyToSelectedIndex(nodes[0]);

                }));
            AddUOL = new ToolStripMenuItem("UOL", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }
                    haRepackerMainPanel.AddWzUOLPropertyToSelectedIndex(nodes[0]);
                }));
            AddVector = new ToolStripMenuItem("Vector", null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1)
                    {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }
                    haRepackerMainPanel.AddWzVectorPropertyToSelectedIndex(nodes[0]);
                }));
            Animate = new ToolStripMenuItem(Properties.Resources.MainPanel_Animate, Properties.Resources.animate, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    haRepackerMainPanel.StartAnimateSelectedCanvas(); 
                }));
            SaveAnimation = new ToolStripMenuItem(Properties.Resources.MainPanel_SaveAnimate, Properties.Resources.animate_save, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    haRepackerMainPanel.SaveImageAnimation_Click();
                }));

            FixInlink = new ToolStripMenuItem(Properties.Resources.MainContextMenu_Batch_EditInlink, null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    haRepackerMainPanel.FixLinkForOldMapleStory_OnClick();
                }));

            // Batch edit
            AiUpscaleImageSubMenu_QualityOnly = new ToolStripMenuItem(Properties.Resources.MainContextMenu_Batch_AIUpscaleImage_QualityOnly, null, new EventHandler(
                delegate (object sender, EventArgs e) {
                    haRepackerMainPanel.AiBatchImageUpscaleEdit(0.25f);
                }));
            AiUpscaleImageSubMenu_1_5x = new ToolStripMenuItem("1.5x", null, new EventHandler(
                delegate (object sender, EventArgs e) {
                    haRepackerMainPanel.AiBatchImageUpscaleEdit(0.375f);
                }));
            AiUpscaleImageSubMenu_2x = new ToolStripMenuItem("2x", null, new EventHandler(
                delegate (object sender, EventArgs e) {
                    haRepackerMainPanel.AiBatchImageUpscaleEdit(0.5f);
                }));
            AiUpscaleImageSubMenu_4x = new ToolStripMenuItem("4x", null, new EventHandler(
                delegate (object sender, EventArgs e) {
                    haRepackerMainPanel.AiBatchImageUpscaleEdit(1f);
            }));
            AiUpscaleImage = new ToolStripMenuItem(Properties.Resources.MainContextMenu_Batch_AIUpscaleImage, null,
                AiUpscaleImageSubMenu_QualityOnly, AiUpscaleImageSubMenu_1_5x, AiUpscaleImageSubMenu_2x, AiUpscaleImageSubMenu_4x
            );


            // Menu
            AddDirsSubMenu = new ToolStripMenuItem("Add", Properties.Resources.add, 
                AddDirectory, AddImage);

            AddPropsSubMenu = new ToolStripMenuItem("Add", Properties.Resources.add, 
                AddCanvas, AddConvex, AddDouble, AddByteFloat, AddLong, AddInt, AddNull, AddUshort, AddSound, AddString, AddSub, AddUOL, AddVector);

            AddBatchMenu = new ToolStripMenuItem(Properties.Resources.MainContextMenu_Batch, Properties.Resources.batch_edit, 
                FixInlink, AiUpscaleImage);

            AddSortMenu = new ToolStripMenuItem("Sort", Properties.Resources.sort, SortAllChildViewNode, SortPropertiesByName);

            Debug.WriteLine(AddSortMenu.DropDown.Items.Count.ToString());
            AddSortMenu_WithoutPropSort = new ToolStripMenuItem("Sort", Properties.Resources.sort, SortAllChildViewNode2);
        }

        /// <summary>
        /// Toolstrip menu when right clicking on nodes
        /// </summary>
        /// <param name="node"></param>
        /// <param name="Tag"></param>
        /// <returns></returns>
        public ContextMenuStrip CreateMenu(WzNode node, WzObject Tag)
        {
            int currentDataTreeSelectedCount = parentPanel.DataTree.SelectedNodes.Count;

            List<ToolStripItem> toolStripmenuItems = new List<ToolStripItem>();

            ContextMenuStrip menu = new ContextMenuStrip();
            if (Tag is WzImage || Tag is IPropertyContainer)
            {
                toolStripmenuItems.Add(AddPropsSubMenu);
                toolStripmenuItems.Add(Rename);
                // Add SaveImg and DeleteImgFile options if from VirtualWzDirectory
                if (IsFromVirtualWzDirectory(Tag))
                {
                    toolStripmenuItems.Add(SaveImg);
                    if (Tag is WzImage)
                    {
                        toolStripmenuItems.Add(DeleteImgFile);
                    }
                }
                else
                {
                    // export, import
                    toolStripmenuItems.Add(Remove);
                }
            }
            else if (Tag is WzImageProperty)
            {
                toolStripmenuItems.Add(Rename);
                // Add SaveImg option if from VirtualWzDirectory
                if (IsFromVirtualWzDirectory(Tag))
                {
                    toolStripmenuItems.Add(SaveImg);
                }
                toolStripmenuItems.Add(Remove);
            }
            else if (Tag is VirtualWzDirectory)
            {
                toolStripmenuItems.Add(CreateNewImgFile);
                toolStripmenuItems.Add(AddDirsSubMenu);
                toolStripmenuItems.Add(Rename);
                toolStripmenuItems.Add(SaveImg);
                toolStripmenuItems.Add(Unload);
            }
            else if (Tag is WzDirectory)
            {
                toolStripmenuItems.Add(AddDirsSubMenu);
                toolStripmenuItems.Add(Rename);
                toolStripmenuItems.Add(Remove);
            }
            else if (Tag is WzFile)
            {
                toolStripmenuItems.Add(AddDirsSubMenu);
                toolStripmenuItems.Add(Rename);
                toolStripmenuItems.Add(SaveFile);
                toolStripmenuItems.Add(Unload);
                toolStripmenuItems.Add(Reload);
            }

            toolStripmenuItems.Add(ExpandAllChildNode);
            toolStripmenuItems.Add(CollapseAllChildNode);

            toolStripmenuItems.Add(AddBatchMenu);

            if (Tag is WzCanvasProperty)
            {
                toolStripmenuItems.Add(Animate);
            }

            if (Tag.GetType() == typeof(WzSubProperty)) {
                toolStripmenuItems.Add(SaveAnimation);
                toolStripmenuItems.Add(AddSortMenu);
            } else {
                toolStripmenuItems.Add(AddSortMenu_WithoutPropSort);
            }

            // Add
            foreach (ToolStripItem toolStripItem in toolStripmenuItems)
            {
                menu.Items.Add(toolStripItem);
            }

            currNode = node;
            return menu;
        }

        private WzNode currNode = null;

        private WzNode[] GetNodes(object sender)
        {
            return new WzNode[] { currNode };
        }

        /// <summary>
        /// Saves a node from a VirtualWzDirectory to the IMG filesystem
        /// </summary>
        private void SaveImgNode(WzNode node)
        {
            WzObject tag = (WzObject)node.Tag;

            // Find the parent VirtualWzDirectory
            WzObject current = tag;
            VirtualWzDirectory virtualDir = null;

            while (current != null)
            {
                if (current.Parent is VirtualWzDirectory vDir)
                {
                    virtualDir = vDir;
                    break;
                }
                current = current.Parent;
            }

            if (virtualDir == null)
            {
                // Check if tag itself is the VirtualWzDirectory
                if (tag is VirtualWzDirectory vd)
                {
                    virtualDir = vd;
                }
            }

            if (virtualDir == null)
            {
                MessageBox.Show("This item is not from an IMG filesystem directory.",
                    "Cannot Save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (tag is WzImage image)
                {
                    // Save single image
                    if (virtualDir.SaveImage(image))
                    {
                        MessageBox.Show($"Saved {image.Name} successfully.",
                            "Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        node.ForeColor = System.Drawing.Color.Black; // Reset color
                    }
                    else
                    {
                        MessageBox.Show($"Failed to save {image.Name}.",
                            "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else if (tag is VirtualWzDirectory vDir)
                {
                    // Save all changed images in directory
                    int savedCount = vDir.SaveAllChangedImages();
                    if (savedCount > 0)
                    {
                        MessageBox.Show($"Saved {savedCount} changed image(s) successfully.",
                            "Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("No changed images to save.",
                            "Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else if (tag is WzImageProperty prop)
                {
                    // Save the parent image
                    if (prop.ParentImage != null)
                    {
                        if (virtualDir.SaveImage(prop.ParentImage))
                        {
                            MessageBox.Show($"Saved {prop.ParentImage.Name} successfully.",
                                "Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show($"Failed to save {prop.ParentImage.Name}.",
                                "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}",
                    "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Checks if a WzObject is from a VirtualWzDirectory
        /// </summary>
        private bool IsFromVirtualWzDirectory(WzObject obj)
        {
            if (obj is VirtualWzDirectory)
                return true;

            WzObject current = obj;
            while (current != null)
            {
                if (current.Parent is VirtualWzDirectory)
                    return true;
                current = current.Parent;
            }
            return false;
        }

        /// <summary>
        /// Creates a new IMG file in a VirtualWzDirectory
        /// </summary>
        private void CreateNewImgFileInDirectory(WzNode node)
        {
            WzObject tag = (WzObject)node.Tag;

            VirtualWzDirectory virtualDir = tag as VirtualWzDirectory;
            if (virtualDir == null)
            {
                MessageBox.Show("Please select a directory from an IMG filesystem.",
                    "Cannot Create File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Prompt for new file name
            string name;
            if (!NameInputBox.Show("Create New IMG File", 0, out name))
                return;

            // Ensure .img extension
            if (!name.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                name += ".img";

            // Check if file already exists
            if (virtualDir.ImageExists(name))
            {
                MessageBox.Show($"A file named '{name}' already exists in this directory.",
                    "File Exists", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Create the new IMG file
                string relativePath = name;
                if (!string.IsNullOrEmpty(virtualDir.RelativePath))
                {
                    relativePath = Path.Combine(virtualDir.RelativePath, name);
                }

                WzImage newImage = virtualDir.Manager.CreateImage(virtualDir.CategoryName, relativePath);
                if (newImage != null)
                {
                    // Add to tree
                    WzNode newNode = new WzNode(newImage, true);
                    node.Nodes.Add(newNode);
                    newNode.EnsureVisible();

                    MessageBox.Show($"Created '{name}' successfully.",
                        "File Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Failed to create '{name}'.",
                        "Creation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating file: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Deletes an IMG file from the filesystem
        /// </summary>
        private void DeleteImgFileFromDirectory(WzNode node)
        {
            WzObject tag = (WzObject)node.Tag;

            if (tag is not WzImage image)
            {
                MessageBox.Show("Please select an IMG file to delete.",
                    "Cannot Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Find parent VirtualWzDirectory
            VirtualWzDirectory virtualDir = null;
            WzObject current = tag;
            while (current != null)
            {
                if (current.Parent is VirtualWzDirectory vDir)
                {
                    virtualDir = vDir;
                    break;
                }
                current = current.Parent;
            }

            if (virtualDir == null)
            {
                MessageBox.Show("This file is not from an IMG filesystem directory.",
                    "Cannot Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Confirm deletion
            DialogResult result = MessageBox.Show(
                $"Are you sure you want to delete '{image.Name}'?\n\n" +
                "This will permanently delete the file from disk.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            try
            {
                // Build relative path
                string relativePath = image.Name;
                if (!string.IsNullOrEmpty(virtualDir.RelativePath))
                {
                    relativePath = Path.Combine(virtualDir.RelativePath, image.Name);
                }

                if (virtualDir.Manager.DeleteImage(virtualDir.CategoryName, relativePath))
                {
                    // Remove from tree
                    node.Remove();

                    MessageBox.Show($"Deleted '{image.Name}' successfully.",
                        "File Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Failed to delete '{image.Name}'.",
                        "Deletion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting file: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}