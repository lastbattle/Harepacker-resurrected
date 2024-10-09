/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

//uncomment the line below to create a space-time tradeoff (saving RAM by wasting more CPU cycles)
#define SPACETIME

using System;
using System.Windows.Forms;
using System.IO;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapEditor;
using HaCreator.Wz;
using System.Collections.Generic;
using HaSharedLibrary.Wz;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.Serializer;

namespace HaCreator.GUI
{
    public partial class FieldSelector : System.Windows.Forms.Form
    {
        private readonly MultiBoard multiBoard;
        private readonly System.Windows.Controls.TabControl Tabs;
        private readonly System.Windows.RoutedEventHandler[] rightClickHandler;

        private readonly string defaultMapNameFilter;

        private bool _bAutoCloseUponSelection = false;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="board"></param>
        /// <param name="Tabs"></param>
        /// <param name="rightClickHandler"></param>
        /// <param name="defaultMapNameFilter">The default text to set for the map name filter</param>
        public FieldSelector(MultiBoard board, System.Windows.Controls.TabControl Tabs, System.Windows.RoutedEventHandler[] rightClickHandler, bool bAutoCloseUponSelection,
            string defaultMapNameFilter = null)
        {
            InitializeComponent();

            DialogResult = DialogResult.Cancel;
            this.multiBoard = board;
            this.Tabs = Tabs;
            this.rightClickHandler = rightClickHandler;
            this._bAutoCloseUponSelection = bAutoCloseUponSelection;
            this.defaultMapNameFilter = defaultMapNameFilter;

            this.searchBox.TextChanged += this.mapBrowser.Search.TextChanged;
        }

        /// <summary>
        /// On Load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Load_Load(object sender, EventArgs e)
        {
            switch (ApplicationSettings.lastRadioIndex)
            {
                case 0:
                    HAMSelect.Checked = true;
                    HAMBox.Text = ApplicationSettings.LastHamPath;
                    break;
                case 1:
                    XMLSelect.Checked = true;
                    XMLBox.Text = ApplicationSettings.LastXmlPath;
                    break;
                case 2:
                    WZSelect.Checked = true;
                    break;
            }
            // Load maplist first
            this.mapBrowser.InitializeMapsListboxItem(true);

            // then load history
            this.mapBrowser_history.InitialiseHistoryListboxItem();

            // after loading
            if (defaultMapNameFilter != null)
            {
                this.searchBox.Focus();
                this.searchBox.Text = defaultMapNameFilter;

                this.mapBrowser.Search.TextChanged(this.searchBox, null);
            }
        }

        /// <summary>
        /// On check uncheck 'town' only filters
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox_townOnly_CheckedChanged(object sender, EventArgs e) {
            // set bool
            this.mapBrowser.TownOnlyFilter = checkBox_townOnly.Checked;

            // search again
            this.mapBrowser.Search.TextChanged(this.searchBox.Text == this.searchBox.WatermarkText ? "" : this.searchBox.Text, null);
        }

        /// <summary>
        /// Clear history map browser
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_clearHistory_Click(object sender, EventArgs e) {
            this.mapBrowser_history.ClearLoadedMapHistory();
        }

        private void SelectionChanged(object sender, EventArgs e)
        {
            if (HAMSelect.Checked)
            {
                ApplicationSettings.lastRadioIndex = 0;
                HAMBox.Enabled = true;
                XMLBox.Enabled = false;
                searchBox.Enabled = false;
                mapBrowser.IsEnabled = false;
                loadButton.Enabled = true;
            }
            else if (XMLSelect.Checked)
            {
                ApplicationSettings.lastRadioIndex = 1;
                HAMBox.Enabled = false;
                XMLBox.Enabled = true;
                searchBox.Enabled = false;
                mapBrowser.IsEnabled = false;
                loadButton.Enabled = XMLBox.Text != "";
            }
            else if (WZSelect.Checked)
            {
                ApplicationSettings.lastRadioIndex = 2;
                HAMBox.Enabled = false;
                XMLBox.Enabled = false;
                searchBox.Enabled = true;
                mapBrowser.IsEnabled = true;
                loadButton.Enabled = mapBrowser.LoadMapEnabled;
            }
        }

        private void BrowseXML_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Select XML to load...";
            dialog.Filter = "eXtensible Markup Language file (*.xml)|*.xml";
            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            XMLBox.Text = dialog.FileName;
            loadButton.Enabled = true;
        }

