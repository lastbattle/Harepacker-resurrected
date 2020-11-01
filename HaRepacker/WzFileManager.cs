/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using MapleLib.WzLib;
using System.IO;
using MapleLib.WzLib.Util;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using HaRepacker.Comparer;
using HaRepacker.GUI.Panels;

namespace HaRepacker
{
    public class WzFileManager
    {
        #region Constants
        public static readonly string[] MOB_WZ_FILES = { "Mob", "Mob001", "Mob2" };
        public static readonly string[] MAP_WZ_FILES = { "Map", "Map001",
            "Map002", //kms now stores main map key here
            "Map2" };
        public static readonly string[] SOUND_WZ_FILES = { "Sound", "Sound001" };

        public static readonly string[] COMMON_MAPLESTORY_DIRECTORY = new string[] {
            @"C:\Nexon\MapleStory",
            @"C:\Program Files\WIZET\MapleStory",
            @"C:\MapleStory",
            @"C:\Program Files (x86)\Wizet\MapleStorySEA"
        };
        #endregion

        private readonly List<WzFile> wzFiles = new List<WzFile>();

        public WzFileManager()
        {
        }

        public IReadOnlyCollection<WzFile> WzFileListReadOnly
        {
            get
            {
                return wzFiles.AsReadOnly();
            }
            set { }
        }

        private bool OpenWzFile(string path, WzMapleVersion encVersion, short version, out WzFile file)
        {
            try
            {
                WzFile f = new WzFile(path, version, encVersion);
                lock (wzFiles)
                {
                    wzFiles.Add(f);
                }
                WzFileParseStatus parseStatus = f.ParseWzFile();
                if (parseStatus != WzFileParseStatus.Success)
                {
                    file = null;
                    Warning.Error("Error initializing " + Path.GetFileName(path) + " (" + parseStatus.GetErrorDescription() + ").");
                    return false;
                }

                file = f;
                return true;
            }
            catch (Exception e)
            {
                Warning.Error("Error initializing " + Path.GetFileName(path) + " (" + e.Message + ").\r\nAlso, check that the directory is valid and the file is not in use.");
                file = null;
                return false;
            }
        }

