/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Shapes
{
    public class MinimapDot : MapleDot
    {
        MapleEmptyRectangle rect;

        public MinimapDot(MapleEmptyRectangle rect, Board board, int x, int y)
            : base(board, x, y)
        {
            this.rect = rect;
        }

        public override XNA.Color Color
        {
            get
            {
                return UserSettings.MinimapBoundColor;
            }
        }

        public override XNA.Color InactiveColor
        {
            get { return MultiBoard.MinimapBoundInactiveColor; }
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
