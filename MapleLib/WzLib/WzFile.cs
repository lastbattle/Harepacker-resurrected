/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System.Threading.Tasks;
using MapleLib.PacketLib;
using MapleLib.MapleCryptoLib;
using System.Linq;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MapleLib.ClientLib;

namespace MapleLib.WzLib
{
    /// <summary>
    /// A class that contains all the information of a wz file
    /// </summary>
    public class WzFile : WzObject
    {
        #region Fields
        internal string path;
        internal WzDirectory wzDir;
        internal WzHeader header;
        internal string name = "";

        internal ushort wzVersionHeader = 0;
        internal const ushort wzVersionHeader64bit_start = 770; // 777 for KMS, GMS v230 uses 778.. wut

        internal uint versionHash = 0;
        internal short mapleStoryPatchVersion = 0;
        internal WzMapleVersion maplepLocalVersion;
        internal MapleStoryLocalisation mapleLocaleVersion = MapleStoryLocalisation.Not_Known;

        internal bool wz_withEncryptVersionHeader = true;  // KMS update after Q4 2021, ver 1.2.357 does not contain any wz enc header information

        internal byte[] WzIv;
        #endregion

        /// <summary>
        /// The parsed IWzDir after having called ParseWzDirectory(), this can either be a WzDirectory or a WzListDirectory
        /// </summary>
        public WzDirectory WzDirectory
        {
            get { return wzDir; }
        }

        /// <summary>
        /// Name of the WzFile
        /// </summary>
        public override string Name
        {
            get { return name; }
            set { name = value; }
        }

        /// <summary>
        /// The WzObjectType of the file
        /// </summary>
        public override WzObjectType ObjectType
        {
            get { return WzObjectType.File; }
        }

        /// <summary>
        /// Returns WzDirectory[name]
        /// </summary>
        /// <param name="name">Name</param>
        /// <returns>WzDirectory[name]</returns>
        public new WzObject this[string name]
        {
            get { return WzDirectory[name]; }
        }

        public WzHeader Header { get { return header; } set { header = value; } }

        public short Version { get { return mapleStoryPatchVersion; } set { mapleStoryPatchVersion = value; } }

        public string FilePath { get { return path; } }

        public WzMapleVersion MapleVersion { get { return maplepLocalVersion; } set { maplepLocalVersion = value; } }

        /// <summary>
        /// The detected MapleStory locale version from 'MapleStory.exe' client.
        /// KMST, GMS, EMS, MSEA, CMS, TWMS, etc.
        /// </summary>
        public MapleStoryLocalisation MapleLocaleVersion { get { return mapleLocaleVersion; } private set { } }

        /// <summary>
        ///  Since KMST1132 / GMSv230 around 2022/02/09, wz removed the 2-byte encVer at position 0x3C, and use a fixed encVer 777.
        /// </summary>
        public bool Is64BitWzFile { get { return !wz_withEncryptVersionHeader; } private set { } }

        public override WzObject Parent { get { return null; } internal set { } }

        public override WzFile WzFileParent { get { return this; } }

        public override void Dispose()
        {
            _isUnloaded = true; // flag first

            if (wzDir == null || wzDir.reader == null)
                return;

            wzDir.reader.Close();
            wzDir.reader = null;
            Header = null;
            path = null;
            name = null;
            WzDirectory.Dispose();
        }

        private bool _isUnloaded = false;
        /// <summary>
        /// Returns true if this WZ file has been unloaded
        /// </summary>
        public bool IsUnloaded { get { return _isUnloaded; } private set { } }

        /// <summary>
        /// Initialize MapleStory WZ file
        /// </summary>
        /// <param name="gameVersion"></param>
        /// <param name="version"></param>
        public WzFile(short gameVersion, WzMapleVersion version)
        {
            wzDir = new WzDirectory();
            this.Header = WzHeader.GetDefault();
            mapleStoryPatchVersion = gameVersion;
            maplepLocalVersion = version;
            WzIv = WzTool.GetIvByMapleVersion(version);
            wzDir.WzIv = WzIv;
        }

        /// <summary>
        /// Open a wz file from a file on the disk
        /// </summary>
        /// <param name="filePath">Path to the wz file</param>
        /// <param name="version"></param>
        public WzFile(string filePath, WzMapleVersion version) : this(filePath, -1, version)
        {
        }

