/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.ThirdParty;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HaCreator.GUI.EditorPanels
{
    public partial class ObjPanel : DockContent
    {
        private HaCreatorStateManager hcsm;

        public ObjPanel(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;
            hcsm.SetObjPanel(this);
            InitializeComponent();

            List<string> sortedObjSets = new List<string>();
            foreach (KeyValuePair<string, WzImage> oS in Program.InfoManager.ObjectSets)
                sortedObjSets.Add(oS.Key);
            sortedObjSets.Sort();
            foreach (string oS in sortedObjSets)
                objSetListBox.Items.Add(oS);
        }

        private void objSetListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (objSetListBox.SelectedItem == null) return;
            objL0ListBox.Items.Clear();
            objL1ListBox.Items.Clear();
            objImagesContainer.Controls.Clear();
            WzImage oSImage = Program.InfoManager.ObjectSets[(string)objSetListBox.SelectedItem];
            if (!oSImage.Parsed) oSImage.ParseImage();
            foreach (WzImageProperty l0Prop in oSImage.WzProperties)
                objL0ListBox.Items.Add(l0Prop.Name);
        }

        private void objL0ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (objL0ListBox.SelectedItem == null) return;
            objL1ListBox.Items.Clear();
            objImagesContainer.Controls.Clear();
            WzImageProperty l0Prop = Program.InfoManager.ObjectSets[(string)objSetListBox.SelectedItem][(string)objL0ListBox.SelectedItem];
            foreach (WzImageProperty l1Prop in l0Prop.WzProperties)
                objL1ListBox.Items.Add(l1Prop.Name);
        }

        private void objL1ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                if (objL1ListBox.SelectedItem == null) return;
                objImagesContainer.Controls.Clear();
                WzImageProperty l1Prop = Program.InfoManager.ObjectSets[(string)objSetListBox.SelectedItem][(string)objL0ListBox.SelectedItem][(string)objL1ListBox.SelectedItem];
                foreach (WzSubProperty l2Prop in l1Prop.WzProperties)
                {
                    ObjectInfo info = ObjectInfo.Get((string)objSetListBox.SelectedItem, (string)objL0ListBox.SelectedItem, (string)objL1ListBox.SelectedItem, l2Prop.Name);
                    ImageViewer item = objImagesContainer.Add(info.Image, l2Prop.Name, true);
                    item.Tag = info;
                    item.MouseDown += new MouseEventHandler(objItem_Click);
                    item.MouseUp += new MouseEventHandler(ImageViewer.item_MouseUp);
                    item.MaxHeight = UserSettings.ImageViewerHeight;
                    item.MaxWidth = UserSettings.ImageViewerWidth;
                }
            }
        }

        public void OnL1Changed(string l1)
        {
            if ((string)objL1ListBox.SelectedItem == l1)
                objL1ListBox_SelectedIndexChanged(null, null);
        }

        private void objItem_Click(object sender, MouseEventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                if (!hcsm.MultiBoard.AssertLayerSelected())
                {
                    return;
                }
                hcsm.EnterEditMode(ItemTypes.Objects);
                hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo((ObjectInfo)((ImageViewer)sender).Tag);
                hcsm.MultiBoard.Focus();
                ((ImageViewer)sender).IsActive = true;
            }
        }
    }
}
