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
using System.Linq;
using MapleLib.WzLib.Util;
using SharpDX.Direct2D1;

namespace MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property that contains a set of properties
    /// </summary>
    public class WzSubProperty : WzExtended, IPropertyContainer
    {
        #region Fields
        internal List<WzImageProperty> properties = new List<WzImageProperty>();
        internal string name;
        internal WzObject parent;
        //internal WzImage imgParent;
        #endregion

        #region Inherited Members
        public override void SetValue(object value)
        {
            throw new System.NotImplementedException();
        }

        public override WzImageProperty DeepClone()
        {
            WzSubProperty clone = new WzSubProperty(name);
            foreach (WzImageProperty prop in properties)
                clone.AddProperty(prop.DeepClone());
            return clone;
        }

        /// <summary>
        /// The parent of the object
        /// </summary>
        public override WzObject Parent { get { return parent; } internal set { parent = value; } }
        /*		/// <summary>
                /// The image that this property is contained in
                /// </summary>
                public override WzImage ParentImage { get { return imgParent; } internal set { imgParent = value; } }*/
        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType { get { return WzPropertyType.SubProperty; } }
        /// <summary>
        /// The wz properties contained in the property
        /// </summary>
        public override List<WzImageProperty> WzProperties
        {
            get
            {
                return properties;
            }
        }
        /// <summary>
        /// The name of the property
        /// </summary>
        public override string Name { get { return name; } set { name = value; } }
        /// <summary>
        /// Gets a wz property by it's name
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <returns>The wz property with the specified name</returns>
        public override WzImageProperty this[string name]
        {
            get
            {
                return properties.FirstOrDefault(iwp => iwp.Name.ToLower() == name.ToLower());
                //throw new KeyNotFoundException("A wz property with the specified name was not found");
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

        /// <summary>
        /// Gets a wz property by a path name
        /// </summary>
        /// <param name="path">path to property</param>
        /// <returns>the wz property with the specified name</returns>
        public override WzImageProperty GetFromPath(string path)
        {
            string[] segments = path.Split(new char[1] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (segments[0] == "..")
            {
                return ((WzImageProperty)Parent)[path.Substring(name.IndexOf('/') + 1)];
            }
            WzImageProperty ret = this;
            foreach (string segment in segments)
            {
                ret = ret.WzProperties.FirstOrDefault(iwp => iwp.Name == segment);
                if (ret == null)
                {
                    break;
                }
            }
            return ret;
        }

        /// <summary>
        /// Write the WzSubProperty 
        /// </summary>
        /// <param name="writer"></param>
        public override void WriteValue(WzBinaryWriter writer)
        {
            bool bIsLuaProperty = properties.Count == 1 && properties[0] is WzLuaProperty;

            if (!bIsLuaProperty)
                writer.WriteStringValue("Property", WzImage.WzImageHeaderByte_WithoutOffset, WzImage.WzImageHeaderByte_WithOffset);

            WzImageProperty.WritePropertyList(writer, properties);
        }


        /// <summary>
        /// Exports as XML
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="level"></param>
        public override void ExportXml(StreamWriter writer, int level)
        {
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.OpenNamedTag("WzSub", this.Name, true));
            WzImageProperty.DumpPropertyList(writer, level, WzProperties);
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.CloseTag("WzSub"));
        }

        /// <summary>
        /// Disposes the object
        /// </summary>
        public override void Dispose()
        {
            name = null;
            foreach (WzImageProperty prop in properties)
            {
                prop.Dispose();
            }
            properties.Clear();
            properties = null;
        }
        #endregion

        #region Custom Members
        /// <summary>
        /// Creates a blank WzSubProperty
        /// </summary>
        public WzSubProperty() { }
        /// <summary>
        /// Creates a WzSubProperty with the specified name
        /// </summary>
        /// <param name="name">The name of the property</param>
        public WzSubProperty(string name)
        {
            this.name = name;
        }
        /// <summary>
        /// Adds a property to the list
        /// </summary>
        /// <param name="prop">The property to add</param>
        public void AddProperty(WzImageProperty prop)
        {
            prop.Parent = this; // dont set new parent if we're dumping.. x_X
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
        /// Add properties into this WzSubProperties for wz dumping
        /// </summary>
        /// <param name="props"></param>
        public void AddPropertiesForWzImageDumping(List<WzImageProperty> props)
        {
            foreach (WzImageProperty prop in props)
            {
                properties.Add(prop);
            }
        }

        public void RemoveProperty(WzImageProperty prop)
        {
            prop.Parent = null;
            properties.Remove(prop);
        }
        /// <summary>
        /// Clears the list of properties
        /// </summary>
        public void ClearProperties()
        {
            foreach (WzImageProperty prop in properties) prop.Parent = null;
            properties.Clear();
        }

        /// <summary>
        /// Sort the properties by its its number, then alphabetical order.
        /// i.e 1,2,3,4,5,6,7,8,9,10,11,a,b,c,d,e,f
        /// </summary>
        public void SortProperties() 
        {
            // Sort WzCanvasPropertys' in images by their name 
            // see https://github.com/lastbattle/Harepacker-resurrected/issues/173
            properties.Sort((img1, img2) => {
                if (img1 == null)
                    return 0;
                else if (img1.GetType() == typeof(WzCanvasProperty) ||  // frames
                    img1.GetType() == typeof(WzSubProperty)) // footholds
                {
                    int nodeId1, nodeId2;
                    if (int.TryParse(img1.Name, out nodeId1) && int.TryParse(img2.Name, out nodeId2)) {
                        if (nodeId1 == nodeId2)
                            return 0;
                        if (nodeId1 > nodeId2)
                            return 1;
                        return -1;
                    }
                    else // default to string compare
                        return img1.Name.CompareTo(img2.Name);
                }
                else
                    return img1.Name.CompareTo(img2.Name);  // (leave non canvas nodes at the very bottom, i.e "info")
            });
        }
        #endregion
    }
}