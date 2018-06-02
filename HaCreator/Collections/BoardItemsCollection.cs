/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Collections
{
    public class BoardItemsCollection : ItemsCollectionBase, IEnumerable<BoardItem>
    {
        public BoardItemsCollection(BoardItemsManager bim, bool items) : base(bim, items)
        {
        }

        public IEnumerator<BoardItem> GetEnumerator()
        {
            return new BoardItemsEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
