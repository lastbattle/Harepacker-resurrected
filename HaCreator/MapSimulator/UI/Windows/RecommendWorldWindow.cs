using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public sealed class RecommendWorldEntry
    {
        public RecommendWorldEntry(int worldId, IEnumerable<string> messages)
        {
            WorldId = Math.Max(0, worldId);
            Messages = (messages ?? Array.Empty<string>())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Take(5)
                .ToArray();
        }

        public int WorldId { get; }
        public IReadOnlyList<string> Messages { get; }
        public string WorldLabel => $"World {WorldId}";
    }

    public sealed class RecommendWorldWindow : UIWindowBase
    {
        private const int MessageAreaWidth = 125;
        private readonly Texture2D _highlightTexture;
        private readonly UIObject _prevButton;
        private readonly UIObject _nextButton;
        private readonly UIObject _selectButton;
        private readonly UIObject _closeButton;
        private readonly List<RecommendWorldEntry> _entries = new();
        private SpriteFont _font;
        private int _selectedIndex;
        private bool _requestAllowed = true;
        private string _statusMessage;

        public RecommendWorldWindow(
            IDXObject frame,
            Texture2D highlightTexture,
            UIObject prevButton,
            UIObject nextButton,
            UIObject selectButton,
            UIObject closeButton)
            : base(frame)
        {
            _highlightTexture = highlightTexture;
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

        public void Configure(IReadOnlyList<RecommendWorldEntry> entries, int selectedIndex, bool requestAllowed, string statusMessage = null)
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
            _statusMessage = statusMessage;

            _prevButton?.SetEnabled(_entries.Count > 1 && _requestAllowed);
            _nextButton?.SetEnabled(_entries.Count > 1 && _requestAllowed);
            _selectButton?.SetEnabled(_entries.Count > 0 && _requestAllowed);
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
            if (_highlightTexture != null)
            {
                sprite.Draw(
                    _highlightTexture,
                    new Rectangle(Position.X + 38, Position.Y + 32, MessageAreaWidth, 32),
                    new Color(52, 70, 102, 200));
            }

            if (_font == null)
            {
                return;
            }

            RecommendWorldEntry selectedEntry = GetSelectedEntry();
            string worldLabel = selectedEntry?.WorldLabel ?? "No Recommendation";
            Vector2 labelSize = _font.MeasureString(worldLabel);
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                worldLabel,
                new Vector2(Position.X + 40 + Math.Max(0f, (MessageAreaWidth - labelSize.X) / 2f), Position.Y + 38),
                Color.White);

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _statusMessage,
                    new Vector2(Position.X + 18, Position.Y + 78),
                    _requestAllowed ? new Color(220, 220, 220) : new Color(255, 204, 107));
            }

            if (selectedEntry == null)
            {
                return;
            }

            int messageIndex = 0;
            foreach (string message in selectedEntry.Messages)
            {
                Vector2 messageSize = _font.MeasureString(message);
                float messageX = Position.X + 40 + Math.Max(0f, (MessageAreaWidth - messageSize.X) / 2f);
                float messageY = Position.Y + 110 + (messageIndex * 14);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    message,
                    new Vector2(messageX, messageY),
                    new Color(232, 232, 232));
                messageIndex++;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                $"{_selectedIndex + 1}/{_entries.Count}",
                new Vector2(Position.X + 90, Position.Y + 90),
                new Color(255, 228, 151));
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
            if (_font == null)
            {
                return;
            }

            DrawButtonLabel(sprite, _prevButton, "Prev");
            DrawButtonLabel(sprite, _nextButton, "Next");
            DrawButtonLabel(sprite, _selectButton, "Select");
            DrawButtonLabel(sprite, _closeButton, "Close");
        }

        private RecommendWorldEntry GetSelectedEntry()
        {
            return _selectedIndex >= 0 && _selectedIndex < _entries.Count
                ? _entries[_selectedIndex]
                : null;
        }

        private void DrawButtonLabel(SpriteBatch sprite, UIObject button, string text)
        {
            if (button == null || !button.ButtonVisible)
            {
                return;
            }

            Vector2 size = _font.MeasureString(text);
            float x = Position.X + button.X + ((button.CanvasSnapshotWidth - size.X) / 2f);
            float y = Position.Y + button.Y + ((button.CanvasSnapshotHeight - size.Y) / 2f) - 1f;
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, text, new Vector2(x, y), Color.White);
        }
    }
}
