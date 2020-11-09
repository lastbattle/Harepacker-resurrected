/* Copyright (C) 2020 LastBattle
* https://github.com/lastbattle/Harepacker-resurrected
* 
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */


using MapleLib.MapleCryptoLib;
using MapleLib.WzLib.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property with a .lua file
    /// </summary>
    public class WzLuaProperty : WzImageProperty
    {
        #region Fields
        internal string name;
        internal byte[] encryptedBytes;
        internal WzObject parent;
        internal WzMutableKey WzKey;
        //internal WzImage imgParent;
        #endregion

        #region Inherited Members
        public override void SetValue(object value)
        {
            encryptedBytes = (byte[])value;
        }

        public override WzImageProperty DeepClone()
        {
            byte[] newArray = new byte[encryptedBytes.Length];
            Buffer.BlockCopy(encryptedBytes, 0, newArray, 0, encryptedBytes.Length);

            WzLuaProperty clone = new WzLuaProperty(name, newArray);
            return clone;
        }

        /// <summary>
        /// The encrypted value of the WzLuaProperty
        /// </summary>
        public override object WzValue { get { return Value; } }
        /// <summary>
        /// The parent of the object
        /// </summary>
        public override WzObject Parent { get { return parent; } internal set { parent = value; } }
        /*/// <summary>
        /// The image that this property is contained in
        /// </summary>
        public override WzImage ParentImage { get { return imgParent; } internal set { imgParent = value; } }*/
        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType { get { return WzPropertyType.Lua; } }
        /// <summary>
        /// The name of the property
        /// </summary>
        public override string Name { get { return name; } set { name = value; } }
        public override void WriteValue(MapleLib.WzLib.Util.WzBinaryWriter writer)
        {
            writer.Write((byte) 0x1);
            writer.WriteCompressedInt(encryptedBytes.Length);
            writer.Write(encryptedBytes);
        }
        public override void ExportXml(StreamWriter writer, int level)
        {
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.EmptyNamedValuePair("WzLua", this.Name, GetString()));
        }

        /// <summary>
        /// Disposes the object
        /// </summary>
        public override void Dispose()
        {
            name = null;
            encryptedBytes = null;
        }
        #endregion

        #region Custom Members
        /// <summary>
        /// The encrypted value of the .lua property
        /// </summary>
        public byte[] Value { get { return encryptedBytes; } set { encryptedBytes = value; } }

        /// <summary>
        /// Creates a WzStringProperty with the specified name and value
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <param name="value">The value of the property</param>
        public WzLuaProperty(string name, byte[] encryptedBytes)
        {
            this.name = name;
            this.encryptedBytes = encryptedBytes;
            this.WzKey = WzKeyGenerator.GenerateLuaWzKey();
        }
        #endregion

        #region Cast Values
        public override string GetString()
        {
            byte[] decodedBytes = EncodeDecode(encryptedBytes);
            string decoded = Encoding.ASCII.GetString(decodedBytes);

            return decoded;
        }

        public override string ToString()
        {
            return GetString();
        }
        #endregion

        #region Encoder Decoder
        /// <summary>
        /// Encodes or decoded a selected chunk of bytes with the xor encryption used with lua property
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public byte[] EncodeDecode(byte[] input)
        {
            byte[] newArray = new byte[input.Length];

            for (int i = 0; i < input.Length; i++)
            {
                byte encryptedChar = (byte)(input[i] ^ WzKey[i]);
                newArray[i] = encryptedChar;
            }
            return newArray;
        }
        #endregion
    }
}