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
    public partial class BackgroundPanel : DockContent
    {
        private HaCreatorStateManager hcsm;

        public BackgroundPanel(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;
            InitializeComponent();

            List<string> sortedBgSets = new List<string>();
            foreach (KeyValuePair<string, WzImage> bS in Program.InfoManager.BackgroundSets)
                sortedBgSets.Add(bS.Key);
            sortedBgSets.Sort();
            foreach (string bS in sortedBgSets)
                bgSetListBox.Items.Add(bS);
        }

        private void bgSetListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (bgSetListBox.SelectedItem == null)
                return;
            bgImageContainer.Controls.Clear();

            WzImageProperty parentProp = Program.InfoManager.BackgroundSets[(string)bgSetListBox.SelectedItem][aniBg.Checked ? "ani" : "back"];
            if (parentProp == null || parentProp.WzProperties == null)
                return;
            foreach (WzImageProperty prop in parentProp.WzProperties)
            {
                BackgroundInfo bgInfo = BackgroundInfo.Get((string)bgSetListBox.SelectedItem, aniBg.Checked, prop.Name);
                if (bgInfo == null)
                    continue;
                ImageViewer item = bgImageContainer.Add(bgInfo.Image, prop.Name, true);
                item.Tag = bgInfo;
                item.MouseDown += new MouseEventHandler(bgItem_Click);
                item.MouseUp += new MouseEventHandler(ImageViewer.item_MouseUp);
                item.MaxHeight = UserSettings.ImageViewerHeight;
                item.MaxWidth = UserSettings.ImageViewerWidth;
            }
        }

        void bgItem_Click(object sender, MouseEventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                hcsm.EnterEditMode(ItemTypes.Backgrounds);
                hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo((BackgroundInfo)((ImageViewer)sender).Tag);
                hcsm.MultiBoard.Focus();
                ((ImageViewer)sender).IsActive = true;
            }
        }
    }
}
