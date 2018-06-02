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
    public class BoardItemPair
    {
        public BoardItem NonSelectedItem;
        public BoardItem SelectedItem;

        public BoardItemPair(BoardItem nonselected, BoardItem selected)
        {
            this.NonSelectedItem = nonselected;
            this.SelectedItem = selected;
        }
    }
}
