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
    internal abstract class QuestTimerRuntimeWindowBase : UIWindowBase
    {
        protected PacketFieldStateRuntime _runtime;
        private readonly int _questId;
        private readonly string _windowName;
        private readonly bool _drawActionLayer;
        private MouseState _previousMouseState;
        private Func<int> _renderWidthProvider;
        private Func<int> _renderHeightProvider;
        private Func<int> _tickProvider;

        protected QuestTimerRuntimeWindowBase(IDXObject frame, string windowName, int questId, bool drawActionLayer)
            : base(frame, Point.Zero)
        {
            _windowName = windowName;
            _questId = questId;
            _drawActionLayer = drawActionLayer;
            SupportsDragging = false;
            IsVisible = true;
        }

        public override string WindowName => _windowName;
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
            base.SetFont(font);
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (_runtime == null)
            {
                _previousMouseState = mouseState;
                return false;
            }

            bool handled = _runtime.HandleQuestTimerMouse(
                _questId,
                mouseState,
                _previousMouseState,
                _drawActionLayer,
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
                _runtime?.DrawQuestTimerActionLayer(sprite, WindowFont, _questId, renderWidth, renderHeight, tick);
            }
            else
            {
                _runtime?.DrawQuestTimerOwnerLayer(sprite, _questId, renderWidth, renderHeight, tick);
            }
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            return _runtime?.GetQuestTimerInteractiveBounds(
                       _questId,
                       _drawActionLayer,
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

    internal sealed class QuestTimerWindow : QuestTimerRuntimeWindowBase
    {
        public QuestTimerWindow(IDXObject frame, string windowName, int questId)
            : base(frame, windowName, questId, drawActionLayer: false)
        {
        }
    }

    internal sealed class QuestTimerActionWindow : QuestTimerRuntimeWindowBase
    {
        public QuestTimerActionWindow(IDXObject frame, string windowName, int questId)
            : base(frame, windowName, questId, drawActionLayer: true)
        {
        }
    }
}
