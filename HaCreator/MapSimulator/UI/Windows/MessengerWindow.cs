using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class MessengerWindow : UIWindowBase
    {
        private const int SlotCount = 3;
        private const int ChatBalloonFrameDelayMs = 120;
        private static readonly Point[] SlotOrigins =
        {
            new(11, 28),
            new(103, 28),
            new(195, 28)
        };

        private readonly IDXObject _maximizedFrame;
        private readonly IDXObject _minimizedFrame;
        private readonly IDXObject _collapsedFrame;
        private readonly IDXObject _maxOverlay;
        private readonly Point _maxOverlayOffset;
        private readonly IDXObject _maxContentOverlay;
        private readonly Point _maxContentOffset;
        private readonly IDXObject _minOverlay;
        private readonly Point _minOverlayOffset;
        private readonly IDXObject _minContentOverlay;
        private readonly Point _minContentOffset;
        private readonly IDXObject _collapsedOverlay;
        private readonly Point _collapsedOverlayOffset;
        private readonly IDXObject _collapsedContentOverlay;
        private readonly Point _collapsedContentOffset;
        private readonly Texture2D[] _nameBarTextures;
        private readonly Texture2D[] _chatBalloonFrames;
        private readonly Texture2D _maxStatusIcon;
        private readonly Point _maxStatusIconPosition;
        private readonly Texture2D _minStatusIcon;
        private readonly Point _minStatusIconPosition;
        private readonly Texture2D _collapsedStatusIcon;
        private readonly Point _collapsedStatusIconPosition;
        private readonly Texture2D _pixel;
        private readonly StringBuilder _inputText = new(80);
        private readonly Point _maxEnterButtonPosition;
        private readonly Point _minEnterButtonPosition;
        private readonly Point _maxClaimButtonPosition;
        private readonly Point _minClaimButtonPosition;
        private readonly Point _maxMaximizeButtonPosition;
        private readonly Point _collapsedMaximizeButtonPosition;
        private readonly Point _maxMinimizeButtonPosition;
        private readonly Point _minMinimizeButtonPosition;
        private readonly Point _collapsedMinimizeButtonPosition;
        private readonly List<string> _inputHistory = new();

        private SpriteFont _font;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private MessengerWindowState _windowState;
        private bool _inputActive;
        private bool _whisperMode;
        private int _cursorPosition;
        private int _cursorBlinkTimer;
        private Keys _lastHeldKey = Keys.None;
        private int _keyHoldStartTime;
        private int _lastKeyRepeatTime;
        private Point? _clickStartPoint;
        private Func<MessengerSnapshot> _snapshotProvider;
        private Action<int> _slotSelectionHandler;
        private Func<string> _claimHandler;
        private Func<string> _claimNoticeHandler;
        private Func<string, string> _claimCategoryHandler;
        private Func<string> _claimCancelHandler;
        private Func<string> _leaveHandler;
        private Func<bool, string> _stateCycleHandler;
        private Func<string, string> _sendMessageHandler;
        private Func<string, string> _sendWhisperHandler;
        private Func<string> _requestExitPromptHandler;
        private Func<MessengerDeleteResult> _confirmExitPromptHandler;
        private Func<string> _cancelExitPromptHandler;
        private Func<MessengerDeleteResult> _closeRequestHandler;
        private Action _closeAcknowledgeHandler;
        private Action<string> _feedbackHandler;
        private UIObject _enterButton;
        private UIObject _claimButton;
        private UIObject _maximizeButton;
        private UIObject _minimizeButton;
        private MessengerSnapshot _currentSnapshot = new();
        private int _historyIndex = -1;
        private string _historyDraft = string.Empty;
        private bool _resumeInputAfterExitPrompt;
        private bool _resumeWhisperAfterExitPrompt;

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
            IDXObject collapsedFrame,
            IDXObject collapsedOverlay,
            Point collapsedOverlayOffset,
            IDXObject collapsedContentOverlay,
            Point collapsedContentOffset,
            Texture2D[] nameBarTextures,
            Texture2D[] chatBalloonFrames,
            Texture2D maxStatusIcon,
            Point maxStatusIconPosition,
            Texture2D minStatusIcon,
            Point minStatusIconPosition,
            Texture2D collapsedStatusIcon,
            Point collapsedStatusIconPosition,
            Point maxEnterButtonPosition,
            Point minEnterButtonPosition,
            Point maxClaimButtonPosition,
            Point minClaimButtonPosition,
            Point maxMaximizeButtonPosition,
            Point collapsedMaximizeButtonPosition,
            Point maxMinimizeButtonPosition,
            Point minMinimizeButtonPosition,
            Point collapsedMinimizeButtonPosition,
            GraphicsDevice device)
            : base(maximizedFrame)
        {
            _maximizedFrame = maximizedFrame ?? throw new ArgumentNullException(nameof(maximizedFrame));
            _minimizedFrame = minimizedFrame ?? maximizedFrame;
            _collapsedFrame = collapsedFrame ?? minimizedFrame ?? maximizedFrame;
            _maxOverlay = maxOverlay;
            _maxOverlayOffset = maxOverlayOffset;
            _maxContentOverlay = maxContentOverlay;
            _maxContentOffset = maxContentOffset;
            _minOverlay = minOverlay;
            _minOverlayOffset = minOverlayOffset;
            _minContentOverlay = minContentOverlay;
            _minContentOffset = minContentOffset;
            _collapsedOverlay = collapsedOverlay;
            _collapsedOverlayOffset = collapsedOverlayOffset;
            _collapsedContentOverlay = collapsedContentOverlay;
            _collapsedContentOffset = collapsedContentOffset;
            _nameBarTextures = nameBarTextures ?? Array.Empty<Texture2D>();
            _chatBalloonFrames = chatBalloonFrames ?? Array.Empty<Texture2D>();
            _maxStatusIcon = maxStatusIcon;
            _maxStatusIconPosition = maxStatusIconPosition;
            _minStatusIcon = minStatusIcon;
            _minStatusIconPosition = minStatusIconPosition;
            _collapsedStatusIcon = collapsedStatusIcon;
            _collapsedStatusIconPosition = collapsedStatusIconPosition;
            _maxEnterButtonPosition = maxEnterButtonPosition;
            _minEnterButtonPosition = minEnterButtonPosition;
            _maxClaimButtonPosition = maxClaimButtonPosition;
            _minClaimButtonPosition = minClaimButtonPosition;
            _maxMaximizeButtonPosition = maxMaximizeButtonPosition;
            _collapsedMaximizeButtonPosition = collapsedMaximizeButtonPosition;
            _maxMinimizeButtonPosition = maxMinimizeButtonPosition;
            _minMinimizeButtonPosition = minMinimizeButtonPosition;
            _collapsedMinimizeButtonPosition = collapsedMinimizeButtonPosition;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });

            RefreshFrame();
        }

        public override string WindowName => MapSimulatorWindowNames.Messenger;
        public override bool CapturesKeyboardInput => IsVisible && _inputActive;

        internal void SetSnapshotProvider(Func<MessengerSnapshot> provider)
        {
            _snapshotProvider = provider;
            _currentSnapshot = RefreshSnapshot();
        }

        internal void SetActionHandlers(
            Action<int> slotSelectionHandler,
            Func<string> claimHandler,
            Func<string> claimNoticeHandler,
            Func<string, string> claimCategoryHandler,
            Func<string> claimCancelHandler,
            Func<string> leaveHandler,
            Func<bool, string> stateCycleHandler,
            Func<string, string> sendMessageHandler,
            Func<string, string> sendWhisperHandler,
            Func<string> requestExitPromptHandler,
            Func<MessengerDeleteResult> confirmExitPromptHandler,
            Func<string> cancelExitPromptHandler,
            Func<MessengerDeleteResult> closeRequestHandler,
            Action closeAcknowledgeHandler,
            Action<string> feedbackHandler)
        {
            _slotSelectionHandler = slotSelectionHandler;
            _claimHandler = claimHandler;
            _claimNoticeHandler = claimNoticeHandler;
            _claimCategoryHandler = claimCategoryHandler;
            _claimCancelHandler = claimCancelHandler;
            _leaveHandler = leaveHandler;
            _stateCycleHandler = stateCycleHandler;
            _sendMessageHandler = sendMessageHandler;
            _sendWhisperHandler = sendWhisperHandler;
            _requestExitPromptHandler = requestExitPromptHandler;
            _confirmExitPromptHandler = confirmExitPromptHandler;
            _cancelExitPromptHandler = cancelExitPromptHandler;
            _closeRequestHandler = closeRequestHandler;
            _closeAcknowledgeHandler = closeAcknowledgeHandler;
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

            ConfigureButton(_enterButton, HandleEnterButton);
            ConfigureButton(_claimButton, HandleClaim);
            ConfigureButton(_maximizeButton, () => CycleState(false));
            ConfigureButton(_minimizeButton, () => CycleState(true));

            UpdateButtonStates(_currentSnapshot ?? RefreshSnapshot());
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            MessengerSnapshot snapshot = RefreshSnapshot();
            ApplyWindowState(snapshot.WindowState);
            UpdateButtonStates(snapshot);

            if (snapshot.ShouldCloseWindow)
            {
                Hide();
                _closeAcknowledgeHandler?.Invoke();
                _previousKeyboardState = Keyboard.GetState();
                _previousMouseState = Mouse.GetState();
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            if (snapshot.ShowExitPrompt)
            {
                HandleExitPromptInput(keyboardState);
            }
            else if (_inputActive)
            {
                HandleKeyboardInput(keyboardState, snapshot, Environment.TickCount);
            }
            else
            {
                ResetKeyRepeat();

                if (!IsCollapsed
                    && keyboardState.IsKeyDown(Keys.Enter)
                    && _previousKeyboardState.IsKeyUp(Keys.Enter))
                {
                    ActivateInput(whisperMode: false, snapshot, clearText: false);
                }
            }

            MouseState mouseState = Mouse.GetState();
            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed
                && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftReleased = mouseState.LeftButton == ButtonState.Released
                && _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftPressed && ContainsPoint(mouseState.X, mouseState.Y))
            {
                _clickStartPoint = new Point(mouseState.X, mouseState.Y);
            }

            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                bool treatAsClick = !_clickStartPoint.HasValue
                    || Vector2.Distance(new Vector2(_clickStartPoint.Value.X, _clickStartPoint.Value.Y), new Vector2(mouseState.X, mouseState.Y)) <= 4f;
                if (treatAsClick && snapshot.ShowExitPrompt)
                {
                    HandleExitPromptClick(mouseState.X, mouseState.Y);
                }
                else if (treatAsClick && snapshot.ClaimDialog.IsOpen && !IsCollapsed)
                {
                    HandleClaimDialogClick(mouseState.X, mouseState.Y, snapshot);
                }
                else if (treatAsClick && !IsCollapsed)
                {
                    if (GetLeaveButtonBounds().Contains(mouseState.X, mouseState.Y))
                    {
                        HandleLeave();
                    }
                    else if (GetInputBounds().Contains(mouseState.X, mouseState.Y))
                    {
                        ActivateInput(whisperMode: false, snapshot, clearText: false);
                    }
                    else
                    {
                        int slot = GetSlotIndexAt(mouseState.X, mouseState.Y);
                        if (slot >= 0)
                        {
                            _slotSelectionHandler?.Invoke(slot);
                            MessengerSnapshot updatedSnapshot = RefreshSnapshot();
                            if (!IsCollapsed
                                && updatedSnapshot.SelectedSlot == slot
                                && updatedSnapshot.CanWhisper
                                && updatedSnapshot.SelectedParticipantOnline)
                            {
                                ActivateInput(whisperMode: true, updatedSnapshot, clearText: true);
                                ShowFeedback($"Whispering to {updatedSnapshot.SelectedParticipantName}.");
                            }
                        }
                    }
                }
            }

            if (mouseState.LeftButton == ButtonState.Released)
            {
                _clickStartPoint = null;
            }

            _previousKeyboardState = keyboardState;
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
            MessengerSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            ApplyWindowState(snapshot.WindowState);

            IDXObject overlay = _windowState switch
            {
                MessengerWindowState.Min => _minOverlay,
                MessengerWindowState.Min2 => _collapsedOverlay,
                _ => _maxOverlay
            };
            Point overlayOffset = _windowState switch
            {
                MessengerWindowState.Min => _minOverlayOffset,
                MessengerWindowState.Min2 => _collapsedOverlayOffset,
                _ => _maxOverlayOffset
            };
            IDXObject contentOverlay = _windowState switch
            {
                MessengerWindowState.Min => _minContentOverlay,
                MessengerWindowState.Min2 => _collapsedContentOverlay,
                _ => _maxContentOverlay
            };
            Point contentOffset = _windowState switch
            {
                MessengerWindowState.Min => _minContentOffset,
                MessengerWindowState.Min2 => _collapsedContentOffset,
                _ => _maxContentOffset
            };

            DrawLayer(sprite, overlay, overlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, contentOverlay, contentOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            if (_font == null)
            {
                return;
            }

            sprite.DrawString(_font, "Messenger", new Vector2(Position.X + 26, Position.Y + 7), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            if (!IsCollapsed)
            {
                DrawStatusBar(sprite, snapshot);
                DrawLeaveControl(sprite, snapshot);
                DrawParticipantSlots(sprite, snapshot);
                DrawParticipantBalloons(sprite, snapshot, TickCount);
                DrawLogEntries(sprite, snapshot, contentOffset);
                DrawInputPanel(sprite, snapshot, TickCount);
            }
            else
            {
                DrawCollapsedStatus(sprite, snapshot);
            }

            if (snapshot.ShowExitPrompt)
            {
                DrawExitPrompt(sprite, snapshot);
            }
            else if (snapshot.ClaimDialog.IsOpen && !IsCollapsed)
            {
                DrawClaimDialog(sprite, snapshot);
            }
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

        private void HandleEnterButton()
        {
            if (IsCollapsed)
            {
                return;
            }

            MessengerSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            if (!_inputActive)
            {
                ActivateInput(whisperMode: false, snapshot, clearText: false);
            }

            if (_inputText.Length == 0)
            {
                return;
            }

            SendCurrentInput(snapshot);
        }

        private void HandleClaim()
        {
            ShowFeedback(_claimHandler?.Invoke());
        }

        private void HandleClaimDialogClick(int mouseX, int mouseY, MessengerSnapshot snapshot)
        {
            GetClaimDialogLayout(
                out Rectangle dialogBounds,
                out Rectangle primaryBounds,
                out Rectangle secondaryBounds,
                out Rectangle cancelBounds);

            if (!dialogBounds.Contains(mouseX, mouseY))
            {
                return;
            }

            switch (snapshot.ClaimDialog.Stage)
            {
                case MessengerClaimDialogStage.Notice:
                    if (primaryBounds.Contains(mouseX, mouseY))
                    {
                        ShowFeedback(_claimNoticeHandler?.Invoke());
                    }
                    else if (cancelBounds.Contains(mouseX, mouseY))
                    {
                        ShowFeedback(_claimCancelHandler?.Invoke());
                    }
                    break;

                case MessengerClaimDialogStage.Category:
                    if (primaryBounds.Contains(mouseX, mouseY))
                    {
                        ShowFeedback(_claimCategoryHandler?.Invoke("chat"));
                    }
                    else if (secondaryBounds.Contains(mouseX, mouseY))
                    {
                        ShowFeedback(_claimCategoryHandler?.Invoke("personal"));
                    }
                    else if (cancelBounds.Contains(mouseX, mouseY))
                    {
                        ShowFeedback(_claimCancelHandler?.Invoke());
                    }
                    break;

                case MessengerClaimDialogStage.Report:
                    if (primaryBounds.Contains(mouseX, mouseY))
                    {
                        ShowFeedback(_claimHandler?.Invoke());
                    }
                    else if (cancelBounds.Contains(mouseX, mouseY))
                    {
                        ShowFeedback(_claimCancelHandler?.Invoke());
                    }
                    break;
            }
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private void CycleState(bool forward)
        {
            ShowFeedback(_stateCycleHandler?.Invoke(forward));
            ApplyWindowState(RefreshSnapshot().WindowState);
        }

        private void ApplyWindowState(MessengerWindowState state)
        {
            _windowState = state;
            if (IsCollapsed)
            {
                DeactivateInput(clearText: false);
            }

            RefreshFrame();
        }

        private void RefreshFrame()
        {
            Frame = _windowState switch
            {
                MessengerWindowState.Min => _minimizedFrame,
                MessengerWindowState.Min2 => _collapsedFrame,
                _ => _maximizedFrame
            };
        }

        private MessengerSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new MessengerSnapshot();
            return _currentSnapshot;
        }

        private bool IsExpanded => _windowState == MessengerWindowState.Max;
        private bool IsCompact => _windowState == MessengerWindowState.Min;
        private bool IsCollapsed => _windowState == MessengerWindowState.Min2;

        private void UpdateButtonStates(MessengerSnapshot snapshot)
        {
            ApplyButtonLayout();

            if (_enterButton != null)
            {
                _enterButton.SetVisible(!IsCollapsed);
                _enterButton.SetButtonState(!IsCollapsed && _inputText.Length > 0 ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_claimButton != null)
            {
                _claimButton.SetVisible(!IsCollapsed);
                _claimButton.SetButtonState(!IsCollapsed && snapshot.CanClaim ? UIObjectState.Normal : UIObjectState.Disabled);
            }
        }

        private void HandleLeave()
        {
            DeactivateInput(clearText: false);
            ShowFeedback(_leaveHandler?.Invoke());
        }

        private void DrawParticipantSlots(SpriteBatch sprite, MessengerSnapshot snapshot)
        {
            IReadOnlyList<MessengerParticipantSnapshot> participants = snapshot.Participants ?? Array.Empty<MessengerParticipantSnapshot>();
            for (int i = 0; i < SlotCount; i++)
            {
                Point origin = SlotOrigins[i];
                Rectangle slotBounds = new Rectangle(Position.X + origin.X, Position.Y + origin.Y, 89, IsCompact ? 74 : 84);
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
                string channelLine = participant.IsLocalPlayer
                    ? $"You  Lv {Math.Max(1, participant.Level)}"
                    : $"CH {participant.Channel}  Lv {Math.Max(1, participant.Level)}";
                DrawCentered(sprite, Truncate(channelLine, 16), cardBounds.X, cardBounds.Y + 8, cardBounds.Width, new Color(156, 228, 188), 0.4f);
                DrawCentered(sprite, Truncate(participant.JobName, 14), cardBounds.X, cardBounds.Y + 22, cardBounds.Width, new Color(224, 228, 235), 0.36f);
                if (!IsCompact)
                {
                    DrawCentered(sprite, Truncate(participant.LocationSummary, 13), cardBounds.X, cardBounds.Y + 38, cardBounds.Width, new Color(224, 228, 235), 0.35f);
                    DrawCentered(sprite, Truncate(participant.StatusText, 15), cardBounds.X, cardBounds.Y + 52, cardBounds.Width, new Color(197, 205, 216), 0.34f);
                }
                else
                {
                    DrawCentered(sprite, Truncate(participant.LocationSummary, 13), cardBounds.X, cardBounds.Y + 36, cardBounds.Width, new Color(224, 228, 235), 0.35f);
                }
            }
        }

        private void DrawParticipantBalloons(SpriteBatch sprite, MessengerSnapshot snapshot, int tickCount)
        {
            if (_chatBalloonFrames.Length == 0)
            {
                return;
            }

            IReadOnlyList<MessengerParticipantSnapshot> participants = snapshot.Participants ?? Array.Empty<MessengerParticipantSnapshot>();
            for (int i = 0; i < SlotCount && i < participants.Count; i++)
            {
                MessengerParticipantSnapshot participant = participants[i];
                if (participant == null
                    || string.IsNullOrWhiteSpace(participant.BubbleText)
                    || tickCount < participant.BubbleStartTick
                    || tickCount > participant.BubbleExpireTick)
                {
                    continue;
                }

                int frameIndex = ((tickCount - participant.BubbleStartTick) / ChatBalloonFrameDelayMs) % _chatBalloonFrames.Length;
                Texture2D frame = _chatBalloonFrames[Math.Clamp(frameIndex, 0, _chatBalloonFrames.Length - 1)];
                if (frame == null)
                {
                    continue;
                }

                Rectangle slotBounds = new Rectangle(Position.X + SlotOrigins[i].X, Position.Y + SlotOrigins[i].Y, 89, IsCompact ? 74 : 84);
                Vector2 balloonPosition = new Vector2(
                    slotBounds.X + ((slotBounds.Width - frame.Width) * 0.5f),
                    slotBounds.Y - frame.Height + 10);
                sprite.Draw(frame, balloonPosition, Color.White);
                DrawCentered(
                    sprite,
                    Truncate(participant.BubbleText, 11),
                    (int)balloonPosition.X + 4,
                    (int)balloonPosition.Y + 11,
                    frame.Width - 8,
                    new Color(48, 36, 18),
                    0.32f);
            }
        }

        private void DrawLogEntries(SpriteBatch sprite, MessengerSnapshot snapshot, Point contentOffset)
        {
            Rectangle panelBounds = new Rectangle(
                Position.X + contentOffset.X + 8,
                Position.Y + contentOffset.Y + 8,
                258,
                IsCompact ? 130 : 155);

            sprite.Draw(_pixel, panelBounds, new Color(7, 12, 20, 160));

            IReadOnlyList<MessengerLogEntrySnapshot> logEntries = snapshot.LogEntries ?? Array.Empty<MessengerLogEntrySnapshot>();
            int visibleEntries = IsCompact ? 5 : 6;
            int startIndex = Math.Max(0, logEntries.Count - visibleEntries);
            int y = panelBounds.Y + 8;
            for (int i = startIndex; i < logEntries.Count; i++)
            {
                MessengerLogEntrySnapshot entry = logEntries[i];
                Color color = entry.IsSystem
                    ? new Color(255, 227, 150)
                    : entry.IsWhisper
                        ? new Color(255, 183, 241)
                        : new Color(220, 227, 238);
                string line = entry.IsSystem
                    ? entry.Message
                    : entry.IsWhisper
                        ? $"[W] {entry.Author} -> {entry.TargetName}: {entry.Message}"
                        : $"{entry.Author}: {entry.Message}";
                if (entry.IsClaimed)
                {
                    color = new Color(155, 166, 181);
                    line = $"[Claimed] {line}";
                }

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

        private void DrawInputPanel(SpriteBatch sprite, MessengerSnapshot snapshot, int tickCount)
        {
            if (IsCollapsed)
            {
                return;
            }

            Rectangle inputBounds = GetInputBounds();
            sprite.Draw(_pixel, inputBounds, _inputActive ? new Color(33, 53, 84, 225) : new Color(18, 25, 40, 190));

            string label = _whisperMode && !string.IsNullOrWhiteSpace(snapshot.SelectedParticipantName)
                ? $"Whisper > {snapshot.SelectedParticipantName}"
                : "Messenger";
            sprite.DrawString(_font, label, new Vector2(inputBounds.X + 6, inputBounds.Y - 12), new Color(202, 213, 226), 0f, Vector2.Zero, 0.35f, SpriteEffects.None, 0f);

            string messageText = _inputText.Length > 0
                ? _inputText.ToString()
                : _whisperMode && !string.IsNullOrWhiteSpace(snapshot.SelectedParticipantName)
                    ? $"Send a whisper to {snapshot.SelectedParticipantName}"
                    : "Type chat or /m <name> to invite";
            Color textColor = _inputText.Length > 0 ? Color.White : new Color(144, 156, 176);
            sprite.DrawString(_font, messageText, new Vector2(inputBounds.X + 6, inputBounds.Y + 6), textColor, 0f, Vector2.Zero, 0.38f, SpriteEffects.None, 0f);

            if (_inputActive && ((tickCount - _cursorBlinkTimer) / 500) % 2 == 0)
            {
                string textBeforeCursor = _inputText.ToString(0, Math.Clamp(_cursorPosition, 0, _inputText.Length));
                float cursorX = inputBounds.X + 6 + (_font.MeasureString(textBeforeCursor).X * 0.38f);
                sprite.Draw(_pixel, new Rectangle((int)cursorX, inputBounds.Y + 4, 1, inputBounds.Height - 8), Color.White);
            }
        }

        private void DrawLeaveControl(SpriteBatch sprite, MessengerSnapshot snapshot)
        {
            Rectangle leaveBounds = GetLeaveButtonBounds();
            Color fill = snapshot.CanLeave ? new Color(103, 63, 73, 180) : new Color(50, 50, 54, 140);
            Color text = snapshot.CanLeave ? new Color(255, 224, 224) : new Color(160, 165, 174);
            sprite.Draw(_pixel, leaveBounds, fill);
            DrawCentered(sprite, "Leave", leaveBounds.X, leaveBounds.Y + 4, leaveBounds.Width, text, 0.35f);
        }

        private void DrawCollapsedStatus(SpriteBatch sprite, MessengerSnapshot snapshot)
        {
            Texture2D icon = _collapsedStatusIcon;
            Point iconPosition = _collapsedStatusIconPosition;
            if (icon != null)
            {
                Color iconTint = snapshot.ShowStatusBlink ? new Color(255, 236, 150) : Color.White;
                sprite.Draw(icon, new Vector2(Position.X + iconPosition.X, Position.Y + iconPosition.Y), iconTint);
            }

            string status = snapshot.CollapsedStatusText;
            if (string.IsNullOrWhiteSpace(status))
            {
                status = "Messenger idle";
            }

            int textStartX = Position.X + 34;
            if (icon != null)
            {
                textStartX = Position.X + iconPosition.X + icon.Width + 6;
            }

            sprite.DrawString(_font, TruncateToWidth(status, 170, 0.34f), new Vector2(textStartX, Position.Y + 6), new Color(212, 220, 231), 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
        }

        private void DrawStatusBar(SpriteBatch sprite, MessengerSnapshot snapshot)
        {
            Texture2D icon = IsCompact ? _minStatusIcon : _maxStatusIcon;
            Point iconPosition = IsCompact ? _minStatusIconPosition : _maxStatusIconPosition;
            string statusText = snapshot.StatusBarText;
            if (string.IsNullOrWhiteSpace(statusText) && !string.IsNullOrWhiteSpace(snapshot.PendingInviteSummary) && !string.Equals(snapshot.PendingInviteSummary, "none", StringComparison.OrdinalIgnoreCase))
            {
                statusText = snapshot.PendingInviteSummary;
            }

            if (icon == null && string.IsNullOrWhiteSpace(statusText))
            {
                return;
            }

            int textX = Position.X + 17;
            int textY = Position.Y + (IsCompact ? 219 : 331);
            if (icon != null)
            {
                Color iconTint = snapshot.ShowStatusBlink ? new Color(255, 236, 150) : Color.White;
                sprite.Draw(icon, new Vector2(Position.X + iconPosition.X, Position.Y + iconPosition.Y), iconTint);
                textX = Position.X + iconPosition.X + icon.Width + 5;
                textY = Position.Y + iconPosition.Y - 1;
            }

            sprite.DrawString(
                _font,
                TruncateToWidth(statusText, 252, 0.34f),
                new Vector2(textX, textY),
                new Color(66, 54, 30),
                0f,
                Vector2.Zero,
                0.34f,
                SpriteEffects.None,
                0f);
        }

        private int GetSlotIndexAt(int mouseX, int mouseY)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                Rectangle bounds = new Rectangle(Position.X + SlotOrigins[i].X, Position.Y + SlotOrigins[i].Y, 89, IsCompact ? 74 : 84);
                if (bounds.Contains(mouseX, mouseY))
                {
                    return i;
                }
            }

            return -1;
        }

        private Rectangle GetInputBounds()
        {
            return _windowState switch
            {
                MessengerWindowState.Min => new Rectangle(Position.X + 19, Position.Y + 159, 252, 24),
                MessengerWindowState.Min2 => Rectangle.Empty,
                _ => new Rectangle(Position.X + 19, Position.Y + 225, 252, 26)
            };
        }

        private Rectangle GetLeaveButtonBounds()
        {
            return new Rectangle(Position.X + 224, Position.Y + 6, 42, 16);
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

        private string TruncateToWidth(string value, float maxWidth, float scale)
        {
            if (string.IsNullOrEmpty(value) || _font == null)
            {
                return value ?? string.Empty;
            }

            string truncated = value;
            while (truncated.Length > 0 && _font.MeasureString(truncated).X * scale > maxWidth)
            {
                truncated = truncated.Length <= 4
                    ? string.Empty
                    : $"{truncated.Substring(0, truncated.Length - 4)}...";
            }

            return truncated;
        }

        private void ActivateInput(bool whisperMode, MessengerSnapshot snapshot, bool clearText)
        {
            _inputActive = true;
            _whisperMode = whisperMode && snapshot.CanWhisper;
            _historyIndex = -1;
            _historyDraft = string.Empty;
            if (clearText)
            {
                _inputText.Clear();
                _cursorPosition = 0;
            }
            else
            {
                _cursorPosition = Math.Clamp(_cursorPosition, 0, _inputText.Length);
            }

            _cursorBlinkTimer = Environment.TickCount;
        }

        private void DeactivateInput(bool clearText)
        {
            _inputActive = false;
            _whisperMode = false;
            if (clearText)
            {
                _inputText.Clear();
                _cursorPosition = 0;
            }

            ResetKeyRepeat();
        }

        private void HandleKeyboardInput(KeyboardState keyboardState, MessengerSnapshot snapshot, int tickCount)
        {
            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                RequestExitPrompt();
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
            {
                SendCurrentInput(snapshot);
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Back))
            {
                if (_previousKeyboardState.IsKeyUp(Keys.Back))
                {
                    if (_cursorPosition > 0)
                    {
                        _inputText.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                    }

                    _lastHeldKey = Keys.Back;
                    _keyHoldStartTime = tickCount;
                    _lastKeyRepeatTime = tickCount;
                }
                else if (ShouldRepeatKey(Keys.Back, tickCount) && _cursorPosition > 0)
                {
                    _inputText.Remove(_cursorPosition - 1, 1);
                    _cursorPosition--;
                    _lastKeyRepeatTime = tickCount;
                }

                return;
            }

            if (keyboardState.IsKeyDown(Keys.Delete))
            {
                if (_previousKeyboardState.IsKeyUp(Keys.Delete))
                {
                    if (_cursorPosition < _inputText.Length)
                    {
                        _inputText.Remove(_cursorPosition, 1);
                    }

                    _lastHeldKey = Keys.Delete;
                    _keyHoldStartTime = tickCount;
                    _lastKeyRepeatTime = tickCount;
                }
                else if (ShouldRepeatKey(Keys.Delete, tickCount) && _cursorPosition < _inputText.Length)
                {
                    _inputText.Remove(_cursorPosition, 1);
                    _lastKeyRepeatTime = tickCount;
                }

                return;
            }

            if (keyboardState.IsKeyDown(Keys.Left))
            {
                if (_previousKeyboardState.IsKeyUp(Keys.Left))
                {
                    _cursorPosition = Math.Max(0, _cursorPosition - 1);
                    _lastHeldKey = Keys.Left;
                    _keyHoldStartTime = tickCount;
                    _lastKeyRepeatTime = tickCount;
                }
                else if (ShouldRepeatKey(Keys.Left, tickCount))
                {
                    _cursorPosition = Math.Max(0, _cursorPosition - 1);
                    _lastKeyRepeatTime = tickCount;
                }

                return;
            }

            if (keyboardState.IsKeyDown(Keys.Right))
            {
                if (_previousKeyboardState.IsKeyUp(Keys.Right))
                {
                    _cursorPosition = Math.Min(_inputText.Length, _cursorPosition + 1);
                    _lastHeldKey = Keys.Right;
                    _keyHoldStartTime = tickCount;
                    _lastKeyRepeatTime = tickCount;
                }
                else if (ShouldRepeatKey(Keys.Right, tickCount))
                {
                    _cursorPosition = Math.Min(_inputText.Length, _cursorPosition + 1);
                    _lastKeyRepeatTime = tickCount;
                }

                return;
            }

            if (keyboardState.IsKeyDown(Keys.Home) && _previousKeyboardState.IsKeyUp(Keys.Home))
            {
                _cursorPosition = 0;
                return;
            }

            if (keyboardState.IsKeyDown(Keys.End) && _previousKeyboardState.IsKeyUp(Keys.End))
            {
                _cursorPosition = _inputText.Length;
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Up) && _previousKeyboardState.IsKeyUp(Keys.Up))
            {
                NavigateHistory(previous: true);
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Down) && _previousKeyboardState.IsKeyUp(Keys.Down))
            {
                NavigateHistory(previous: false);
                return;
            }

            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in keyboardState.GetPressedKeys())
            {
                if (_previousKeyboardState.IsKeyDown(key)
                    || key == Keys.Enter
                    || key == Keys.Escape
                    || key == Keys.Back
                    || key == Keys.Delete
                    || key == Keys.Left
                    || key == Keys.Right
                    || key == Keys.Up
                    || key == Keys.Down
                    || key == Keys.Home
                    || key == Keys.End
                    || key == Keys.LeftShift
                    || key == Keys.RightShift
                    || key == Keys.LeftControl
                    || key == Keys.RightControl
                    || key == Keys.LeftAlt
                    || key == Keys.RightAlt)
                {
                    continue;
                }

                char? character = KeyToChar(key, shift);
                if (!character.HasValue || _inputText.Length >= 70)
                {
                    continue;
                }

                _inputText.Insert(_cursorPosition, character.Value);
                _cursorPosition++;
                _lastHeldKey = key;
                _keyHoldStartTime = tickCount;
                _lastKeyRepeatTime = tickCount;
            }

            if (_lastHeldKey != Keys.None
                && _lastHeldKey != Keys.Back
                && _lastHeldKey != Keys.Delete
                && keyboardState.IsKeyDown(_lastHeldKey)
                && ShouldRepeatKey(_lastHeldKey, tickCount))
            {
                char? repeatedCharacter = KeyToChar(_lastHeldKey, shift);
                if (repeatedCharacter.HasValue && _inputText.Length < 70)
                {
                    _inputText.Insert(_cursorPosition, repeatedCharacter.Value);
                    _cursorPosition++;
                    _lastKeyRepeatTime = tickCount;
                }
            }
            else if (_lastHeldKey != Keys.None
                && _lastHeldKey != Keys.Back
                && _lastHeldKey != Keys.Delete
                && !keyboardState.IsKeyDown(_lastHeldKey))
            {
                ResetKeyRepeat();
            }
        }

        private void HandleExitPromptInput(KeyboardState keyboardState)
        {
            if (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
            {
                ConfirmExitPrompt();
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                CancelExitPrompt();
            }
        }

        private void SendCurrentInput(MessengerSnapshot snapshot)
        {
            string text = _inputText.ToString().Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowFeedback(_whisperMode ? "Type a whisper before sending." : "Type a Messenger message before sending.");
                return;
            }

            string result = _whisperMode
                ? _sendWhisperHandler?.Invoke(text)
                : _sendMessageHandler?.Invoke(text);
            ShowFeedback(result);
            RecordHistory(text);

            _inputText.Clear();
            _cursorPosition = 0;
            _cursorBlinkTimer = Environment.TickCount;
            _historyIndex = -1;
            _historyDraft = string.Empty;

            if (_whisperMode && !snapshot.CanWhisper)
            {
                _whisperMode = false;
            }
        }

        private void ApplyButtonLayout()
        {
            if (_enterButton != null)
            {
                Point position = _windowState == MessengerWindowState.Min ? _minEnterButtonPosition : _maxEnterButtonPosition;
                _enterButton.X = position.X;
                _enterButton.Y = position.Y;
            }

            if (_claimButton != null)
            {
                Point position = _windowState == MessengerWindowState.Min ? _minClaimButtonPosition : _maxClaimButtonPosition;
                _claimButton.X = position.X;
                _claimButton.Y = position.Y;
            }

            if (_maximizeButton != null)
            {
                Point position = _windowState == MessengerWindowState.Min2 ? _collapsedMaximizeButtonPosition : _maxMaximizeButtonPosition;
                _maximizeButton.X = position.X;
                _maximizeButton.Y = position.Y;
                _maximizeButton.SetVisible(true);
            }

            if (_minimizeButton != null)
            {
                Point position = _windowState switch
                {
                    MessengerWindowState.Min => _minMinimizeButtonPosition,
                    MessengerWindowState.Min2 => _collapsedMinimizeButtonPosition,
                    _ => _maxMinimizeButtonPosition
                };
                _minimizeButton.X = position.X;
                _minimizeButton.Y = position.Y;
                _minimizeButton.SetVisible(true);
            }
        }

        private bool ShouldRepeatKey(Keys key, int tickCount)
        {
            if (_lastHeldKey != key)
            {
                return false;
            }

            int holdDuration = tickCount - _keyHoldStartTime;
            if (holdDuration < 400)
            {
                return false;
            }

            return tickCount - _lastKeyRepeatTime >= 35;
        }

        private void ResetKeyRepeat()
        {
            _lastHeldKey = Keys.None;
            _keyHoldStartTime = 0;
            _lastKeyRepeatTime = 0;
        }

        protected override void OnCloseButtonClicked(UIObject sender)
        {
            RequestClose();
        }

        private void RequestExitPrompt()
        {
            _resumeInputAfterExitPrompt = _inputActive;
            _resumeWhisperAfterExitPrompt = _whisperMode;
            DeactivateInput(clearText: false);
            ShowFeedback(_requestExitPromptHandler?.Invoke());
        }

        private void ConfirmExitPrompt()
        {
            _resumeInputAfterExitPrompt = false;
            _resumeWhisperAfterExitPrompt = false;
            DeactivateInput(clearText: false);
            MessengerDeleteResult result = _confirmExitPromptHandler?.Invoke();
            ShowFeedback(result?.Message);
            if (result?.ShouldHideWindow == true)
            {
                Hide();
                _closeAcknowledgeHandler?.Invoke();
            }
        }

        private void CancelExitPrompt()
        {
            ShowFeedback(_cancelExitPromptHandler?.Invoke());
            if (_resumeInputAfterExitPrompt)
            {
                MessengerSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
                ActivateInput(_resumeWhisperAfterExitPrompt, snapshot, clearText: false);
            }

            _resumeInputAfterExitPrompt = false;
            _resumeWhisperAfterExitPrompt = false;
        }

        private void RequestClose()
        {
            DeactivateInput(clearText: false);
            MessengerDeleteResult result = _closeRequestHandler?.Invoke();
            ShowFeedback(result?.Message);
            if (result?.ShouldHideWindow == true)
            {
                Hide();
                _closeAcknowledgeHandler?.Invoke();
            }
        }

        private void HandleExitPromptClick(int mouseX, int mouseY)
        {
            GetExitPromptLayout(out Rectangle promptBounds, out Rectangle yesBounds, out Rectangle noBounds);
            if (yesBounds.Contains(mouseX, mouseY))
            {
                ConfirmExitPrompt();
            }
            else if (noBounds.Contains(mouseX, mouseY))
            {
                CancelExitPrompt();
            }
            else if (!promptBounds.Contains(mouseX, mouseY))
            {
                CancelExitPrompt();
            }
        }

        private void DrawExitPrompt(SpriteBatch sprite, MessengerSnapshot snapshot)
        {
            GetExitPromptLayout(out Rectangle promptBounds, out Rectangle yesBounds, out Rectangle noBounds);

            int frameWidth = CurrentFrame?.Width ?? 0;
            int frameHeight = CurrentFrame?.Height ?? 0;
            sprite.Draw(_pixel, new Rectangle(Position.X, Position.Y, frameWidth, frameHeight), new Color(0, 0, 0, 140));
            sprite.Draw(_pixel, promptBounds, new Color(38, 24, 12, 230));
            sprite.Draw(_pixel, new Rectangle(promptBounds.X + 1, promptBounds.Y + 1, promptBounds.Width - 2, promptBounds.Height - 2), new Color(247, 232, 194, 240));

            DrawOutlinedText(sprite, "Confirm", new Vector2(promptBounds.X + 10, promptBounds.Y + 8), Color.Black, new Color(96, 60, 20), 0.44f);
            DrawOutlinedText(sprite, snapshot.ExitPromptText, new Vector2(promptBounds.X + 10, promptBounds.Y + 32), Color.Black, new Color(72, 52, 24), 0.38f);
            DrawPromptButton(sprite, yesBounds, "Yes");
            DrawPromptButton(sprite, noBounds, "No");
        }

        private void DrawClaimDialog(SpriteBatch sprite, MessengerSnapshot snapshot)
        {
            MessengerClaimDialogSnapshot claim = snapshot.ClaimDialog;
            GetClaimDialogLayout(
                out Rectangle dialogBounds,
                out Rectangle primaryBounds,
                out Rectangle secondaryBounds,
                out Rectangle cancelBounds);

            int frameWidth = CurrentFrame?.Width ?? 0;
            int frameHeight = CurrentFrame?.Height ?? 0;
            sprite.Draw(_pixel, new Rectangle(Position.X, Position.Y, frameWidth, frameHeight), new Color(0, 0, 0, 132));
            sprite.Draw(_pixel, dialogBounds, new Color(42, 32, 24, 236));
            sprite.Draw(_pixel, new Rectangle(dialogBounds.X + 2, dialogBounds.Y + 2, dialogBounds.Width - 4, dialogBounds.Height - 4), new Color(247, 235, 205, 246));

            string title = claim.Stage switch
            {
                MessengerClaimDialogStage.Notice => "Claim Notice",
                MessengerClaimDialogStage.Category => "Claim Category",
                MessengerClaimDialogStage.Report => "Report",
                MessengerClaimDialogStage.PendingResult => "Claim Pending",
                MessengerClaimDialogStage.Completed => "Claim Complete",
                _ => "Claim"
            };

            DrawOutlinedText(sprite, title, new Vector2(dialogBounds.X + 12, dialogBounds.Y + 9), Color.Black, new Color(112, 74, 38), 0.46f);
            DrawOutlinedText(sprite, $"Target: {claim.TargetCharacterName}", new Vector2(dialogBounds.X + 12, dialogBounds.Y + 34), Color.Black, new Color(92, 66, 40), 0.36f);

            string body = claim.Stage switch
            {
                MessengerClaimDialogStage.Notice => $"Review the Messenger claim notice from {claim.NoticeAsset}.",
                MessengerClaimDialogStage.Category => $"Choose {claim.AssetRoot} category button.",
                MessengerClaimDialogStage.Report => $"Submitting {claim.ChatLineCount} Messenger chat line(s) through {claim.ReportAsset}.",
                MessengerClaimDialogStage.PendingResult => $"Waiting for server-owned claim completion for {claim.Context}.",
                MessengerClaimDialogStage.Completed => "Server-owned claim completion was applied.",
                _ => string.Empty
            };

            int bodyY = dialogBounds.Y + 55;
            foreach (string line in WrapText(body, dialogBounds.Width - 24))
            {
                DrawOutlinedText(sprite, line, new Vector2(dialogBounds.X + 12, bodyY), Color.Black, new Color(92, 66, 40), 0.34f);
                bodyY += 16;
            }

            if (claim.Stage == MessengerClaimDialogStage.Category)
            {
                DrawPromptButton(sprite, primaryBounds, "BtCClaim");
                DrawPromptButton(sprite, secondaryBounds, "BtPClaim");
                DrawPromptButton(sprite, cancelBounds, "Cancel");
            }
            else if (claim.Stage == MessengerClaimDialogStage.Notice)
            {
                DrawPromptButton(sprite, primaryBounds, "OK");
                DrawPromptButton(sprite, cancelBounds, "Cancel");
            }
            else if (claim.Stage == MessengerClaimDialogStage.Report)
            {
                DrawPromptButton(sprite, primaryBounds, "BtClaim");
                DrawPromptButton(sprite, cancelBounds, "BtCancel");
            }
        }

        private void DrawPromptButton(SpriteBatch sprite, Rectangle bounds, string label)
        {
            sprite.Draw(_pixel, bounds, new Color(120, 90, 48, 220));
            sprite.Draw(_pixel, new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2), new Color(255, 239, 188, 240));
            DrawCentered(sprite, label, bounds.X, bounds.Y + 5, bounds.Width, Color.Black, 0.38f);
        }

        private void DrawOutlinedText(SpriteBatch sprite, string text, Vector2 position, Color color, Color outlineColor, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2[] offsets =
            {
                new(-1f, 0f),
                new(1f, 0f),
                new(0f, -1f),
                new(0f, 1f)
            };

            foreach (Vector2 offset in offsets)
            {
                sprite.DrawString(_font, text, position + offset, outlineColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void GetExitPromptLayout(out Rectangle promptBounds, out Rectangle yesBounds, out Rectangle noBounds)
        {
            int promptWidth = 250;
            int promptHeight = 98;
            int frameWidth = CurrentFrame?.Width ?? 0;
            int frameHeight = CurrentFrame?.Height ?? 0;
            int promptX = Position.X + ((frameWidth - promptWidth) / 2);
            int promptY = Position.Y + ((frameHeight - promptHeight) / 2);
            promptBounds = new Rectangle(promptX, promptY, promptWidth, promptHeight);
            yesBounds = new Rectangle(promptBounds.X + 26, promptBounds.Bottom - 30, 64, 22);
            noBounds = new Rectangle(promptBounds.Right - 26 - 64, promptBounds.Bottom - 30, 64, 22);
        }

        private void GetClaimDialogLayout(
            out Rectangle dialogBounds,
            out Rectangle primaryBounds,
            out Rectangle secondaryBounds,
            out Rectangle cancelBounds)
        {
            int dialogWidth = 264;
            int dialogHeight = 126;
            int frameWidth = CurrentFrame?.Width ?? 0;
            int frameHeight = CurrentFrame?.Height ?? 0;
            int dialogX = Position.X + ((frameWidth - dialogWidth) / 2);
            int dialogY = Position.Y + Math.Max(18, ((frameHeight - dialogHeight) / 2));
            dialogBounds = new Rectangle(dialogX, dialogY, dialogWidth, dialogHeight);
            primaryBounds = new Rectangle(dialogBounds.X + 18, dialogBounds.Bottom - 30, 68, 22);
            secondaryBounds = new Rectangle(dialogBounds.X + 98, dialogBounds.Bottom - 30, 68, 22);
            cancelBounds = new Rectangle(dialogBounds.Right - 18 - 68, dialogBounds.Bottom - 30, 68, 22);
        }

        private void NavigateHistory(bool previous)
        {
            if (_inputHistory.Count == 0)
            {
                return;
            }

            if (_historyIndex < 0)
            {
                _historyDraft = _inputText.ToString();
                _historyIndex = _inputHistory.Count;
            }

            _historyIndex = previous
                ? Math.Max(0, _historyIndex - 1)
                : Math.Min(_inputHistory.Count, _historyIndex + 1);

            string historyValue = _historyIndex >= 0 && _historyIndex < _inputHistory.Count
                ? _inputHistory[_historyIndex]
                : _historyDraft;
            _inputText.Clear();
            _inputText.Append(historyValue);
            _cursorPosition = _inputText.Length;
            _cursorBlinkTimer = Environment.TickCount;
        }

        private void RecordHistory(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (_inputHistory.Count > 0 && string.Equals(_inputHistory[^1], text, StringComparison.Ordinal))
            {
                return;
            }

            _inputHistory.Add(text);
            if (_inputHistory.Count > 20)
            {
                _inputHistory.RemoveAt(0);
            }
        }

        private static char? KeyToChar(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                char c = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpperInvariant(c) : c;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                if (shift)
                {
                    char[] shifted = { ')', '!', '@', '#', '$', '%', '^', '&', '*', '(' };
                    return shifted[key - Keys.D0];
                }

                return (char)('0' + (key - Keys.D0));
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }

            return key switch
            {
                Keys.Space => ' ',
                Keys.OemPeriod => shift ? '>' : '.',
                Keys.OemComma => shift ? '<' : ',',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPlus => shift ? '+' : '=',
                Keys.OemQuestion => shift ? '?' : '/',
                Keys.OemSemicolon => shift ? ':' : ';',
                Keys.OemQuotes => shift ? '"' : '\'',
                Keys.OemOpenBrackets => shift ? '{' : '[',
                Keys.OemCloseBrackets => shift ? '}' : ']',
                Keys.OemPipe => shift ? '|' : '\\',
                Keys.OemTilde => shift ? '~' : '`',
                _ => null
            };
        }
    }
}
