/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010 Snow and haha01haha01
   
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapleLib.WzLib.WzProperties
{
    public class WzListEntry : IWzObject
    {
        public WzListEntry(string value)
        {
            this.value = value;
        }

        private string value;
        private WzListFile parentFile;

        public override IWzObject Parent
        {
            get
            {
                return parentFile;
            }
            internal set
            {
                parentFile = (WzListFile)value;
            }
        }

        public override string Name
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value;
            }
        }

        public override object WzValue
        {
            get
            {
                return value;
            }
        }

        public override void Dispose()
        {
        }

        public override void Remove()
        {
            parentFile.WzListEntries.Remove(this);
        }

        public override WzObjectType ObjectType
        {
            get { return WzObjectType.List; }
        }

        public override IWzFile WzFileParent
        {
            get { return parentFile; }
        }
    }
}
