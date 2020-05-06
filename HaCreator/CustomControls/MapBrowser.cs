/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Threading;

namespace HaCreator.CustomControls
{
    public partial class MapBrowser : UserControl
    {
        private bool load = false;
        private List<string> maps = new List<string>();

        public MapBrowser()
        {
            InitializeComponent();
        }

        public bool LoadAvailable
        {
            get
            {
                return load;
            }
        }

        public string SelectedItem
        {
            get
            {
                return (string)mapNamesBox.SelectedItem;
            }
        }

        public bool IsEnabled
        {
            set
            {
                mapNamesBox.Enabled = value;
                minimapBox.Visible = value;
            }
        }

        public delegate void MapSelectChangedDelegate();
        public event MapSelectChangedDelegate SelectionChanged;

        public void InitializeMaps(bool special)
        {
            bool mapLogin1 = Program.WzManager["ui"]["MapLogin1.img"] != null;
            bool mapLogin2 = Program.WzManager["ui"]["MapLogin2.img"] != null;
            bool mapLogin3 = Program.WzManager["ui"]["MapLogin3.img"] != null;

            foreach (KeyValuePair<string, string> map in Program.InfoManager.Maps)
            {
                maps.Add(map.Key + " - " + map.Value);
            }
            maps.Sort();
            if (special)
            {
                maps.Insert(0, "CashShopPreview");
                maps.Insert(0, "MapLogin");
                if (mapLogin1)
                    maps.Insert(0, "MapLogin1");
                if (mapLogin2)
                    maps.Insert(0, "MapLogin2");
                if (mapLogin3)
                    maps.Insert(0, "MapLogin3");
            }

            object[] mapsObjs = maps.Cast<object>().ToArray();
            mapNamesBox.Items.AddRange(mapsObjs);
        }

        private string _previousSeachText = string.Empty;
        /// <summary>
        /// On search box text changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void searchBox_TextChanged(object sender, EventArgs e)
        {
            TextBox searchBox = (TextBox)sender;
            string tosearch = searchBox.Text.ToLower();

            if (_previousSeachText == tosearch)
            {
                return;
            }
            _previousSeachText = tosearch; // set


            mapNamesBox.Items.Clear();
            if (tosearch == string.Empty)
            {
                mapNamesBox.Items.AddRange(maps.Cast<object>().ToArray<object>());
            }
            else
            {
                foreach (string map in maps)
                {
                    if (map.ToLower().Contains(tosearch))
                    {
                        mapNamesBox.Items.Add(map);
                    }
                }
            }
            mapNamesBox.SelectedItem = null;
            mapNamesBox.SelectedIndex = -1;
            mapNamesBox_SelectedIndexChanged(null, null);
        }

        private void mapNamesBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedName = (string)mapNamesBox.SelectedItem;

            if (selectedName == "MapLogin" ||
                selectedName == "MapLogin1" ||
                selectedName == "MapLogin2" ||
                selectedName == "MapLogin3" ||
                selectedName == "CashShopPreview" ||
                selectedName == null)
            {
                linkLabel.Visible = false;
                mapNotExist.Visible = false;
                minimapBox.Image = (Image)new Bitmap(1, 1);
                load = mapNamesBox.SelectedItem != null;
            }
            else
            {
                string mapid = (selectedName).Substring(0, 9);
                string mapcat = "Map" + mapid.Substring(0, 1);
                WzImage mapImage = null;
                if (Program.WzManager.wzFiles.ContainsKey("map002"))//i hate nexon so much
                {
                    mapImage = (WzImage)Program.WzManager["map002"]["Map"][mapcat][mapid + ".img"];
                }
                else
                {
                    mapImage = (WzImage)Program.WzManager["map"]["Map"][mapcat][mapid + ".img"];
                }
                if (mapImage == null)
                {
                    linkLabel.Visible = false;
                    mapNotExist.Visible = true;
                    minimapBox.Image = (Image)new Bitmap(1, 1);
                    load = false;
                }
                else
                {
                    using (WzImageResource rsrc = new WzImageResource(mapImage))
                    {
                        if (mapImage["info"]["link"] != null)
                        {
                            linkLabel.Visible = true;
                            mapNotExist.Visible = false;
                            minimapBox.Image = (Image)new Bitmap(1, 1);
                            load = false;
                        }
                        else
                        {
                            linkLabel.Visible = false;
                            mapNotExist.Visible = false;
                            load = true;
                            WzCanvasProperty minimap = (WzCanvasProperty)mapImage.GetFromPath("miniMap/canvas");
                            if (minimap != null)
                            {
                                minimapBox.Image = (Image)minimap.GetLinkedWzCanvasBitmap();
                            }
                            else
                            {
                                minimapBox.Image = (Image)new Bitmap(1, 1);
                            }
                            load = true;
                        }
                    }
                }
            }
            SelectionChanged.Invoke();
        }
    }
}
