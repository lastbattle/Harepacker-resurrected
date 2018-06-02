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
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;

namespace MapleLib.WzLib
{
	/// <summary>
	/// A class that parses and contains the data of a wz list file
	/// </summary>
	public static class ListFileParser
	{
        /// <summary>
		/// Parses a wz list file on the disk
		/// </summary>
		/// <param name="filePath">Path to the wz file</param>
        public static List<string> ParseListFile(string filePath, WzMapleVersion version)
        {
            return ParseListFile(filePath, WzTool.GetIvByMapleVersion(version));
        }

        /// <summary>
		/// Parses a wz list file on the disk
		/// </summary>
		/// <param name="filePath">Path to the wz file</param>
        public static List<string> ParseListFile(string filePath, byte[] WzIv)
        {
            List<string> listEntries = new List<string>();
            byte[] wzFileBytes = File.ReadAllBytes(filePath);
            WzBinaryReader wzParser = new WzBinaryReader(new MemoryStream(wzFileBytes), WzIv);
            while (wzParser.PeekChar() != -1)
            {
                int len = wzParser.ReadInt32();
                char[] strChrs = new char[len];
                for (int i = 0; i < len; i++)
                    strChrs[i] = (char)wzParser.ReadInt16();
                wzParser.ReadUInt16(); //encrypted null
                string decryptedStr = wzParser.DecryptString(strChrs);
                listEntries.Add(decryptedStr);
            }
            wzParser.Close();
            int lastIndex= listEntries.Count - 1;
            string lastEntry = listEntries[lastIndex];
            listEntries[lastIndex] = lastEntry.Substring(0, lastEntry.Length - 1) + "g";
            return listEntries;
        }

        public static void SaveToDisk(string path, WzMapleVersion version, List<string> listEntries)
        {
            SaveToDisk(path, WzTool.GetIvByMapleVersion(version), listEntries);
        }

		public static void SaveToDisk(string path, byte[] WzIv, List<string> listEntries)
		{
            int lastIndex = listEntries.Count - 1;
            string lastEntry = listEntries[lastIndex];
            listEntries[lastIndex] = lastEntry.Substring(0, lastEntry.Length - 1) + "/";
            WzBinaryWriter wzWriter = new WzBinaryWriter(File.Create(path), WzIv);
            string s;
            for (int i = 0; i < listEntries.Count; i++)
            {
                s = listEntries[i];
                wzWriter.Write((int)s.Length);
                char[] encryptedChars = wzWriter.EncryptString(s + (char)0);
                for (int j = 0; j < encryptedChars.Length; j++)
                    wzWriter.Write((short)encryptedChars[j]);
            }
            listEntries[lastIndex] = lastEntry.Substring(0, lastEntry.Length - 1) + "/";
		}
    }
}