        /// <summary>
        /// Open a wz file from a file on the disk
        /// </summary>
        /// <param name="filePath">Path to the wz file</param>
        /// <param name="gameVersion"></param>
        /// <param name="version"></param>
        public WzFile(string filePath, short gameVersion, WzMapleVersion version)
        {
            name = Path.GetFileName(filePath);
            path = filePath;
            mapleStoryPatchVersion = gameVersion;
            maplepLocalVersion = version;

            if (version == WzMapleVersion.GETFROMZLZ)
            {
                using (FileStream zlzStream = File.OpenRead(Path.Combine(Path.GetDirectoryName(filePath), "ZLZ.dll")))
                {
                    this.WzIv = Util.WzKeyGenerator.GetIvFromZlz(zlzStream);
                }
            }
            else
                this.WzIv = WzTool.GetIvByMapleVersion(version);
        }

        /// <summary>
        /// Open a wz file from a file on the disk with a custom WzIv key
        /// </summary>
        /// <param name="filePath">Path to the wz file</param>
        public WzFile(string filePath, byte[] wzIv)
        {
            name = Path.GetFileName(filePath);
            path = filePath;
            mapleStoryPatchVersion = -1;
            maplepLocalVersion = WzMapleVersion.CUSTOM;

            this.WzIv = wzIv;
        }

        /// <summary>
        /// Parses the wz file, if the wz file is a list.wz file, WzDirectory will be a WzListDirectory, if not, it'll simply be a WzDirectory
        /// </summary>
        /// <param name="WzIv">WzIv is not set if null (Use existing iv)</param>
        public WzFileParseStatus ParseWzFile(byte[] WzIv = null)
        {
            /*if (maplepLocalVersion != WzMapleVersion.GENERATE)
            {
                parseErrorMessage = ("Cannot call ParseWzFile() if WZ file type is not GENERATE. Have you entered an invalid WZ key? ");
                return false;
            }*/
            if (WzIv != null)
            {
                this.WzIv = WzIv;
            }
            return ParseMainWzDirectory(false);
        }


        /// <summary>
        /// Parse directories in the WZ file
        /// </summary>
        /// <param name="parseErrorMessage"></param>
        /// <param name="lazyParse">Only load the firt WzDirectory found if true</param>
        /// <returns></returns>
        internal WzFileParseStatus ParseMainWzDirectory(bool lazyParse = false)
        {
            if (this.path == null)
            {
                Helpers.ErrorLogger.Log(Helpers.ErrorLevel.Critical, "[Error] Path is null");
                return WzFileParseStatus.Path_Is_Null;
            }
            WzBinaryReader reader = new WzBinaryReader(File.Open(this.path, FileMode.Open, FileAccess.Read, FileShare.Read), WzIv);

            this.Header = new WzHeader();
            this.Header.Ident = reader.ReadString(4);
            this.Header.FSize = reader.ReadUInt64();
            this.Header.FStart = reader.ReadUInt32();
            this.Header.Copyright = reader.ReadString((int)(Header.FStart - 17U));

            byte unk1 = reader.ReadByte();
            byte[] unk2 = reader.ReadBytes((int)(Header.FStart - (ulong)reader.BaseStream.Position));
            reader.Header = this.Header;

            Check64BitClient(reader);  // update b64BitClient flag

            // the value of wzVersionHeader is less important. It is used for reading/writing from/to WzFile Header, and calculating the versionHash.
            // it can be any number if the client is 64-bit. Assigning 777 is just for convenience when calculating the versionHash.
            this.wzVersionHeader = this.wz_withEncryptVersionHeader ? reader.ReadUInt16() : wzVersionHeader64bit_start;

            Debug.WriteLine("----------------------------------------");
            Debug.WriteLine(string.Format("Read Wz File {0}", this.Name));
            Debug.WriteLine(string.Format("wz_withEncryptVersionHeader: {0}", wz_withEncryptVersionHeader));
            Debug.WriteLine(string.Format("wzVersionHeader: {0}", wzVersionHeader));
            Debug.WriteLine("----------------------------------------");

            if (mapleStoryPatchVersion == -1)
            {
                // for 64-bit client, return immediately if version 777 works correctly.
                // -- the latest KMS update seems to have changed it to 778? 779?
                if (!this.wz_withEncryptVersionHeader) 
                {
                    for (ushort maplestoryVerToDecode = wzVersionHeader64bit_start; maplestoryVerToDecode < wzVersionHeader64bit_start + 10; maplestoryVerToDecode++) // 770 ~ 780
                    {
                        if (TryDecodeWithWZVersionNumber(reader, wzVersionHeader, maplestoryVerToDecode, lazyParse))
                        {
                            return WzFileParseStatus.Success;
                        }
                    }
                }
                // Attempt to get version from MapleStory.exe first
                short maplestoryVerDetectedFromClient = GetMapleStoryVerFromExe(this.path, out this.mapleLocaleVersion);

                // this step is actually not needed if we know the maplestory patch version (the client .exe), but since we dont..
                // we'll need a bruteforce way around it. 
                const short MAX_PATCH_VERSION = 1000; // wont be reached for the forseeable future.

                for (int j = maplestoryVerDetectedFromClient; j < MAX_PATCH_VERSION; j++)
                {
                    //Debug.WriteLine("Try decode 1 with maplestory ver: " + j);

                    if (TryDecodeWithWZVersionNumber(reader, wzVersionHeader, j, lazyParse))
                    {
                        return WzFileParseStatus.Success;
                    }
                }
                //parseErrorMessage = "Error with game version hash : The specified game version is incorrect and WzLib was unable to determine the version itself";
                return WzFileParseStatus.Error_Game_Ver_Hash;
            }
            else
            {
                this.versionHash = CheckAndGetVersionHash(wzVersionHeader, mapleStoryPatchVersion);
                reader.Hash = this.versionHash;

                WzDirectory directory = new WzDirectory(reader, this.name, this.versionHash, this.WzIv, this);
                directory.ParseDirectory();
                this.wzDir = directory;
            }
            return WzFileParseStatus.Success;
        }

