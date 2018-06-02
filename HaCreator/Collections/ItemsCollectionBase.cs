/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Collections
{
    public abstract class ItemsCollectionBase
    {
        BoardItemsManager bim;
        bool items;

        public ItemsCollectionBase(BoardItemsManager bim, bool items)
        {
            this.bim = bim;
            this.items = items;
        }

        public BoardItemsManager Manager
        {
            get { return bim; }
        }

        public bool Items
        {
            get { return items; }
        }
    }
}
