using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class QuestTimerRuntimeWindow : UIWindowBase
    {
        private readonly bool _drawActionLayer;
        private PacketFieldStateRuntime _runtime;
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private Func<int> _renderWidthProvider;
        private Func<int> _renderHeightProvider;
        private Func<int> _tickProvider;

        public QuestTimerRuntimeWindow(IDXObject frame, string windowName, bool drawActionLayer)
            : base(frame, Point.Zero)
        {
            _drawActionLayer = drawActionLayer;
            RuntimeWindowName = windowName;
            SupportsDragging = false;
            IsVisible = true;
        }

        private string RuntimeWindowName { get; }

        public override string WindowName => RuntimeWindowName;
        public override bool ExcludeFromWindowManagerHide => true;

        public void BindRuntime(
            PacketFieldStateRuntime runtime,
            Func<int> renderWidthProvider,
            Func<int> renderHeightProvider,
            Func<int> tickProvider)
        {
            _runtime = runtime;
            _renderWidthProvider = renderWidthProvider;
            _renderHeightProvider = renderHeightProvider;
            _tickProvider = tickProvider;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (_runtime == null)
            {
                _previousMouseState = mouseState;
                return false;
            }

            bool handled = _runtime.HandleQuestTimerMouse(
                mouseState,
                _previousMouseState,
                ResolveRenderWidth(renderWidth),
                ResolveRenderHeight(renderHeight),
                ResolveTick());

            if (handled)
            {
                mouseCursor?.SetMouseCursorMovedToClickableItem();
            }

            _previousMouseState = mouseState;
            return handled;
        }

        protected override void DrawContents(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_runtime == null)
            {
                return;
            }

            int renderWidth = ResolveRenderWidth(renderParameters.RenderWidth);
            int renderHeight = ResolveRenderHeight(renderParameters.RenderHeight);
            int tick = ResolveTick(TickCount);

            if (_drawActionLayer)
            {
                _runtime.DrawQuestTimerActionLayer(sprite, _font, renderWidth, renderHeight, tick);
                return;
            }

            _runtime.DrawQuestTimerOwnerLayer(sprite, renderWidth, renderHeight, tick);
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            return _runtime?.GetQuestTimerInteractiveBounds(
                       ResolveRenderWidth(0),
                       ResolveRenderHeight(0),
                       ResolveTick())
                   ?? Array.Empty<Rectangle>();
        }

        private int ResolveRenderWidth(int fallbackWidth)
        {
            int width = _renderWidthProvider?.Invoke() ?? fallbackWidth;
            return Math.Max(1, width);
        }

        private int ResolveRenderHeight(int fallbackHeight)
        {
            int height = _renderHeightProvider?.Invoke() ?? fallbackHeight;
            return Math.Max(1, height);
        }

        private int ResolveTick(int fallbackTick = 0)
        {
            return _tickProvider?.Invoke() ?? fallbackTick;
        }
    }
}