        /// <summary>
        /// encVer detecting:
        /// Since KMST1132 (GMSv230, 2022/02/09), wz removed the 2-byte encVer at 0x3C, and use a fixed encVer 777.
        /// Here we try to read the first 2 bytes from data part (0x3C) and guess if it looks like an encVer.
        ///
        /// Credit: WzComparerR2 project
        /// </summary>
        private void Check64BitClient(WzBinaryReader reader)
        {
            if (this.Header.FSize >= 2)
            {
                reader.BaseStream.Position = this.header.FStart; // go back to 0x3C

                int encver = reader.ReadUInt16();
                if (encver > 0xff) // encver always less than 256
                {
                    this.wz_withEncryptVersionHeader = false;
                }
                else if (encver == 0x80)
                {
                    // there's an exceptional case that the first field of data part is a compressed int which determined property count,
                    // if the value greater than 127 and also to be a multiple of 256, the first 5 bytes will become to
                    //   80 00 xx xx xx
                    // so we additional check the int value, at most time the child node count in a wz won't greater than 65536.
                    if (this.Header.FSize >= 5)
                    {
                        reader.BaseStream.Position = this.header.FStart; // go back to 0x3C
                        int propCount = reader.ReadInt32();
                        if (propCount > 0 && (propCount & 0xff) == 0 && propCount <= 0xffff)
                        {
                            this.wz_withEncryptVersionHeader = false;
                        }
                    }
                } else
                {
                    // old wz file with header version
                }
            }
            else
            {
                // Obviously, if data part have only 1 byte, encver must be deleted.
                this.wz_withEncryptVersionHeader = false;
            }


            // reset position
            reader.BaseStream.Position = this.Header.FStart;
        }

        private bool TryDecodeWithWZVersionNumber(WzBinaryReader reader, int useWzVersionHeader, int useMapleStoryPatchVersion, bool lazyParse)
        {
            this.mapleStoryPatchVersion = (short)useMapleStoryPatchVersion;

            this.versionHash = CheckAndGetVersionHash(useWzVersionHeader, mapleStoryPatchVersion);
            if (this.versionHash == 0) // ugly hack, but that's the only way if the version number isnt known (nexon stores this in the .exe)
                return false;

            reader.Hash = this.versionHash;
            long fallbackOffsetPosition = reader.BaseStream.Position; // save position to rollback to, if should parsing fail from here
            WzDirectory testDirectory;
            try
            {
                testDirectory = new WzDirectory(reader, this.name, this.versionHash, this.WzIv, this);
                testDirectory.ParseDirectory(lazyParse);
            }
            catch (Exception exp)
            {
                Debug.WriteLine(exp.ToString());

                reader.BaseStream.Position = fallbackOffsetPosition;
                return false;
            }

            // test the image and see if its correct by parsing it 
            bool bCloseTestDirectory = true;
            try
            {
                WzImage testImage = testDirectory.WzImages.FirstOrDefault();
                if (testImage != null)
                {
                    try
                    {
                        reader.BaseStream.Position = testImage.Offset;
                        byte checkByte = reader.ReadByte();
                        reader.BaseStream.Position = fallbackOffsetPosition;

                        switch (checkByte)
                        {
                            case 0x73:
                            case 0x1b:
                                {
                                    WzDirectory directory = new WzDirectory(reader, this.name, this.versionHash, this.WzIv, this);

                                    directory.ParseDirectory(lazyParse);
                                    this.wzDir = directory;
                                    return true;
                                }
                            case 0x30:
                            case 0x6C: // idk
                            case 0xBC: // Map002.wz? KMST?
                            default:
                                {
                                    string printError = string.Format("[WzFile.cs] New Wz image header found. checkByte = {0}. File Name = {1}", checkByte, Name);

                                    Helpers.ErrorLogger.Log(Helpers.ErrorLevel.MissingFeature,printError);
                                    Debug.WriteLine(printError);
                                    // log or something
                                    break;
                                }
                        }
                        reader.BaseStream.Position = fallbackOffsetPosition; // reset
                    }
                    catch
                    {
                        reader.BaseStream.Position = fallbackOffsetPosition; // reset
                        return false;
                    }
                    return true;
                }
                else // if there's no image in the WZ file (new KMST Base.wz), test the directory instead
                {
                    // coincidentally in msea v194 Map001.wz, the hash matches exactly using mapleStoryPatchVersion of 113, and it fails to decrypt later on (probably 1 in a million chance? o_O).
                    // damn, technical debt accumulating here
                    if (mapleStoryPatchVersion == 113)
                    {
                        // hack for now
                        reader.BaseStream.Position = fallbackOffsetPosition; // reset
                        return false;
                    }
                    else
                    {
                        this.wzDir = testDirectory;
                        bCloseTestDirectory = false;

                        return true;
                    }
                }
            }
            finally
            {
                if (bCloseTestDirectory)
                    testDirectory.Dispose();
            }
        }

