using HaCreator.MapEditor.Instance.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Collections
{
    public class MapleLinesCollection : ItemsCollectionBase, IEnumerable<MapleLine>
    {
        public MapleLinesCollection(BoardItemsManager bim, bool items) : base(bim, items)
        {
        }

        public IEnumerator<MapleLine> GetEnumerator()
        {
            return new MapleLinesEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
