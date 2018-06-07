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
using HaRepackerLib.Controls;
using System.Collections;
using HaRepackerLib;
using HaRepacker.GUI.Interaction;
using HaRepacker.GUI;
using HaRepackerLib.Controls.HaRepackerMainPanels;

namespace HaRepacker
{
    public class ContextMenuManager
    {
        //private HaRepackerMainPanel parent;

        public ContextMenuStrip WzFileMenu;
        public ContextMenuStrip WzDirectoryMenu;
        public ContextMenuStrip PropertyContainerMenu;
        public ContextMenuStrip SubPropertyMenu;
        public ContextMenuStrip PropertyMenu;

        private ToolStripMenuItem SaveFile;
        private ToolStripMenuItem Remove;
        private ToolStripMenuItem Unload;
        private ToolStripMenuItem Reload;

        private ToolStripMenuItem AddPropsSubMenu;
        private ToolStripMenuItem AddDirsSubMenu;
        private ToolStripMenuItem AddConvexSubMenu;
        private ToolStripMenuItem AddImage;
        private ToolStripMenuItem AddDirectory;
        private ToolStripMenuItem AddByteFloat;
        private ToolStripMenuItem AddCanvas;
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

        public ContextMenuManager(HaRepackerMainPanel haRepackerMainPanel, UndoRedoManager undoMan)
        {
            //this.parent = parent;
            SaveFile = new ToolStripMenuItem("Save...", Properties.Resources.disk, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    foreach (WzNode node in GetNodes(sender))
                    {
                        HaRepackerMainPanel parent = ((HaRepackerMainPanel)node.TreeView.Parent.Parent.Parent);
                        new SaveForm(parent, node).ShowDialog();
                    }
                }));