        /// <summary>
        /// Attempts to get the MapleStory patch version number from MapleStory.exe
        /// </summary>
        /// <returns>0 if the exe could not be found, or version number be detected</returns>
        private static short GetMapleStoryVerFromExe(string wzFilePath, out MapleStoryLocalisation mapleLocaleVersion)
        {
            // https://github.com/lastbattle/Harepacker-resurrected/commit/63e2d72ac006f0a45fc324a2c33c23f0a4a988fa#r56759414
            // <3 mechpaul
            const string MAPLESTORY_EXE_NAME = "MapleStory.exe";
            const string MAPLESTORYT_EXE_NAME = "MapleStoryT.exe";
            const string MAPLESTORYADMIN_EXE_NAME = "MapleStoryA.exe";

            FileInfo wzFileInfo = new FileInfo(wzFilePath);
            if (!wzFileInfo.Exists)
            {
                mapleLocaleVersion = MapleStoryLocalisation.Not_Known; // set
                return 0;
            }

            System.IO.DirectoryInfo currentDirectory = wzFileInfo.Directory;
            for (int i = 0; i < 4; i++)  // just attempt 4 directories here
            {
                FileInfo[] msExeFileInfos = currentDirectory.GetFiles(MAPLESTORY_EXE_NAME, SearchOption.TopDirectoryOnly); // case insensitive 
                FileInfo[] msTExeFileInfos = currentDirectory.GetFiles(MAPLESTORYT_EXE_NAME, SearchOption.TopDirectoryOnly);  // case insensitive 
                FileInfo[] msAdminExeFileInfos = currentDirectory.GetFiles(MAPLESTORYADMIN_EXE_NAME, SearchOption.TopDirectoryOnly);  // case insensitive 

                List<FileInfo> exeFileInfo = new List<FileInfo>();
                if (msTExeFileInfos.Length > 0 && msTExeFileInfos[0].Exists) // prioritize MapleStoryT.exe first
                {
                    exeFileInfo.Add(msTExeFileInfos[0]);
                }
                if (msAdminExeFileInfos.Length > 0 && msAdminExeFileInfos[0].Exists)
                {
                    exeFileInfo.Add(msAdminExeFileInfos[0]);
                }
                if (msExeFileInfos.Length > 0 && msExeFileInfos[0].Exists)
                {
                    exeFileInfo.Add(msExeFileInfos[0]);
                }

                foreach (FileInfo msExeFileInfo in exeFileInfo)
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(Path.Combine(currentDirectory.FullName, msExeFileInfo.FullName));

                    if ((versionInfo.FileMajorPart == 1 && versionInfo.FileMinorPart == 0 && versionInfo.FileBuildPart == 0)
                        || (versionInfo.FileMajorPart == 0 && versionInfo.FileMinorPart == 0 && versionInfo.FileBuildPart == 0)) // older client uses 1.0.0.1 
                        continue;

                    int locale = versionInfo.FileMajorPart;
                    MapleStoryLocalisation localeVersion = MapleStoryLocalisation.Not_Known;
                    if (Enum.IsDefined(typeof(MapleStoryLocalisation), locale))
                    {
                        localeVersion = (MapleStoryLocalisation)locale;
                    }
                    var msVersion = versionInfo.FileMinorPart;
                    var msMinorPatchVersion = versionInfo.FileBuildPart;

                    mapleLocaleVersion = localeVersion; // set
                    return (short)msVersion;
                }
                currentDirectory = currentDirectory.Parent; // check the parent folder on the next run
                if (currentDirectory == null)
                    break;
            }

