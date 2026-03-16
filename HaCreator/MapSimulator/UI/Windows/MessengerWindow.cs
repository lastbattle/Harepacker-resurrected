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
    internal sealed class MessengerWindow : UIWindowBase
    {
        private const int SlotCount = 3;
        private static readonly Point[] SlotOrigins =
        {
            new(11, 28),
            new(103, 28),
            new(195, 28)
        };

        private readonly IDXObject _maximizedFrame;
        private readonly IDXObject _minimizedFrame;
        private readonly IDXObject _maxOverlay;
        private readonly Point _maxOverlayOffset;
        private readonly IDXObject _maxContentOverlay;
        private readonly Point _maxContentOffset;
        private readonly IDXObject _minOverlay;
        private readonly Point _minOverlayOffset;
        private readonly IDXObject _minContentOverlay;
        private readonly Point _minContentOffset;
        private readonly Texture2D[] _nameBarTextures;
        private readonly Texture2D _pixel;

        private SpriteFont _font;
        private MouseState _previousMouseState;
        private bool _isMinimized;
        private Func<MessengerSnapshot> _snapshotProvider;
        private Action<int> _slotSelectionHandler;
        private Func<string> _inviteHandler;
        private Func<string> _whisperHandler;
        private Action<string> _feedbackHandler;
        private UIObject _enterButton;
        private UIObject _claimButton;
        private UIObject _maximizeButton;
        private UIObject _minimizeButton;

        public MessengerWindow(
            IDXObject maximizedFrame,
            IDXObject minimizedFrame,
            IDXObject maxOverlay,
            Point maxOverlayOffset,
            IDXObject maxContentOverlay,
            Point maxContentOffset,
            IDXObject minOverlay,
            Point minOverlayOffset,
            IDXObject minContentOverlay,
            Point minContentOffset,
            Texture2D[] nameBarTextures,
            GraphicsDevice device)
            : base(maximizedFrame)
        {
            _maximizedFrame = maximizedFrame ?? throw new ArgumentNullException(nameof(maximizedFrame));
            _minimizedFrame = minimizedFrame ?? maximizedFrame;
            _maxOverlay = maxOverlay;
            _maxOverlayOffset = maxOverlayOffset;
            _maxContentOverlay = maxContentOverlay;
            _maxContentOffset = maxContentOffset;
            _minOverlay = minOverlay;
            _minOverlayOffset = minOverlayOffset;
            _minContentOverlay = minContentOverlay;
            _minContentOffset = minContentOffset;
            _nameBarTextures = nameBarTextures ?? Array.Empty<Texture2D>();
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });

            RefreshFrame();
        }

        public override string WindowName => MapSimulatorWindowNames.Messenger;

        internal void SetSnapshotProvider(Func<MessengerSnapshot> provider)
        {
            _snapshotProvider = provider;
        }

        internal void SetActionHandlers(
            Action<int> slotSelectionHandler,
            Func<string> inviteHandler,
            Func<string> whisperHandler,
            Action<string> feedbackHandler)
        {
            _slotSelectionHandler = slotSelectionHandler;
            _inviteHandler = inviteHandler;
            _whisperHandler = whisperHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void InitializeControls(
            UIObject enterButton,
            UIObject claimButton,
            UIObject maximizeButton,
            UIObject minimizeButton)
        {
            _enterButton = enterButton;
            _claimButton = claimButton;
            _maximizeButton = maximizeButton;
            _minimizeButton = minimizeButton;

            ConfigureButton(_enterButton, HandleInvite);
            ConfigureButton(_claimButton, HandleWhisper);
            ConfigureButton(_maximizeButton, () => SetMinimized(false));
            ConfigureButton(_minimizeButton, () => SetMinimized(true));

            UpdateButtonStates(GetSnapshot());
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            MessengerSnapshot snapshot = GetSnapshot();
            UpdateButtonStates(snapshot);

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released
                && _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                int slot = GetSlotIndexAt(mouseState.X, mouseState.Y);
                if (slot >= 0)
                {
                    _slotSelectionHandler?.Invoke(slot);
                }
            }

            _previousMouseState = mouseState;
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
            IDXObject overlay = _isMinimized ? _minOverlay : _maxOverlay;
            Point overlayOffset = _isMinimized ? _minOverlayOffset : _maxOverlayOffset;
            IDXObject contentOverlay = _isMinimized ? _minContentOverlay : _maxContentOverlay;
            Point contentOffset = _isMinimized ? _minContentOffset : _maxContentOffset;

            DrawLayer(sprite, overlay, overlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, contentOverlay, contentOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            if (_font == null)
            {
                return;
            }

            MessengerSnapshot snapshot = GetSnapshot();
            sprite.DrawString(_font, "Messenger", new Vector2(Position.X + 26, Position.Y + 7), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

            DrawParticipantSlots(sprite, snapshot);
            DrawLogEntries(sprite, snapshot, contentOffset);
        }

        private void ConfigureButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            if (action != null)
            {
                button.ButtonClickReleased += _ => action();
            }
        }

        private void HandleInvite()
        {
            ShowFeedback(_inviteHandler?.Invoke());
        }

        private void HandleWhisper()
        {
            ShowFeedback(_whisperHandler?.Invoke());
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private void SetMinimized(bool minimized)
        {
            _isMinimized = minimized;
            RefreshFrame();
            UpdateButtonStates(GetSnapshot());
        }

        private void RefreshFrame()
        {
            Frame = _isMinimized ? _minimizedFrame : _maximizedFrame;
        }

        private MessengerSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new MessengerSnapshot();
        }

        private void UpdateButtonStates(MessengerSnapshot snapshot)
        {
            _maximizeButton?.SetVisible(_isMinimized);
            _minimizeButton?.SetVisible(!_isMinimized);

            if (_enterButton != null)
            {
                _enterButton.SetButtonState(snapshot.CanInvite ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_claimButton != null)
            {
                _claimButton.SetButtonState(snapshot.CanWhisper ? UIObjectState.Normal : UIObjectState.Disabled);
            }
        }

        private void DrawParticipantSlots(SpriteBatch sprite, MessengerSnapshot snapshot)
        {
            IReadOnlyList<MessengerParticipantSnapshot> participants = snapshot.Participants ?? Array.Empty<MessengerParticipantSnapshot>();
            for (int i = 0; i < SlotCount; i++)
            {
                Point origin = SlotOrigins[i];
                Rectangle slotBounds = new Rectangle(Position.X + origin.X, Position.Y + origin.Y, 89, _isMinimized ? 74 : 84);
                bool selected = snapshot.SelectedSlot == i;

                Texture2D nameBar = i < _nameBarTextures.Length ? _nameBarTextures[i] : null;
                if (nameBar != null)
                {
                    sprite.Draw(nameBar, new Vector2(slotBounds.X, slotBounds.Y), selected ? Color.White : new Color(220, 220, 220, 235));
                }
                else
                {
                    sprite.Draw(_pixel, new Rectangle(slotBounds.X, slotBounds.Y, slotBounds.Width, 19), selected ? new Color(96, 143, 207) : new Color(60, 74, 95));
                }

                Rectangle cardBounds = new Rectangle(slotBounds.X, slotBounds.Y + 21, slotBounds.Width, slotBounds.Height - 21);
                sprite.Draw(_pixel, cardBounds, selected ? new Color(43, 75, 122, 180) : new Color(11, 18, 30, 170));

                MessengerParticipantSnapshot participant = i < participants.Count ? participants[i] : null;
                if (participant == null)
                {
                    DrawCentered(sprite, "(empty)", slotBounds.X, slotBounds.Y + 4, slotBounds.Width, new Color(181, 188, 199), 0.42f);
                    DrawCentered(sprite, "Invite", cardBounds.X, cardBounds.Y + 18, cardBounds.Width, new Color(213, 217, 225), 0.42f);
                    continue;
                }

                DrawCentered(sprite, Truncate(participant.Name, 11), slotBounds.X, slotBounds.Y + 4, slotBounds.Width, participant.IsLocalPlayer ? new Color(255, 242, 178) : Color.White, 0.42f);
                DrawCentered(sprite, participant.IsLocalPlayer ? "You" : $"CH {participant.Channel}", cardBounds.X, cardBounds.Y + 8, cardBounds.Width, new Color(156, 228, 188), 0.4f);
                DrawCentered(sprite, Truncate(participant.LocationSummary, 13), cardBounds.X, cardBounds.Y + 22, cardBounds.Width, new Color(224, 228, 235), 0.38f);
                if (!_isMinimized)
                {
                    DrawCentered(sprite, Truncate(participant.StatusText, 15), cardBounds.X, cardBounds.Y + 40, cardBounds.Width, new Color(197, 205, 216), 0.35f);
                }
            }
        }

        private void DrawLogEntries(SpriteBatch sprite, MessengerSnapshot snapshot, Point contentOffset)
        {
            Rectangle panelBounds = new Rectangle(
                Position.X + contentOffset.X + 8,
                Position.Y + contentOffset.Y + 8,
                258,
                _isMinimized ? 130 : 185);

            sprite.Draw(_pixel, panelBounds, new Color(7, 12, 20, 160));

            IReadOnlyList<MessengerLogEntrySnapshot> logEntries = snapshot.LogEntries ?? Array.Empty<MessengerLogEntrySnapshot>();
            int visibleEntries = _isMinimized ? 5 : 8;
            int startIndex = Math.Max(0, logEntries.Count - visibleEntries);
            int y = panelBounds.Y + 8;
            for (int i = startIndex; i < logEntries.Count; i++)
            {
                MessengerLogEntrySnapshot entry = logEntries[i];
                Color color = entry.IsSystem ? new Color(255, 227, 150) : new Color(220, 227, 238);
                string line = entry.IsSystem
                    ? entry.Message
                    : $"{entry.Author}: {entry.Message}";

                foreach (string wrappedLine in WrapText(line, panelBounds.Width - 10))
                {
                    if (y > panelBounds.Bottom - (_font.LineSpacing * 0.45f))
                    {
                        return;
                    }

                    sprite.DrawString(_font, wrappedLine, new Vector2(panelBounds.X + 5, y), color, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
                    y += (int)(_font.LineSpacing * 0.5f);
                }

                y += 3;
            }
        }

        private int GetSlotIndexAt(int mouseX, int mouseY)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                Rectangle bounds = new Rectangle(Position.X + SlotOrigins[i].X, Position.Y + SlotOrigins[i].Y, 89, _isMinimized ? 74 : 84);
                if (bounds.Contains(mouseX, mouseY))
                {
                    return i;
                }
            }

            return -1;
        }

        private void DrawLayer(
            SpriteBatch sprite,
            IDXObject layer,
            Point offset,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            if (layer == null)
            {
                return;
            }

            layer.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + offset.X,
                Position.Y + offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private void DrawCentered(SpriteBatch sprite, string text, int x, int y, int width, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = _font.MeasureString(text) * scale;
            float drawX = x + Math.Max(0f, (width - size.X) * 0.5f);
            sprite.DrawString(_font, text, new Vector2(drawX, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X * 0.4f > maxWidth)
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

        private static string Truncate(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
            {
                return value ?? string.Empty;
            }

            return $"{value.Substring(0, Math.Max(0, maxChars - 3))}...";
        }
    }
}
