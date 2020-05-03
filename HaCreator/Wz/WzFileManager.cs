/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using MapleLib.WzLib.WzStructure.Data;
using HaCreator.MapEditor.Info;

namespace HaCreator.Wz
{
    public class WzFileManager
    {
        private string baseDir;
        public Dictionary<string, WzFile> wzFiles = new Dictionary<string, WzFile>();
        public Dictionary<WzFile, bool> wzFilesUpdated = new Dictionary<WzFile, bool>();
        public HashSet<WzImage> updatedImages = new HashSet<WzImage>();
        public Dictionary<string, WzMainDirectory> wzDirs = new Dictionary<string, WzMainDirectory>();
        private WzMapleVersion version;

        public WzFileManager(string directory, WzMapleVersion version)
        {
            baseDir = directory;
            this.version = version;
        }

        public WzFileManager(string directory)
        {
            baseDir = directory;
            this.version = WzMapleVersion.GENERATE;
        }

        private string Capitalize(string x)
        {
            if (x.Length > 0 && char.IsLower(x[0]))
            {
                return new string(new char[] { char.ToUpper(x[0]) }) + x.Substring(1);
            }
            else
            {
                return x;
            }
        }

        public bool LoadWzFile(string name)
        {
            try
            {
                WzFile wzf = new WzFile(Path.Combine(baseDir, Capitalize(name) + ".wz"), version);

                string parseErrorMessage = string.Empty;
                bool parseSuccess = wzf.ParseWzFile(out parseErrorMessage);

                name = name.ToLower();
                wzFiles[name] = wzf;
                wzFilesUpdated[wzf] = false;
                wzDirs[name] = new WzMainDirectory(wzf);
                return true;
            }
            catch (Exception e)
            {
                //HaRepackerLib.Warning.Error("Error initializing " + name + ".wz (" + e.Message + ").\r\nCheck that the directory is valid and the file is not in use.");
                return false;
            }
        }

        public bool LoadDataWzFile(string name)
        {
            try
            {
                WzFile wzf = new WzFile(Path.Combine(baseDir, Capitalize(name) + ".wz"), version);

                string parseErrorMessage = string.Empty;
                bool parseSuccess = wzf.ParseWzFile(out parseErrorMessage);

                name = name.ToLower();
                wzFiles[name] = wzf;
                wzFilesUpdated[wzf] = false;
                wzDirs[name] = new WzMainDirectory(wzf);
                foreach (WzDirectory mainDir in wzf.WzDirectory.WzDirectories)
                {
                    wzDirs[mainDir.Name.ToLower()] = new WzMainDirectory(wzf, mainDir);
                }
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Error initializing " + name + ".wz (" + e.Message + ").\r\nCheck that the directory is valid and the file is not in use.");
                return false;
            }
        }

        public void SetUpdated(string name, WzImage img)
        {
            img.Changed = true;
            updatedImages.Add(img);
            wzFilesUpdated[GetMainDirectoryByName(name).File] = true;
        }

        public WzMainDirectory GetMainDirectoryByName(string name)
        {
            return wzDirs[name.ToLower()];
        }

        public WzDirectory this[string name]
        {
            get { return (wzDirs.ContainsKey(name.ToLower()) ? wzDirs[name.ToLower()].MainDir : null); }    //really not very useful to return null in this case
        }

        public WzDirectory String
        {
            get { return GetMainDirectoryByName("string").MainDir; }
        }

        //data.wz is wildly inconsistent between versions now, just avoid at all costs
        public bool HasDataFile
        {
            get { return false; }//return File.Exists(Path.Combine(baseDir, "Data.wz")); }
        }

        public string BaseDir
        {
            get { return baseDir; }
        }

