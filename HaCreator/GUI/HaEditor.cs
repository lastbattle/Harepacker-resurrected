/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.CustomControls;
using HaCreator.GUI.EditorPanels;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI;
using WeifenLuo.WinFormsUI.Docking;

namespace HaCreator.GUI
{
    public partial class HaEditor : Form
    {
        private InputHandler handler;
        public HaCreatorStateManager hcsm;

        private TilePanel tilePanel;
        private ObjPanel objPanel;
        private LifePanel lifePanel;
        private PortalPanel portalPanel;
        private BackgroundPanel bgPanel;
        private CommonPanel commonPanel;

        public HaEditor()
        {
            InitializeComponent();
            InitializeComponentCustom();
            RedockControls();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            multiBoard.TriggerMouseWheel(e);
        }

        private void InitializeComponentCustom()
        {
            // helper classes
            handler = new InputHandler(multiBoard);
            hcsm = new HaCreatorStateManager(multiBoard, ribbon, tabs, handler);
            hcsm.CloseRequested += hcsm_CloseRequested;
            hcsm.FirstMapLoaded += hcsm_FirstMapLoaded;

        }

        void hcsm_FirstMapLoaded()
        {
            tilePanel.Enabled = true;
            objPanel.Enabled = true;
            lifePanel.Enabled = true;
            portalPanel.Enabled = true;
            bgPanel.Enabled = true;
            commonPanel.Enabled = true;
            WindowState = FormWindowState.Maximized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.HaEditor_FormClosing);
        }

        void hcsm_CloseRequested()
        {
            Close();
        }

        private void HaEditor_Load(object sender, EventArgs e)
        {
            // This has to be here and not in .ctor for some reason, otherwise subwindows are not locating properly
            tilePanel = new TilePanel(hcsm) { Enabled = false };
            objPanel = new ObjPanel(hcsm) { Enabled = false };
            lifePanel = new LifePanel(hcsm) { Enabled = false };
            portalPanel = new PortalPanel(hcsm) { Enabled = false };
            bgPanel = new BackgroundPanel(hcsm) { Enabled = false };
            commonPanel = new CommonPanel(hcsm) { Enabled = false };

            List<DockContent> dockContents = new List<DockContent> { tilePanel, objPanel, lifePanel, portalPanel, bgPanel, commonPanel };

            dockContents.ForEach(x => x.Show(dockPanel));
            dockContents.ForEach(x => x.DockState = DockState.DockRight);
            dockContents.ForEach(x => x.DockAreas = DockAreas.DockRight);
            commonPanel.Pane = bgPanel.Pane = portalPanel.Pane = lifePanel.Pane = objPanel.Pane = tilePanel.Pane;

            if (!hcsm.backupMan.AttemptRestore())
                hcsm.LoadMap(new Load(multiBoard, tabs, hcsm.MakeRightClickHandler()));
        }

        private void RedockControls()
        {
            int ribbonHeight = (int)ribbon.ribbon.ActualHeight - ribbon.reducedHeight;

            wpfHost.Location = new Point();
            wpfHost.Size = new Size(panel1.Width, ribbonHeight);
            tabs.Location = new Point(tabs.Margin.Left, ribbonHeight + tabs.Margin.Top);
            tabs.Size = new Size(panel1.Width - tabs.Margin.Left - tabs.Margin.Right, panel1.Height - ribbonHeight - tabs.Margin.Top - tabs.Margin.Bottom);
        }

        private void HaEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!Program.Restarting && MessageBox.Show("Are you sure you want to quit?", "Quit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                e.Cancel = true;
            }
            else
            {
                // Thread safe without locks since reference assignment is atomic
                Program.AbortThreads = true;
            }
        }

        private void HaEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            multiBoard.Stop();
        }

        private void panel1_SizeChanged(object sender, EventArgs e)
        {
            RedockControls();
        }
    }
}
