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
