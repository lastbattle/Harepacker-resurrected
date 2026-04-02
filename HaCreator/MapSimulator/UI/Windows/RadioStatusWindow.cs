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
    internal sealed class RadioStatusWindow : UIWindowBase
    {
        private const int ClientTopInset = 3;
        private const int ClientRightInset = 3;
        private const int ClientLeftModeExtraRightInset = 40;
        private const float TooltipScale = 0.38f;
        private const int TooltipPadding = 6;
        private const int TooltipOffsetY = 20;

        private readonly struct IndicatorFrame
        {
            public IndicatorFrame(Texture2D texture, int delayMs)
            {
                Texture = texture;
                DelayMs = Math.Max(1, delayMs);
            }

            public Texture2D Texture { get; }
            public int DelayMs { get; }
        }

        private readonly string _windowName;
        private readonly List<IndicatorFrame> _activeFrames = new();
        private readonly Dictionary<Texture2D, IDXObject> _frameCache = new();
        private readonly Texture2D _pixel;
        private readonly Texture2D _inactiveTexture;

        private SpriteFont _font;
        private Func<bool> _indicatorActiveProvider;
        private Func<bool> _clientLeftInsetProvider;
        private Func<string> _trackNameProvider;

        internal RadioStatusWindow(
            GraphicsDevice device,
            Texture2D inactiveTexture,
            IReadOnlyList<UtilityPanelWindow.IndicatorFrame> activeFrames,
            string windowName,
            int rightMargin = 3,
            int topMargin = 3)
            : base(CreateFrameObject(inactiveTexture, activeFrames))
        {
            _windowName = string.IsNullOrWhiteSpace(windowName) ? MapSimulatorWindowNames.Radio : windowName;
            _inactiveTexture = inactiveTexture ?? throw new ArgumentNullException(nameof(inactiveTexture));
            SupportsDragging = false;

            if (activeFrames != null)
            {
                foreach (UtilityPanelWindow.IndicatorFrame frame in activeFrames)
                {
                    if (frame.Texture != null)
                    {
                        _activeFrames.Add(new IndicatorFrame(frame.Texture, frame.DelayMs));
                    }
                }
            }

            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
            Frame = CacheFrame(ResolveIndicatorTexture(Environment.TickCount));
        }

        public override string WindowName => _windowName;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        internal void SetIndicatorActiveProvider(Func<bool> indicatorActiveProvider)
        {
            _indicatorActiveProvider = indicatorActiveProvider;
        }

        internal void SetClientLeftInsetProvider(Func<bool> clientLeftInsetProvider)
        {
            _clientLeftInsetProvider = clientLeftInsetProvider;
        }

        internal void SetTrackNameProvider(Func<string> trackNameProvider)
        {
            _trackNameProvider = trackNameProvider;
        }

        public override void Draw(
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
            if (!IsVisible)
            {
                return;
            }

            UpdateAnchoredPosition(renderParameters);
            Frame = CacheFrame(ResolveIndicatorTexture(TickCount));
            base.Draw(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                drawReflectionInfo,
                renderParameters,
                TickCount);
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
            if (_font == null)
            {
                return;
            }

            string trackName = _trackNameProvider?.Invoke();
            if (string.IsNullOrWhiteSpace(trackName))
            {
                return;
            }

            MouseState mouse = Mouse.GetState();
            if (!GetWindowBounds().Contains(mouse.X, mouse.Y))
            {
                return;
            }

            Vector2 textSize = ClientTextDrawing.Measure((GraphicsDevice)null, trackName, TooltipScale, _font);
            int tooltipWidth = (int)Math.Ceiling(textSize.X) + (TooltipPadding * 2);
            int tooltipHeight = (int)Math.Ceiling(textSize.Y) + (TooltipPadding * 2);
            int tooltipX = Math.Max(0, Math.Min(mouse.X, Math.Max(0, renderParameters.RenderWidth - tooltipWidth)));
            int tooltipY = Math.Max(0, Math.Min(mouse.Y + TooltipOffsetY, Math.Max(0, renderParameters.RenderHeight - tooltipHeight)));
            Rectangle tooltipBounds = new(tooltipX, tooltipY, tooltipWidth, tooltipHeight);

            sprite.Draw(_pixel, tooltipBounds, new Color(24, 24, 24, 235));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X, tooltipBounds.Y, tooltipBounds.Width, 1), new Color(255, 228, 151));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X, tooltipBounds.Bottom - 1, tooltipBounds.Width, 1), new Color(255, 228, 151));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X, tooltipBounds.Y, 1, tooltipBounds.Height), new Color(255, 228, 151));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.Right - 1, tooltipBounds.Y, 1, tooltipBounds.Height), new Color(255, 228, 151));
            ClientTextDrawing.Draw(
                sprite,
                trackName,
                new Vector2(tooltipBounds.X + TooltipPadding, tooltipBounds.Y + TooltipPadding),
                Color.White,
                TooltipScale,
                _font);
        }

        private void UpdateAnchoredPosition(RenderParameters renderParameters)
        {
            // CUIRadio::CreateLayer anchors the widget to Origin_RT, then applies
            // x = -3 - width - (bLeft ? 40 : 0), y = +3.
            int frameWidth = ResolveIndicatorTexture(Environment.TickCount)?.Width ?? _inactiveTexture.Width;
            int rightInset = ClientRightInset + ((_clientLeftInsetProvider?.Invoke() == true) ? ClientLeftModeExtraRightInset : 0);
            Position = new Point(
                Math.Max(0, renderParameters.RenderWidth - frameWidth - rightInset),
                ClientTopInset);
        }

        private Texture2D ResolveIndicatorTexture(int tickCount)
        {
            if (_indicatorActiveProvider?.Invoke() == true && _activeFrames.Count > 0)
            {
                int totalDelay = 0;
                for (int i = 0; i < _activeFrames.Count; i++)
                {
                    totalDelay += _activeFrames[i].DelayMs;
                }

                if (totalDelay <= 0)
                {
                    return _activeFrames[0].Texture;
                }

                int animationTime = Math.Abs(tickCount % totalDelay);
                for (int i = 0; i < _activeFrames.Count; i++)
                {
                    if (animationTime < _activeFrames[i].DelayMs)
                    {
                        return _activeFrames[i].Texture;
                    }

                    animationTime -= _activeFrames[i].DelayMs;
                }

                return _activeFrames[_activeFrames.Count - 1].Texture;
            }

            return _inactiveTexture;
        }

        private IDXObject CacheFrame(Texture2D texture)
        {
            if (!_frameCache.TryGetValue(texture, out IDXObject frame))
            {
                frame = new DXObject(0, 0, texture, 0);
                _frameCache[texture] = frame;
            }

            return frame;
        }

        private static IDXObject CreateFrameObject(Texture2D inactiveTexture, IReadOnlyList<UtilityPanelWindow.IndicatorFrame> activeFrames)
        {
            Texture2D initialTexture = inactiveTexture;
            if (initialTexture == null && activeFrames != null)
            {
                for (int i = 0; i < activeFrames.Count; i++)
                {
                    if (activeFrames[i].Texture != null)
                    {
                        initialTexture = activeFrames[i].Texture;
                        break;
                    }
                }
            }

            if (initialTexture == null)
            {
                throw new ArgumentException("RadioStatusWindow requires at least one indicator texture.");
            }

            return new DXObject(0, 0, initialTexture, 0);
        }
    }
}
