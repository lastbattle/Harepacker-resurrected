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
using MapleLib.WzLib.Util;

namespace MapleLib.WzLib.WzProperties
{
	/// <summary>
	/// A property that contains an x and a y value
	/// </summary>
	public class WzVectorProperty : WzExtended
	{
		#region Fields
		internal string name;
		internal WzIntProperty x, y;
		internal WzObject parent;
		//internal WzImage imgParent;
		#endregion

		#region Inherited Members
        public override void SetValue(object value)
        {
            if (value is System.Drawing.Point)
            {
                x.val = ((System.Drawing.Point)value).X;
                y.val = ((System.Drawing.Point)value).Y;
            }
            else
            {
                x.val = ((System.Drawing.Size)value).Width;
                y.val = ((System.Drawing.Size)value).Height;
            }
        }

        public override WzImageProperty DeepClone()
        {
            WzVectorProperty clone = new WzVectorProperty(name, x, y);
            return clone;
        }

		public override object WzValue { get { return new System.Drawing.Point(x.Value, y.Value); } }
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
		public override WzPropertyType PropertyType { get { return WzPropertyType.Vector; } }
		public override void WriteValue(MapleLib.WzLib.Util.WzBinaryWriter writer)
		{
			writer.WriteStringValue("Shape2D#Vector2D", 0x73, 0x1B);
			writer.WriteCompressedInt(X.Value);
			writer.WriteCompressedInt(Y.Value);
		}
		public override void ExportXml(StreamWriter writer, int level)
		{
			writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.OpenNamedTag("WzVector", this.Name, false, false) +
				XmlUtil.Attrib("X", this.X.Value.ToString()) + XmlUtil.Attrib("Y", this.Y.Value.ToString(), true, true));
		}
		/// <summary>
		/// Disposes the object
		/// </summary>
		public override void Dispose()
		{
			name = null;
			x.Dispose();
			x = null;
			y.Dispose();
			y = null;
		}
		#endregion

		#region Custom Members
		/// <summary>
		/// The X value of the Vector2D
		/// </summary>
		public WzIntProperty X { get { return x; } set { x = value; } }
		/// <summary>
		/// The Y value of the Vector2D
		/// </summary>
		public WzIntProperty Y { get { return y; } set { y = value; } }
		/// <summary>
		/// The Point of the Vector2D created from the X and Y
		/// </summary>
		public System.Drawing.Point Pos { get { return new System.Drawing.Point(X.Value, Y.Value); } }
		/// <summary>
		/// Creates a blank WzVectorProperty
		/// </summary>
		public WzVectorProperty() { }
		/// <summary>
		/// Creates a WzVectorProperty with the specified name
		/// </summary>
		/// <param name="name">The name of the property</param>
		public WzVectorProperty(string name)
		{
			this.name = name;
		}
		/// <summary>
		/// Creates a WzVectorProperty with the specified name, x and y
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="x">The x value of the vector</param>
		/// <param name="y">The y value of the vector</param>
		public WzVectorProperty(string name, WzIntProperty x, WzIntProperty y)
		{
			this.name = name;
			this.x = x;
			this.y = y;
		}
		#endregion

        #region Cast Values
        public override System.Drawing.Point GetPoint()
        {
            return new System.Drawing.Point(x.val, y.val);
        }

        public override string ToString()
        {
            return "X: " + x.val.ToString() + ", Y: " + y.val.ToString();
        }
        #endregion
	}
}