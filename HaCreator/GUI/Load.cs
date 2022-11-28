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


namespace HaCreator.GUI
{
    public partial class Load : System.Windows.Forms.Form
    {
        private readonly MultiBoard multiBoard;
        private readonly System.Windows.Controls.TabControl Tabs;
        private readonly System.Windows.RoutedEventHandler[] rightClickHandler;

        private readonly string defaultMapNameFilter;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="board"></param>
        /// <param name="Tabs"></param>
        /// <param name="rightClickHandler"></param>
        /// <param name="defaultMapNameFilter">The default text to set for the map name filter</param>
        public Load(MultiBoard board, System.Windows.Controls.TabControl Tabs, System.Windows.RoutedEventHandler[] rightClickHandler,
            string defaultMapNameFilter = null)
        {
            InitializeComponent();

            DialogResult = DialogResult.Cancel;
            this.multiBoard = board;
            this.Tabs = Tabs;
            this.rightClickHandler = rightClickHandler;
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
                    if (Initialization.isClient64()) {
                        mapImage = (WzImage)Program.WzManager["ui_000"][selectedName + ".img"];
                    }
                    else
                    {
                        mapImage = (WzImage)Program.WzManager["ui"][selectedName + ".img"];
                    }
                    mapName = streetName = categoryName = selectedName;
                }
                else if (mapBrowser.SelectedItem == "CashShopPreview")
                {
                    if (Initialization.isClient64())
                    {
                        mapImage = (WzImage)Program.WzManager["ui_000"]["CashShopPreview.img"];
                    }
                    else
                    {
                        mapImage = (WzImage)Program.WzManager["ui"]["CashShopPreview.img"];
                    }
                    mapName = streetName = categoryName = "CashShopPreview";
                }
                else
                {
                    string mapid_str = mapBrowser.SelectedItem.Substring(0, 9);
                    int.TryParse(mapid_str, out mapid);
                    string mapcat;
                    WzDirectory directory;
                    if (Initialization.isClient64())
                    {
                        Console.WriteLine("mapid_str -> " + mapid_str);
                        mapcat = mapid_str.Substring(0, 1);
                        mapImage = Program.WzManager.FindMapImage(mapid_str, mapcat);
                    } else
                    {
                        mapcat = "Map" + mapid_str.Substring(0, 1);
                        directory = Program.WzManager.FindMapWz(mapcat);
                        mapImage = (WzImage)directory[mapid_str + ".img"];
                    }
                    strMapProp = WzInfoTools.GetMapStringProp(mapid_str);
                    mapName = WzInfoTools.GetMapName(strMapProp);
                    streetName = WzInfoTools.GetMapStreetName(strMapProp);
                    categoryName = WzInfoTools.GetMapCategoryName(strMapProp);
                }
            }
            MapLoader.CreateMapFromImage(mapid, mapImage, mapName, streetName, categoryName, strMapProp, Tabs, multiBoard, rightClickHandler);

            DialogResult = DialogResult.OK;
            ww.EndWait();
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