        //tbd load mob2.wz etc
        public void ExtractMobFile()
        {
            WzImage mobImage = (WzImage)String["Mob.img"];
            if (!mobImage.Parsed) mobImage.ParseImage();
            foreach (WzSubProperty mob in mobImage.WzProperties)
            {
                WzStringProperty nameProp = (WzStringProperty)mob["name"];
                string name = nameProp == null ? "" : nameProp.Value;
                Program.InfoManager.Mobs.Add(WzInfoTools.AddLeadingZeros(mob.Name, 7), name);
            }
            /*   WzImage mobImage2 = (WzImage)String["Mob001.img"];
               if (mobImage2 != null)
               {
                   if (!mobImage2.Parsed) mobImage2.ParseImage();
                   foreach (WzSubProperty mob in mobImage2.WzProperties)
                   {
                       WzStringProperty nameProp = (WzStringProperty)mob["name"];
                       string name = nameProp == null ? "" : nameProp.Value;
                       Program.InfoManager.Mobs.Add(WzInfoTools.AddLeadingZeros(mob.Name, 7), name);
                   }
               }
               WzImage mobImage3 = (WzImage)String["Mob2.img"];
               if (mobImage3 != null)
               {
                   if (!mobImage3.Parsed) mobImage3.ParseImage();
                   foreach (WzSubProperty mob in mobImage3.WzProperties)
                   {
                       WzStringProperty nameProp = (WzStringProperty)mob["name"];
                       string name = nameProp == null ? "" : nameProp.Value;
                       Program.InfoManager.Mobs.Add(WzInfoTools.AddLeadingZeros(mob.Name, 7), name);
                   }
               }*/
        }

        public void ExtractNpcFile()
        {
            WzImage npcImage = (WzImage)String["Npc.img"];
            if (!npcImage.Parsed) npcImage.ParseImage();
            foreach (WzSubProperty npc in npcImage.WzProperties)
            {
                WzStringProperty nameProp = (WzStringProperty)npc["name"];
                string name = nameProp == null ? "" : nameProp.Value;
                Program.InfoManager.NPCs.Add(WzInfoTools.AddLeadingZeros(npc.Name, 7), name);
            }
        }

        public void ExtractReactorFile()
        {
            foreach (WzImage reactorImage in this["reactor"].WzImages)
            {
                ReactorInfo reactor = ReactorInfo.Load(reactorImage);
                Program.InfoManager.Reactors[reactor.ID] = reactor;
            }
        }

        public void ExtractSoundFile()
        {
            foreach (WzImage soundImage in this["sound"].WzImages)
            {
                if (!soundImage.Name.ToLower().Contains("bgm")) continue;
                if (!soundImage.Parsed) soundImage.ParseImage();
                try
                {
                    foreach (WzSoundProperty bgm in soundImage.WzProperties)
                    {
                        Program.InfoManager.BGMs[WzInfoTools.RemoveExtension(soundImage.Name) + @"/" + bgm.Name] = bgm;
                    }
                }
                catch (Exception e) { continue; }
            }
        }

        public void ExtractMapMarks()
        {
            WzImage mapHelper = (WzImage)this["map"]["MapHelper.img"];
            foreach (WzCanvasProperty mark in mapHelper["mark"].WzProperties)
            {
                Program.InfoManager.MapMarks[mark.Name] = mark.GetLinkedWzCanvasBitmap();
            }
        }

        public void ExtractTileSets()
        {
            WzDirectory tileParent = (WzDirectory)this["map"]["Tile"];
            foreach (WzImage tileset in tileParent.WzImages)
                Program.InfoManager.TileSets[WzInfoTools.RemoveExtension(tileset.Name)] = tileset;
        }

        //Handle various scenarios ie Map001.wz exists but may only contain Back or only Obj etc
        public void ExtractObjSets()
        {
            WzDirectory objParent1 = (WzDirectory)this["map"]["Obj"];
            if (objParent1 != null)
            {
                foreach (WzImage objset in objParent1.WzImages)
                    Program.InfoManager.ObjectSets[WzInfoTools.RemoveExtension(objset.Name)] = objset;
            }

            if (this.wzFiles.ContainsKey("map001"))
            {
                WzDirectory objParent2 = (WzDirectory)this["map001"]["Obj"];
                if (objParent2 != null)
                {
                    foreach (WzImage objset in objParent2.WzImages)
                        Program.InfoManager.ObjectSets[WzInfoTools.RemoveExtension(objset.Name)] = objset;
                }
            }
            if (this.wzFiles.ContainsKey("map2"))
            {
                WzDirectory objParent3 = (WzDirectory)this["map2"]["Obj"];
                if (objParent3 != null)
                {
                    foreach (WzImage objset in objParent3.WzImages)
                        Program.InfoManager.ObjectSets[WzInfoTools.RemoveExtension(objset.Name)] = objset;
                }
            }
        }

