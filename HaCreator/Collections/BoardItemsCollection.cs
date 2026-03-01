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
