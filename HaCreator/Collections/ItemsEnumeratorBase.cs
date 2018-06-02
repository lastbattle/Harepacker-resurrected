/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Collections
{
    public abstract class ItemsEnumeratorBase
    {
        private BoardItemsManager parent;
        private bool items;
        private int currListIndex;
        private IMapleList currList;
        private int listIndex;

        public ItemsEnumeratorBase(ItemsCollectionBase icb)
        {
            parent = icb.Manager;
            items = icb.Items;
            currList = parent.BackBackgrounds;
            listIndex = -1;
            currListIndex = 0;
        }

        public bool MoveNext()
        {
            listIndex++;
            if (listIndex == currList.Count)
            {
                listIndex = 0;
                do
                {
                replaceList:
                    currListIndex++;
                    if (currListIndex == parent.AllItemLists.Length) return false;
                    currList = parent.AllItemLists[currListIndex];
                    if (currList.IsItem != items) 
                        goto replaceList;
                }
                while (currList.Count == 0);
            }
            return true;
        }

        public void Reset()
        {
            listIndex = -1;
            currListIndex = 0;
            currList = parent.BackBackgrounds;
        }

        protected object CurrentObject
        {
            get
            {
                return currList[listIndex];
            }
        }

        public void Dispose()
        {
        }
    }
}
