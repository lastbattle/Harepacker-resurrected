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
        private SpriteFont _font;
        private Func<IReadOnlyList<string>> _contentProvider;
        private Func<string> _footerProvider;

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
