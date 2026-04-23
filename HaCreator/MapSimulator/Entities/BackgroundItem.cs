using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Entities
{
    public class BackgroundItem : BaseDXDrawableItem
    {
        private readonly int _rx;
        private readonly int _ry;
        private int _cx;
        private int _cy;
        private readonly BackgroundType _type;
        private readonly int _a;
        private Color _color;
        private readonly bool front;
        private readonly int _pageId;
        private readonly int screenMode;

        private double bgMoveShiftX = 0;
        private double bgMoveShiftY = 0;

        // Custom property
        private readonly bool disabledBackground; // disabled background for images that are removed from Map.wz/bg, but entry still presist in maps

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_cx"></param>
        /// <param name="_cy"></param>
        /// <param name="_rx"></param>
        /// <param name="_ry"></param>
        /// <param name="_type"></param>
        /// <param name="a"></param>
        /// <param name="front"></param>
        /// <param name="frames"></param>
        /// <param name="flip"></param>
        /// <param name="screenMode">The screen resolution to display this background object. (0 = all res)</param>
        public BackgroundItem(int _cx, int _cy, int _rx, int _ry, BackgroundType _type, int a, bool front, int pageId, List<IDXObject> frames, bool flip, int screenMode)
            : base(frames, flip)
        {
            int CurTickCount = Environment.TickCount;

            this.LastShiftIncreaseX = CurTickCount;
            this.LastShiftIncreaseY = CurTickCount;
            this._rx = _rx;
            this._cx = _cx;
            this._ry = _ry;
            this._cy = _cy;
            this._type = _type;
            this._a = a;
            this.front = front;
            this._pageId = pageId;
            this.screenMode = screenMode;

            _color = new Color(0xFF, 0xFF, 0xFF, a);

            this.disabledBackground = false;

            CheckBGData();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_cx"></param>
        /// <param name="_cy"></param>
        /// <param name="_rx"></param>
        /// <param name="_ry"></param>
        /// <param name="_type"></param>
        /// <param name="a"></param>
        /// <param name="front"></param>
        /// <param name="frame0"></param>
        /// <param name="screenMode">The screen resolution to display this background object. (0 = all res)</param>
        public BackgroundItem(int _cx, int _cy, int _rx, int _ry, BackgroundType _type, int a, bool front, int pageId, IDXObject frame0, bool flip, int screenMode)
            : base(frame0, flip)
        {
            int CurTickCount = Environment.TickCount;

            this.LastShiftIncreaseX = CurTickCount;
            this.LastShiftIncreaseY = CurTickCount;
            this._rx = _rx;
            this._cx = _cx;
            this._ry = _ry;
            this._cy = _cy;
            this._type = _type;
            this._a = a;
            this.front = front; 
            this._pageId = pageId;
            this.screenMode = screenMode;

            _color = new Color(0xFF, 0xFF, 0xFF, a);

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
            if (_type != BackgroundType.Regular)
            {
                if (_cx < 0)
                    this._cx = 0;
                if (_cy < 0)
                    this._cy = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            if (remainder != 0 && value < 0)
            {
                quotient--;
            }

            return quotient;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CeilDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            if (remainder != 0 && value > 0)
            {
                quotient++;
            }

            return quotient;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawHorizontalCopies(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int simWidth, int x, int y, int _cx, IDXObject frame)
        {
            if (_cx <= 0)
            {
                Draw2D(sprite, skeletonMeshRenderer, gameTime, x, y, frame);
                return;
            }

            int width = frame.Width > 0 ? frame.Width : Math.Max(simWidth, _cx);

            int firstCopyIndex = CeilDiv((-width + 1) - x, _cx);
            int lastCopyIndex = FloorDiv((simWidth - 1) - x, _cx);
            for (int copyIndex = firstCopyIndex; copyIndex <= lastCopyIndex; copyIndex++)
            {
                int copyX = x + (copyIndex * _cx);
                Draw2D(sprite, skeletonMeshRenderer, gameTime, copyX, y, frame);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawVerticalCopies(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int simHeight, int x, int y, int _cy, IDXObject frame)
        {
            if (_cy <= 0)
            {
                Draw2D(sprite, skeletonMeshRenderer, gameTime, x, y, frame);
                return;
            }

            int height = frame.Height > 0 ? frame.Height : Math.Max(simHeight, _cy);

            int firstCopyIndex = CeilDiv((-height + 1) - y, _cy);
            int lastCopyIndex = FloorDiv((simHeight - 1) - y, _cy);
            for (int copyIndex = firstCopyIndex; copyIndex <= lastCopyIndex; copyIndex++)
            {
                int copyY = y + (copyIndex * _cy);
                Draw2D(sprite, skeletonMeshRenderer, gameTime, x, copyY, frame);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawHVCopies(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int simWidth, int simHeight, int x, int y, int _cx, int _cy, IDXObject frame)
        {
            if (_cx <= 0 || _cy <= 0)
            {
                Draw2D(sprite, skeletonMeshRenderer, gameTime, x, y, frame);
                return;
            }

            int width = frame.Width > 0 ? frame.Width : Math.Max(simWidth, _cx);
            int height = frame.Height > 0 ? frame.Height : Math.Max(simHeight, _cy);

            int firstCopyXIndex = CeilDiv((-width + 1) - x, _cx);
            int lastCopyXIndex = FloorDiv((simWidth - 1) - x, _cx);
            int firstCopyYIndex = CeilDiv((-height + 1) - y, _cy);
            int lastCopyYIndex = FloorDiv((simHeight - 1) - y, _cy);

            for (int copyXIndex = firstCopyXIndex; copyXIndex <= lastCopyXIndex; copyXIndex++)
            {
                int copyX = x + (copyXIndex * _cx);
                for (int copyYIndex = firstCopyYIndex; copyYIndex <= lastCopyYIndex; copyYIndex++)
                {
                    int copyY = y + (copyYIndex * _cy);
                    Draw2D(sprite, skeletonMeshRenderer, gameTime, copyX, copyY, frame);
                }
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
        public void IncreaseShiftX(int _cx, int TickCount)
        {
            bgMoveShiftX += _rx * (TickCount - LastShiftIncreaseX) / 200d;
            bgMoveShiftX %= _cx;
            LastShiftIncreaseX = TickCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncreaseShiftY(int _cy, int TickCount)
        {
            bgMoveShiftY += _ry * (TickCount - LastShiftIncreaseY) / 200d;
            bgMoveShiftY %= _cy;
            LastShiftIncreaseY = TickCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMotionShift(int cx, int cy, int tickCount)
        {
            switch (_type)
            {
                case BackgroundType.HorizontalMoving:
                case BackgroundType.HorizontalMovingHVTiling:
                    if (cx > 0)
                        IncreaseShiftX(cx, tickCount);
                    break;
                case BackgroundType.VerticalMoving:
                case BackgroundType.VerticalMovingHVTiling:
                    if (cy > 0)
                        IncreaseShiftY(cy, tickCount);
                    break;
            }
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
            int cx = _cx == 0 ? drawFrame.Width : _cx;
            int cy = _cy == 0 ? drawFrame.Height : _cy;

            UpdateMotionShift(cx, cy, TickCount);

            switch (_type)
            {
                case BackgroundType.Regular:
                    Draw2D(sprite, skeletonMeshRenderer, gameTime, X, Y, drawFrame);
                    break;
                case BackgroundType.HorizontalTiling:
                    DrawHorizontalCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, X, Y, cx, drawFrame);
                    break;
                case BackgroundType.VerticalTiling:
                    DrawVerticalCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderHeight, X, Y, cy, drawFrame);
                    break;
                case BackgroundType.HVTiling:
                    DrawHVCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, renderParameters.RenderHeight, X, Y, cx, cy, drawFrame);
                    break;
                case BackgroundType.HorizontalMoving:
                    DrawHorizontalCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, X + (int)bgMoveShiftX, Y, cx, drawFrame);
                    break;
                case BackgroundType.VerticalMoving:
                    DrawVerticalCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderHeight, X, Y + (int)bgMoveShiftY, cy, drawFrame);
                    break;
                case BackgroundType.HorizontalMovingHVTiling:
                    DrawHVCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, renderParameters.RenderHeight, X + (int)bgMoveShiftX, Y, cx, cy, drawFrame);
                    break;
                case BackgroundType.VerticalMovingHVTiling:
                    DrawHVCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, renderParameters.RenderHeight, X, Y + (int)bgMoveShiftY, cx, cy, drawFrame);
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

            return (_rx * (mapShiftX - centerX + width) / 100) + frame.X + width; 
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

            return (_ry * (mapShiftY - centerY + height) / 100) + frame.Y + height;
        }

        public Color Color
        {
            get
            {
                return _color;
            }
        }

        public byte DefaultAlpha => (byte)Math.Clamp(_a, byte.MinValue, byte.MaxValue);

        public void SetAlpha(byte alpha)
        {
            _color = new Color(_color.R, _color.G, _color.B, alpha);
        }

        public bool Front { get { return front; } }

        public int PageId { get { return _pageId; } }

        public bool DisabledBackground { get { return disabledBackground; } }
    }
}
