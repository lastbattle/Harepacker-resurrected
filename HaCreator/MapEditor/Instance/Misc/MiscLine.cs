using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.UndoRedo;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Misc
{
    public class MiscLine : MapleLine
    {
        public MiscLine(Board board, MapleDot firstDot, MapleDot secondDot)
            : base(board, firstDot, secondDot)
        {
        }

        public override XNA.Color Color
        {
            get { return UserSettings.MiscColor; }
        }

        public override XNA.Color InactiveColor
        {
            get { return MultiBoard.MiscInactiveColor; }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.Misc; }
        }

        public override void Remove(bool removeDots, List<UndoRedoAction> undoPipe)
        {

        }
    }
}
