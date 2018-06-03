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

using System.IO;
using System;
using MapleLib.WzLib.Util;
using NAudio.Wave;
using MapleLib.Helpers;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property that contains data for an MP3 file
    /// </summary>
    public class WzSoundProperty : WzExtended
    {
        #region Fields
        internal string name;
        internal byte[] mp3bytes = null;
        internal WzObject parent;
        internal int len_ms;
        internal byte[] header;
        //internal WzImage imgParent;
        internal WzBinaryReader wzReader;
        internal bool headerEncrypted = false;
        internal long offs;
        internal int soundDataLen;
        public static readonly byte[] soundHeader = new byte[] {
            0x02,
            0x83, 0xEB, 0x36, 0xE4, 0x4F, 0x52, 0xCE, 0x11, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70,
            0x8B, 0xEB, 0x36, 0xE4, 0x4F, 0x52, 0xCE, 0x11, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70,
            0x00,
            0x01,
            0x81, 0x9F, 0x58, 0x05, 0x56, 0xC3, 0xCE, 0x11, 0xBF, 0x01, 0x00, 0xAA, 0x00, 0x55, 0x59, 0x5A };

        internal WaveFormat wavFormat;
        #endregion

        #region Inherited Members

        public override WzImageProperty DeepClone()
        {
            WzSoundProperty clone = new WzSoundProperty(this);
            return clone;
        }

        public override object WzValue { get { return GetBytes(false); } }

        public override void SetValue(object value)
        {
            return;
        }
        /// <summary>
        /// The parent of the object
        /// </summary>
        public override WzObject Parent { get { return parent; } internal set { parent = value; } }
        /*/// <summary>
		/// The image that this property is contained in
		/// </summary>
		public override WzImage ParentImage { get { return imgParent; } internal set { imgParent = value; } }*/
        /// <summary>
        /// The name of the property
        /// </summary>
        public override string Name { get { return name; } set { name = value; } }
        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType { get { return WzPropertyType.Sound; } }
        public override void WriteValue(WzBinaryWriter writer)
        {
            byte[] data = GetBytes(false);
            writer.WriteStringValue("Sound_DX8", 0x73, 0x1B);
            writer.Write((byte)0);
            writer.WriteCompressedInt(data.Length);
            writer.WriteCompressedInt(len_ms);
            writer.Write(header);
            writer.Write(data);
        }
        public override void ExportXml(StreamWriter writer, int level)
        {
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.EmptyNamedTag("WzSound", this.Name));
        }
        /// <summary>
        /// Disposes the object
        /// </summary>
        public override void Dispose()
        {
            name = null;
            mp3bytes = null;
        }
        #endregion

        #region Custom Members
        /// <summary>
        /// The data of the mp3 header
        /// </summary>
        public byte[] Header { get { return header; } set { header = value; } }
        /// <summary>
        /// Length of the mp3 file in milliseconds
        /// </summary>
        public int Length { get { return len_ms; } set { len_ms = value; } }
        /// <summary>
        /// Frequency of the mp3 file in Hz
        /// </summary>
        public int Frequency
        {
            get { return wavFormat != null ? wavFormat.SampleRate : 0; }
        }
        /// <summary>
        /// BPS of the mp3 file
        /// </summary>
        //public byte BPS { get { return bps; } set { bps = value; } }
        /// <summary>
        /// Creates a WzSoundProperty with the specified name
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <param name="reader">The wz reader</param>
        /// <param name="parseNow">Indicating whether to parse the property now</param>
        public WzSoundProperty(string name, WzBinaryReader reader, bool parseNow)
        {
            this.name = name;
            wzReader = reader;
            reader.BaseStream.Position++;

            //note - soundDataLen does NOT include the length of the header.
            soundDataLen = reader.ReadCompressedInt();
            len_ms = reader.ReadCompressedInt();

            long headerOff = reader.BaseStream.Position;
            reader.BaseStream.Position += soundHeader.Length; //skip GUIDs
            int wavFormatLen = reader.ReadByte();
            reader.BaseStream.Position = headerOff;

            header = reader.ReadBytes(soundHeader.Length + 1 + wavFormatLen);
            ParseWzSoundPropertyHeader();

            //sound file offs
            offs = reader.BaseStream.Position;
            if (parseNow)
                mp3bytes = reader.ReadBytes(soundDataLen);
            else
                reader.BaseStream.Position += soundDataLen;
        }

        /*public WzSoundProperty(string name)
        {
            this.name = name;
            this.len_ms = 0;
            this.header = null;
            this.mp3bytes = null;
        }*/

        /// <summary>
        /// Creates a WzSoundProperty with the specified name and data from another WzSoundProperty Object
        /// </summary>
        /// <param name="name"></param>
        /// <param name="wavFormat"></param>
        /// <param name="len_ms"></param>
        /// <param name="soundDataLen"></param>
        /// <param name="headerClone"></param>
        /// <param name="data"></param>
        /// <param name="headerEncrypted"></param>
        public WzSoundProperty(WzSoundProperty otherProperty)
        {
            this.name = otherProperty.name;
            this.wavFormat = otherProperty.wavFormat;
            this.len_ms = otherProperty.len_ms;
            this.soundDataLen = otherProperty.soundDataLen;
            this.offs = otherProperty.offs;

            if (otherProperty.header == null) // not initialized yet
            {
                otherProperty.ParseWzSoundPropertyHeader();
            }
            this.header = new byte[otherProperty.header.Length];
            Array.Copy(otherProperty.header, this.header, otherProperty.header.Length);

            if (otherProperty.mp3bytes == null)
                this.mp3bytes = otherProperty.GetBytes(false);
            else
            {
                this.mp3bytes = new byte[otherProperty.mp3bytes.Length];
                Array.Copy(otherProperty.mp3bytes, mp3bytes, otherProperty.mp3bytes.Length);
            }
            this.headerEncrypted = otherProperty.headerEncrypted;
        }

        /// <summary>
        /// Creates a WzSoundProperty with the specified name and data
        /// </summary>
        /// <param name="name"></param>
        /// <param name="len_ms"></param>
        /// <param name="headerClone"></param>
        /// <param name="data"></param>
        public WzSoundProperty(string name, int len_ms, byte[] headerClone, byte[] data)
        {
            this.name = name;
            this.len_ms = len_ms;

            this.header = new byte[headerClone.Length];
            Array.Copy(headerClone, this.header, headerClone.Length);

            this.mp3bytes = new byte[data.Length];
            Array.Copy(data, mp3bytes, data.Length);

            ParseWzSoundPropertyHeader();
        }

        /// <summary>
        /// Creates a WzSoundProperty with the specified name from a file
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <param name="file">The path to the sound file</param>
        public WzSoundProperty(string name, string file)
        {
            this.name = name;
            Mp3FileReader reader = new Mp3FileReader(file);
            this.wavFormat = reader.Mp3WaveFormat;
            this.len_ms = (int)((double)reader.Length * 1000d / (double)reader.WaveFormat.AverageBytesPerSecond);
            RebuildHeader();
            reader.Dispose();
            this.mp3bytes = File.ReadAllBytes(file);
        }

        public static bool memcmp(byte[] a, byte[] b, int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 3);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2} ", b);
            return hex.ToString();
        }

        public void RebuildHeader()
        {
            using (BinaryWriter bw = new BinaryWriter(new MemoryStream()))
            {
                bw.Write(soundHeader);
                byte[] wavHeader = StructToBytes(wavFormat);
                if (headerEncrypted)
                {
                    for (int i = 0; i < wavHeader.Length; i++)
                    {
                        wavHeader[i] ^= this.wzReader.WzKey[i];
                    }
                }
                bw.Write((byte)wavHeader.Length);
                bw.Write(wavHeader, 0, wavHeader.Length);
                header = ((MemoryStream)bw.BaseStream).ToArray();
            }
        }

        private static byte[] StructToBytes<T>(T obj)
        {
            byte[] result = new byte[Marshal.SizeOf(obj)];
            GCHandle handle = GCHandle.Alloc(result, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(obj, handle.AddrOfPinnedObject(), false);
                return result;
            }
            finally
            {
                handle.Free();
            }
        }

        private static T BytesToStruct<T>(byte[] data) where T : new()
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        private static T BytesToStructConstructorless<T>(byte[] data)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                T obj = (T)FormatterServices.GetUninitializedObject(typeof(T));
                Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject(), obj);
                return obj;
            }
            finally
            {
                handle.Free();
            }
        }

        private void ParseWzSoundPropertyHeader()
        {
            byte[] wavHeader = new byte[header.Length - soundHeader.Length - 1];
            Buffer.BlockCopy(header, soundHeader.Length + 1, wavHeader, 0, wavHeader.Length);

            if (wavHeader.Length < Marshal.SizeOf<WaveFormat>())
                return;

            WaveFormat wavFmt = BytesToStruct<WaveFormat>(wavHeader);

            if (Marshal.SizeOf<WaveFormat>() + wavFmt.ExtraSize != wavHeader.Length)
            {
                //try decrypt
                for (int i = 0; i < wavHeader.Length; i++)
                {
                    wavHeader[i] ^= this.wzReader.WzKey[i];
                }
                wavFmt = BytesToStruct<WaveFormat>(wavHeader);

                if (Marshal.SizeOf<WaveFormat>() + wavFmt.ExtraSize != wavHeader.Length)
                {
                    ErrorLogger.Log(ErrorLevel.Critical, "parse sound header failed");
                    return;
                }
                headerEncrypted = true;
            }

            // parse to mp3 header
            if (wavFmt.Encoding == WaveFormatEncoding.MpegLayer3 && wavHeader.Length >= Marshal.SizeOf<Mp3WaveFormat>())
            {
                this.wavFormat = BytesToStructConstructorless<Mp3WaveFormat>(wavHeader);
            }
            else if (wavFmt.Encoding == WaveFormatEncoding.Pcm)
            {
                this.wavFormat = wavFmt;
            }
            else
            {
                ErrorLogger.Log(ErrorLevel.MissingFeature, string.Format("Unknown wave encoding {0}", wavFmt.Encoding.ToString()));
            }
        }
        #endregion

        #region Parsing Methods
        public byte[] GetBytes(bool saveInMemory)
        {
            if (mp3bytes != null)
                return mp3bytes;
            else
            {
                if (wzReader == null) return null;
                long currentPos = wzReader.BaseStream.Position;
                wzReader.BaseStream.Position = offs;
                mp3bytes = wzReader.ReadBytes(soundDataLen);
                wzReader.BaseStream.Position = currentPos;
                if (saveInMemory)
                    return mp3bytes;
                else
                {
                    byte[] result = mp3bytes;
                    mp3bytes = null;
                    return result;
                }
            }
        }

        public void SaveToFile(string file)
        {
            File.WriteAllBytes(file, GetBytes(false));
        }
        #endregion

        #region Cast Values
        public override byte[] GetBytes()
        {
            return GetBytes(false);
        }
        #endregion
    }
}