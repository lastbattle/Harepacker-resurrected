using XNA = Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HaCreator.MapEditor.Instance.Shapes;

namespace HaCreator.MapEditor.Instance.Misc
{
    public class Clock : MiscRectangle, ISerializable
    {
        public Clock(Board board, XNA.Rectangle rect)
            : base(board, rect)
        {
        }

        public override string Name
        {
            get { return "Clock"; }
        }

        public Clock(Board board, MapleRectangle.SerializationForm json)
            : base(board, json) { }
    }
}
