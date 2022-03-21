/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MapleLib.WzLib;
using HaCreator.MapEditor;
using MapleLib.WzLib.Util;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure;
using MapleLib.Helpers;
using HaCreator.MapEditor.Instance;

namespace HaCreator.GUI
{
    public partial class Initialization : System.Windows.Forms.Form
    {
        public HaEditor editor = null;

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

        private void button1_Click(object sender, EventArgs e)
        {
            ApplicationSettings.MapleVersionIndex = versionBox.SelectedIndex;
            ApplicationSettings.MapleFolderIndex = pathBox.SelectedIndex;
            string wzPath = pathBox.Text;

            if (wzPath == "Select MapleStory Folder")
            {
                MessageBox.Show("Please select the MapleStory folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
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
            if (Program.WzManager != null)
            {
                Program.WzManager.Clear();
                Program.WzManager = null; // old loaded items
            }
            if (Program.InfoManager != null)
            {
                Program.InfoManager.Clear();
            }

            Program.WzManager = new WzFileManager(wzPath, fileVersion);

            if (Program.WzManager.HasDataFile) //currently always false
            {
                textBox2.Text = "Initializing Data.wz...";
                Application.DoEvents();
                Program.WzManager.LoadDataWzFile("data");
                Program.WzManager.ExtractStringWzMaps();
                //Program.WzManager.ExtractItems();
                foreach (string mobWZFile in WzFileManager.MOB_WZ_FILES)
                {
                }
                Program.WzManager.ExtractMobFile();
                Program.WzManager.ExtractNpcFile();
                Program.WzManager.ExtractReactorFile();
                Program.WzManager.ExtractSoundFile("sound");
                Program.WzManager.ExtractMapMarks();
                Program.WzManager.ExtractPortals();
                Program.WzManager.ExtractTileSets();
                Program.WzManager.ExtractObjSets();
                Program.WzManager.ExtractBackgroundSets();
            }
            else
            {
                textBox2.Text = "Initializing String.wz...";
                Application.DoEvents();
                Program.WzManager.LoadWzFile("string");
                Program.WzManager.ExtractStringWzMaps();

                // Mob WZ
                foreach (string mobWZFile in WzFileManager.MOB_WZ_FILES)
                {
                    textBox2.Text = string.Format("Initializing {0}.wz...", mobWZFile);
                    Application.DoEvents();
                    if (Program.WzManager.LoadWzFile(mobWZFile.ToLower()))
                    {
                        // mob is a little special... gonna load all 3 wz first
                    }
                }
                Program.WzManager.ExtractMobFile();

                textBox2.Text = "Initializing Npc.wz...";
                Application.DoEvents();
                Program.WzManager.LoadWzFile("npc");
                Program.WzManager.ExtractNpcFile();

                textBox2.Text = "Initializing Reactor.wz...";
                Application.DoEvents();
                Program.WzManager.LoadWzFile("reactor");
                Program.WzManager.ExtractReactorFile();

                // Load sound
                foreach (string soundWzFile in WzFileManager.SOUND_WZ_FILES)
                {
                    textBox2.Text = string.Format("Initializing {0}.wz...", soundWzFile);
                    Application.DoEvents();
                    Program.WzManager.LoadWzFile(soundWzFile.ToLower());
                    Program.WzManager.ExtractSoundFile(soundWzFile.ToLower());
                }

                textBox2.Text = "Initializing Map.wz...";
                Application.DoEvents();
                Program.WzManager.LoadWzFile("map");
                Program.WzManager.ExtractMapMarks();
                Program.WzManager.ExtractPortals();
                Program.WzManager.ExtractTileSets();
                Program.WzManager.ExtractObjSets();
                Program.WzManager.ExtractBackgroundSets();

                foreach (string mapwzFile in WzFileManager.MAP_WZ_FILES)
                {
                    if (Program.WzManager.LoadWzFile(mapwzFile.ToLower()))
                    {
                        textBox2.Text = string.Format("Initializing {0}.wz...", mapwzFile);
                        Application.DoEvents();
                        Program.WzManager.ExtractBackgroundSets();
                        Program.WzManager.ExtractObjSets();
                    }
                }

                textBox2.Text = "Initializing UI.wz...";
                Application.DoEvents();
                Program.WzManager.LoadWzFile("ui");
            }
        }


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
        /// Check map errors
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
                string mapcat = "Map" + mapid.Substring(0, 1);

                WzImage mapImage = Program.WzManager.FindMapImage(mapid, mapcat);
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
                    string error = string.Format("Exception occured loading {0}{1}{2}{3}{4}", mapcat, Environment.NewLine, mapImage.ToString() /*overrides, see WzImage.ToString*/, Environment.NewLine, exp.ToString());
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
                button1_Click(null, null);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }
    }
}