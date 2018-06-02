/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance
{
    public abstract class LifeInstance : BoardItem, IFlippable, ISerializable
    {
        private int _rx0Shift;
        private int _rx1Shift;
        private int _yShift;
        private int? mobTime;
        private string limitedname;
        private MapleBool flip;
        private MapleBool hide;
        private int? info; //no idea
        private int? team; //for carnival

        public LifeInstance(MapleDrawableInfo baseInfo, Board board, int x, int y, int rx0Shift, int rx1Shift, int yShift, string limitedname, int? mobTime, MapleBool flip, MapleBool hide, int? info, int? team)
            : base(board, x, y, -1)
        {
            this.limitedname = limitedname;
            this._rx0Shift = rx0Shift;
            this._rx1Shift = rx1Shift;
            this._yShift = yShift;
            this.mobTime = mobTime;
            this.info = info;
            this.team = team;
            this.flip = flip;
            if (flip == true)
            {
                // We need to use the data from baseInfo directly because BaseInfo property is only instantiated in the child ctor,
                // which will execute after we are finished.
                X -= baseInfo.Width - 2 * baseInfo.Origin.X;
            }
        }

        public override void Draw(SpriteBatch sprite, XNA.Color color, int xShift, int yShift)
        {
            XNA.Rectangle destinationRectangle = new XNA.Rectangle((int)X + xShift - Origin.X, (int)Y + yShift - Origin.Y, Width, Height);
            //if (baseInfo.Texture == null) baseInfo.CreateTexture(sprite.GraphicsDevice);
            sprite.Draw(BaseInfo.GetTexture(sprite), destinationRectangle, null, color, 0f, new XNA.Vector2(0f, 0f), Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 1f);
            base.Draw(sprite, color, xShift, yShift);
        }

        public bool Flip
        {
            get
            {
                return flip == true;
            }
            set
            {
                if ((flip == true) == value) return;
                flip = value;
                int xFlipShift = Width - 2 * Origin.X;
                if (flip == true) X -= xFlipShift;
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

        public string LimitedName
        {
            get { return limitedname; }
            set { limitedname = value; }
        }

        public override XNA.Color GetColor(SelectionInfo sel, bool selected)
        {
            XNA.Color c = base.GetColor(sel, selected);
            if (hide == true) c.R = (byte)UserSettings.HiddenLifeR;
            return c;
        }

        public MapleBool Hide
        {
            get { return hide; }
            set { hide = value; }
        }

        public override System.Drawing.Bitmap Image
        {
            get
            {
                return BaseInfo.Image;
            }
        }

        public override int Width
        {
            get { return BaseInfo.Width; }
        }

        public override int Height
        {
            get { return BaseInfo.Height; }
        }

        public override System.Drawing.Point Origin
        {
            get
            {
                return BaseInfo.Origin;
            }
        }

        public int rx0Shift
        {
            get
            {
                return _rx0Shift;
            }
            set
            {
                _rx0Shift = value;
            }
        }

        public int rx1Shift
        {
            get
            {
                return _rx1Shift;
            }
            set
            {
                _rx1Shift = value;
            }
        }

        public int yShift
        {
            get
            {
                return _yShift;
            }
            set
            {
                _yShift = value;
            }
        }

        public int? MobTime
        {
            get
            {
                return mobTime;
            }
            set
            {
                mobTime = value;
            }
        }

        public int? Info { get { return info; } set { info = value; } }
        public int? Team { get { return team; } set { team = value; } }

        public new class SerializationForm : BoardItem.SerializationForm
        {
            public int rx0, rx1, yshift;
            public int? mobtime;
            public string limitedname;
            public MapleBool flip, hide;
            public int? info, team;
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
            result.rx0 = _rx0Shift;
            result.rx1 = _rx1Shift;
            result.yshift = _yShift;
            result.mobtime = mobTime;
            result.limitedname = limitedname;
            result.flip = flip;
            result.hide = hide;
            result.info = info;
            result.team = team;
        }

        public LifeInstance(Board board, SerializationForm json)
            : base(board, json)
        {
            _rx0Shift = json.rx0;
            _rx1Shift = json.rx1;
            _yShift = json.yshift;
            mobTime = json.mobtime;
            flip = json.flip;
            hide = json.hide;
            info = json.info;
            team = json.team;
        }
    }
}
