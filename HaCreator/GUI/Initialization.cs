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

namespace HaCreator.GUI
{
    public partial class Initialization : System.Windows.Forms.Form
    {
        public HaEditor editor = null;

        public Initialization()
        {
            InitializeComponent();
            if (UserSettings.enableDebug)
            {
                debugButton.Visible = true;
            }
        }

        private bool IsPathCommon(string path)
        {
            foreach (string commonPath in commonMaplePaths)
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
            if (wzPath == "Select Maple Folder")
            {
                MessageBox.Show("Please select the maple folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!ApplicationSettings.MapleFolder.Contains(wzPath) && !IsPathCommon(wzPath))
            {
                ApplicationSettings.MapleFolder = ApplicationSettings.MapleFolder == "" ? wzPath : (ApplicationSettings.MapleFolder + "," + wzPath);
            }
            WzMapleVersion fileVersion;
            short version = -1;
            if (versionBox.SelectedIndex == 3)
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
            {
                fileVersion = (WzMapleVersion)versionBox.SelectedIndex;
            }

            InitializeWzFiles(wzPath, fileVersion);

            Hide();
            Application.DoEvents();
            editor = new HaEditor();
            editor.ShowDialog();
            Application.Exit();
        }

        private void InitializeWzFiles(string wzPath, WzMapleVersion fileVersion)
        {
            Program.WzManager = new WzFileManager(wzPath, fileVersion);
            if (Program.WzManager.HasDataFile)
            {
                textBox2.Text = "Initializing Data.wz...";
                Application.DoEvents();
                Program.WzManager.LoadDataWzFile("data");
                Program.WzManager.ExtractMaps();
                //Program.WzManager.ExtractItems();
                Program.WzManager.ExtractMobFile();
                Program.WzManager.ExtractNpcFile();
                Program.WzManager.ExtractReactorFile();
                Program.WzManager.ExtractSoundFile();
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
                Program.WzManager.ExtractMaps();
                //Program.WzManager.ExtractItems();
                textBox2.Text = "Initializing Mob.wz...";
                Application.DoEvents();
                Program.WzManager.LoadWzFile("mob");
                Program.WzManager.ExtractMobFile();
                textBox2.Text = "Initializing Npc.wz...";
                Application.DoEvents();
                Program.WzManager.LoadWzFile("npc");
                Program.WzManager.ExtractNpcFile();
                textBox2.Text = "Initializing Reactor.wz...";
                Application.DoEvents();
                Program.WzManager.LoadWzFile("reactor");
                Program.WzManager.ExtractReactorFile();
                textBox2.Text = "Initializing Sound.wz...";
                Application.DoEvents();
                Program.WzManager.LoadWzFile("sound");
                Program.WzManager.ExtractSoundFile();
                textBox2.Text = "Initializing Map.wz...";
                Application.DoEvents();
                Program.WzManager.LoadWzFile("map");
                Program.WzManager.ExtractMapMarks();
                Program.WzManager.ExtractPortals();
                Program.WzManager.ExtractTileSets();
                Program.WzManager.ExtractObjSets();
                Program.WzManager.ExtractBackgroundSets();
                textBox2.Text = "Initializing UI.wz...";
                Application.DoEvents();
                Program.WzManager.LoadWzFile("ui");
            }
        }


        private static readonly string[] commonMaplePaths = new string[] { @"C:\Nexon\MapleStory", @"C:\Program Files\WIZET\MapleStory", @"C:\MapleStory" };

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
                foreach (string path in commonMaplePaths)
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
            FolderBrowserDialog mapleSelect = new FolderBrowserDialog();
            if (mapleSelect.ShowDialog() != DialogResult.OK)
                return;
            pathBox.Items.Add(mapleSelect.SelectedPath);
            pathBox.SelectedIndex = pathBox.Items.Count - 1;
        }

        private void debugButton_Click(object sender, EventArgs e)
        {
            // This function iterates over all maps in the game and verifies that we recognize all their props
            // It is meant to use by the developer(s) to speed up the process of adjusting this program for different MapleStory versions
            string wzPath = pathBox.Text;
            short version = -1;
            WzMapleVersion fileVersion = WzTool.DetectMapleVersion(Path.Combine(wzPath, "Item.wz"), out version);
            InitializeWzFiles(wzPath, fileVersion);

            MultiBoard mb = new MultiBoard();
            Board b = new Board(new Microsoft.Xna.Framework.Point(), new Microsoft.Xna.Framework.Point(), mb, null, MapleLib.WzLib.WzStructure.Data.ItemTypes.None, MapleLib.WzLib.WzStructure.Data.ItemTypes.None);

            foreach (string mapid in Program.InfoManager.Maps.Keys)
            {
                MapLoader loader = new MapLoader();
                string mapcat = "Map" + mapid.Substring(0, 1);
                WzImage mapImage = (WzImage)Program.WzManager["map"]["Map"][mapcat][mapid + ".img"];
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
                loader.VerifyMapPropsKnown(mapImage, true);
                MapInfo info = new MapInfo(mapImage, null, null, null);
                loader.LoadMisc(mapImage, b);
                if (ErrorLogger.ErrorsPresent())
                {
                    ErrorLogger.SaveToFile("debug_errors.txt");
                    ErrorLogger.ClearErrors();
                }
                mapImage.UnparseImage(); // To preserve memory, since this is a very memory intensive test
            }
            MessageBox.Show("Done");
        }

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