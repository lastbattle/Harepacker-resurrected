using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Shapes
{
    public class RopeLine : MapleLine
    {
        public RopeLine(Board board, MapleDot firstDot, MapleDot secondDot)
            : base(board, firstDot, secondDot)
        {
            xBind = true;
        }

        public RopeLine(Board board, MapleDot firstDot)
            : base(board, firstDot)
        {
            xBind = true;
        }

        public override XNA.Color Color
        {
            get { return UserSettings.RopeColor; }
        }

        public override XNA.Color InactiveColor
        {
            get { return MultiBoard.RopeInactiveColor; }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.Ropes; }
        }
    }
}
