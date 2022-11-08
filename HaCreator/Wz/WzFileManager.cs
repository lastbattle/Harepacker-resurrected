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
using MapleLib.Helpers;
using HaCreator.GUI;

namespace HaCreator.Wz
{
    public class WzFileManager
    {
        #region Constants
        public static readonly string[] MOB_WZ_FILES = {
            "Mob",  "Mob001", "Mob2" };
        public static readonly string[] MAP_WZ_FILES = {
            "Map", "Map001","Map002", //kms now stores main map key here"Map2" 
        };
        public static readonly string[] SOUND_WZ_FILES = {
            "Sound",  "Sound001","Sound2","Sound002"
        };

        public static string[] MOB_WZ_FILES_64 = {
            "mob_000", "mob_001", "mob_002",
            "mob_003", "mob_004", "mob_005",
            "mob_006", "mob_007", "mob_008",
            "mob_009", "mob_010", "mob_011",
            "mob_012", "mob_013", "mob_014",
            "mob_015", "mob_016", "mob_017",
            "mob_018", "mob_019", "mob_020",
            "mob_021", "mob_022", "mob_023",
            "mob_024", "mob_025", "mob_026",
            "mob_027",
        };

        public static string[] NPC_WZ_FILES_64 = {
           "npc_000", "npc_001", "npc_002",
           "npc_003", "npc_004", "npc_005",
           "npc_006", "npc_007"
        };

        public static string[] MAP_WZ_FILES_64 = {
            "map_000", "map_001", "map_002",
            "map_003"
        };

        public static string[] MAPMAPS_WZ_FILES_64 = { // Lel
            "map0_000", "map1_000", "map2_000",
            "map3_000", "map4_000", "map5_000",
            "map6_000", "map9_000", "map9_001"
        };

        public static string[] OBJ_WZ_FILES_64 = {
            "obj_000",  "obj_001",  "obj_002",
            "obj_003",  "obj_004",  "obj_005",
            "obj_006",  "obj_007",  "obj_008",
            "obj_009",  "obj_010",  "obj_011",
            "obj_012",  "obj_013",  "obj_014",
            "obj_015",  "obj_016",  "obj_017",
        };

        public static string[] BACK_WZ_FILES_64 = {
            "back_000",  "back_001",  "back_002",
            "back_003",  "back_004",  "back_005",
            "back_006",  "back_007",  "back_008",
            "back_009",  "back_010",
        };

        public static string[] SOUND_WZ_FILES_64 = {
            "sound_000", "sound_001", "sound_002",
            "sound_003", "sound_004", "sound_005",
            "sound_006", "sound_007", "sound_008",
            "sound_009", "sound_010", "sound_011",
            "sound_012", "sound_013", "sound_014",
            "sound_015", "sound_016", "sound_017",
            "sound_018", "sound_019", "sound_020",
            "sound_021", "sound_022", "sound_023",
            "sound_024", "sound_025", "sound_026",
            "sound_027",
        };

        public static readonly string[] UI_WZ_FILES_64 = {
           "ui_000", "ui_001", "ui_002",
           "ui_003", "ui_004", "ui_005"
        };



        public static readonly string[] COMMON_MAPLESTORY_DIRECTORY = new string[] {
            @"C:\Nexon\MapleStory",
            @"D:\Nexon\Maple",
            @"C:\Program Files\WIZET\MapleStory",
            @"C:\MapleStory",
            @"C:\Program Files (x86)\Wizet\MapleStorySEA"
        };
        #endregion


        private string baseDir;
        public Dictionary<string, WzFile> wzFiles = new Dictionary<string, WzFile>();
        public Dictionary<WzFile, bool> wzFilesUpdated = new Dictionary<WzFile, bool>(); // flag for the list of WZ files changed to be saved later via Repack 
        public HashSet<WzImage> updatedImages = new HashSet<WzImage>();
        public Dictionary<string, WzMainDirectory> wzDirs = new Dictionary<string, WzMainDirectory>();
        private readonly WzMapleVersion version;

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
                return new string(new char[] { char.ToUpper(x[0]) }) + x.Substring(1);
            return x;
        }

