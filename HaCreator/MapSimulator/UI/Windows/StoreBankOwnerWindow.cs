using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class StoreBankOwnerWindow : UIWindowBase
    {
        private const int VisibleRowCount = 5;
        private const int RowX = 10;
        private const int RowY = 93;
        private const int RowWidth = 178;
        private const int RowHeight = 35;
        private const int RowPitch = 42;
        private const int RowHitHeight = 42;
        private const int RowIconX = 12;
        private const int RowIconY = 1;
        private const int RowIconSize = 32;
        private const int RowClientStockRightX = 160;
        private const int RowCashIconRightX = 42;
        private const int RowPrimaryTextX = 53;
        private const int RowPrimaryTextY = 3;
        private const int RowSecondaryTextX = 53;
        private const int RowSecondaryTextY = 20;
        private const int ScrollBarX = 190;
        private const int ScrollBarY = 93;
        private const int ScrollBarHeight = 203;
        private const int MoneyRightX = 170;
        private const int MoneyY = 304;
        private const int FooterX = 12;
        private const int FooterY = 308;
        private const int FooterWidth = 182;
        private static readonly Color MoneyShadowColor = Color.Black;
        private static readonly Color MoneyTextColor = Color.White;

        private readonly struct Layer
        {
            public Layer(IDXObject drawable, Point offset)
            {
                Drawable = drawable;
                Offset = offset;
            }

            public IDXObject Drawable { get; }
            public Point Offset { get; }
        }

        private readonly List<Layer> _layers = new();
        private readonly List<UIObject> _rowButtons = new();
        private readonly UIObject _getButton;
        private readonly Texture2D _rowTexture;
        private readonly Texture2D _rowLineTexture;
        private readonly Texture2D[] _itemNumberDigits;
        private readonly Texture2D _cashIconTexture;
        private readonly VerticalScrollbarSkin _scrollbarSkin;
        private readonly Texture2D _selectionTexture;
        private readonly Dictionary<int, Texture2D> _itemIconTextureCache = new();

        private PacketOwnedStoreBankDialogRuntime _runtime;
        private Func<string> _footerProvider;
        private Action _getAction;
        private Func<bool> _closeAction;
        private Func<int, Texture2D> _itemIconProvider;
        private SpriteFont _font;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private Point _lastMousePosition;
        private int _scrollOffset;
        private int _selectedRowIndex = -1;
        private int _lastOwnerRowRevision = -1;
        private StoreBankOwnerSelectionAnchor? _selectionAnchor;
        private bool _draggingScrollThumb;
        private int _scrollThumbDragOffsetY;

        internal StoreBankOwnerWindow(
            IDXObject frame,
            UIObject getButton,
            UIObject exitButton,
            Texture2D rowTexture,
            Texture2D rowLineTexture,
            Texture2D[] itemNumberDigits,
            Texture2D cashIconTexture,
            VerticalScrollbarSkin scrollbarSkin,
            GraphicsDevice device)
            : base(frame)
        {
            _getButton = getButton;
            _rowTexture = rowTexture;
            _rowLineTexture = rowLineTexture;
            _itemNumberDigits = itemNumberDigits ?? Array.Empty<Texture2D>();
            _cashIconTexture = cashIconTexture;
            _scrollbarSkin = scrollbarSkin;

            _selectionTexture = new Texture2D(device, 1, 1);
            _selectionTexture.SetData(new[] { Color.White });

            if (_getButton != null)
            {
                AddButton(_getButton);
                _getButton.ButtonClickReleased += _ => _getAction?.Invoke();
            }

            if (exitButton != null)
            {
                AddButton(exitButton);
                exitButton.ButtonClickReleased += _ =>
                {
                    bool handled = _closeAction?.Invoke() != false;
                    if (handled)
                    {
                        Hide();
                    }
                };
            }

            for (int i = 0; i < VisibleRowCount; i++)
            {
                UIObject rowButton = CreateTransparentButton(device, RowWidth, RowHitHeight);
                rowButton.X = RowX;
                rowButton.Y = RowY + (i * RowPitch);
                int visibleRowIndex = i;
                rowButton.ButtonClickReleased += _ => SelectVisibleRow(visibleRowIndex);
                AddButton(rowButton);
                _rowButtons.Add(rowButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.StoreBank;

        internal int SelectedOwnerRowIndex => _selectedRowIndex;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        internal void SetRuntime(PacketOwnedStoreBankDialogRuntime runtime)
        {
            _runtime = runtime;
            _lastOwnerRowRevision = runtime?.OwnerRowRevision ?? -1;
            ClampState();
            UpdateInteractiveState();
        }

        internal void SetFooterProvider(Func<string> footerProvider)
        {
            _footerProvider = footerProvider;
        }

        internal void SetGetAction(Action getAction)
        {
            _getAction = getAction;
        }

        internal void SetCloseAction(Func<bool> closeAction)
        {
            _closeAction = closeAction;
        }

        internal void SetItemIconProvider(Func<int, Texture2D> itemIconProvider)
        {
            _itemIconProvider = itemIconProvider;
            _itemIconTextureCache.Clear();
        }

        internal void AddLayer(IDXObject drawable, Point offset)
        {
            if (drawable != null)
            {
                _layers.Add(new Layer(drawable, offset));
            }
        }

        public override void Show()
        {
            base.Show();
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            _draggingScrollThumb = false;
            ClampState();
            UpdateInteractiveState();
        }

        public override void Hide()
        {
            base.Hide();
            _draggingScrollThumb = false;
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible)
            {
                return;
            }

            MouseState mouseState = Mouse.GetState();
            _lastMousePosition = mouseState.Position;
            SyncOwnerRowRevision();
            UpdateScrollThumbDrag(mouseState);
            HandleKeyboardInput();

            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (wheelDelta != 0 && GetListBounds().Contains(mouseState.Position))
            {
                AdjustScrollOffset(wheelDelta > 0 ? -1 : 1);
            }

            _previousMouseState = mouseState;
            _previousKeyboardState = Keyboard.GetState();
            ClampState();
            UpdateInteractiveState();
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            bool handled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            if (!IsVisible)
            {
                _previousMouseState = mouseState;
                return handled;
            }

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            if (!handled && leftJustPressed && TryHandleScrollBarMouseDown(mouseState))
            {
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                handled = true;
            }

            _previousMouseState = mouseState;
            return handled;
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
            foreach (Layer layer in _layers)
            {
                layer.Drawable.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    Position.X + layer.Offset.X,
                    Position.Y + layer.Offset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }

            DrawRows(sprite);
            DrawScrollBar(sprite);
            DrawMoney(sprite);
            DrawFooter(sprite);
        }

        private void HandleKeyboardInput()
        {
            KeyboardState keyboardState = Keyboard.GetState();
            if (WasPressed(keyboardState, Keys.Up))
            {
                MoveSelection(-1);
            }
            else if (WasPressed(keyboardState, Keys.Down))
            {
                MoveSelection(1);
            }
            else if (WasPressed(keyboardState, Keys.PageUp))
            {
                AdjustScrollOffset(-VisibleRowCount);
            }
            else if (WasPressed(keyboardState, Keys.PageDown))
            {
                AdjustScrollOffset(VisibleRowCount);
            }
            else if (WasPressed(keyboardState, Keys.Home))
            {
                SelectAbsoluteRow(0);
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                SelectAbsoluteRow(GetRows().Count - 1);
            }
            else if (WasPressed(keyboardState, Keys.Enter) && IsGetButtonEnabled())
            {
                _getAction?.Invoke();
            }
        }

        private void DrawRows(SpriteBatch sprite)
        {
            IReadOnlyList<StoreBankOwnerRowSnapshot> rows = GetRows();
            int ownerSlotCount = Math.Max(0, _runtime?.OwnerSlotCount ?? 0);
            int visibleCount = VisibleRowCount;
            for (int i = 0; i < visibleCount; i++)
            {
                int rowIndex = _scrollOffset + i;
                int drawX = Position.X + RowX;
                int drawY = Position.Y + RowY + (i * RowPitch);

                bool drawsClientSlotBackground = rowIndex < ownerSlotCount
                    || (rowIndex >= 0 && rowIndex < rows.Count && rows[rowIndex].DrawsClientSlotBackground);
                if (_rowTexture != null && drawsClientSlotBackground)
                {
                    sprite.Draw(_rowTexture, new Vector2(drawX, drawY), Color.White);
                }

                if (rowIndex >= 0 && rowIndex < rows.Count && rowIndex == _selectedRowIndex)
                {
                    sprite.Draw(_selectionTexture, new Rectangle(drawX, drawY, RowWidth, RowHeight), new Color(255, 224, 128, 70));
                }

                DrawRowSeparator(sprite, drawX, drawY);

                if (rowIndex < 0 || rowIndex >= rows.Count || _font == null)
                {
                    continue;
                }

                StoreBankOwnerRowSnapshot row = rows[rowIndex];
                DrawItemIcon(sprite, row, drawX, drawY);

                string primary = TrimToWidth(row.PrimaryText, RowWidth - RowPrimaryTextX - 8f, 0.62f);
                InventoryRenderUtil.DrawOutlinedText(
                    sprite,
                    _font,
                    primary,
                    new Vector2(drawX + RowPrimaryTextX, drawY + RowPrimaryTextY),
                    Color.White,
                    0.62f);

                string secondary = TrimToWidth(row.SecondaryText, RowWidth - RowSecondaryTextX - 40f, 0.52f);
                if (!string.IsNullOrWhiteSpace(secondary))
                {
                    InventoryRenderUtil.DrawOutlinedText(
                        sprite,
                        _font,
                        secondary,
                        new Vector2(drawX + RowSecondaryTextX, drawY + RowSecondaryTextY),
                        new Color(255, 235, 194),
                        0.52f);
                }

                DrawClientStock(sprite, row, drawX, drawY);
                DrawCashIcon(sprite, row, drawX, drawY);
            }
        }

        private void DrawItemIcon(SpriteBatch sprite, StoreBankOwnerRowSnapshot row, int rowX, int rowY)
        {
            Texture2D texture = ResolveItemIconTexture(row.ItemId);
            if (texture == null)
            {
                return;
            }

            sprite.Draw(
                texture,
                new Rectangle(rowX + RowIconX, rowY + RowIconY, RowIconSize, RowIconSize),
                Color.White);
        }

        private Texture2D ResolveItemIconTexture(int itemId)
        {
            if (itemId <= 0 || _itemIconProvider == null)
            {
                return null;
            }

            if (_itemIconTextureCache.TryGetValue(itemId, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D texture = _itemIconProvider(itemId);
            _itemIconTextureCache[itemId] = texture;
            return texture;
        }

        private void DrawClientStock(SpriteBatch sprite, StoreBankOwnerRowSnapshot row, int rowX, int rowY)
        {
            if (!row.ShowsClientStock || row.ClientStock <= 0 || _itemNumberDigits.Length < 10)
            {
                return;
            }

            string text = row.ClientStock.ToString(CultureInfo.InvariantCulture);
            int x = rowX + RowClientStockRightX - MeasureDigitWidth(text);
            int y = rowY + RowSecondaryTextY;
            for (int i = 0; i < text.Length; i++)
            {
                int digit = text[i] - '0';
                if (digit < 0 || digit > 9)
                {
                    continue;
                }

                Texture2D texture = _itemNumberDigits[digit];
                if (texture == null)
                {
                    continue;
                }

                sprite.Draw(texture, new Vector2(x, y), Color.White);
                x += texture.Width;
            }
        }

        private int MeasureDigitWidth(string text)
        {
            if (string.IsNullOrEmpty(text) || _itemNumberDigits.Length < 10)
            {
                return 0;
            }

            int width = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int digit = text[i] - '0';
                if (digit < 0 || digit > 9)
                {
                    continue;
                }

                width += _itemNumberDigits[digit]?.Width ?? 0;
            }

            return width;
        }

        private void DrawCashIcon(SpriteBatch sprite, StoreBankOwnerRowSnapshot row, int rowX, int rowY)
        {
            if (!row.IsCashItem || _cashIconTexture == null)
            {
                return;
            }

            sprite.Draw(
                _cashIconTexture,
                new Vector2(rowX + RowCashIconRightX - _cashIconTexture.Width, rowY + RowSecondaryTextY),
                Color.White);
        }

        private void DrawMoney(SpriteBatch sprite)
        {
            if (_font == null)
            {
                return;
            }

            const float moneyScale = 1.0f;
            string moneyText = FormatClientMoneyText(_runtime?.OwnerMoney ?? 0);
            Vector2 size = ClientTextDrawing.Measure(
                sprite.GraphicsDevice,
                moneyText,
                moneyScale,
                _font);
            Vector2 position = new(
                Position.X + MoneyRightX - size.X,
                Position.Y + MoneyY);
            ClientTextDrawing.Draw(
                sprite,
                moneyText,
                position + Vector2.One,
                MoneyShadowColor,
                moneyScale,
                _font);
            ClientTextDrawing.Draw(
                sprite,
                moneyText,
                position,
                MoneyTextColor,
                moneyScale,
                _font);
        }

        private static string FormatClientMoneyText(int money)
        {
            return Math.Max(0, money).ToString("N0", CultureInfo.InvariantCulture);
        }

        private void DrawRowSeparator(SpriteBatch sprite, int rowX, int rowY)
        {
            if (_rowLineTexture == null)
            {
                return;
            }

            sprite.Draw(_rowLineTexture, new Vector2(rowX, rowY + RowHeight), Color.White);
        }

        private void DrawFooter(SpriteBatch sprite)
        {
            if (_font == null)
            {
                return;
            }

            IReadOnlyList<StoreBankOwnerRowSnapshot> rows = GetRows();
            string footer = _footerProvider?.Invoke();
            if (_runtime != null
                && !_runtime.HasPendingGetAllRequest
                && !_runtime.HasPendingFeeCalculationRequest
                && _selectedRowIndex >= 0
                && _selectedRowIndex < rows.Count)
            {
                footer = rows[_selectedRowIndex].SelectionSummary;
            }

            if (string.IsNullOrWhiteSpace(footer))
            {
                footer = rows.Count == 0
                    ? "No packet-authored store-bank rows are staged."
                    : _selectedRowIndex >= 0 && _selectedRowIndex < rows.Count
                        ? rows[_selectedRowIndex].SelectionSummary
                        : "Select a packet-authored store-bank row to arm BtGet.";
            }

            float y = Position.Y + FooterY;
            foreach (string line in WrapFooterText(footer))
            {
                InventoryRenderUtil.DrawOutlinedText(
                    sprite,
                    _font,
                    line,
                    new Vector2(Position.X + FooterX, y),
                    new Color(255, 228, 151),
                    0.55f);
                y += 12f;
            }
        }

        private void DrawScrollBar(SpriteBatch sprite)
        {
            if (_scrollbarSkin?.IsReady != true)
            {
                return;
            }

            Rectangle baseBounds = GetScrollBarBounds();
            Rectangle prevBounds = GetScrollPrevBounds();
            Rectangle nextBounds = GetScrollNextBounds();
            Rectangle thumbBounds = GetScrollThumbBounds();
            bool canScroll = GetMaxScrollOffset() > 0;
            bool hoverPrev = prevBounds.Contains(_lastMousePosition);
            bool hoverNext = nextBounds.Contains(_lastMousePosition);
            bool hoverThumb = thumbBounds.Contains(_lastMousePosition);

            DrawTexture(sprite, _scrollbarSkin.Base, baseBounds.Location);
            DrawTexture(sprite, ResolvePrevTexture(canScroll, hoverPrev), prevBounds.Location);
            DrawTexture(sprite, ResolveNextTexture(canScroll, hoverNext), nextBounds.Location);
            DrawTexture(sprite, ResolveThumbTexture(canScroll, hoverThumb), thumbBounds.Location);
        }

        private void SelectVisibleRow(int visibleRowIndex)
        {
            SelectAbsoluteRow(_scrollOffset + visibleRowIndex);
        }

        private void SelectAbsoluteRow(int rowIndex)
        {
            IReadOnlyList<StoreBankOwnerRowSnapshot> rows = GetRows();
            if (rowIndex < 0 || rowIndex >= rows.Count)
            {
                return;
            }

            _selectedRowIndex = rowIndex;
            _selectionAnchor = BuildSelectionAnchor(rows[rowIndex]);
            EnsureSelectedRowVisible();
            UpdateInteractiveState();
        }

        private void MoveSelection(int delta)
        {
            IReadOnlyList<StoreBankOwnerRowSnapshot> rows = GetRows();
            if (rows.Count == 0)
            {
                _selectedRowIndex = -1;
                return;
            }

            int nextIndex = _selectedRowIndex < 0
                ? (delta >= 0 ? 0 : rows.Count - 1)
                : Math.Clamp(_selectedRowIndex + delta, 0, rows.Count - 1);
            SelectAbsoluteRow(nextIndex);
        }

        private void EnsureSelectedRowVisible()
        {
            if (_selectedRowIndex < _scrollOffset)
            {
                _scrollOffset = _selectedRowIndex;
            }
            else if (_selectedRowIndex >= _scrollOffset + VisibleRowCount)
            {
                _scrollOffset = _selectedRowIndex - VisibleRowCount + 1;
            }
        }

        private void ClampState()
        {
            IReadOnlyList<StoreBankOwnerRowSnapshot> rows = GetRows();
            _selectedRowIndex = rows.Count == 0 ? -1 : Math.Clamp(_selectedRowIndex, -1, rows.Count - 1);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, GetMaxScrollOffset());
            if (_selectedRowIndex >= 0 && _selectedRowIndex < rows.Count)
            {
                _selectionAnchor = BuildSelectionAnchor(rows[_selectedRowIndex]);
            }
            else if (rows.Count == 0)
            {
                _selectionAnchor = null;
            }

            if (_selectedRowIndex >= 0)
            {
                EnsureSelectedRowVisible();
            }
        }

        private void UpdateInteractiveState()
        {
            IReadOnlyList<StoreBankOwnerRowSnapshot> rows = GetRows();
            bool rowSelectionLocked = _runtime?.HasPendingFeeCalculationRequest == true
                || _runtime?.HasPendingGetAllRequest == true
                || _runtime?.HasAcceptedGetAllRequestInFlight == true;
            bool canSelect = IsVisible && rows.Count > 0 && !rowSelectionLocked;
            for (int i = 0; i < _rowButtons.Count; i++)
            {
                bool visible = _scrollOffset + i < rows.Count;
                _rowButtons[i].SetVisible(visible);
                _rowButtons[i].SetEnabled(visible && canSelect);
            }

            _getButton?.SetEnabled(IsGetButtonEnabled());
        }

        private bool IsGetButtonEnabled()
        {
            if (!IsVisible || _runtime == null || !_runtime.IsOwnerGetButtonEnabled)
            {
                return false;
            }

            return _runtime.HasPendingGetAllRequest || _runtime.HasDecodedItems;
        }

        private IReadOnlyList<StoreBankOwnerRowSnapshot> GetRows()
        {
            return _runtime?.BuildOwnerRows() ?? Array.Empty<StoreBankOwnerRowSnapshot>();
        }

        private Rectangle GetListBounds()
        {
            return new Rectangle(Position.X + RowX, Position.Y + RowY, RowWidth, VisibleRowCount * RowPitch);
        }

        private Rectangle GetScrollBarBounds()
        {
            return new Rectangle(Position.X + ScrollBarX, Position.Y + ScrollBarY, _scrollbarSkin?.Width ?? 11, ScrollBarHeight);
        }

        private Rectangle GetScrollPrevBounds()
        {
            Rectangle bounds = GetScrollBarBounds();
            return new Rectangle(bounds.X, bounds.Y, _scrollbarSkin?.Width ?? 11, _scrollbarSkin?.PrevHeight ?? 12);
        }

        private Rectangle GetScrollNextBounds()
        {
            Rectangle bounds = GetScrollBarBounds();
            int nextHeight = _scrollbarSkin?.NextHeight ?? 12;
            return new Rectangle(bounds.X, bounds.Bottom - nextHeight, _scrollbarSkin?.Width ?? 11, nextHeight);
        }

        private Rectangle GetScrollTrackBounds()
        {
            Rectangle bounds = GetScrollBarBounds();
            Rectangle prev = GetScrollPrevBounds();
            Rectangle next = GetScrollNextBounds();
            return new Rectangle(bounds.X, prev.Bottom, bounds.Width, Math.Max(0, next.Y - prev.Bottom));
        }

        private Rectangle GetScrollThumbBounds()
        {
            Rectangle trackBounds = GetScrollTrackBounds();
            int thumbHeight = _scrollbarSkin?.ThumbHeight ?? 26;
            int maxScroll = GetMaxScrollOffset();
            if (maxScroll <= 0)
            {
                return new Rectangle(trackBounds.X, trackBounds.Y, _scrollbarSkin?.Width ?? 11, thumbHeight);
            }

            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int thumbY = trackBounds.Y + (int)Math.Round((_scrollOffset / (float)maxScroll) * travel);
            return new Rectangle(trackBounds.X, thumbY, _scrollbarSkin?.Width ?? 11, thumbHeight);
        }

        private bool TryHandleScrollBarMouseDown(MouseState mouseState)
        {
            if (!GetScrollBarBounds().Contains(mouseState.Position) || GetMaxScrollOffset() <= 0)
            {
                return false;
            }

            if (GetScrollPrevBounds().Contains(mouseState.Position))
            {
                AdjustScrollOffset(-1);
                return true;
            }

            if (GetScrollNextBounds().Contains(mouseState.Position))
            {
                AdjustScrollOffset(1);
                return true;
            }

            Rectangle thumbBounds = GetScrollThumbBounds();
            if (thumbBounds.Contains(mouseState.Position))
            {
                _draggingScrollThumb = true;
                _scrollThumbDragOffsetY = mouseState.Y - thumbBounds.Y;
                return true;
            }

            Rectangle trackBounds = GetScrollTrackBounds();
            if (trackBounds.Contains(mouseState.Position))
            {
                AdjustScrollOffset(mouseState.Y < thumbBounds.Y ? -VisibleRowCount : VisibleRowCount);
                return true;
            }

            return false;
        }

        private void UpdateScrollThumbDrag(MouseState mouseState)
        {
            if (!_draggingScrollThumb)
            {
                return;
            }

            if (mouseState.LeftButton != ButtonState.Pressed)
            {
                _draggingScrollThumb = false;
                return;
            }

            Rectangle trackBounds = GetScrollTrackBounds();
            Rectangle thumbBounds = GetScrollThumbBounds();
            int travel = Math.Max(0, trackBounds.Height - thumbBounds.Height);
            int maxScroll = GetMaxScrollOffset();
            if (travel <= 0 || maxScroll <= 0)
            {
                _scrollOffset = 0;
                return;
            }

            int thumbTop = Math.Clamp(mouseState.Y - _scrollThumbDragOffsetY, trackBounds.Y, trackBounds.Bottom - thumbBounds.Height);
            float ratio = (thumbTop - trackBounds.Y) / (float)travel;
            _scrollOffset = (int)Math.Round(ratio * maxScroll);
            ClampState();
        }

        private void AdjustScrollOffset(int delta)
        {
            _scrollOffset = Math.Clamp(_scrollOffset + delta, 0, GetMaxScrollOffset());
            ClampState();
            UpdateInteractiveState();
        }

        private int GetMaxScrollOffset()
        {
            return Math.Max(0, GetRows().Count - VisibleRowCount);
        }

        private void SyncOwnerRowRevision()
        {
            if (_runtime == null)
            {
                _lastOwnerRowRevision = -1;
                return;
            }

            if (_lastOwnerRowRevision == _runtime.OwnerRowRevision)
            {
                return;
            }

            IReadOnlyList<StoreBankOwnerRowSnapshot> rows = GetRows();
            if (_selectionAnchor.HasValue)
            {
                _selectedRowIndex = PacketOwnedStoreBankDialogRuntime.ResolveOwnerRowIndex(rows, _selectionAnchor.Value);
            }
            else if (rows.Count == 0)
            {
                _selectedRowIndex = -1;
            }

            _lastOwnerRowRevision = _runtime.OwnerRowRevision;
            ClampState();
            UpdateInteractiveState();
        }

        private static StoreBankOwnerSelectionAnchor BuildSelectionAnchor(StoreBankOwnerRowSnapshot row)
        {
            return new StoreBankOwnerSelectionAnchor(
                row.PacketGroupInventoryType,
                row.PacketGroupRowIndex,
                row.ItemId,
                row.ItemSerialNumber,
                row.CashSerialNumber,
                row.Title ?? string.Empty);
        }

        private Texture2D ResolvePrevTexture(bool canScroll, bool hover)
        {
            if (!canScroll)
            {
                return _scrollbarSkin?.PrevDisabled ?? _scrollbarSkin?.PrevStates?.FirstOrDefault();
            }

            Texture2D[] states = _scrollbarSkin?.PrevStates ?? Array.Empty<Texture2D>();
            return hover ? states.ElementAtOrDefault(1) ?? states.FirstOrDefault() : states.FirstOrDefault();
        }

        private Texture2D ResolveNextTexture(bool canScroll, bool hover)
        {
            if (!canScroll)
            {
                return _scrollbarSkin?.NextDisabled ?? _scrollbarSkin?.NextStates?.FirstOrDefault();
            }

            Texture2D[] states = _scrollbarSkin?.NextStates ?? Array.Empty<Texture2D>();
            return hover ? states.ElementAtOrDefault(1) ?? states.FirstOrDefault() : states.FirstOrDefault();
        }

        private Texture2D ResolveThumbTexture(bool canScroll, bool hover)
        {
            Texture2D[] states = _scrollbarSkin?.ThumbStates ?? Array.Empty<Texture2D>();
            if (!canScroll)
            {
                return states.ElementAtOrDefault(2) ?? states.FirstOrDefault();
            }

            if (_draggingScrollThumb)
            {
                return states.ElementAtOrDefault(2) ?? states.ElementAtOrDefault(1) ?? states.FirstOrDefault();
            }

            return hover ? states.ElementAtOrDefault(1) ?? states.FirstOrDefault() : states.FirstOrDefault();
        }

        private static void DrawTexture(SpriteBatch sprite, Texture2D texture, Point position)
        {
            if (texture != null)
            {
                sprite.Draw(texture, position.ToVector2(), Color.White);
            }
        }

        private IEnumerable<string> WrapFooterText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _font == null)
            {
                yield break;
            }

            string remaining = text.Trim();
            while (remaining.Length > 0)
            {
                int length = remaining.Length;
                while (length > 1 && (_font.MeasureString(remaining[..length]) * 0.55f).X > FooterWidth)
                {
                    int whitespaceIndex = remaining.LastIndexOf(' ', length - 1, length);
                    length = whitespaceIndex > 0 ? whitespaceIndex : length - 1;
                }

                yield return remaining[..Math.Max(1, length)].Trim();
                remaining = remaining[Math.Max(1, length)..].TrimStart();
            }
        }

        private string TrimToWidth(string text, float maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            if ((_font.MeasureString(text) * scale).X <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            for (int length = text.Length - 1; length > 0; length--)
            {
                string candidate = text[..length] + ellipsis;
                if ((_font.MeasureString(candidate) * scale).X <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private bool WasPressed(KeyboardState currentState, Keys key)
        {
            return currentState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private static UIObject CreateTransparentButton(GraphicsDevice device, int width, int height)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] pixels = new Color[width * height];
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
    }
}