            mapleLocaleVersion = MapleStoryLocalisation.Not_Known; // set
            return 0;
        }

        /// <summary>
        /// Check and gets the version hash.
        /// </summary>
        /// <param name="wzVersionHeader">The version header from .wz file.</param>
        /// <param name="maplestoryPatchVersion"></param>
        /// <returns></returns>
        private static uint CheckAndGetVersionHash(int wzVersionHeader, int maplestoryPatchVersion)
        {
            uint versionHash = 0;

            foreach (char ch in maplestoryPatchVersion.ToString())
            {
                versionHash = (versionHash * 32) + (byte)ch + 1;
            }

            if (wzVersionHeader == wzVersionHeader64bit_start)
                return (uint)versionHash; // always 59192

            int decryptedVersionNumber = (byte)~((versionHash >> 24) & 0xFF ^ (versionHash >> 16) & 0xFF ^ (versionHash >> 8) & 0xFF ^ versionHash & 0xFF);

            if (wzVersionHeader == decryptedVersionNumber)
                return (uint)versionHash;
            return 0; // invalid
        }

        /// <summary>
        /// Version hash
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateWZVersionHash()
        {
            versionHash = 0;
            foreach (char ch in mapleStoryPatchVersion.ToString())
            {
                versionHash = (versionHash * 32) + (byte)ch + 1;
            }
            wzVersionHeader = (byte)~((versionHash >> 24) & 0xFF ^ (versionHash >> 16) & 0xFF ^ (versionHash >> 8) & 0xFF ^ versionHash & 0xFF);
        }

        /// <summary>
        /// Saves a wz file to the disk, AKA repacking.
        /// </summary>
        /// <param name="path">Path to the output wz file</param>
        /// <param name="override_saveAs64BitWZ"></param>
        /// <param name="savingToPreferredWzVer"></param>
        public void SaveToDisk(string path, bool? override_saveAs64BitWZ = null, WzMapleVersion savingToPreferredWzVer = WzMapleVersion.UNKNOWN)
        {
            // WZ IV
            if (savingToPreferredWzVer == WzMapleVersion.UNKNOWN)
                WzIv = WzTool.GetIvByMapleVersion(maplepLocalVersion); // get from local WzFile
            else
                WzIv = WzTool.GetIvByMapleVersion(savingToPreferredWzVer); // custom selected

            bool bIsWzIvSimilar = WzIv.SequenceEqual(wzDir.WzIv); // check if its saving to the same IV.
            wzDir.WzIv = WzIv;

            // MapleStory UserKey
            bool bIsWzUserKeyDefault = MapleCryptoConstants.IsDefaultMapleStoryUserKey(); // check if its saving to the same UserKey.
            // Save WZ as 64-bit wz format
            bool bSaveAs64BitWz = this.wz_withEncryptVersionHeader;
            if (override_saveAs64BitWZ != null)
            {
                bSaveAs64BitWz = (bool)override_saveAs64BitWZ;
            }

            CreateWZVersionHash();
            wzDir.SetVersionHash(versionHash);

            Debug.WriteLine("----------------------------------------");
            Debug.WriteLine(string.Format("Saving Wz File {0}", this.Name));
            Debug.WriteLine(string.Format("wzVersionHeader: {0}", wzVersionHeader));
            Debug.WriteLine(string.Format("bSaveAs64BitWz: {0}", bSaveAs64BitWz));
            Debug.WriteLine("----------------------------------------");

            try
            {
                string tempFile = Path.GetFileNameWithoutExtension(path) + ".TEMP";
                File.Create(tempFile).Close();
                using (FileStream fs = new FileStream(tempFile, FileMode.Append, FileAccess.Write))
                {
                    wzDir.GenerateDataFile(bIsWzIvSimilar ? null : WzIv, bIsWzUserKeyDefault, fs);
                }

                WzTool.StringCache.Clear();

                using (WzBinaryWriter wzWriter = new WzBinaryWriter(File.Create(path), WzIv))
                {
                    wzWriter.Hash = versionHash;

                    uint totalLen = wzDir.GetImgOffsets(wzDir.GetOffsets(Header.FStart + (!bSaveAs64BitWz ? 2u : 0)));
                    Header.FSize = totalLen - Header.FStart;
                    for (int i = 0; i < 4; i++)
                    {
                        wzWriter.Write((byte)Header.Ident[i]);
                    }
                    wzWriter.Write((long)Header.FSize);
                    wzWriter.Write(Header.FStart);
                    wzWriter.WriteNullTerminatedString(Header.Copyright);

                    long extraHeaderLength = Header.FStart - wzWriter.BaseStream.Position;
                    if (extraHeaderLength > 0)
                    {
                        wzWriter.Write(new byte[(int)extraHeaderLength]);
                    }
                    if (!bSaveAs64BitWz) // 64 bit doesnt have a version number.
                        wzWriter.Write((ushort) wzVersionHeader);

                    wzWriter.Header = Header;
                    wzDir.SaveDirectory(wzWriter);
                    wzWriter.StringCache.Clear();

                    using (FileStream fs = File.OpenRead(tempFile))
                    {
                        wzDir.SaveImages(wzWriter, fs);
                    }
                    File.Delete(tempFile);

                    wzWriter.StringCache.Clear();
                }
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public void ExportXml(string path, bool oneFile)
        {
            if (oneFile)
            {
                FileStream fs = File.Create(path + "/" + this.name + ".xml");
                StreamWriter writer = new StreamWriter(fs);

                int level = 0;
                writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.OpenNamedTag("WzFile", this.name, true));
                this.wzDir.ExportXml(writer, oneFile, level, false);
                writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.CloseTag("WzFile"));

                writer.Close();
            }
            else
            {
                throw new Exception("Under Construction");
            }
        }