        private void BrowseHAM_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Select Map to load...";
            dialog.Filter = "HaCreator Map File (*.ham)|*.ham";
            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            HAMBox.Text = dialog.FileName;
            loadButton.Enabled = true;
        }

        /// <summary>
        /// Load map
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadButton_Click(object sender, EventArgs e) {
            OnLoadMap(false);
        }

        /// <summary>
        /// On click of load history map button 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_loadHistory_Click(object sender, EventArgs e) {
            OnLoadMap(true);
        }

        /// <summary>
        /// Loads the map from the selected menu
        /// </summary>
        /// <param name="bFromHistory"></param>
        private void OnLoadMap(bool bFromHistory) {
            //Hide();
            WaitWindow ww = new WaitWindow("Loading...");
            ww.Show();
            Application.DoEvents();

            WzImage mapImage = null;
            int mapid = -1;
            string mapName = null, streetName = "", categoryName = "";
            MapInfo info = null;

            if (!bFromHistory) {
                if (HAMSelect.Checked) {
                    MapLoader.CreateMapFromHam(multiBoard, Tabs, File.ReadAllText(HAMBox.Text), rightClickHandler);
                    DialogResult = DialogResult.OK;
                    ww.EndWait();
                    Close();
                    return;
                }
                else if (XMLSelect.Checked) {
                    try {
                        mapImage = (WzImage)new WzXmlDeserializer(false, null).ParseXML(XMLBox.Text)[0];
                    }
                    catch {
                        MessageBox.Show("Error while loading XML. Aborted.");
                        ww.EndWait();
                        Show();
                        return;
                    }
                    info = new MapInfo(mapImage, mapName, streetName, categoryName);
                }
            }
            
            if (info == null && WZSelect.Checked) {
                string selectedItem = bFromHistory ? mapBrowser_history.SelectedItem : mapBrowser.SelectedItem;

                if (selectedItem == null)
                    return; // racing event

                if (selectedItem.StartsWith("MapLogin")) // MapLogin, MapLogin1, MapLogin2, MapLogin3
                {
                    List<WzDirectory> uiWzDirs = Program.WzManager.GetWzDirectoriesFromBase("ui");
                    foreach (WzDirectory uiWzDir in uiWzDirs) {
                        mapImage = (WzImage)uiWzDir?[selectedItem + ".img"];
                        if (mapImage != null)
                            break;
                    }
                    mapName = streetName = categoryName = selectedItem;
                    info = new MapInfo(mapImage, mapName, streetName, categoryName);
                }
                else if (selectedItem == "CashShopPreview") {
                    List<WzDirectory> uiWzDirs = Program.WzManager.GetWzDirectoriesFromBase("ui");
                    foreach (WzDirectory uiWzDir in uiWzDirs) {
                        mapImage = (WzImage)uiWzDir?["CashShopPreview.img"];
                        if (mapImage != null)
                            break;
                    }
                    mapName = streetName = categoryName = "CashShopPreview";
                    info = new MapInfo(mapImage, mapName, streetName, categoryName);
                }
                else {
                    string mapid_str = selectedItem.Substring(0, 9);
                    int.TryParse(mapid_str, out mapid);

                    if (Program.InfoManager.MapsCache.ContainsKey(mapid_str)) {
                        Tuple<WzImage, string, string, string, MapInfo> loadedMap = Program.InfoManager.MapsCache[mapid_str];

                        mapImage = loadedMap.Item1;
                        mapName = loadedMap.Item2;
                        streetName = loadedMap.Item3;
                        categoryName = loadedMap.Item4;
                        info = loadedMap.Item5;
                    }
                    else {
                        MessageBox.Show("Map is missing.", "Error");
                        return; // map isnt available in Map.wz, despite it being listed on String.wz
                    }
                }
                if (!bFromHistory) {
                    // add to history
                    this.mapBrowser_history.AddLoadedMapToHistory(selectedItem);
                }
            }
            MapLoader.CreateMapFromImage(mapid, mapImage, info, mapName, streetName, categoryName, Tabs, multiBoard, rightClickHandler);

            DialogResult = DialogResult.OK;
            ww.EndWait();

            if (_bAutoCloseUponSelection) {
                Close();
            }
        }

        /// <summary>
        /// On selection changed of the maps on the list
        /// </summary>
        private void MapBrowser_SelectionChanged()
        {
            bool bLoadAvailable = mapBrowser.LoadMapEnabled;

            loadButton.Enabled = mapBrowser.LoadMapEnabled;
        }

        /// <summary>
        /// On selection changed of the maps on the list
        /// </summary>
        private void mapBrowserHistory_OnSelectionChanged() {
            button_loadHistory.Enabled = mapBrowser_history.LoadMapEnabled;
        }

        private void Load_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                LoadButton_Click(null, null);
            }
        }

        private void HAMBox_TextChanged(object sender, EventArgs e)
        {
            ApplicationSettings.LastHamPath = HAMBox.Text;
        }

        private void XMLBox_TextChanged(object sender, EventArgs e)
        {
            ApplicationSettings.LastXmlPath = XMLBox.Text;
        }
    }
}
