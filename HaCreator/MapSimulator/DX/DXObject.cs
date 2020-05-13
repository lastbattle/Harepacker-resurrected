/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HaCreator.MapEditor;
using MapleLib.WzLib.WzStructure.Data;

namespace HaCreator.MapSimulator.DX
{
    public class DXObject
    {
        protected Texture2D texture;
        private int x;
        private int y;

        private int delay;

        public DXObject(int x, int y, Texture2D texture, int delay = 0)
        {
            this.x = x;
            this.y = y;
            this.texture = texture;

            this.delay = delay;
        }

        public virtual void Draw(SpriteBatch sprite, int mapShiftX, int mapShiftY, bool flip)
        {
            sprite.Draw(texture, new Rectangle(X - mapShiftX, Y - mapShiftY, texture.Width, texture.Height), null, Color, 0f, new Vector2(0f, 0f), flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }

        public virtual void Draw(SpriteBatch sprite, int x, int y, Color color, bool flip)
        {
            sprite.Draw(texture, new Rectangle(x, y, texture.Width, texture.Height), null, color, 0f, new Vector2(0f, 0f), flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }

        public int Delay
        {
            get { return delay; }
        }

        public virtual Color Color { get { return Color.White; } }
        public virtual int X { get { return x; } }
        public virtual int Y { get { return y; } }

        public virtual int Width { get { return texture.Width; } }
        public virtual int Height { get { return texture.Height; } }
    }
}