        /// <summary>
        /// Returns an array of objects from a given path. Wild cards are supported
        /// For example :
        /// GetObjectsFromPath("Map.wz/Map0/*");
        /// Would return all the objects (in this case images) from the sub directory Map0
        /// </summary>
        /// <param name="path">The path to the object(s)</param>
        /// <returns>An array of IWzObjects containing the found objects</returns>
        public List<WzObject> GetObjectsFromWildcardPath(string path)
        {
            if (path.ToLower() == name.ToLower())
                return new List<WzObject> { WzDirectory };
            else if (path == "*")
            {
                var fullList = new List<WzObject> { WzDirectory };
                fullList.AddRange(GetObjectsFromDirectory(WzDirectory));
                return fullList;
            }
            else if (!path.Contains("*"))
                return new List<WzObject> { GetObjectFromPath(path) };

            string[] seperatedNames = path.Split("/".ToCharArray());
            if (seperatedNames.Length == 2 && seperatedNames[1] == "*")
                return GetObjectsFromDirectory(WzDirectory);

            // Use Linq to flatten the sequence of paths returned by the GetPathsFromImage and GetPathsFromDirectory methods
            // and filter the paths that match the given wildcard pattern
            var objList = WzDirectory.WzImages.SelectMany(img => GetPathsFromImage(img, name + "/" + img.Name))
                .Concat(wzDir.WzDirectories.SelectMany(dir => GetPathsFromDirectory(dir, name + "/" + dir.Name)))
                .Where(spath => StringMatch(path, spath)) // filter the paths that match the pattern
                .Select(spath => GetObjectFromPath(spath)) // convert the filtered paths into WzObjects
                .ToList();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            return objList;
        }

        public List<WzObject> GetObjectsFromRegexPath(string path)
        {
            if (path.ToLower() == name.ToLower())
                return new List<WzObject> { WzDirectory };

            // Use Linq to flatten the sequence of paths returned by the GetPathsFromImage and GetPathsFromDirectory methods
            // and filter the paths that match the given regular expression
            var objList = WzDirectory.WzImages.SelectMany(img => GetPathsFromImage(img, name + "/" + img.Name))
                .Concat(wzDir.WzDirectories.SelectMany(dir => GetPathsFromDirectory(dir, name + "/" + dir.Name)))
                .Where(spath => Regex.Match(spath, path).Success)
                .Select(spath => GetObjectFromPath(spath)) // convert the filtered paths into WzObjects
                .ToList();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            return objList;
        }

        public List<WzObject> GetObjectsFromDirectory(WzDirectory dir)
        {
            // Create a list to store the objects
            List<WzObject> objList = new List<WzObject>();

            // Get the objects from the WzImages in the directory
            // and add them to the list
            objList.AddRange(dir.WzImages.SelectMany(img => GetObjectsFromImage(img)));

            // Get the objects from the WzDirectories in the directory
            // and add them to the list
            objList.AddRange(dir.WzDirectories.SelectMany(subdir => GetObjectsFromDirectory(subdir)));

            // Return the list of objects
            return objList;
        }

        public List<WzObject> GetObjectsFromImage(WzImage img)
        {
            // Use Linq to flatten the sequence of WzObjects returned by the GetObjectsFromProperty method
            // and convert the results into a List<WzObject>
            var objList = img.WzProperties.SelectMany(prop =>
            {
                var objects = new List<WzObject> { prop }; // initialize the list with the current property
                objects.AddRange(GetObjectsFromProperty(prop)); // add the objects from the property
                return objects; // return the list of objects
            }).ToList();

            return objList;
        }

