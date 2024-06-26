/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections;
using HaRepacker.GUI.Interaction;
using HaRepacker.GUI;
using HaRepacker.GUI.Panels;
using HaRepacker.GUI.Input;
using System.Diagnostics;

namespace HaRepacker
{
    public class ContextMenuManager
    {
        private MainPanel parentPanel;

        private ToolStripMenuItem SaveFile;
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
        private ToolStripMenuItem FixInlink, AiUpscaleImage;

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
                    if (!Warning.Warn("Are you sure you want to unload this file?"))
                        return;

                    var nodesSelected = GetNodes(sender);
                    foreach (WzNode node in nodesSelected)
                    {
                        parentPanel.MainForm.UnloadWzFile(node.Tag as WzFile);
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

            FixInlink = new ToolStripMenuItem(Properties.Resources.MainContextMenu_Batch_EditInlink, null, new EventHandler(
                delegate (object sender, EventArgs e)
                {
                    haRepackerMainPanel.FixLinkForOldMapleStory_OnClick();
                }));
            AiUpscaleImage = new ToolStripMenuItem(Properties.Resources.MainContextMenu_Batch_AIUpscaleImage, null, new EventHandler(
                delegate (object sender, EventArgs e) {
                    haRepackerMainPanel.AiBatchImageUpscaleEdit();
                }));

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
                // export, import
                toolStripmenuItems.Add(Remove);
            }
            else if (Tag is WzImageProperty)
            {
                toolStripmenuItems.Add(Rename);
                toolStripmenuItems.Add(Remove);
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

            if (Tag.GetType() == typeof(WzSubProperty)) {
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
    }
}