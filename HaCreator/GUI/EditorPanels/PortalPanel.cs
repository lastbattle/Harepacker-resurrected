/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.ThirdParty;
using MapleLib.WzLib.WzStructure.Data;
using System;
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
    public partial class PortalPanel : DockContent
    {
        HaCreatorStateManager hcsm;

        public PortalPanel(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;
            InitializeComponent();

            foreach (string pt in Program.InfoManager.PortalTypeById)
            {
                PortalInfo pInfo = PortalInfo.GetPortalInfoByType(pt);
                ImageViewer item = portalImageContainer.Add(pInfo.Image, Tables.PortalTypeNames[pt], true);
                item.Tag = pInfo;
                item.MouseDown += new MouseEventHandler(portal_MouseDown);
                item.MouseUp += new MouseEventHandler(ImageViewer.item_MouseUp);
            }
        }

        void portal_MouseDown(object sender, MouseEventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                hcsm.EnterEditMode(ItemTypes.Portals);
                hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo((PortalInfo)((ImageViewer)sender).Tag);
                hcsm.MultiBoard.Focus();
                ((ImageViewer)sender).IsActive = true;
            }
        }
    }
}