        public List<WzObject> GetObjectsFromProperty(WzImageProperty prop)
        {
            List<WzObject> objList = new List<WzObject>();
            var subProperties = new List<WzImageProperty>();

            bool bAddRange = true;
            switch (prop.PropertyType)
            {
                case WzPropertyType.Canvas:
                    subProperties = ((WzCanvasProperty)prop).WzProperties;
                    objList.Add(((WzCanvasProperty)prop).PngProperty);
                    break;
                case WzPropertyType.Convex:
                    subProperties = ((WzConvexProperty)prop).WzProperties;
                    break;
                case WzPropertyType.SubProperty:
                    subProperties = ((WzSubProperty)prop).WzProperties;
                    break;
                case WzPropertyType.Vector:
                    objList.Add(((WzVectorProperty)prop).X);
                    objList.Add(((WzVectorProperty)prop).Y);
                    bAddRange = false;
                    break;
            }

            if (bAddRange)
                objList.AddRange(subProperties.SelectMany(p => GetObjectsFromProperty(p)));

            return objList;
        }

        internal List<string> GetPathsFromDirectory(WzDirectory dir, string curPath)
        {
            // Use Linq to flatten the sequence of paths returned by the GetPathsFromImage and GetPathsFromDirectory methods
            // and convert the results into a List<string>
            var objList = dir.WzImages.SelectMany(img =>
            {
                var paths = new List<string> { curPath + "/" + img.Name }; // initialize the list with the current path
                paths.AddRange(GetPathsFromImage(img, curPath + "/" + img.Name)); // add the paths from the image
                return paths; // return the list of paths
            }).Concat(dir.WzDirectories.SelectMany(subdir =>
            {
                var paths = new List<string> { curPath + "/" + subdir.Name }; // initialize the list with the current path
                paths.AddRange(GetPathsFromDirectory(subdir, curPath + "/" + subdir.Name)); // add the paths from the subdirectory
                return paths; // return the list of paths
            })).ToList();

            return objList;
        }


        internal List<string> GetPathsFromImage(WzImage img, string curPath)
        {
            // Use Linq to flatten the sequence of paths returned by the GetPathsFromProperty method
            // and convert the results into a List<string>
            var objList = img.WzProperties.SelectMany(prop =>
            {
                var paths = new List<string> { curPath + "/" + prop.Name }; // initialize the list with the current path
                paths.AddRange(GetPathsFromProperty(prop, curPath + "/" + prop.Name)); // add the paths from the property
                return paths; // return the list of paths
            }).ToList();

            return objList;
        }

        internal List<string> GetPathsFromProperty(WzImageProperty prop, string curPath)
        {
            List<string> objList = new List<string>();
            var subProperties = new List<WzImageProperty>();

            bool bAddRange = true;
            switch (prop.PropertyType)
            {
                case WzPropertyType.Canvas:
                    subProperties = ((WzCanvasProperty)prop).WzProperties;
                    objList.Add(curPath + "/PNG");
                    break;
                case WzPropertyType.Convex:
                    subProperties = ((WzConvexProperty)prop).WzProperties;
                    break;
                case WzPropertyType.SubProperty:
                    subProperties = ((WzSubProperty)prop).WzProperties;
                    break;
                case WzPropertyType.Vector:
                    objList.Add(curPath + "/X");
                    objList.Add(curPath + "/Y");
                    bAddRange = false;
                    break;
            }

            if (bAddRange)
                objList.AddRange(subProperties.SelectMany(p => GetPathsFromProperty(p, curPath + "/" + p.Name)));

            return objList;
        }

