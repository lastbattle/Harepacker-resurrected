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
using System.Data.SQLite;
using HaCreator.GUI.InstanceEditor;
using System.Diagnostics;

namespace HaCreator.CustomControls
{
    public partial class MapBrowser : UserControl
    {
        private bool bLoadMapEnabled = false;

        private bool _bMapsLoaded = false;
        private readonly List<string> maps = new List<string>(); // cache
        private readonly Dictionary<string, Tuple<WzImage, MapInfo>> mapsMapInfo = new Dictionary<string, Tuple<WzImage, MapInfo>>();

        private bool _bTownOnlyFilter = false;
        private bool _bIsHistoryMapBrowser = false;

        private LoadSearchHelper _search;
        public LoadSearchHelper Search
        {
            get { return _search; }
            private set { }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public MapBrowser()
        {
            InitializeComponent();

            this._search = new LoadSearchHelper(mapNamesBox, maps);

            this.minimapBox.SizeMode = PictureBoxSizeMode.Zoom;
        }

        /// <summary>
        /// Determines if loading the selected map is possible
        /// </summary>
        public bool LoadMapEnabled
        {
            get { return bLoadMapEnabled; }
        }

        /// <summary>
        /// The selected map in the listbox
        /// </summary>
        public string SelectedItem
        {
            get { return (string)mapNamesBox.SelectedItem; }
        }

        /// <summary>
        /// Sets the map browser inner contents to be enabled or not.
        /// </summary>
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
        public bool TownOnlyFilter {
            get { return _bTownOnlyFilter; }
            set { 
                this._bTownOnlyFilter = value;
            }
        }

        public delegate void MapSelectChangedDelegate();
        public event MapSelectChangedDelegate SelectionChanged;

        #region Initialise
        /// <summary>
        /// Initialise
        /// </summary>
        /// <param name="special">True to include cash shop and login.</param>
        public void InitializeMapsListboxItem(bool special)
        {
            if (_bMapsLoaded) {
                return; // already loaded
            }
            _bMapsLoaded = true;

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
            foreach (KeyValuePair<string, Tuple<string, string, string>> map in Program.InfoManager.MapsNameCache) // street name, map name, category name
            {
                string mapid_str = map.Key.Substring(0, 9);

                if (Program.InfoManager.MapsCache.ContainsKey(mapid_str)) {
                    Tuple<WzImage, string, string, string, MapInfo> loadedMap = Program.InfoManager.MapsCache[mapid_str];

                    string displayMapNameString = string.Format("{0} - {1} : {2}", map.Key, map.Value.Item1, map.Value.Item2);

                    maps.Add(displayMapNameString);
                    mapsMapInfo.Add(displayMapNameString, new Tuple<WzImage, MapInfo>(loadedMap.Item1, loadedMap.Item5));
                }
            }
            maps.Sort();

            if (special)
            {
                maps.Insert(0, "CashShopPreview");
                maps.Insert(1, "ITCPreview");

                foreach (string mapLogin in mapLogins)
                    maps.Insert(0, mapLogin.Replace(".img", ""));
            }

            object[] mapsObjs = maps.Cast<object>().ToArray();
            mapNamesBox.Items.AddRange(mapsObjs);
        }


        private const string SQLITE_DB_CONNECTION_STRING = "Data Source=hacreator.db;Version=3;";
        private const string SQLITE_DB_HISTORY_TABLE_NAME = "LoadedMapsHistory";

        /// <summary>
        /// Initialise the list of history maps
        /// </summary>
        public void InitialiseHistoryListboxItem() {
            if (_bMapsLoaded) {
                return; // already loaded
            }
            _bMapsLoaded = true;

            using (var connection = new SQLiteConnection(SQLITE_DB_CONNECTION_STRING)) {
                connection.Open();

                string sql_create = 
                    string.Format(
                        "CREATE TABLE IF NOT EXISTS {0} (" +
                        "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
                        "OpenedMapName TEXT);", SQLITE_DB_HISTORY_TABLE_NAME);

                SQLiteCommand command = new SQLiteCommand(sql_create, connection);
                command.ExecuteNonQuery();

                string sql_select = string.Format("SELECT * FROM {0};", SQLITE_DB_HISTORY_TABLE_NAME);
                command = new SQLiteCommand(sql_select, connection);
                SQLiteDataReader reader = command.ExecuteReader();

                int i = 0;
                while (reader.Read()) {
                    string OpenedMapName = (string)reader["OpenedMapName"];
                    Debug.WriteLine("Entry [" + i + "] Name: '" + OpenedMapName + "'");

                    string mapid_str = OpenedMapName.Substring(0, Math.Min(OpenedMapName.Length, 9));

                    if (Program.InfoManager.MapsCache.ContainsKey(mapid_str)) {
                        Tuple<WzImage, string, string, string, MapInfo> loadedMap = Program.InfoManager.MapsCache[mapid_str];

                        maps.Add(OpenedMapName);
                        if (!mapsMapInfo.ContainsKey(OpenedMapName))
                            mapsMapInfo.Add(OpenedMapName, new Tuple<WzImage, MapInfo>(loadedMap.Item1, loadedMap.Item5));
                    }
                }
                object[] mapsObjs = maps.Cast<object>().ToArray();
                mapNamesBox.Items.AddRange(mapsObjs);
            }
        }

        public void AddLoadedMapToHistory(string loadedMapName) {
            // add to database
            using (var connection = new SQLiteConnection(SQLITE_DB_CONNECTION_STRING)) {
                connection.Open();

                // Insert data using parameterized query
                string sqlInsert = string.Format("INSERT INTO {0} (OpenedMapName) VALUES (@OpenedMapName)", SQLITE_DB_HISTORY_TABLE_NAME);
                SQLiteCommand insertCommand = new SQLiteCommand(sqlInsert, connection);

                // Adding parameters with values
                insertCommand.Parameters.AddWithValue("@OpenedMapName", loadedMapName);

                // Execute the command
                insertCommand.ExecuteNonQuery();
            }
            // add to current listbox
            maps.Add(loadedMapName);
            mapNamesBox.Items.Add(loadedMapName);
        }

        public void ClearLoadedMapHistory() {
            // add to database
            using (var connection = new SQLiteConnection(SQLITE_DB_CONNECTION_STRING)) {
                connection.Open();

                // Insert data using parameterized query
                string sql = string.Format("DELETE FROM {0};", SQLITE_DB_HISTORY_TABLE_NAME);
                SQLiteCommand sqlDelCommand = new SQLiteCommand(sql, connection);

                // Execute the command
                sqlDelCommand.ExecuteNonQuery();
            }
            // Clear the current list
            maps.Clear();
            mapNamesBox.Items.Clear();
        }
        #endregion

        #region UI
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
                selectedName == "ITCPreview" ||
                selectedName == null)
            {
                panel_linkWarning.Visible = false;
                panel_mapExistWarning.Visible = false;

                minimapBox.Image = new Bitmap(1, 1);
                bLoadMapEnabled = mapNamesBox.SelectedItem != null;
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
                    bLoadMapEnabled = false;
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
                            bLoadMapEnabled = false;
                        }
                        else
                        {
                            panel_linkWarning.Visible = false;
                            panel_mapExistWarning.Visible = false;

                            bLoadMapEnabled = true;
                            WzCanvasProperty minimap = (WzCanvasProperty)mapTupleInfo.Item1.GetFromPath("miniMap/canvas");
                            if (minimap != null)
                            {
                                minimapBox.Image = minimap.GetLinkedWzCanvasBitmap();
                            }
                            else
                            {
                                minimapBox.Image = new Bitmap(1, 1);
                            }
                            bLoadMapEnabled = true;
                        }
                    }
                }
            }
            SelectionChanged?.Invoke();
        }
        #endregion
    }
}
