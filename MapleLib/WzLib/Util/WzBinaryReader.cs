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
using System.Diagnostics;
using System.IO;
using System.Text;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;

namespace MapleLib.WzLib.Util
{
    public class WzBinaryReader : BinaryReader
    {
        #region Properties
        public WzMutableKey WzKey { get; set; }
        public uint Hash { get; set; }
        public WzHeader Header { get; set; }
        #endregion

        #region Constructors
        public WzBinaryReader(Stream input, byte[] WzIv)
            : base(input)
        {
            WzKey = WzKeyGenerator.GenerateWzKey(WzIv);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Sets the base stream position to the header FStart + offset
        /// </summary>
        /// <param name="offset"></param>
        public void SetOffsetFromFStartToPosition(int offset)
        {
            BaseStream.Position = Header.FStart + offset;
        }

        public void RollbackStreamPosition(int byOffset)
        {
            if (BaseStream.Position < byOffset)
                throw new Exception("Cant rollback stream position below 0");

            BaseStream.Position -= byOffset;
        }

        public string ReadStringAtOffset(long Offset)
        {
            return ReadStringAtOffset(Offset, false);
        }

        public string ReadStringAtOffset(long Offset, bool readByte)
        {
            long CurrentOffset = BaseStream.Position;
            BaseStream.Position = Offset;
            if (readByte)
            {
                ReadByte();
            }
            string ReturnString = ReadString();
            BaseStream.Position = CurrentOffset;
            return ReturnString;
        }

        public override string ReadString()
        {
            sbyte smallLength = base.ReadSByte();

            if (smallLength == 0)
            {
                return string.Empty;
            }

            int length;
            StringBuilder retString = new StringBuilder();
            if (smallLength > 0) // Unicode
            {
                ushort mask = 0xAAAA;
                if (smallLength == sbyte.MaxValue)
                {
                    length = ReadInt32();
                }
                else
                {
                    length = (int)smallLength;
                }
                if (length <= 0)
                {
                    return string.Empty;
                }
                
                for (int i = 0; i < length; i++)
                {
                    ushort encryptedChar = ReadUInt16();
                    encryptedChar ^= mask;
                    encryptedChar ^= (ushort)((WzKey[(i * 2 + 1)] << 8) + WzKey[(i * 2)]);
                    retString.Append((char)encryptedChar);
                    mask++;
                }
            }
            else
            { // ASCII
                byte mask = 0xAA;
                if (smallLength == sbyte.MinValue)
                {
                    length = ReadInt32();
                }
                else
                {
                    length = (int)(-smallLength);
                }
                if (length <= 0)
                {
                    return string.Empty;
                }

                for (int i = 0; i < length; i++)
                {
                    byte encryptedChar = ReadByte();
                    encryptedChar ^= mask;
                    encryptedChar ^= (byte)WzKey[i];
                    retString.Append((char)encryptedChar);
                    mask++;
                }
            }
            return retString.ToString();
        }

        /// <summary>
        /// Reads an ASCII string, without decryption
        /// </summary>
        /// <param name="filePath">Length of bytes to read</param>
        public string ReadString(int length)
        {
            return Encoding.ASCII.GetString(ReadBytes(length));
        }

        public string ReadNullTerminatedString()
        {
            StringBuilder retString = new StringBuilder();
            byte b = ReadByte();
            while (b != 0)
            {
                retString.Append((char)b);
                b = ReadByte();
            }
            return retString.ToString();
        }

        public int ReadCompressedInt()
        {
            sbyte sb = base.ReadSByte();
            if (sb == sbyte.MinValue)
            {
                return ReadInt32();
            }
            return sb;
        }

        public long ReadLong()
        {
            sbyte sb = base.ReadSByte();
            if (sb == sbyte.MinValue)
            {
                return ReadInt64();
            }
            return sb;
        }

        /// <summary>
        /// The amount of bytes available remaining in the stream
        /// </summary>
        /// <returns></returns>
        public long Available()
        {
            return BaseStream.Length - BaseStream.Position;
        }

        public uint ReadOffset()
        {
            uint offset = (uint)BaseStream.Position;
            offset = (offset - Header.FStart) ^ uint.MaxValue;
            offset *= Hash;
            offset -= MapleCryptoConstants.WZ_OffsetConstant;
            offset = WzTool.RotateLeft(offset, (byte)(offset & 0x1F));
            uint encryptedOffset = ReadUInt32();
            offset ^= encryptedOffset;
            offset += Header.FStart * 2;
            return offset;
        }

        public string DecryptString(char[] stringToDecrypt)
        {
            StringBuilder outputString = new StringBuilder();

            int i = 0;
            foreach (char c in stringToDecrypt)
            {
                outputString.Append((char)(c ^ ((char)((WzKey[i * 2 + 1] << 8) + WzKey[i * 2]))));
                i++;
            }
            return outputString.ToString();
        }


        public string DecryptNonUnicodeString(char[] stringToDecrypt)
        {
            // Initialize the output string with the correct capacity
            StringBuilder outputString = new StringBuilder(stringToDecrypt.Length);

            for (int i = 0; i < stringToDecrypt.Length; i++)
            {
                // Append the decrypted character to the StringBuilder object
                outputString.Append((char)(stringToDecrypt[i] ^ WzKey[i]));
            }

            // Convert the StringBuilder object to a string and return it
            return outputString.ToString();
        }

        public string ReadStringBlock(uint offset)
        {
            switch (ReadByte())
            {
                case 0:
                case WzImage.WzImageHeaderByte_WithoutOffset:
                    return ReadString();
                case 1:
                case WzImage.WzImageHeaderByte_WithOffset:
                    return ReadStringAtOffset(offset + ReadInt32());
                default:
                    return "";
            }
        }

        #endregion

        #region Debugging Methods
        /// <summary>
        /// Prints the next numberOfBytes in the stream in the system debug console.
        /// </summary>
        /// <param name="numberOfBytes"></param>
        public void PrintHexBytes(int numberOfBytes)
        {
#if DEBUG // only debug
            string hex = HexTool.ToString(ReadBytes(numberOfBytes));
            Debug.WriteLine(hex);

            this.BaseStream.Position -= numberOfBytes;
#endif
        }
#endregion

#region Overrides
        public override void Close()
        {
            // debug here
            base.Close();
        }
#endregion
    }
}