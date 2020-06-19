/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.ThirdParty;
using HaSharedLibrary.GUI;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HaCreator.GUI.EditorPanels
{
    public partial class BackgroundPanel : UserControl
    {
        private HaCreatorStateManager hcsm;

        public BackgroundPanel()
        {
            InitializeComponent();
        }

        public void Initialize(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;

            List<string> sortedBgSets = new List<string>();
            foreach (KeyValuePair<string, WzImage> bS in Program.InfoManager.BackgroundSets)
            {
                sortedBgSets.Add(bS.Key);
            }
            sortedBgSets.Sort();
            foreach (string bS in sortedBgSets)
            {
                bgSetListBox.Items.Add(bS);
            }
        }

        /// <summary>
        /// On image selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bgSetListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (bgSetListBox.SelectedItem == null)
                return;
            bgImageContainer.Controls.Clear();

            string path;
            BackgroundInfoType infoType = BackgroundInfoType.Animation;
            if (radioButton_spine.Checked)
            {
                infoType = BackgroundInfoType.Spine;
                path = "spine";
            }
            else if (aniBg.Checked)
            {
                infoType = BackgroundInfoType.Animation;
                path = "ani";
            }
            else
            {
                infoType = BackgroundInfoType.Background;
                path = "back";
            }

            WzImageProperty parentProp = Program.InfoManager.BackgroundSets[(string)bgSetListBox.SelectedItem][path];
            if (parentProp == null || parentProp.WzProperties == null)
                return;

            foreach (WzImageProperty prop in parentProp.WzProperties)
            {
                BackgroundInfo bgInfo = BackgroundInfo.Get((string)bgSetListBox.SelectedItem, infoType, prop.Name);
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

        /// <summary>
        /// On click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bgItem_Click(object sender, MouseEventArgs e)
        {
            ImageViewer imageViewer = sender as ImageViewer;
            BackgroundInfo bgInfo = (BackgroundInfo)((ImageViewer)sender).Tag;

            if (e.Button == MouseButtons.Left)
            {
                lock (hcsm.MultiBoard)
                {
                    hcsm.EnterEditMode(ItemTypes.Backgrounds);
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(bgInfo);
                    hcsm.MultiBoard.Focus();
                    ((ImageViewer)sender).IsActive = true;
                }
            }
            else // right click
            {
                if (bgInfo.Type == BackgroundInfoType.Spine) // only shows an animation preview window if its a spine object.
                {
                    ContextMenu cm = new ContextMenu();

                    MenuItem menuItem = new MenuItem();
                    menuItem.Text = "Preview";
                    menuItem.Tag = bgInfo;
                    menuItem.Click += new EventHandler(delegate (object sender_, EventArgs e_)
                    {
                        MenuItem menuItem_ = sender_ as MenuItem;
                        BackgroundInfo bgInfo_ = menuItem_.Tag as BackgroundInfo;

                        WzImageProperty spineAtlasProp = bgInfo_.WzImageProperty.WzProperties.FirstOrDefault(
                            wzprop => wzprop is WzStringProperty && ((WzStringProperty)wzprop).IsSpineAtlasResources);

                        if (spineAtlasProp != null)
                        {
                            WzStringProperty stringObj = (WzStringProperty)spineAtlasProp;
                            Thread thread = new Thread(() =>
                            {
                                try
                                {
                                    WzSpineAnimationItem item = new WzSpineAnimationItem(stringObj);

                                    // Create xna window
                                    SpineAnimationWindow Window = new SpineAnimationWindow(item);
                                    Window.Run();
                                }
                                catch (Exception ex)
                                {
                                }
                            });
                            thread.Start();
                            thread.Join();
                        }
                    });
                    cm.MenuItems.Add(menuItem);

                    cm.Show(imageViewer, new Point(0, 50));
                }
            }
        }
    }
}
