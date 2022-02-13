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
        internal const ushort wzVersionHeader64bit = 777;
        internal uint versionHash = 0;
        internal short mapleStoryPatchVersion = 0;
        internal WzMapleVersion maplepLocalVersion;
        internal MapleStoryLocalisation mapleLocaleVersion = MapleStoryLocalisation.Not_Known;
        internal bool b64BitClient = false; // KMS update after Q4 2021, ver 1.2.357
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

        public bool Is64BitWZFile { get { return b64BitClient; } private set { } }

        public override WzObject Parent { get { return null; } internal set { } }

        public override WzFile WzFileParent { get { return this; } }

        public override void Dispose()
        {
            if (wzDir == null || wzDir.reader == null)
                return;
            try
            {
                wzDir.reader.Close();
            }
            finally
            {
                wzDir.reader = null;
            }
            Header = null;
            path = null;
            name = null;
            WzDirectory.Dispose();
        }

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
        public WzFile(string filePath, WzMapleVersion version) : this(filePath, -1, version)
        {
        }

        /// <summary>
        /// Open a wz file from a file on the disk
        /// </summary>
        /// <param name="filePath">Path to the wz file</param>
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

            string[] filePathSplit = Regex.Split(filePath, @"\\");
            this.b64BitClient = filePathSplit.Contains("Data"); // TODO: Find a better way of identifying 64-bit client/ WZ from the WZ files instead. This might break if it isnt installed in the right path, esp. private servers
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

            this.b64BitClient = false; // TODO
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

            //this.Header.Copyright = reader.ReadNullTerminatedString();
            this.Header.Copyright = reader.ReadString((int)(Header.FStart - 17U));
            byte unk1 = reader.ReadByte();
            byte[] unk2 = reader.ReadBytes((int)(Header.FStart - (ulong)reader.BaseStream.Position));

            reader.Header = this.Header;

            Check64BitClient(reader);  // update b64BitClient flag

            // the value of wzVersionHeader is less important. It is used for reading/writing from/to WzFile Header, and calculating the versionHash.
            // it can be any number if the client is 64-bit. Assigning 777 is just for convenience when calculating the versionHash.
            this.wzVersionHeader = b64BitClient ? wzVersionHeader64bit : reader.ReadUInt16();

            if (mapleStoryPatchVersion == -1)
            {
                // for 64-bit client, return immediately if version 777 works correctly.
                if (b64BitClient && TryDecodeWithWZVersionNumber(reader, wzVersionHeader, wzVersionHeader64bit, lazyParse))
                {
                    return WzFileParseStatus.Success;
                }

                // Attempt to get version from MapleStory.exe first
                short maplestoryVerDetectedFromClient = GetMapleStoryVerFromExe(this.path, out this.mapleLocaleVersion);

                // this step is actually not needed if we know the maplestory patch version (the client .exe), but since we dont..
                // we'll need a bruteforce way around it. 
                const short MAX_PATCH_VERSION = 1000; // wont be reached for the forseeable future.

                for (int j = maplestoryVerDetectedFromClient; j < MAX_PATCH_VERSION; j++)
                {
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
                this.wzVersionHeader = reader.ReadUInt16();
                if (this.wzVersionHeader > 0xff)
                {
                    b64BitClient = true;
                }
                else if (this.wzVersionHeader == 0x80)
                {
                    // there's an exceptional case that the first field of data part is a compressed int which determines the property count,
                    // if the value greater than 127 and also to be a multiple of 256, the first 5 bytes will become to
                    // 80 00 xx xx xx
                    // so we additional check the int value, at most time the child node count in a WzFile won't greater than 65536 (0xFFFF).
                    if (this.Header.FSize >= 5)
                    {
                        reader.BaseStream.Position = this.header.FStart; // go back to 0x3C
                        int propCount = reader.ReadCompressedInt();
                        if (propCount > 0 && (propCount & 0xFF) == 0 && propCount <= 0xFFFF)
                        {
                            b64BitClient = true;
                        }
                    }
                }
            }
            else
            {
                // Obviously, if data part have only 1 byte, encVer must be deleted.
                b64BitClient = true;
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
                            case WzImage.WzImageHeaderByte_WithoutOffset: // 0x73
                            case WzImage.WzImageHeaderByte_WithOffset:
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
                                    Helpers.ErrorLogger.Log(Helpers.ErrorLevel.MissingFeature,
                                        string.Format("[WzFile.cs] New Wz image header found. checkByte = {0}. File Name = {1}", checkByte, Name));
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
            int VersionNumber = maplestoryPatchVersion;
            int VersionHash = 0;
            string VersionNumberStr = VersionNumber.ToString();

            for (int i = 0; i < VersionNumberStr.Length; i++)
            {
                VersionHash = (32 * VersionHash) + (int)VersionNumberStr[i] + 1;
            }
            if (wzVersionHeader == wzVersionHeader64bit)
                return (uint)VersionHash; // always 59192

            int a = (VersionHash >> 24) & 0xFF;
            int b = (VersionHash >> 16) & 0xFF;
            int c = (VersionHash >> 8) & 0xFF;
            int d = VersionHash & 0xFF;
            int DecryptedVersionNumber = (0xff ^ a ^ b ^ c ^ d);

            if (wzVersionHeader == DecryptedVersionNumber)
                return (uint)VersionHash;
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
            uint a = (versionHash >> 24) & 0xFF,
                b = (versionHash >> 16) & 0xFF,
                c = (versionHash >> 8) & 0xFF,
                d = versionHash & 0xFF;
            wzVersionHeader = (byte)~(a ^ b ^ c ^ d);
        }

        /// <summary>
        /// Saves a wz file to the disk, AKA repacking.
        /// </summary>
        /// <param name="path">Path to the output wz file</param>
        /// <param name="bSaveAs64BitWzFile">Save as WZ file for the 64-bit client</param>
        /// <param name="savingToPreferredWzVer"></param>
        public void SaveToDisk(string path, bool? bSaveAs64BitWzFile, WzMapleVersion savingToPreferredWzVer = WzMapleVersion.UNKNOWN)
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
            //

            CreateWZVersionHash();
            wzDir.SetVersionHash(versionHash);

            string tempFile = Path.GetFileNameWithoutExtension(path) + ".TEMP";
            File.Create(tempFile).Close();
            using (FileStream fs = new FileStream(tempFile, FileMode.Append, FileAccess.Write)) 
            {
                wzDir.GenerateDataFile(bIsWzIvSimilar ? null : WzIv, bIsWzUserKeyDefault, fs);
            }

            WzTool.StringCache.Clear();

            using (WzBinaryWriter wzWriter = new WzBinaryWriter(File.Create(path), WzIv, versionHash))
            {
                wzWriter.Hash = versionHash;

                uint totalLen = wzDir.GetImgOffsets(wzDir.GetOffsets(Header.FStart + (b64BitClient ? 0 : 2u)));
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

                if (bSaveAs64BitWzFile != null)
                {
                    if (bSaveAs64BitWzFile == false)
                        wzWriter.Write(wzVersionHeader);
                } else if (!b64BitClient)
                    wzWriter.Write(wzVersionHeader);

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

            GC.Collect();
            GC.WaitForPendingFinalizers();
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
                List<WzObject> fullList = new List<WzObject>
                {
                    WzDirectory
                };
                fullList.AddRange(GetObjectsFromDirectory(WzDirectory));
                return fullList;
            }
            else if (!path.Contains("*"))
                return new List<WzObject> { GetObjectFromPath(path) };
            string[] seperatedNames = path.Split("/".ToCharArray());
            if (seperatedNames.Length == 2 && seperatedNames[1] == "*")
                return GetObjectsFromDirectory(WzDirectory);
            List<WzObject> objList = new List<WzObject>();
            foreach (WzImage img in WzDirectory.WzImages)
                foreach (string spath in GetPathsFromImage(img, name + "/" + img.Name))
                    if (StringMatch(path, spath))
                        objList.Add(GetObjectFromPath(spath));
            foreach (WzDirectory dir in wzDir.WzDirectories)
                foreach (string spath in GetPathsFromDirectory(dir, name + "/" + dir.Name))
                    if (StringMatch(path, spath))
                        objList.Add(GetObjectFromPath(spath));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return objList;
        }

        public List<WzObject> GetObjectsFromRegexPath(string path)
        {
            if (path.ToLower() == name.ToLower())
                return new List<WzObject> { WzDirectory };
            List<WzObject> objList = new List<WzObject>();
            foreach (WzImage img in WzDirectory.WzImages)
                foreach (string spath in GetPathsFromImage(img, name + "/" + img.Name))
                    if (Regex.Match(spath, path).Success)
                        objList.Add(GetObjectFromPath(spath));
            foreach (WzDirectory dir in wzDir.WzDirectories)
                foreach (string spath in GetPathsFromDirectory(dir, name + "/" + dir.Name))
                    if (Regex.Match(spath, path).Success)
                        objList.Add(GetObjectFromPath(spath));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return objList;
        }

        public List<WzObject> GetObjectsFromDirectory(WzDirectory dir)
        {
            List<WzObject> objList = new List<WzObject>();
            foreach (WzImage img in dir.WzImages)
            {
                objList.Add(img);
                objList.AddRange(GetObjectsFromImage(img));
            }
            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                objList.Add(subdir);
                objList.AddRange(GetObjectsFromDirectory(subdir));
            }
            return objList;
        }

        public List<WzObject> GetObjectsFromImage(WzImage img)
        {
            List<WzObject> objList = new List<WzObject>();
            foreach (WzImageProperty prop in img.WzProperties)
            {
                objList.Add(prop);
                objList.AddRange(GetObjectsFromProperty(prop));
            }
            return objList;
        }

        public List<WzObject> GetObjectsFromProperty(WzImageProperty prop)
        {
            List<WzObject> objList = new List<WzObject>();
            switch (prop.PropertyType)
            {
                case WzPropertyType.Canvas:
                    foreach (WzImageProperty canvasProp in ((WzCanvasProperty)prop).WzProperties)
                        objList.AddRange(GetObjectsFromProperty(canvasProp));
                    objList.Add(((WzCanvasProperty)prop).PngProperty);
                    break;
                case WzPropertyType.Convex:
                    foreach (WzImageProperty exProp in ((WzConvexProperty)prop).WzProperties)
                        objList.AddRange(GetObjectsFromProperty(exProp));
                    break;
                case WzPropertyType.SubProperty:
                    foreach (WzImageProperty subProp in ((WzSubProperty)prop).WzProperties)
                        objList.AddRange(GetObjectsFromProperty(subProp));
                    break;
                case WzPropertyType.Vector:
                    objList.Add(((WzVectorProperty)prop).X);
                    objList.Add(((WzVectorProperty)prop).Y);
                    break;
            }
            return objList;
        }

        internal List<string> GetPathsFromDirectory(WzDirectory dir, string curPath)
        {
            List<string> objList = new List<string>();
            foreach (WzImage img in dir.WzImages)
            {
                objList.Add(curPath + "/" + img.Name);

                objList.AddRange(GetPathsFromImage(img, curPath + "/" + img.Name));
            }
            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                objList.Add(curPath + "/" + subdir.Name);
                objList.AddRange(GetPathsFromDirectory(subdir, curPath + "/" + subdir.Name));
            }
            return objList;
        }

        internal List<string> GetPathsFromImage(WzImage img, string curPath)
        {
            List<string> objList = new List<string>();
            foreach (WzImageProperty prop in img.WzProperties)
            {
                objList.Add(curPath + "/" + prop.Name);
                objList.AddRange(GetPathsFromProperty(prop, curPath + "/" + prop.Name));
            }
            return objList;
        }

        internal List<string> GetPathsFromProperty(WzImageProperty prop, string curPath)
        {
            List<string> objList = new List<string>();
            switch (prop.PropertyType)
            {
                case WzPropertyType.Canvas:
                    foreach (WzImageProperty canvasProp in ((WzCanvasProperty)prop).WzProperties)
                    {
                        objList.Add(curPath + "/" + canvasProp.Name);
                        objList.AddRange(GetPathsFromProperty(canvasProp, curPath + "/" + canvasProp.Name));
                    }
                    objList.Add(curPath + "/PNG");
                    break;
                case WzPropertyType.Convex:
                    foreach (WzImageProperty exProp in ((WzConvexProperty)prop).WzProperties)
                    {
                        objList.Add(curPath + "/" + exProp.Name);
                        objList.AddRange(GetPathsFromProperty(exProp, curPath + "/" + exProp.Name));
                    }
                    break;
                case WzPropertyType.SubProperty:
                    foreach (WzImageProperty subProp in ((WzSubProperty)prop).WzProperties)
                    {
                        objList.Add(curPath + "/" + subProp.Name);
                        objList.AddRange(GetPathsFromProperty(subProp, curPath + "/" + subProp.Name));
                    }
                    break;
                case WzPropertyType.Vector:
                    objList.Add(curPath + "/X");
                    objList.Add(curPath + "/Y");
                    break;
            }
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

            if (checkFirstDirectoryName)
            {
                if (seperatedPath[0].ToLower() != wzDir.name.ToLower() && seperatedPath[0].ToLower() != wzDir.name.Substring(0, wzDir.name.Length - 3).ToLower())
                    return null;
            }

            if (seperatedPath.Length == 1)
                return WzDirectory;
            WzObject curObj = WzDirectory;
            for (int i = 1; i < seperatedPath.Length; i++)
            {
                if (curObj == null)
                {
                    return null;
                }
                switch (curObj.ObjectType)
                {
                    case WzObjectType.Directory:
                        curObj = ((WzDirectory)curObj)[seperatedPath[i]];
                        continue;
                    case WzObjectType.Image:
                        curObj = ((WzImage)curObj)[seperatedPath[i]];
                        continue;
                    case WzObjectType.Property:
                        switch (((WzImageProperty)curObj).PropertyType)
                        {
                            case WzPropertyType.Canvas:
                                curObj = ((WzCanvasProperty)curObj)[seperatedPath[i]];
                                continue;
                            case WzPropertyType.Convex:
                                curObj = ((WzConvexProperty)curObj)[seperatedPath[i]];
                                continue;
                            case WzPropertyType.SubProperty:
                                curObj = ((WzSubProperty)curObj)[seperatedPath[i]];
                                continue;
                            case WzPropertyType.Vector:
                                if (seperatedPath[i] == "X")
                                    return ((WzVectorProperty)curObj).X;
                                else if (seperatedPath[i] == "Y")
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
            foreach (WzFile file in wzFiles)
            {
                WzObject obj = file.GetObjectFromPath(path, false);
                if (obj != null)
                    return obj;
            }
            return null;
        }

        internal bool StringMatch(string strWildCard, string strCompare)
        {
            if (strWildCard.Length == 0) return strCompare.Length == 0;
            if (strCompare.Length == 0) return false;
            if (strWildCard[0] == '*' && strWildCard.Length > 1)
                for (int index = 0; index < strCompare.Length; index++)
                {
                    if (StringMatch(strWildCard.Substring(1), strCompare.Substring(index)))
                        return true;
                }
            else if (strWildCard[0] == '*')
                return true;
            else if (strWildCard[0] == strCompare[0])
                return StringMatch(strWildCard.Substring(1), strCompare.Substring(1));
            return false;
        }

        public override void Remove()
        {
            Dispose();
        }
    }
}