using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Objects.FieldObject
{
    public class BackgroundItem : BaseDXDrawableItem
    {
        private readonly int rx;
        private readonly int ry;
        private int cx;
        private int cy;
        private readonly BackgroundType type;
        private readonly int a;
        private Color color;
        private readonly bool front;
        private readonly int screenMode;

        private double bgMoveShiftX = 0;
        private double bgMoveShiftY = 0;

        // Custom property
        private readonly bool disabledBackground; // disabled background for images that are removed from Map.wz/bg, but entry still presist in maps

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
        public BackgroundItem(int cx, int cy, int rx, int ry, BackgroundType type, int a, bool front, List<IDXObject> frames, bool flip, int screenMode)
            : base(frames, flip)
        {
            int CurTickCount = Environment.TickCount;

            this.LastShiftIncreaseX = CurTickCount;
            this.LastShiftIncreaseY = CurTickCount;
            this.rx = rx;
            this.cx = cx;
            this.ry = ry;
            this.cy = cy;
            this.type = type;
            this.a = a;
            this.front = front;
            this.screenMode = screenMode;

            color = new Color(0xFF, 0xFF, 0xFF, a);

            this.disabledBackground = false;

            CheckBGData();
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
        public BackgroundItem(int cx, int cy, int rx, int ry, BackgroundType type, int a, bool front, IDXObject frame0, bool flip, int screenMode)
            : base(frame0, flip)
        {
            int CurTickCount = Environment.TickCount;

            this.LastShiftIncreaseX = CurTickCount;
            this.LastShiftIncreaseY = CurTickCount;
            this.rx = rx;
            this.cx = cx;
            this.ry = ry;
            this.cy = cy;
            this.type = type;
            this.a = a;
            this.front = front; 
            this.screenMode = screenMode;

            color = new Color(0xFF, 0xFF, 0xFF, a);

            if (frame0.Height <= 1 && frame0.Width <= 1)
                this.disabledBackground = true; // removed from Map.wz/bg, but entry still presist in maps
            else
                this.disabledBackground = false;

            CheckBGData();
        }

        /// <summary>
        /// Input validation for the background data. 
        /// </summary>
        private void CheckBGData()
        {
            if (type != BackgroundType.Regular)
            {
                if (cx < 0)
                    this.cx = 0;
                if (cy < 0)
                    this.cy = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawHorizontalCopies(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int simWidth, int x, int y, int cx, IDXObject frame)
        {
            int width = frame.Width;
            Draw2D(sprite, skeletonMeshRenderer, gameTime, x, y, frame);
            int copyX = x - cx;
            while (copyX + width > 0)
            {
                Draw2D(sprite, skeletonMeshRenderer, gameTime, copyX, y, frame);
                copyX -= cx;
            }
            copyX = x + cx;
            while (copyX < simWidth)
            {
                Draw2D(sprite, skeletonMeshRenderer, gameTime, copyX, y, frame);
                copyX += cx;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawVerticalCopies(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int simHeight, int x, int y, int cy, IDXObject frame)
        {
            int height = frame.Height;
            Draw2D(sprite, skeletonMeshRenderer, gameTime, x, y, frame);
            int copyY = y - cy;
            while (copyY + height > 0)
            {
                Draw2D(sprite, skeletonMeshRenderer, gameTime, x, copyY, frame);
                copyY -= cy;
            }
            copyY = y + cy;
            while (copyY < simHeight)
            {
                Draw2D(sprite, skeletonMeshRenderer, gameTime, x, copyY, frame);
                copyY += cy;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawHVCopies(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int simWidth, int simHeight, int x, int y, int cx, int cy, IDXObject frame)
        {
            int width = frame.Width;
            DrawVerticalCopies(sprite, skeletonMeshRenderer, gameTime, simHeight, x, y, cy, frame);
            int copyX = x - cx;
            while (copyX + width > 0)
            {
                DrawVerticalCopies(sprite, skeletonMeshRenderer, gameTime, simHeight, copyX, y, cy, frame);
                copyX -= cx;
            }
            copyX = x + cx;
            while (copyX < simWidth)
            {
                DrawVerticalCopies(sprite, skeletonMeshRenderer, gameTime, simHeight, copyX, y, cy, frame);
                copyX += cx;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Draw2D(SpriteBatch sprite, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int x, int y, IDXObject frame)
        {
            frame.DrawBackground(sprite, skeletonRenderer, gameTime, x, y, Color, flip, null);
        }

        private int LastShiftIncreaseX = 0;
        private int LastShiftIncreaseY = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncreaseShiftX(int cx, int TickCount)
        {
            bgMoveShiftX += rx * (TickCount - LastShiftIncreaseX) / 200d;
            bgMoveShiftX %= cx;
            LastShiftIncreaseX = TickCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncreaseShiftY(int cy, int TickCount)
        {
            bgMoveShiftY += ry * (TickCount - LastShiftIncreaseY) / 200d;
            bgMoveShiftY %= cy;
            LastShiftIncreaseY = TickCount;
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (((int)renderParameters.Resolution & screenMode) != screenMode || disabledBackground) // dont draw if the screenMode isnt for this
                return;

            IDXObject drawFrame = GetCurrentFrame(TickCount);
            int X = CalculateBackgroundPosX(drawFrame, mapShiftX, centerX, renderParameters.RenderWidth, renderParameters.RenderObjectScaling);
            int Y = CalculateBackgroundPosY(drawFrame, mapShiftY, centerY, renderParameters.RenderHeight, renderParameters.RenderObjectScaling);
            int _cx = cx == 0 ? drawFrame.Width : cx;
            int _cy = cy == 0 ? drawFrame.Height : cy;

            switch (type)
            {
                case BackgroundType.Regular:
                    Draw2D(sprite, skeletonMeshRenderer, gameTime, X, Y, drawFrame);
                    break;
                case BackgroundType.HorizontalTiling:
                    DrawHorizontalCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, X, Y, _cx, drawFrame);
                    break;
                case BackgroundType.VerticalTiling:
                    DrawVerticalCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderHeight, X, Y, _cy, drawFrame);
                    break;
                case BackgroundType.HVTiling:
                    DrawHVCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, renderParameters.RenderHeight, X, Y, _cx, _cy, drawFrame);
                    break;
                case BackgroundType.HorizontalMoving:
                    DrawHorizontalCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, X + (int)bgMoveShiftX, Y, _cx, drawFrame);
                    IncreaseShiftX(_cx, TickCount);
                    break;
                case BackgroundType.VerticalMoving:
                    DrawVerticalCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderHeight, X, Y + (int)bgMoveShiftY, _cy, drawFrame);
                    IncreaseShiftY(_cy, TickCount);
                    break;
                case BackgroundType.HorizontalMovingHVTiling:
                    DrawHVCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, renderParameters.RenderHeight, X + (int)bgMoveShiftX, Y, _cx, _cy, drawFrame);
                    IncreaseShiftX(_cx, TickCount);
                    break;
                case BackgroundType.VerticalMovingHVTiling:
                    DrawHVCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, renderParameters.RenderHeight, X, Y + (int)bgMoveShiftY, _cx, _cy, drawFrame);
                    IncreaseShiftX(_cy, TickCount);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// draw_layer(int a1, int punk, IUnknown *a3, int a4, int a5, int a6)
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="mapShiftX"></param>
        /// <param name="centerX"></param>
        /// <param name="RenderWidth"></param>
        /// <param name="RenderObjectScaling"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CalculateBackgroundPosX(IDXObject frame, int mapShiftX, int centerX, int RenderWidth, float RenderObjectScaling)
        {
            int width = (int) ((RenderWidth / 2) / RenderObjectScaling);
            //int width = RenderWidth / 2;

            return (rx * (mapShiftX - centerX + width) / 100) + frame.X + width; 
        }

        /// <summary>
        /// draw_layer(int a1, int punk, IUnknown *a3, int a4, int a5, int a6)
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="mapShiftY"></param>
        /// <param name="centerY"></param>
        /// <param name="RenderHeight"></param>
        /// <param name="RenderObjectScaling"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CalculateBackgroundPosY(IDXObject frame, int mapShiftY, int centerY, int RenderHeight, float RenderObjectScaling)
        {
            int height = (int)((RenderHeight / 2) / RenderObjectScaling);
            //int height = RenderHeight / 2;

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

        public bool DisabledBackground { get { return disabledBackground; } }
    }
}
