/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.DX
{
    public class DXObject : IDXObject
    {
        protected Texture2D texture;
        private readonly int _x;
        private readonly int _y;

        private readonly int delay;

        public DXObject(int x, int y, Texture2D texture, int delay = 0)
        {
            this._x = x;
            this._y = y;
            this.texture = texture;

            this.delay = delay;
        }

        /// <summary>
        /// Draw map objects
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="meshRenderer"></param>
        /// <param name="gameTime"></param>
        /// <param name="mapShiftX"></param>
        /// <param name="mapShiftY"></param>
        /// <param name="flip"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawObject(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, SkeletonMeshRenderer meshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, bool flip)
        {
            spriteBatch.Draw(texture, new Rectangle(X - mapShiftX, Y - mapShiftY, texture.Width, texture.Height), null, Color.White, 0f, new Vector2(0f, 0f), flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Draw background
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="meshRenderer"></param>
        /// <param name="gameTime"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="color"></param>
        /// <param name="flip"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawBackground(Microsoft.Xna.Framework.Graphics.SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime,
            int x, int y, Color color, bool flip)
        {
            sprite.Draw(texture, new Rectangle(x, y, texture.Width, texture.Height), null, color, 0f, new Vector2(0f, 0f), flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }

        public bool IsSpineDxObject { get { return false; } }

        public int Delay
        {
            get { return delay; }
        }

        public int X { get { return _x; } }
        public int Y { get { return _y; } }

        public int Width { get { return texture.Width; } }
        public int Height { get { return texture.Height; } }
    }
}