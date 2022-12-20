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
using System;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System.Linq;

namespace MapleLib.WzLib
{
    /// <summary>
    /// A .img contained in a wz directory
    /// </summary>
    public class WzImage : WzObject, IPropertyContainer
    {
        //TODO: nest wzproperties in a wzsubproperty inside of WzImage

        /// <summary>
        /// bExistID_0x73
        /// </summary>
        public const int WzImageHeaderByte_WithoutOffset = 0x73;
        /// <summary>
        /// bNewID_0x1b
        /// </summary>
        public const int WzImageHeaderByte_WithOffset = 0x1B;

        #region Fields
        internal bool parsed = false;
        internal string name;
        internal int size;
        private int checksum;
        internal uint offset = 0;
        internal WzBinaryReader reader;
        internal List<WzImageProperty> properties = new List<WzImageProperty>();
        internal WzObject parent;
        internal int blockStart = 0;
        internal long tempFileStart = 0;
        internal long tempFileEnd = 0;
        internal bool bIsImageChanged = false;
        private bool parseEverything = false;

        /// <summary>
        /// Wz image embedding .lua file.
        /// </summary>
        public bool IsLuaWzImage
        {
            get { return Name.EndsWith(".lua"); } // TODO: find some ways to avoid user from adding a new image with .lua name
        }
        #endregion

        #region Constructors\Destructors
        /// <summary>
        /// Creates a blank WzImage
        /// </summary>
        public WzImage() { }
        /// <summary>
        /// Creates a WzImage with the given name
        /// </summary>
        /// <param name="name">The name of the image</param>
        public WzImage(string name)
        {
            this.name = name;
        }
        public WzImage(string name, Stream dataStream, WzMapleVersion mapleVersion)
        {
            this.name = name;
            this.reader = new WzBinaryReader(dataStream, WzTool.GetIvByMapleVersion(mapleVersion));
        }
        internal WzImage(string name, WzBinaryReader reader)
        {
            this.name = name;
            this.reader = reader;
            this.blockStart = (int)reader.BaseStream.Position;
            this.checksum = 0;
        }

        /// <summary>
        /// WzImage Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="reader"></param>
        /// <param name="checksum"></param>
        /// <param name="unk_GMS230"></param>
        internal WzImage(string name, WzBinaryReader reader, int checksum)
        {
            this.name = name;
            this.reader = reader;
            this.blockStart = (int)reader.BaseStream.Position;
            this.checksum = checksum;
        }

        public override void Dispose()
        {
            name = null;
            if (properties != null)
            {
                foreach (WzImageProperty prop in properties)
                {
                    prop.Dispose();
                }
                properties.Clear();
                properties = null;
            }
            if (reader != null)
            {
                reader.Close();
                reader.Dispose();
                reader = null;
            }
        }
        #endregion

        #region Inherited Members
        /// <summary>
		/// The parent of the object
		/// </summary>
		public override WzObject Parent { get { return parent; } internal set { parent = value; } }

        /// <summary>
        /// The name of the image
        /// </summary>
        public override string Name { get { return name; } set { name = value; } }
        public override WzFile WzFileParent { get { return Parent != null ? Parent.WzFileParent : null; } }

        /// <summary>
        /// Is the object parsed
        /// </summary>
        public bool Parsed { get { return parsed; } set { parsed = value; } }

        /// <summary>
        /// Set the property if the image should be fully parsed
        /// </summary>
        public bool ParseEverything { get { return parseEverything; } set { this.parseEverything = value; } } 

        /// <summary>
        /// Was the image changed
        /// </summary>
        public bool Changed { get { return bIsImageChanged; } set { bIsImageChanged = value; } }
        /// <summary>
        /// The size in the wz file of the image
        /// </summary>
        public int BlockSize { get { return size; } set { size = value; } }
        /// <summary>
        /// The checksum of the image
        /// </summary>
        public int Checksum { 
            get { return this.checksum; }
            private set {  } 
        }
        /// <summary>
        /// The offset of the start of this image
        /// </summary>
        public uint Offset { get { return offset; } set { offset = value; } }
        public int BlockStart { get { return blockStart; } }
        /// <summary>
        /// The WzObjectType of the image
        /// </summary>
        public override WzObjectType ObjectType
        {
            get
            {
                if (reader != null)
                    if (!parsed)
                        ParseImage();
                return WzObjectType.Image;
            }
        }

