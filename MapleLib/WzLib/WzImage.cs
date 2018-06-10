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

namespace MapleLib.WzLib
{
    /// <summary>
    /// A .img contained in a wz directory
    /// </summary>
    public class WzImage : WzObject, IPropertyContainer
    {
        //TODO: nest wzproperties in a wzsubproperty inside of WzImage

        public const int WzImageHeaderByte = 0x73;

        #region Fields
        internal bool parsed = false;
        internal string name;
        internal int size, checksum;
        internal uint offset = 0;
        internal WzBinaryReader reader;
        internal List<WzImageProperty> properties = new List<WzImageProperty>();
        internal WzObject parent;
        internal int blockStart = 0;
        internal long tempFileStart = 0;
        internal long tempFileEnd = 0;
        internal bool changed = false;
        internal bool parseEverything = false;
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
        }

        public override void Dispose()
        {
            name = null;
            reader = null;
            if (properties != null)
            {
                foreach (WzImageProperty prop in properties)
                    prop.Dispose();
                properties.Clear();
                properties = null;
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
        /// Was the image changed
        /// </summary>
        public bool Changed { get { return changed; } set { changed = value; } }
        /// <summary>
        /// The size in the wz file of the image
        /// </summary>
        public int BlockSize { get { return size; } set { size = value; } }
        /// <summary>
        /// The checksum of the image
        /// </summary>
        public int Checksum { get { return checksum; } set { checksum = value; } }
        /// <summary>
        /// The offset of the image
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
            WzImage clone = new WzImage(name);
            clone.changed = true;
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
                if (reader != null) if (!parsed) ParseImage();
                foreach (WzImageProperty iwp in properties)
                    if (iwp.Name.ToLower() == name.ToLower())
                        return iwp;
                return null;
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
            if (segments[0] == "..")
            {
                return null;
            }

            WzImageProperty ret = null;
            for (int x = 0; x < segments.Length; x++)
            {
                bool foundChild = false;
                foreach (WzImageProperty iwp in (ret == null ? this.properties : ret.WzProperties))
                {
                    if (iwp.Name == segments[x])
                    {
                        ret = iwp;
                        foundChild = true;
                        break;
                    }
                }
                if (!foundChild)
                {
                    return null;
                }
            }
            return ret;
        }

        /// <summary>
        /// Adds a property to the image
        /// </summary>
        /// <param name="prop">Property to add</param>
        public void AddProperty(WzImageProperty prop)
        {
            prop.Parent = this;
            if (reader != null && !parsed) ParseImage();
            properties.Add(prop);
        }
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
            if (reader != null && !parsed) ParseImage();
            prop.Parent = null;
            properties.Remove(prop);
        }
        public void ClearProperties()
        {
            foreach (WzImageProperty prop in properties) prop.Parent = null;
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
		/// Parses the image from the wz filetod
		/// </summary>
		/// <param name="wzReader">The BinaryReader that is currently reading the wz file</param>
        /// <returns>bool Parse status</returns>
        public bool ParseImage(bool parseEverything = false)
        {
            if (Parsed)
            {
                return true;
            }
            else if (Changed)
            {
                Parsed = true;
                return true;
            }

            lock (reader) // for multi threaded XMLWZ export. 
            {
                this.parseEverything = parseEverything;
                long originalPos = reader.BaseStream.Position;
                reader.BaseStream.Position = offset;

                byte b = reader.ReadByte();
                string prop = reader.ReadString();
                ushort val = reader.ReadUInt16();

                if (b != WzImageHeaderByte || prop != "Property" || val != 0)
                    return false;

                properties.AddRange(WzImageProperty.ParsePropertyList(offset, reader, this, this));

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
            this.properties = new List<WzImageProperty>();
        }

        /// <summary>
        /// Writes the WzImage object to the underlying WzBinaryWriter
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="forceReadFromData">Read from data regardless of base data that's changed or not.</param>
		public void SaveImage(WzBinaryWriter writer, bool forceReadFromData = false)
        {
            if (changed || forceReadFromData)
            {
                if (reader != null && !parsed)
                    ParseImage();
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
                writer.Write(reader.ReadBytes(size));
                reader.BaseStream.Position = pos;
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
    }
}