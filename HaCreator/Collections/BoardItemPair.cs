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
