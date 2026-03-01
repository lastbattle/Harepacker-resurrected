using HaCreator.MapEditor.Instance.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Collections
{
    public class MapleLinesEnumerator : ItemsEnumeratorBase, IEnumerator<MapleLine>
    {
        public MapleLinesEnumerator(MapleLinesCollection mlc) : base(mlc)
        {
        }

        public MapleLine Current
        {
            get { return (MapleLine)base.CurrentObject; }
        }

        object IEnumerator.Current
        {
            get { return base.CurrentObject; }
        }
    }
}
