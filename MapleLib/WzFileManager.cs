
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
using System.Xml.Linq;
using System.Collections.ObjectModel;
using System.Threading;
using System.Text.RegularExpressions;

namespace MapleLib
{
    public class WzFileManager : IDisposable
    {
        #region Constants
        private static readonly string[] EXCLUDED_DIRECTORY_FROM_WZ_LIST = { "bak", "backup", "hshield", "blackcipher", "harepacker", "hacreator", "xml" };

        public static readonly string[] COMMON_MAPLESTORY_DIRECTORY = new string[] {
            @"C:\Nexon\MapleStory",
            @"D:\Nexon\Maple",
            @"C:\Program Files\WIZET\MapleStory",
            @"C:\MapleStory",
            @"C:\Program Files (x86)\Wizet\MapleStorySEA"
        };
        #endregion

        #region Fields
        public static WzFileManager fileManager; // static, to allow access from anywhere

        private readonly string baseDir;
        /// <summary>
        /// Gets the base directory of the WZ file.
        /// Returns the "Data" folder if 64-bit client.
        /// </summary>
        /// <returns></returns>
        public string WzBaseDirectory
        {
            get { return this._bInitAs64Bit ? (baseDir + "\\Data\\") : baseDir; }
            private set { }
        }
        private readonly bool _bInitAs64Bit;
        public bool Is64Bit
        {
            get { return _bInitAs64Bit; }
            private set { }
        }


        private readonly ReaderWriterLockSlim _readWriteLock = new ReaderWriterLockSlim(); // for '_wzFiles', '_wzFilesUpdated', '_updatedImages', & '_wzDirs'
        private readonly Dictionary<string, WzFile> _wzFiles = new Dictionary<string, WzFile>();
        private readonly Dictionary<string, bool> _wzFilesUpdated = new Dictionary<string, bool>(); // key = filepath, flag for the list of WZ files changed to be saved later via Repack 
        private readonly HashSet<WzImage> _updatedWzImages = new HashSet<WzImage>();
        private readonly Dictionary<string, WzMainDirectory> _wzDirs = new Dictionary<string, WzMainDirectory>();


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

        #endregion

        #region Constructor
        /// <summary>
        /// Constructor to init WzFileManager for HaRepacker
        /// </summary>
        public WzFileManager()
        {
            this.baseDir = string.Empty;
            this._bInitAs64Bit = false;

            fileManager = this;
        }

        /// <summary>
        /// Constructor to init WzFileManager for HaCreator
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="_bInitAs64Bit"></param>
        public WzFileManager(string directory, bool _bInitAs64Bit)
        {
            this.baseDir = directory;
            this._bInitAs64Bit = _bInitAs64Bit;

            fileManager = this;
        }
        #endregion

        #region Loader
        /// <summary>
        /// Automagically detect if the following directory where MapleStory installation is saved
        /// is a 64-bit wz directory.
        /// </summary>
        /// <returns></returns>
        public static bool Detect64BitDirectoryWzFileFormat(string baseDirectoryPath)
        {
            if (!Directory.Exists(baseDirectoryPath))
                throw new Exception("Non-existent directory provided.");

            string dataDirectoryPath = Path.Combine(baseDirectoryPath, "Data");
            bool bDirectoryContainsDataDir = Directory.Exists(dataDirectoryPath);

            if (bDirectoryContainsDataDir)
            {
                // Use a regular expression to search for .wz files in the Data directory
                string searchPattern = @"*.wz";
                int nNumWzFilesInDataDir = Directory.EnumerateFileSystemEntries(dataDirectoryPath, searchPattern, SearchOption.AllDirectories).Count();

                if (nNumWzFilesInDataDir > 40)
                    return true;
            }

            return false;
        }

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
                string baseDir = WzBaseDirectory;

                // Use Where() and Select() to filter and transform the directories
                var directories = Directory.EnumerateDirectories(baseDir, "*", SearchOption.AllDirectories)
                                           .Where(dir => !EXCLUDED_DIRECTORY_FROM_WZ_LIST.Any(x => x.ToLower() == new DirectoryInfo(Path.GetDirectoryName(dir)).Name.ToLower()));

