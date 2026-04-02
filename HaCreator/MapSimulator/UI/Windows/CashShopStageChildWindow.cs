using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CashShopStageChildWindow : UIWindowBase
    {
        private readonly struct LayerInfo
        {
            public LayerInfo(IDXObject layer, Point offset)
            {
                Layer = layer;
                Offset = offset;
            }

            public IDXObject Layer { get; }
            public Point Offset { get; }
        }

        private readonly string _windowName;
        private readonly string _title;
        private readonly List<LayerInfo> _layers = new();
        private readonly Dictionary<UIObject, Func<string>> _buttonActions = new();
        private readonly List<string> _fallbackLines = new();
        private SpriteFont _font;
        private Func<IReadOnlyList<string>> _contentProvider;
        private string _statusMessage = string.Empty;
        private Rectangle _contentBounds;
        private Point? _titlePositionOverride;

        public CashShopStageChildWindow(IDXObject frame, string windowName, string title)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _title = title ?? string.Empty;
            _contentBounds = new Rectangle(0, 0, CurrentFrame?.Width ?? 240, CurrentFrame?.Height ?? 140);
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
                _layers.Add(new LayerInfo(layer, offset));
            }
        }

        public void SetFallbackLines(params string[] lines)
        {
            _fallbackLines.Clear();
            if (lines == null)
            {
                return;
            }

            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _fallbackLines.Add(line);
                }
            }
        }

        public void SetContentProvider(Func<IReadOnlyList<string>> contentProvider)
        {
            _contentProvider = contentProvider;
        }

        public void SetStatusMessage(string statusMessage)
        {
            _statusMessage = statusMessage?.Trim() ?? string.Empty;
        }

        public void SetContentBounds(Rectangle contentBounds)
        {
            _contentBounds = contentBounds;
        }

        public void SetTitlePosition(Point titlePosition)
        {
            _titlePositionOverride = titlePosition;
        }

        public void RegisterButton(UIObject button, Func<string> action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            _buttonActions[button] = action;
            button.ButtonClickReleased += _ => HandleButtonAction(button);
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
            foreach (LayerInfo layer in _layers)
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

            Rectangle contentBounds = ResolveContentBounds();
            Vector2 titleOrigin = _titlePositionOverride.HasValue
                ? new Vector2(Position.X + _titlePositionOverride.Value.X, Position.Y + _titlePositionOverride.Value.Y)
                : new Vector2(Position.X + contentBounds.X + 12, Position.Y + contentBounds.Y + 10);
            sprite.DrawString(_font, _title, titleOrigin, Color.White);

            float lineY = titleOrigin.Y + _font.LineSpacing + 6f;
            IReadOnlyList<string> lines = _contentProvider?.Invoke();
            if (lines == null || lines.Count == 0)
            {
                lines = _fallbackLines;
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    lineY += 6f;
                    continue;
                }

                foreach (string wrappedLine in WrapText(line, Math.Max(180f, contentBounds.Width - 24f)))
                {
                    sprite.DrawString(
                        _font,
                        wrappedLine,
                        new Vector2(Position.X + contentBounds.X + 12, lineY),
                        new Color(225, 225, 225));
                    lineY += _font.LineSpacing;
                }

                lineY += 2f;
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                float footerY = Position.Y + Math.Max(
                    contentBounds.Y + 20,
                    contentBounds.Bottom - (_font.LineSpacing * 2) - 12);
                foreach (string wrappedLine in WrapText(_statusMessage, Math.Max(180f, contentBounds.Width - 24f)))
                {
                    sprite.DrawString(
                        _font,
                        wrappedLine,
                        new Vector2(Position.X + contentBounds.X + 12, footerY),
                        new Color(255, 223, 149));
                    footerY += _font.LineSpacing;
                }
            }
        }

        private Rectangle ResolveContentBounds()
        {
            int frameWidth = CurrentFrame?.Width ?? 240;
            int frameHeight = CurrentFrame?.Height ?? 140;
            if (_contentBounds.Width <= 0 || _contentBounds.Height <= 0)
            {
                return new Rectangle(0, 0, frameWidth, frameHeight);
            }

            return _contentBounds;
        }

        private void HandleButtonAction(UIObject button)
        {
            if (!_buttonActions.TryGetValue(button, out Func<string> action) || action == null)
            {
                return;
            }

            string message = action();
            if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message.Trim();
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
