/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
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
using HaSharedLibrary;

namespace HaCreator.GUI
{
    public partial class Initialization : System.Windows.Forms.Form
    {
        public HaEditor editor = null;
        public static bool _Client64;

        public static bool IsClient64()
        {
            return _Client64;
        }

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

        /// <summary>
        /// Initialise
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_initialise_Click(object sender, EventArgs e)
        {
            ApplicationSettings.MapleVersionIndex = versionBox.SelectedIndex;
            ApplicationSettings.MapleFolderIndex = pathBox.SelectedIndex;
            string wzPath = pathBox.Text;

            DirectoryInfo di = new DirectoryInfo(wzPath + "\\Data");

            if (wzPath == "Select MapleStory Folder")
            {
                MessageBox.Show("Please select the MapleStory folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (ClientTypeBox.SelectedIndex == -1)
            {
                MessageBox.Show("Please Select the Client Type.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (ClientTypeBox.SelectedIndex == 1)
            {
                if (!di.Exists)
                {
                    MessageBox.Show("Error did not detect Data folder (Perhaps wrong Client Type?)", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            if (!ApplicationSettings.MapleFolder.Contains(wzPath) && !IsPathCommon(wzPath))
            {
                ApplicationSettings.MapleFolder = ApplicationSettings.MapleFolder == "" ? wzPath : (ApplicationSettings.MapleFolder + "," + wzPath);
            }
            WzMapleVersion fileVersion;
            /* short version = -1;
             if (versionBox.SelectedIndex != 3)
             {
                 string testFile = File.Exists(Path.Combine(wzPath, "Data.wz")) ? "Data.wz" : "Item.wz";
                 try
                 {
                     fileVersion = WzTool.DetectMapleVersion(Path.Combine(wzPath, testFile), out version);
                 }
                 catch (Exception ex)
                 {
                     HaRepackerLib.Warning.Error("Error initializing " + testFile + " (" + ex.Message + ").\r\nCheck that the directory is valid and the file is not in use.");
                     return;
                 }
             }
             else
             {*/
            fileVersion = (WzMapleVersion)versionBox.SelectedIndex;
            //  }

            InitializeWzFiles(wzPath, fileVersion);

            Hide();
            Application.DoEvents();
            editor = new HaEditor();

            editor.ShowDialog();
            Application.Exit();
        }

        private void InitializeWzFiles(string wzPath, WzMapleVersion fileVersion)
        {

            if (ClientTypeBox.SelectedIndex == 0)
            {
                SetClientSelection64(false);
            }
            else
            {
                SetClientSelection64(true);
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

            Program.WzManager = new WzFileManager(wzPath, IsClient64());
            Program.WzManager.BuildWzFileList(); // builds the list of WZ files in the directories (for HaCreator)

            // for old maplestory with only Data.wz
            if (Program.WzManager.HasDataFile) //currently always false
            {
                UpdateUI_CurrentLoadingWzFile("Data.wz");

                try
                {
                    Program.WzManager.LoadLegacyDataWzFile("data", _wzMapleVersion);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error initializing data.wz (" + e.Message + ").\r\nCheck that the directory is valid and the file is not in use.");
                    return;
                }

                ExtractStringWzMaps();
                //Program.WzManager.ExtractItems();

                ExtractMobFile();
                ExtractNpcFile();
                ExtractReactorFile();
                ExtractSoundFile();
                ExtractMapMarks();
                ExtractPortals();
                ExtractTileSets();
                ExtractObjSets();
                ExtractBackgroundSets();
            }
            else // for versions beyond v30x
            {
                // String.wz
                List<string> stringWzFiles = Program.WzManager.GetWzFileNameListFromBase("string");
                foreach (string stringWzFileName in stringWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(stringWzFileName);

                    Program.WzManager.LoadWzFile(stringWzFileName, _wzMapleVersion);
                }
                ExtractStringWzMaps();

                // Mob WZ
                List<string> mobWzFiles = Program.WzManager.GetWzFileNameListFromBase("mob");
                foreach (string mobWZFile in mobWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(mobWZFile);

                    Program.WzManager.LoadWzFile(mobWZFile, _wzMapleVersion);
                }
                ExtractMobFile();


                // Load Npc
                List<string> npcWzFiles = Program.WzManager.GetWzFileNameListFromBase("npc");
                foreach (string npc in npcWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(npc);

                    Program.WzManager.LoadWzFile(npc, _wzMapleVersion);
                }
                ExtractNpcFile();

                // Load reactor
                List<string> reactorWzFiles = Program.WzManager.GetWzFileNameListFromBase("reactor");
                foreach (string reactor in reactorWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(reactor);

                    Program.WzManager.LoadWzFile(reactor, _wzMapleVersion);
                }
                ExtractReactorFile();

                // Load sound
                List<string> soundWzFiles = Program.WzManager.GetWzFileNameListFromBase("sound");
                foreach (string soundWzFileName in soundWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(soundWzFileName);

                    Program.WzManager.LoadWzFile(soundWzFileName, _wzMapleVersion);
                    ExtractSoundFile();
                }


                // Load maps
                List<string> mapWzFiles = Program.WzManager.GetWzFileNameListFromBase("map");
                foreach (string mapWzFileName in mapWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(mapWzFileName);

                    Program.WzManager.LoadWzFile(mapWzFileName, _wzMapleVersion);
                }
                for (int i_map = 0; i_map <= 9; i_map++)
                {
                    List<string> map_iWzFiles = Program.WzManager.GetWzFileNameListFromBase("map\\map\\map" + i_map);
                    foreach (string map_iWzFileName in map_iWzFiles)
                    {
                        UpdateUI_CurrentLoadingWzFile(map_iWzFileName);

                        Program.WzManager.LoadWzFile(map_iWzFileName, _wzMapleVersion);
                    }
                }
                List<string> tileWzFiles = Program.WzManager.GetWzFileNameListFromBase("map\\tile"); // this doesnt exist before 64-bit client, and is kept in Map.wz
                foreach (string tileWzFileNames in tileWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(tileWzFileNames);

                    Program.WzManager.LoadWzFile(tileWzFileNames, _wzMapleVersion);
                }
                List<string> objWzFiles = Program.WzManager.GetWzFileNameListFromBase("map\\obj"); // this doesnt exist before 64-bit client, and is kept in Map.wz
                foreach (string objWzFileName in objWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(objWzFileName);

                    Program.WzManager.LoadWzFile(objWzFileName, _wzMapleVersion);
                }
                List<string> backWzFiles = Program.WzManager.GetWzFileNameListFromBase("map\\back"); // this doesnt exist before 64-bit client, and is kept in Map.wz
                foreach (string backWzFileName in backWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(backWzFileName);

                    Program.WzManager.LoadWzFile(backWzFileName, _wzMapleVersion);
                }
                ExtractMapMarks();
                ExtractPortals();
                ExtractTileSets();
                ExtractObjSets();
                ExtractBackgroundSets();


                // UI.wz
                List<string> uiWzFiles = Program.WzManager.GetWzFileNameListFromBase("ui");
                foreach (string uiWzFileNames in uiWzFiles)
                {
                    UpdateUI_CurrentLoadingWzFile(uiWzFileNames);

                    Program.WzManager.LoadWzFile(uiWzFileNames, _wzMapleVersion);
                }
            }
        }

        private void UpdateUI_CurrentLoadingWzFile(string fileName)
        {
            textBox2.Text = string.Format("Initializing {0}.wz...", fileName);
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
                string[] paths = ApplicationSettings.MapleFolder.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (string x in paths)
                {
                    pathBox.Items.Add(x);
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

            // set default client type box 32-bit, 64-bit
            ClientTypeBox.SelectedIndex = ApplicationSettings.WzClientSelectionIndex;
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
            short version = -1;
            WzMapleVersion fileVersion = WzTool.DetectMapleVersion(Path.Combine(wzPath, "Item.wz"), out version);
            InitializeWzFiles(wzPath, fileVersion);

            MultiBoard mb = new MultiBoard();
            Board mapBoard = new Board(
                new Microsoft.Xna.Framework.Point(),
                new Microsoft.Xna.Framework.Point(),
                mb,
                null,
                MapleLib.WzLib.WzStructure.Data.ItemTypes.None,
                MapleLib.WzLib.WzStructure.Data.ItemTypes.None);

            foreach (string mapid in Program.InfoManager.Maps.Keys)
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

        public static void SetClientSelection64(bool isClient64)
        {
            _Client64 = isClient64;
            ApplicationSettings.WzClientSelectionIndex = isClient64 ? 1 : 0;
        }


        #region Extractor
        /// <summary>
        /// 
        /// </summary>
        public void ExtractMobFile()
        {
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

                if (!mobStringImage.Parsed)
                    mobStringImage.ParseImage();
                foreach (WzSubProperty mob in mobStringImage.WzProperties)
                {
                    WzStringProperty nameProp = (WzStringProperty)mob["name"];
                    string name = nameProp == null ? "" : nameProp.Value;

                    Program.InfoManager.Mobs.Add(WzInfoTools.AddLeadingZeros(mob.Name, 7), name);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ExtractNpcFile()
        {
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

                if (!npcImage.Parsed)
                    npcImage.ParseImage();
                foreach (WzSubProperty npc in npcImage.WzProperties)
                {
                    WzStringProperty nameProp = (WzStringProperty)npc["name"];
                    string name = nameProp == null ? "" : nameProp.Value;

                    Program.InfoManager.NPCs.Add(WzInfoTools.AddLeadingZeros(npc.Name, 7), name);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ExtractReactorFile()
        {
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
        /// 
        /// </summary>
        public void ExtractSoundFile()
        {
            List<WzDirectory> soundWzDirs = Program.WzManager.GetWzDirectoriesFromBase("sound");
            foreach (WzDirectory soundWzDir in soundWzDirs)
            {
                foreach (WzImage soundImage in soundWzDir.WzImages)
                {
                    if (!soundImage.Name.ToLower().Contains("bgm"))
                        continue;
                    if (!soundImage.Parsed)
                        soundImage.ParseImage();
                    try
                    {
                        foreach (WzImageProperty bgmImage in soundImage.WzProperties)
                        {
                            WzBinaryProperty binProperty = null;
                            if (bgmImage is WzBinaryProperty bgm)
                            {
                                binProperty = bgm;
                            }
                            else if (bgmImage is WzUOLProperty uolBGM) // is UOL property
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
        }

        /// <summary>
        /// 
        /// </summary>
        public void ExtractMapMarks()
        {
            WzImage mapWzImg = (WzImage)Program.WzManager.FindWzImageByName("map", "MapHelper.img");
            if (mapWzImg == null)
                throw new Exception("MapHelper.img not found in map.wz.");

            foreach (WzCanvasProperty mark in mapWzImg["mark"].WzProperties)
            {
                Program.InfoManager.MapMarks[mark.Name] = mark.GetLinkedWzCanvasBitmap();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ExtractTileSets()
        {
            bool bLoadedInMap = false;

            WzDirectory mapWzDirs = (WzDirectory)Program.WzManager.FindWzImageByName("map", "Tile");
            if (mapWzDirs != null)
            {
                foreach (WzImage tileset in mapWzDirs.WzImages)
                    Program.InfoManager.TileSets[WzInfoTools.RemoveExtension(tileset.Name)] = tileset;

                bLoadedInMap = true;
                return; // only needs to be loaded once
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
        }

        /// <summary>
        /// Handle various scenarios ie Map001.wz exists but may only contain Back or only Obj etc
        /// </summary>
        public void ExtractObjSets()
        {
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
        /// 
        /// </summary>
        public void ExtractBackgroundSets()
        {
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
        /// 
        /// </summary>
        public void ExtractStringWzMaps()
        {
            WzImage stringWzImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Map.img");

            if (!stringWzImg.Parsed)
                stringWzImg.ParseImage();
            foreach (WzSubProperty mapCat in stringWzImg.WzProperties)
            {
                foreach (WzSubProperty map in mapCat.WzProperties)
                {
                    WzStringProperty streetName = (WzStringProperty)map["streetName"];
                    WzStringProperty mapName = (WzStringProperty)map["mapName"];
                    string id;
                    if (map.Name.Length == 9)
                        id = map.Name;
                    else
                        id = WzInfoTools.AddLeadingZeros(map.Name, 9);

                    if (mapName == null)
                        Program.InfoManager.Maps[id] = new Tuple<string, string>("", "");
                    else
                        Program.InfoManager.Maps[id] = new Tuple<string, string>(streetName?.Value == null ? string.Empty : streetName.Value, mapName.Value);
                }
            }
        }

        public void ExtractPortals()
        {
            WzImage mapImg = (WzImage)Program.WzManager.FindWzImageByName("map", "MapHelper.img");
            if (mapImg == null)
                throw new Exception("Couldnt extract portals. MapHelper.img not found.");

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