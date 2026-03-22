using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    public sealed class RecommendWorldEntry
    {
        public RecommendWorldEntry(int worldId, string message)
        {
            WorldId = Math.Max(0, worldId);
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public int WorldId { get; }
        public string Message { get; }
        public string WorldLabel => $"World {WorldId}";
    }

    public sealed class RecommendWorldWindow : UIWindowBase
    {
        private const int MessageAreaWidth = 125;
        private readonly IReadOnlyDictionary<int, Texture2D> _worldNameTextures;
        private readonly UIObject _prevButton;
        private readonly UIObject _nextButton;
        private readonly UIObject _selectButton;
        private readonly UIObject _closeButton;
        private readonly List<RecommendWorldEntry> _entries = new();
        private SpriteFont _font;
        private int _selectedIndex;
        private bool _requestAllowed = true;

        public RecommendWorldWindow(
            IDXObject frame,
            IReadOnlyDictionary<int, Texture2D> worldNameTextures,
            UIObject prevButton,
            UIObject nextButton,
            UIObject selectButton,
            UIObject closeButton)
            : base(frame)
        {
            _worldNameTextures = worldNameTextures ?? new Dictionary<int, Texture2D>();
            _prevButton = prevButton;
            _nextButton = nextButton;
            _selectButton = selectButton;
            _closeButton = closeButton;

            if (_prevButton != null)
            {
                _prevButton.ButtonClickReleased += _ => PreviousRequested?.Invoke();
                AddButton(_prevButton);
            }

            if (_nextButton != null)
            {
                _nextButton.ButtonClickReleased += _ => NextRequested?.Invoke();
                AddButton(_nextButton);
            }

            if (_selectButton != null)
            {
                _selectButton.ButtonClickReleased += _ =>
                {
                    RecommendWorldEntry selectedEntry = GetSelectedEntry();
                    if (selectedEntry != null)
                    {
                        SelectRequested?.Invoke(selectedEntry.WorldId);
                    }
                };
                AddButton(_selectButton);
            }

            if (_closeButton != null)
            {
                _closeButton.ButtonClickReleased += _ =>
                {
                    Hide();
                    CloseRequested?.Invoke();
                };
                AddButton(_closeButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.RecommendWorld;

        public event Action PreviousRequested;
        public event Action NextRequested;
        public event Action<int> SelectRequested;
        public event Action CloseRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Configure(IReadOnlyList<RecommendWorldEntry> entries, int selectedIndex, bool requestAllowed)
        {
            _entries.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries.Where(entry => entry != null));
            }

            _selectedIndex = _entries.Count == 0
                ? -1
                : Math.Clamp(selectedIndex, 0, _entries.Count - 1);
            _requestAllowed = requestAllowed;

            _prevButton?.SetEnabled(_entries.Count > 1 && _requestAllowed);
            _nextButton?.SetEnabled(_entries.Count > 1 && _requestAllowed);
            _selectButton?.SetEnabled(_entries.Count > 0 && _requestAllowed);
            _closeButton?.SetEnabled(_requestAllowed);
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

            RecommendWorldEntry selectedEntry = GetSelectedEntry();
            if (selectedEntry != null &&
                _worldNameTextures.TryGetValue(selectedEntry.WorldId, out Texture2D worldTexture) &&
                worldTexture != null)
            {
                sprite.Draw(worldTexture, new Vector2(Position.X + 34, Position.Y + 35), Color.White);
            }
            else
            {
                string worldLabel = selectedEntry?.WorldLabel ?? "World";
                Vector2 labelSize = _font.MeasureString(worldLabel);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    worldLabel,
                    new Vector2(Position.X + 40 + Math.Max(0f, (MessageAreaWidth - labelSize.X) / 2f), Position.Y + 38),
                    Color.White);
            }

            if (selectedEntry == null)
            {
                return;
            }

            int lineHeight = Math.Max(1, _font.LineSpacing);
            int maxLineCount = Math.Max(1, 70 / lineHeight);
            IReadOnlyList<string> lines = WrapMessage(selectedEntry.Message, maxLineCount);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                Vector2 lineSize = _font.MeasureString(line);
                float lineX = Position.X + 40 + Math.Max(0f, (MessageAreaWidth - lineSize.X) / 2f);
                float lineY = Position.Y + 110 + (i * lineHeight);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    line,
                    new Vector2(lineX, lineY),
                    new Color(232, 232, 232));
            }
        }

        private RecommendWorldEntry GetSelectedEntry()
        {
            return _selectedIndex >= 0 && _selectedIndex < _entries.Count
                ? _entries[_selectedIndex]
                : null;
        }

        private IReadOnlyList<string> WrapMessage(string message, int maxLineCount)
        {
            if (string.IsNullOrWhiteSpace(message) || _font == null || maxLineCount <= 0)
            {
                return Array.Empty<string>();
            }

            List<string> lines = new();
            foreach (string paragraph in message
                         .Replace("\r\n", "\n", StringComparison.Ordinal)
                         .Replace('\r', '\n')
                         .Split('\n'))
            {
                AppendWrappedParagraph(lines, paragraph.Trim(), maxLineCount);
                if (lines.Count >= maxLineCount)
                {
                    break;
                }
            }

            return lines;
        }

        private void AppendWrappedParagraph(List<string> lines, string paragraph, int maxLineCount)
        {
            if (lines.Count >= maxLineCount)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(paragraph))
            {
                if (lines.Count == 0)
                {
                    lines.Add(string.Empty);
                }

                return;
            }

            string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder builder = new();
            foreach (string word in words)
            {
                string candidate = builder.Length == 0
                    ? word
                    : $"{builder} {word}";
                if (_font.MeasureString(candidate).X <= MessageAreaWidth)
                {
                    builder.Clear();
                    builder.Append(candidate);
                    continue;
                }

                if (builder.Length > 0)
                {
                    lines.Add(builder.ToString());
                    if (lines.Count >= maxLineCount)
                    {
                        return;
                    }

                    builder.Clear();
                }

                AppendBrokenWord(lines, word, maxLineCount, builder);
                if (lines.Count >= maxLineCount)
                {
                    return;
                }
            }

            if (builder.Length > 0 && lines.Count < maxLineCount)
            {
                lines.Add(builder.ToString());
            }
        }

        private void AppendBrokenWord(List<string> lines, string word, int maxLineCount, StringBuilder carry)
        {
            if (_font.MeasureString(word).X <= MessageAreaWidth)
            {
                carry.Append(word);
                return;
            }

            StringBuilder segment = new();
            foreach (char ch in word)
            {
                string candidate = segment.ToString() + ch;
                if (_font.MeasureString(candidate).X <= MessageAreaWidth || segment.Length == 0)
                {
                    segment.Append(ch);
                    continue;
                }

                lines.Add(segment.ToString());
                if (lines.Count >= maxLineCount)
                {
                    return;
                }

                segment.Clear();
                segment.Append(ch);
            }

            carry.Append(segment);
        }
    }
}
