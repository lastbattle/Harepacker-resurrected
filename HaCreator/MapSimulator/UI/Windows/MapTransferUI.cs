using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class MapTransferUI : UIWindowBase, ISoftKeyboardHost
    {
        public sealed class DestinationEntry
        {
            public int MapId { get; init; }
            public string DisplayName { get; init; }
            public string DetailText { get; init; }
            public string RestrictionMessage { get; init; }
            public string TargetPortalName { get; init; }
            public int SavedSlotIndex { get; init; } = -1;
            public bool CanDelete { get; init; }
            public bool CanMove => MapId > 0 && string.IsNullOrWhiteSpace(RestrictionMessage);
            public bool IsSavedSlot => SavedSlotIndex >= 0;
        }

        private const int MaxVisibleRows = 5;
        private const int RowHeight = 17;
        private const int RowStartX = 19;
        private const int RowStartY = 39;
        private const int RowTextX = 25;
        private const int RowTextY = 41;
        private const int RowTextWidth = 110;
        private const int EditTargetX = 14;
        private const int EditTargetY = 231;
        private const int EditTargetWidth = 120;
        private const int EditTargetHeight = 13;
        private const int EditTargetTextInsetX = 3;
        private const int EditTargetTextInsetY = 1;
        private const int EditTargetMaxLength = 12;
        private const int ScrollBarX = 139;
        private const int ScrollBarY = 76;
        private const int ScrollBarHeight = 93;
        private const int ScrollBarButtonHeight = 12;
        private const int ScrollBarWheelRange = 165;

        private readonly IDXObject _innerFrame;
        private readonly IDXObject _listFrame;
        private readonly Texture2D _selectionTexture;
        private readonly List<UIObject> _rowButtons = new();
        private readonly List<DestinationEntry> _destinations = new();
        private readonly UIObject _registerButton;
        private readonly UIObject _deleteButton;
        private readonly UIObject _moveButton;
        private readonly UIObject _mapButton;
        private readonly Texture2D _confirmationTexture;
        private readonly UIObject _confirmationOkButton;
        private readonly UIObject _confirmationCancelButton;
        private readonly List<UIObject> _confirmationButtons = new();
        private readonly int _maxSavedDestinations;
        private readonly Texture2D _scrollUpNormal;
        private readonly Texture2D _scrollUpPressed;
        private readonly Texture2D _scrollUpDisabled;
        private readonly Texture2D _scrollDownNormal;
        private readonly Texture2D _scrollDownPressed;
        private readonly Texture2D _scrollDownDisabled;
        private readonly Texture2D _scrollTrackEnabled;
        private readonly Texture2D _scrollTrackDisabled;
        private readonly Texture2D _scrollThumbNormal;
        private readonly Texture2D _scrollThumbPressed;

        private SpriteFont _font;
        private string _currentMapName = string.Empty;
        private string _statusMessage = "Register maps or select a route to transfer.";
        private string _manualTargetText = string.Empty;
        private int _selectedIndex = -1;
        private int _scrollOffset;
        private int _previousScrollWheelValue;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private bool _editTargetFocused;
        private bool _softKeyboardActive;
        private string _compositionText = string.Empty;
        private ImeCandidateListState _candidateListState = ImeCandidateListState.Empty;
        private bool _confirmationVisible;
        private string _confirmationMessage = string.Empty;
        private Action _pendingConfirmationAction;
        private bool _isDraggingScrollThumb;
        private int _scrollThumbDragOffsetY;
        private Point _lastMousePosition;

        public MapTransferUI(
            IDXObject frame,
            IDXObject innerFrame,
            IDXObject listFrame,
            Texture2D selectionTexture,
            UIObject registerButton,
            UIObject deleteButton,
            UIObject moveButton,
            UIObject mapButton,
            Texture2D confirmationTexture,
            UIObject confirmationOkButton,
            UIObject confirmationCancelButton,
            int maxSavedDestinations,
            Texture2D scrollUpNormal,
            Texture2D scrollUpPressed,
            Texture2D scrollUpDisabled,
            Texture2D scrollDownNormal,
            Texture2D scrollDownPressed,
            Texture2D scrollDownDisabled,
            Texture2D scrollTrackEnabled,
            Texture2D scrollTrackDisabled,
            Texture2D scrollThumbNormal,
            Texture2D scrollThumbPressed,
            GraphicsDevice device)
            : base(frame)
        {
            _innerFrame = innerFrame;
            _listFrame = listFrame;
            _selectionTexture = selectionTexture;
            _registerButton = registerButton;
            _deleteButton = deleteButton;
            _moveButton = moveButton;
            _mapButton = mapButton;
            _confirmationTexture = confirmationTexture;
            _confirmationOkButton = confirmationOkButton;
            _confirmationCancelButton = confirmationCancelButton;
            _maxSavedDestinations = Math.Max(MaxVisibleRows, maxSavedDestinations);
            _scrollUpNormal = scrollUpNormal;
            _scrollUpPressed = scrollUpPressed;
            _scrollUpDisabled = scrollUpDisabled;
            _scrollDownNormal = scrollDownNormal;
            _scrollDownPressed = scrollDownPressed;
            _scrollDownDisabled = scrollDownDisabled;
            _scrollTrackEnabled = scrollTrackEnabled;
            _scrollTrackDisabled = scrollTrackDisabled;
            _scrollThumbNormal = scrollThumbNormal;
            _scrollThumbPressed = scrollThumbPressed;

            InitializeCloseAndActionButtons(registerButton, deleteButton, moveButton, mapButton);
            InitializeRowButtons(device);
            InitializeConfirmationButtons();
            UpdateButtonStates();
        }

        public MapTransferUI(
            IDXObject frame,
            IDXObject innerFrame,
            IDXObject listFrame,
            Texture2D selectionTexture,
            UIObject registerButton,
            UIObject deleteButton,
            UIObject moveButton,
            UIObject mapButton,
            Texture2D confirmationTexture,
            UIObject confirmationOkButton,
            UIObject confirmationCancelButton,
            int maxSavedDestinations,
            GraphicsDevice device)
            : this(
                frame,
                innerFrame,
                listFrame,
                selectionTexture,
                registerButton,
                deleteButton,
                moveButton,
                mapButton,
                confirmationTexture,
                confirmationOkButton,
                confirmationCancelButton,
                maxSavedDestinations,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                device)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.MapTransfer;
        public override bool CapturesKeyboardInput => IsVisible && _editTargetFocused;
        public int MaxSavedDestinations => _maxSavedDestinations;
        public bool UsesContinentDestinationBook => _maxSavedDestinations > MaxVisibleRows;
        public int SavedDestinationCount => _destinations.FindAll(entry => entry.IsSavedSlot && entry.MapId > 0).Count;
        bool ISoftKeyboardHost.WantsSoftKeyboard => IsVisible && _editTargetFocused && _softKeyboardActive;
        SoftKeyboardKeyboardType ISoftKeyboardHost.SoftKeyboardKeyboardType => SoftKeyboardKeyboardType.NumericOnly;
        int ISoftKeyboardHost.SoftKeyboardTextLength => _manualTargetText?.Length ?? 0;
        int ISoftKeyboardHost.SoftKeyboardMaxLength => EditTargetMaxLength;
        bool ISoftKeyboardHost.CanSubmitSoftKeyboard => _editTargetFocused && TryParseManualTargetMapId(out _);

        public Action<DestinationEntry> RegisterCurrentMapRequested { get; set; }
        public Action<DestinationEntry> DeleteDestinationRequested { get; set; }
        public Action<DestinationEntry> MoveDestinationRequested { get; set; }
        public Action<DestinationEntry> WorldMapRequested { get; set; }
        public Action<int> ManualMapMoveRequested { get; set; }

        public override void Show()
        {
            base.Show();
            MouseState mouseState = Mouse.GetState();
            _previousMouseState = mouseState;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
            _previousKeyboardState = Keyboard.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
        }

        public override void Hide()
        {
            base.Hide();
            ClearEditTargetFocus();
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetCurrentMapName(string displayName)
        {
            _currentMapName = displayName ?? string.Empty;
        }

        public void SetStatusMessage(string message)
        {
            _statusMessage = message ?? string.Empty;
        }

        public void SetDestinations(IReadOnlyList<DestinationEntry> destinations)
        {
            _destinations.Clear();
            if (destinations != null)
            {
                _destinations.AddRange(destinations);
            }

            if (_destinations.Count == 0)
            {
                _selectedIndex = -1;
                _scrollOffset = 0;
            }
            else
            {
                _selectedIndex = Math.Clamp(_selectedIndex, 0, _destinations.Count - 1);
                ClampScrollOffset();
            }

            UpdateRowButtons();
            UpdateButtonStates();
        }

        public void SetSelectedMapId(int mapId)
        {
            if (mapId <= 0 || _destinations.Count == 0)
            {
                return;
            }

            int index = _destinations.FindIndex(entry => entry.MapId == mapId);
            if (index < 0)
            {
                return;
            }

            _selectedIndex = index;
            _editTargetFocused = false;
            ClampScrollOffset();
            UpdateRowButtons();
            UpdateButtonStates();
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible)
            {
                return;
            }

            MouseState mouseState = Mouse.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            int wheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;

            if (_confirmationVisible)
            {
                KeyboardState keyboardState = Keyboard.GetState();
                if (WasPressed(keyboardState, Keys.Enter))
                {
                    ConfirmPendingRequest();
                }

                if (WasPressed(keyboardState, Keys.Escape))
                {
                    CancelPendingRequest();
                }

                _previousKeyboardState = keyboardState;
                _previousMouseState = mouseState;
                return;
            }

            HandleEditTargetMouseInput(mouseState);
            HandleEditTargetKeyboardInput();
            HandleScrollBarInput(mouseState);

            if (wheelDelta == 0 || _destinations.Count <= MaxVisibleRows || !ContainsPoint(mouseState.X, mouseState.Y))
            {
                _previousMouseState = mouseState;
                return;
            }

            if (wheelDelta > 0)
            {
                _scrollOffset--;
            }
            else if (wheelDelta < 0)
            {
                _scrollOffset++;
            }

            ClampScrollOffset();
            UpdateRowButtons();
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
            DrawFrameLayer(sprite, skeletonMeshRenderer, gameTime, _innerFrame, 0, 0, centerX, centerY, drawReflectionInfo, renderParameters, TickCount);
            DrawFrameLayer(sprite, skeletonMeshRenderer, gameTime, _listFrame, 0, 0, centerX, centerY, drawReflectionInfo, renderParameters, TickCount);

            if (_selectionTexture != null && _selectedIndex >= _scrollOffset && _selectedIndex < _scrollOffset + MaxVisibleRows)
            {
                int row = _selectedIndex - _scrollOffset;
                sprite.Draw(_selectionTexture, new Vector2(Position.X + RowStartX, Position.Y + RowStartY + (row * RowHeight)), Color.White);
            }

            if (_font == null)
            {
                return;
            }

            sprite.DrawString(_font, "Map Transfer", new Vector2(Position.X + 12, Position.Y + 9), Color.White);
            sprite.DrawString(_font, TrimToWidth(_currentMapName, 134f), new Vector2(Position.X + 12, Position.Y + 24), new Color(220, 220, 220));
            string countText = $"{SavedDestinationCount}/{_maxSavedDestinations}";
            Vector2 countSize = _font.MeasureString(countText);
            sprite.DrawString(_font, countText, new Vector2(Position.X + 144 - countSize.X, Position.Y + 9), new Color(200, 200, 200));

            int visibleCount = Math.Min(MaxVisibleRows, _destinations.Count - _scrollOffset);
            for (int row = 0; row < visibleCount; row++)
            {
                DestinationEntry entry = _destinations[_scrollOffset + row];
                Color textColor = (_scrollOffset + row) == _selectedIndex ? Color.White : new Color(230, 230, 230);
                sprite.DrawString(
                    _font,
                    TrimToWidth(entry.DisplayName, RowTextWidth),
                    new Vector2(Position.X + RowTextX, Position.Y + RowTextY + (row * RowHeight)),
                    textColor);
            }

            DrawEditTarget(sprite, TickCount);
            DrawImeCandidateWindow(sprite);
            DrawScrollBar(sprite);

            if (_selectedIndex >= 0 && _selectedIndex < _destinations.Count)
            {
                DestinationEntry entry = _destinations[_selectedIndex];
                sprite.DrawString(_font, TrimToWidth(entry.DetailText, 132f), new Vector2(Position.X + 12, Position.Y + 304), new Color(235, 224, 164));
            }

            sprite.DrawString(_font, TrimToWidth(_statusMessage, 132f), new Vector2(Position.X + 12, Position.Y + 286), new Color(208, 208, 208));

            if (_confirmationVisible)
            {
                DrawConfirmation(sprite);
            }
        }

        private void InitializeCloseAndActionButtons(UIObject registerButton, UIObject deleteButton, UIObject moveButton, UIObject mapButton)
        {
            if (registerButton != null)
            {
                AddButton(registerButton);
                registerButton.ButtonClickReleased += _ => RequestRegisterConfirmation(GetSelectedEntry());
            }

            if (deleteButton != null)
            {
                AddButton(deleteButton);
                deleteButton.ButtonClickReleased += _ =>
                {
                    DestinationEntry entry = GetSelectedEntry();
                    if (entry != null && entry.CanDelete)
                    {
                        RequestDeleteConfirmation(entry);
                    }
                };
            }

            if (moveButton != null)
            {
                AddButton(moveButton);
                moveButton.ButtonClickReleased += _ =>
                {
                    if (_confirmationVisible)
                    {
                        return;
                    }

                    DestinationEntry entry = GetSelectedEntry();
                    if (entry != null)
                    {
                        RequestMoveConfirmation(entry, null);
                        return;
                    }

                    if (TryParseManualTargetMapId(out int targetMapId))
                    {
                        RequestMoveConfirmation(null, targetMapId);
                    }
                };
            }

            if (mapButton != null)
            {
                AddButton(mapButton);
                mapButton.ButtonClickReleased += _ => WorldMapRequested?.Invoke(GetSelectedEntry());
            }
        }

        private void InitializeConfirmationButtons()
        {
            if (_confirmationOkButton != null)
            {
                AddButton(_confirmationOkButton);
                _confirmationButtons.Add(_confirmationOkButton);
                _confirmationOkButton.SetVisible(false);
                _confirmationOkButton.ButtonClickReleased += _ => ConfirmPendingRequest();
            }

            if (_confirmationCancelButton != null)
            {
                AddButton(_confirmationCancelButton);
                _confirmationButtons.Add(_confirmationCancelButton);
                _confirmationCancelButton.SetVisible(false);
                _confirmationCancelButton.ButtonClickReleased += _ => CancelPendingRequest();
            }
        }

        private void InitializeRowButtons(GraphicsDevice device)
        {
            for (int row = 0; row < MaxVisibleRows; row++)
            {
                UIObject rowButton = CreateRowButton(device);
                rowButton.X = RowStartX;
                rowButton.Y = RowStartY + (row * RowHeight);
                int rowIndex = row;
                rowButton.ButtonClickReleased += _ => SelectRow(rowIndex);
                AddButton(rowButton);
                _rowButtons.Add(rowButton);
            }
        }

        private static UIObject CreateRowButton(GraphicsDevice device)
        {
            Texture2D texture = new Texture2D(device, 122, RowHeight);
            Color[] pixels = new Color[122 * RowHeight];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.Transparent;
            }
            texture.SetData(pixels);

            BaseDXDrawableItem normal = new BaseDXDrawableItem(new DXObject(0, 0, texture, 0), false);
            BaseDXDrawableItem disabled = new BaseDXDrawableItem(new DXObject(0, 0, texture, 0), false);
            BaseDXDrawableItem pressed = new BaseDXDrawableItem(new DXObject(0, 0, texture, 0), false);
            BaseDXDrawableItem mouseOver = new BaseDXDrawableItem(new DXObject(0, 0, texture, 0), false);
            return new UIObject(normal, disabled, pressed, mouseOver);
        }

        private void SelectRow(int rowIndex)
        {
            int actualIndex = _scrollOffset + rowIndex;
            if (actualIndex < 0 || actualIndex >= _destinations.Count)
            {
                return;
            }

            _selectedIndex = actualIndex;
            ClearEditTargetFocus();
            UpdateButtonStates();
        }

        private void UpdateRowButtons()
        {
            for (int row = 0; row < _rowButtons.Count; row++)
            {
                int actualIndex = _scrollOffset + row;
                _rowButtons[row].SetVisible(actualIndex < _destinations.Count);
            }
        }

        private void UpdateButtonStates()
        {
            DestinationEntry entry = GetSelectedEntry();
            bool modalBlocked = _confirmationVisible;

            if (_moveButton != null)
            {
                _moveButton.SetEnabled(!modalBlocked && (entry?.CanMove == true || (_selectedIndex < 0 && !string.IsNullOrWhiteSpace(_manualTargetText))));
            }

            if (_deleteButton != null)
            {
                _deleteButton.SetEnabled(!modalBlocked && entry?.CanDelete == true);
            }

            if (_registerButton != null)
            {
                _registerButton.SetEnabled(!modalBlocked);
            }

            if (_mapButton != null)
            {
                _mapButton.SetEnabled(!modalBlocked);
            }

            for (int i = 0; i < _rowButtons.Count; i++)
            {
                _rowButtons[i].SetEnabled(!modalBlocked);
            }
        }

        private DestinationEntry GetSelectedEntry()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _destinations.Count)
            {
                return null;
            }

            return _destinations[_selectedIndex];
        }

        private void ClampScrollOffset()
        {
            int maxScroll = Math.Max(0, _destinations.Count - MaxVisibleRows);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);

            if (_selectedIndex >= 0)
            {
                if (_selectedIndex < _scrollOffset)
                {
                    _scrollOffset = _selectedIndex;
                }
                else if (_selectedIndex >= _scrollOffset + MaxVisibleRows)
                {
                    _scrollOffset = Math.Max(0, _selectedIndex - MaxVisibleRows + 1);
                }
            }
        }

        private void DrawScrollBar(SpriteBatch sprite)
        {
            if (!ShouldShowScrollBar())
            {
                return;
            }

            Rectangle upButtonBounds = GetScrollUpButtonBounds();
            Rectangle downButtonBounds = GetScrollDownButtonBounds();
            Rectangle trackBounds = GetScrollTrackBounds();
            Rectangle thumbBounds = GetScrollThumbBounds();
            bool canScroll = GetMaxScrollOffset() > 0;
            bool leftPressed = Mouse.GetState().LeftButton == ButtonState.Pressed;

            DrawScrollTexture(
                sprite,
                canScroll
                    ? ((leftPressed && upButtonBounds.Contains(_lastMousePosition) && !_isDraggingScrollThumb)
                        ? _scrollUpPressed ?? _scrollUpNormal
                        : _scrollUpNormal)
                    : _scrollUpDisabled ?? _scrollUpNormal,
                upButtonBounds);

            DrawTiledTrack(sprite, canScroll ? _scrollTrackEnabled : _scrollTrackDisabled ?? _scrollTrackEnabled, trackBounds);

            if (canScroll)
            {
                bool thumbPressed = _isDraggingScrollThumb || (leftPressed && thumbBounds.Contains(_lastMousePosition));
                DrawScrollTexture(sprite, thumbPressed ? _scrollThumbPressed ?? _scrollThumbNormal : _scrollThumbNormal, thumbBounds);
            }

            DrawScrollTexture(
                sprite,
                canScroll
                    ? ((leftPressed && downButtonBounds.Contains(_lastMousePosition) && !_isDraggingScrollThumb)
                        ? _scrollDownPressed ?? _scrollDownNormal
                        : _scrollDownNormal)
                    : _scrollDownDisabled ?? _scrollDownNormal,
                downButtonBounds);
        }

        private void DrawEditTarget(SpriteBatch sprite, int tickCount)
        {
            if (_selectionTexture == null)
            {
                return;
            }

            Rectangle bounds = GetEditTargetBounds();
            Color borderColor = _editTargetFocused ? new Color(247, 222, 112) : new Color(88, 88, 88);
            sprite.Draw(_selectionTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height), new Color(20, 20, 20, 185));
            sprite.Draw(_selectionTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), borderColor);
            sprite.Draw(_selectionTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), borderColor);
            sprite.Draw(_selectionTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), borderColor);
            sprite.Draw(_selectionTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), borderColor);

            string displayText = string.IsNullOrEmpty(_manualTargetText) && !_editTargetFocused
                ? "Target map ID"
                : BuildVisibleTargetText();
            Color textColor = string.IsNullOrEmpty(_manualTargetText) && !_editTargetFocused
                ? new Color(136, 136, 136)
                : Color.White;
            sprite.DrawString(
                _font,
                TrimToWidth(displayText, EditTargetWidth - (EditTargetTextInsetX * 2)),
                new Vector2(bounds.X + EditTargetTextInsetX, bounds.Y + EditTargetTextInsetY),
                textColor);

            if (_editTargetFocused && (tickCount / 450) % 2 == 0)
            {
                float textWidth = _font.MeasureString(BuildVisibleTargetText()).X;
                int caretX = bounds.X + EditTargetTextInsetX + Math.Min((int)textWidth, EditTargetWidth - 6);
                sprite.Draw(_selectionTexture, new Rectangle(caretX, bounds.Y + 2, 1, bounds.Height - 4), Color.White);
            }
        }

        private void HandleEditTargetMouseInput(MouseState mouseState)
        {
            bool leftClicked = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            if (!leftClicked)
            {
                return;
            }

            if (_confirmationVisible)
            {
                return;
            }

            Point mousePoint = new Point(mouseState.X, mouseState.Y);
            if (GetEditTargetBounds().Contains(mousePoint))
            {
                _editTargetFocused = true;
                _softKeyboardActive = true;
                _selectedIndex = -1;
                UpdateButtonStates();
                return;
            }

            if (ContainsPoint(mousePoint.X, mousePoint.Y))
            {
                ClearEditTargetFocus();
                UpdateButtonStates();
            }
        }

        private void HandleEditTargetKeyboardInput()
        {
            if (!_editTargetFocused)
            {
                ClearCompositionText();
                _previousKeyboardState = Keyboard.GetState();
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            bool ctrl = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);

            if (ctrl && WasPressed(keyboardState, Keys.V))
            {
                ClearCompositionText();
                HandleClipboardPaste();
            }
            else if (WasPressed(keyboardState, Keys.Back) && _manualTargetText.Length > 0)
            {
                ClearCompositionText();
                _manualTargetText = _manualTargetText[..^1];
            }
            else if (WasPressed(keyboardState, Keys.Enter))
            {
                if (TryParseManualTargetMapId(out int targetMapId))
                {
                    ClearCompositionText();
                    _softKeyboardActive = false;
                    RequestMoveConfirmation(null, targetMapId);
                }
            }
            else if (WasPressed(keyboardState, Keys.Escape))
            {
                ClearEditTargetFocus();
            }

            _previousKeyboardState = keyboardState;
            UpdateButtonStates();
        }

        private void HandleScrollBarInput(MouseState mouseState)
        {
            if (!ShouldShowScrollBar())
            {
                _isDraggingScrollThumb = false;
                return;
            }

            if (_isDraggingScrollThumb)
            {
                if (mouseState.LeftButton == ButtonState.Released)
                {
                    _isDraggingScrollThumb = false;
                }
                else
                {
                    UpdateScrollOffsetFromThumb(mouseState.Y);
                }

                return;
            }

            bool leftClicked = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            if (!leftClicked)
            {
                return;
            }

            Rectangle scrollBarBounds = GetScrollBarBounds();
            if (!scrollBarBounds.Contains(mouseState.X, mouseState.Y))
            {
                return;
            }

            if (GetScrollUpButtonBounds().Contains(mouseState.X, mouseState.Y))
            {
                ScrollUp();
                return;
            }

            if (GetScrollDownButtonBounds().Contains(mouseState.X, mouseState.Y))
            {
                ScrollDown();
                return;
            }

            Rectangle thumbBounds = GetScrollThumbBounds();
            if (thumbBounds.Contains(mouseState.X, mouseState.Y))
            {
                _isDraggingScrollThumb = true;
                _scrollThumbDragOffsetY = mouseState.Y - thumbBounds.Y;
                return;
            }

            Rectangle trackBounds = GetScrollTrackBounds();
            if (trackBounds.Contains(mouseState.X, mouseState.Y))
            {
                int thumbCenter = thumbBounds.Y + (thumbBounds.Height / 2);
                if (mouseState.Y < thumbCenter)
                {
                    _scrollOffset = Math.Max(0, _scrollOffset - MaxVisibleRows);
                }
                else if (mouseState.Y > thumbCenter)
                {
                    _scrollOffset = Math.Min(GetMaxScrollOffset(), _scrollOffset + MaxVisibleRows);
                }

                ClampScrollOffset();
                UpdateRowButtons();
            }
        }

        private void HandleClipboardPaste()
        {
            try
            {
                if (!System.Windows.Forms.Clipboard.ContainsText())
                {
                    return;
                }

                string clipboardText = System.Windows.Forms.Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(clipboardText))
                {
                    return;
                }

                foreach (char character in clipboardText)
                {
                    if (!char.IsDigit(character) || _manualTargetText.Length >= EditTargetMaxLength)
                    {
                        continue;
                    }

                    _manualTargetText += character;
                }
            }
            catch
            {
            }
        }

        private bool TryParseManualTargetMapId(out int targetMapId)
        {
            return int.TryParse(_manualTargetText, out targetMapId) && targetMapId > 0;
        }

        private void ScrollUp()
        {
            if (_scrollOffset <= 0)
            {
                return;
            }

            _scrollOffset--;
            ClampScrollOffset();
            UpdateRowButtons();
        }

        private void ScrollDown()
        {
            if (_scrollOffset >= GetMaxScrollOffset())
            {
                return;
            }

            _scrollOffset++;
            ClampScrollOffset();
            UpdateRowButtons();
        }

        private void RequestRegisterConfirmation(DestinationEntry selectedEntry)
        {
            string targetLabel = ResolveRegisterTargetLabel(selectedEntry);
            RequestConfirmation(
                MapTransferClientParityText.BuildRegisterConfirmationPrompt(targetLabel),
                () => RegisterCurrentMapRequested?.Invoke(selectedEntry));
        }

        private void RequestDeleteConfirmation(DestinationEntry entry)
        {
            if (entry == null || !entry.CanDelete)
            {
                return;
            }

            string targetLabel = TrimToWidth(entry.DisplayName, 150f);
            if (string.IsNullOrWhiteSpace(targetLabel))
            {
                targetLabel = entry.MapId > 0 ? entry.MapId.ToString() : $"saved slot {entry.SavedSlotIndex + 1}";
            }

            RequestConfirmation(
                MapTransferClientParityText.BuildDeleteConfirmationPrompt(targetLabel),
                () => DeleteDestinationRequested?.Invoke(entry));
        }

        private void RequestMoveConfirmation(DestinationEntry entry, int? manualTargetMapId)
        {
            string targetLabel;
            Action confirmationAction;

            if (entry != null)
            {
                targetLabel = TrimToWidth(entry.DisplayName, 150f);
                confirmationAction = () => MoveDestinationRequested?.Invoke(entry);
            }
            else if (manualTargetMapId.HasValue && manualTargetMapId.Value > 0)
            {
                targetLabel = manualTargetMapId.Value.ToString();
                confirmationAction = () => ManualMapMoveRequested?.Invoke(manualTargetMapId.Value);
            }
            else
            {
                return;
            }

            RequestConfirmation(MapTransferClientParityText.BuildMoveConfirmationPrompt(targetLabel), confirmationAction);
        }

        private void RequestConfirmation(string message, Action confirmationAction)
        {
            if (confirmationAction == null)
            {
                return;
            }

            if (_confirmationTexture == null || _confirmationOkButton == null || _confirmationCancelButton == null)
            {
                confirmationAction.Invoke();
                return;
            }

            _confirmationMessage = message ?? string.Empty;
            _pendingConfirmationAction = confirmationAction;
            _confirmationVisible = true;
            ClearEditTargetFocus();
            UpdateConfirmationLayout();
            UpdateButtonStates();
        }

        private void ConfirmPendingRequest()
        {
            Action action = _pendingConfirmationAction;
            CancelPendingRequest();
            action?.Invoke();
        }

        private void CancelPendingRequest()
        {
            _confirmationVisible = false;
            _confirmationMessage = string.Empty;
            _pendingConfirmationAction = null;
            UpdateConfirmationLayout();
            UpdateButtonStates();
        }

        private string ResolveRegisterTargetLabel(DestinationEntry selectedEntry)
        {
            if (_selectedIndex < 0 && TryParseManualTargetMapId(out int manualTargetMapId))
            {
                return manualTargetMapId.ToString();
            }

            DestinationEntry registerTarget = selectedEntry?.IsSavedSlot == true
                ? null
                : selectedEntry;
            if (registerTarget != null)
            {
                if (registerTarget.MapId > 0)
                {
                    return TrimToWidth(registerTarget.DisplayName, 150f);
                }
            }

            return string.IsNullOrWhiteSpace(_currentMapName)
                ? "the current field"
                : TrimToWidth(_currentMapName, 150f);
        }

        private void UpdateConfirmationLayout()
        {
            Rectangle bounds = GetConfirmationBounds();
            if (_confirmationOkButton != null)
            {
                _confirmationOkButton.X = bounds.X - Position.X + 34;
                _confirmationOkButton.Y = bounds.Y - Position.Y + 28;
                _confirmationOkButton.SetVisible(_confirmationVisible);
                _confirmationOkButton.SetEnabled(_confirmationVisible);
            }

            if (_confirmationCancelButton != null)
            {
                _confirmationCancelButton.X = bounds.X - Position.X + 111;
                _confirmationCancelButton.Y = bounds.Y - Position.Y + 28;
                _confirmationCancelButton.SetVisible(_confirmationVisible);
                _confirmationCancelButton.SetEnabled(_confirmationVisible);
            }
        }

        private void DrawConfirmation(SpriteBatch sprite)
        {
            Rectangle bounds = GetConfirmationBounds();
            if (_confirmationTexture != null)
            {
                sprite.Draw(_confirmationTexture, new Vector2(bounds.X, bounds.Y), Color.White);
            }
            else if (_selectionTexture != null)
            {
                sprite.Draw(_selectionTexture, bounds, new Color(38, 38, 38, 235));
            }

            if (_font == null)
            {
                return;
            }

            float lineY = bounds.Y + 12f;
            foreach (string line in WrapText(_confirmationMessage, bounds.Width - 18f))
            {
                Vector2 size = _font.MeasureString(line);
                float lineX = bounds.X + (bounds.Width - size.X) / 2f;
                sprite.DrawString(_font, line, new Vector2(lineX, lineY), new Color(60, 45, 0));
                lineY += Math.Max(12f, size.Y);
            }
        }

        private Rectangle GetConfirmationBounds()
        {
            int width = _confirmationTexture?.Width ?? 206;
            int height = _confirmationTexture?.Height ?? 60;
            int x = Position.X + ((FrameWidth - width) / 2);
            int y = Position.Y + 120;
            return new Rectangle(x, y, width, height);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');
            for (int paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
            {
                string paragraph = paragraphs[paragraphIndex];
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    yield return string.Empty;
                    continue;
                }

                string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    continue;
                }

                string currentLine = words[0];
                for (int i = 1; i < words.Length; i++)
                {
                    string candidate = $"{currentLine} {words[i]}";
                    if (_font.MeasureString(candidate).X <= maxWidth)
                    {
                        currentLine = candidate;
                        continue;
                    }

                    yield return currentLine;
                    currentLine = words[i];
                }

                yield return currentLine;
                if (paragraphIndex < paragraphs.Length - 1)
                {
                    yield return string.Empty;
                }
            }
        }

        private Rectangle GetEditTargetBounds()
        {
            return new Rectangle(
                Position.X + EditTargetX,
                Position.Y + EditTargetY,
                EditTargetWidth,
                EditTargetHeight);
        }

        private Rectangle GetScrollBarBounds()
        {
            int width = Math.Max(
                Math.Max(_scrollUpNormal?.Width ?? 0, _scrollDownNormal?.Width ?? 0),
                Math.Max(_scrollTrackEnabled?.Width ?? 0, _scrollThumbNormal?.Width ?? 0));
            if (width <= 0)
            {
                width = 11;
            }

            return new Rectangle(Position.X + ScrollBarX, Position.Y + ScrollBarY, width, ScrollBarHeight);
        }

        private Rectangle GetScrollUpButtonBounds()
        {
            Rectangle bounds = GetScrollBarBounds();
            int height = _scrollUpNormal?.Height ?? ScrollBarButtonHeight;
            return new Rectangle(bounds.X, bounds.Y, bounds.Width, height);
        }

        private Rectangle GetScrollDownButtonBounds()
        {
            Rectangle bounds = GetScrollBarBounds();
            int height = _scrollDownNormal?.Height ?? ScrollBarButtonHeight;
            return new Rectangle(bounds.X, bounds.Bottom - height, bounds.Width, height);
        }

        private Rectangle GetScrollTrackBounds()
        {
            Rectangle bounds = GetScrollBarBounds();
            Rectangle upBounds = GetScrollUpButtonBounds();
            Rectangle downBounds = GetScrollDownButtonBounds();
            int top = upBounds.Bottom;
            int bottom = downBounds.Y;
            return new Rectangle(bounds.X, top, bounds.Width, Math.Max(0, bottom - top));
        }

        private Rectangle GetScrollThumbBounds()
        {
            Rectangle trackBounds = GetScrollTrackBounds();
            int thumbHeight = Math.Min(trackBounds.Height, Math.Max(1, _scrollThumbNormal?.Height ?? trackBounds.Width));
            int maxScroll = GetMaxScrollOffset();
            if (maxScroll <= 0)
            {
                return new Rectangle(trackBounds.X, trackBounds.Y, trackBounds.Width, thumbHeight);
            }

            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int thumbTop = trackBounds.Y + (int)Math.Round((_scrollOffset / (double)maxScroll) * travel);
            return new Rectangle(trackBounds.X, thumbTop, trackBounds.Width, thumbHeight);
        }

        private void UpdateScrollOffsetFromThumb(int mouseY)
        {
            Rectangle trackBounds = GetScrollTrackBounds();
            Rectangle thumbBounds = GetScrollThumbBounds();
            int maxScroll = GetMaxScrollOffset();
            if (maxScroll <= 0)
            {
                return;
            }

            int travel = Math.Max(0, trackBounds.Height - thumbBounds.Height);
            if (travel <= 0)
            {
                _scrollOffset = 0;
            }
            else
            {
                int thumbTop = Math.Clamp(mouseY - _scrollThumbDragOffsetY, trackBounds.Y, trackBounds.Y + travel);
                double ratio = (thumbTop - trackBounds.Y) / (double)travel;
                _scrollOffset = (int)Math.Round(ratio * maxScroll);
            }

            ClampScrollOffset();
            UpdateRowButtons();
        }

        private void DrawTiledTrack(SpriteBatch sprite, Texture2D texture, Rectangle bounds)
        {
            if (texture == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            for (int y = bounds.Y; y < bounds.Bottom; y += texture.Height)
            {
                int tileHeight = Math.Min(texture.Height, bounds.Bottom - y);
                Rectangle destination = new Rectangle(bounds.X, y, bounds.Width, tileHeight);
                Rectangle source = new Rectangle(0, 0, Math.Min(texture.Width, bounds.Width), tileHeight);
                sprite.Draw(texture, destination, source, Color.White);
            }
        }

        private void DrawScrollTexture(SpriteBatch sprite, Texture2D texture, Rectangle bounds)
        {
            if (texture == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            sprite.Draw(texture, bounds, Color.White);
        }

        private void DrawImeCandidateWindow(SpriteBatch sprite)
        {
            if (_font == null || _selectionTexture == null || !_candidateListState.HasCandidates)
            {
                return;
            }

            Rectangle bounds = GetImeCandidateWindowBounds(sprite.GraphicsDevice.Viewport);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            sprite.Draw(_selectionTexture, bounds, new Color(33, 33, 41, 235));
            sprite.Draw(_selectionTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(214, 214, 214, 220));
            sprite.Draw(_selectionTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(214, 214, 214, 220));
            sprite.Draw(_selectionTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(214, 214, 214, 220));
            sprite.Draw(_selectionTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(214, 214, 214, 220));

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(GetVisibleCandidateCount(), _candidateListState.Candidates.Count - start);
            int rowHeight = Math.Max(_font.LineSpacing + 1, 16);
            int numberWidth = (int)Math.Ceiling(_font.MeasureString($"{Math.Max(1, count)}.").X);
            for (int i = 0; i < count; i++)
            {
                int candidateIndex = start + i;
                Rectangle rowBounds = new(bounds.X + 2, bounds.Y + 2 + (i * rowHeight), bounds.Width - 4, rowHeight);
                bool selected = candidateIndex == _candidateListState.Selection;
                if (selected)
                {
                    sprite.Draw(_selectionTexture, rowBounds, new Color(89, 108, 147, 220));
                }

                sprite.DrawString(_font, $"{i + 1}.", new Vector2(rowBounds.X + 4, rowBounds.Y), selected ? Color.White : new Color(222, 222, 222));
                sprite.DrawString(
                    _font,
                    _candidateListState.Candidates[candidateIndex] ?? string.Empty,
                    new Vector2(rowBounds.X + 8 + numberWidth, rowBounds.Y),
                    selected ? Color.White : new Color(240, 235, 200));
            }
        }

        private Rectangle GetImeCandidateWindowBounds(Viewport viewport)
        {
            if (ImeCandidateWindowRendering.ShouldPreferNativeWindow(_candidateListState))
            {
                return Rectangle.Empty;
            }

            int visibleCount = GetVisibleCandidateCount();
            if (visibleCount <= 0 || _font == null)
            {
                return Rectangle.Empty;
            }

            int widestEntryWidth = 0;
            for (int i = 0; i < visibleCount; i++)
            {
                int candidateIndex = Math.Clamp(_candidateListState.PageStart + i, 0, _candidateListState.Candidates.Count - 1);
                string candidateText = _candidateListState.Candidates[candidateIndex] ?? string.Empty;
                int entryWidth = (int)Math.Ceiling(_font.MeasureString($"{i + 1}.").X + _font.MeasureString(candidateText).X) + 16;
                widestEntryWidth = Math.Max(widestEntryWidth, entryWidth);
            }

            int width = Math.Max(96, widestEntryWidth + 14);
            int height = (visibleCount * Math.Max(_font.LineSpacing + 1, 16)) + 4;
            Rectangle ownerBounds = GetEditTargetBounds();
            int x = Math.Clamp(ownerBounds.X, 0, Math.Max(0, viewport.Width - width));
            int y = ownerBounds.Bottom + 2;
            if (y + height > viewport.Height)
            {
                y = Math.Max(0, ownerBounds.Y - height - 2);
            }

            return new Rectangle(x, y, width, height);
        }

        private int GetVisibleCandidateCount()
        {
            if (!_candidateListState.HasCandidates)
            {
                return 0;
            }

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int pageSize = _candidateListState.PageSize > 0 ? _candidateListState.PageSize : _candidateListState.Candidates.Count;
            return Math.Max(0, Math.Min(pageSize, _candidateListState.Candidates.Count - start));
        }

        private int GetMaxScrollOffset()
        {
            return Math.Max(0, _destinations.Count - MaxVisibleRows);
        }

        private bool ShouldShowScrollBar()
        {
            return _maxSavedDestinations > MaxVisibleRows
                && _scrollUpNormal != null
                && _scrollDownNormal != null
                && _scrollTrackEnabled != null
                && _scrollThumbNormal != null;
        }

        public override void HandleCommittedText(string text)
        {
            if (!_editTargetFocused || string.IsNullOrEmpty(text))
            {
                return;
            }

            ClearCompositionText();
            foreach (char character in text)
            {
                if (!char.IsDigit(character) || _manualTargetText.Length >= EditTargetMaxLength)
                {
                    continue;
                }

                _manualTargetText += character;
            }

            UpdateButtonStates();
        }

        public override void HandleCompositionText(string text)
        {
            HandleCompositionState(new ImeCompositionState(text ?? string.Empty, Array.Empty<int>(), -1));
        }

        public override void HandleCompositionState(ImeCompositionState state)
        {
            if (!_editTargetFocused)
            {
                ClearCompositionText();
                return;
            }

            _compositionText = SanitizeCompositionText(state?.Text);
            if (_compositionText.Length == 0)
            {
                ClearImeCandidateList();
            }
        }

        public override void ClearCompositionText()
        {
            _compositionText = string.Empty;
            ClearImeCandidateList();
        }

        public override void HandleImeCandidateList(ImeCandidateListState state)
        {
            _candidateListState = _editTargetFocused && state != null && state.HasCandidates
                ? state
                : ImeCandidateListState.Empty;
        }

        public override void ClearImeCandidateList()
        {
            _candidateListState = ImeCandidateListState.Empty;
        }

        Rectangle ISoftKeyboardHost.GetSoftKeyboardAnchorBounds() => GetEditTargetBounds();

        bool ISoftKeyboardHost.TryInsertSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!char.IsDigit(character) || _manualTargetText.Length >= EditTargetMaxLength)
            {
                return false;
            }

            _manualTargetText += character;
            UpdateButtonStates();
            return true;
        }

        bool ISoftKeyboardHost.TryReplaceLastSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!char.IsDigit(character))
            {
                errorMessage = "Only numeric map ids are supported.";
                return false;
            }

            if (string.IsNullOrEmpty(_manualTargetText))
            {
                errorMessage = "Nothing to replace.";
                return false;
            }

            _manualTargetText = _manualTargetText[..^1] + character;
            UpdateButtonStates();
            return true;
        }

        bool ISoftKeyboardHost.TryBackspaceSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrEmpty(_manualTargetText))
            {
                return false;
            }

            _manualTargetText = _manualTargetText[..^1];
            UpdateButtonStates();
            return true;
        }

        bool ISoftKeyboardHost.TrySubmitSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!TryParseManualTargetMapId(out int targetMapId))
            {
                errorMessage = "Enter a valid map id first.";
                return false;
            }

            RequestMoveConfirmation(null, targetMapId);
            return true;
        }

        void ISoftKeyboardHost.OnSoftKeyboardClosed()
        {
            _softKeyboardActive = false;
            ClearCompositionText();
            UpdateButtonStates();
        }

        void ISoftKeyboardHost.SetSoftKeyboardCompositionText(string text)
        {
            HandleCompositionText(text);
        }

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private string BuildVisibleTargetText()
        {
            return string.IsNullOrEmpty(_compositionText)
                ? _manualTargetText
                : _manualTargetText + _compositionText;
        }

        private string SanitizeCompositionText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            List<char> accepted = new(text.Length);
            foreach (char character in text)
            {
                if (!char.IsDigit(character) || _manualTargetText.Length + accepted.Count >= EditTargetMaxLength)
                {
                    continue;
                }

                accepted.Add(character);
            }

            return accepted.Count == 0
                ? string.Empty
                : new string(accepted.ToArray());
        }

        private void ClearEditTargetFocus()
        {
            _editTargetFocused = false;
            _softKeyboardActive = false;
            ClearCompositionText();
        }

        private int FrameWidth => CurrentFrame?.Texture?.Width ?? _confirmationTexture?.Width ?? 0;

        private void DrawFrameLayer(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            IDXObject layer,
            int offsetX,
            int offsetY,
            int centerX,
            int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (layer == null)
            {
                return;
            }

            layer.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + offsetX,
                Position.Y + offsetY,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private string TrimToWidth(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (_font.MeasureString(text).X <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            string value = text;
            while (value.Length > 0 && _font.MeasureString(value + ellipsis).X > maxWidth)
            {
                value = value[..^1];
            }

            return string.IsNullOrEmpty(value) ? ellipsis : value + ellipsis;
        }
    }
}
