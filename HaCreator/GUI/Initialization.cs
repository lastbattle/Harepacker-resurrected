using System;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using MapleLib.WzLib;
using HaCreator.MapEditor;
using MapleLib.WzLib.Util;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure;
using MapleLib.Helpers;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Info;
using MapleLib.WzLib.WzProperties;
using System.Drawing;
using HaSharedLibrary.Wz;
using MapleLib;
using System.Threading.Tasks;
using System.Diagnostics;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.Img;

namespace HaCreator.GUI
{
    public partial class Initialization : System.Windows.Forms.Form
    {
        public HaEditor editor = null;

        private static WzMapleVersion _wzMapleVersion = WzMapleVersion.BMS; // Default to BMS, the enc version to use when decrypting the WZ files.
        public static WzMapleVersion WzMapleVersion
        {
            get { return _wzMapleVersion; }
            private set { }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public Initialization()
        {
            InitializeComponent();

            // Subscribe to hot swap events for IMG versions
            SubscribeToHotSwapEvents();
        }

        private bool IsPathCommon(string path)
        {
            foreach (string commonPath in WzFileManager.COMMON_MAPLESTORY_DIRECTORY)
            {
                if (commonPath == path)
                    return true;
            }
            return false;
        }

        private bool _bIsInitialising = false;

        /// <summary>
        /// Unified Initialize button - works based on active tab
        /// </summary>
        private void button_initialise_Click(object sender, EventArgs e)
        {
            if (_bIsInitialising)
            {
                return;
            }
            _bIsInitialising = true;

            try
            {
                if (tabControl_dataSource.SelectedTab == tabPage_wzFiles)
                {
                    // WZ Files initialization
                    InitializeFromWzFiles();
                }
                else if (tabControl_dataSource.SelectedTab == tabPage_imgVersions)
                {
                    // IMG version initialization
                    InitializeFromSelectedImgVersion();
                }
            }
            finally
            {
                _bIsInitialising = false;
            }
        }

        /// <summary>
        /// Initialize from WZ files (original initialization logic)
        /// </summary>
        private void InitializeFromWzFiles()
        {
            ApplicationSettings.MapleVersionIndex = versionBox.SelectedIndex;
            ApplicationSettings.MapleFolderIndex = pathBox.SelectedIndex;
            ApplicationSettings.MapleStoryClientLocalisation = (int)comboBox_localisation.SelectedValue;

            string wzPath = pathBox.Text;

            // MapleStoryDataFolder
            if (wzPath == "Select MapleStory Folder")
            {
                MessageBox.Show("Please select the MapleStory folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!ApplicationSettings.MapleFoldersList.Contains(wzPath) && !IsPathCommon(wzPath))
            {
                ApplicationSettings.MapleFoldersList = ApplicationSettings.MapleFoldersList == "" ? wzPath : (ApplicationSettings.MapleFoldersList + "," + wzPath);
            }
            WzMapleVersion fileVersion = (WzMapleVersion)versionBox.SelectedIndex;

            // Save the data source mode
            var config = HaCreatorConfig.Load();
            config.DataSourceMode = DataSourceMode.WzFiles;
            config.Save();

            if (InitializeWzFilesInternal(wzPath, fileVersion, false))
            {
                Hide();
                Application.DoEvents();
                editor = new HaEditor();
                editor.ShowDialog();

                Application.Exit();
            }
        }

        /// <summary>
        /// Initialize from selected IMG version in the list
        /// </summary>
        private void InitializeFromSelectedImgVersion()
        {
            if (listBox_imgVersions.SelectedItem is VersionListItem item)
            {
                var selectedVersion = item.Version;

                // Save to config for next time
                var config = HaCreatorConfig.Load();
                config.LastUsedVersion = selectedVersion.Version;
                config.DataSourceMode = DataSourceMode.ImgFileSystem;
                config.AddToRecentVersionPaths(selectedVersion.DirectoryPath);
                config.Save();

                if (InitializeFromImgFileSystem(selectedVersion))
                {
                    Hide();
                    Application.DoEvents();
                    try
                    {
                        editor = new HaEditor();
                        editor.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error showing editor:\n{ex.Message}\n\n{ex.StackTrace}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    Application.Exit();
                }
            }
            else
            {
                MessageBox.Show("Please select a version from the list.", "No Version Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Initialize from an IMG filesystem version
        /// </summary>
        private bool InitializeFromImgFileSystem(VersionInfo version)
        {
            try
            {
                UpdateUI_CurrentLoadingWzFile("Creating data source...", false);

                // Dispose old managers
                if (Program.WzManager != null)
                {
                    Program.WzManager.Dispose();
                    Program.WzManager = null;
                }
                if (Program.DataSource != null)
                {
                    Program.DataSource.Dispose();
                    Program.DataSource = null;
                }
                if (Program.InfoManager != null)
                {
                    Program.InfoManager.Clear();
                }

                // Create data source from version
                Program.DataSource = Program.StartupManager.CreateDataSource(version);

                // Parse encryption from version info
                if (Enum.TryParse<WzMapleVersion>(version.Encryption, out var mapleVersion))
                {
                    _wzMapleVersion = mapleVersion;
                }

                UpdateUI_CurrentLoadingWzFile("Extracting game data...", false);

                // Use ImgDataExtractor to populate InfoManager
                var extractor = new ImgDataExtractor(Program.DataSource, Program.InfoManager);
                extractor.ProgressChanged += (s, args) =>
                {
                    UpdateUI_CurrentLoadingWzFile(args.Message, false);
                };

                extractor.ExtractAll();

                // Set image format detection flag for pre-Big Bang compatibility
                // DXT formats (Format3, Format1026, Format2050) are not supported by pre-BB clients
                ImageFormatDetector.UsePreBigBangImageFormats = Program.IsPreBBDataWzFormat;

                UpdateUI_CurrentLoadingWzFile("Initialization complete.", false);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing from IMG filesystem:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Initialise the WZ files with the provided folder path
        /// </summary>
        private bool InitializeWzFilesInternal(string wzPath, WzMapleVersion fileVersion, bool bFromImgFile)
        {
            // Check if directory exist
            if (!Directory.Exists(wzPath))
            {
                MessageBox.Show(string.Format(Properties.Resources.Initialization_Error_MSDirectoryNotExist, wzPath), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (Program.WzManager != null)
            {
                Program.WzManager.Dispose();
                Program.WzManager = null; // old loaded items
            }
            if (Program.InfoManager != null)
            {
                Program.InfoManager.Clear();
            }

            _wzMapleVersion = fileVersion; // set version to static vars

            Program.WzManager = new WzFileManager(wzPath, false);
            Program.WzManager.BuildWzFileList(); // builds the list of WZ files in the directories (for HaCreator)

            // for old maplestory with only Data.wz
            if (Program.WzManager.IsPreBBDataWzFormat) //currently always false
            {
                UpdateUI_CurrentLoadingWzFile("Data.wz", true);

                try
                {
                    Program.WzManager.LoadLegacyDataWzFile("Data", _wzMapleVersion);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error initializing data.wz (" + e.Message + ").\r\nCheck that the directory is valid and the file is not in use.");
                    return false;
                }

                ExtractStringFile(true);

                ExtractMobFile();
                ExtractNpcFile();
                ExtractReactorFile();
                ExtractSoundFile();
                ExtractQuestFile();
                ExtractSkillFile();
                ExtractItemFile();
                ExtractMapMarks();
                ExtractMapPortals();
                ExtractMapTileSets();
                ExtractMapObjSets();
                ExtractMapBackgroundSets();

                ExtractMaps();

                ImageFormatDetector.UsePreBigBangImageFormats = true;
            }
            else // for versions beyond v30x
            {
                Program.WzManager.LoadListWzFile(_wzMapleVersion);

                UpdateUI_CurrentLoadingWzFile("encrypted .ms file(s).", false);
                Program.WzManager.LoadPacksFiles();

                // String.wz
                const string STRING_PATH = "string";
                List<string> stringWzFiles = Program.WzManager.GetWzFileNameListFromBase(STRING_PATH);
                foreach (string stringWzFileName in stringWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(stringWzFileName, true);
                    Program.WzManager.LoadWzFile(stringWzFileName, _wzMapleVersion);
                }
                ExtractStringFile(false);
                LoadCanvasSection(STRING_PATH);

                // Mob WZ
                const string MOB_PATH = "mob";
                List<string> mobWzFiles = Program.WzManager.GetWzFileNameListFromBase(MOB_PATH);
                foreach (string mobWZFile in mobWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(mobWZFile, true);
                    Program.WzManager.LoadWzFile(mobWZFile, _wzMapleVersion);
                }
                ExtractMobFile();
                LoadCanvasSection(MOB_PATH);

                // Load Npc
                const string NPC_PATH = "npc";
                List<string> npcWzFiles = Program.WzManager.GetWzFileNameListFromBase(NPC_PATH);
                foreach (string npc in npcWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(npc, true);
                    Program.WzManager.LoadWzFile(npc, _wzMapleVersion);
                }
                ExtractNpcFile();
                LoadCanvasSection(NPC_PATH);

                // Load reactor
                const string REACTOR_PATH = "reactor";
                List<string> reactorWzFiles = Program.WzManager.GetWzFileNameListFromBase(REACTOR_PATH);
                foreach (string reactor in reactorWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(reactor, true);
                    Program.WzManager.LoadWzFile(reactor, _wzMapleVersion);
                }
                ExtractReactorFile();
                LoadCanvasSection(REACTOR_PATH);

                // Load sound
                const string SOUND_PATH = "sound";
                List<string> soundWzDirs = Program.WzManager.GetWzFileNameListFromBase(SOUND_PATH);
                foreach (string soundDirName in soundWzDirs)
                {
                    UpdateUI_CurrentLoadingWzFile(soundDirName, true);
                    Program.WzManager.LoadWzFile(soundDirName, _wzMapleVersion);
                }
                ExtractSoundFile();
                LoadCanvasSection(SOUND_PATH);

                // Load quests
                const string QUEST_PATH = "quest";
                List<string> questWzDirs = Program.WzManager.GetWzFileNameListFromBase(QUEST_PATH);
                foreach (string questWzDir in questWzDirs)
                {
                    UpdateUI_CurrentLoadingWzFile(questWzDir, true);
                    Program.WzManager.LoadWzFile(questWzDir, _wzMapleVersion);
                }
                ExtractQuestFile();
                LoadCanvasSection(QUEST_PATH);

                // Load character
                const string CHARACTER_PATH = "character";
                List<string> characterWzDirs = Program.WzManager.GetWzFileNameListFromBase(CHARACTER_PATH);
                foreach (string characterWzDir in characterWzDirs)
                {
                    UpdateUI_CurrentLoadingWzFile(characterWzDir, true);
                    Program.WzManager.LoadWzFile(characterWzDir, _wzMapleVersion);
                }
                LoadCanvasSection(CHARACTER_PATH);

                // Load skills
                const string SKILL_PATH = "skill";
                List<string> skillWzDirs = Program.WzManager.GetWzFileNameListFromBase(SKILL_PATH);
                foreach (string skillWzDir in skillWzDirs)
                {
                    UpdateUI_CurrentLoadingWzFile(skillWzDir, true);
                    Program.WzManager.LoadWzFile(skillWzDir, _wzMapleVersion);
                }
                ExtractSkillFile();
                LoadCanvasSection(SKILL_PATH);

                // Load Items
                const string ITEM_PATH = "item";
                List<string> itemWzDirs = Program.WzManager.GetWzFileNameListFromBase(ITEM_PATH);
                foreach (string itemWzDir in itemWzDirs)
                {
                    UpdateUI_CurrentLoadingWzFile(itemWzDir, true);
                    Program.WzManager.LoadWzFile(itemWzDir, _wzMapleVersion);
                }
                ExtractItemFile();
                LoadCanvasSection(ITEM_PATH);

                // Load maps
                const string MAP_PATH = "map";
                List<string> mapWzFiles = Program.WzManager.GetWzFileNameListFromBase(MAP_PATH);
                foreach (string mapWzFileName in mapWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(mapWzFileName, true);
                    Program.WzManager.LoadWzFile(mapWzFileName, _wzMapleVersion);
                }
                LoadCanvasSection(MAP_PATH);

                for (int i_map = 0; i_map <= 9; i_map++)
                {
                    string MAP_PART_PATH = "map\\map\\map" + i_map;
                    List<string> map_iWzFiles = Program.WzManager.GetWzFileNameListFromBase(MAP_PART_PATH);
                    foreach (string map_iWzFileName in map_iWzFiles)
                    {
                        UpdateUI_CurrentLoadingWzFile(map_iWzFileName, true);
                        Program.WzManager.LoadWzFile(map_iWzFileName, _wzMapleVersion);
                    }
                    LoadCanvasSection(MAP_PART_PATH);
                }

                const string MAPTILE_PATH = "map\\tile";
                List<string> tileWzFiles = Program.WzManager.GetWzFileNameListFromBase(MAPTILE_PATH);
                foreach (string tileWzFileNames in tileWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(tileWzFileNames, true);
                    Program.WzManager.LoadWzFile(tileWzFileNames, _wzMapleVersion);
                }
                LoadCanvasSection(MAPTILE_PATH);

                const string MAPOBJ_WZ_PATH = "map\\obj";
                List<string> objWzFiles = Program.WzManager.GetWzFileNameListFromBase(MAPOBJ_WZ_PATH);
                foreach (string objWzFileName in objWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(objWzFileName, true);
                    Program.WzManager.LoadWzFile(objWzFileName, _wzMapleVersion);
                }
                LoadCanvasSection(MAPOBJ_WZ_PATH);

                const string MAPBACK_WZ_PATH = "map\\back";
                List<string> backWzFiles = Program.WzManager.GetWzFileNameListFromBase(MAPBACK_WZ_PATH);
                foreach (string backWzFileName in backWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(backWzFileName, true);
                    Program.WzManager.LoadWzFile(backWzFileName, _wzMapleVersion);
                }
                LoadCanvasSection(MAPBACK_WZ_PATH);

                ExtractMapMarks();
                ExtractMapPortals();
                ExtractMapTileSets();
                ExtractMapObjSets();
                ExtractMapBackgroundSets();
                ExtractMaps();

                ImageFormatDetector.UsePreBigBangImageFormats = Program.IsPreBBDataWzFormat;

                // UI.wz
                const string UI_WZ_PATH = "ui";
                List<string> uiWzFiles = Program.WzManager.GetWzFileNameListFromBase(UI_WZ_PATH);
                foreach (string uiWzFileNames in uiWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(uiWzFileNames, true);
                    Program.WzManager.LoadWzFile(uiWzFileNames, _wzMapleVersion);
                }
                LoadCanvasSection(UI_WZ_PATH);
            }
            return true;
        }

        /// <summary>
        /// Load canvas section for the directory
        /// </summary>
        private void LoadCanvasSection(string directory)
        {
            directory = directory.Replace("\\", "/");
            WzFileManager.fileManager.LoadCanvasSection(directory, _wzMapleVersion);
        }

        private void UpdateUI_CurrentLoadingWzFile(string fileName, bool isWzFile)
        {
            textBox2.Text = string.Format("Initializing {0}{1}...", fileName, isWzFile ? ".wz" : "");
            Application.DoEvents();
        }

        /// <summary>
        /// On loading initialization.cs
        /// </summary>
        private void Initialization_Load(object sender, EventArgs e)
        {
            // WZ Tab initialization
            versionBox.SelectedIndex = 0;
            try
            {
                string[] paths = ApplicationSettings.MapleFoldersList.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (string path in paths)
                {
                    if (!Directory.Exists(path))
                        continue;
                    pathBox.Items.Add(path);
                }
                foreach (string path in WzFileManager.COMMON_MAPLESTORY_DIRECTORY)
                {
                    if (Directory.Exists(path))
                    {
                        pathBox.Items.Add(path);
                    }
                }
                if (pathBox.Items.Count == 0)
                    pathBox.Items.Add("Select Maple Folder");
            }
            catch
            {
            }
            versionBox.SelectedIndex = ApplicationSettings.MapleVersionIndex;
            if (pathBox.Items.Count < ApplicationSettings.MapleFolderIndex + 1)
            {
                pathBox.SelectedIndex = pathBox.Items.Count - 1;
            }
            else
            {
                pathBox.SelectedIndex = ApplicationSettings.MapleFolderIndex;
            }

            // Populate the MapleStory localisation box
            var values = Enum.GetValues(typeof(MapleLib.ClientLib.MapleStoryLocalisation))
                    .Cast<MapleLib.ClientLib.MapleStoryLocalisation>()
                    .Select(v => new
                    {
                        Text = v.ToString().Replace("MapleStory", "MapleStory "),
                        Value = (int)v
                    })
                    .ToList();
            comboBox_localisation.DataSource = values;
            comboBox_localisation.DisplayMember = "Text";
            comboBox_localisation.ValueMember = "Value";

            var savedLocaliation = values.Where(x => x.Value == ApplicationSettings.MapleStoryClientLocalisation).FirstOrDefault();
            comboBox_localisation.SelectedItem = savedLocaliation ?? values[0];

            // IMG Versions Tab initialization
            LoadAdditionalVersionPaths();
            LoadRecentVersionPaths();
            RefreshVersionList();

            // Select last used version if available
            var config = HaCreatorConfig.Load();
            bool foundLastUsed = false;

            if (!string.IsNullOrEmpty(config.LastUsedVersion))
            {
                for (int i = 0; i < listBox_imgVersions.Items.Count; i++)
                {
                    if (listBox_imgVersions.Items[i] is VersionListItem item &&
                        item.Version.Version == config.LastUsedVersion)
                    {
                        listBox_imgVersions.SelectedIndex = i;
                        foundLastUsed = true;
                        break;
                    }
                }
            }

            if (!foundLastUsed && listBox_imgVersions.Items.Count > 0)
            {
                listBox_imgVersions.SelectedIndex = 0;
            }

            UpdateImgButtonStates();

            // Select appropriate default tab based on config
            if (config.DataSourceMode == DataSourceMode.ImgFileSystem && listBox_imgVersions.Items.Count > 0)
            {
                tabControl_dataSource.SelectedTab = tabPage_imgVersions;
            }
            else
            {
                tabControl_dataSource.SelectedTab = tabPage_wzFiles;
            }
        }

        /// <summary>
        /// Browse for WZ folder
        /// </summary>
        private void button_browseWz_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog mapleSelect = new()
            {
                ShowNewFolderButton = true,
                Description = "Select the MapleStory folder."
            })
            {
                if (mapleSelect.ShowDialog() != DialogResult.OK)
                    return;

                pathBox.Items.Add(mapleSelect.SelectedPath);
                pathBox.SelectedIndex = pathBox.Items.Count - 1;
            }
        }

        /// <summary>
        /// Debug button for check map errors
        /// </summary>
        private void debugButton_Click(object sender, EventArgs e)
        {
            const string OUTPUT_ERROR_FILENAME = "Errors_MapDebug.txt";

            string wzPath = pathBox.Text;

            WzMapleVersion fileVersion = (WzMapleVersion)versionBox.SelectedIndex;
            if (!InitializeWzFilesInternal(wzPath, fileVersion, false))
            {
                return;
            }

            MultiBoard mb = new MultiBoard();
            Board mapBoard = new Board(
                new Microsoft.Xna.Framework.Point(),
                new Microsoft.Xna.Framework.Point(),
                mb,
                false,
                null,
                MapleLib.WzLib.WzStructure.Data.ItemTypes.None,
                MapleLib.WzLib.WzStructure.Data.ItemTypes.None);

            foreach (string mapid in Program.InfoManager.MapsNameCache.Keys)
            {
                WzImage mapImage = WzInfoTools.FindMapImage(mapid, Program.WzManager);
                if (mapImage == null)
                {
                    continue;
                }
                mapImage.ParseImage();
                if (mapImage["info"]["link"] != null)
                {
                    mapImage.UnparseImage();
                    continue;
                }
                MapLoader.VerifyMapPropsKnown(mapImage, true);
                MapInfo info = new MapInfo(mapImage, null, null, null);
                try
                {
                    mapBoard.CreateMapLayers();

                    MapLoader.LoadLayers(mapImage, mapBoard);
                    MapLoader.LoadLife(mapImage, mapBoard);
                    MapLoader.LoadFootholds(mapImage, mapBoard);
                    MapLoader.GenerateDefaultZms(mapBoard);
                    MapLoader.LoadRopes(mapImage, mapBoard);
                    MapLoader.LoadChairs(mapImage, mapBoard);
                    MapLoader.LoadPortals(mapImage, mapBoard);
                    MapLoader.LoadReactors(mapImage, mapBoard);
                    MapLoader.LoadToolTips(mapImage, mapBoard);
                    MapLoader.LoadBackgrounds(mapImage, mapBoard);
                    MapLoader.LoadMisc(mapImage, mapBoard);

                    List<BackgroundInstance> allBackgrounds = new List<BackgroundInstance>();
                    allBackgrounds.AddRange(mapBoard.BoardItems.BackBackgrounds);
                    allBackgrounds.AddRange(mapBoard.BoardItems.FrontBackgrounds);

                    foreach (BackgroundInstance bg in allBackgrounds)
                    {
                        if (bg.type != MapleLib.WzLib.WzStructure.Data.BackgroundType.Regular)
                        {
                            if (bg.cx < 0 || bg.cy < 0)
                            {
                                string error = string.Format("Negative CX/ CY moving background object. CX='{0}', CY={1}, Type={2}, {3}{4}", bg.cx, bg.cy, bg.type.ToString(), Environment.NewLine, mapImage.ToString());
                                ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                            }
                        }
                    }
                    allBackgrounds.Clear();
                }
                catch (Exception exp)
                {
                    string error = string.Format("Exception occured loading {0}{1}{2}{3}", Environment.NewLine, mapImage.ToString(), Environment.NewLine, exp.ToString());
                    ErrorLogger.Log(ErrorLevel.Crash, error);
                }
                finally
                {
                    mapBoard.Dispose();

                    mapBoard.BoardItems.BackBackgrounds.Clear();
                    mapBoard.BoardItems.FrontBackgrounds.Clear();

                    mapImage.UnparseImage();
                }

                if (ErrorLogger.NumberOfErrorsPresent() > 200)
                    ErrorLogger.SaveToFile(OUTPUT_ERROR_FILENAME);
            }
            ErrorLogger.SaveToFile(OUTPUT_ERROR_FILENAME);

            MessageBox.Show(string.Format("Check for map errors completed. See '{0}' for more information.", OUTPUT_ERROR_FILENAME));
        }

        /// <summary>
        /// Keyboard navigation
        /// </summary>
        private void Initialization_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button_initialise_Click(null, null);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }

        /// <summary>
        /// Tab selection changed
        /// </summary>
        private void tabControl_dataSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Update button states when switching tabs
            if (tabControl_dataSource.SelectedTab == tabPage_imgVersions)
            {
                UpdateImgButtonStates();
            }
        }

        #region IMG Version Tab Methods

        /// <summary>
        /// Subscribes to hot swap events from the VersionManager
        /// </summary>
        private void SubscribeToHotSwapEvents()
        {
            if (Program.StartupManager?.VersionManager != null)
            {
                Program.StartupManager.VersionManager.VersionsChanged += OnVersionsChanged;
            }
        }

        /// <summary>
        /// Handles version list changes from hot swap
        /// </summary>
        private void OnVersionsChanged(object sender, VersionsChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => HandleVersionChange(e)));
            }
            else
            {
                HandleVersionChange(e);
            }
        }

        /// <summary>
        /// Handles the version change on the UI thread
        /// </summary>
        private void HandleVersionChange(VersionsChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case VersionChangeType.Added:
                    if (e.AffectedVersion != null)
                    {
                        var newItem = new VersionListItem(e.AffectedVersion);
                        listBox_imgVersions.Items.Add(newItem);
                        label_noVersions.Visible = false;
                        SortVersionList();
                    }
                    break;

                case VersionChangeType.Removed:
                    if (e.AffectedVersion != null)
                    {
                        for (int i = listBox_imgVersions.Items.Count - 1; i >= 0; i--)
                        {
                            if (listBox_imgVersions.Items[i] is VersionListItem item &&
                                item.Version.DirectoryPath.Equals(e.AffectedVersion.DirectoryPath, StringComparison.OrdinalIgnoreCase))
                            {
                                bool wasSelected = listBox_imgVersions.SelectedIndex == i;
                                listBox_imgVersions.Items.RemoveAt(i);

                                if (wasSelected && listBox_imgVersions.Items.Count > 0)
                                {
                                    listBox_imgVersions.SelectedIndex = Math.Min(i, listBox_imgVersions.Items.Count - 1);
                                }
                                break;
                            }
                        }

                        if (listBox_imgVersions.Items.Count == 0)
                        {
                            label_noVersions.Visible = true;
                            panel_versionDetails.Visible = false;
                        }
                    }
                    break;

                case VersionChangeType.Refreshed:
                    RefreshVersionList();
                    break;
            }

            UpdateImgButtonStates();
        }

        /// <summary>
        /// Sorts the version list alphabetically
        /// </summary>
        private void SortVersionList()
        {
            var items = listBox_imgVersions.Items.Cast<VersionListItem>()
                .OrderBy(i => i.Version.Version)
                .ToList();

            var selectedItem = listBox_imgVersions.SelectedItem;
            listBox_imgVersions.Items.Clear();

            foreach (var item in items)
            {
                listBox_imgVersions.Items.Add(item);
            }

            if (selectedItem != null)
            {
                listBox_imgVersions.SelectedItem = selectedItem;
            }
        }

        /// <summary>
        /// Loads additional version paths from config and validates they still exist
        /// </summary>
        private void LoadAdditionalVersionPaths()
        {
            if (Program.StartupManager?.VersionManager == null) return;

            // Use StartupManager's config instance to avoid being overwritten on shutdown
            var config = Program.StartupManager.Config;
            bool configChanged = false;

            var pathsToRemove = new List<string>();
            foreach (var path in config.AdditionalVersionPaths.ToList())
            {
                if (Directory.Exists(path))
                {
                    Program.StartupManager.VersionManager.AddExternalVersion(path);
                }
                else
                {
                    pathsToRemove.Add(path);
                    configChanged = true;
                }
            }

            if (configChanged)
            {
                foreach (var path in pathsToRemove)
                {
                    config.AdditionalVersionPaths.Remove(path);
                }
                config.Save();
            }
        }

        /// <summary>
        /// Loads recent version paths from history and validates they still exist
        /// </summary>
        private void LoadRecentVersionPaths()
        {
            if (Program.StartupManager?.VersionManager == null) return;

            // Use StartupManager's config instance to avoid being overwritten on shutdown
            var config = Program.StartupManager.Config;
            bool configChanged = false;

            var pathsToRemove = new List<string>();
            foreach (var path in config.RecentVersionPaths.ToList())
            {
                if (Directory.Exists(path))
                {
                    Program.StartupManager.VersionManager.AddExternalVersion(path);
                }
                else
                {
                    pathsToRemove.Add(path);
                    configChanged = true;
                }
            }

            if (configChanged)
            {
                foreach (var path in pathsToRemove)
                {
                    config.RecentVersionPaths.Remove(path);
                }
                config.Save();
            }
        }

        /// <summary>
        /// Refreshes the IMG version list
        /// </summary>
        private void RefreshVersionList()
        {
            listBox_imgVersions.Items.Clear();

            if (Program.StartupManager?.VersionManager != null)
            {
                Program.StartupManager.VersionManager.Refresh();

                foreach (var version in Program.StartupManager.VersionManager.AvailableVersions)
                {
                    listBox_imgVersions.Items.Add(new VersionListItem(version));
                }
            }

            if (listBox_imgVersions.Items.Count == 0)
            {
                label_noVersions.Visible = true;
                panel_versionDetails.Visible = false;
            }
            else
            {
                label_noVersions.Visible = false;
            }

            UpdateImgButtonStates();
        }

        /// <summary>
        /// Version list selection changed
        /// </summary>
        private void listBox_imgVersions_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateVersionDetails();
            UpdateImgButtonStates();
        }

        /// <summary>
        /// Updates the version details panel
        /// </summary>
        private void UpdateVersionDetails()
        {
            if (listBox_imgVersions.SelectedItem is VersionListItem item)
            {
                panel_versionDetails.Visible = true;

                var v = item.Version;
                label_versionName.Text = v.DisplayName ?? v.Version;
                label_extractedDate.Text = $"Extracted: {v.ExtractedDate:yyyy-MM-dd HH:mm}";
                label_encryptionInfo.Text = $"Encryption: {v.Encryption}";

                // Build detailed format string
                string formatDetails = GetVersionFormatDetails(v);
                label_format.Text = $"Format: {formatDetails}";

                int totalImages = v.Categories.Values.Sum(c => c.FileCount);
                label_imageCount.Text = $"Total Images: {totalImages:N0}";
                label_categoryCount.Text = $"Categories: {v.Categories.Count}";

                // Build features string
                string features = GetVersionFeatures(v);
                label_features.Text = $"Info: {features}";

                if (!v.IsValid && v.ValidationErrors.Count > 0)
                {
                    label_validationStatus.Text = $"Warning: {v.ValidationErrors.First()}";
                    label_validationStatus.ForeColor = Color.OrangeRed;
                }
                else
                {
                    label_validationStatus.Text = "Status: Valid";
                    label_validationStatus.ForeColor = Color.Green;
                }
            }
            else
            {
                panel_versionDetails.Visible = false;
            }
        }

        /// <summary>
        /// Gets a detailed format description for a version
        /// </summary>
        private string GetVersionFormatDetails(VersionInfo v)
        {
            var parts = new List<string>();

            if (v.IsBetaMs)
                parts.Add("Beta MapleStory (v0.01-v0.30)");
            else if (v.IsPreBB)
                parts.Add("Pre-Big Bang");
            else if (v.IsBigBang2)
                parts.Add("Big Bang 2 / Chaos");
            else
                parts.Add("Post-Big Bang");

            if (v.Is64Bit)
                parts.Add("64-bit");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Gets feature/info string for a version
        /// </summary>
        private string GetVersionFeatures(VersionInfo v)
        {
            var parts = new List<string>();

            // Add patch version if available
            if (v.PatchVersion > 0)
                parts.Add($"Patch v{v.PatchVersion}");

            // Add region if available
            if (!string.IsNullOrEmpty(v.SourceRegion))
                parts.Add(v.SourceRegion);

            // Add external marker
            if (v.IsExternal)
                parts.Add("External");

            return parts.Count > 0 ? string.Join(", ", parts) : "-";
        }

        /// <summary>
        /// Updates button states for IMG version tab
        /// </summary>
        private void UpdateImgButtonStates()
        {
            bool hasSelection = listBox_imgVersions.SelectedItem != null;
            button_deleteVersion.Enabled = hasSelection;
        }

        /// <summary>
        /// Extract new version button click
        /// </summary>
        private void button_extractNew_Click(object sender, EventArgs e)
        {
            UnpackWzToImg unpacker = new UnpackWzToImg();
            unpacker.ShowDialog(this);
            unpacker.Close();

            // After extraction, refresh and try to select the new version
            Program.StartupManager?.ScanVersions();
            RefreshVersionList();

            // Select the most recently added version (last in list after sort)
            if (listBox_imgVersions.Items.Count > 0)
            {
                listBox_imgVersions.SelectedIndex = listBox_imgVersions.Items.Count - 1;
            }
        }

        /// <summary>
        /// Browse for existing IMG version folder
        /// </summary>
        private void button_browseVersion_Click(object sender, EventArgs e)
        {
            using (var folderBrowser = new FolderBrowserDialog())
            {
                folderBrowser.Description = "Select a folder containing extracted IMG files";
                folderBrowser.ShowNewFolderButton = false;

                if (folderBrowser.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = folderBrowser.SelectedPath;

                    bool hasManifest = File.Exists(Path.Combine(selectedPath, "manifest.json"));
                    bool hasStringFolder = Directory.Exists(Path.Combine(selectedPath, "String"));
                    bool hasMapFolder = Directory.Exists(Path.Combine(selectedPath, "Map"));

                    if (!hasManifest && !hasStringFolder && !hasMapFolder)
                    {
                        MessageBox.Show(
                            "The selected folder doesn't appear to contain extracted IMG files.\n\n" +
                            "A valid version folder should contain:\n" +
                            "- A manifest.json file, OR\n" +
                            "- String/ and Map/ folders with .img files",
                            "Invalid Folder",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    if (Program.StartupManager?.VersionManager != null)
                    {
                        var version = Program.StartupManager.VersionManager.AddExternalVersion(selectedPath);
                        if (version != null)
                        {
                            // Use StartupManager's config instance to avoid being overwritten on shutdown
                            var config = Program.StartupManager.Config;

                            string normalizedPath = Path.GetFullPath(selectedPath);
                            if (!config.AdditionalVersionPaths.Any(p =>
                                Path.GetFullPath(p).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase)))
                            {
                                config.AdditionalVersionPaths.Add(selectedPath);
                            }

                            config.AddToRecentVersionPaths(selectedPath);
                            config.Save();

                            var newItem = new VersionListItem(version);
                            listBox_imgVersions.Items.Add(newItem);
                            listBox_imgVersions.SelectedItem = newItem;

                            label_noVersions.Visible = false;

                            UpdateVersionDetails();
                            UpdateImgButtonStates();
                        }
                        else
                        {
                            MessageBox.Show(
                                "Failed to add the version. It may already be in the list.",
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Delete selected version
        /// </summary>
        private void button_deleteVersion_Click(object sender, EventArgs e)
        {
            if (listBox_imgVersions.SelectedItem is VersionListItem item)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete version '{item.Version.DisplayName}'?\n\nThis will permanently delete all extracted IMG files.",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    string versionPath = item.Version.DirectoryPath;

                    if (Program.StartupManager?.VersionManager?.DeleteVersion(item.Version.Version) == true)
                    {
                        RemoveVersionPathFromConfig(versionPath);
                        RefreshVersionList();
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete version. The files may be in use.",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Refresh versions list
        /// </summary>
        private void button_refreshVersions_Click(object sender, EventArgs e)
        {
            RefreshVersionList();
        }

        /// <summary>
        /// Double-click on version list to initialize
        /// </summary>
        private void listBox_imgVersions_DoubleClick(object sender, EventArgs e)
        {
            if (listBox_imgVersions.SelectedItem != null)
            {
                button_initialise_Click(sender, e);
            }
        }

        /// <summary>
        /// Removes a version path from all config lists
        /// </summary>
        private void RemoveVersionPathFromConfig(string path)
        {
            // Use StartupManager's config instance to avoid being overwritten on shutdown
            var config = Program.StartupManager?.Config;
            if (config == null) return;

            string normalizedPath = Path.GetFullPath(path);
            bool changed = false;

            var toRemoveAdditional = config.AdditionalVersionPaths
                .FirstOrDefault(p => Path.GetFullPath(p).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (toRemoveAdditional != null)
            {
                config.AdditionalVersionPaths.Remove(toRemoveAdditional);
                changed = true;
            }

            var toRemoveRecent = config.RecentVersionPaths
                .FirstOrDefault(p => Path.GetFullPath(p).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (toRemoveRecent != null)
            {
                config.RecentVersionPaths.Remove(toRemoveRecent);
                changed = true;
            }

            if (changed)
            {
                config.Save();
            }
        }

        /// <summary>
        /// List item wrapper for VersionInfo
        /// </summary>
        private class VersionListItem
        {
            public VersionInfo Version { get; }

            public VersionListItem(VersionInfo version)
            {
                Version = version;
            }

            public override string ToString()
            {
                var v = Version;
                string name = v.DisplayName ?? v.Version;

                // Build format tag
                string formatTag = GetFormatTag(v);

                // Build version number if available
                string versionTag = v.PatchVersion > 0 ? $"v{v.PatchVersion}" : "";

                // Build region tag if available
                string regionTag = !string.IsNullOrEmpty(v.SourceRegion) ? v.SourceRegion : "";

                // Combine tags
                var tags = new List<string>();
                if (!string.IsNullOrEmpty(formatTag)) tags.Add(formatTag);
                if (!string.IsNullOrEmpty(versionTag) && !name.Contains($"v{v.PatchVersion}")) tags.Add(versionTag);
                if (!string.IsNullOrEmpty(regionTag) && !name.ToUpper().Contains(regionTag.ToUpper())) tags.Add(regionTag);

                if (tags.Count > 0)
                    return $"{name} [{string.Join(", ", tags)}]";
                return name;
            }

            private static string GetFormatTag(VersionInfo v)
            {
                if (v.IsBetaMs) return "Beta";
                if (v.IsPreBB) return "Pre-BB";
                if (v.IsBigBang2) return "BB2/Chaos";
                if (v.Is64Bit) return "64-bit";
                return "";
            }
        }

        #endregion

        #region Extractor Methods

        public void ExtractMobFile()
        {
            if (Program.InfoManager.MobIconCache.Count != 0)
                return;

            const string MOB_WZ_PATH = "mob";
            List<WzDirectory> mobWzDirs = Program.WzManager.GetWzDirectoriesFromBase(MOB_WZ_PATH);

            foreach (WzDirectory mobWzDir in mobWzDirs)
            {
                foreach (WzImage mobImage in mobWzDir.WzImages)
                {
                    string mobIdStr = mobImage.Name.Replace(".img", "");

                    switch (mobIdStr)
                    {
                        case "BossAzmothCanyon":
                        case "BossBaldrix":
                        case "BossChampionRaid":
                        case "BossCommon":
                        case "BossEnterAni":
                        case "BossLimbo":
                        case "BossNohime":
                        case "BossSuu":
                        case "PatternSystem":
                        case "pack_ignore.txt":
                            break;
                        default:
                            {
                                int mobId = 0;
                                int.TryParse(mobIdStr, out mobId);

                                if (mobId == 0)
                                {
                                    ErrorLogger.Log(ErrorLevel.Info, "New file in Mob.wz: " + mobIdStr);
                                    continue;
                                }

                                WzImageProperty standCanvas = (WzCanvasProperty)mobImage["stand"]?["0"]?.GetLinkedWzImageProperty();

                                if (standCanvas == null) continue;

                                if (!Program.InfoManager.MobIconCache.ContainsKey(mobId))
                                    Program.InfoManager.MobIconCache.Add(mobId, standCanvas);
                                break;
                            }
                    }
                }
            }
            LoadCanvasSection(MOB_WZ_PATH);
        }

        public void ExtractNpcFile()
        {
            if (Program.InfoManager.NpcPropertyCache.Count != 0)
                return;

            const string NPC_WZ_PATH = "npc";
            List<WzDirectory> npcWzDirs = Program.WzManager.GetWzDirectoriesFromBase(NPC_WZ_PATH);

            foreach (WzDirectory npcWzDir in npcWzDirs)
            {
                foreach (WzImage npcImage in npcWzDir.WzImages)
                {
                    string npcId = npcImage.Name.Replace(".img", "");

                    if (!Program.InfoManager.NpcPropertyCache.ContainsKey(npcId))
                    {
                        Program.InfoManager.NpcPropertyCache.Add(npcId, npcImage);
                    }
                }
            }
            LoadCanvasSection(NPC_WZ_PATH);
        }

        public void ExtractReactorFile()
        {
            if (Program.InfoManager.Reactors.Count != 0)
                return;

            const string REACTOR_WZ_PATH = "reactor";
            List<WzDirectory> reactorWzDirs = Program.WzManager.GetWzDirectoriesFromBase(REACTOR_WZ_PATH);
            foreach (WzDirectory reactorWzDir in reactorWzDirs)
            {
                foreach (WzImage reactorImage in reactorWzDir.WzImages)
                {
                    WzSubProperty infoProp = (WzSubProperty)reactorImage["info"];

                    string reactorId = WzInfoTools.RemoveExtension(reactorImage.Name);
                    string name = "NO NAME";
                    if (infoProp != null)
                    {
                        name = ((WzStringProperty)infoProp?["info"])?.Value ?? null;
                        if (name == null)
                            name = ((WzStringProperty)infoProp?["viewName"])?.Value ?? string.Empty;
                    }

                    ReactorInfo reactor = new ReactorInfo(null, new System.Drawing.Point(), reactorId, name, reactorImage);

                    Program.InfoManager.Reactors[reactor.ID] = reactor;
                }
            }
            LoadCanvasSection(REACTOR_WZ_PATH);
        }

        public void ExtractQuestFile()
        {
            if (Program.InfoManager.QuestActs.Count != 0)
                return;

            const string QUEST_WZ_PATH = "quest";
            List<WzDirectory> questWzDirs = Program.WzManager.GetWzDirectoriesFromBase(QUEST_WZ_PATH);
            foreach (WzDirectory questWzDir in questWzDirs)
            {
                foreach (WzImage questImage in questWzDir.WzImages)
                {
                    switch (questImage.Name)
                    {
                        case "Act.img":
                            foreach (WzImageProperty questActImage in questImage.WzProperties)
                            {
                                Program.InfoManager.QuestActs.Add(questActImage.Name, questActImage as WzSubProperty);
                            }
                            break;
                        case "Check.img":
                            foreach (WzImageProperty questCheckImage in questImage.WzProperties)
                            {
                                Program.InfoManager.QuestChecks.Add(questCheckImage.Name, questCheckImage as WzSubProperty);
                            }
                            break;
                        case "QuestInfo.img":
                            foreach (WzImageProperty questInfoImage in questImage.WzProperties)
                            {
                                Program.InfoManager.QuestInfos.Add(questInfoImage.Name, questInfoImage as WzSubProperty);
                            }
                            break;
                        case "Say.img":
                            foreach (WzImageProperty questSayImage in questImage.WzProperties)
                            {
                                Program.InfoManager.QuestSays.Add(questSayImage.Name, questSayImage as WzSubProperty);
                            }
                            break;
                        case "ChangeableQExpTable.img":
                        case "Exclusive.img":
                        case "PQuest.img":
                        case "PQuestSearch.img":
                        case "QuestDestination.img":
                            break;
                        default:
                            break;
                    }
                }
            }
            LoadCanvasSection(QUEST_WZ_PATH);
        }

        public void ExtractCharacterFile()
        {
            // disabled due to performance issue on startup
        }

        public void ExtractSkillFile()
        {
            if (Program.InfoManager.SkillWzImageCache.Count != 0)
                return;

            const string SKILL_WZ_PATH = "skill";
            List<WzDirectory> skillWzDirs = Program.WzManager.GetWzDirectoriesFromBase(SKILL_WZ_PATH);
            foreach (WzDirectory skillWzDir in skillWzDirs)
            {
                foreach (WzImage skillWzImage in skillWzDir.WzImages)
                {
                    string skillDirectoryId = skillWzImage.Name;

                    WzImageProperty imgSkill = skillWzImage["skill"];
                    if (imgSkill != null)
                    {
                        foreach (WzImageProperty skillItemImage in imgSkill.WzProperties)
                        {
                            string skillId = skillItemImage.Name;
                            Program.InfoManager.SkillWzImageCache.Add(skillId, skillItemImage);
                        }
                    }
                }
            }
            LoadCanvasSection(SKILL_WZ_PATH);
        }

        public void ExtractItemFile()
        {
            if (Program.InfoManager.MapsNameCache.Count == 0)
                throw new Exception("ExtractStringWzFile needs to be called first.");
            else if (Program.InfoManager.ItemIconCache.Count != 0)
                return;

            const string ITEM_WZ_PATH = "item";
            List<WzDirectory> itemWzDirs = Program.WzManager.GetWzDirectoriesFromBase(ITEM_WZ_PATH);
            foreach (WzDirectory itemWzDir in itemWzDirs)
            {
                Parallel.ForEach(itemWzDir.WzDirectories, itemWzImage =>
                {
                    switch (itemWzImage.Name)
                    {
                        case "ItemOption.img":
                        case "Special":
                            break;
                        case "Consume":
                        case "Etc":
                        case "Cash":
                        case "Install":
                            {
                                foreach (WzImage itemGroupImg in itemWzImage.WzImages)
                                {
                                    foreach (WzImageProperty itemImg in itemGroupImg.WzProperties)
                                    {
                                        string itemId = itemImg.Name;
                                        WzSubProperty itemProp = itemImg as WzSubProperty;
                                        if (itemProp != null)
                                        {
                                            WzCanvasProperty icon = itemProp?["info"]?["icon"] as WzCanvasProperty;
                                            if (icon != null)
                                            {
                                                int intName = 0;
                                                int.TryParse(itemId, out intName);

                                                lock (Program.InfoManager.ItemIconCache)
                                                {
                                                    if (!Program.InfoManager.ItemIconCache.ContainsKey(intName))
                                                        Program.InfoManager.ItemIconCache.Add(intName, icon);
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                            }
                        case "Pet":
                            {
                                foreach (WzImage petImg in itemWzImage.WzImages)
                                {
                                    string itemId = petImg.Name.Replace(".img", "");
                                    WzCanvasProperty icon = petImg["info"]?["icon"] as WzCanvasProperty;
                                    if (icon != null)
                                    {
                                        int intName = 0;
                                        int.TryParse(itemId, out intName);

                                        lock (Program.InfoManager.ItemIconCache)
                                        {
                                            if (!Program.InfoManager.ItemIconCache.ContainsKey(intName))
                                                Program.InfoManager.ItemIconCache.Add(intName, icon);
                                        }
                                    }
                                }
                                break;
                            }
                        default:
                            break;
                    }
                });
            }
            LoadCanvasSection(ITEM_WZ_PATH);
        }

        public void ExtractSoundFile()
        {
            if (Program.InfoManager.BGMs.Count != 0)
                return;

            const string SOUND_WZ_PATH = "sound";
            List<WzDirectory> soundWzDirs = Program.WzManager.GetWzDirectoriesFromBase(SOUND_WZ_PATH);

            foreach (WzDirectory soundWzDir in soundWzDirs)
            {
                if (Program.WzManager.IsPreBBDataWzFormat)
                {
                    WzDirectory x = (WzDirectory)soundWzDir["Sound"];
                }

                foreach (WzImage soundImage in soundWzDir.WzImages)
                {
                    if (!soundImage.Name.ToLower().Contains("bgm"))
                        continue;
                    try
                    {
                        foreach (WzImageProperty bgmImage in soundImage.WzProperties)
                        {
                            WzBinaryProperty binProperty = null;
                            if (bgmImage is WzBinaryProperty bgm)
                            {
                                binProperty = bgm;
                            }
                            else if (bgmImage is WzUOLProperty uolBGM)
                            {
                                WzObject linkVal = ((WzUOLProperty)bgmImage).LinkValue;
                                if (linkVal is WzBinaryProperty linkCanvas)
                                {
                                    binProperty = linkCanvas;
                                }
                            }

                            if (binProperty != null)
                                Program.InfoManager.BGMs[WzInfoTools.RemoveExtension(soundImage.Name) + @"/" + binProperty.Name] = binProperty;
                        }
                    }
                    catch (Exception e)
                    {
                        string error = string.Format("[ExtractSoundFile] Error parsing {0}, {1} file.\r\nError: {2}", soundWzDir.Name, soundImage.Name, e.ToString());
                        MapleLib.Helpers.ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                        continue;
                    }
                }
            }
            LoadCanvasSection(SOUND_WZ_PATH);
        }

        public void ExtractMapMarks()
        {
            if (Program.InfoManager.MapMarks.Count != 0)
                return;

            WzImage mapWzImg = (WzImage)Program.WzManager.FindWzImageByName("map", "MapHelper.img");
            if (mapWzImg == null)
                throw new Exception("MapHelper.img not found in map.wz.");

            foreach (WzCanvasProperty mark in mapWzImg["mark"].WzProperties)
            {
                Program.InfoManager.MapMarks[mark.Name] = mark.GetLinkedWzCanvasBitmap();
            }
        }

        public void ExtractMapTileSets()
        {
            if (Program.InfoManager.TileSets.Count != 0)
                return;

            bool bLoadedInMap = false;

            WzDirectory mapWzDirs = (WzDirectory)Program.WzManager.FindWzImageByName("map", "Tile");
            if (mapWzDirs != null)
            {
                foreach (WzImage tileset in mapWzDirs.WzImages)
                    Program.InfoManager.TileSets[WzInfoTools.RemoveExtension(tileset.Name)] = tileset;

                bLoadedInMap = true;
            }

            if (!bLoadedInMap)
            {
                const string MAP_TILE_WZ_PATH = "map\\tile";
                List<WzDirectory> tileWzDirs = Program.WzManager.GetWzDirectoriesFromBase(MAP_TILE_WZ_PATH);
                foreach (WzDirectory tileWzDir in tileWzDirs)
                {
                    foreach (WzImage tileset in tileWzDir.WzImages)
                        Program.InfoManager.TileSets[WzInfoTools.RemoveExtension(tileset.Name)] = tileset;
                }
                LoadCanvasSection(MAP_TILE_WZ_PATH);
            }

            if (Program.InfoManager.TileSets is Dictionary<string, WzImage> regularDict)
            {
                Program.InfoManager.TileSets = regularDict.OrderBy(kvp => kvp.Key)
                       .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        public void ExtractMapObjSets()
        {
            if (Program.InfoManager.ObjectSets.Count != 0)
                return;

            bool bLoadedInMap = false;

            WzDirectory mapWzDirs = (WzDirectory)Program.WzManager.FindWzImageByName("map", "Obj");
            if (mapWzDirs != null)
            {
                foreach (WzImage objset in mapWzDirs.WzImages)
                    Program.InfoManager.ObjectSets[WzInfoTools.RemoveExtension(objset.Name)] = objset;

                bLoadedInMap = true;
                return;
            }

            if (!bLoadedInMap)
            {
                const string MAP_OBJ_WZ_PATH = "map\\obj";
                List<WzDirectory> objWzDirs = Program.WzManager.GetWzDirectoriesFromBase(MAP_OBJ_WZ_PATH);
                foreach (WzDirectory objWzDir in objWzDirs)
                {
                    foreach (WzImage objset in objWzDir.WzImages)
                        Program.InfoManager.ObjectSets[WzInfoTools.RemoveExtension(objset.Name)] = objset;
                }
                LoadCanvasSection(MAP_OBJ_WZ_PATH);
            }
        }

        public void ExtractMapBackgroundSets()
        {
            if (Program.InfoManager.BackgroundSets.Count != 0)
                return;

            bool bLoadedInMap = false;

            WzDirectory mapWzDirs = (WzDirectory)Program.WzManager.FindWzImageByName("map", "Back");
            if (mapWzDirs != null)
            {
                foreach (WzImage bgset in mapWzDirs.WzImages)
                    Program.InfoManager.BackgroundSets[WzInfoTools.RemoveExtension(bgset.Name)] = bgset;

                bLoadedInMap = true;
            }

            if (!bLoadedInMap)
            {
                const string MAP_BACK_WZ_PATH = "map\\back";
                List<WzDirectory> backWzDirs = Program.WzManager.GetWzDirectoriesFromBase(MAP_BACK_WZ_PATH);
                foreach (WzDirectory backWzDir in backWzDirs)
                {
                    foreach (WzImage bgset in backWzDir.WzImages)
                        Program.InfoManager.BackgroundSets[WzInfoTools.RemoveExtension(bgset.Name)] = bgset;
                }
                LoadCanvasSection(MAP_BACK_WZ_PATH);
            }
        }

        public void ExtractStringFile(bool bIsBetaMapleStory)
        {
            if (Program.InfoManager.MapsNameCache.Count != 0)
                return;

            // Npc strings
            WzImage stringNpcImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Npc.img");
            foreach (WzSubProperty npcImg in stringNpcImg.WzProperties)
            {
                string npcId = npcImg.Name;
                string npcName = (npcImg["name"] as WzStringProperty)?.Value ?? "NO NAME";
                string npcFunc = (npcImg["func"] as WzStringProperty)?.Value ?? string.Empty;

                if (!Program.InfoManager.NpcNameCache.ContainsKey(npcId))
                    Program.InfoManager.NpcNameCache[npcId] = new Tuple<string, string>(npcName, npcFunc);
                else
                {
                    string error = string.Format("[Initialization] Duplicate [Npc] name in String.wz. NpcId='{0}'", npcId);
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                }
            }

            // Map strings
            WzImage stringMapWzImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Map.img");
            foreach (WzSubProperty mapCat in stringMapWzImg.WzProperties)
            {
                foreach (WzSubProperty map in mapCat.WzProperties)
                {
                    WzStringProperty streetNameWzProp = (WzStringProperty)map["streetName"];
                    WzStringProperty mapNameWzProp = (WzStringProperty)map["mapName"];
                    string mapIdStr;
                    if (map.Name.Length == 9)
                        mapIdStr = map.Name;
                    else
                        mapIdStr = WzInfoTools.AddLeadingZeros(map.Name, 9);
                    string categoryName = map.Parent.Name;

                    if (mapNameWzProp == null)
                        Program.InfoManager.MapsNameCache[mapIdStr] = new Tuple<string, string, string>("NO NAME", "NO NAME", "NO NAME");
                    else
                    {
                        Program.InfoManager.MapsNameCache[mapIdStr] = new Tuple<string, string, string>(
                            streetNameWzProp?.Value == null ? string.Empty : streetNameWzProp.Value,
                            mapNameWzProp.Value,
                            categoryName);
                    }
                }
            }

            // Mob strings
            WzImage stringMobImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Mob.img");
            foreach (WzSubProperty mobImg in stringMobImg.WzProperties)
            {
                string mobId = mobImg.Name;
                string itemName = (mobImg["name"] as WzStringProperty)?.Value ?? "NO NAME";

                if (!Program.InfoManager.MobNameCache.ContainsKey(mobId))
                    Program.InfoManager.MobNameCache[mobId] = itemName;
                else
                {
                    string error = string.Format("[Initialization] Duplicate [Mob] name in String.wz. MobId='{0}'", mobId);
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                }
            }

            // Skill strings
            WzImage stringSkillImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Skill.img");
            foreach (WzSubProperty skillImg in stringSkillImg.WzProperties)
            {
                string skillId = skillImg.Name;
                string skillName = (skillImg["name"] as WzStringProperty)?.Value ?? "NO NAME";
                string skillDesc = (skillImg["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                if (!Program.InfoManager.SkillNameCache.ContainsKey(skillId))
                    Program.InfoManager.SkillNameCache[skillId] = new Tuple<string, string>(skillName, skillDesc);
                else
                {
                    string error = string.Format("[Initialization] Duplicate [Skill] name in String.wz. SkillId='{0}'", skillId);
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                }
            }

            // Item strings - Equipment
            WzPropertyCollection stringEqpImg;
            if (bIsBetaMapleStory)
                stringEqpImg = ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Eqp"]).WzProperties;
            else
                stringEqpImg = ((WzImage)Program.WzManager.FindWzImageByName("string", "Eqp.img")).WzProperties;

            foreach (WzSubProperty eqpSubProp in stringEqpImg)
            {
                foreach (WzSubProperty eqpCategorySubProp in eqpSubProp.WzProperties)
                {
                    if (bIsBetaMapleStory)
                    {
                        ExtractStringFile_ProcessEquipmentItem(eqpCategorySubProp, eqpCategorySubProp.Name);
                    }
                    else
                    {
                        eqpCategorySubProp.WzProperties
                            .ToList()
                            .ForEach(itemProp => ExtractStringFile_ProcessEquipmentItem(itemProp, eqpCategorySubProp.Name));
                    }
                }
            }

            WzPropertyCollection stringInsImg;
            if (bIsBetaMapleStory)
                stringInsImg = ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Ins"]).WzProperties;
            else
                stringInsImg = ((WzImage)Program.WzManager.FindWzImageByName("string", "Ins.img")).WzProperties;

            foreach (WzImageProperty insItemImage in stringInsImg)
            {
                if (insItemImage is WzSubProperty insItemSubProp)
                {
                    string itemId = insItemSubProp.Name;
                    const string itemCategory = "Ins";
                    string itemName = (insItemSubProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
                    string itemDesc = (insItemSubProp["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                    int intName = 0;
                    int.TryParse(itemId, out intName);

                    if (!Program.InfoManager.ItemNameCache.ContainsKey(intName))
                        Program.InfoManager.ItemNameCache[intName] = new Tuple<string, string, string>(itemCategory, itemName, itemDesc);
                    else
                    {
                        string error = string.Format("[Initialization] Duplicate [Ins] item name in String.wz. ItemId='{0}', Category={1}", itemId, insItemSubProp.Name);
                        ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                    }
                }
            }

            WzPropertyCollection stringCashImg;
            if (bIsBetaMapleStory)
                stringCashImg = ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Cash"]).WzProperties;
            else
                stringCashImg = ((WzImage)Program.WzManager.FindWzImageByName("string", "Cash.img")).WzProperties;

            foreach (WzSubProperty cashItemImg in stringCashImg)
            {
                string itemId = cashItemImg.Name;
                const string itemCategory = "Cash";
                string itemName = (cashItemImg["name"] as WzStringProperty)?.Value ?? "NO NAME";
                string itemDesc = (cashItemImg["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                int intName = 0;
                int.TryParse(itemId, out intName);

                if (!Program.InfoManager.ItemNameCache.ContainsKey(intName))
                    Program.InfoManager.ItemNameCache[intName] = new Tuple<string, string, string>(itemCategory, itemName, itemDesc);
                else
                {
                    string error = string.Format("[Initialization] Duplicate [Cash] item name in String.wz. ItemId='{0}', Category={1}", itemId, cashItemImg.Name);
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                }
            }

            WzPropertyCollection stringConsumeImg;
            if (bIsBetaMapleStory)
                stringConsumeImg = ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Con"]).WzProperties;
            else
                stringConsumeImg = ((WzImage)Program.WzManager.FindWzImageByName("string", "Consume.img")).WzProperties;

            foreach (WzSubProperty consumeItemImg in stringConsumeImg)
            {
                string itemId = consumeItemImg.Name;
                const string itemCategory = "Consume";
                string itemName = (consumeItemImg["name"] as WzStringProperty)?.Value ?? "NO NAME";
                string itemDesc = (consumeItemImg["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                int intName = 0;
                int.TryParse(itemId, out intName);

                if (!Program.InfoManager.ItemNameCache.ContainsKey(intName))
                    Program.InfoManager.ItemNameCache[intName] = new Tuple<string, string, string>(itemCategory, itemName, itemDesc);
                else
                {
                    string error = string.Format("[Initialization] Duplicate [Consume] item name in String.wz. ItemId='{0}', Category={1}", itemId, consumeItemImg.Name);
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                }
            }

            WzPropertyCollection stringEtcImg;
            if (bIsBetaMapleStory)
                stringEtcImg = ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Etc"]).WzProperties;
            else
                stringEtcImg = ((WzImage)Program.WzManager.FindWzImageByName("string", "Etc.img")).WzProperties;

            foreach (WzSubProperty etcSubProp in stringEtcImg)
            {
                if (bIsBetaMapleStory)
                {
                    ExtractStringFile_ProcessEtcItem(etcSubProp, etcSubProp.Name);
                }
                else
                {
                    etcSubProp.WzProperties
                        .Cast<WzSubProperty>()
                        .ToList()
                        .ForEach(itemProp => ExtractStringFile_ProcessEtcItem(itemProp, etcSubProp.Name));
                }
            }

            WzPropertyCollection stringPetImg;
            if (bIsBetaMapleStory)
                stringPetImg = ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Pet"]).WzProperties;
            else
                stringPetImg = ((WzImage)Program.WzManager.FindWzImageByName("string", "Pet.img")).WzProperties;

            stringPetImg
                .ToList()
                .ForEach(itemProp => ExtractStringFile_ProcessPetItem(itemProp));
        }

        private void ExtractStringFile_ProcessEtcItem(WzSubProperty itemProp, string parentName)
        {
            string itemId = itemProp.Name;
            const string itemCategory = "Etc";
            string itemName = (itemProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
            string itemDesc = (itemProp["desc"] as WzStringProperty)?.Value ?? "NO DESC";

            int intName = 0;
            int.TryParse(itemId, out intName);

            if (!Program.InfoManager.ItemNameCache.ContainsKey(intName))
                Program.InfoManager.ItemNameCache[intName] = new Tuple<string, string, string>(itemCategory, itemName, itemDesc);
            else
            {
                string error = string.Format("[Initialization] Duplicate [{0}] item name in String.wz. ItemId='{1}', Category={2}", itemCategory, itemId, parentName);
                ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
            }
        }

        private void ExtractStringFile_ProcessPetItem(WzImageProperty petProp)
        {
            if (petProp is WzSubProperty petSubProp)
            {
                const string itemCategory = "Pet";

                string itemId = petSubProp.Name;
                string itemName = (petSubProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
                string itemDesc = (petSubProp["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                int intName = 0;
                int.TryParse(itemId, out intName);

                if (!Program.InfoManager.ItemNameCache.ContainsKey(intName))
                    Program.InfoManager.ItemNameCache[intName] = new Tuple<string, string, string>(itemCategory, itemName, itemDesc);
                else
                {
                    string error = string.Format("[Initialization] Duplicate [{0}] item name in String.wz. ItemId='{1}', Category={2}", itemCategory, itemId, petSubProp.Name);
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                }
            }
        }

        private void ExtractStringFile_ProcessEquipmentItem(WzImageProperty itemImageProp, string category)
        {
            if (itemImageProp is WzSubProperty itemProp)
            {
                string itemId = itemProp.Name;
                string itemName = (itemProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
                string itemDesc = (itemProp["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                if (int.TryParse(itemId, out int intName))
                {
                    if (!Program.InfoManager.ItemNameCache.ContainsKey(intName))
                    {
                        Program.InfoManager.ItemNameCache[intName] = new Tuple<string, string, string>(category, itemName, itemDesc);
                    }
                    else
                    {
                        string error = $"[Initialization] Duplicate [Equip] item name in String.wz. ItemId='{itemId}', Category={category}";
                        ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                    }
                }
            }
        }

        public void ExtractMaps()
        {
            if (Program.InfoManager.MapsCache.Count != 0)
                return;

            UpdateUI_CurrentLoadingWzFile(string.Format("{0} map data", Program.InfoManager.MapsNameCache.Count), false);

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2
            };

            Parallel.ForEach(Program.InfoManager.MapsNameCache, parallelOptions, val =>
            {
                int mapid = 0;
                int.TryParse(val.Key, out mapid);

                WzImage mapImage = WzInfoTools.FindMapImage(mapid.ToString(), Program.WzManager);
                if (mapImage != null)
                {
                    string mapId = val.Key;
                    string mapName = "NO NAME";
                    string streetName = "NO NAME";
                    string categoryName = "NO NAME";

                    if (Program.InfoManager.MapsNameCache.ContainsKey(mapId))
                    {
                        var mapNames = Program.InfoManager.MapsNameCache[mapId];
                        mapName = mapNames.Item1;
                        streetName = mapNames.Item2;
                        categoryName = mapNames.Item3;
                    }
                    if (mapImage["info"] != null)
                    {
                        MapInfo info = new MapInfo(mapImage, mapName, streetName, categoryName);

                        lock (Program.InfoManager.MapsCache)
                        {
                            Program.InfoManager.MapsCache[val.Key] = new Tuple<WzImage, string, string, string, MapInfo>(
                                mapImage, mapName, streetName, categoryName, info
                            );
                        }
                    }
                }
            });
        }

        public void ExtractMapPortals()
        {
            if (Program.InfoManager.PortalGame.Count != 0)
                return;

            WzImage mapImg = (WzImage)Program.WzManager.FindWzImageByName("map", "MapHelper.img");
            if (mapImg == null)
                throw new Exception("Couldn't extract portals. MapHelper.img not found.");

            WzSubProperty portalParent = (WzSubProperty)mapImg["portal"];

            // Editor portals
            WzSubProperty editorParent = (WzSubProperty)portalParent["editor"];
            foreach (WzCanvasProperty portalProp in editorParent.WzProperties)
            {
                Program.InfoManager.PortalEditor_TypeById.Add(PortalTypeExtensions.FromCode(portalProp.Name));
                PortalInfo.Load(portalProp);
            }

            // Game portals
            WzSubProperty gameParent = (WzSubProperty)portalParent["game"];
            foreach (WzImageProperty portalProp in gameParent.WzProperties)
            {
                PortalType portalType = PortalTypeExtensions.FromCode(portalProp.Name);

                if (portalProp["default"]?["portalStart"] != null)
                {
                    Dictionary<string, List<Bitmap>> portalTemplateImage = new();

                    foreach (WzSubProperty portalImageProp in portalProp.WzProperties)
                    {
                        WzSubProperty portalStartProp = portalImageProp["portalStart"] as WzSubProperty;
                        List<Bitmap> images = new();

                        foreach (WzCanvasProperty portalImageCanvas in portalStartProp.WzProperties)
                        {
                            Bitmap portalImage = portalImageCanvas.GetLinkedWzCanvasBitmap();
                            images.Add(portalImage);
                        }
                        portalTemplateImage.Add(portalImageProp.Name, images);
                    }

                    Program.InfoManager.PortalGame.Add(portalType, new PortalGameImageInfo(portalTemplateImage.FirstOrDefault().Value[0], portalTemplateImage));
                }
                else
                {
                    Dictionary<string, List<Bitmap>> portalTemplateImage = new();
                    Bitmap defaultImage = null;

                    List<Bitmap> images = new();
                    foreach (WzImageProperty image in portalProp.WzProperties)
                    {
                        if (image is WzCanvasProperty portalImg)
                        {
                            Bitmap portalImage = portalImg.GetLinkedWzCanvasBitmap();
                            defaultImage = portalImage;
                            images.Add(portalImage);
                        }
                    }
                    portalTemplateImage.Add("default", images);

                    Program.InfoManager.PortalGame.Add(portalType, new PortalGameImageInfo(defaultImage, portalTemplateImage));
                }
            }

            for (int i = 0; i < Program.InfoManager.PortalEditor_TypeById.Count; i++)
            {
                Program.InfoManager.PortalIdByType[Program.InfoManager.PortalEditor_TypeById[i]] = i;
            }
        }

        #endregion

        /// <summary>
        /// On click unpack from .wz to -> .img files
        /// </summary>
        private void button_unpack_Click(object sender, EventArgs e)
        {
            UnpackWzToImg unpacker = new UnpackWzToImg();
            unpacker.ShowDialog(this);
            unpacker.Close();
        }

        /// <summary>
        /// Opens the data source settings dialog
        /// </summary>
        private void button_settings_Click(object sender, EventArgs e)
        {
            using (var settingsForm = new DataSourceSettings())
            {
                if (settingsForm.ShowDialog(this) == DialogResult.OK)
                {
                    if (settingsForm.ConfigChanged)
                    {
                        Program.StartupManager?.ReloadConfig();
                        Program.StartupManager?.ScanVersions();
                        RefreshVersionList();
                    }
                }
            }
        }
    }
}
