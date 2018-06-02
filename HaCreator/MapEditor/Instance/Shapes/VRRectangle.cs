/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.UndoRedo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Shapes
{
    public class VRRectangle : MapleEmptyRectangle
    {
        public VRRectangle(Board board, XNA.Rectangle rect)
            : base(board, rect)
        {
        }

        public override MapleDot CreateDot(int x, int y)
        {
            return new VRDot(this, board, x, y);
        }

        public override MapleLine CreateLine(MapleDot a, MapleDot b)
        {
            return new VRLine(board, a, b);
        }

        public override void RemoveItem(List<UndoRedoAction> undoPipe)
        {
            lock (board.ParentControl)
            {
                base.RemoveItem(null);
                board.VRRectangle = null;
            }
        }

        public VRRectangle(Board board, SerializationForm json)
            : base(board, new XNA.Rectangle(json.x0, json.y0, json.x1 - json.x0, json.y1 - json.y0))
        {
            board.VRRectangle = this;
        }
    }
}
