/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.GUI.InstanceEditor;
using HaCreator.MapEditor;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
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

namespace HaCreator.GUI
{
    public partial class Save : Form
    {
        private readonly Board board;

        /// <summary>
        /// Save UI window for saving a map
        /// </summary>
        /// <param name="board"></param>
        /// <exception cref="NotSupportedException"></exception>
        public Save(Board board)
        {
            this.board = board;
            InitializeComponent();

            if (board.IsNewMapDesign) {
                idBox_mapId.Text =  MapConstants.MaxMap.ToString();
            }
            else {
                switch (board.MapInfo.mapType) {
                    case MapType.CashShopPreview:
                    case MapType.ITCPreview:
                    case MapType.MapLogin:
                        idBox_mapId.Text = board.MapInfo.strMapName;
                        break;
                    case MapType.RegularMap:
                        idBox_mapId.Text = board.MapInfo.id == -1 ? "-1" : board.MapInfo.id.ToString();
                        break;
                    default:
                        throw new NotSupportedException("Unknown map type at Save::.ctor()");
                }
            }
            idBox_TextChanged(null, null);
        }

        private MapType GetIdBoxMapType()
        {
            if (idBox_mapId.Text.StartsWith("MapLogin"))
                return MapType.MapLogin;
            else if (idBox_mapId.Text == "CashShopPreview")
                return MapType.CashShopPreview;
            else if (idBox_mapId.Text == "ITCPreview")
                return MapType.ITCPreview;
            else
                return MapType.RegularMap;
        }

        /// <summary>
        /// On map id textbox change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void idBox_TextChanged(object sender, EventArgs e)
        {
            int id = 0;
            if (idBox_mapId.Text == string.Empty)
            {
                statusLabel.Text = "Please choose an ID";
                saveButton.Enabled = false;
            }
            else if (GetIdBoxMapType() != MapType.RegularMap)
            {
                statusLabel.Text = "";
                saveButton.Enabled = true;
            }
            else if (!int.TryParse(idBox_mapId.Text, out id) || id == MapConstants.MaxMap)
            {
                statusLabel.Text = "You must enter a number.";
                saveButton.Enabled = false;
            }
            else if (id < MapConstants.MinMap || id > MapConstants.MaxMap)
            {
                statusLabel.Text = "Out of range. Select between "+ MapConstants.MinMap + " and "+ MapConstants.MaxMap + ".";
                saveButton.Enabled = false;
            }
            else if (Program.InfoManager.MapsNameCache.ContainsKey(id.ToString())) {
                if (board.IsNewMapDesign) { // if its a new design, do not allow overriding regardless 
                    statusLabel.Text = "WARNING: It will overwrite existing map, select an empty ID.";
                    saveButton.Enabled = false;
                } else {
                    statusLabel.Text = "WARNING: It will overwrite existing map.";
                    saveButton.Enabled = true;
                }
            }
            else
            {
                statusLabel.Text = "";
                saveButton.Enabled = true;
            }
        }

        /// <summary>
        /// Click save button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveButton_Click(object sender, EventArgs e)
        {
            if (board.ParentControl.UserObjects.NewObjects.Count > 0)
            {
                // Flush the UserObjects cache because the map we are saving might use of those images
                if (MessageBox.Show("You have unsaved user objects (images from your computer that you added to the editor). If you proceed, the following images will be written to the WZ file:\r\n\r\n" + board.ParentControl.UserObjects.NewObjects.Select(x => x.l2).Aggregate((x, y) => x + "\r\n" + y) + "\r\n\r\nIf you want to remove some or all of them, exit the saving dialog and remove them first.\r\nProceed?", "Unsaved Objects", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                    return;
                board.ParentControl.UserObjects.Flush();
            }
            MapType type = GetIdBoxMapType();
            MapSaver saver = new MapSaver(board);

            // Regenerate minimap first
            board.RegenerateMinimap();

            if (type == MapType.RegularMap)
            {
                int newId = int.Parse(idBox_mapId.Text);
                saver.ChangeMapTypeAndID(newId, MapType.RegularMap);
                saver.SaveMapImage();
                saver.UpdateMapLists();

                MessageBox.Show("Saved map with ID: " + newId.ToString());
            }
            else
            {
                board.MapInfo.strMapName = idBox_mapId.Text;
                ((TabItemContainer)board.TabPage.Tag).Text = board.MapInfo.strMapName;
                saver.ChangeMapTypeAndID(-1, type);
                saver.SaveMapImage();

                MessageBox.Show("Saved map: " + board.MapInfo.strMapName);
            }
            Close();
        }

        /// <summary>
        /// On key down
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Save_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                saveButton_Click(null, null);
            }
        }

        /// <summary>
        /// Select a map from the list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_select_Click(object sender, EventArgs e) {
            LoadMapSelector selector = new LoadMapSelector(idBox_mapId);
            selector.ShowDialog();
        }
    }
}
