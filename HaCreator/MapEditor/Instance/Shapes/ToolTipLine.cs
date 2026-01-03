using HaCreator.MapEditor.UndoRedo;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Shapes
{
    public class ToolTipLine : MapleLine
    {
        public ToolTipLine(Board board, MapleDot firstDot, MapleDot secondDot)
            : base(board, firstDot, secondDot)
        {
        }

        public override XNA.Color Color
        {
            get { return UserSettings.ToolTipColor; }
        }

        public override XNA.Color InactiveColor
        {
            get { return MultiBoard.ToolTipInactiveColor; }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.ToolTips; }
        }

        public override void Remove(bool removeDots, List<UndoRedoAction> undoPipe)
        {

        }
    }
}