        //this handling sucks but nexon naming is not consistent enough to handle much better idk
        public void ExtractBackgroundSets()
        {
            WzDirectory bgParent1 = (WzDirectory)this["map"]["Back"];
            if (bgParent1 != null)
            {
                foreach (WzImage bgset in bgParent1.WzImages)
                    Program.InfoManager.BackgroundSets[WzInfoTools.RemoveExtension(bgset.Name)] = bgset;
            }

            if (this.wzFiles.ContainsKey("map001"))
            {
                WzDirectory bgParent2 = (WzDirectory)this["map001"]["Back"];
                if (bgParent2 != null)
                {
                    foreach (WzImage bgset in bgParent2.WzImages)
                        Program.InfoManager.BackgroundSets[WzInfoTools.RemoveExtension(bgset.Name)] = bgset;
                }
            }
            if (this.wzFiles.ContainsKey("map2"))
            {
                WzDirectory bgParent3 = (WzDirectory)this["map2"]["Back"];
                if (bgParent3 != null)
                {
                    foreach (WzImage bgset in bgParent3.WzImages)
                        Program.InfoManager.BackgroundSets[WzInfoTools.RemoveExtension(bgset.Name)] = bgset;
                }
            }
        }

        public void ExtractMaps()
        {
            WzImage mapStringsParent = (WzImage)String["Map.img"];
            if (!mapStringsParent.Parsed) mapStringsParent.ParseImage();
            foreach (WzSubProperty mapCat in mapStringsParent.WzProperties)
            {
                foreach (WzSubProperty map in mapCat.WzProperties)
                {
                    WzStringProperty mapName = (WzStringProperty)map["mapName"];
                    string id;
                    if (map.Name.Length == 9)
                        id = map.Name;
                    else
                        id = WzInfoTools.AddLeadingZeros(map.Name, 9);

                    if (mapName == null)
                        Program.InfoManager.Maps[id] = "";
                    else
                        Program.InfoManager.Maps[id] = mapName.Value;
                }
            }
        }

        public void ExtractPortals()
        {
            WzSubProperty portalParent = (WzSubProperty)this["map"]["MapHelper.img"]["portal"];
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
                else if(portal.WzProperties[0] is WzCanvasProperty)
                {
                    Dictionary<string, Bitmap> images = new Dictionary<string, Bitmap>();
                    Bitmap defaultImage = null;
                    try
                    {
                        foreach (WzCanvasProperty image in portal.WzProperties)
                        {
                            //WzSubProperty portalContinue = (WzSubProperty)image["portalContinue"];
                            //if (portalContinue == null) continue;
                            Bitmap portalImage = image.GetBitmap();
                            defaultImage = portalImage;
                            images.Add(image.Name, portalImage);
                        }
                        Program.InfoManager.GamePortals.Add(portal.Name, new PortalGameImageInfo(defaultImage, images));
                    }
                    catch (InvalidCastException) { continue; } //nexon likes to toss ints in here zType etc
                }
            }

            for (int i = 0; i < Program.InfoManager.PortalTypeById.Count; i++)
            {
                Program.InfoManager.PortalIdByType[Program.InfoManager.PortalTypeById[i]] = i;
            }
        }

        /*        public void ExtractItems()
                {
                    WzImage consImage = (WzImage)String["Consume.img"];
                    if (!consImage.Parsed) consImage.ParseImage();
                    foreach (WzSubProperty item in consImage.WzProperties)
                    {
                        WzStringProperty nameProp = (WzStringProperty)item["name"];
                        string name = nameProp == null ? "" : nameProp.Value;
                        Program.InfoManager.Items.Add(WzInfoTools.AddLeadingZeros(item.Name, 7), name);
                    }
                }*/
    }
}
