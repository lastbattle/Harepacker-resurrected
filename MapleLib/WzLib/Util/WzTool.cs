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

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using MapleLib.Configuration;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;

namespace MapleLib.WzLib.Util
{
    public class WzTool
    {

        public static Hashtable StringCache = new Hashtable();

        public static UInt32 RotateLeft(UInt32 x, byte n)
        {
            return (UInt32)(((x) << (n)) | ((x) >> (32 - (n))));
        }

        public static UInt32 RotateRight(UInt32 x, byte n)
        {
            return (UInt32)(((x) >> (n)) | ((x) << (32 - (n))));
        }

        public static int GetCompressedIntLength(int i)
        {
            if (i > 127 || i < -127)
                return 5;
            return 1;
        }

        public static int GetEncodedStringLength(string s)
        {
            if (string.IsNullOrEmpty(s))
                return 1;

            bool unicode = false;
            int length = s.Length;

            foreach (char c in s) {
                if (c > 255) {
                    unicode = true;
                    break;
                }
            }
            int prefixLength = length > (unicode ? 126 : 127) ? 5 : 1;
            int encodedLength = unicode ? length * 2 : length;

            return prefixLength + encodedLength;
        }

        public static int GetWzObjectValueLength(string s, byte type)
        {
            string storeName = type + "_" + s;
            if (s.Length > 4 && StringCache.ContainsKey(storeName))
            {
                return 5;
            }
            else
            {
                StringCache[storeName] = 1;
                return 1 + GetEncodedStringLength(s);
            }
        }

        public static T StringToEnum<T>(string name)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), name);
            }
            catch
            {
                return default(T);
            }
        }

        /// <summary>
        /// Get WZ encryption IV from maple version 
        /// </summary>
        /// <param name="ver"></param>
        /// <param name="fallbackCustomIv">The custom bytes to use as IV</param>
        /// <returns></returns>
        public static byte[] GetIvByMapleVersion(WzMapleVersion ver)
        {
            switch (ver)
            {
                case WzMapleVersion.EMS:
                    return MapleCryptoConstants.WZ_MSEAIV;//?
                case WzMapleVersion.GMS:
                    return MapleCryptoConstants.WZ_GMSIV;
                case WzMapleVersion.CUSTOM: // custom WZ encryption bytes from stored app setting
                    {
                        ConfigurationManager config = new ConfigurationManager();
                        return config.GetCusomWzIVEncryption(); // fallback with BMS
                    }
                case WzMapleVersion.GENERATE: // dont fill anything with GENERATE, it is not supposed to load anything
                    return new byte[4]; 

                case WzMapleVersion.BMS:
                case WzMapleVersion.CLASSIC:
                default:
                    return new byte[4];
            }
        }

        private static int GetRecognizedCharacters(string source) {
            return source.Count(c => c >= 0x20 && c <= 0x7E);
        }

        /// <summary>
        /// Attempts to bruteforce the WzKey with a given WZ file
        /// </summary>
        /// <param name="wzPath"></param>
        /// <param name="wzIvKey"></param>
        /// <returns>The probability. Normalized to 100</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryBruteforcingWzIVKey(string wzPath, byte[] wzIvKey)
        {
            using (WzFile wzf = new WzFile(wzPath, wzIvKey))
            {
                string parseErrorMessage = string.Empty;
                WzFileParseStatus parseStatus = wzf.ParseMainWzDirectory(true);
                if (parseStatus != WzFileParseStatus.Success)
                {
                    wzf.Dispose();
                    return false;
                }
                if (wzf.WzDirectory.WzImages.Count > 0 && wzf.WzDirectory.WzImages[0].Name.EndsWith(".img"))
                {
                    wzf.Dispose();
                    return true;
                }

                wzf.Dispose();
            }
            return false;
        }

        private static double GetDecryptionSuccessRate(string wzPath, WzMapleVersion encVersion, ref short? version)
        {
            WzFile wzf;
            if (version == null)
                wzf = new WzFile(wzPath, encVersion);
            else
                wzf = new WzFile(wzPath, (short)version, encVersion);

            WzFileParseStatus parseStatus = wzf.ParseWzFile();
            if (parseStatus != WzFileParseStatus.Success)
            {
                return 0.0d;
            }

            if (version == null) version = wzf.Version;
            int recognizedChars = 0;
            int totalChars = 0;
            foreach (WzDirectory wzdir in wzf.WzDirectory.WzDirectories)
            {
                recognizedChars += GetRecognizedCharacters(wzdir.Name);
                totalChars += wzdir.Name.Length;
            }
            foreach (WzImage wzimg in wzf.WzDirectory.WzImages)
            {
                recognizedChars += GetRecognizedCharacters(wzimg.Name);
                totalChars += wzimg.Name.Length;
            }
            wzf.Dispose();
            return (double)recognizedChars / (double)totalChars;
        }

        public static WzMapleVersion DetectMapleVersion(string wzFilePath, out short fileVersion)
        {
            Hashtable mapleVersionSuccessRates = new Hashtable();
            short? version = null;
            mapleVersionSuccessRates.Add(WzMapleVersion.GMS, GetDecryptionSuccessRate(wzFilePath, WzMapleVersion.GMS, ref version));
            mapleVersionSuccessRates.Add(WzMapleVersion.EMS, GetDecryptionSuccessRate(wzFilePath, WzMapleVersion.EMS, ref version));
            mapleVersionSuccessRates.Add(WzMapleVersion.BMS, GetDecryptionSuccessRate(wzFilePath, WzMapleVersion.BMS, ref version));
            fileVersion = (short)version;
            WzMapleVersion mostSuitableVersion = WzMapleVersion.GMS;
            double maxSuccessRate = 0;

            foreach (DictionaryEntry mapleVersionEntry in mapleVersionSuccessRates)
            {
                if ((double)mapleVersionEntry.Value > maxSuccessRate)
                {
                    mostSuitableVersion = (WzMapleVersion)mapleVersionEntry.Key;
                    maxSuccessRate = (double)mapleVersionEntry.Value;
                }
            }
            if (maxSuccessRate < 0.7 && File.Exists(Path.Combine(Path.GetDirectoryName(wzFilePath), "ZLZ.dll")))
                return WzMapleVersion.GETFROMZLZ;
            else return mostSuitableVersion;
        }

        public const int WzHeader = 0x31474B50; //PKG1

        public static bool IsListFile(string path)
        {
            bool result;
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                int header = reader.ReadInt32();
                result = header != WzHeader;
            }
            return result;
        }

        /// <summary>
        /// Checks if the input file is Data.wz hotfix file [not to be mistaken for Data.wz for pre v4x!]
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsDataWzHotfixFile(string path)
        {
            bool result = false;
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                byte firstByte = reader.ReadByte();

                result = firstByte == WzImage.WzImageHeaderByte_WithoutOffset; // check the first byte. It should be 0x73 that represends a WzImage
            }

            return result;
        }

        private static byte[] Combine(byte[] a, byte[] b)
        {
            byte[] result = new byte[a.Length + b.Length];
            Array.Copy(a, 0, result, 0, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }
}