            Remove = new ToolStripMenuItem("Remove", Properties.Resources.delete, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    haRepackerMainPanel.PromotRemoveSelectedTreeNodes();
                }));

            Unload = new ToolStripMenuItem("Unload", Properties.Resources.delete, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    if (!Warning.Warn("Are you sure you want to unload this file?"))
                        return;

                    foreach (WzNode node in GetNodes(sender))
                    {
                        Program.WzMan.UnloadWzFile((WzFile)node.Tag);
                    }
                }));
            Reload = new ToolStripMenuItem("Reload", Properties.Resources.arrow_refresh, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    if (!Warning.Warn("Are you sure you want to reload this file?"))
                        return;

                    foreach (WzNode node in GetNodes(sender))
                    {
                        HaRepackerMainPanel parent = ((HaRepackerMainPanel)node.TreeView.Parent.Parent.Parent);
                        Program.WzMan.ReloadWzFile((WzFile)node.Tag, parent);
                    }
                }));

            AddImage = new ToolStripMenuItem("Image", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name;
                    if (NameInputBox.Show("Add Image", out name))
                        nodes[0].AddObject(new WzImage(name) { Changed = true }, undoMan);
                }));
            AddDirectory = new ToolStripMenuItem("Directory", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name;
                    if (NameInputBox.Show("Add Directory", out name))
                        nodes[0].AddObject(new WzDirectory(name), undoMan);
                }));
            AddByteFloat = new ToolStripMenuItem("Float", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name; double? val;
                    if (FloatingPointInputBox.Show("Add Float", out name, out val))
                        nodes[0].AddObject(new WzFloatProperty(name, (float)val), undoMan);
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

                    string name;
                    List<Bitmap> bitmaps = new List<Bitmap>();
                    if (BitmapInputBox.Show("Add Canvas", out name, out bitmaps))
                    {
                        int i = 0;
                        foreach (Bitmap bmp in bitmaps)
                        {
                            string addName = bitmaps.Count() == 1 ? name : (name + i);

                            WzCanvasProperty prop = new WzCanvasProperty(addName);
                            prop.PngProperty = new WzPngProperty();
                            prop.PngProperty.SetPNG(bmp);

                            WzNode newInsertedNode = nodes[0].AddObject(prop, undoMan);
                            // Add an additional WzVector
                            newInsertedNode.AddObject(new WzVectorProperty(name, new WzIntProperty("X", 0), new WzIntProperty("Y", 0)), undoMan);

                            i++;
                        }
                    }
                }));
            AddInt = new ToolStripMenuItem("Int", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name; int? val;
                    if (IntInputBox.Show("Add Int", out name, out val))
                        nodes[0].AddObject(new WzIntProperty(name, (int)val), undoMan);
                }));
            AddConvex = new ToolStripMenuItem("Convex", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) { MessageBox.Show("Please select only ONE node"); return; }
                    string name;
                    if (NameInputBox.Show("Add Convex", out name))
                        nodes[0].AddObject(new WzConvexProperty(name), undoMan);
                }));
            AddDouble = new ToolStripMenuItem("Double", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name; double? val;
                    if (FloatingPointInputBox.Show("Add Double", out name, out val))
                        nodes[0].AddObject(new WzDoubleProperty(name, (double)val), undoMan);
                }));
            AddNull = new ToolStripMenuItem("Null", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name;
                    if (NameInputBox.Show("Add Null", out name))
                        nodes[0].AddObject(new WzNullProperty(name), undoMan);
                }));
            AddSound = new ToolStripMenuItem("Sound", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name, path;
                    if (SoundInputBox.Show("Add Sound", out name, out path))
                    {
                        try { nodes[0].AddObject(new WzSoundProperty(name, path), undoMan); }
                        catch (Exception ex) { MessageBox.Show("Exception caught while adding property: \"" + ex.Message + "\"", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                }));
            AddString = new ToolStripMenuItem("String", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name, value;
                    if (NameValueInputBox.Show("Add String", out name, out value))
                        nodes[0].AddObject(new WzStringProperty(name, value), undoMan);
                }));
            AddSub = new ToolStripMenuItem("Sub", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name;
                    if (NameInputBox.Show("Add Sub", out name))
                        nodes[0].AddObject(new WzSubProperty(name), undoMan);
                }));
            AddUshort = new ToolStripMenuItem("Short", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name; int? val;
                    if (IntInputBox.Show("Add Unsigned Short", out name, out val))
                        nodes[0].AddObject(new WzShortProperty(name, (short)val), undoMan);
                }));
            AddUOL = new ToolStripMenuItem("UOL", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name, value;
                    if (NameValueInputBox.Show("Add UOL", out name, out value))
                        nodes[0].AddObject(new WzUOLProperty(name, value), undoMan);
                }));
            AddVector = new ToolStripMenuItem("Vector", null, new EventHandler(
                delegate(object sender, EventArgs e)
                {
                    WzNode[] nodes = GetNodes(sender);
                    if (nodes.Length != 1) {
                        MessageBox.Show("Please select only ONE node");
                        return;
                    }

                    string name; Point? pt;
                    if (VectorInputBox.Show("Add Vector", out name, out pt))
                        nodes[0].AddObject(new WzVectorProperty(name, new WzIntProperty("X", pt.Value.X), new WzIntProperty("Y", pt.Value.Y)), undoMan);
                }));

            AddConvexSubMenu = new ToolStripMenuItem("Add", Properties.Resources.add, AddVector);
            AddDirsSubMenu = new ToolStripMenuItem("Add", Properties.Resources.add, AddDirectory, AddImage);
            AddPropsSubMenu = new ToolStripMenuItem("Add", Properties.Resources.add, AddCanvas, AddConvex, AddDouble, AddByteFloat, AddInt, AddNull, AddUshort, AddSound, AddString, AddSub, AddUOL, AddVector);

            WzFileMenu = new ContextMenuStrip();
            WzFileMenu.Items.AddRange(new ToolStripItem[] { AddDirsSubMenu, SaveFile, Unload, Reload });
            WzDirectoryMenu = new ContextMenuStrip();
            WzDirectoryMenu.Items.AddRange(new ToolStripItem[] { AddDirsSubMenu, /*export, import,*/Remove });
            PropertyContainerMenu = new ContextMenuStrip();
            PropertyContainerMenu.Items.AddRange(new ToolStripItem[] { AddPropsSubMenu, /*export, import,*/Remove });
            PropertyMenu = new ContextMenuStrip();
            PropertyMenu.Items.AddRange(new ToolStripItem[] { /*export, import,*/Remove });
            SubPropertyMenu = new ContextMenuStrip();
            SubPropertyMenu.Items.AddRange(new ToolStripItem[] { AddPropsSubMenu, /*export, import,*/Remove });
        }

        public ContextMenuStrip CreateMenu(WzNode node, WzObject Tag)
        {
            ContextMenuStrip menu = null;
            if (Tag is WzImage || Tag is IPropertyContainer)
            {
                if (Tag is WzSubProperty)
                {
                    menu = new ContextMenuStrip();
                    menu.Items.AddRange(new ToolStripItem[] { AddPropsSubMenu, /*export, import,*/Remove });
                }
                else
                {
                    menu = new ContextMenuStrip();
                    menu.Items.AddRange(new ToolStripItem[] { AddPropsSubMenu, /*export, import,*/Remove });
                }
            }
            else if (Tag is WzImageProperty)
            {
                menu = new ContextMenuStrip();
                menu.Items.AddRange(new ToolStripItem[] { /*export, import,*/Remove });
            }
            else if (Tag is WzDirectory)
            {
                menu = new ContextMenuStrip();
                menu.Items.AddRange(new ToolStripItem[] { AddDirsSubMenu, /*export, import,*/Remove });
            }
            else if (Tag is WzFile)
            {
                menu = new ContextMenuStrip();
                menu.Items.AddRange(new ToolStripItem[] { AddDirsSubMenu, SaveFile, Unload, Reload });
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