/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using HaCreator.GUI;
using HaSharedLibrary.Wz;
using MapleLib.WzLib.WzStructure;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Collections.Specialized;

namespace HaCreator.CustomControls
{
    public partial class MapBrowser : UserControl
    {
        private bool loadMapAvailable = false;
        private List<string> maps = null; // cache
        private Dictionary<string, Tuple<WzImage, MapInfo>> mapsMapInfo = new Dictionary<string, Tuple<WzImage, MapInfo>>();

        private bool _bEnableIsTownFilter = false;

        public MapBrowser()
        {
            InitializeComponent();

            this.minimapBox.SizeMode = PictureBoxSizeMode.Zoom;
        }

        public bool LoadAvailable
        {
            get
            {
                return loadMapAvailable;
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

        /// <summary>
        /// Sets the 'town' filter for searchbox
        /// </summary>
        public bool EnableIsTownFilter {
            get { return _bEnableIsTownFilter; }
            set { 
                this._bEnableIsTownFilter = value;
            }
        }

        public delegate void MapSelectChangedDelegate();
        public event MapSelectChangedDelegate SelectionChanged;

        /// <summary>
        /// Initialise
        /// </summary>
        /// <param name="special">True to include cash shop and login.</param>
        public void InitializeMapsListboxItem(bool special)
        {
            if (maps != null) {
                return; // already loaded
            }
            maps = new List<string>();

            // Logins
            List<string> mapLogins = new List<string>();
            for (int i = 0; i < 20; i++) // Not exceeding 20 logins yet.
            {
                string imageName = "MapLogin" + (i == 0 ? "" : i.ToString()) + ".img";

                WzObject mapLogin = null;

                List<WzDirectory> uiWzFiles = Program.WzManager.GetWzDirectoriesFromBase("ui");
                foreach (WzDirectory uiWzFile in uiWzFiles)
                {
                    mapLogin = uiWzFile?[imageName];
                    if (mapLogin != null)
                        break;
                }

                if (mapLogin == null)
                    break;
                mapLogins.Add(imageName);
            }

            // Maps
            foreach (KeyValuePair<string, Tuple<string, string>> map in Program.InfoManager.MapsNameCache)
            {
                string mapid_str = map.Key.Substring(0, 9);

                if (Program.InfoManager.MapsCache.ContainsKey(mapid_str)) {
                    Tuple<WzImage, WzSubProperty, string, string, string, MapInfo> loadedMap = Program.InfoManager.MapsCache[mapid_str];

                    string displayMapNameString = string.Format("{0} - {1} : {2}", map.Key, map.Value.Item1, map.Value.Item2);

                    maps.Add(displayMapNameString);
                    mapsMapInfo.Add(displayMapNameString, new Tuple<WzImage, MapInfo>(loadedMap.Item1, loadedMap.Item6));
                }
            }
            maps.Sort();

            if (special)
            {
                maps.Insert(0, "CashShopPreview");

                foreach (string mapLogin in mapLogins)
                    maps.Insert(0, mapLogin.Replace(".img", ""));
            }

            object[] mapsObjs = maps.Cast<object>().ToArray();
            mapNamesBox.Items.AddRange(mapsObjs);
        }

        private string _previousSeachText = string.Empty;
        private CancellationTokenSource _existingSearchTaskToken = null;
        /// <summary>
        /// On search box text changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">May be null</param>
        public void searchBox_TextChanged(object sender, EventArgs e)
        {
            TextBox searchBox = (TextBox)sender;
            string searchText = searchBox.Text.ToLower();

            if (_previousSeachText == searchText)
                return;
            _previousSeachText = searchText; // set
            
            // start searching
            searchMapsInternal(searchText);
        }

        /// <summary>
        /// Search and filters map according to the user's query
        /// </summary>
        /// <param name="searchText"></param>
        public void searchMapsInternal(string searchText) {
            // Cancel existing task if any
            if (_existingSearchTaskToken != null && !_existingSearchTaskToken.IsCancellationRequested) {
                _existingSearchTaskToken.Cancel();
            }

            // Clear 
            mapNamesBox.Items.Clear();
            if (searchText == string.Empty) {
                var filteredItems = maps.Where(kvp => {
                    MapInfo mapInfo = null;
                    if (mapsMapInfo.ContainsKey(kvp)) {
                        mapInfo = mapsMapInfo[kvp].Item2;

                        if (this._bEnableIsTownFilter) {
                            if (!mapInfo.town)
                                return false;
                        }
                    }
                    return true;
                }).Select(kvp => kvp) // or kvp.Value or any transformation you need
                  .Cast<object>()
                  .ToArray();


                mapNamesBox.Items.AddRange(filteredItems);

                mapNamesBox_SelectedIndexChanged(null, null);
            }
            else {

                Dispatcher currentDispatcher = Dispatcher.CurrentDispatcher;

                // new task
                _existingSearchTaskToken = new CancellationTokenSource();
                var cancellationToken = _existingSearchTaskToken.Token;

                Task t = Task.Run(() => {
                    Thread.Sleep(500); // average key typing speed

                    List<string> mapsFiltered = new List<string>();
                    foreach (string map in maps) {
                        if (_existingSearchTaskToken.IsCancellationRequested)
                            return; // stop immediately

                        MapInfo mapInfo = null;
                        if (mapsMapInfo.ContainsKey(map)) {
                            mapInfo = mapsMapInfo[map].Item2;
                        }

                        // Filter by string first
                        if (map.ToLower().Contains(searchText)) {

                            // Filter again by 'town' if mapInfo is not null.
                            if (mapInfo != null) {
                                if (this._bEnableIsTownFilter) {
                                    if (!mapInfo.town)
                                        continue;
                                }
                            }
                            mapsFiltered.Add(map);
                        }
                    }

                    currentDispatcher.BeginInvoke(new Action(() => {
                        foreach (string map in mapsFiltered) {
                            if (_existingSearchTaskToken.IsCancellationRequested)
                                return; // stop immediately

                            mapNamesBox.Items.Add(map);
                        }

                        if (mapNamesBox.Items.Count > 0) {
                            mapNamesBox.SelectedIndex = 0; // set default selection to reduce clicks
                        }
                    }));
                }, cancellationToken);

            }
        }

        /// <summary>
        /// On map selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mapNamesBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedName = (string)mapNamesBox.SelectedItem;

            if (selectedName == "MapLogin" ||
                selectedName == "MapLogin1" ||
                selectedName == "MapLogin2" ||
                selectedName == "MapLogin3" ||
                selectedName == "MapLogin4" ||
                selectedName == "MapLogin5" ||
                selectedName == "CashShopPreview" ||
                selectedName == null)
            {
                panel_linkWarning.Visible = false;
                panel_mapExistWarning.Visible = false;

                minimapBox.Image = new Bitmap(1, 1);
                loadMapAvailable = mapNamesBox.SelectedItem != null;
            }
            else
            {
                string mapid = (selectedName).Substring(0, 9);

                Tuple<WzImage, MapInfo> mapTupleInfo = null;
                if (mapsMapInfo.ContainsKey(selectedName)) {
                    mapTupleInfo = mapsMapInfo[selectedName];
                }


                if (mapTupleInfo == null)
                {
                    panel_linkWarning.Visible = false;
                    panel_mapExistWarning.Visible = true;

                    minimapBox.Image = (Image)new Bitmap(1, 1);
                    loadMapAvailable = false;
                }
                else
                {
                    using (WzImageResource rsrc = new WzImageResource(mapTupleInfo.Item1))
                    {
                        if (mapTupleInfo.Item1["info"]["link"] != null)
                        {
                            panel_linkWarning.Visible = true;
                            panel_mapExistWarning.Visible = false;
                            label_linkMapId.Text = mapTupleInfo.Item1["info"]["link"].ToString();

                            minimapBox.Image = new Bitmap(1, 1);
                            loadMapAvailable = false;
                        }
                        else
                        {
                            panel_linkWarning.Visible = false;
                            panel_mapExistWarning.Visible = false;

                            loadMapAvailable = true;
                            WzCanvasProperty minimap = (WzCanvasProperty)mapTupleInfo.Item1.GetFromPath("miniMap/canvas");
                            if (minimap != null)
                            {
                                minimapBox.Image = minimap.GetLinkedWzCanvasBitmap();
                            }
                            else
                            {
                                minimapBox.Image = new Bitmap(1, 1);
                            }
                            loadMapAvailable = true;
                        }
                    }
                }
            }
            SelectionChanged.Invoke();
        }
    }
}
