/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.GUI.InstanceEditor;
using HaCreator.MapEditor;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.GUI
{
    public partial class New : Form
    {
        private MultiBoard multiBoard;
        private System.Windows.Controls.TabControl Tabs;
        private System.Windows.RoutedEventHandler[] rightClickHandler;

        public New(MultiBoard board, System.Windows.Controls.TabControl Tabs, System.Windows.RoutedEventHandler[] rightClickHandler)
        {
            InitializeComponent();
            this.multiBoard = board;
            this.Tabs = Tabs;
            this.rightClickHandler = rightClickHandler;
        }

        /// <summary>
        /// On Load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void New_Load(object sender, EventArgs e)
        {
            newWidth.Text = ApplicationSettings.LastMapSize.Width.ToString();
            newHeight.Text = ApplicationSettings.LastMapSize.Height.ToString();
        }

        #region Create new
        private void newButton_Click(object sender, EventArgs e)
        {
            int w = int.Parse(newWidth.Text);
            int h = int.Parse(newHeight.Text);

            MapLoader.CreateMap("", "<Untitled>", -1, "", MapLoader.CreateStandardMapMenu(rightClickHandler), new XNA.Point(w, h), new XNA.Point(w / 2, h / 2), Tabs, multiBoard);
            DialogResult = DialogResult.OK;
            Close();
        }
        #endregion

        #region Clone map
        /// <summary>
        /// Select a map
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_SelectCloneMap_Click(object sender, EventArgs e)
        {
            LoadMapSelector selector = new LoadMapSelector(numericUpDown1);
            selector.ShowDialog();
        }

        /// <summary>
        /// On map id selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDown1.Value != -1)
            {
                buttonCreateFrmClone.Enabled = true; // enable the button after selecting a map
            }
        }


        /// <summary>
        /// Button on create map
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonCreateFrmClone_Click(object sender, EventArgs e)
        {
            if (numericUpDown1.Value == -1)
                return;

            long mapid = (long) numericUpDown1.Value; // should be int, but anyway in case the future version uses more than 2.1b
            string mapId_str = mapid.ToString();
            string mapId_PaddingZeros = mapId_str.PadLeft(9, '0') + ".img"; // 100000000.img.xml
            string mapcat = "Map" + mapId_PaddingZeros.Substring(0, 1);

            WzDirectory directory = Program.WzManager.FindMapWz(mapcat);
            WzImage mapImage = (WzImage)directory[mapId_PaddingZeros];

            if (mapImage == null)
            {
                MessageBox.Show("Map is null.");
                return;
            }

            WzSubProperty strMapProp = WzInfoTools.GetMapStringProp(mapId_str);
            string cloneMapName = WzInfoTools.GetMapName(strMapProp);
            string cloneStreetName = WzInfoTools.GetMapStreetName(strMapProp);
            string cloneCategoryName = WzInfoTools.GetMapCategoryName(strMapProp);
            
            MapLoader.CreateMapFromImage(-1 /*mapid*/, mapImage.DeepClone(), cloneMapName, cloneStreetName, cloneCategoryName, (WzSubProperty) strMapProp.DeepClone(), Tabs, multiBoard, rightClickHandler);

            Close();
        }
        #endregion

        #region Misc
        private void New_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                newButton_Click(null, null);
            }
        }
        #endregion
    }
}
