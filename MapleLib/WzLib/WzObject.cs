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
using System.Drawing;
using MapleLib.WzLib.WzProperties;

namespace MapleLib.WzLib
{
	/// <summary>
	/// An abstract class for wz objects
	/// </summary>
	public abstract class WzObject : IDisposable
	{
        private object hcTag = null;
        private object hcTag_spine = null;
        private object msTag = null;
        private object msTag_spine = null;
        private object tag3 = null;

		public abstract void Dispose();

		/// <summary>
		/// The name of the object
		/// </summary>
		public abstract string Name { get; set; }
		/// <summary>
		/// The WzObjectType of the object
		/// </summary>
		public abstract WzObjectType ObjectType { get; }
		/// <summary>
		/// Returns the parent object
		/// </summary>
		public abstract WzObject Parent { get; internal set; }
        /// <summary>
        /// Returns the parent WZ File
        /// </summary>
        public abstract WzFile WzFileParent { get; }

        public WzObject this[string name]
        {
            get
            {
                WzObject wzObject = this;
                
                if (wzObject is WzFile)
                {
                    return ((WzFile)this)[name];
                } 
                else if (wzObject is WzDirectory)
                {
                    return ((WzDirectory)this)[name];
                }
                else if (wzObject is WzImage)
                {
                    return ((WzImage)this)[name];
                }
                else if (wzObject is WzImageProperty)
                {
                    return ((WzImageProperty)this)[name];
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }


        /// <summary>
        /// Gets the top most WZObject directory (i.e Map.wz, Skill.wz)
        /// </summary>
        /// <returns></returns>
        public WzObject GetTopMostWzDirectory()
        {
            WzObject parent = this.Parent;
            if (parent == null)
                return this; // this

            while (parent.Parent != null )
            {
                parent = parent.Parent;
            }
            return parent;
        }

        public string FullPath
        {
            get
            {
                if (this is WzFile file) 
                    return file.WzDirectory.Name;
                
                string result = this.Name;
                WzObject currObj = this;
                while (currObj.Parent != null)
                {
                    currObj = currObj.Parent;
                    result = currObj.Name + @"\" + result;
                }
                return result;
            }
        }

        /// <summary>
        /// Used in HaCreator to save already parsed images
        /// </summary>
        public virtual object HCTag
        {
            get { return hcTag; }
            set { hcTag = value; }
        }


        /// <summary>
        /// Used in HaCreator to save already parsed spine images
        /// </summary>
        public virtual object HCTagSpine
        {
            get { return hcTag_spine; }
            set { hcTag_spine = value; }
        }

        /// <summary>
        /// Used in HaCreator's MapSimulator to save already parsed textures
        /// </summary>
        public virtual object MSTag
        {
            get { return msTag; }
            set { msTag = value; }
        }

        /// <summary>
        /// Used in HaCreator's MapSimulator to save already parsed spine objects
        /// </summary>
        public virtual object MSTagSpine
        {
            get { return msTag_spine; }
            set { msTag_spine = value; }
        }

        /// <summary>
        /// Used in HaRepacker to save WzNodes
        /// </summary>
        public virtual object HRTag
        {
            get { return tag3; }
            set { tag3 = value; }
        }

        public virtual object WzValue { get { return null; } }

        public abstract void Remove();

        //Credits to BluePoop for the idea of using cast overriding
        //2015 - That is the worst idea ever, removed and replaced with Get* methods
        #region Cast Values
        public virtual int GetInt()
        {
            throw new NotImplementedException();
        }

        public virtual short GetShort()
        {
            throw new NotImplementedException();
        }

        public virtual long GetLong()
        {
            throw new NotImplementedException();
        }

        public virtual float GetFloat()
        {
            throw new NotImplementedException();
        }

        public virtual double GetDouble()
        {
            throw new NotImplementedException();
        }

        public virtual string GetString()
        {
            throw new NotImplementedException();
        }

        public virtual Point GetPoint()
        {
            throw new NotImplementedException();
        }

        public virtual Bitmap GetBitmap()
        {
            throw new NotImplementedException();
        }

        public virtual byte[] GetBytes()
        {
            throw new NotImplementedException();
        }
        #endregion

	}
}