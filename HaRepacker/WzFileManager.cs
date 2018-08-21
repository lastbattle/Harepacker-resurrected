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
using HaRepacker.GUI.Panels;
using HaRepacker.Comparer;
using System.Diagnostics;

namespace HaRepacker
{
    public class WzFileManager
    {
        private static TreeViewNodeSorter SORTER = new TreeViewNodeSorter();

        private List<WzFile> wzFiles = new List<WzFile>();

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
                f.ParseWzFile();
                file = f;
                return true;
            }
            catch (Exception e)
            {
                Warning.Error("Error initializing " + Path.GetFileName(path) + " (" + e.Message + ").\r\nCheck that the directory is valid and the file is not in use.");
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

            LoadWzFile(path, encVersion, (short)-1, panel, currentDispatcher);
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
            FileStream fs = File.Open(path, FileMode.Open);

            WzImage img = new WzImage(Path.GetFileName(path), fs, encVersion);
            img.ParseImage(true);

            WzNode node = new WzNode(img);
            panel.DataTree.Nodes.Add(node);
            if (Program.ConfigurationManager.UserSettings.Sort)
            {
                SortNodesRecursively(node);
            }
            return img;

        }

        /// <summary>
        /// Load a WZ file from path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="panel"></param>
        /// <returns></returns>
        public WzFile LoadWzFile(string path, MainPanel panel)
        {
            short fileVersion = -1;
            bool isList = WzTool.IsListFile(path);
            return LoadWzFile(path, WzTool.DetectMapleVersion(path, out fileVersion), fileVersion, panel);
        }

        /// <summary>
        /// Load a WZ file from path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encVersion"></param>
        /// <param name="panel"></param>
        /// <param name="currentDispatcher">Dispatcher thread</param>
        /// <returns></returns>
        public WzFile LoadWzFile(string path, WzMapleVersion encVersion, MainPanel panel, Dispatcher currentDispatcher = null)
        {
            return LoadWzFile(path, encVersion, (short)-1, panel, currentDispatcher);
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
        private WzFile LoadWzFile(string path, WzMapleVersion encVersion, short version, MainPanel panel, Dispatcher currentDispatcher = null)
        {
            WzFile newFile;
            if (!OpenWzFile(path, encVersion, version, out newFile))
                return null;
            WzNode node = new WzNode(newFile);

            // execute in main thread
            if (currentDispatcher != null)
            {
                currentDispatcher.BeginInvoke((Action)(() =>
                {
                    panel.DataTree.Nodes.Add(node);
                    SortNodesRecursively(node);
                }));
            }
            else
            {
                panel.DataTree.Nodes.Add(node);
                SortNodesRecursively(node);
            }
            return newFile;
        }

        public void InsertWzFileUnsafe(WzFile f, MainPanel panel)
        {
            lock (wzFiles)
            {
                wzFiles.Add(f);
            }
            WzNode node = new WzNode(f);
            panel.DataTree.Nodes.Add(node);

            SortNodesRecursively(node);
        }

        private void SortNodesRecursively(WzNode parent)
        {
            if (Program.ConfigurationManager.UserSettings.Sort)
            {
                parent.TreeView.TreeViewNodeSorter = SORTER;

                parent.TreeView.Sort();
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