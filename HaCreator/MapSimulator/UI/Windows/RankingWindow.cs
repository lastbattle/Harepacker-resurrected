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
    internal sealed class RankingWindow : UIWindowBase
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

        private readonly List<PageLayer> _layers = new();
        private readonly Texture2D _highlightTexture;
        private readonly string _windowName;
        private SpriteFont _font;
        private Func<RankingWindowSnapshot> _snapshotProvider;

        public RankingWindow(IDXObject frame, string windowName, Texture2D highlightTexture)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _highlightTexture = highlightTexture ?? throw new ArgumentNullException(nameof(highlightTexture));
        }

        public override string WindowName => _windowName;

        public void AddLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _layers.Add(new PageLayer(layer, offset));
            }
        }

        public void SetSnapshotProvider(Func<RankingWindowSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            return base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
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

            RankingWindowSnapshot snapshot = _snapshotProvider?.Invoke() ?? new RankingWindowSnapshot();
            sprite.DrawString(_font, snapshot.Title, new Vector2(Position.X + 18, Position.Y + 16), Color.White);
            DrawWrappedText(sprite, snapshot.Subtitle, Position.X + 18, Position.Y + 38, Math.Max(220f, (CurrentFrame?.Width ?? 303) - 36f), new Color(220, 220, 220));

            float entriesTop = Position.Y + 78f;
            if (snapshot.Entries.Count == 0)
            {
                DrawWrappedText(sprite, "Ranking owner art is loaded, but no simulator-side ranking data is currently available.", Position.X + 18, (int)entriesTop, Math.Max(220f, (CurrentFrame?.Width ?? 303) - 36f), new Color(224, 224, 224));
            }
            else
            {
                int maxVisibleEntries = Math.Min(snapshot.Entries.Count, 3);
                for (int i = 0; i < maxVisibleEntries; i++)
                {
                    Rectangle bounds = GetEntryBounds(i);
                    Color fillColor = i == 0
                        ? new Color(94, 123, 188, 210)
                        : new Color(34, 42, 60, 210);
                    sprite.Draw(_highlightTexture, bounds, fillColor);

                    RankingEntrySnapshot entry = snapshot.Entries[i];
                    sprite.DrawString(_font, entry.Label, new Vector2(bounds.X + 10, bounds.Y + 6), new Color(255, 228, 151));
                    sprite.DrawString(_font, entry.Value, new Vector2(bounds.X + 10, bounds.Y + 24), Color.White);
                    DrawWrappedText(sprite, entry.Detail, bounds.X + 10, bounds.Y + 42, bounds.Width - 16f, new Color(215, 215, 215));
                }
            }

            if (!string.IsNullOrWhiteSpace(snapshot.StatusText))
            {
                DrawWrappedText(
                    sprite,
                    snapshot.StatusText,
                    Position.X + 18,
                    Position.Y + Math.Max(0, (CurrentFrame?.Height ?? 298) - (_font.LineSpacing * 3) - 12),
                    Math.Max(220f, (CurrentFrame?.Width ?? 303) - 36f),
                    new Color(255, 228, 151));
            }
        }

        private Rectangle GetEntryBounds(int index)
        {
            int width = Math.Max(240, (CurrentFrame?.Width ?? 303) - 24);
            return new Rectangle(Position.X + 12, Position.Y + 78 + (index * 62), width, 60);
        }

        private void DrawWrappedText(SpriteBatch sprite, string text, int x, int y, float maxWidth, Color color)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float drawY = y;
            foreach (string line in WrapText(text, maxWidth))
            {
                sprite.DrawString(_font, line, new Vector2(x, drawY), color);
                drawY += _font.LineSpacing;
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
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
