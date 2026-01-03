using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Shapes
{
    public class ToolTipDot : MapleDot
    {
        private MapleRectangle parentTooltip;

        public ToolTipDot(MapleRectangle parentTooltip, Board board, int x, int y)
            : base(board, x, y)
        {
            this.parentTooltip = parentTooltip;
        }

        public override XNA.Color Color
        {
            get
            {
                return UserSettings.ToolTipColor;
            }
        }

        public override XNA.Color InactiveColor
        {
            get { return MultiBoard.ToolTipInactiveColor; }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.ToolTips; }
        }

        public MapleRectangle ParentTooltip
        {
            get { return parentTooltip; }
            set { parentTooltip = value; }
        }

        protected override bool RemoveConnectedLines
        {
            get { return false; }
        }

        public override bool ShouldSelectSerialized
        {
            get
            {
                return true;
            }
        }

        public override List<ISerializableSelector> SelectSerialized(HashSet<ISerializableSelector> serializedItems)
        {
            return new List<ISerializableSelector> { parentTooltip };
        }
    }
}