        /// <summary>
        /// Cleanup 
        /// </summary>
        public void Clear()
        {
            wzFiles.Clear();
            wzFilesUpdated.Clear();
            updatedImages.Clear();
            wzDirs.Clear();
        }

        public bool LoadWzFile(string name)
        {


            if (Initialization.isClient64())
            {
                baseDir = Initialization.getMainWzDirectory();

                // Load NPC Data
                foreach (String npc in NPC_WZ_FILES_64)
                {
                    if (name.Contains(npc))
                    {
                        baseDir = baseDir + "Npc\\";
                    }
                }

                // Load Mob Data
                foreach (String mob in MOB_WZ_FILES_64)
                {
                    if (name.Contains(mob))
                    {
                        baseDir = baseDir + "Mob\\";
                    }
                }

                // Load Obj Data
                foreach (String obj in OBJ_WZ_FILES_64)
                {
                    if (name.Contains(obj))
                    {
                        baseDir = baseDir + "Map\\Obj\\";
                    }
                }

                // Load Back Data
                foreach (String back in BACK_WZ_FILES_64)
                {
                    if (name.Contains(back))
                    {
                        baseDir = baseDir + "Map\\Back\\";
                    }
                }

                // Load Map Data
                foreach (String map in MAP_WZ_FILES_64)
                {
                    if (name.Contains(map))
                    {
                        baseDir = baseDir + "Map\\";
                    }
                }

                // Load UI Data
                foreach (String ui in UI_WZ_FILES_64)
                {
                    if (name.Contains(ui))
                    {
                        baseDir = baseDir + "UI\\";
                    }
                }

                if (name.Contains("string_000"))
                {
                    baseDir = baseDir + "String\\";
                }
                else if (name.Contains("reactor_000"))
                {
                    baseDir = baseDir + "Reactor\\";
                }
                else if (name.Contains("tile_000"))
                {
                    baseDir = baseDir + "Map\\Tile\\";
                }

                else if (name.Contains("map0_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map0\\";
                }
                else if (name.Contains("map1_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map1\\";
                }
                else if (name.Contains("map2_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map2\\";
                }
                else if (name.Contains("map3_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map3\\";
                }
                else if (name.Contains("map4_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map4\\";
                }
                else if (name.Contains("map5_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map5\\";
                }
                else if (name.Contains("map6_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map6\\";
                }
                else if (name.Contains("map9_000") || name.Contains("map9_001"))
                {
                    baseDir = baseDir + "Map\\Map\\Map9\\";
                }
            }


            try
            {
                WzFile wzf = new WzFile(Path.Combine(baseDir, Capitalize(name) + ".wz"), version);

                WzFileParseStatus parseStatus = wzf.ParseWzFile();
                if (parseStatus != WzFileParseStatus.Success)
                {
                    MessageBox.Show("Error parsing " + name + ".wz (" + parseStatus.GetErrorDescription() + ")");
                    return false;
                }

                name = name.ToLower();
                wzFiles[name] = wzf;
                wzFilesUpdated[wzf] = false;
                wzDirs[name] = new WzMainDirectory(wzf);
                return true;
            }
            catch (Exception)
            {
                //HaRepackerLib.Warning.Error("Error initializing " + name + ".wz (" + e.Message + ").\r\nCheck that the directory is valid and the file is not in use.");
                return false;
            }
        }

        public bool LoadDataWzFile(string name)
        {


            if (Initialization.isClient64())
            {
                baseDir = Initialization.getMainWzDirectory();

                // Load NPC Data
                foreach (String npc in NPC_WZ_FILES_64)
                {
                    if (name.Contains(npc))
                    {
                        baseDir = baseDir + "Npc\\";
                    }
                }

                // Load Mob Data
                foreach (String mob in MOB_WZ_FILES_64)
                {
                    if (name.Contains(mob))
                    {
                        baseDir = baseDir + "Mob\\";
                    }
                }

                // Load Obj Data
                foreach (String obj in OBJ_WZ_FILES_64)
                {
                    if (name.Contains(obj))
                    {
                        baseDir = baseDir + "Obj\\";
                    }
                }

                // Load Back Data
                foreach (String back in BACK_WZ_FILES_64)
                {
                    if (name.Contains(back))
                    {
                        baseDir = baseDir + "Map\\Back\\";
                    }
                }

                // Load Map Data
                foreach (String map in MAP_WZ_FILES_64)
                {
                    if (name.Contains(map))
                    {
                        baseDir = baseDir + "Map\\";
                    }
                }

                // Load UI Data
                foreach (String ui in UI_WZ_FILES_64)
                {
                    if (name.Contains(ui))
                    {
                        baseDir = baseDir + "UI\\";
                    }
                }

                if (name.Contains("string_000"))
                {
                    baseDir = baseDir + "String\\";
                }
                else if (name.Contains("reactor_000"))
                {
                    baseDir = baseDir + "Reactor\\";
                }
                else if (name.Contains("tile_000"))
                {
                    baseDir = baseDir + "Map\\Tile\\";
                }

                else if (name.Contains("map0_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map0\\";
                }
                else if (name.Contains("map1_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map1\\";
                }
                else if (name.Contains("map2_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map2\\";
                }
                else if (name.Contains("map3_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map3\\";
                }
                else if (name.Contains("map4_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map4\\";
                }
                else if (name.Contains("map5_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map5\\";
                }
                else if (name.Contains("map6_000"))
                {
                    baseDir = baseDir + "Map\\Map\\Map6\\";
                }
                else if (name.Contains("map9_000") || name.Contains("map9_001"))
                {
                    baseDir = baseDir + "Map\\Map\\Map9\\";
                }

            }

            try
            {
                WzFile wzf = new WzFile(Path.Combine(baseDir, Capitalize(name) + ".wz"), version);
                
                WzFileParseStatus parseStatus = wzf.ParseWzFile();
                if (parseStatus != WzFileParseStatus.Success)
                {
                    MessageBox.Show("Error parsing " + name + ".wz (" + parseStatus.GetErrorDescription() + ")");
                    return false;
                }

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

        /// <summary>
        /// Sets WZ file as updated for saving
        /// </summary>
        /// <param name="name"></param>
        /// <param name="img"></param>
        public void SetWzFileUpdated(string name, WzImage img)
        {
            img.Changed = true;
            updatedImages.Add(img);
            wzFilesUpdated[GetMainDirectoryByName(name).File] = true;
        }

        /// <summary>
        /// Gets WZ by name from the list of loaded files
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public WzMainDirectory GetMainDirectoryByName(string name)
        {
            name = name.ToLower();

            if (name.EndsWith(".wz"))
                name = name.Replace(".wz", "");

            return wzDirs[name];
        }

        public WzDirectory this[string name]
        {
            get { return (wzDirs.ContainsKey(name.ToLower()) ? wzDirs[name.ToLower()].MainDir : null); }    //really not very useful to return null in this case
        }

        public WzDirectory String
        {
            get
            {
                if (Initialization.isClient64())
                {
                    return GetMainDirectoryByName("string_000").MainDir;
                }
                else
                {
                    return GetMainDirectoryByName("string").MainDir;
                }

            }
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

        #region Extract
        public void ExtractMobFile()
        {
            WzImage mobStringImage = (WzImage)String["mob.img"];
            if (mobStringImage == null)
                return;

            if (!mobStringImage.Parsed)
                mobStringImage.ParseImage();
            foreach (WzSubProperty mob in mobStringImage.WzProperties)
            {
                WzStringProperty nameProp = (WzStringProperty)mob["name"];
                string name = nameProp == null ? "" : nameProp.Value;
                Program.InfoManager.Mobs.Add(WzInfoTools.AddLeadingZeros(mob.Name, 7), name);
            }
        }


        public void ExtractNpcFile()
        {
            WzImage npcImage = (WzImage)String["Npc.img"];
            if (!npcImage.Parsed)
                npcImage.ParseImage();
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

        public void ExtractReactorFile64()
        {
            foreach (WzImage reactorImage in this["reactor_000"].WzImages)
            {
                ReactorInfo reactor = ReactorInfo.Load(reactorImage);
                Program.InfoManager.Reactors[reactor.ID] = reactor;
            }
        }

        public void ExtractSoundFile(string soundWzFile)
        {
            WzDirectory directory = this[soundWzFile];
            if (directory == null)
                return;

            foreach (WzImage soundImage in directory.WzImages)
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
                    string error = string.Format("[ExtractSoundFile] Error parsing {0}, {1} file.\r\nError: {2}", soundWzFile, soundImage.Name, e.ToString());
                    MapleLib.Helpers.ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                    continue; 
                }
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

        public void ExtractMapMarks64()
        {
            WzImage mapHelper = (WzImage)this["map_003"]["MapHelper.img"];
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

        public void ExtractTileSets64()
        {
            WzDirectory tileParent = (WzDirectory)this["tile_000"];
            foreach (WzImage tileset in tileParent.WzImages)
                Program.InfoManager.TileSets[WzInfoTools.RemoveExtension(tileset.Name)] = tileset;
        }

        //Handle various scenarios ie Map001.wz exists but may only contain Back or only Obj etc
        public void ExtractObjSets()
        {

            if (Initialization.isClient64())
            {
                foreach (string objWzFile in OBJ_WZ_FILES_64)
                {
                    WzDirectory objParent = (WzDirectory)this[objWzFile];
                    if (objParent != null)
                    {
                        foreach (WzImage objset in objParent.WzImages)
                            Program.InfoManager.ObjectSets[WzInfoTools.RemoveExtension(objset.Name)] = objset;
                    }
                }
            } else
            {
                foreach (string mapWzFile in MAP_WZ_FILES)
                {
                    string mapWzFile_ = mapWzFile.ToLower();

                    if (this.wzFiles.ContainsKey(mapWzFile_))
                    {
                        WzDirectory objParent = (WzDirectory)this[mapWzFile_]["Obj"];
                        if (objParent != null)
                        {
                            foreach (WzImage objset in objParent.WzImages)
                                Program.InfoManager.ObjectSets[WzInfoTools.RemoveExtension(objset.Name)] = objset;
                        }
                    }
                }
            }
        }

        //this handling sucks but nexon naming is not consistent enough to handle much better idk
        public void ExtractBackgroundSets()
        {

            if (Initialization.isClient64())
            {
                foreach (string back in BACK_WZ_FILES_64)
                {
                    string mapWzFile_ = back.ToLower();
                    WzDirectory bgParent1 = (WzDirectory)this[mapWzFile_];
                    if (bgParent1 != null)
                    {
                        foreach (WzImage bgset in bgParent1.WzImages)
                            Program.InfoManager.BackgroundSets[WzInfoTools.RemoveExtension(bgset.Name)] = bgset;
                    }

                }


            } else
            {
                foreach (string mapWzFile in MAP_WZ_FILES)
                {
                    string mapWzFile_ = mapWzFile.ToLower();

                    if (this.wzFiles.ContainsKey(mapWzFile_))
                    {
                        WzDirectory bgParent1 = (WzDirectory)this[mapWzFile_]["Back"];
                        if (bgParent1 != null)
                        {
                            foreach (WzImage bgset in bgParent1.WzImages)
                                Program.InfoManager.BackgroundSets[WzInfoTools.RemoveExtension(bgset.Name)] = bgset;
                        }
                    }
                }
            }
           
        }

        public void ExtractStringWzMaps()
        {
            WzImage mapStringsParent = (WzImage)String["Map.img"];
            if (!mapStringsParent.Parsed) mapStringsParent.ParseImage();
            foreach (WzSubProperty mapCat in mapStringsParent.WzProperties)
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

        public void ExtractPortals64()
        {
            WzSubProperty portalParent = (WzSubProperty)this["map_003"]["MapHelper.img"]["portal"];
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

        #region Find    
        /// <summary>
        /// Finds a map image from the list of Map.wzs
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="mapcat"></param>
        /// <returns></returns>
        public WzImage FindMobImage(string mobId)
        {

            if (Initialization.isClient64())
            {
                foreach (string mobWzFile in MOB_WZ_FILES_64)
                {
                    if (this.wzFiles.ContainsKey(mobWzFile))
                    {
                        WzObject mobImage = (WzImage)Program.WzManager[mobWzFile][mobId + ".img"];

                            if (mobImage != null)
                            {
                                return (WzImage)mobImage;
                            }
                    }
                }
            }
            else
            {
                foreach (string mobWzFile in MOB_WZ_FILES)
                {
                    string mobWzFile_ = mobWzFile.ToLower();

                    if (this.wzFiles.ContainsKey(mobWzFile_))
                    {
                        WzObject mobImage = (WzImage)Program.WzManager[mobWzFile_][mobId + ".img"];

                        if (mobImage != null)
                        {
                            return (WzImage)mobImage;
                        }
                    }
                }
            }
            return null;
        }


        /// <summary>
        /// Finds a map image from the list of Map.wzs
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="mapcat"></param>
        /// <returns></returns>
        public WzImage FindMapImage(string mapid, string mapcat)
        {

            if (Initialization.isClient64())
            {

                foreach (string mapWzFile in MAPMAPS_WZ_FILES_64)
                {
                    string mapWzFile_ = mapWzFile.ToLower();
                    if (this.wzFiles.ContainsKey(mapWzFile_))
                    {
                        WzObject mapImage = (WzImage)this[mapWzFile_][mapid + ".img"];
                        if (mapImage != null)
                        {
                            return (WzImage)mapImage;
                        }
                    }
                }
            }
            else
            {
                foreach (string mapWzFile in MAP_WZ_FILES)
                {
                    string mapWzFile_ = mapWzFile.ToLower();
                    if (this.wzFiles.ContainsKey(mapWzFile_))
                    {
                        WzObject mapImage = (WzImage)this[mapWzFile_]?["Map"]?[mapcat]?[mapid + ".img"];

                        if (mapImage != null)
                        {
                            return (WzImage)mapImage;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a suitable (Map.wz, Map001.wz, Map2.wz) for storing the newly created map
        /// </summary>
        /// <param name="cat">Map01, Map02, Map001.wz</param>
        /// <returns></returns>
        public WzDirectory FindMapWz(string cat)
        {
            if (Initialization.isClient64())
            {
                foreach (string mapWzFile in MAPMAPS_WZ_FILES_64)
                {
                    string mapWzFile_ = mapWzFile.ToLower();
                    WzDirectory mapDir = (WzDirectory)Program.WzManager[mapWzFile_];
                    Console.WriteLine("MapDir -> " + mapDir);
                    if (mapDir.Name.ToLower() == "map" + cat + "_000")
                    {
                        mapDir = (WzDirectory)Program.WzManager["Map" + cat + "_000"];
                    }
                    if (mapDir != null)
                    {
                        WzDirectory catDir = (WzDirectory)mapDir;
                        if (catDir != null)
                            return catDir;
                    }
                }
            } else
            {
                foreach (string mapWzFile in MAP_WZ_FILES)
                {
                    string mapWzFile_ = mapWzFile.ToLower();
                    WzDirectory mapDir = (WzDirectory)Program.WzManager[mapWzFile_]?["Map"];
                    if (mapDir != null)
                    {
                        WzDirectory catDir = (WzDirectory)mapDir[cat];
                        if (catDir != null)
                            return catDir;
                    }
                }
            }
            return null;
        }






   
        #endregion


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
