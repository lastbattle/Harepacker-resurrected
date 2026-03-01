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
        private readonly int screenMode;

        private double bgMoveShiftX = 0;
        private double bgMoveShiftY = 0;

        // Custom property
        private readonly bool disabledBackground; // disabled background for images that are removed from Map.wz/bg, but entry still presist in maps

        // Pre-calculated tile iteration limits (optimization - avoid while loop overhead)
        private int _cachedMaxHorizontalTiles = 0;
        private int _cachedMaxVerticalTiles = 0;
        private int _lastCalcWidth = 0;
        private int _lastCalcHeight = 0;
        private int _lastCalcCx = 0;
        private int _lastCalcCy = 0;

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
        public BackgroundItem(int _cx, int _cy, int _rx, int _ry, BackgroundType _type, int a, bool front, List<IDXObject> frames, bool flip, int screenMode)
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
        public BackgroundItem(int _cx, int _cy, int _rx, int _ry, BackgroundType _type, int a, bool front, IDXObject frame0, bool flip, int screenMode)
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

        /// <summary>
        /// Updates the cached max tile counts if screen dimensions or tile spacing changed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTileCache(int screenWidth, int screenHeight, int tileCx, int tileCy)
        {
            if (screenWidth != _lastCalcWidth || tileCx != _lastCalcCx)
            {
                _lastCalcWidth = screenWidth;
                _lastCalcCx = tileCx;
                // +2 for partial tiles on edges
                _cachedMaxHorizontalTiles = tileCx > 0 ? (screenWidth / tileCx) + 2 : 1;
            }
            if (screenHeight != _lastCalcHeight || tileCy != _lastCalcCy)
            {
                _lastCalcHeight = screenHeight;
                _lastCalcCy = tileCy;
                _cachedMaxVerticalTiles = tileCy > 0 ? (screenHeight / tileCy) + 2 : 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawHorizontalCopies(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int simWidth, int x, int y, int _cx, IDXObject frame)
        {
            int width = frame.Width;
            Draw2D(sprite, skeletonMeshRenderer, gameTime, x, y, frame);

            // Draw left copies using bounded for loop
            int copyX = x - _cx;
            for (int i = 0; i < _cachedMaxHorizontalTiles && copyX + width > 0; i++, copyX -= _cx)
            {
                Draw2D(sprite, skeletonMeshRenderer, gameTime, copyX, y, frame);
            }

            // Draw right copies using bounded for loop
            copyX = x + _cx;
            for (int i = 0; i < _cachedMaxHorizontalTiles && copyX < simWidth; i++, copyX += _cx)
            {
                Draw2D(sprite, skeletonMeshRenderer, gameTime, copyX, y, frame);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawVerticalCopies(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int simHeight, int x, int y, int _cy, IDXObject frame)
        {
            int height = frame.Height;
            Draw2D(sprite, skeletonMeshRenderer, gameTime, x, y, frame);

            // Draw top copies using bounded for loop
            int copyY = y - _cy;
            for (int i = 0; i < _cachedMaxVerticalTiles && copyY + height > 0; i++, copyY -= _cy)
            {
                Draw2D(sprite, skeletonMeshRenderer, gameTime, x, copyY, frame);
            }

            // Draw bottom copies using bounded for loop
            copyY = y + _cy;
            for (int i = 0; i < _cachedMaxVerticalTiles && copyY < simHeight; i++, copyY += _cy)
            {
                Draw2D(sprite, skeletonMeshRenderer, gameTime, x, copyY, frame);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawHVCopies(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int simWidth, int simHeight, int x, int y, int _cx, int _cy, IDXObject frame)
        {
            int width = frame.Width;
            DrawVerticalCopies(sprite, skeletonMeshRenderer, gameTime, simHeight, x, y, _cy, frame);

            // Draw left column copies using bounded for loop
            int copyX = x - _cx;
            for (int i = 0; i < _cachedMaxHorizontalTiles && copyX + width > 0; i++, copyX -= _cx)
            {
                DrawVerticalCopies(sprite, skeletonMeshRenderer, gameTime, simHeight, copyX, y, _cy, frame);
            }

            // Draw right column copies using bounded for loop
            copyX = x + _cx;
            for (int i = 0; i < _cachedMaxHorizontalTiles && copyX < simWidth; i++, copyX += _cx)
            {
                DrawVerticalCopies(sprite, skeletonMeshRenderer, gameTime, simHeight, copyX, y, _cy, frame);
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

            // Update tile cache if needed (only recalculates when dimensions change)
            UpdateTileCache(renderParameters.RenderWidth, renderParameters.RenderHeight, cx, cy);

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
                    IncreaseShiftX(cx, TickCount);
                    break;
                case BackgroundType.VerticalMoving:
                    DrawVerticalCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderHeight, X, Y + (int)bgMoveShiftY, cy, drawFrame);
                    IncreaseShiftY(cy, TickCount);
                    break;
                case BackgroundType.HorizontalMovingHVTiling:
                    DrawHVCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, renderParameters.RenderHeight, X + (int)bgMoveShiftX, Y, cx, cy, drawFrame);
                    IncreaseShiftX(cx, TickCount);
                    break;
                case BackgroundType.VerticalMovingHVTiling:
                    DrawHVCopies(sprite, skeletonMeshRenderer, gameTime, renderParameters.RenderWidth, renderParameters.RenderHeight, X, Y + (int)bgMoveShiftY, cx, cy, drawFrame);
                    IncreaseShiftX(cy, TickCount);
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

        public bool Front { get { return front; } }

        public bool DisabledBackground { get { return disabledBackground; } }
    }
}
