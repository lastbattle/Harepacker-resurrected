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
using MapleLib.WzLib.WzStructure.Data;

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
                ApplicationSettings.MapleStoryClientLocalisation = (int) comboBox_localisation.SelectedValue;

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

                ExtractStringFile(true);
                //Program.WzManager.ExtractItems();

                ExtractMobFile();
                ExtractNpcFile();
                ExtractReactorFile();
                ExtractSoundFile();
                ExtractQuestFile();
                //ExtractCharacterFile(); // due to performance issue, its loaded on demand
                ExtractSkillFile();
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
                ExtractStringFile(false);

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

                // Load skills
                List<string> skillWzDirs = Program.WzManager.GetWzFileNameListFromBase("skill");
                foreach (string skillWzDir in skillWzDirs)
                {
                    UpdateUI_CurrentLoadingWzFile(skillWzDir, true);

                    Program.WzManager.LoadWzFile(skillWzDir, _wzMapleVersion);
                }
                ExtractSkillFile();

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

            // Populate the MapleStory localisation box
            var values = Enum.GetValues(typeof(MapleLib.ClientLib.MapleStoryLocalisation))
                    .Cast<MapleLib.ClientLib.MapleStoryLocalisation>()
                    .Select(v => new
                    {
                        Text = v.ToString().Replace("MapleStory", "MapleStory "),
                        Value = (int)v
                    })
                    .ToList();
            // set ComboBox properties
            comboBox_localisation.DataSource = values;
            comboBox_localisation.DisplayMember = "Text";
            comboBox_localisation.ValueMember = "Value";

            var savedLocaliation = values.Where(x => x.Value == ApplicationSettings.MapleStoryClientLocalisation).FirstOrDefault(); // get the saved location from settings
            comboBox_localisation.SelectedItem = savedLocaliation ?? values[0]; // KMS if null
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog mapleSelect = new()
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
            const string OUTPUT_ERROR_FILENAME = "Errors_MapDebug.txt";

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
            if (Program.InfoManager.MobIconCache.Count != 0)
                return;

            // Mob.wz
            List<WzDirectory> mobWzDirs = Program.WzManager.GetWzDirectoriesFromBase("mob");

            foreach (WzDirectory mobWzDir in mobWzDirs)
            {
                foreach (WzImage mobImage in mobWzDir.WzImages)
                {
                    string mobIdStr = mobImage.Name.Replace(".img", "");
                    int mobId = int.Parse(mobIdStr);

                    WzImageProperty standCanvas = (WzCanvasProperty) mobImage["stand"]?["0"]?.GetLinkedWzImageProperty();

                    if (standCanvas == null) continue;

                    if (!Program.InfoManager.MobIconCache.ContainsKey(mobId))
                        Program.InfoManager.MobIconCache.Add(mobId, standCanvas);
                }
            }
        }

        /// <summary>
        /// NPC.wz
        /// </summary>
        public void ExtractNpcFile()
        {
            if (Program.InfoManager.NpcPropertyCache.Count != 0)
                return;

            // Npc.wz
            List<WzDirectory> npcWzDirs = Program.WzManager.GetWzDirectoriesFromBase("npc");

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

            // String.wz
            /*List<WzDirectory> stringWzDirs = Program.WzManager.GetWzDirectoriesFromBase("string");
            foreach (WzDirectory stringWzDir in stringWzDirs)
            {
                WzImage npcImage = (WzImage)stringWzDir?["Npc.img"];
                if (npcImage == null)
                    continue; // not in this wz

                foreach (WzSubProperty npc in npcImage.WzProperties)
                {
                    WzStringProperty nameProp = (WzStringProperty)npc["name"];
                    string name = nameProp == null ? "" : nameProp.Value;

                    Program.InfoManager.NPCs.Add(WzInfoTools.AddLeadingZeros(npc.Name, 7), new Tuple<WzSubProperty, string>(npc, name));
                }
            }*/
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
        /// Skill.wz
        /// </summary>
        public void ExtractSkillFile()
        {
            if (Program.InfoManager.SkillWzImageCache.Count != 0)
                return;

            List<WzDirectory> skillWzDirs = Program.WzManager.GetWzDirectoriesFromBase("skill");
            foreach (WzDirectory skillWzDir in skillWzDirs)
            {
                foreach (WzImage skillWzImage in skillWzDir.WzImages) // <imgdir name="9201.img">
                {
                    string skillDirectoryId = skillWzImage.Name;

                    WzImageProperty imgSkill = skillWzImage["skill"]; // in each xml contains the skills
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
        /// <paramref name="bIsBetaMapleStory">Versions before 30 with Data.wz</paramref>
        /// </summary>
        public void ExtractStringFile(bool bIsBetaMapleStory)
        {
            if (Program.InfoManager.MapsNameCache.Count != 0)
                return;

            // Npc strings
            WzImage stringNpcImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Npc.img");
            foreach (WzSubProperty npcImg in stringNpcImg.WzProperties) // String.wz/Npc.img/2000
            {
                string npcId = npcImg.Name;
                string npcName = (npcImg["name"] as WzStringProperty)?.Value ?? "NO NAME";
                string npcFunc = (npcImg["func"] as WzStringProperty)?.Value ?? string.Empty;  // dont use "NO FUNC" for desc

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
                
                //string WzInfoTools.AddLeadingZeros(mob.Name, 7)

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
            foreach (WzSubProperty skillImg in stringSkillImg.WzProperties) // String.wz/Mob.img/100100
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

            // Item strings
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
                        // In beta, process the category property directly
                        ExtractStringFile_ProcessEquipmentItem(eqpCategorySubProp, eqpCategorySubProp.Name);
                    }
                    else
                    {
                        // In non-beta, process each item within the category
                        eqpCategorySubProp.WzProperties
                            //.Cast<WzSubProperty>()
                            .ToList()
                            .ForEach(itemProp => ExtractStringFile_ProcessEquipmentItem(
                                itemProp,
                                eqpCategorySubProp.Name));
                    }
                }
            }

            WzPropertyCollection stringInsImg;
            if (bIsBetaMapleStory)
                stringInsImg = ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Ins"]).WzProperties;
            else
                stringInsImg = ((WzImage)Program.WzManager.FindWzImageByName("string", "Ins.img")).WzProperties;

            foreach (WzImageProperty insItemImage in stringInsImg) // String.wz/Ins.img/3010000
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
                } else
                {
                    // TOOD: Handle MapleStoryN related items
                    // WzUOLProperty? or is it a mistake
                    // Ins/3019381 Ins/3700770 Ins/3700771
                }
            }

            WzPropertyCollection stringCashImg;
            if (bIsBetaMapleStory)
                stringCashImg = ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Cash"]).WzProperties;
            else
                stringCashImg = ((WzImage)Program.WzManager.FindWzImageByName("string", "Cash.img")).WzProperties;

            foreach (WzSubProperty cashItemImg in stringCashImg) // String.wz/Cash.img/5010000
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

            foreach (WzSubProperty consumeItemImg in stringConsumeImg) // String.wz/Cash.img/5010000
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

            foreach (WzSubProperty etcSubProp in stringEtcImg) // String.wz/Etc.img/Etc/1010000
            {
                if (bIsBetaMapleStory)
                {
                    // In beta, process the property directly
                    ExtractStringFile_ProcessEtcItem(etcSubProp, etcSubProp.Name);
                }
                else
                {
                    // In non-beta, process each item within the property
                    etcSubProp.WzProperties
                        .Cast<WzSubProperty>()
                        .ToList()
                        .ForEach(itemProp => ExtractStringFile_ProcessEtcItem(
                            itemProp,
                            etcSubProp.Name));
                }
            }

            WzPropertyCollection stringPetImg;
            if (bIsBetaMapleStory)
                stringPetImg = ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Pet"]).WzProperties;
            else
                stringPetImg = ((WzImage)Program.WzManager.FindWzImageByName("string", "Pet.img")).WzProperties;

            // In non-beta, process each item within the category
            stringPetImg
                //.Cast<WzSubProperty>()
                .ToList()
                .ForEach(itemProp => ExtractStringFile_ProcessPetItem(
                    itemProp
                    ));
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
                string itemDescD = (petSubProp["descD"] as WzStringProperty)?.Value ?? "NO DESC"; // if the pet is dead

                int intName = 0;
                int.TryParse(itemId, out intName);

                if (!Program.InfoManager.ItemNameCache.ContainsKey(intName))
                    Program.InfoManager.ItemNameCache[intName] = new Tuple<string, string, string>(itemCategory, itemName, itemDesc);
                else
                {
                    string error = string.Format("[Initialization] Duplicate [{0}] item name in String.wz. ItemId='{1}', Category={2}", itemCategory, itemId, petSubProp.Name);
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                }
            } else
            {
                // 5002129, 5002128, 5002127, 5002223, 5002224, 
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
            } else
            {
                // TODO: Handle MapleStoryN related equipments
                // WzUOLProperty? or is it a mistake
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
                    if (mapImage["info"] != null)
                    {
                        MapInfo info = new MapInfo(mapImage, mapName, streetName, categoryName);

                        // Ensure thread safety when writing to the shared resource
                        lock (Program.InfoManager.MapsCache)
                        {
                            Program.InfoManager.MapsCache[val.Key] = new Tuple<WzImage, string, string, string, MapInfo>(
                                mapImage, mapName, streetName, categoryName, info
                            );
                        }
                    } else
                    {
                        // Japan Maplestory
                        // MapID 100020100, 100020101, 120000100, 120000200, 120000201, 120000202, 120000300, 120000301, 120000101
                        // is missing of "info"
                        // it is an empty "map".img, likely pre-bb deleted 
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
            if (Program.InfoManager.PortalGame.Count != 0)
                return;

            WzImage mapImg = (WzImage)Program.WzManager.FindWzImageByName("map", "MapHelper.img");
            if (mapImg == null)
                throw new Exception("Couldn't extract portals. MapHelper.img not found.");

            WzSubProperty portalParent = (WzSubProperty)mapImg["portal"];

            // Editor portals
            WzSubProperty editorParent = (WzSubProperty)portalParent["editor"];
            foreach (WzCanvasProperty portalProp in editorParent.WzProperties) // sp, pi, pv, pc, pg, tp, ps, ph, pcj, pci, pci2, pcig, pshg, pcir
            {
                Program.InfoManager.PortalEditor_TypeById.Add(PortalTypeExtensions.FromCode(portalProp.Name));
                PortalInfo.Load(portalProp);
            }

            // Game portals
            WzSubProperty gameParent = (WzSubProperty)portalParent["game"];
            foreach (WzImageProperty portalProp in gameParent.WzProperties) // "pv", "ph", "psh"
            {
                PortalType portalType = PortalTypeExtensions.FromCode(portalProp.Name);

                // These are portal types with multiple image template.
                if (portalProp["default"]?["portalStart"] != null) // psh, ph. 
                {
                    Dictionary<string, List<Bitmap>> portalTemplateImage = new(); 

                    foreach (WzSubProperty portalImageProp in portalProp.WzProperties)  // "1", "2", "default"
                    {
                        WzSubProperty portalStartProp = portalImageProp["portalStart"] as WzSubProperty; // "portalStart", "portalContinue", "portalExit"
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
                else // pv. 
                {
                    // These are portal types with only a single image template.

                    Dictionary<string, List<Bitmap>> portalTemplateImage = new();
                    Bitmap defaultImage = null;

                    List<Bitmap> images = new();
                    foreach (WzImageProperty image in portalProp.WzProperties)  // 1,2,3,4,5,6,7,8,9,10,11
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
    }
}