        /// <summary>
        /// The properties contained in the image
        /// </summary>
        public List<WzImageProperty> WzProperties
        {
            get
            {
                if (reader != null && !parsed)
                {
                    ParseImage();
                }
                return properties;
            }
        }

        public WzImage DeepClone()
        {
            if (reader != null && !parsed) ParseImage();
            WzImage clone = new WzImage(name)
            {
                bIsImageChanged = true
            };
            foreach (WzImageProperty prop in properties)
                clone.AddProperty(prop.DeepClone());
            return clone;
        }

        /// <summary>
        /// Gets a wz property by it's name
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <returns>The wz property with the specified name</returns>
        public new WzImageProperty this[string name]
        {
            get
            {
                if (reader != null) 
                    if (!parsed) 
                        ParseImage();
                
                // Find the first WzImageProperty with a matching name (case-insensitive)
                return properties.FirstOrDefault(iwp => iwp.Name.ToLower() == name.ToLower());
            }
            set
            {
                if (value != null)
                {
                    value.Name = name;
                    AddProperty(value);
                }
            }
        }
        #endregion 

        #region Custom Members
        /// <summary>
		/// Gets a WzImageProperty from a path
		/// </summary>
		/// <param name="path">path to object</param>
		/// <returns>the selected WzImageProperty</returns>
		public WzImageProperty GetFromPath(string path)
        {
            if (reader != null) if (!parsed) ParseImage();

            string[] segments = path.Split(new char[1] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);

            // If the first segment is "..", return null
            if (segments[0] == "..")
                return null;

            WzImageProperty ret = null;

            foreach (string segment in segments)
            {
                // Check if the current property has a child with the matching name
                ret = (ret == null ? this.properties : ret.WzProperties)
                    .FirstOrDefault(iwp => iwp.Name == segment);

                // If no matching child was found, return null
                if (ret == null)
                    return null;
            }

            return ret;
        }

        /// <summary>
        /// Adds a property to the WzImage
        /// </summary>
        /// <param name="prop">Property to add</param>
        public void AddProperty(WzImageProperty prop)
        {
            prop.Parent = this;
            if (reader != null && !parsed) 
                ParseImage();
            properties.Add(prop);
        }
        /// <summary>
        /// Add a list of properties to the WzImage
        /// </summary>
        /// <param name="props"></param>
        public void AddProperties(List<WzImageProperty> props)
        {
            foreach (WzImageProperty prop in props)
            {
                AddProperty(prop);
            }
        }
        /// <summary>
        /// Removes a property by name
        /// </summary>
        /// <param name="name">The name of the property to remove</param>
        public void RemoveProperty(WzImageProperty prop)
        {
            if (reader != null && !parsed) 
                ParseImage();
            prop.Parent = null;
            properties.Remove(prop);
        }
        public void ClearProperties()
        {
            foreach (WzImageProperty prop in properties) 
                prop.Parent = null;
            properties.Clear();
        }

        public override void Remove()
        {
            if (Parent != null)
            {
                ((WzDirectory)Parent).RemoveImage(this);
            }
        }
        #endregion

        #region Parsing Methods
        /// <summary>
        /// Calculates and set the image header checksum
        /// </summary>
        /// <param name="memStream"></param>
        internal void CalculateAndSetImageChecksum(byte[] bytes)
        {
            this.checksum = 0;
            foreach (byte b in bytes)
            {
                this.checksum += b;
            }
        }