                // Iterate over the filtered and transformed directories
                foreach (string dir in directories)
                {
                    //string folderName = new DirectoryInfo(Path.GetDirectoryName(dir)).Name.ToLower();
                    //Debug.WriteLine("----");
                    //Debug.WriteLine(dir);

                    string[] iniFiles = Directory.GetFiles(dir, "*.ini");
                    if (iniFiles.Length <= 0 || iniFiles.Length > 1)
                        throw new Exception(".ini file at the directory '" + dir + "' is missing, or unavailable.");

                    string iniFile = iniFiles[0];
                    if (!File.Exists(iniFile))
                        throw new Exception(".ini file at the directory '" + dir + "' is missing.");
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
                                _wzFilesList.Add(wzDirectoryNameOfWzFile, new List<string> { fileName2 });

                            if (!_wzFilesDirectoryList.ContainsKey(fileName2))
                                _wzFilesDirectoryList.Add(fileName2, dir);
                        }
                    }
                }
            }
            else
            {
                var wzFileNames = Directory.EnumerateFileSystemEntries(baseDir, "*.wz", SearchOption.AllDirectories)
                    .Where(f => !File.GetAttributes(f).HasFlag(FileAttributes.Directory) // exclude directories
                                && !EXCLUDED_DIRECTORY_FROM_WZ_LIST.Any(x => x.ToLower() == new DirectoryInfo(Path.GetDirectoryName(f)).Name)); // exclude folders
                foreach (string wzFileName in wzFileNames)
                {
                    //string folderName = new DirectoryInfo(Path.GetDirectoryName(wzFileName)).Name;
                    string directory = Path.GetDirectoryName(wzFileName);

                    string fileName = Path.GetFileName(wzFileName);
                    string fileName2 = fileName.Replace(".wz", "");

                    // Mob2, Mob001, Map001, Map002
                    // remove the numbers to get the base name 'map'
                    string wzBaseFileName = new string(fileName2.ToLower().Where(c => char.IsLetter(c)).ToArray());

                    if (_wzFilesList.ContainsKey(wzBaseFileName))
                        _wzFilesList[wzBaseFileName].Add(fileName2);
                    else
                        _wzFilesList.Add(wzBaseFileName, new List<string> { fileName2 });

                    if (!_wzFilesDirectoryList.ContainsKey(fileName2))
                        _wzFilesDirectoryList.Add(fileName2, directory);
                }
            }
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

            if (_wzFilesUpdated.ContainsKey(wzf.FilePath)) // some safety check
                throw new Exception(string.Format("Wz {0} at the path {1} has already been loaded, and cannot be loaded again. Remove it from memory first.", fileName_, wzf.FilePath));

            // write lock to begin adding to the dictionary
            _readWriteLock.EnterWriteLock();
            try
            {
                _wzFiles[fileName_] = wzf;
                _wzFilesUpdated[wzf.FilePath] = false;
                _wzDirs[fileName_] = new WzMainDirectory(wzf);
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
            return wzf;
        }

        /// <summary>
        /// Loads the Data.wz file (Legacy MapleStory WZ before version 30)
        /// </summary>
        /// <param name="baseName"></param>
        /// <returns></returns>
        public bool LoadLegacyDataWzFile(string baseName, WzMapleVersion encVersion)
        {
            string filePath = GetWzFilePath(baseName);
            WzFile wzf = new WzFile(filePath, encVersion);

            WzFileParseStatus parseStatus = wzf.ParseWzFile();
            if (parseStatus != WzFileParseStatus.Success)
            {
                MessageBox.Show("Error parsing " + baseName + ".wz (" + parseStatus.GetErrorDescription() + ")");
                return false;
            }

            baseName = baseName.ToLower();

            if (_wzFilesUpdated.ContainsKey(wzf.FilePath)) // some safety check
                throw new Exception(string.Format("Wz file {0} at the path {1} has already been loaded, and cannot be loaded again.", baseName, wzf.FilePath));

            // write lock to begin adding to the dictionary
            _readWriteLock.EnterWriteLock();
            try
            {
                _wzFiles[baseName] = wzf;
                _wzFilesUpdated[wzf.FilePath] = false;
                _wzDirs[baseName] = new WzMainDirectory(wzf);
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }

            foreach (WzDirectory mainDir in wzf.WzDirectory.WzDirectories)
            {
                _wzDirs[mainDir.Name.ToLower()] = new WzMainDirectory(wzf, mainDir);
            }
            return true;
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
        #endregion

        #region Loaded Items
        /// <summary>
        /// Sets WZ file as updated for saving
        /// </summary>
        /// <param name="name"></param>
        /// <param name="img"></param>
        public void SetWzFileUpdated(string name, WzImage img)
        {
            img.Changed = true;
            _updatedWzImages.Add(img);

            WzFile wzFile = GetMainDirectoryByName(name).File;
            SetWzFileUpdated(wzFile);
        }

        /// <summary>
        /// Sets WZ file as updated for saving
        /// </summary>
        /// <param name="wzFile"></param>
        /// <exception cref="Exception"></exception>
        public void SetWzFileUpdated(WzFile wzFile)
        {
            if (_wzFilesUpdated.ContainsKey(wzFile.FilePath))
            {
                // write lock to begin adding to the dictionary
                _readWriteLock.EnterWriteLock();
                try
                {
                    _wzFilesUpdated[wzFile.FilePath] = true;
                }
                finally
                {
                    _readWriteLock.ExitWriteLock();
                }
            }
            else
                throw new Exception("wz file to be flagged do not exist in memory " + wzFile.FilePath);
        }

        /// <summary>
        /// Unload the wz file from memory
        /// </summary>
        /// <param name="wzFile"></param>
        public void UnloadWzFile(WzFile wzFile, string wzFilePath)
        {
            string baseName = wzFilePath.ToLower().Replace(".wz", "");
            if (_wzFiles.ContainsKey(baseName))
            {
                // write lock to begin adding to the dictionary
                _readWriteLock.EnterWriteLock();
                try
                {
                    _wzFiles.Remove(baseName);
                    _wzFilesUpdated.Remove(wzFilePath);
                    _wzDirs.Remove(baseName);
                }
                finally
                {
                    _readWriteLock.ExitWriteLock();
                }
                wzFile.Dispose();
            }
        }
        #endregion

        #region Inherited Members
        /// <summary>
        /// Dispose when shutting down the application
        /// </summary>
        public void Dispose()
        {
            _readWriteLock.EnterWriteLock();
            try
            {
                foreach (WzFile wzf in _wzFiles.Values)
                {
                    wzf.Dispose();
                }
                _wzFiles.Clear();
                _wzFilesUpdated.Clear();
                _updatedWzImages.Clear();
                _wzDirs.Clear();
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
        }
        #endregion

        #region Custom Members
        public WzDirectory this[string name]
        {
            get
            {
                return _wzDirs.ContainsKey(name.ToLower()) ? _wzDirs[name.ToLower()].MainDir : null;
            }    //really not very useful to return null in this case
        }

        /// <summary>
        /// Gets a read-only list of loaded WZ files in the WzFileManager
        /// </summary>
        /// <returns></returns>
        public ReadOnlyCollection<WzFile> WzFileList
        {
            get { return new List<WzFile>(this._wzFiles.Values).AsReadOnly(); }
            private set { }
        }

        /// <summary>
        /// Gets a read-only list of loaded WZ files in the WzFileManager
        /// </summary>
        /// <returns></returns>
        public ReadOnlyCollection<WzImage> WzUpdatedImageList
        {
            get { return new List<WzImage>(this._updatedWzImages).AsReadOnly(); }
            private set { }
        }

        /// <summary>
        /// data.wz is wildly inconsistent between versions now, just avoid at all costs
        /// </summary>
        public bool HasDataFile
        {
            get { return false; }//return File.Exists(Path.Combine(baseDir, "Data.wz")); }
        }
        #endregion

        #region Finder
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

            return _wzDirs[name];
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
            // Use Select() and Where() to transform and filter the WzDirectory list
            return GetWzFileNameListFromBase(baseName)
                    .Select(name => this[name])
                    .Where(dir => dir != null)
                    .ToList();
        }

        /// <summary>
        /// Finds the wz image within the multiple wz files (by the base wz name)
        /// </summary>
        /// <param name="baseWzName"></param>
        /// <param name="imageName"></param>
        /// <returns></returns>
        public WzObject FindWzImageByName(string baseWzName, string imageName)
        {
            baseWzName = baseWzName.ToLower();

            // Use Where() and FirstOrDefault() to filter the WzDirectories and find the first matching WzObject
            WzObject image = GetWzDirectoriesFromBase(baseWzName)
                                .Where(wzFile => wzFile != null && wzFile[imageName] != null)
                                .Select(wzFile => wzFile[imageName])
                                .FirstOrDefault();

            return image;
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
        #endregion
    }
}
