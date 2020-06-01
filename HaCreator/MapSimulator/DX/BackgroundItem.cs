using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;


namespace HaCreator.MapSimulator.DX
{
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
        private int screenMode;

        private double bgMoveShiftX = 0;
        private double bgMoveShiftY = 0;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cx"></param>
        /// <param name="cy"></param>
        /// <param name="rx"></param>
        /// <param name="ry"></param>
        /// <param name="type"></param>
        /// <param name="a"></param>
        /// <param name="front"></param>
        /// <param name="frames"></param>
        /// <param name="flip"></param>
        /// <param name="screenMode">The screen resolution to display this background object. (0 = all res)</param>
        public BackgroundItem(int cx, int cy, int rx, int ry, BackgroundType type, int a, bool front, List<DXObject> frames, bool flip, int screenMode)
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
            this.screenMode = screenMode;

            color = new Color(0xFF, 0xFF, 0xFF, a);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cx"></param>
        /// <param name="cy"></param>
        /// <param name="rx"></param>
        /// <param name="ry"></param>
        /// <param name="type"></param>
        /// <param name="a"></param>
        /// <param name="front"></param>
        /// <param name="frame0"></param>
        /// <param name="screenMode">The screen resolution to display this background object. (0 = all res)</param>
        public BackgroundItem(int cx, int cy, int rx, int ry, BackgroundType type, int a, bool front, DXObject frame0, bool flip, int screenMode)
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
            this.screenMode = screenMode;

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

        public void IncreaseShiftX(int cx, int TickCount)
        {
            bgMoveShiftX += rx * (TickCount - LastShiftIncreaseX) / 200d;
            bgMoveShiftX %= cx;
            LastShiftIncreaseX = TickCount;
        }

        public void IncreaseShiftY(int cy, int TickCount)
        {
            bgMoveShiftY += ry * (TickCount - LastShiftIncreaseY) / 200d;
            bgMoveShiftY %= cy;
            LastShiftIncreaseY = TickCount;
        }

        public override void Draw(SpriteBatch sprite, int mapShiftX, int mapShiftY, int centerX, int centerY, 
            int renderWidth, int renderHeight, float RenderObjectScaling, MapRenderResolution mapRenderResolution,
            int TickCount)
        {
            if (((int) mapRenderResolution & screenMode) != screenMode) // dont draw if the screenMode isnt for this
                return;

            DXObject frame = GetCurrFrame();
            int X = CalculateBackgroundPosX(frame, mapShiftX, centerX, renderWidth, RenderObjectScaling);
            int Y = CalculateBackgroundPosY(frame, mapShiftY, centerY, renderHeight, RenderObjectScaling);
            int _cx = cx == 0 ? frame.Width : cx;
            int _cy = cy == 0 ? frame.Height : cy;

            switch (type)
            {
                default:
                case BackgroundType.Regular:
                    Draw2D(sprite, X, Y, frame);
                    break;
                case BackgroundType.HorizontalTiling:
                    DrawHorizontalCopies(sprite, renderWidth, X, Y, _cx, frame);
                    break;
                case BackgroundType.VerticalTiling:
                    DrawVerticalCopies(sprite, renderHeight, X, Y, _cy, frame);
                    break;
                case BackgroundType.HVTiling:
                    DrawHVCopies(sprite, renderWidth, renderHeight, X, Y, _cx, _cy, frame);
                    break;
                case BackgroundType.HorizontalMoving:
                    DrawHorizontalCopies(sprite, renderWidth, X + (int)bgMoveShiftX, Y, _cx, frame);
                    IncreaseShiftX(_cx, TickCount);
                    break;
                case BackgroundType.VerticalMoving:
                    DrawVerticalCopies(sprite, renderHeight, X, Y + (int)bgMoveShiftY, _cy, frame);
                    IncreaseShiftY(_cy, TickCount);
                    break;
                case BackgroundType.HorizontalMovingHVTiling:
                    DrawHVCopies(sprite, renderWidth, renderHeight, X + (int)bgMoveShiftX, Y, _cx, _cy, frame);
                    IncreaseShiftX(_cx, TickCount);
                    break;
                case BackgroundType.VerticalMovingHVTiling:
                    DrawHVCopies(sprite, renderWidth, renderHeight, X, Y + (int)bgMoveShiftY, _cx, _cy, frame);
                    IncreaseShiftX(_cy, TickCount);
                    break;
            }
        }

        public int CalculateBackgroundPosX(DXObject frame, int mapShiftX, int centerX, int RenderWidth, float RenderObjectScaling)
        {
            int width = (int) ((RenderWidth / 2) / RenderObjectScaling);

            return (rx * (mapShiftX - centerX + width) / 100) + frame.X + width;
        }

        public int CalculateBackgroundPosY(DXObject frame, int mapShiftY, int centerY, int RenderHeight, float RenderObjectScaling)
        {
            int height = (int)((RenderHeight / 2) / RenderObjectScaling);

            return (ry * (mapShiftY - centerY + height) / 100) + frame.Y + height;
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
