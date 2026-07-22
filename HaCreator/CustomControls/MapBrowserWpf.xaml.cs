using HaSharedLibrary.Wz;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HaCreator.CustomControls
{
    public partial class MapBrowserWpf : UserControl
    {
        private bool bLoadMapEnabled = false;
        private bool _bMapsLoaded = false;
        private readonly List<string> maps = new List<string>();
        private ICollectionView visibleMapsView;
        private bool _bTownOnlyFilter = false;
        private bool _bIsHistoryMapBrowser = false;
        private bool _previewPanelVisible = true;
        private string _normalizedSearchText = string.Empty;
        private readonly Dictionary<string, bool> townLookup = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private Queue<string> pendingTownMapIds;
        private DispatcherTimer townScanTimer;
        private int townMatchesSinceRefresh;

        private static readonly string PrimaryHistoryDatabasePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hacreator.db");
        private const string SQLITE_DB_HISTORY_TABLE_NAME = "LoadedMapsHistory";

        public MapBrowserWpf()
        {
            InitializeComponent();
            UpdatePreviewVisibility();
        }

        public bool LoadMapEnabled
        {
            get { return bLoadMapEnabled; }
        }

        public string SelectedItem
        {
            get { return mapNamesBox.SelectedItem as string; }
        }

        public int ItemCount
        {
            get { return mapNamesBox.Items.Count; }
        }

        public bool TownOnlyFilter
        {
            get { return _bTownOnlyFilter; }
            set => ApplyFilters(_normalizedSearchText, value);
        }

        public bool PreviewPanelVisible
        {
            get { return _previewPanelVisible; }
            set
            {
                _previewPanelVisible = value;
                UpdatePreviewVisibility();
            }
        }

        public bool IsHistoryMapBrowser
        {
            get { return _bIsHistoryMapBrowser; }
            set
            {
                _bIsHistoryMapBrowser = value;
                UpdatePreviewVisibility();
            }
        }

        public delegate void MapSelectChangedDelegate();
        public event MapSelectChangedDelegate SelectionChanged;

        public void ApplySearch(string searchText)
        {
            ApplyFilters(searchText, _bTownOnlyFilter);
        }

        public void ApplyFilters(string searchText, bool townOnly)
        {
            string normalizedSearchText = (searchText ?? string.Empty).Trim();
            bool searchChanged = !string.Equals(
                _normalizedSearchText, normalizedSearchText, StringComparison.Ordinal);
            bool townFilterChanged = _bTownOnlyFilter != townOnly;
            if (!searchChanged && !townFilterChanged)
                return;

            _normalizedSearchText = normalizedSearchText;
            _bTownOnlyFilter = townOnly;
            if (townFilterChanged)
            {
                if (townOnly)
                    StartIncrementalTownScan();
                else
                    StopIncrementalTownScan();
            }
            RefreshVisibleItems();
        }

        private static WzImage FindMapImageOnDemand(string mapId)
        {
            if (Program.DataSource == null)
                return null;

            string paddedId = mapId.PadLeft(9, '0');
            string folderNum = paddedId[0].ToString();
            string relativePath = $"Map/Map{folderNum}/{paddedId}.img";
            var mapImage = Program.DataSource.GetImageByPath($"Map/{relativePath}");

            if (mapImage == null)
            {
                mapImage = Program.DataSource.GetImage("Map", $"Map/Map{folderNum}/{paddedId}.img");
            }

            return mapImage;
        }

        public void InitializeMapsListboxItem(bool special)
        {
            if (_bMapsLoaded)
            {
                return;
            }

            _bMapsLoaded = true;

            List<string> mapLogins = new List<string>();
            for (int i = 0; i < 20; i++)
            {
                string imageName = "MapLogin" + (i == 0 ? "" : i.ToString()) + ".img";
                WzObject mapLogin = null;

                if (Program.DataSource != null)
                {
                    mapLogin = Program.DataSource.GetImage("UI", imageName);
                }

                if (mapLogin == null && Program.WzManager != null)
                {
                    List<WzDirectory> uiWzFiles = Program.WzManager.GetWzDirectoriesFromBase("ui");
                    foreach (WzDirectory uiWzFile in uiWzFiles)
                    {
                        mapLogin = uiWzFile?[imageName];
                        if (mapLogin != null)
                            break;
                    }
                }

                if (mapLogin == null)
                    break;

                mapLogins.Add(imageName);
            }

            foreach (KeyValuePair<string, Tuple<WzImage, string, string, string, MapInfo>> map in Program.InfoManager.MapsCache)
            {
                string displayMapNameString = string.Format("{0} - {1} : {2}", map.Key, map.Value.Item2, map.Value.Item3);
                maps.Add(displayMapNameString);
            }

            maps.Sort();

            if (special)
            {
                maps.Insert(0, "CashShopPreview");
                maps.Insert(1, "ITCPreview");

                foreach (string mapLogin in mapLogins)
                    maps.Insert(0, mapLogin.Replace(".img", ""));
            }

            RebuildVisibleItems();
            if (_bTownOnlyFilter)
                StartIncrementalTownScan();
        }

        public void ReloadMapsListboxItem(bool special)
        {
            maps.Clear();
            townLookup.Clear();
            StopIncrementalTownScan();
            mapNamesBox.ItemsSource = null;
            visibleMapsView = null;
            ClearPreview();
            bLoadMapEnabled = false;
            _bMapsLoaded = false;

            InitializeMapsListboxItem(special);
        }

        public void ReloadHistoryListboxItem()
        {
            maps.Clear();
            StopIncrementalTownScan();
            mapNamesBox.ItemsSource = null;
            visibleMapsView = null;
            ClearPreview();
            bLoadMapEnabled = false;
            _bMapsLoaded = false;

            InitialiseHistoryListboxItem();
        }

        public void InitialiseHistoryListboxItem()
        {
            if (_bMapsLoaded)
            {
                return;
            }

            _bMapsLoaded = true;
            HashSet<string> loadedHistoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string databasePath in GetHistoryDatabasePaths(includeMissingPrimary: true))
            {
                using (var connection = new SQLiteConnection(GetSqliteDbConnectionString(databasePath)))
                {
                    connection.Open();
                    EnsureHistoryTable(connection);

                    string sql_select = string.Format("SELECT * FROM {0};", SQLITE_DB_HISTORY_TABLE_NAME);
                    SQLiteCommand command = new SQLiteCommand(sql_select, connection);
                    SQLiteDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        string OpenedMapName = (string)reader["OpenedMapName"];
                        if (string.IsNullOrWhiteSpace(OpenedMapName))
                        {
                            continue;
                        }

                        if (!loadedHistoryNames.Add(OpenedMapName))
                        {
                            continue;
                        }

                        maps.Add(OpenedMapName);

                    }
                }
            }

            RebuildVisibleItems();
        }

        public void AddLoadedMapToHistory(string loadedMapName)
        {
            using (var connection = new SQLiteConnection(GetSqliteDbConnectionString(PrimaryHistoryDatabasePath)))
            {
                connection.Open();
                EnsureHistoryTable(connection);

                string sqlInsert = string.Format("INSERT INTO {0} (OpenedMapName) VALUES (@OpenedMapName)", SQLITE_DB_HISTORY_TABLE_NAME);
                SQLiteCommand insertCommand = new SQLiteCommand(sqlInsert, connection);
                insertCommand.Parameters.AddWithValue("@OpenedMapName", loadedMapName);
                insertCommand.ExecuteNonQuery();
            }

            if (!maps.Contains(loadedMapName))
            {
                maps.Add(loadedMapName);
            }

            RebuildVisibleItems();
        }

        public void ClearLoadedMapHistory()
        {
            foreach (string databasePath in GetHistoryDatabasePaths(includeMissingPrimary: true))
            {
                using (var connection = new SQLiteConnection(GetSqliteDbConnectionString(databasePath)))
                {
                    connection.Open();
                    EnsureHistoryTable(connection);

                    string sql = string.Format("DELETE FROM {0};", SQLITE_DB_HISTORY_TABLE_NAME);
                    SQLiteCommand sqlDelCommand = new SQLiteCommand(sql, connection);
                    sqlDelCommand.ExecuteNonQuery();
                }
            }

            maps.Clear();
            RebuildVisibleItems();
        }

        public bool RemoveSelectedMapFromHistory()
        {
            string selectedItem = SelectedItem;
            if (selectedItem == null)
            {
                return false;
            }

            foreach (string databasePath in GetHistoryDatabasePaths(includeMissingPrimary: true))
            {
                using (var connection = new SQLiteConnection(GetSqliteDbConnectionString(databasePath)))
                {
                    connection.Open();
                    EnsureHistoryTable(connection);

                    string sql = string.Format(
                        "DELETE FROM {0} WHERE Id = (SELECT Id FROM {0} WHERE OpenedMapName = @OpenedMapName LIMIT 1);",
                        SQLITE_DB_HISTORY_TABLE_NAME);
                    SQLiteCommand sqlDelCommand = new SQLiteCommand(sql, connection);
                    sqlDelCommand.Parameters.AddWithValue("@OpenedMapName", selectedItem);
                    sqlDelCommand.ExecuteNonQuery();
                }
            }

            maps.Remove(selectedItem);
            RebuildVisibleItems();
            return true;
        }

        private void RefreshVisibleItems()
        {
            if (mapNamesBox == null)
                return;

            if (visibleMapsView == null)
            {
                RebuildVisibleItems();
                return;
            }

            string selectedItem = SelectedItem;
            visibleMapsView.Refresh();
            RestoreSelection(selectedItem);
        }

        private void RebuildVisibleItems()
        {
            if (mapNamesBox == null)
                return;

            string selectedItem = SelectedItem;
            visibleMapsView = new ListCollectionView(new ArrayList(maps))
            {
                Filter = candidate => candidate is string item && ShouldShowMapItem(item)
            };
            mapNamesBox.ItemsSource = visibleMapsView;
            RestoreSelection(selectedItem);
        }

        private void RestoreSelection(string selectedItem)
        {
            if (!string.IsNullOrEmpty(selectedItem) && mapNamesBox.Items.Contains(selectedItem))
            {
                mapNamesBox.SelectedItem = selectedItem;
            }
            else if (!string.IsNullOrEmpty(_normalizedSearchText) && mapNamesBox.Items.Count > 0)
            {
                mapNamesBox.SelectedIndex = 0;
            }
            else
            {
                ClearPreview();
                bLoadMapEnabled = false;
                SelectionChanged?.Invoke();
            }
        }

        private bool ShouldShowMapItem(string itemName)
        {
            if (!string.IsNullOrEmpty(_normalizedSearchText) &&
                itemName.IndexOf(_normalizedSearchText, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (!_bTownOnlyFilter)
                return true;

            if (!TryGetMapId(itemName, out string mapId))
                return false;

            if (Program.InfoManager.MapsCache.TryGetValue(mapId,
                out Tuple<WzImage, string, string, string, MapInfo> loadedMap))
                return IsKnownTownMap(mapId, loadedMap.Item5);

            return false;
        }

        private bool IsKnownTownMap(string mapId, MapInfo mapInfo)
        {
            if (townLookup.TryGetValue(mapId, out bool isTown))
                return isTown;

            if (mapInfo != null)
            {
                townLookup[mapId] = mapInfo.town;
                return mapInfo.town;
            }

            return false;
        }

        private void StartIncrementalTownScan()
        {
            StopIncrementalTownScan();

            pendingTownMapIds = new Queue<string>(maps.Count);
            foreach (string mapItem in maps)
            {
                if (TryGetMapId(mapItem, out string mapId) && !townLookup.ContainsKey(mapId))
                    pendingTownMapIds.Enqueue(mapId);
            }

            if (pendingTownMapIds.Count == 0)
                return;

            townMatchesSinceRefresh = 0;
            townScanTimer ??= new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1)
            };
            townScanTimer.Tick -= TownScanTimer_Tick;
            townScanTimer.Tick += TownScanTimer_Tick;
            townScanTimer.Start();
        }

        private void StopIncrementalTownScan()
        {
            townScanTimer?.Stop();
            pendingTownMapIds?.Clear();
            pendingTownMapIds = null;
            townMatchesSinceRefresh = 0;
        }

        private void TownScanTimer_Tick(object sender, EventArgs e)
        {
            if (!_bTownOnlyFilter || pendingTownMapIds == null || pendingTownMapIds.Count == 0)
            {
                bool refresh = _bTownOnlyFilter && townMatchesSinceRefresh > 0;
                StopIncrementalTownScan();
                if (refresh)
                    RefreshVisibleItems();
                return;
            }

            string mapId = pendingTownMapIds.Dequeue();
            bool isTown = ResolveTownMap(mapId);
            townLookup[mapId] = isTown;

            if (isTown && ++townMatchesSinceRefresh >= 8)
            {
                townMatchesSinceRefresh = 0;
                RefreshVisibleItems();
            }
        }

        private static bool ResolveTownMap(string mapId)
        {
            WzImage mapImage = null;
            if (Program.InfoManager.MapsCache.TryGetValue(mapId,
                out Tuple<WzImage, string, string, string, MapInfo> loadedMap))
            {
                if (loadedMap.Item5 != null)
                    return loadedMap.Item5.town;

                mapImage = loadedMap.Item1;
            }

            if (mapImage == null)
                mapImage = FindMapImageOnDemand(mapId);

            if (mapImage == null)
                return false;

            bool wasParsed = mapImage.Parsed;
            try
            {
                if (!wasParsed)
                    mapImage.ParseImage();

                return mapImage["info"]?["town"].GetBool() == true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (!wasParsed && mapImage.Parsed && !mapImage.Changed)
                    mapImage.UnparseImage();
            }
        }

        private void UpdatePreviewVisibility()
        {
            if (previewPanel == null || previewRow == null)
            {
                return;
            }

            previewPanel.Visibility = _previewPanelVisible ? Visibility.Visible : Visibility.Collapsed;
            previewRow.Height = _previewPanelVisible ? GridLength.Auto : new GridLength(0);
        }

        private void MapNamesBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedName = SelectedItem;

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
                panel_linkWarning.Visibility = Visibility.Collapsed;
                panel_mapExistWarning.Visibility = Visibility.Collapsed;
                ClearPreview();
                bLoadMapEnabled = selectedName != null;
            }
            else
            {
                if (!TryGetMapId(selectedName, out string mapid))
                {
                    panel_linkWarning.Visibility = Visibility.Collapsed;
                    panel_mapExistWarning.Visibility = Visibility.Visible;
                    ClearPreview();
                    bLoadMapEnabled = false;
                    SelectionChanged?.Invoke();
                    return;
                }

                if (!TryGetCachedMapInfo(selectedName,
                    out Tuple<WzImage, string, string, string, MapInfo> loadedMap))
                {
                    panel_linkWarning.Visibility = Visibility.Collapsed;
                    panel_mapExistWarning.Visibility = Visibility.Visible;
                    ClearPreview();
                    bLoadMapEnabled = _bIsHistoryMapBrowser && Program.InfoManager.MapsCache.ContainsKey(mapid);
                }
                else
                {
                    WzImage mapImage = loadedMap.Item1 ?? FindMapImageOnDemand(mapid);

                    if (mapImage == null)
                    {
                        panel_linkWarning.Visibility = Visibility.Collapsed;
                        panel_mapExistWarning.Visibility = Visibility.Visible;
                        ClearPreview();
                        bLoadMapEnabled = false;
                    }
                    else
                    {
                        using (WzImageResource rsrc = new WzImageResource(mapImage))
                        {
                            if (mapImage["info"]["link"] != null)
                            {
                                panel_linkWarning.Visibility = Visibility.Visible;
                                panel_mapExistWarning.Visibility = Visibility.Collapsed;
                                label_linkMapId.Text = mapImage["info"]["link"].ToString();

                                ClearPreview();
                                bLoadMapEnabled = false;
                            }
                            else
                            {
                                panel_linkWarning.Visibility = Visibility.Collapsed;
                                panel_mapExistWarning.Visibility = Visibility.Collapsed;

                                WzCanvasProperty minimap = (WzCanvasProperty)mapImage.GetFromPath("miniMap/canvas");
                                if (minimap != null)
                                {
                                    using (Bitmap minimapBitmap = minimap.GetLinkedWzCanvasBitmap())
                                    {
                                        minimapBox.Source = BitmapToImageSource(minimapBitmap);
                                    }
                                }
                                else
                                {
                                    ClearPreview();
                                }

                                bLoadMapEnabled = true;
                            }
                        }
                    }
                }
            }

            SelectionChanged?.Invoke();
        }

        private void ClearPreview()
        {
            if (minimapBox != null)
            {
                minimapBox.Source = null;
            }
        }

        private static IEnumerable<string> GetHistoryDatabasePaths(bool includeMissingPrimary)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (includeMissingPrimary || File.Exists(PrimaryHistoryDatabasePath))
            {
                paths.Add(PrimaryHistoryDatabasePath);
            }

            string workingDirectoryPath = Path.Combine(Environment.CurrentDirectory, "hacreator.db");
            if (File.Exists(workingDirectoryPath))
            {
                paths.Add(workingDirectoryPath);
            }

            return paths;
        }

        private static string GetSqliteDbConnectionString(string databasePath)
        {
            SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder
            {
                DataSource = databasePath,
                Version = 3
            };

            return builder.ToString();
        }

        private static void EnsureHistoryTable(SQLiteConnection connection)
        {
            string sqlCreate =
                string.Format(
                    "CREATE TABLE IF NOT EXISTS {0} (" +
                    "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
                    "OpenedMapName TEXT);", SQLITE_DB_HISTORY_TABLE_NAME);

            using (SQLiteCommand command = new SQLiteCommand(sqlCreate, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private static bool TryGetCachedMapInfo(string displayMapName, out Tuple<WzImage, string, string, string, MapInfo> loadedMap)
        {
            loadedMap = null;

            if (string.IsNullOrEmpty(displayMapName) || displayMapName.Length < 9)
            {
                return false;
            }

            string mapId = displayMapName.Substring(0, 9);
            return Program.InfoManager.MapsCache.TryGetValue(mapId, out loadedMap);
        }

        private static bool TryGetMapId(string displayMapName, out string mapId)
        {
            mapId = null;

            if (string.IsNullOrEmpty(displayMapName) || displayMapName.Length < 9)
            {
                return false;
            }

            string candidate = displayMapName.Substring(0, 9);
            if (!candidate.All(char.IsDigit))
            {
                return false;
            }

            mapId = candidate;
            return true;
        }

        private static bool IsRegularMapSelection(string selectedName)
        {
            return !string.IsNullOrEmpty(selectedName) &&
                   selectedName.Length >= 9 &&
                   selectedName.Substring(0, 9).All(char.IsDigit);
        }

        private static BitmapSource BitmapToImageSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
