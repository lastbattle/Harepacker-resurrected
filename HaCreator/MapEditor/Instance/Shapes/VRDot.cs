using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Shapes
{
    public class VRDot : MapleDot
    {
        MapleEmptyRectangle rect;

        public VRDot(MapleEmptyRectangle rect, Board board, int x, int y)
            : base(board, x, y)
        {
            this.rect = rect;
        }

        public override XNA.Color Color
        {
            get
            {
                return UserSettings.VRColor;
            }
        }

        public override XNA.Color InactiveColor
        {
            get { return MultiBoard.VRInactiveColor; }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.Misc; }
        }

        protected override bool RemoveConnectedLines
        {
            get { return false; }
        }
    }
}
