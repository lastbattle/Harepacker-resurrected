/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.GUI;
using HaCreator.MapEditor.Info;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HaCreator.Wz
{
    public class WzFileManager
    {
        #region Constants
        public static readonly string[] COMMON_MAPLESTORY_DIRECTORY = new string[] {
            @"C:\Nexon\MapleStory",
            @"D:\Nexon\Maple",
            @"C:\Program Files\WIZET\MapleStory",
            @"C:\MapleStory",
            @"C:\Program Files (x86)\Wizet\MapleStorySEA"
        };
        #endregion


        private string baseDir;
        /// <summary>
        /// Gets the base directory of the WZ file.
        /// Returns the "Data" folder if 64-bit client.
        /// </summary>
        /// <returns></returns>
        public string GetWzBaseDirectory()
        {
            bool b64BitClient = Initialization.IsClient64();
            return b64BitClient ? (baseDir + "\\Data\\") : baseDir;
        }

        public Dictionary<string, WzFile> wzFiles = new Dictionary<string, WzFile>();
        public Dictionary<WzFile, bool> wzFilesUpdated = new Dictionary<WzFile, bool>(); // flag for the list of WZ files changed to be saved later via Repack 
        public HashSet<WzImage> updatedImages = new HashSet<WzImage>();
        public Dictionary<string, WzMainDirectory> wzDirs = new Dictionary<string, WzMainDirectory>();
        private readonly WzMapleVersion version;

        /// <summary>
        /// The list of sub wz files.
        /// Key, <List of files, directory path>
        /// i.e sound.wz expands to the list array of "Mob001", "Mob2"
        /// 
        /// {[Map\Map\Map4, Count = 1]}
        /// </summary>
        private readonly Dictionary<string, List<string>> _wzFilesList = new Dictionary<string, List<string>>();
        /// <summary>
        /// The list of directory where the wz file residues
        /// </summary>
        private readonly Dictionary<string, string> _wzFilesDirectoryList = new Dictionary<string, string>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="version"></param>
        public WzFileManager(string directory, WzMapleVersion version)
        {
            this.baseDir = directory;
            this.version = version;

            BuildWzFileList();
        }

        private string CapitalizeFirstCharacter(string x)
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

        private static string[] EXCLUDED_DIRECTORY_FROM_WZ_LIST = { "bak", "backup", "hshield", "blackcipher", "harepacker", "hacreator", "xml" };
        /// <summary>
        /// Builds the list of WZ files in the MapleStory directory
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public void BuildWzFileList()
        {
            bool b64BitClient = Initialization.IsClient64();
            if (b64BitClient)
            {
                // parse through "Data" directory and iterate through every folder
                string baseDir = this.GetWzBaseDirectory();
                foreach (string dir in Directory.EnumerateDirectories(baseDir, "*", SearchOption.AllDirectories))
                {
                    string folderName = new DirectoryInfo(System.IO.Path.GetDirectoryName(dir)).Name.ToLower();
                    if (EXCLUDED_DIRECTORY_FROM_WZ_LIST.Any(x => x.ToLower() == folderName))
                        continue; // exclude folders

                    //Debug.WriteLine("----");
                    //Debug.WriteLine(dir);

                    string[] iniFiles = Directory.GetFiles(dir, "*.ini");
                    if (iniFiles.Length <= 0 || iniFiles.Length > 1)
                    {
                        throw new Exception(".ini file at the directory '" + dir + "' is missing, or unavailable.");
                    }
                    string iniFile = iniFiles[0];
                    if (!File.Exists(iniFile))
                    {
                        throw new Exception(".ini file at the directory '" + dir + "' is missing.");
                    }
                    else
                    {
                        string[] iniFileLines = File.ReadAllLines(iniFile);
                        if (iniFileLines.Length <= 0)
                            throw new Exception(".ini file does not contain LastWzIndex information.");

                        string[] iniFileSplit = iniFileLines[0].Split('|');
                        if (iniFileSplit.Length <= 1)
                            throw new Exception(".ini file does not contain LastWzIndex information.");

                        int index = int.Parse(iniFileSplit[1]);

                        for (int i = 0; i <= index; i++)
                        {
                            string partialWzFilePath = string.Format(iniFile.Replace(".ini", "_{0}.wz"), i.ToString("D3")); // 3 padding '0's
                            string fileName = Path.GetFileName(partialWzFilePath);
                            string fileName2 = fileName.Replace(".wz", "");

                            string wzDirectoryNameOfWzFile = dir.Replace(baseDir, "").ToLower();

                            //Debug.WriteLine(partialWzFileName);
                            //Debug.WriteLine(wzDirectoryOfWzFile);

                            if (_wzFilesList.ContainsKey(wzDirectoryNameOfWzFile))
                                _wzFilesList[wzDirectoryNameOfWzFile].Add(fileName2);
                            else
                            {
                                _wzFilesList.Add(wzDirectoryNameOfWzFile,
                                        new List<string>
                                        {
                                            fileName2
                                        });
                            }
                            if (!_wzFilesDirectoryList.ContainsKey(fileName2))
                                _wzFilesDirectoryList.Add(fileName2, dir);
                        }
                    }
                }
            }
            else
            {
                foreach (string wzFileName in Directory.EnumerateFileSystemEntries(baseDir, "*.wz", SearchOption.AllDirectories))
                {
                    FileAttributes attr = File.GetAttributes(wzFileName);
                    if (attr.HasFlag(FileAttributes.Directory)) // exclude directories, only want the files.wz
                        continue;

                    string folderName = new DirectoryInfo(System.IO.Path.GetDirectoryName(wzFileName)).Name;
                    string directory = Path.GetDirectoryName(wzFileName);

                    if (EXCLUDED_DIRECTORY_FROM_WZ_LIST.Any(x => x.ToLower() == folderName))
                        continue; // exclude folders

                    string fileName = Path.GetFileName(wzFileName);
                    string fileName2 = fileName.Replace(".wz", "");

                    // Mob2, Mob001, Map001, Map002
                    // remove the numbers to get the base name 'map'
                    string wzBaseFileName = fileName.Replace(".wz", "");
                    wzBaseFileName = string.Join("", wzBaseFileName.ToLower().Where(c => char.IsLetter(c)));

                    //Debug.WriteLine(wzFileName);

                    if (_wzFilesList.ContainsKey(wzBaseFileName))
                        _wzFilesList[wzBaseFileName].Add(fileName2);
                    else
                    {
                        _wzFilesList.Add(wzBaseFileName,
                                        new List<string>
                                        {
                                            fileName2
                                        });
                    }
                    if (!_wzFilesDirectoryList.ContainsKey(fileName2))
                        _wzFilesDirectoryList.Add(fileName2, directory);
                }
            }
        }

        /// <summary>
        /// Get the list of sub wz files.
        /// Key, <List of files, directory path>
        /// i.e 'mob' expands to the list array of "Mob001", "Mob2"
        /// </summary>
        /// <param name="baseName"></param>
        /// <returns></returns>
        public List<string> GetWzFileNameListFromBase(string baseName)
        {
            if (!_wzFilesList.ContainsKey(baseName))
                return new List<string>(); // return as an empty list if none
            return _wzFilesList[baseName];
        }

        /// <summary>
        /// Get the list of sub wz directories by its base name
        /// </summary>
        /// <param name="baseName"></param>
        /// <returns></returns>
        public List<WzDirectory> GetWzDirectoriesFromBase(string baseName)
        {
            List<WzDirectory> dir = new List<WzDirectory>();

            List<string> nameList = GetWzFileNameListFromBase(baseName);
            foreach (string name in nameList)
            {
                dir.Add(this[name]);
            }
            return dir;
        }

        public void LoadWzFile(string baseName)
        {
            // find the base directory from 'wzFilesList'
            if (!_wzFilesDirectoryList.ContainsKey(baseName))
                throw new Exception("Couldnt find the directory key for the wz file " + baseName);

            string fileName = CapitalizeFirstCharacter(baseName) + ".wz";
            string filePath = Path.Combine(_wzFilesDirectoryList[baseName], fileName);
            if (!File.Exists(filePath))
                throw new Exception("wz file at the path '" + baseName + "' does not exist.");

            WzFile wzf = new WzFile(filePath, version);

            WzFileParseStatus parseStatus = wzf.ParseWzFile();
            if (parseStatus != WzFileParseStatus.Success)
            {
                throw new Exception("Error parsing " + baseName + ".wz (" + parseStatus.GetErrorDescription() + ")");
            }

            string fileName_ = baseName.ToLower().Replace(".wz", "");

            wzFiles[fileName_] = wzf;
            wzFilesUpdated[wzf] = false;
            wzDirs[fileName_] = new WzMainDirectory(wzf);
        }

        public bool LoadDataWzFile(string name)
        {
            try
            {
                WzFile wzf = new WzFile(Path.Combine(baseDir, CapitalizeFirstCharacter(name) + ".wz"), version);

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
            get 
            { 
                return wzDirs.ContainsKey(name.ToLower()) ? wzDirs[name.ToLower()].MainDir : null; 
            }    //really not very useful to return null in this case
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

        #region Extractor
        /// <summary>
        /// 
        /// </summary>
        public void ExtractMobFile()
        {
            // Mob.wz
            List<string> mobWzFiles = Program.WzManager.GetWzFileNameListFromBase("mob");

            foreach (string mobWzFileName in mobWzFiles)
            {
            }

            // String.wz
            List<string> stringWzFiles = Program.WzManager.GetWzFileNameListFromBase("string");
            foreach (string stringWzFileName in stringWzFiles)
            {
                WzImage mobStringImage = (WzImage)this[stringWzFileName]?["mob.img"];
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
            List<string> npcWzFiles = Program.WzManager.GetWzFileNameListFromBase("npc");

            foreach (string npcWzFileName in npcWzFiles)
            {
            }

            // String.wz
            List<string> stringWzFiles = Program.WzManager.GetWzFileNameListFromBase("string");
            foreach (string stringWzFileName in stringWzFiles)
            {
                WzImage npcImage = (WzImage)this[stringWzFileName]?["Npc.img"];
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
            List<string> reactorWzFiles = Program.WzManager.GetWzFileNameListFromBase("reactor");
            foreach (string reactorWzFileName in reactorWzFiles)
            {
                foreach (WzImage reactorImage in this[reactorWzFileName].WzImages)
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
            List<string> soundWzFiles = Program.WzManager.GetWzFileNameListFromBase("sound");
            foreach (string soundWzFileName in soundWzFiles)
            {
                WzDirectory directory = this[soundWzFileName];
                if (directory == null)
                    continue;

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
                        string error = string.Format("[ExtractSoundFile] Error parsing {0}, {1} file.\r\nError: {2}", soundWzFileName, soundImage.Name, e.ToString());
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
            List<string> mapWzFiles = Program.WzManager.GetWzFileNameListFromBase("map");
            foreach (string mapWzFileName in mapWzFiles)
            {
                WzImage mapHelper = (WzImage)this[mapWzFileName]?["MapHelper.img"];
                if (mapHelper == null)
                    continue;

                foreach (WzCanvasProperty mark in mapHelper["mark"].WzProperties)
                {
                    Program.InfoManager.MapMarks[mark.Name] = mark.GetLinkedWzCanvasBitmap();
                }
                break; // only needs to be loaded once
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ExtractTileSets()
        {
            bool bLoadedInMap = false;

            List<string> mapWzFiles = Program.WzManager.GetWzFileNameListFromBase("map");
            foreach (string mapWzFileName in mapWzFiles)
            {
                WzDirectory tileParent = (WzDirectory)this[mapWzFileName]?["Tile"];
                if (tileParent == null)
                    continue; // not in this wz

                foreach (WzImage tileset in tileParent.WzImages)
                    Program.InfoManager.TileSets[WzInfoTools.RemoveExtension(tileset.Name)] = tileset;

                bLoadedInMap = true;
                break; // only needs to be loaded once
            }

            // Not loaded, try to find it in "tile.wz"
            // on 64-bit client it is stored in a different file apart from map
            if (!bLoadedInMap)
            {
                List<string> tileWzFiles = Program.WzManager.GetWzFileNameListFromBase("map\\tile");
                foreach (string tileWzFileNames in tileWzFiles)
                {
                    WzDirectory tileParent = (WzDirectory)this[tileWzFileNames];
                    if (tileParent == null)
                        continue;

                    foreach (WzImage tileset in tileParent.WzImages)
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

            List<string> mapWzFiles = Program.WzManager.GetWzFileNameListFromBase("map");
            foreach (string mapWzFileName in mapWzFiles)
            {
                WzDirectory objParent = (WzDirectory)this[mapWzFileName]?["Obj"];
                if (objParent == null)
                    continue; // not in this wz

                foreach (WzImage objset in objParent.WzImages)
                    Program.InfoManager.ObjectSets[WzInfoTools.RemoveExtension(objset.Name)] = objset;

                bLoadedInMap = true;
                break; // only needs to be loaded once
            }

            // Not loaded, try to find it in "tile.wz"
            // on 64-bit client it is stored in a different file apart from map
            if (!bLoadedInMap)
            {
                List<string> objWzFiles = Program.WzManager.GetWzFileNameListFromBase("map\\obj");
                foreach (string objWzFileName in objWzFiles)
                {
                    WzDirectory objParent = (WzDirectory)this[objWzFileName];
                    if (objParent == null)
                        continue; // not in this wz

                    foreach (WzImage objset in objParent.WzImages)
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

            List<string> mapWzFiles = Program.WzManager.GetWzFileNameListFromBase("map");
            foreach (string mapWzFileName in mapWzFiles)
            {
                WzDirectory bgParent1 = (WzDirectory)this[mapWzFileName]?["Back"];
                if (bgParent1 == null)
                    continue; // not in this wz

                foreach (WzImage bgset in bgParent1.WzImages)
                    Program.InfoManager.BackgroundSets[WzInfoTools.RemoveExtension(bgset.Name)] = bgset;

                bLoadedInMap = true;
            }

            // Not loaded, try to find it in "tile.wz"
            // on 64-bit client it is stored in a different file apart from map
            if (!bLoadedInMap)
            {
                List<string> backWzFiles = Program.WzManager.GetWzFileNameListFromBase("map\\back");
                foreach (string backWzFileName in backWzFiles)
                {
                    WzDirectory bgParent1 = (WzDirectory)this[backWzFileName];
                    if (bgParent1 == null)
                        continue; // not in this wz

                    foreach (WzImage bgset in bgParent1.WzImages)
                        Program.InfoManager.BackgroundSets[WzInfoTools.RemoveExtension(bgset.Name)] = bgset;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ExtractStringWzMaps()
        {
            List<string> stringWzFiles = Program.WzManager.GetWzFileNameListFromBase("string");
            foreach (string stringWzFileName in stringWzFiles)
            {
                WzImage mapStringsParent = (WzImage)this[stringWzFileName]?["Map.img"];
                if (mapStringsParent == null)
                    continue; // not in this wz

                if (!mapStringsParent.Parsed)
                    mapStringsParent.ParseImage();
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
        }

        public void ExtractPortals()
        {
            List<string> mapWzFiles = Program.WzManager.GetWzFileNameListFromBase("map");
            foreach (string mapWzFileName in mapWzFiles)
            {
                WzObject mapHelperParent = this[mapWzFileName]?["MapHelper.img"];
                if (mapHelperParent == null)
                    continue; // not in this wz

                WzSubProperty portalParent = (WzSubProperty)mapHelperParent["portal"];
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
        }
        #endregion

        #region Find
        /// <summary>
        /// Finds the wz image within the multiple wz files (by the base wz name)
        /// </summary>
        /// <param name="baseWzName"></param>
        /// <param name="imageName"></param>
        /// <returns></returns>
        public WzObject FindWzImageByName(string baseWzName, string imageName)
        {
            List<WzDirectory> wzFiles = Program.WzManager.GetWzDirectoriesFromBase(baseWzName);
            foreach (WzDirectory wzFile in wzFiles)
            {
                if (wzFile == null)
                    continue; // hmm?

                //foreach (WzObject obj in wzFile.WzImages)
                //    Debug.WriteLine(obj.Name);

                WzObject image = wzFile[imageName];
                if (image == null)
                    continue; // not in this wz

                return image;
            }
            return null;
        }

        /// <summary>
        /// Finds a map image from the list of Map.wzs
        /// On pre 64-bit client:
        /// Map.wz/Map/Map1/10000000.img
        /// 
        /// On post 64-bit client:
        /// Map/Map/Map1/Map1_000.wz/10000000.img
        /// </summary>
        /// <param name="mapid"></param>
        /// <returns></returns>
        public WzImage FindMapImage(string mapid)
        {
            string mapIdNamePadded = mapid.PadLeft(9, '0') + ".img";

            string mapcat;
            if (Initialization.IsClient64())
                mapcat = mapIdNamePadded.Substring(0, 1);
            else
                mapcat = "Map" + mapIdNamePadded.Substring(0, 1);

            if (!Initialization.IsClient64())
            {
                List<string> mapWzFiles = this.GetWzFileNameListFromBase("map");
                foreach (string mapWzFileName in mapWzFiles)
                {
                    WzObject mapImage = (WzImage)this[mapWzFileName]?["Map"]?[mapcat]?[mapIdNamePadded];
                    if (mapImage != null)
                        return (WzImage)mapImage;
                }
            }
            else
            {
                List<string> mapWzFiles = this.GetWzFileNameListFromBase("map\\map\\map" + mapcat);
                foreach (string mapWzFileName in mapWzFiles)
                {
                    WzObject mapImage = (WzImage)this[mapWzFileName]?[mapIdNamePadded];
                    if (mapImage != null)
                        return (WzImage)mapImage;
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
