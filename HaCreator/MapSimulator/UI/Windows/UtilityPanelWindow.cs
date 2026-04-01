using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class UtilityPanelWindow : UIWindowBase
    {
        public readonly struct IndicatorFrame
        {
            public IndicatorFrame(Texture2D texture, int delayMs)
            {
                Texture = texture;
                DelayMs = Math.Max(1, delayMs);
            }

            public Texture2D Texture { get; }
            public int DelayMs { get; }
        }

        private readonly struct PageLayer
        {
            public PageLayer(IDXObject layer, Point offset)
            {
                Layer = layer;
                Offset = offset;
            }

            public IDXObject Layer { get; }
            public Point Offset { get; }
        }

        private readonly string _windowName;
        private readonly string _title;
        private readonly List<PageLayer> _layers = new();
        private readonly Dictionary<UIObject, Action> _buttonActions = new();
        private readonly List<string> _staticLines = new();
        private readonly List<IndicatorFrame> _activeIndicatorFrames = new();
        private SpriteFont _font;
        private Func<IReadOnlyList<string>> _contentProvider;
        private Func<string> _footerProvider;
        private Func<bool> _indicatorActiveProvider;
        private Texture2D _inactiveIndicatorTexture;
        private Point _indicatorOffset;

        public UtilityPanelWindow(IDXObject frame, string windowName, string title)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _title = title ?? string.Empty;
        }

        public override string WindowName => _windowName;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void AddLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _layers.Add(new PageLayer(layer, offset));
            }
        }

        public void SetStaticLines(params string[] lines)
        {
            _staticLines.Clear();
            if (lines == null)
            {
                return;
            }

            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _staticLines.Add(line);
                }
            }
        }

        public void SetContentProvider(Func<IReadOnlyList<string>> contentProvider)
        {
            _contentProvider = contentProvider;
        }

        public void SetFooterProvider(Func<string> footerProvider)
        {
            _footerProvider = footerProvider;
        }

        public void SetIndicatorFrames(Texture2D inactiveTexture, IReadOnlyList<IndicatorFrame> activeFrames, Point offset)
        {
            _inactiveIndicatorTexture = inactiveTexture;
            _indicatorOffset = offset;
            _activeIndicatorFrames.Clear();
            if (activeFrames == null)
            {
                return;
            }

            for (int i = 0; i < activeFrames.Count; i++)
            {
                if (activeFrames[i].Texture != null)
                {
                    _activeIndicatorFrames.Add(activeFrames[i]);
                }
            }
        }

        public void SetIndicatorActiveProvider(Func<bool> indicatorActiveProvider)
        {
            _indicatorActiveProvider = indicatorActiveProvider;
        }

        public void RegisterButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            _buttonActions[button] = action;
            button.ButtonClickReleased += HandleButtonReleased;
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
            foreach (PageLayer layer in _layers)
            {
                layer.Layer.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    Position.X + layer.Offset.X,
                    Position.Y + layer.Offset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }

            DrawIndicator(sprite, TickCount);

            if (_font == null)
            {
                return;
            }

            Vector2 origin = new(Position.X + 16, Position.Y + 16);
            sprite.DrawString(_font, _title, origin, Color.White);

            float y = origin.Y + _font.LineSpacing + 10;
            IReadOnlyList<string> lines = _contentProvider?.Invoke();
            if (lines == null || lines.Count == 0)
            {
                lines = _staticLines;
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    y += _font.LineSpacing * 0.5f;
                    continue;
                }

                foreach (string wrappedLine in WrapText(line, Math.Max(200f, (CurrentFrame?.Width ?? 320) - 32f)))
                {
                    sprite.DrawString(_font, wrappedLine, new Vector2(origin.X, y), new Color(224, 224, 224));
                    y += _font.LineSpacing;
                }

                y += 2f;
            }

            string footer = _footerProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(footer))
            {
                sprite.DrawString(
                    _font,
                    footer,
                    new Vector2(origin.X, Position.Y + Math.Max(24, (CurrentFrame?.Height ?? 180) - _font.LineSpacing - 12)),
                    new Color(255, 228, 151));
            }
        }

        private void HandleButtonReleased(UIObject button)
        {
            if (_buttonActions.TryGetValue(button, out Action action))
            {
                action?.Invoke();
            }
        }

        private void DrawIndicator(SpriteBatch sprite, int tickCount)
        {
            Texture2D indicatorTexture = ResolveIndicatorTexture(tickCount);
            if (indicatorTexture == null)
            {
                return;
            }

            int indicatorX = Position.X + Math.Max(8, (CurrentFrame?.Width ?? indicatorTexture.Width) - indicatorTexture.Width - 12) + _indicatorOffset.X;
            int indicatorY = Position.Y + 10 + _indicatorOffset.Y;
            sprite.Draw(indicatorTexture, new Vector2(indicatorX, indicatorY), Color.White);
        }

        private Texture2D ResolveIndicatorTexture(int tickCount)
        {
            if (_indicatorActiveProvider?.Invoke() == true && _activeIndicatorFrames.Count > 0)
            {
                int totalDelay = 0;
                for (int i = 0; i < _activeIndicatorFrames.Count; i++)
                {
                    totalDelay += _activeIndicatorFrames[i].DelayMs;
                }

                if (totalDelay <= 0)
                {
                    return _activeIndicatorFrames[0].Texture;
                }

                int time = Math.Abs(tickCount % totalDelay);
                for (int i = 0; i < _activeIndicatorFrames.Count; i++)
                {
                    if (time < _activeIndicatorFrames[i].DelayMs)
                    {
                        return _activeIndicatorFrames[i].Texture;
                    }

                    time -= _activeIndicatorFrames[i].DelayMs;
                }

                return _activeIndicatorFrames[_activeIndicatorFrames.Count - 1].Texture;
            }

            return _inactiveIndicatorTexture;
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                {
                    yield return currentLine;
                    currentLine = word;
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
    }
}
