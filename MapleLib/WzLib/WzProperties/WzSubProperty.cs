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

                foreach (WzImageProperty iwp in properties)
                    if (iwp.Name.ToLower() == name.ToLower())
                        return iwp;
				//throw new KeyNotFoundException("A wz property with the specified name was not found");
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
			for (int x = 0; x < segments.Length; x++)
			{
				bool foundChild = false;
				foreach (WzImageProperty iwp in ret.WzProperties)
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
		public override void WriteValue(MapleLib.WzLib.Util.WzBinaryWriter writer)
		{
			writer.WriteStringValue("Property", 0x73, 0x1B);
			WzImageProperty.WritePropertyList(writer, properties);
		}
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
				prop.Dispose();
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
            prop.Parent = this;
            properties.Add(prop);
		}
		public void AddProperties(List<WzImageProperty> props)
		{
			foreach (WzImageProperty prop in props)
			{
				AddProperty(prop);
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
		#endregion
	}
}