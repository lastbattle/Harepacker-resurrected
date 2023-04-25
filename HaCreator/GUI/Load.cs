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
using MapleLib.WzLib.Serialization;
using System.Collections.Generic;
using HaSharedLibrary.Wz;

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

            this.searchBox.TextChanged += this.mapBrowser.searchBox_TextChanged;
        }

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
            this.mapBrowser.InitializeMaps(true);

            // after loading
            if (defaultMapNameFilter != null)
            {
                this.searchBox.Focus();
                this.searchBox.Text = defaultMapNameFilter;

                this.mapBrowser.searchBox_TextChanged(this.searchBox, null);
            }
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
                loadButton.Enabled = mapBrowser.LoadAvailable;
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
        private void LoadButton_Click(object sender, EventArgs e)
        {
            //Hide();
            WaitWindow ww = new WaitWindow("Loading...");
            ww.Show();
            Application.DoEvents();

            WzImage mapImage = null;
            int mapid = -1;
            string mapName = null, streetName = "", categoryName = "";
            WzSubProperty strMapProp = null;


            if (HAMSelect.Checked)
            {
                MapLoader.CreateMapFromHam(multiBoard, Tabs, File.ReadAllText(HAMBox.Text), rightClickHandler);
                DialogResult = DialogResult.OK;
                ww.EndWait();
                Close();
                return;
            }
            else if (XMLSelect.Checked)
            {
                try
                {
                    mapImage = (WzImage)new WzXmlDeserializer(false, null).ParseXML(XMLBox.Text)[0];
                }
                catch
                {
                    MessageBox.Show("Error while loading XML. Aborted.");
                    ww.EndWait();
                    Show();
                    return;
                }
            }
            else if (WZSelect.Checked)
            {
                if (mapBrowser.SelectedItem == null)
                    return; // racing event

                string selectedName = mapBrowser.SelectedItem;

                if (selectedName.StartsWith("MapLogin")) // MapLogin, MapLogin1, MapLogin2, MapLogin3
                {
                    List<WzDirectory> uiWzDirs = Program.WzManager.GetWzDirectoriesFromBase("ui");
                    foreach (WzDirectory uiWzDir in uiWzDirs)
                    {
                        mapImage = (WzImage) uiWzDir?[selectedName + ".img"];
                        if (mapImage != null)
                            break;
                    }
                    mapName = streetName = categoryName = selectedName;
                }
                else if (mapBrowser.SelectedItem == "CashShopPreview")
                {
                    List<WzDirectory> uiWzDirs = Program.WzManager.GetWzDirectoriesFromBase("ui");
                    foreach (WzDirectory uiWzDir in uiWzDirs)
                    {
                        mapImage = (WzImage) uiWzDir?["CashShopPreview.img"];
                        if (mapImage != null)
                            break;
                    }
                    mapName = streetName = categoryName = "CashShopPreview";
                }
                else
                {
                    string mapid_str = mapBrowser.SelectedItem.Substring(0, 9);
                    int.TryParse(mapid_str, out mapid);

                    mapImage = WzInfoTools.FindMapImage(mapid.ToString(), Program.WzManager);

                    strMapProp = WzInfoTools.GetMapStringProp(mapid_str, Program.WzManager);
                    mapName = WzInfoTools.GetMapName(strMapProp);
                    streetName = WzInfoTools.GetMapStreetName(strMapProp);
                    categoryName = WzInfoTools.GetMapCategoryName(strMapProp);
                }
            }
            MapLoader.CreateMapFromImage(mapid, mapImage, mapName, streetName, categoryName, strMapProp, Tabs, multiBoard, rightClickHandler);

            DialogResult = DialogResult.OK;
            ww.EndWait();

            if (_bAutoCloseUponSelection)
                Close();
        }

        private void MapBrowser_SelectionChanged()
        {
            bool bLoadAvailable = mapBrowser.LoadAvailable;

            loadButton.Enabled = mapBrowser.LoadAvailable;
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
