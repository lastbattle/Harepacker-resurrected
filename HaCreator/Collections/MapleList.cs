/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Collections
{
    public class MapleList<T> : List<T>, IMapleList
    {
        private ItemTypes listType;
        private bool item;

        public MapleList(ItemTypes listType, bool item)
            : base()
        {
            this.listType = listType;
            this.item = item;
        }

        public bool IsItem
        {
            get { return item; }
        }

        public ItemTypes ListType
        {
            get { return listType; }
        }
    }
}
