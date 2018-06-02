/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Misc
{
    public class ShipObject : BoardItem, IFlippable, INamedMisc, ISerializable
    {
        private ObjectInfo baseInfo; //shipObj
        private bool flip;
        private int? x0;
        private int? zVal;
        private int tMove;
        private int shipKind;

        public ShipObject(ObjectInfo baseInfo, Board board, int x, int y, int? zVal, int? x0, int tMove, int shipKind, bool flip)
            : base(board, x, y, -1)
        {
            this.baseInfo = baseInfo;
            this.flip = flip;
            this.x0 = x0;
            this.zVal = zVal;
            this.tMove = tMove;
            this.shipKind = shipKind;
            if (flip)
                X -= Width - 2 * Origin.X;
        }

        public int? X0
        {
            get { return x0; }
            set { x0 = value; }
        }

        public int? zValue
        {
            get { return zVal; }
            set { zVal = value; }
        }

        public int TimeMove
        {
            get { return tMove; }
            set { tMove = value; }
        }

        public int ShipKind
        {
            get { return shipKind; }
            set { shipKind = value; }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.Misc; }
        }

        public string Name
        {
            get
            {
                return "Special: Ship";
            }
        }

        public override MapleDrawableInfo BaseInfo
        {
            get { return baseInfo; }
        }

        public override XNA.Color GetColor(SelectionInfo sel, bool selected)
        {
            XNA.Color c = base.GetColor(sel, selected);
            return c;
        }

        public bool Flip
        {
            get
            {
                return flip;
            }
            set
            {
                if (flip == value) return;
                flip = value;
                int xFlipShift = Width - 2 * Origin.X;
                if (flip) X -= xFlipShift;
                else X += xFlipShift;
            }
        }

        public int UnflippedX
        {
            get
            {
                return flip ? (X + Width - 2 * Origin.X) : X;
            }
        }

        public override void Draw(SpriteBatch sprite, XNA.Color color, int xShift, int yShift)
        {
            XNA.Rectangle destinationRectangle = new XNA.Rectangle((int)X + xShift - Origin.X, (int)Y + yShift - Origin.Y, Width, Height);
            sprite.Draw(baseInfo.GetTexture(sprite), destinationRectangle, null, color, 0f, new XNA.Vector2(0, 0), Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0 /*Layer.LayerNumber / 10f + Z / 1000f*/);
        }

        public override System.Drawing.Bitmap Image
        {
            get
            {
                return baseInfo.Image;
            }
        }

        public override int Width
        {
            get { return baseInfo.Width; }
        }

        public override int Height
        {
            get { return baseInfo.Height; }
        }

        public override System.Drawing.Point Origin
        {
            get
            {
                return baseInfo.Origin;
            }
        }

        public new class SerializationForm : BoardItem.SerializationForm
        {
            public string os, l0, l1, l2;
            public bool flip;
            public int? x0, zval;
            public int tmove, shipkind;
        }

        public override object Serialize()
        {
            SerializationForm result = new SerializationForm();
            UpdateSerializedForm(result);
            return result;
        }

        protected void UpdateSerializedForm(SerializationForm result)
        {
            base.UpdateSerializedForm(result);
            result.os = baseInfo.oS;
            result.l0 = baseInfo.l0;
            result.l1 = baseInfo.l1;
            result.l2 = baseInfo.l2;
            result.flip = flip;
            result.x0 = x0;
            result.zval = zVal;
            result.tmove = tMove;
            result.shipkind = shipKind;
        }

        public ShipObject(Board board, SerializationForm json)
            : base(board, json)
        {
            flip = json.flip;
            x0 = json.x0;
            zVal = json.zval;
            tMove = json.tmove;
            shipKind = json.shipkind;
            baseInfo = ObjectInfo.Get(json.os, json.l0, json.l1, json.l2);
        }
    }
}
