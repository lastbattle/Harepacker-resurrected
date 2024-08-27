/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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
        /// Initialise
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_initialise_Click(object sender, EventArgs e)
        {
            if (_bIsInitialising) {
                return;
            }
            _bIsInitialising = true;

            try {
                ApplicationSettings.MapleVersionIndex = versionBox.SelectedIndex;
                ApplicationSettings.MapleFolderIndex = pathBox.SelectedIndex;
                string wzPath = pathBox.Text;

                if (wzPath == "Select MapleStory Folder") {
                    MessageBox.Show("Please select the MapleStory folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!ApplicationSettings.MapleFoldersList.Contains(wzPath) && !IsPathCommon(wzPath)) {
                    ApplicationSettings.MapleFoldersList = ApplicationSettings.MapleFoldersList == "" ? wzPath : (ApplicationSettings.MapleFoldersList + "," + wzPath);
                }
                WzMapleVersion fileVersion = (WzMapleVersion)versionBox.SelectedIndex;
                if (InitializeWzFiles(wzPath, fileVersion)) {
                    Hide();
                    Application.DoEvents();
                    editor = new HaEditor();
                    editor.ShowDialog();

                    Application.Exit();
                }
            } finally {
                _bIsInitialising = false;
            }
        }

        /// <summary>
        /// Initialise the WZ files with the provided folder path
        /// </summary>
        /// <param name="wzPath"></param>
        /// <param name="fileVersion"></param>
        /// <returns></returns>
        private bool InitializeWzFiles(string wzPath, WzMapleVersion fileVersion)
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

            Program.WzManager = new WzFileManager(wzPath);
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

                ExtractStringFile();
                //Program.WzManager.ExtractItems();

                ExtractMobFile();
                ExtractNpcFile();
                ExtractReactorFile();
                ExtractSoundFile();
                ExtractQuestFile();
                //ExtractCharacterFile(); // due to performance issue, its loaded on demand
                ExtractItemFile();
                ExtractMapMarks();
                ExtractMapPortals();
                ExtractMapTileSets();
                ExtractMapObjSets();
                ExtractMapBackgroundSets();

                ExtractMaps();
            }
            else // for versions beyond v30x
            {
                // Check if this wz is list.wz, and load if possible
                // this is only available in pre-bb variants of the client.
                // and contains the possible path of .img that uses a different encryption
                Program.WzManager.LoadListWzFile(_wzMapleVersion);

                // String.wz
                List<string> stringWzFiles = Program.WzManager.GetWzFileNameListFromBase("string");
                foreach (string stringWzFileName in stringWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(stringWzFileName, true);

                    Program.WzManager.LoadWzFile(stringWzFileName, _wzMapleVersion);
                }
                ExtractStringFile();

                // Mob WZ
                List<string> mobWzFiles = Program.WzManager.GetWzFileNameListFromBase("mob");
                foreach (string mobWZFile in mobWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(mobWZFile, true);

                    Program.WzManager.LoadWzFile(mobWZFile, _wzMapleVersion);
                }
                ExtractMobFile();


                // Load Npc
                List<string> npcWzFiles = Program.WzManager.GetWzFileNameListFromBase("npc");
                foreach (string npc in npcWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(npc, true);

                    Program.WzManager.LoadWzFile(npc, _wzMapleVersion);
                }
                ExtractNpcFile();

                // Load reactor
                List<string> reactorWzFiles = Program.WzManager.GetWzFileNameListFromBase("reactor");
                foreach (string reactor in reactorWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(reactor, true);

                    Program.WzManager.LoadWzFile(reactor, _wzMapleVersion);
                }
                ExtractReactorFile();

                // Load sound
                List<string> soundWzDirs = Program.WzManager.GetWzFileNameListFromBase("sound");
                foreach (string soundDirName in soundWzDirs)
                {
                    UpdateUI_CurrentLoadingWzFile(soundDirName, true);

                    Program.WzManager.LoadWzFile(soundDirName, _wzMapleVersion);
                }
                ExtractSoundFile();

                // Load quests
                List<string> questWzDirs = Program.WzManager.GetWzFileNameListFromBase("quest");
                foreach (string questWzDir in questWzDirs)
                {
                    UpdateUI_CurrentLoadingWzFile(questWzDir, true);

                    Program.WzManager.LoadWzFile(questWzDir, _wzMapleVersion);
                }
                ExtractQuestFile();

                // Load character
                List<string> characterWzDirs = Program.WzManager.GetWzFileNameListFromBase("character");
                foreach (string characterWzDir in characterWzDirs)
                {
                    UpdateUI_CurrentLoadingWzFile(characterWzDir, true);

                    Program.WzManager.LoadWzFile(characterWzDir, _wzMapleVersion);
                }
                //ExtractCharacterFile(); // due to performance issue, its loaded on demand

                // Load Items
                List<string> itemWzDirs = Program.WzManager.GetWzFileNameListFromBase("item");
                foreach (string itemWzDir in itemWzDirs)
                {
                    UpdateUI_CurrentLoadingWzFile(itemWzDir, true);

                    Program.WzManager.LoadWzFile(itemWzDir, _wzMapleVersion);
                }
                ExtractItemFile();

                // Load maps
                List<string> mapWzFiles = Program.WzManager.GetWzFileNameListFromBase("map");
                foreach (string mapWzFileName in mapWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(mapWzFileName, true);

                    Program.WzManager.LoadWzFile(mapWzFileName, _wzMapleVersion);
                }
                for (int i_map = 0; i_map <= 9; i_map++)
                {
                    List<string> map_iWzFiles = Program.WzManager.GetWzFileNameListFromBase("map\\map\\map" + i_map);
                    foreach (string map_iWzFileName in map_iWzFiles)
                    {
                        UpdateUI_CurrentLoadingWzFile(map_iWzFileName, true);

                        Program.WzManager.LoadWzFile(map_iWzFileName, _wzMapleVersion);
                    }
                }
                List<string> tileWzFiles = Program.WzManager.GetWzFileNameListFromBase("map\\tile"); // this doesnt exist before 64-bit client, and is kept in Map.wz
                foreach (string tileWzFileNames in tileWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(tileWzFileNames, true);

                    Program.WzManager.LoadWzFile(tileWzFileNames, _wzMapleVersion);
                }
                List<string> objWzFiles = Program.WzManager.GetWzFileNameListFromBase("map\\obj"); // this doesnt exist before 64-bit client, and is kept in Map.wz
                foreach (string objWzFileName in objWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(objWzFileName, true);

                    Program.WzManager.LoadWzFile(objWzFileName, _wzMapleVersion);
                }
                List<string> backWzFiles = Program.WzManager.GetWzFileNameListFromBase("map\\back"); // this doesnt exist before 64-bit client, and is kept in Map.wz
                foreach (string backWzFileName in backWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(backWzFileName, true);

                    Program.WzManager.LoadWzFile(backWzFileName, _wzMapleVersion);
                }
                ExtractMapMarks();
                ExtractMapPortals();
                ExtractMapTileSets();
                ExtractMapObjSets();
                ExtractMapBackgroundSets();
                ExtractMaps();

                // UI.wz
                List<string> uiWzFiles = Program.WzManager.GetWzFileNameListFromBase("ui");
                foreach (string uiWzFileNames in uiWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(uiWzFileNames, true);

                    Program.WzManager.LoadWzFile(uiWzFileNames, _wzMapleVersion);
                }
            }
            return true;
        }

        private void UpdateUI_CurrentLoadingWzFile(string fileName, bool isWzFile)
        {
            textBox2.Text = string.Format("Initializing {0}{1}...", fileName, isWzFile ? ".wz" : "");
            Application.DoEvents();
        }

        /// <summary>
        /// On loading initialization.cs
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Initialization_Load(object sender, EventArgs e)
        {
            versionBox.SelectedIndex = 0;
            try
            {
                string[] paths = ApplicationSettings.MapleFoldersList.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (string path in paths)
                {
                    if (!Directory.Exists(path)) // check if the old path actually exist before adding it to the combobox
                        continue;

                    pathBox.Items.Add(path);
                }
                foreach (string path in WzFileManager.COMMON_MAPLESTORY_DIRECTORY) // default path list
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
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog mapleSelect = new FolderBrowserDialog()
            {
                ShowNewFolderButton = true,
                //   RootFolder = Environment.SpecialFolder.ProgramFilesX86,
                Description = "Select the MapleStory folder."
            })
            {
                if (mapleSelect.ShowDialog() != DialogResult.OK)
                    return;

                pathBox.Items.Add(mapleSelect.SelectedPath);
                pathBox.SelectedIndex = pathBox.Items.Count - 1;
            };
        }

        /// <summary>
        /// Debug button for check map errors
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void debugButton_Click(object sender, EventArgs e)
        {
            const string OUTPUT_ERROR_FILENAME = "Debug_errors.txt";

            // This function iterates over all maps in the game and verifies that we recognize all their props
            // It is meant to use by the developer(s) to speed up the process of adjusting this program for different MapleStory versions
            string wzPath = pathBox.Text;

            WzMapleVersion fileVersion = (WzMapleVersion)versionBox.SelectedIndex;
            if (!InitializeWzFiles(wzPath, fileVersion))
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

                    //MapLoader.LoadBackgrounds(mapImage, board);
                    //MapLoader.LoadMisc(mapImage, board);

                    // Check background to ensure that its correct
                    List<BackgroundInstance> allBackgrounds = new List<BackgroundInstance>();
                    allBackgrounds.AddRange(mapBoard.BoardItems.BackBackgrounds);
                    allBackgrounds.AddRange(mapBoard.BoardItems.FrontBackgrounds);

                    foreach (BackgroundInstance bg in allBackgrounds)
                    {
                        if (bg.type != MapleLib.WzLib.WzStructure.Data.BackgroundType.Regular)
                        {
                            if (bg.cx < 0 || bg.cy < 0)
                            {
                                string error = string.Format("Negative CX/ CY moving background object. CX='{0}', CY={1}, Type={2}, {3}{4}", bg.cx, bg.cy, bg.type.ToString(), Environment.NewLine, mapImage.ToString() /*overrides, see WzImage.ToString*/);
                                ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                            }
                        }
                    }
                    allBackgrounds.Clear();
                }
                catch (Exception exp)
                {
                    string error = string.Format("Exception occured loading {0}{1}{2}{3}", Environment.NewLine, mapImage.ToString() /*overrides, see WzImage.ToString*/, Environment.NewLine, exp.ToString());
                    ErrorLogger.Log(ErrorLevel.Crash, error);
                }
                finally
                {
                    mapBoard.Dispose();

                    mapBoard.BoardItems.BackBackgrounds.Clear();
                    mapBoard.BoardItems.FrontBackgrounds.Clear();

                    mapImage.UnparseImage(); // To preserve memory, since this is a very memory intensive test
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
        /// <param name="sender"></param>
        /// <param name="e"></param>
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


        #region Extractor
        /// <summary>
        /// Mob.wz
        /// </summary>
        public void ExtractMobFile()
        {
            if (Program.InfoManager.Mobs.Count != 0)
                return;

            // Mob.wz
            List<WzDirectory> mobWzDirs = Program.WzManager.GetWzDirectoriesFromBase("mob");

            foreach (WzDirectory mobWzDir in mobWzDirs)
            {
            }

            // String.wz
            List<WzDirectory> stringWzDirs = Program.WzManager.GetWzDirectoriesFromBase("string");
            foreach (WzDirectory stringWzDir in stringWzDirs)
            {
                WzImage mobStringImage = (WzImage)stringWzDir?["mob.img"];
                if (mobStringImage == null)
                    continue; // not in this wz

                foreach (WzSubProperty mob in mobStringImage.WzProperties)
                {
                    WzStringProperty nameProp = (WzStringProperty)mob["name"];
                    string name = nameProp == null ? "" : nameProp.Value;

                    Program.InfoManager.Mobs.Add(WzInfoTools.AddLeadingZeros(mob.Name, 7), name);
                }
            }
        }

        /// <summary>
        /// NPC.wz
        /// </summary>
        public void ExtractNpcFile()
        {
            if (Program.InfoManager.NPCs.Count != 0)
                return;

            // Npc.wz
            List<WzDirectory> npcWzDirs = Program.WzManager.GetWzDirectoriesFromBase("npc");

            foreach (WzDirectory npcWzDir in npcWzDirs)
            {
            }

            // String.wz
            List<WzDirectory> stringWzDirs = Program.WzManager.GetWzDirectoriesFromBase("string");
            foreach (WzDirectory stringWzDir in stringWzDirs)
            {
                WzImage npcImage = (WzImage)stringWzDir?["Npc.img"];
                if (npcImage == null)
                    continue; // not in this wz

                foreach (WzSubProperty npc in npcImage.WzProperties)
                {
                    WzStringProperty nameProp = (WzStringProperty)npc["name"];
                    string name = nameProp == null ? "" : nameProp.Value;

                    Program.InfoManager.NPCs.Add(WzInfoTools.AddLeadingZeros(npc.Name, 7), name);
                }
            }
        }

        /// <summary>
        /// Reactor.wz
        /// </summary>
        public void ExtractReactorFile()
        {
            if (Program.InfoManager.Reactors.Count != 0)
                return;

            List<WzDirectory> reactorWzDirs = Program.WzManager.GetWzDirectoriesFromBase("reactor");
            foreach (WzDirectory reactorWzDir in reactorWzDirs)
            {
                foreach (WzImage reactorImage in reactorWzDir.WzImages)
                {
                    ReactorInfo reactor = ReactorInfo.Load(reactorImage);
                    Program.InfoManager.Reactors[reactor.ID] = reactor;
                }
            }
        }

        /// <summary>
        /// Quest.wz
        /// </summary>
        public void ExtractQuestFile()
        {
            if (Program.InfoManager.QuestActs.Count != 0) // already loaded
                return;

            List<WzDirectory> questWzDirs = Program.WzManager.GetWzDirectoriesFromBase("quest");
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

                        case "ChangeableQExpTable.img": // later ver of maplestory
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
        }

        public void ExtractCharacterFile()
        {
            // disabled due to performance issue on startup
            // only load on demand

            /*if (Program.InfoManager.MapsNameCache.Count == 0)
                throw new Exception("ExtractStringWzFile needs to be called first.");
            else if (Program.InfoManager.EquipItemCache.Count != 0)
                return; // loaded

            List<WzDirectory> characterWzDirs = Program.WzManager.GetWzDirectoriesFromBase("character");
            foreach (WzDirectory characterWzDir in characterWzDirs)
            {
                // foreach (WzDirectory characterWzImage in characterWzDir.WzDirectories)
                Parallel.ForEach(characterWzDir.WzDirectories, characterWzImage =>
                {
                    switch (characterWzImage.Name)
                    {
                        case "Afterimage": // weapon delays
                            break;
                        default:
                            {
                                foreach (WzImage itemImg in characterWzImage.WzImages)
                                {
                                    string itemId = itemImg.Name.Replace(".img", "");
                                    WzCanvasProperty icon = itemImg["info"]?["icon"] as WzCanvasProperty;
                                    if (icon != null)
                                    {
                                        int intName = 0;
                                        int.TryParse(itemId, out intName);

                                        Program.InfoManager.EquipItemCache.Add(intName, itemImg);
                                    }
                                }
                                break;
                            }
                    }
                });
            }*/
        }

        /// <summary>
        /// Item.wz
        /// </summary>
        public void ExtractItemFile()
        {
            if (Program.InfoManager.MapsNameCache.Count == 0)
                throw new Exception("ExtractStringWzFile needs to be called first.");
            else if (Program.InfoManager.ItemIconCache.Count != 0)
                return; // loaded

            List<WzDirectory> itemWzDirs = Program.WzManager.GetWzDirectoriesFromBase("item");
            foreach (WzDirectory itemWzDir in itemWzDirs)
            {
                Parallel.ForEach(itemWzDir.WzDirectories, itemWzImage =>
                //foreach (WzDirectory itemWzImage in itemWzDir.WzDirectories)
                {
                    switch (itemWzImage.Name)
                    {
                        case "ItemOption.img":
                        case "Special":
                            {
                                break;
                            }
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
                                        WzCanvasProperty icon = itemProp["info"]?["icon"] as WzCanvasProperty;
                                        if (icon != null)
                                        {
                                            int intName = 0;
                                            int.TryParse(itemId, out intName);

                                            Program.InfoManager.ItemIconCache.Add(intName, icon);
                                        }
                                    }
                                }
                                break;
                            }
                        case "Pet": // pet doesnt have a group directory
                            {
                                foreach (WzImage petImg in itemWzImage.WzImages)
                                {
                                    string itemId = petImg.Name.Replace(".img", "");
                                    WzCanvasProperty icon = petImg["info"]?["icon"] as WzCanvasProperty;
                                    if (icon != null)
                                    {
                                        int intName = 0;
                                        int.TryParse(itemId, out intName);

                                        Program.InfoManager.ItemIconCache.Add(intName, icon);
                                    }
                                }
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                });
            }
        }

        /// <summary>
        /// Sound.wz
        /// </summary>
        public void ExtractSoundFile() {
            if (Program.InfoManager.BGMs.Count != 0)
                return;

            List<WzDirectory> soundWzDirs = Program.WzManager.GetWzDirectoriesFromBase("sound");

            foreach (WzDirectory soundWzDir in soundWzDirs) 
            {
                if (Program.WzManager.IsPreBBDataWzFormat) {
                    WzDirectory x = (WzDirectory) soundWzDir["Sound"];
                }

                foreach (WzImage soundImage in soundWzDir.WzImages)
                {
                    if (!soundImage.Name.ToLower().Contains("bgm"))
                        continue;
                    try {
                        foreach (WzImageProperty bgmImage in soundImage.WzProperties) {
                            WzBinaryProperty binProperty = null;
                            if (bgmImage is WzBinaryProperty bgm) {
                                binProperty = bgm;
                            }
                            else if (bgmImage is WzUOLProperty uolBGM) // is UOL property
                            {
                                WzObject linkVal = ((WzUOLProperty)bgmImage).LinkValue;
                                if (linkVal is WzBinaryProperty linkCanvas) {
                                    binProperty = linkCanvas;
                                }
                            }

                            if (binProperty != null)
                                Program.InfoManager.BGMs[WzInfoTools.RemoveExtension(soundImage.Name) + @"/" + binProperty.Name] = binProperty;
                        }
                    }
                    catch (Exception e) {
                        string error = string.Format("[ExtractSoundFile] Error parsing {0}, {1} file.\r\nError: {2}", soundWzDir.Name, soundImage.Name, e.ToString());
                        MapleLib.Helpers.ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Map marks
        /// </summary>
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

        /// <summary>
        /// Map tiles
        /// </summary>
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

            // Not loaded, try to find it in "tile.wz"
            // on 64-bit client it is stored in a different file apart from map
            if (!bLoadedInMap)
            {
                List<WzDirectory> tileWzDirs = Program.WzManager.GetWzDirectoriesFromBase("map\\tile");
                foreach (WzDirectory tileWzDir in tileWzDirs)
                {
                    foreach (WzImage tileset in tileWzDir.WzImages)
                        Program.InfoManager.TileSets[WzInfoTools.RemoveExtension(tileset.Name)] = tileset;
                }
            }

            // Finally
            // Sort order in advance
            Program.InfoManager.TileSets = Program.InfoManager.TileSets.OrderBy(kvp => kvp.Key)
                   .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Handle various scenarios ie Map001.wz exists but may only contain Back or only Obj etc
        /// </summary>
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
                return; // only needs to be loaded once
            }

            // Not loaded, try to find it in "tile.wz"
            // on 64-bit client it is stored in a different file apart from map
            if (!bLoadedInMap)
            {
                List<WzDirectory> objWzDirs = Program.WzManager.GetWzDirectoriesFromBase("map\\obj");
                foreach (WzDirectory objWzDir in objWzDirs)
                {
                    foreach (WzImage objset in objWzDir.WzImages)
                        Program.InfoManager.ObjectSets[WzInfoTools.RemoveExtension(objset.Name)] = objset;
                }
            }
        }

        /// <summary>
        /// Map background sets
        /// </summary>
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

            // Not loaded, try to find it in "tile.wz"
            // on 64-bit client it is stored in a different file apart from map
            if (!bLoadedInMap)
            {
                List<WzDirectory> backWzDirs = Program.WzManager.GetWzDirectoriesFromBase("map\\back");
                foreach (WzDirectory backWzDir in backWzDirs)
                {
                    foreach (WzImage bgset in backWzDir.WzImages)
                        Program.InfoManager.BackgroundSets[WzInfoTools.RemoveExtension(bgset.Name)] = bgset;
                }
            }
        }

        /// <summary>
        /// Extracts all string.wz map list and places it in Program.InfoManager.Maps dictionary
        /// </summary>
        public void ExtractStringFile()
        {
            if (Program.InfoManager.MapsNameCache.Count != 0)
                return;

            // Npc strings
            WzImage stringNpcImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Npc.img");
            foreach (WzSubProperty npcImg in stringNpcImg.WzProperties) // String.wz/Npc.img/2000
            {
                string npcId = npcImg.Name;
                string npcName = (npcImg["name"] as WzStringProperty)?.Value ?? "NO NAME";

                if (!Program.InfoManager.NpcNameCache.ContainsKey(npcId))
                    Program.InfoManager.NpcNameCache[npcId] = npcName;
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
                    else {
                        Program.InfoManager.MapsNameCache[mapIdStr] = new Tuple<string, string, string>(
                            streetNameWzProp?.Value == null ? string.Empty : streetNameWzProp.Value, 
                            mapNameWzProp.Value,
                            categoryName);
                    }
                }
            }

            // Mob strings
            WzImage stringMobImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Mob.img");
            foreach (WzSubProperty mobImg in stringMobImg.WzProperties) // String.wz/Mob.img/100100
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

            // Item strings
            WzImage stringEqpImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Eqp.img");
            foreach (WzSubProperty eqpSubProp in stringEqpImg.WzProperties)
            {
                foreach (WzSubProperty eqpCategorySubProp in eqpSubProp.WzProperties)
                {
                    foreach (WzSubProperty eqpItemSubProp in eqpCategorySubProp.WzProperties) // String.wz/Eqp.img/Accessory/1010000
                    {
                        string itemId = eqpItemSubProp.Name;
                        string itemCategory = eqpCategorySubProp.Name;
                        string itemName = (eqpItemSubProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
                        string itemDesc = (eqpItemSubProp["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                        int intName = 0;
                        int.TryParse(itemId, out intName);

                        if (!Program.InfoManager.ItemNameCache.ContainsKey(intName))
                            Program.InfoManager.ItemNameCache[intName] = new Tuple<string, string, string>(itemCategory, itemName, itemDesc);
                        else
                        {
                            string error = string.Format("[Initialization] Duplicate [Equip] item name in String.wz. ItemId='{0}', Category={1}", itemId, eqpCategorySubProp.Name);
                            ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                        }
                    }
                }
            }

            WzImage stringInsImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Ins.img");
            foreach (WzSubProperty insItemImg in stringInsImg.WzProperties) // String.wz/Ins.img/3010000
            {
                string itemId = insItemImg.Name;
                const string itemCategory = "Ins";
                string itemName = (insItemImg["name"] as WzStringProperty)?.Value ?? "NO NAME";
                string itemDesc = (insItemImg["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                int intName = 0;
                int.TryParse(itemId, out intName);

                if (!Program.InfoManager.ItemNameCache.ContainsKey(intName))
                    Program.InfoManager.ItemNameCache[intName] = new Tuple<string, string, string>(itemCategory, itemName, itemDesc);
                else
                {
                    string error = string.Format("[Initialization] Duplicate [Ins] item name in String.wz. ItemId='{0}', Category={1}", itemId, insItemImg.Name);
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                }
            }

            WzImage stringCashImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Cash.img");
            foreach (WzSubProperty cashItemImg in stringCashImg.WzProperties) // String.wz/Cash.img/5010000
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

            WzImage stringConsumeImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Consume.img");
            foreach (WzSubProperty consumeItemImg in stringConsumeImg.WzProperties) // String.wz/Cash.img/5010000
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

            WzImage stringEtcImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Etc.img");
            foreach (WzSubProperty etcSubProp in stringEtcImg.WzProperties)
            {
                foreach (WzSubProperty etcItemSubProp in etcSubProp.WzProperties) // String.wz/Etc.img/Etc/1010000
                {
                    string itemId = etcItemSubProp.Name;
                    const string itemCategory = "Etc";
                    string itemName = (etcItemSubProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
                    string itemDesc = (etcItemSubProp["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                    int intName = 0;
                    int.TryParse(itemId, out intName);

                    if (!Program.InfoManager.ItemNameCache.ContainsKey(intName))
                        Program.InfoManager.ItemNameCache[intName] = new Tuple<string, string, string>(itemCategory, itemName, itemDesc);
                    else
                    {
                        string error = string.Format("[Initialization] Duplicate [Etc] item name in String.wz. ItemId='{0}', Category={1}", itemId, etcSubProp.Name);
                        ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                    }
                }
            }

        }

        /// <summary>
        /// Pre-load all maps in the memory
        /// </summary>
        public void ExtractMaps() {
            if (Program.InfoManager.MapsCache.Count != 0)
                return;

            UpdateUI_CurrentLoadingWzFile(string.Format("{0} map data", Program.InfoManager.MapsNameCache.Count), false);

            // Create a ParallelOptions object
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2 // number of concurrent tasks, typically bottlenecked by disk I/O and RAM speeds
            };

            //foreach (KeyValuePair<string, Tuple<string, string>> val in Program.InfoManager.MapsNameCache) {
            Parallel.ForEach(Program.InfoManager.MapsNameCache, parallelOptions, val => {
                int mapid = 0;
                int.TryParse(val.Key, out mapid);

                WzImage mapImage = WzInfoTools.FindMapImage(mapid.ToString(), Program.WzManager);
                if (mapImage != null) { // its okay if the image is not found, sometimes there may be strings in String.wz but not the actual maps in Map.wz
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
                    MapInfo info = new MapInfo(mapImage, mapName, streetName, categoryName);

                    // Ensure thread safety when writing to the shared resource
                    lock (Program.InfoManager.MapsCache) {
                        Program.InfoManager.MapsCache[val.Key] = new Tuple<WzImage, string, string, string, MapInfo>(
                            mapImage, mapName, streetName, categoryName, info
                        );
                    }
                }
            //}
            });
        }

        /// <summary>
        /// Map portals
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void ExtractMapPortals()
        {
            if (Program.InfoManager.GamePortals.Count != 0)
                return;

            WzImage mapImg = (WzImage)Program.WzManager.FindWzImageByName("map", "MapHelper.img");
            if (mapImg == null)
                throw new Exception("Couldn't extract portals. MapHelper.img not found.");

            WzSubProperty portalParent = (WzSubProperty)mapImg["portal"];
            WzSubProperty editorParent = (WzSubProperty)portalParent["editor"];
            for (int i = 0; i < editorParent.WzProperties.Count; i++)
            {
                WzCanvasProperty portal = (WzCanvasProperty)editorParent.WzProperties[i];
                Program.InfoManager.PortalTypeById.Add(portal.Name);
                PortalInfo.Load(portal);
            }

            WzSubProperty gameParent = (WzSubProperty)portalParent["game"]["pv"];
            foreach (WzImageProperty portal in gameParent.WzProperties)
            {
                if (portal.WzProperties[0] is WzSubProperty)
                {
                    Dictionary<string, Bitmap> images = new Dictionary<string, Bitmap>();
                    Bitmap defaultImage = null;
                    foreach (WzSubProperty image in portal.WzProperties)
                    {
                        //WzSubProperty portalContinue = (WzSubProperty)image["portalContinue"];
                        //if (portalContinue == null) continue;
                        Bitmap portalImage = image["0"].GetBitmap();
                        if (image.Name == "default")
                            defaultImage = portalImage;
                        else
                            images.Add(image.Name, portalImage);
                    }
                    Program.InfoManager.GamePortals.Add(portal.Name, new PortalGameImageInfo(defaultImage, images));
                }
                else if (portal.WzProperties[0] is WzCanvasProperty)
                {
                    Dictionary<string, Bitmap> images = new Dictionary<string, Bitmap>();
                    Bitmap defaultImage = null;
                    try
                    {
                        foreach (WzCanvasProperty image in portal.WzProperties)
                        {
                            //WzSubProperty portalContinue = (WzSubProperty)image["portalContinue"];
                            //if (portalContinue == null) continue;
                            Bitmap portalImage = image.GetLinkedWzCanvasBitmap();
                            defaultImage = portalImage;
                            images.Add(image.Name, portalImage);
                        }
                        Program.InfoManager.GamePortals.Add(portal.Name, new PortalGameImageInfo(defaultImage, images));
                    }
                    catch (InvalidCastException)
                    {
                        continue;
                    } //nexon likes to toss ints in here zType etc
                }
            }

            for (int i = 0; i < Program.InfoManager.PortalTypeById.Count; i++)
            {
                Program.InfoManager.PortalIdByType[Program.InfoManager.PortalTypeById[i]] = i;
            }
        }
        #endregion
    }
}