        /// <summary>
        /// Get WZ objects from path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="lookupOtherOpenedWzFile"></param>
        /// <returns></returns>
        public WzObject GetObjectFromPath(string path, bool checkFirstDirectoryName = true)
        {
            string[] seperatedPath = path.Split("/".ToCharArray());
            if (seperatedPath.Length == 1)
                return WzDirectory;

            WzObject checkObjInOtherWzFile = null;

            if (checkFirstDirectoryName)
            {
                if (WzFileManager.fileManager != null)
                {
                    // Use FirstOrDefault() and Any() to find the first matching WzDirectory
                    // and check if there are any matching WzDirectory in the list
                    WzDirectory wzDir = WzFileManager.fileManager.GetWzDirectoriesFromBase(seperatedPath[0]).
                        FirstOrDefault(
                            dir => dir.name.ToLower() == seperatedPath[0].ToLower() || 
                            dir.name.Substring(0, dir.name.Length - 3).ToLower() == seperatedPath[0].ToLower());
                    if (wzDir == null && seperatedPath.Length >= 1)
                    {
                        checkObjInOtherWzFile = WzFileManager.fileManager.FindWzImageByName(seperatedPath[0], seperatedPath[1]); // Map/xxx.img

                        if (checkObjInOtherWzFile == null && seperatedPath.Length >= 2) // Map/Obj/xxx.img -> Obj.wz
                        {
                            checkObjInOtherWzFile = WzFileManager.fileManager.FindWzImageByName(seperatedPath[0] + Path.DirectorySeparatorChar + seperatedPath[1], seperatedPath[2]);
                            if (checkObjInOtherWzFile == null)
                                return null;
                            seperatedPath = seperatedPath.Skip(2).ToArray();
                        } else
                        {
                            seperatedPath = seperatedPath.Skip(1).ToArray();
                        }
                    }
                    else
                        return null;
                } else
                    return null;
            }
            
            WzObject curObj = checkObjInOtherWzFile ?? WzDirectory;
            if (curObj == null)
                return null;
            
            bool bFirst = true;
            foreach (string pathPart in seperatedPath)
            {
                if (bFirst)
                {
                    bFirst = false;
                    continue;
                }
                if (curObj == null)
                    return null;

                switch (curObj.ObjectType)
                {
                    case WzObjectType.Directory:
                        curObj = ((WzDirectory)curObj)[pathPart];
                        continue;
                    case WzObjectType.Image:
                        curObj = ((WzImage)curObj)[pathPart];
                        continue;
                    case WzObjectType.Property:
                        switch (((WzImageProperty)curObj).PropertyType)
                        {
                            case WzPropertyType.Canvas:
                                curObj = ((WzCanvasProperty)curObj)[pathPart];
                                continue;
                            case WzPropertyType.Convex:
                                curObj = ((WzConvexProperty)curObj)[pathPart];
                                continue;
                            case WzPropertyType.SubProperty:
                                curObj = ((WzSubProperty)curObj)[pathPart];
                                continue;
                            case WzPropertyType.Vector:
                                if (pathPart == "X")
                                    return ((WzVectorProperty)curObj).X;
                                else if (pathPart == "Y")
                                    return ((WzVectorProperty)curObj).Y;
                                else
                                    return null;
                            default: // Wut?
                                return null;
                        }
                }
            }
            if (curObj == null)
            {
                return null;
            }
            return curObj;
        }

        /// <summary>
        /// Get WZ object from multiple loaded WZ files in memory
        /// </summary>
        /// <param name="path"></param>
        /// <param name="wzFiles"></param>
        /// <returns></returns>
        public static WzObject GetObjectFromMultipleWzFilePath(string path, IReadOnlyCollection<WzFile> wzFiles)
        {
            // Use Select() and FirstOrDefault() to transform and find the first matching WzObject
            return wzFiles.Select(file => file.GetObjectFromPath(path, false))
                          .FirstOrDefault(obj => obj != null);
        }


        internal bool StringMatch(string strWildCard, string strCompare)
        {
            int wildCardLength = strWildCard.Length;
            int compareLength = strCompare.Length;
            int wildCardIndex = 0;
            int compareIndex = 0;

            while (wildCardIndex < wildCardLength && compareIndex < compareLength)
            {
                if (strWildCard[wildCardIndex] == '*')
                {
                    // If there are multiple * in the wildcard, move to the last *
                    while (wildCardIndex < wildCardLength && strWildCard[wildCardIndex] == '*')
                    {
                        wildCardIndex++;
                    }

                    // If there are no characters left in the wildcard, return true
                    if (wildCardIndex == wildCardLength)
                    {
                        return true;
                    }

                    // Try to match the remaining part of the wildcard with the remaining part of the compare string
                    // starting from the current compare index.
                    while (compareIndex < compareLength)
                    {
                        if (StringMatch(strWildCard.Substring(wildCardIndex), strCompare.Substring(compareIndex)))
                        {
                            return true;
                        }

                        compareIndex++;
                    }

                    // If we reached here, it means the remaining part of the wildcard could not be matched
                    // with the remaining part of the compare string, so return false.
                    return false;
                }
                else if (strWildCard[wildCardIndex] == strCompare[compareIndex])
                {
                    wildCardIndex++;
                    compareIndex++;
                }
                else
                {
                    // If the current characters do not match and the wildcard character is not a *,
                    // return false.
                    return false;
                }
            }

            // If we reached here, it means one of the strings has been fully processed.
            // If both strings have been fully processed, return true, else return false.
            return wildCardIndex == wildCardLength && compareIndex == compareLength;
        }

        public override void Remove()
        {
            Dispose();
        }
    }
}