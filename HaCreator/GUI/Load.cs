//uncomment the line below to create a space-time tradeoff (saving RAM by wasting more CPU cycles)
#define SPACETIME

using System;
using System.Windows.Forms;
using System.IO;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapEditor;
using HaCreator.Wz;
using System.Collections.Generic;
using System.Linq;
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
        /// Loads a map image on-demand from the data source.
        /// This is used when WzImage was not stored in MapsCache to save memory.
        /// </summary>
        /// <param name="mapId">The 9-digit map ID</param>
        /// <returns>The loaded WzImage or null if not found</returns>
        private WzImage LoadMapImageOnDemand(string mapId)
        {
            if (Program.DataSource == null)
                return null;

            string paddedId = mapId.PadLeft(9, '0');
            string folderNum = paddedId[0].ToString();

            // Try to load from Map/Map/MapX/mapid.img
            string relativePath = $"Map/Map{folderNum}/{paddedId}.img";
            var mapImage = Program.DataSource.GetImageByPath($"Map/{relativePath}");

            if (mapImage == null)
            {
                // Try without extra Map/ prefix
                mapImage = Program.DataSource.GetImage("Map", $"Map/Map{folderNum}/{paddedId}.img");
            }

            if (mapImage != null)
                mapImage.ParseImage();

            return mapImage;
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
            button_deleteSelected.Enabled = false;
        }

        /// <summary>
        /// Delete the selected map from history
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_deleteSelected_Click(object sender, EventArgs e) {
            this.mapBrowser_history.RemoveSelectedMapFromHistory();
            button_deleteSelected.Enabled = mapBrowser_history.LoadMapEnabled;
            button_loadHistory.Enabled = mapBrowser_history.LoadMapEnabled;
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
                    foreach (WzDirectory uiWzDir in uiWzDirs)
                    {
                        mapImage = (WzImage)uiWzDir?[selectedItem + ".img"];
                        if (mapImage != null)
                            break;
                    }
                    mapName = streetName = categoryName = selectedItem;
                    info = new MapInfo(mapImage, mapName, streetName, categoryName);
                }
                else if (selectedItem == "CashShopPreview")
                {
                    List<WzDirectory> uiWzDirs = Program.WzManager.GetWzDirectoriesFromBase("ui");
                    foreach (WzDirectory uiWzDir in uiWzDirs)
                    {
                        mapImage = (WzImage)uiWzDir?["CashShopPreview.img"];
                        if (mapImage != null)
                            break;
                    }
                    mapName = streetName = categoryName = "CashShopPreview";
                    info = new MapInfo(mapImage, mapName, streetName, categoryName);
                }
                else if (selectedItem == "ITCPreview")
                {
                    var uiWzDirs = Program.WzManager.GetWzDirectoriesFromBase("ui");
                    foreach (var uiWzDir in uiWzDirs)
                    {
                        mapImage = (WzImage)uiWzDir?["ITCPreview.img"];
                        if (mapImage != null)
                            break;
                    }
                    mapName = streetName = categoryName = "ITCPreview";
                    info = new MapInfo(mapImage, mapName, streetName, categoryName);
                }
                else
                {
                    string mapid_str = selectedItem.Substring(0, 9);
                    int.TryParse(mapid_str, out mapid);

                    if (Program.InfoManager.MapsCache.ContainsKey(mapid_str))
                    {
                        Tuple<WzImage, string, string, string, MapInfo> loadedMap = Program.InfoManager.MapsCache[mapid_str];

                        mapImage = loadedMap.Item1;
                        mapName = loadedMap.Item2;
                        streetName = loadedMap.Item3;
                        categoryName = loadedMap.Item4;
                        info = loadedMap.Item5;

                        // Load WzImage on-demand if null (memory optimization)
                        if (mapImage == null)
                        {
                            mapImage = LoadMapImageOnDemand(mapid_str);
                        }
                        if (mapImage == null)
                        {
                            MessageBox.Show("Failed to load map image.", "Error");
                            return;
                        }

                        // Create MapInfo on-demand if null (memory optimization)
                        if (info == null)
                        {
                            info = new MapInfo(mapImage, streetName, mapName, categoryName);
                        }
                    }
                    else
                    {
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
            button_deleteSelected.Enabled = mapBrowser_history.SelectedItem != null;
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

        private void button_resolveMissingMaps_Click(object sender, EventArgs e)
        {
            WaitWindow waitWindow = new WaitWindow("Resolving missing map strings...");
            waitWindow.Show();
            Application.DoEvents();

            try
            {
                MissingMapResolutionResult result = ResolveMissingMapStringEntries();

                mapBrowser.ReloadMapsListboxItem(true);
                mapBrowser.Search.TextChanged(searchBox, EventArgs.Empty);
                MapBrowser_SelectionChanged();

                ShowScrollableMessage("Resolve Missing Maps", BuildResolutionSummaryMessage(result));
            }
            catch (Exception ex)
            {
                ShowScrollableMessage("Resolve Missing Maps", $"Failed to resolve missing map strings.\r\n\r\n{ex}");
            }
            finally
            {
                waitWindow.EndWait();
            }
        }

        private MissingMapResolutionResult ResolveMissingMapStringEntries()
        {
            WzImage stringMapImage = GetStringMapImage();
            if (stringMapImage == null)
            {
                throw new InvalidOperationException("String.wz/Map.img could not be found.");
            }

            stringMapImage.ParseImage();

            Dictionary<string, WzSubProperty> stringCategories = stringMapImage.WzProperties
                .OfType<WzSubProperty>()
                .ToDictionary(category => category.Name, category => category, System.StringComparer.OrdinalIgnoreCase);

            HashSet<string> existingMapIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (WzSubProperty category in stringCategories.Values)
            {
                foreach (WzSubProperty mapEntry in category.WzProperties.OfType<WzSubProperty>())
                {
                    existingMapIds.Add(WzInfoTools.AddLeadingZeros(mapEntry.Name, 9));
                }
            }

            MissingMapResolutionResult result = new MissingMapResolutionResult();
            foreach (WzImage mapImage in EnumerateAllMapImages())
            {
                string mapId = Path.GetFileNameWithoutExtension(mapImage.Name);
                if (string.IsNullOrEmpty(mapId))
                {
                    continue;
                }

                if (existingMapIds.Contains(mapId))
                {
                    result.AlreadyPresentMapIds.Add(mapId);
                    continue;
                }

                if (HasPositiveLinkTarget(mapImage))
                {
                    continue;
                }

                string categoryName = GetGeneratedMapCategoryName(mapImage);
                if (!stringCategories.TryGetValue(categoryName, out WzSubProperty stringCategory))
                {
                    stringCategory = new WzSubProperty(categoryName);
                    stringMapImage.AddProperty(stringCategory);
                    stringCategories[categoryName] = stringCategory;
                }

                WzSubProperty mapEntry = new WzSubProperty(mapId);
                mapEntry.AddProperty(new WzStringProperty("streetName", "NO NAME"));
                mapEntry.AddProperty(new WzStringProperty("mapName", "NO NAME"));
                stringCategory.AddProperty(mapEntry);

                Program.InfoManager.MapsNameCache[mapId] = new Tuple<string, string, string>("NO NAME", "NO NAME", categoryName);

                if (mapImage["info"] != null)
                {
                    MapInfo info = new MapInfo(mapImage, "NO NAME", "NO NAME", categoryName);
                    Program.InfoManager.MapsCache[mapId] = new Tuple<WzImage, string, string, string, MapInfo>(
                        mapImage,
                        "NO NAME",
                        "NO NAME",
                        categoryName,
                        info);
                }

                existingMapIds.Add(mapId);
                result.AddedMapIds.Add(mapId);
            }

            if (result.AddedMapIds.Count > 0)
            {
                stringMapImage.Changed = true;

                if (ShouldConfirmImmediateStringMapSave() &&
                    MessageBox.Show(
                        "Saving is immediate for the current data source.\r\n\r\nYes: write String/Map.img now.\r\nNo: keep it modified in memory and save it manually later.",
                        "Resolve Missing Maps",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Program.DataSource?.MarkImageUpdated("String", stringMapImage);
                }
            }

            result.AlreadyPresentMapIds.Sort(System.StringComparer.OrdinalIgnoreCase);
            result.AddedMapIds.Sort(System.StringComparer.OrdinalIgnoreCase);

            return result;
        }

        private IEnumerable<WzImage> EnumerateAllMapImages()
        {
            HashSet<string> seenMapIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            if (Program.DataSource != null)
            {
                foreach (string subDirectory in Program.DataSource.GetSubdirectories("Map"))
                {
                    if (!IsMapImageDirectory(subDirectory))
                    {
                        continue;
                    }

                    foreach (WzImage mapImage in Program.DataSource.GetImagesInDirectory("Map", subDirectory))
                    {
                        string mapId = Path.GetFileNameWithoutExtension(mapImage.Name);
                        if (string.IsNullOrEmpty(mapId) || !seenMapIds.Add(mapId))
                        {
                            continue;
                        }

                        yield return mapImage;
                    }
                }

                yield break;
            }

            foreach (WzDirectory rootDirectory in GetMapRootsFromWzManager())
            {
                foreach (WzImage mapImage in EnumerateMapImagesRecursive(rootDirectory))
                {
                    string mapId = Path.GetFileNameWithoutExtension(mapImage.Name);
                    if (!seenMapIds.Add(mapId))
                    {
                        continue;
                    }

                    yield return mapImage;
                }
            }
        }

        private IEnumerable<WzImage> EnumerateMapImagesRecursive(WzDirectory directory)
        {
            if (directory == null)
            {
                yield break;
            }

            foreach (WzImage image in directory.WzImages)
            {
                string mapId = Path.GetFileNameWithoutExtension(image.Name);
                if (image.Name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) &&
                    mapId.Length == 9 &&
                    mapId.All(char.IsDigit))
                {
                    yield return image;
                }
            }

            foreach (WzDirectory childDirectory in directory.WzDirectories)
            {
                foreach (WzImage image in EnumerateMapImagesRecursive(childDirectory))
                {
                    yield return image;
                }
            }
        }

        private static string GetGeneratedMapCategoryName(WzImage mapImage)
        {
            WzObject current = mapImage.Parent;
            while (current != null)
            {
                if (current is WzDirectory directory &&
                    directory.Name.Length == 4 &&
                    directory.Name.StartsWith("Map", StringComparison.OrdinalIgnoreCase) &&
                    char.IsDigit(directory.Name[3]))
                {
                    return directory.Name;
                }

                current = current.Parent;
            }

            return "AutoGenerated";
        }

        private WzImage GetStringMapImage()
        {
            WzImage stringMapImage = Program.DataSource?.GetImage("String", "Map.img");
            if (stringMapImage == null)
            {
                stringMapImage = Program.DataSource?.GetImageByPath("String/Map.img");
            }
            if (stringMapImage == null)
            {
                stringMapImage = (WzImage)Program.WzManager?.FindWzImageByName("string", "Map.img");
            }

            return stringMapImage;
        }

        private IEnumerable<WzDirectory> GetMapRootsFromWzManager()
        {
            foreach (WzDirectory rootDirectory in Program.WzManager.GetWzDirectoriesFromBase("map"))
            {
                if (rootDirectory == null)
                {
                    continue;
                }

                WzDirectory mapDirectory = rootDirectory["Map"] as WzDirectory;
                yield return mapDirectory ?? rootDirectory;
            }
        }

        private static bool IsMapImageDirectory(string subDirectory)
        {
            if (string.IsNullOrEmpty(subDirectory))
            {
                return false;
            }

            string[] segments = subDirectory
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 1)
            {
                return IsMapCategorySegment(segments[0]);
            }

            if (segments.Length == 2 && segments[0].Equals("Map", StringComparison.OrdinalIgnoreCase))
            {
                return IsMapCategorySegment(segments[1]);
            }

            return false;
        }

        private static bool IsMapCategorySegment(string segment)
        {
            return !string.IsNullOrEmpty(segment) &&
                   segment.Length == 4 &&
                   segment.StartsWith("Map", StringComparison.OrdinalIgnoreCase) &&
                   char.IsDigit(segment[3]);
        }

        private static bool HasPositiveLinkTarget(WzImage mapImage)
        {
            WzImageProperty linkProperty = mapImage.GetFromPath("info/link");
            if (linkProperty == null)
            {
                return false;
            }

            try
            {
                return linkProperty.GetInt() > 0;
            }
            catch
            {
                return false;
            }
        }

        private bool ShouldConfirmImmediateStringMapSave()
        {
            if (Program.DataSource is ImgFileSystemDataSource)
            {
                return true;
            }

            if (Program.DataSource is HybridDataSource hybridDataSource && hybridDataSource.ImgSource != null)
            {
                return true;
            }

            return false;
        }

        private static string BuildResolutionSummaryMessage(MissingMapResolutionResult result)
        {
            List<string> lines = new List<string>
            {
                $"Added {result.AddedMapIds.Count} new entr{(result.AddedMapIds.Count == 1 ? "y" : "ies")}.",
                $"Already present in memory: {result.AlreadyPresentMapIds.Count}.",
            };

            if (result.AddedMapIds.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Added:");
                lines.AddRange(result.AddedMapIds);
            }

            if (result.AddedMapIds.Count == 0 && result.AlreadyPresentMapIds.Count == 0)
            {
                lines.Add(string.Empty);
                lines.Add("No maps were discovered under Map.wz\\Map\\Map*.");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void ShowScrollableMessage(string title, string message)
        {
            using (Form dialog = new Form())
            using (TextBox messageBox = new TextBox())
            using (Button okButton = new Button())
            {
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new System.Drawing.Size(720, 520);
                dialog.MinimumSize = new System.Drawing.Size(500, 320);
                dialog.FormBorderStyle = FormBorderStyle.SizableToolWindow;

                messageBox.Multiline = true;
                messageBox.ReadOnly = true;
                messageBox.ScrollBars = ScrollBars.Both;
                messageBox.WordWrap = false;
                messageBox.Dock = DockStyle.Fill;
                messageBox.Font = new System.Drawing.Font("Consolas", 9F);
                messageBox.Text = message;

                okButton.Text = "OK";
                okButton.Dock = DockStyle.Bottom;
                okButton.Height = 30;
                okButton.DialogResult = DialogResult.OK;

                dialog.Controls.Add(messageBox);
                dialog.Controls.Add(okButton);
                dialog.AcceptButton = okButton;

                dialog.ShowDialog(this);
            }
        }

        private sealed class MissingMapResolutionResult
        {
            public List<string> AddedMapIds { get; } = new List<string>();
            public List<string> AlreadyPresentMapIds { get; } = new List<string>();
        }
    }
}
