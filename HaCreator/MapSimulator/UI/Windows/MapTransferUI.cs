using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class MapTransferUI : UIWindowBase
    {
        public sealed class DestinationEntry
        {
            public int MapId { get; init; }
            public string DisplayName { get; init; }
            public string DetailText { get; init; }
            public string TargetPortalName { get; init; }
            public int SavedSlotIndex { get; init; } = -1;
            public bool CanDelete { get; init; }
            public bool CanMove => MapId > 0;
            public bool IsSavedSlot => SavedSlotIndex >= 0;
        }

        private const int MaxVisibleRows = 5;
        private const int RowHeight = 17;
        private const int RowStartX = 19;
        private const int RowStartY = 39;
        private const int RowTextX = 25;
        private const int RowTextY = 41;
        private const int RowTextWidth = 110;

        private readonly IDXObject _innerFrame;
        private readonly IDXObject _listFrame;
        private readonly Texture2D _selectionTexture;
        private readonly List<UIObject> _rowButtons = new();
        private readonly List<DestinationEntry> _destinations = new();
        private readonly UIObject _registerButton;
        private readonly UIObject _deleteButton;
        private readonly UIObject _moveButton;
        private readonly UIObject _mapButton;
        private readonly int _maxSavedDestinations;

        private SpriteFont _font;
        private string _currentMapName = string.Empty;
        private string _statusMessage = "Register maps or select a route to transfer.";
        private int _selectedIndex = -1;
        private int _scrollOffset;
        private int _previousScrollWheelValue;

        public MapTransferUI(
            IDXObject frame,
            IDXObject innerFrame,
            IDXObject listFrame,
            Texture2D selectionTexture,
            UIObject registerButton,
            UIObject deleteButton,
            UIObject moveButton,
            UIObject mapButton,
            int maxSavedDestinations,
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
            _maxSavedDestinations = Math.Max(MaxVisibleRows, maxSavedDestinations);

            InitializeCloseAndActionButtons(registerButton, deleteButton, moveButton, mapButton);
            InitializeRowButtons(device);
            UpdateButtonStates();
        }

        public override string WindowName => MapSimulatorWindowNames.MapTransfer;
        public int MaxSavedDestinations => _maxSavedDestinations;

        public Action<DestinationEntry> RegisterCurrentMapRequested { get; set; }
        public Action<DestinationEntry> DeleteDestinationRequested { get; set; }
        public Action<DestinationEntry> MoveDestinationRequested { get; set; }
        public Action<DestinationEntry> WorldMapRequested { get; set; }

        public override void Show()
        {
            base.Show();
            _previousScrollWheelValue = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
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

            Microsoft.Xna.Framework.Input.MouseState mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            int wheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;

            if (wheelDelta == 0 || _destinations.Count <= MaxVisibleRows || !ContainsPoint(mouseState.X, mouseState.Y))
            {
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

            if (_selectedIndex >= 0 && _selectedIndex < _destinations.Count)
            {
                DestinationEntry entry = _destinations[_selectedIndex];
                sprite.DrawString(_font, TrimToWidth(entry.DetailText, 132f), new Vector2(Position.X + 12, Position.Y + 304), new Color(235, 224, 164));
            }

            sprite.DrawString(_font, TrimToWidth(_statusMessage, 132f), new Vector2(Position.X + 12, Position.Y + 286), new Color(208, 208, 208));

            if (_destinations.Count > MaxVisibleRows)
            {
                string page = $"{_scrollOffset + 1}-{Math.Min(_scrollOffset + MaxVisibleRows, _destinations.Count)}/{_destinations.Count}";
                Vector2 pageSize = _font.MeasureString(page);
                sprite.DrawString(_font, page, new Vector2(Position.X + 144 - pageSize.X, Position.Y + 9), new Color(200, 200, 200));
            }
        }

        private void InitializeCloseAndActionButtons(UIObject registerButton, UIObject deleteButton, UIObject moveButton, UIObject mapButton)
        {
            if (registerButton != null)
            {
                AddButton(registerButton);
                registerButton.ButtonClickReleased += _ => RegisterCurrentMapRequested?.Invoke(GetSelectedEntry());
            }

            if (deleteButton != null)
            {
                AddButton(deleteButton);
                deleteButton.ButtonClickReleased += _ =>
                {
                    DestinationEntry entry = GetSelectedEntry();
                    if (entry != null && entry.CanDelete)
                    {
                        DeleteDestinationRequested?.Invoke(entry);
                    }
                };
            }

            if (moveButton != null)
            {
                AddButton(moveButton);
                moveButton.ButtonClickReleased += _ =>
                {
                    DestinationEntry entry = GetSelectedEntry();
                    if (entry != null)
                    {
                        MoveDestinationRequested?.Invoke(entry);
                    }
                };
            }

            if (mapButton != null)
            {
                AddButton(mapButton);
                mapButton.ButtonClickReleased += _ => WorldMapRequested?.Invoke(GetSelectedEntry());
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
            if (_moveButton != null)
            {
                _moveButton.SetEnabled(entry?.CanMove == true);
            }

            if (_deleteButton != null)
            {
                _deleteButton.SetEnabled(entry?.CanDelete == true);
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
