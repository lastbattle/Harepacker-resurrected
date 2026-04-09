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
        private const int IndicatorFadeDurationMs = 100;
        private const float TooltipScale = 0.38f;
        private const int TooltipPadding = 6;
        private const int TooltipOffsetY = 20;
        private const int TooltipMaxWidth = 360;
        private const int TooltipMinWidth = 220;
        private const float TooltipLineGap = 2f;
        private const float TooltipSectionGap = 4f;

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
        private Func<bool> _indicatorPlaybackProvider;
        private Func<bool> _indicatorMutedProvider;
        private Func<bool> _clientLeftInsetProvider;
        private Func<int?> _indicatorAnimationStartTickProvider;
        private Func<string> _trackNameProvider;
        private Func<IReadOnlyList<string>> _detailLinesProvider;
        private Func<string> _footerProvider;
        private float _activeOverlayAlpha;
        private float _inactiveLayerAlpha = 1f;
        private bool? _lastFadingInActiveLayer;
        private float _fadeInStartAlpha;
        private int _layerFadeStartTick = int.MinValue;

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
            Frame = CacheFrame(_inactiveTexture);
        }

        public override string WindowName => _windowName;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        internal void SetIndicatorActiveProvider(Func<bool> indicatorActiveProvider)
        {
            _indicatorPlaybackProvider = indicatorActiveProvider;
        }

        internal void SetIndicatorMutedProvider(Func<bool> indicatorMutedProvider)
        {
            _indicatorMutedProvider = indicatorMutedProvider;
        }

        internal void SetClientLeftInsetProvider(Func<bool> clientLeftInsetProvider)
        {
            _clientLeftInsetProvider = clientLeftInsetProvider;
        }

        internal void SetIndicatorAnimationStartTickProvider(Func<int?> indicatorAnimationStartTickProvider)
        {
            _indicatorAnimationStartTickProvider = indicatorAnimationStartTickProvider;
        }

        internal void SetTrackNameProvider(Func<string> trackNameProvider)
        {
            _trackNameProvider = trackNameProvider;
        }

        internal void SetDetailLinesProvider(Func<IReadOnlyList<string>> detailLinesProvider)
        {
            _detailLinesProvider = detailLinesProvider;
        }

        internal void SetFooterProvider(Func<string> footerProvider)
        {
            _footerProvider = footerProvider;
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
            UpdateIndicatorOverlayState(TickCount);
            Frame = CacheFrame(_inactiveTexture);
            sprite.Draw(_inactiveTexture, Position.ToVector2(), Color.White * _inactiveLayerAlpha);
            DrawContents(
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
            DrawOverlay(
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

            MouseState mouse = Mouse.GetState();
            if (!GetWindowBounds().Contains(mouse.X, mouse.Y))
            {
                return;
            }

            List<string> wrappedLines = BuildWrappedTooltipLines();
            string footer = _footerProvider?.Invoke();
            if (wrappedLines.Count == 0 && string.IsNullOrWhiteSpace(footer))
            {
                return;
            }

            float maxLineWidth = 0f;
            float lineHeight = WindowLineSpacing * TooltipScale;
            for (int i = 0; i < wrappedLines.Count; i++)
            {
                maxLineWidth = Math.Max(maxLineWidth, MeasureWindowText(sprite, wrappedLines[i], TooltipScale).X);
            }

            if (!string.IsNullOrWhiteSpace(footer))
            {
                maxLineWidth = Math.Max(maxLineWidth, MeasureWindowText(sprite, footer, TooltipScale).X);
            }

            int tooltipWidth = Math.Max(
                TooltipMinWidth,
                Math.Min(TooltipMaxWidth, (int)Math.Ceiling(maxLineWidth) + (TooltipPadding * 2)));
            float bodyHeight = wrappedLines.Count > 0
                ? (wrappedLines.Count * lineHeight) + (Math.Max(0, wrappedLines.Count - 1) * TooltipLineGap)
                : 0f;
            float footerHeight = string.IsNullOrWhiteSpace(footer) ? 0f : lineHeight;
            float footerGap = wrappedLines.Count > 0 && footerHeight > 0f ? TooltipSectionGap : 0f;
            int tooltipHeight = (int)Math.Ceiling(bodyHeight + footerHeight + footerGap) + (TooltipPadding * 2);
            int tooltipX = Math.Max(0, Math.Min(mouse.X, Math.Max(0, renderParameters.RenderWidth - tooltipWidth)));
            int tooltipY = Math.Max(0, Math.Min(mouse.Y + TooltipOffsetY, Math.Max(0, renderParameters.RenderHeight - tooltipHeight)));
            Rectangle tooltipBounds = new(tooltipX, tooltipY, tooltipWidth, tooltipHeight);

            sprite.Draw(_pixel, tooltipBounds, new Color(24, 24, 24, 235));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X, tooltipBounds.Y, tooltipBounds.Width, 1), new Color(255, 228, 151));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X, tooltipBounds.Bottom - 1, tooltipBounds.Width, 1), new Color(255, 228, 151));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X, tooltipBounds.Y, 1, tooltipBounds.Height), new Color(255, 228, 151));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.Right - 1, tooltipBounds.Y, 1, tooltipBounds.Height), new Color(255, 228, 151));
            float y = tooltipBounds.Y + TooltipPadding;
            for (int i = 0; i < wrappedLines.Count; i++)
            {
                DrawWindowText(
                    sprite,
                    wrappedLines[i],
                    new Vector2(tooltipBounds.X + TooltipPadding, y),
                    Color.White,
                    TooltipScale,
                    tooltipBounds.Width - (TooltipPadding * 2));
                y += lineHeight + TooltipLineGap;
            }

            if (!string.IsNullOrWhiteSpace(footer))
            {
                if (wrappedLines.Count > 0)
                {
                    y += TooltipSectionGap - TooltipLineGap;
                }

                DrawWindowText(
                    sprite,
                    footer,
                    new Vector2(tooltipBounds.X + TooltipPadding, y),
                    new Color(255, 228, 151),
                    TooltipScale,
                    tooltipBounds.Width - (TooltipPadding * 2));
            }
        }

        protected override void DrawOverlay(
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
            if (_activeOverlayAlpha <= 0f)
            {
                return;
            }

            Texture2D activeTexture = ResolveActiveIndicatorTexture(TickCount);
            if (activeTexture == null)
            {
                return;
            }

            sprite.Draw(activeTexture, Position.ToVector2(), Color.White * _activeOverlayAlpha);
        }

        private void UpdateAnchoredPosition(RenderParameters renderParameters)
        {
            // CUIRadio::CreateLayer anchors the widget to Origin_RT, then applies
            // x = -3 - width - (bLeft ? 40 : 0), y = +3. IDA also recovers
            // nMargin = (CWvsContext slot 3562 != 0) ? 40 : 0 in the ctor path.
            // The client positions from the Off-layer canvas width, not the
            // current animated On frame width.
            int frameWidth = _inactiveTexture.Width;
            int rightInset = ClientRightInset + ((_clientLeftInsetProvider?.Invoke() == true) ? ClientLeftModeExtraRightInset : 0);
            Position = new Point(
                renderParameters.RenderWidth - frameWidth - rightInset,
                ClientTopInset);
        }

        private List<string> BuildWrappedTooltipLines()
        {
            List<string> lines = new();
            IReadOnlyList<string> detailLines = _detailLinesProvider?.Invoke();
            if (detailLines != null)
            {
                for (int i = 0; i < detailLines.Count; i++)
                {
                    AppendWrappedTooltipLine(lines, detailLines[i]);
                }
            }

            if (lines.Count == 0)
            {
                AppendWrappedTooltipLine(lines, _trackNameProvider?.Invoke());
            }

            return lines;
        }

        private void AppendWrappedTooltipLine(List<string> lines, string text)
        {
            if (lines == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                lines.Add(string.Empty);
                return;
            }

            foreach (string wrappedLine in WrapTooltipText(text, TooltipMaxWidth - (TooltipPadding * 2)))
            {
                lines.Add(wrappedLine);
            }
        }

        private IEnumerable<string> WrapTooltipText(string text, float maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            for (int i = 0; i < words.Length; i++)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? words[i] : $"{currentLine} {words[i]}";
                if (!string.IsNullOrEmpty(currentLine)
                    && MeasureWindowText(null, candidate, TooltipScale).X > maxWidth)
                {
                    yield return currentLine;
                    currentLine = words[i];
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }

        private void UpdateIndicatorOverlayState(int tickCount)
        {
            bool ownsPlayback = _indicatorPlaybackProvider?.Invoke() == true;
            bool muted = _indicatorMutedProvider?.Invoke() == true;
            bool fadeInActiveLayer = ShouldFadeInActiveLayer(ownsPlayback, muted);
            if (_lastFadingInActiveLayer != fadeInActiveLayer)
            {
                _fadeInStartAlpha = fadeInActiveLayer ? _activeOverlayAlpha : _inactiveLayerAlpha;
                _layerFadeStartTick = tickCount;
                _lastFadingInActiveLayer = fadeInActiveLayer;
            }

            int elapsedMs = _layerFadeStartTick == int.MinValue
                ? IndicatorFadeDurationMs
                : Math.Max(0, tickCount - _layerFadeStartTick);
            float fadeInAlpha = StepIndicatorFadeInAlpha(_fadeInStartAlpha, elapsedMs);
            (float activeAlpha, float inactiveAlpha) = ResolveIndicatorLayerAlphas(fadeInAlpha, fadeInActiveLayer);
            _activeOverlayAlpha = activeAlpha;
            _inactiveLayerAlpha = inactiveAlpha;
        }

        private Texture2D ResolveActiveIndicatorTexture(int tickCount)
        {
            if (_activeFrames.Count > 0)
            {
                List<int> frameDelays = new(_activeFrames.Count);
                for (int i = 0; i < _activeFrames.Count; i++)
                {
                    frameDelays.Add(_activeFrames[i].DelayMs);
                }

                int frameIndex = ResolveAnimatedFrameIndex(
                    tickCount,
                    _indicatorAnimationStartTickProvider?.Invoke(),
                    frameDelays);
                if ((uint)frameIndex < (uint)_activeFrames.Count)
                {
                    return _activeFrames[frameIndex].Texture;
                }

                return _activeFrames[0].Texture;
            }

            return null;
        }

        internal static int ResolveAnimatedFrameIndex(int tickCount, int? animationStartTick, IReadOnlyList<int> frameDelays)
        {
            if (frameDelays == null || frameDelays.Count == 0)
            {
                return -1;
            }

            int totalDelay = 0;
            for (int i = 0; i < frameDelays.Count; i++)
            {
                totalDelay += Math.Max(1, frameDelays[i]);
            }

            if (totalDelay <= 0)
            {
                return 0;
            }

            int elapsedAnimationMs = animationStartTick.HasValue
                ? Math.Max(0, tickCount - animationStartTick.Value)
                : Math.Abs(tickCount);
            int animationTime = elapsedAnimationMs % totalDelay;
            for (int i = 0; i < frameDelays.Count; i++)
            {
                int frameDelay = Math.Max(1, frameDelays[i]);
                if (animationTime < frameDelay)
                {
                    return i;
                }

                animationTime -= frameDelay;
            }

            return frameDelays.Count - 1;
        }

        internal static bool ShouldFadeInActiveLayer(bool ownsPlayback, bool muted)
        {
            return ownsPlayback && !muted;
        }

        internal static float StepIndicatorFadeInAlpha(float currentAlpha, int elapsedMs)
        {
            if (elapsedMs <= 0)
            {
                return currentAlpha;
            }

            if (elapsedMs >= IndicatorFadeDurationMs)
            {
                return 1f;
            }

            float step = elapsedMs / (float)IndicatorFadeDurationMs;
            return MathHelper.Clamp(currentAlpha + ((1f - currentAlpha) * step), 0f, 1f);
        }

        internal static (float ActiveAlpha, float InactiveAlpha) ResolveIndicatorLayerAlphas(float fadeInAlpha, bool fadeInActiveLayer)
        {
            float normalizedAlpha = MathHelper.Clamp(fadeInAlpha, 0f, 1f);
            return fadeInActiveLayer
                ? (normalizedAlpha, 1f - normalizedAlpha)
                : (1f - normalizedAlpha, normalizedAlpha);
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
