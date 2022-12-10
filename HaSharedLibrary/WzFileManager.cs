using HaSharedLibrary.Util;
using MapleLib.Helpers;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HaSharedLibrary.Wz;
using System.Xml.Linq;

namespace HaSharedLibrary
{
    public class WzFileManager : IDisposable
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


        private readonly string baseDir;
        /// <summary>
        /// Gets the base directory of the WZ file.
        /// Returns the "Data" folder if 64-bit client.
        /// </summary>
        /// <returns></returns>
        public string GetWzBaseDirectory()
        {
            return this._bInitAs64Bit ? (baseDir + "\\Data\\") : baseDir;
        }
        private readonly bool _bInitAs64Bit;
        public bool Is64Bit
        {
            get { return _bInitAs64Bit; }
            private set { }
        }

        public Dictionary<string, WzFile> wzFiles = new Dictionary<string, WzFile>();
        public Dictionary<WzFile, bool> wzFilesUpdated = new Dictionary<WzFile, bool>(); // flag for the list of WZ files changed to be saved later via Repack 
        public HashSet<WzImage> updatedImages = new HashSet<WzImage>();
        public Dictionary<string, WzMainDirectory> wzDirs = new Dictionary<string, WzMainDirectory>();

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
        /// <param name="_bInitAs64Bit"></param>
        public WzFileManager(string directory, bool _bInitAs64Bit)
        {
            this.baseDir = directory;
            this._bInitAs64Bit = _bInitAs64Bit;
        }

        private static readonly string[] EXCLUDED_DIRECTORY_FROM_WZ_LIST = { "bak", "backup", "hshield", "blackcipher", "harepacker", "hacreator", "xml" };
        /// <summary>
        /// Builds the list of WZ files in the MapleStory directory (for HaCreator only, not used for HaRepacker)
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public void BuildWzFileList()
        {
            bool b64BitClient = this._bInitAs64Bit;
            if (b64BitClient)
            {
                // parse through "Data" directory and iterate through every folder
                string baseDir = this.GetWzBaseDirectory();
                foreach (string dir in Directory.EnumerateDirectories(baseDir, "*", SearchOption.AllDirectories))
                {
                    string folderName = new DirectoryInfo(Path.GetDirectoryName(dir)).Name.ToLower();
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
        /// Get the list of sub wz files by its base name ("mob")
        /// i.e 'mob' expands to the list array of files "Mob001", "Mob2"
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
        /// Get the list of sub wz directories by its base name ("mob")
        /// </summary>
        /// <param name="baseName"></param>
        /// <returns></returns>
        public List<WzDirectory> GetWzDirectoriesFromBase(string baseName)
        {
            List<WzDirectory> dirs = new List<WzDirectory>();

            List<string> nameList = GetWzFileNameListFromBase(baseName);
            foreach (string name in nameList)
            {
                WzDirectory dir = this[name];
                if (dir != null)
                    dirs.Add(this[name]);
            }
            return dirs;
        }

        /// <summary>
        /// Loads the oridinary WZ file
        /// </summary>
        /// <param name="baseName"></param>
        /// <param name="encVersion"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public WzFile LoadWzFile(string baseName, WzMapleVersion encVersion)
        {
            string filePath = GetWzFilePath(baseName);
            WzFile wzf = new WzFile(filePath, encVersion);

            WzFileParseStatus parseStatus = wzf.ParseWzFile();
            if (parseStatus != WzFileParseStatus.Success)
            {
                throw new Exception("Error parsing " + baseName + ".wz (" + parseStatus.GetErrorDescription() + ")");
            }

            string fileName_ = baseName.ToLower().Replace(".wz", "");

            wzFiles[fileName_] = wzf;
            wzFilesUpdated[wzf] = false;
            wzDirs[fileName_] = new WzMainDirectory(wzf);

            return wzf;
        }

        /// <summary>
        /// Loads the Data.wz file (Legacy MapleStory WZ before version 30)
        /// </summary>
        /// <param name="baseName"></param>
        /// <returns></returns>
        public bool LoadDataWzFile(string baseName, WzMapleVersion encVersion)
        {
            string filePath = GetWzFilePath(baseName);
            try
            {
                WzFile wzf = new WzFile(filePath, encVersion);

                WzFileParseStatus parseStatus = wzf.ParseWzFile();
                if (parseStatus != WzFileParseStatus.Success)
                {
                    MessageBox.Show("Error parsing " + baseName + ".wz (" + parseStatus.GetErrorDescription() + ")");
                    return false;
                }

                baseName = baseName.ToLower();
                wzFiles[baseName] = wzf;
                wzFilesUpdated[wzf] = false;
                wzDirs[baseName] = new WzMainDirectory(wzf);
                foreach (WzDirectory mainDir in wzf.WzDirectory.WzDirectories)
                {
                    wzDirs[mainDir.Name.ToLower()] = new WzMainDirectory(wzf, mainDir);
                }
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Error initializing " + baseName + ".wz (" + e.Message + ").\r\nCheck that the directory is valid and the file is not in use.");
                return false;
            }
        }

        /// <summary>
        /// Loads the hotfix Data.wz file
        /// </summary>
        /// <param name="baseName"></param>
        /// <param name="encVersion"></param>
        /// <param name="panel"></param>
        /// <returns></returns>
        public WzImage LoadDataWzHotfixFile(string baseName, WzMapleVersion encVersion)
        {
            string filePath = GetWzFilePath(baseName);
            FileStream fs = File.Open(filePath, FileMode.Open); // dont close this file stream until it is unloaded from memory

            WzImage img = new WzImage(Path.GetFileName(filePath), fs, encVersion);
            img.ParseImage(true);

            return img;
        }

        /// <summary>
        /// Gets the wz file path by its base name, or check if it is a file path.
        /// </summary>
        /// <param name="filePathOrBaseFileName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private string GetWzFilePath(string filePathOrBaseFileName)
        {
            // find the base directory from 'wzFilesList'
            if (!_wzFilesDirectoryList.ContainsKey(filePathOrBaseFileName)) // if the key is not found, it might be a path instead
            {
                if (File.Exists(filePathOrBaseFileName))
                    return filePathOrBaseFileName;
                throw new Exception("Couldnt find the directory key for the wz file " + filePathOrBaseFileName);
            }
            
            string fileName = StringUtility.CapitalizeFirstCharacter(filePathOrBaseFileName) + ".wz";
            string filePath = Path.Combine(_wzFilesDirectoryList[filePathOrBaseFileName], fileName);
            if (!File.Exists(filePath))
                throw new Exception("wz file at the path '" + filePathOrBaseFileName + "' does not exist.");

            return filePath;
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
        /// Dispose when shutting down the application
        /// </summary>
        public void Dispose()
        {
            foreach (WzFile wzf in wzFiles.Values)
            {
                wzf.Dispose();
            }
            wzFiles.Clear();
            wzFilesUpdated.Clear();
            updatedImages.Clear();
            wzDirs.Clear();
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

        #region Find
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

        
        /// <summary>
        /// Finds the wz image within the multiple wz files (by the base wz name)
        /// </summary>
        /// <param name="baseWzName"></param>
        /// <param name="imageName"></param>
        /// <returns></returns>
        public WzObject FindWzImageByName(string baseWzName, string imageName)
        {
            List<WzDirectory> wzFiles = GetWzDirectoriesFromBase(baseWzName);
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
        #endregion
    }
}