        /// <summary>
		/// Parses the image from the wz filetod
		/// </summary>
		/// <param name="wzReader">The BinaryReader that is currently reading the wz file</param>
        /// <returns>bool Parse status</returns>
        public bool ParseImage(bool forceReadFromData = false)
        {
            if (!forceReadFromData) { // only check if parsed or changed if its not false read
                if (Parsed)
                {
                    return true;
                }
                else if (Changed)
                {
                    Parsed = true;
                    return true;
                }
            }

            lock (reader) // for multi threaded XMLWZ export. 
            {
                long originalPos = reader.BaseStream.Position;
                reader.BaseStream.Position = offset;

                byte b = reader.ReadByte();
                switch (b)
                {
                    case 0x1: // .lua   
                        {
                            if (IsLuaWzImage) 
                            {
                                WzLuaProperty lua = WzImageProperty.ParseLuaProperty(offset, reader, this, this);
                                List<WzImageProperty> luaImage = new List<WzImageProperty>
                                {
                                    lua
                                };
                                properties.AddRange(luaImage);
                                parsed = true; // test
                                return true;
                            }
                            return false; // unhandled for now, if it isnt an .lua image
                        }
                    case WzImageHeaderByte_WithoutOffset:
                        {
                            string prop = reader.ReadString();
                            ushort val = reader.ReadUInt16();
                            if (prop != "Property" || val != 0)
                            {
                                return false;
                            }
                            break;
                        }
                    default:
                        {
                            // todo: log this or warn.
                            Helpers.ErrorLogger.Log(Helpers.ErrorLevel.MissingFeature, "[WzImage] New Wz image header found. b = " + b);
                            return false;
                        }
                }
                List<WzImageProperty> images = WzImageProperty.ParsePropertyList(offset, reader, this, this);
                properties.AddRange(images);

                parsed = true;
            }
            return true;
        }

        /// <summary>
        /// Marks this WzImage as parsed to avoid loading from file once again
        /// This function will be used exclusively for creating new Data.wz file for now :) 
        /// </summary>
        public void MarkWzImageAsParsed()
        {
            Parsed = true;
        }

        public byte[] DataBlock
        {
            get
            {
                byte[] blockData = null;
                if (reader != null && size > 0)
                {
                    blockData = reader.ReadBytes(size);
                    reader.BaseStream.Position = blockStart;
                }
                return blockData;
            }
        }

        public void UnparseImage()
        {
            parsed = false;
            this.properties.Clear();
            this.properties = new List<WzImageProperty>();
        }

        /// <summary>
        /// Writes the WzImage object to the underlying WzBinaryWriter
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="bIsWzUserKeyDefault">Uses the default MapleStory UserKey or a custom key.</param>
        /// <param name="forceReadFromData">Read from data regardless of base data that's changed or not.</param>
		public void SaveImage(WzBinaryWriter writer, bool bIsWzUserKeyDefault = true, bool forceReadFromData = false)
        {
            if (bIsImageChanged ||
                !bIsWzUserKeyDefault || //  everything needs to be re-written when a custom UserKey is used
                forceReadFromData) // if its not being force-read and written, it saves with the previous WZ encryption IV.
            {
                if (reader != null && !parsed)
                {
                    this.ParseEverything = true;
                    ParseImage(forceReadFromData);
                }

                WzSubProperty imgProp = new WzSubProperty();

                long startPos = writer.BaseStream.Position;
                imgProp.AddPropertiesForWzImageDumping(WzProperties);
                imgProp.WriteValue(writer);

                writer.StringCache.Clear();

                size = (int)(writer.BaseStream.Position - startPos);
            }
            else
            {
                long pos = reader.BaseStream.Position;
                reader.BaseStream.Position = offset;
                writer.Write(reader.ReadBytes((int) pos));

                reader.BaseStream.Position = pos; // reset
            }
        }

        public void ExportXml(StreamWriter writer, bool oneFile, int level)
        {
            if (oneFile)
            {
                writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.OpenNamedTag("WzImage", this.name, true));
                WzImageProperty.DumpPropertyList(writer, level, WzProperties);
                writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.CloseTag("WzImage"));
            }
            else
            {
                throw new Exception("Under Construction");
            }
        }
        #endregion

        #region Overrides
        public override string ToString()
        {
            string loggerSuffix = string.Format("WzImage: '{0}' {1}", Name,
                ((WzFileParent != null) ? (", ver. " + Enum.GetName(typeof(WzMapleVersion), WzFileParent.MapleVersion) + ", v" + WzFileParent.Version.ToString()) : ""));

            return loggerSuffix;
        }
        #endregion
    }
}