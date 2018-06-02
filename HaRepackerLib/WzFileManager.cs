/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Windows.Forms;
using System.IO;
using MapleLib.WzLib.Util;
using HaRepackerLib.Controls;
using HaRepackerLib.Controls.HaRepackerMainPanels;

namespace HaRepackerLib
{
    public class WzFileManager
    {
        private List<WzFile> wzFiles = new List<WzFile>();

        public WzFileManager()
        {
        }

        private bool OpenWzFile(string path, WzMapleVersion encVersion, short version, out WzFile file)
        {
            try
            {
                WzFile f = new WzFile(path, version, encVersion);
                wzFiles.Add(f);
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
            ((WzNode)file.HRTag).Delete();
            wzFiles.Remove(file);
        }

        public void ReloadWzFile(WzFile file, HaRepackerMainPanel panel)
        {
            WzMapleVersion encVersion = file.MapleVersion;
            string path = file.FilePath;
            short version = ((WzFile)file).Version;
            ((WzNode)file.HRTag).Delete();
            wzFiles.Remove(file);
            LoadWzFile(path, encVersion, (short)-1, panel);
        }

        /// <summary>
        /// Loads the Data.wz file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encVersion"></param>
        /// <param name="panel"></param>
        /// <returns></returns>
        public WzImage LoadDataWzHotfixFile(string path, WzMapleVersion encVersion, HaRepackerMainPanel panel)
        {
            FileStream fs = File.Open(path, FileMode.Open);

            WzImage img = new WzImage(Path.GetFileName(path), fs, encVersion);
            img.ParseImage(true);

            WzNode node = new WzNode(img);
            panel.DataTree.Nodes.Add(node);
            if (UserSettings.Sort)
            {
                SortNodesRecursively(node);
            }
            return img;

        }

        public WzFile LoadWzFile(string path, HaRepackerMainPanel panel)
        {
            short fileVersion = -1;
            bool isList = WzTool.IsListFile(path);
            return LoadWzFile(path, WzTool.DetectMapleVersion(path, out fileVersion), fileVersion, panel);
        }

        public WzFile LoadWzFile(string path, WzMapleVersion encVersion, HaRepackerMainPanel panel)
        {
            return LoadWzFile(path, encVersion, (short)-1, panel);
        }

        private WzFile LoadWzFile(string path, WzMapleVersion encVersion, short version, HaRepackerMainPanel panel)
        {
            WzFile newFile;
            if (!OpenWzFile(path, encVersion, version, out newFile))
                return null;
            WzNode node = new WzNode(newFile);
            panel.DataTree.Nodes.Add(node);
            if (UserSettings.Sort)
            {
                SortNodesRecursively(node);
            }
            return newFile;
        }

        public void InsertWzFileUnsafe(WzFile f, HaRepackerMainPanel panel)
        {
            wzFiles.Add(f);
            WzNode node = new WzNode(f);
            panel.DataTree.Nodes.Add(node);
            if (UserSettings.Sort)
            {
                SortNodesRecursively(node);
            }
        }

        private void SortNodesRecursively(WzNode parent)
        {
            parent.TreeView.Sort();
        }

        public void ReloadAll(HaRepackerMainPanel panel)
        {
            for (int i = 0; i < wzFiles.Count; i++)
                ReloadWzFile(wzFiles[i], panel);
        }

        public void UnloadAll()
        {
            while (wzFiles.Count > 0) UnloadWzFile(wzFiles[0]);
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