        public void UnloadWzFile(WzFile file)
        {
            lock (wzFiles)
            {
                if (wzFiles.Contains(file)) // check again within scope
                {
                    ((WzNode)file.HRTag).Delete();
                    wzFiles.Remove(file);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="panel"></param>
        /// <param name="currentDispatcher"></param>
        public void ReloadWzFile(WzFile file, MainPanel panel, Dispatcher currentDispatcher = null)
        {
            WzMapleVersion encVersion = file.MapleVersion;
            string path = file.FilePath;
            short version = ((WzFile)file).Version;
            if (currentDispatcher != null)
            {
                currentDispatcher.BeginInvoke((Action)(() =>
                {
                    UnloadWzFile(file);
                }));
            }
            else
                UnloadWzFile(file);

            WzFile loadedWzFile = LoadWzFile(path, encVersion, (short)-1);
            if (loadedWzFile != null)
                Program.WzFileManager.AddLoadedWzFileToMainPanel(loadedWzFile, panel, currentDispatcher);
        }

        /// <summary>
        /// Loads the Data.wz file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encVersion"></param>
        /// <param name="panel"></param>
        /// <returns></returns>
        public WzImage LoadDataWzHotfixFile(string path, WzMapleVersion encVersion, MainPanel panel)
        {
            using (FileStream fs = File.Open(path, FileMode.Open))
            {
                WzImage img = new WzImage(Path.GetFileName(path), fs, encVersion);
                img.ParseImage(true);

                WzNode node = new WzNode(img);

                panel.DataTree.BeginUpdate();
                panel.DataTree.Nodes.Add(node);
                panel.DataTree.EndUpdate();

                if (Program.ConfigurationManager.UserSettings.Sort)
                {
                    SortNodesRecursively(node);
                }
                return img;
            }
        }

        /// <summary>
        /// Load a WZ file from path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public WzFile LoadWzFile(string path)
        {
            short fileVersion = -1;
            bool isList = WzTool.IsListFile(path);

            return LoadWzFile(path, WzTool.DetectMapleVersion(path, out fileVersion), fileVersion);
        }

        /// <summary>
        /// Load a WZ file from path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encVersion"></param>
        /// <param name="panel"></param>
        /// <param name="currentDispatcher">Dispatcher thread</param>
        /// <returns></returns>
        public WzFile LoadWzFile(string path, WzMapleVersion encVersion)
        {
            return LoadWzFile(path, encVersion, (short)-1);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="otherWzFileToLoadAt"></param>
        /// <param name="path"></param>
        /// <param name="encVersion"></param>
        /// <param name="version"></param>
        /// <param name="panel"></param>
        /// <param name="currentDispatcher">Dispatcher thread</param>
        /// <returns></returns>
        private WzFile LoadWzFile(string path, WzMapleVersion encVersion, short version)
        {
            WzFile newFile;
            if (!OpenWzFile(path, encVersion, version, out newFile))
            {
                return null;
            }
            return newFile;
        }

        /// <summary>
        /// Delayed loading of the loaded WzFile to the TreeNode panel
        /// This primarily fixes some performance issue when loading multiple WZ concurrently.
        /// </summary>
        /// <param name="newFile"></param>
        /// <param name="panel"></param>
        /// <param name="currentDispatcher"></param>
        public void AddLoadedWzFileToMainPanel(WzFile newFile, MainPanel panel, Dispatcher currentDispatcher = null)
        {
            WzNode node = new WzNode(newFile);

            // execute in main thread
            if (currentDispatcher != null)
            {
                currentDispatcher.BeginInvoke((Action)(() =>
                {
                    panel.DataTree.BeginUpdate();

                    panel.DataTree.Nodes.Add(node);
                    SortNodesRecursively(node);

                    panel.DataTree.EndUpdate();
                }));
            }
            else
            {
                panel.DataTree.BeginUpdate();

                panel.DataTree.Nodes.Add(node);
                SortNodesRecursively(node);

                panel.DataTree.EndUpdate();
            }
        }

        public void InsertWzFileUnsafe(WzFile f, MainPanel panel)
        {
            lock (wzFiles)
            {
                wzFiles.Add(f);
            }
            WzNode node = new WzNode(f);

            panel.DataTree.BeginUpdate();
            panel.DataTree.Nodes.Add(node);
            panel.DataTree.EndUpdate();

            SortNodesRecursively(node);
        }

        /// <summary>
        /// Sort all nodes that is a parent of 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="sortFromTheParentNode">Sorts only items in the parent node</param>
        public void SortNodesRecursively(WzNode parent, bool sortFromTheParentNode = false)
        {
            if (Program.ConfigurationManager.UserSettings.Sort || sortFromTheParentNode)
            {
                parent.TreeView.TreeViewNodeSorter = new TreeViewNodeSorter(sortFromTheParentNode ? parent : null);

                parent.TreeView.BeginUpdate();
                parent.TreeView.Sort();
                parent.TreeView.EndUpdate();
            }
        }

        public void ReloadAll(MainPanel panel)
        {
            Dispatcher currentThread = Dispatcher.CurrentDispatcher;
            IReadOnlyCollection<WzFile> wzFileListCopy = this.WzFileListReadOnly;

            Parallel.ForEach(wzFiles, file =>
            {
                ReloadWzFile(file, panel, currentThread);
            });
        }

        public void UnloadAll()
        {
            IReadOnlyCollection<WzFile> wzFileListCopy = new List<WzFile>(this.WzFileListReadOnly);

            foreach (WzFile file in wzFileListCopy)
            {
                if (wzFiles.Contains(file)) // check again.
                {
                    UnloadWzFile(file);
                }
            }
        }

        public void Terminate()
        {
            foreach (WzFile f in wzFiles)
            {
                f.Dispose();
            }
        }
    }
}