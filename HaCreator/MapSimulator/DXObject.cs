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

namespace HaCreator.MapSimulator
{
    public class DXObject
    {
        protected Texture2D texture;
        private int x;
        private int y;

        private int delay;

        public DXObject(int x, int y, Texture2D texture)
        {
            this.x = x;
            this.y = y;
            this.texture = texture;
        }

        public DXObject(int x, int y, int delay, Texture2D texture)
            : this(x, y, texture)
        {
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

    public class MapItem
    {
        private List<DXObject> frames;
        private int currFrame = 0;
        private int lastFrameSwitchTime = 0;

        protected bool flip;
        protected bool notAnimated;
        private DXObject frame0;

        public MapItem(List<DXObject> frames, bool flip)
        {
            this.frames = frames;
            notAnimated = false;
            this.flip = flip;
        }

        public MapItem(DXObject frame0, bool flip)
        {
            this.frame0 = frame0;
            notAnimated = true;
            this.flip = flip;
        }

        protected DXObject GetCurrFrame()
        {
            if (notAnimated) return frame0;
            else
            {
                int tc = Environment.TickCount;
                if (tc - lastFrameSwitchTime > frames[currFrame].Delay)
                { //advance frame
                    currFrame++;
                    if (currFrame == frames.Count) currFrame = 0;
                    lastFrameSwitchTime = tc;
                }
                return frames[currFrame];
            }
        }

        public virtual void Draw(SpriteBatch sprite, int mapShiftX, int mapShiftY, int centerX, int centerY, int width, int height)
        {
            if (notAnimated)
            {
                if (frame0.X - mapShiftX + frame0.Width > 0 && frame0.Y - mapShiftY + frame0.Height > 0 && frame0.X - mapShiftX < width && frame0.Y - mapShiftY < height)
                    frame0.Draw(sprite, mapShiftX, mapShiftY, flip);
            }
            else
                GetCurrFrame().Draw(sprite, mapShiftX, mapShiftY, flip);
        }
    }

    public class BackgroundItem : MapItem
    {
        private int rx;
        private int ry;
        private int cx;
        private int cy;
        private BackgroundType type;
        private int a;
        private Color color;
        private bool front;

        private double bgMoveShiftX = 0;
        private double bgMoveShiftY = 0;

        public BackgroundItem(int cx, int cy, int rx, int ry, BackgroundType type, int a, bool front, List<DXObject> frames, bool flip)
            : base(frames, flip)
        {
            LastShiftIncreaseX = Environment.TickCount;
            LastShiftIncreaseY = Environment.TickCount;
            this.rx = rx;
            this.cx = cx;
            this.ry = ry;
            this.cy = cy;
            this.type = type;
            this.a = a;
            this.front = front;
            color = new Color(0xFF, 0xFF, 0xFF, a);
        }

        public BackgroundItem(int cx, int cy, int rx, int ry, BackgroundType type, int a, bool front, DXObject frame0, bool flip)
            : base(frame0, flip)
        {
            LastShiftIncreaseX = Environment.TickCount;
            LastShiftIncreaseY = Environment.TickCount;
            this.rx = rx;
            this.cx = cx;
            this.ry = ry;
            this.cy = cy;
            this.type = type;
            this.a = a;
            this.front = front;
            color = new Color(0xFF, 0xFF, 0xFF, a);
        }

        private void DrawHorizontalCopies(SpriteBatch sprite, int simWidth, int x, int y, int cx, DXObject frame)
        {
            int width = frame.Width;
            Draw2D(sprite, x, y, frame);
            int copyX = x - cx;
            while (copyX + width > 0)
            {
                Draw2D(sprite, copyX, y, frame);
                copyX -= cx;
            }
            copyX = x + cx;
            while (copyX < simWidth)
            {
                Draw2D(sprite, copyX, y, frame);
                copyX += cx;
            }
        }

        private void DrawVerticalCopies(SpriteBatch sprite, int simHeight, int x, int y, int cy, DXObject frame)
        {
            int height = frame.Height;
            Draw2D(sprite, x, y, frame);
            int copyY = y - cy;
            while (copyY + height > 0)
            {
                Draw2D(sprite, x, copyY, frame);
                copyY -= cy;
            }
            copyY = y + cy;
            while (copyY < simHeight)
            {
                Draw2D(sprite, x, copyY, frame);
                copyY += cy;
            }
        }

        private void DrawHVCopies(SpriteBatch sprite, int simWidth, int simHeight, int x, int y, int cx, int cy, DXObject frame)
        {
            int width = frame.Width;
            DrawVerticalCopies(sprite, simHeight, x, y, cy, frame);
            int copyX = x - cx;
            while (copyX + width > 0)
            {
                DrawVerticalCopies(sprite, simHeight, copyX, y, cy, frame);
                copyX -= cx;
            }
            copyX = x + cx;
            while (copyX < simWidth)
            {
                DrawVerticalCopies(sprite, simHeight, copyX, y, cy, frame);
                copyX += cx;
            }
        }

        public void Draw2D(SpriteBatch sprite, int x, int y, DXObject frame)
        {
            frame.Draw(sprite, x, y, Color, flip);
        }

        private int LastShiftIncreaseX = 0;
        private int LastShiftIncreaseY = 0;

        public void IncreaseShiftX(int cx)
        {
            bgMoveShiftX += rx * (Environment.TickCount - LastShiftIncreaseX) / 200d;
            bgMoveShiftX %= cx;
            LastShiftIncreaseX = Environment.TickCount;
        }

        public void IncreaseShiftY(int cy)
        {
            bgMoveShiftY += ry * (Environment.TickCount - LastShiftIncreaseY) / 200d;
            bgMoveShiftY %= cy;
            LastShiftIncreaseY = Environment.TickCount;
        }

        public override void Draw(SpriteBatch sprite, int mapShiftX, int mapShiftY, int centerX, int centerY, int simWidth, int simHeight)
        {
            DXObject frame = GetCurrFrame();
            int X = CalculateBackgroundPosX(frame, mapShiftX, centerX);
            int Y = CalculateBackgroundPosY(frame, mapShiftY, centerY);
            int _cx = cx == 0 ? frame.Width : cx;
            int _cy = cy == 0 ? frame.Height : cy;
            switch (type)
            {
                default:
                case BackgroundType.Regular:
                    Draw2D(sprite, X, Y, frame);
                    break;
                case BackgroundType.HorizontalTiling:
                    DrawHorizontalCopies(sprite, simWidth, X, Y, _cx, frame);
                    break;
                case BackgroundType.VerticalTiling:
                    DrawVerticalCopies(sprite, simHeight, X, Y, _cy, frame);
                    break;
                case BackgroundType.HVTiling:
                    DrawHVCopies(sprite, simWidth, simHeight, X, Y, _cx, _cy, frame);
                    break;
                case BackgroundType.HorizontalMoving:
                    DrawHorizontalCopies(sprite, simWidth, X + (int)bgMoveShiftX, Y, _cx, frame);
                    IncreaseShiftX(_cx);
                    break;
                case BackgroundType.VerticalMoving:
                    DrawVerticalCopies(sprite, simHeight, X, Y + (int)bgMoveShiftY, _cy, frame);
                    IncreaseShiftY(_cy);
                    break;
                case BackgroundType.HorizontalMovingHVTiling:
                    DrawHVCopies(sprite, simWidth, simHeight, X + (int)bgMoveShiftX, Y, _cx, _cy, frame);
                    IncreaseShiftX(_cx);
                    break;
                case BackgroundType.VerticalMovingHVTiling:
                    DrawHVCopies(sprite, simWidth, simHeight, X, Y + (int)bgMoveShiftY, _cx, _cy, frame);
                    IncreaseShiftX(_cy);
                    break;
            }
        }

        public int CalculateBackgroundPosX(DXObject frame, int mapShiftX, int centerX)
        {
            return (rx * (mapShiftX - centerX + 400) / 100) + frame.X + 400;
        }

        public int CalculateBackgroundPosY(DXObject frame, int mapShiftY, int centerY)
        {
            return (ry * (mapShiftY - centerY + 300) / 100) + frame.Y + 300;
        }

        public Color Color
        {
            get
            {
                return color;
            }
        }

        public bool Front { get { return front; } }
    }
}