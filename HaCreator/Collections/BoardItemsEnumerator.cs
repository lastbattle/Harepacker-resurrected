using HaCreator.MapEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Collections
{
    public class BoardItemsEnumerator : ItemsEnumeratorBase, IEnumerator<BoardItem>
    {
        public BoardItemsEnumerator(BoardItemsCollection bic) : base(bic)
        {
        }

        public BoardItem Current
        {
            get { return (BoardItem)base.CurrentObject; }
        }

        object IEnumerator.Current
        {
            get { return base.CurrentObject; }
        }
    }
}
