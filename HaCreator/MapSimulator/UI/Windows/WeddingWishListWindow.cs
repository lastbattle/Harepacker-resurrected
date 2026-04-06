using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class WeddingWishListWindow : UIWindowBase
    {
        private const int DefaultWidth = 423;
        private const int DefaultHeight = 290;
        private const int RowIconSize = 24;
        private const int TooltipPadding = 8;
        private const int TooltipOffsetX = 14;
        private const int TooltipOffsetY = 8;
        private const float ItemTextScale = 0.66f;
        private const float TooltipTitleScale = 0.78f;
        private const float TooltipBodyScale = 0.66f;

        private readonly GraphicsDevice _device;
        private readonly Texture2D _pixel;
        private readonly WeddingWishListWindowAssets _assets;

        private SpriteFont _font;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private Func<WeddingWishListSnapshot> _snapshotProvider;
        private Func<WeddingWishListSelectionPane, string> _focusPaneHandler;
        private Func<int, string> _setTabHandler;
        private Func<WeddingWishListSelectionPane, int, string> _selectEntryHandler;
        private Func<WeddingWishListSelectionPane, int, string> _scrollPaneHandler;
        private Func<char, string> _appendCandidateQueryHandler;
        private Func<string> _backspaceCandidateQueryHandler;
        private Func<string> _getSelectedHandler;
        private Func<string> _putSelectedHandler;
        private Func<string> _enterSelectedHandler;
        private Func<string> _deleteSelectedHandler;
        private Func<string> _confirmHandler;
        private Func<string> _closeHandler;
        private Action<string> _feedbackHandler;
        private WeddingWishListSnapshot _snapshot = new();
        private WeddingWishListSelectionPane? _hoveredPane;
        private int _hoveredIndex = -1;
        private Point _lastMousePosition;

        private UIObject _getButton;
        private UIObject _putButton;
        private UIObject _enterButton;
        private UIObject _deleteButton;
        private UIObject _confirmButton;
        private UIObject _closeButton;

        internal WeddingWishListWindow(WeddingWishListWindowAssets assets, GraphicsDevice device)
            : base(new DXObject(0, 0, CreateFilledTexture(device, DefaultWidth, DefaultHeight, Color.Transparent), 0))
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _pixel = CreateFilledTexture(device, 1, 1, Color.White);
            RefreshLayout();
        }

        public override string WindowName => MapSimulatorWindowNames.WeddingWishList;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;

        internal void SetSnapshotProvider(Func<WeddingWishListSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _snapshot = RefreshSnapshot();
            RefreshLayout();
        }

        internal void InitializeControls(
            UIObject getButton,
            UIObject putButton,
            UIObject enterButton,
            UIObject deleteButton,
            UIObject confirmButton,
            UIObject closeButton)
        {
            _getButton = getButton;
            _putButton = putButton;
            _enterButton = enterButton;
            _deleteButton = deleteButton;
            _confirmButton = confirmButton;
            _closeButton = closeButton;

            AddControl(_getButton, () => _getSelectedHandler?.Invoke());
            AddControl(_putButton, () => _putSelectedHandler?.Invoke());
            AddControl(_enterButton, () => _enterSelectedHandler?.Invoke());
            AddControl(_deleteButton, () => _deleteSelectedHandler?.Invoke());
            AddControl(_confirmButton, () => _confirmHandler?.Invoke());
            AddControl(_closeButton, () => _closeHandler?.Invoke());

            RefreshButtonLayout();
        }

        internal void SetActionHandlers(
            Func<WeddingWishListSelectionPane, string> focusPaneHandler,
            Func<int, string> setTabHandler,
            Func<WeddingWishListSelectionPane, int, string> selectEntryHandler,
            Func<WeddingWishListSelectionPane, int, string> scrollPaneHandler,
            Func<char, string> appendCandidateQueryHandler,
            Func<string> backspaceCandidateQueryHandler,
            Func<string> getSelectedHandler,
            Func<string> putSelectedHandler,
            Func<string> enterSelectedHandler,
            Func<string> deleteSelectedHandler,
            Func<string> confirmHandler,
            Func<string> closeHandler,
            Action<string> feedbackHandler)
        {
            _focusPaneHandler = focusPaneHandler;
            _setTabHandler = setTabHandler;
            _selectEntryHandler = selectEntryHandler;
            _scrollPaneHandler = scrollPaneHandler;
            _appendCandidateQueryHandler = appendCandidateQueryHandler;
            _backspaceCandidateQueryHandler = backspaceCandidateQueryHandler;
            _getSelectedHandler = getSelectedHandler;
            _putSelectedHandler = putSelectedHandler;
            _enterSelectedHandler = enterSelectedHandler;
            _deleteSelectedHandler = deleteSelectedHandler;
            _confirmHandler = confirmHandler;
            _closeHandler = closeHandler;
            _feedbackHandler = feedbackHandler;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _snapshot = RefreshSnapshot();
            RefreshLayout();

            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            _lastMousePosition = mouseState.Position;

            if (IsVisible && _snapshot.IsOpen)
            {
                UpdateHover(mouseState);
                HandleKeyboard(keyboardState);
                HandleMouse(mouseState);
            }
            else
            {
                _hoveredPane = null;
                _hoveredIndex = -1;
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
            WeddingWishListModeAssets modeAssets = GetModeAssets();
            WeddingWishListRoleAssets roleAssets = modeAssets?.GetRoleAssets(_snapshot.Role);
            if (roleAssets == null)
            {
                return;
            }

            DrawLayer(sprite, roleAssets.Background, Point.Zero);
            DrawLayer(sprite, roleAssets.Foreground, roleAssets.ForegroundOffset);
            DrawLayer(sprite, roleAssets.HeaderOverlay, roleAssets.HeaderOverlayOffset);

            DrawTabs(sprite);
            DrawRows(sprite);
            DrawStatus(sprite);
            DrawTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight))
            {
                return true;
            }

            if (!IsVisible || !_snapshot.IsOpen)
            {
                return false;
            }

            if (GetTabBounds().Any(bounds => bounds.Contains(mouseState.Position)) || GetInteractiveRowBounds().Any(bounds => bounds.Contains(mouseState.Position)))
            {
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            return false;
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            foreach (Rectangle bounds in GetTabBounds())
            {
                yield return bounds;
            }

            foreach (Rectangle bounds in GetInteractiveRowBounds())
            {
                yield return bounds;
            }
        }

        private void AddControl(UIObject button, Func<string> handler)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => ShowFeedback(handler?.Invoke());
        }

        private void HandleKeyboard(KeyboardState keyboardState)
        {
            if (Pressed(keyboardState, Keys.Escape))
            {
                ShowFeedback(_closeHandler?.Invoke());
                return;
            }

            if (_snapshot.Mode == WeddingWishListDialogMode.Input && _snapshot.ActivePane == WeddingWishListSelectionPane.Candidate)
            {
                if (Pressed(keyboardState, Keys.Back))
                {
                    ShowFeedback(_backspaceCandidateQueryHandler?.Invoke());
                }

                foreach (char character in EnumerateTypedCharacters(keyboardState))
                {
                    ShowFeedback(_appendCandidateQueryHandler?.Invoke(character));
                }
            }

            if (_snapshot.Mode == WeddingWishListDialogMode.Receive || _snapshot.Mode == WeddingWishListDialogMode.Give)
            {
                if (Pressed(keyboardState, Keys.D1)) ShowFeedback(_setTabHandler?.Invoke(0));
                if (Pressed(keyboardState, Keys.D2)) ShowFeedback(_setTabHandler?.Invoke(1));
                if (Pressed(keyboardState, Keys.D3)) ShowFeedback(_setTabHandler?.Invoke(2));
                if (Pressed(keyboardState, Keys.D4)) ShowFeedback(_setTabHandler?.Invoke(3));
                if (Pressed(keyboardState, Keys.D5)) ShowFeedback(_setTabHandler?.Invoke(4));
            }

            if (Pressed(keyboardState, Keys.Tab))
            {
                CyclePane(keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift) ? -1 : 1);
            }

            if (Pressed(keyboardState, Keys.Up))
            {
                MoveSelection(-1);
            }

            if (Pressed(keyboardState, Keys.Down))
            {
                MoveSelection(1);
            }

            if (Pressed(keyboardState, Keys.PageUp))
            {
                PageSelection(-1);
            }

            if (Pressed(keyboardState, Keys.PageDown))
            {
                PageSelection(1);
            }

            if (Pressed(keyboardState, Keys.Home))
            {
                JumpSelectionToBoundary(0);
            }

            if (Pressed(keyboardState, Keys.End))
            {
                JumpSelectionToBoundary(-1);
            }

            if (Pressed(keyboardState, Keys.Enter))
            {
                switch (_snapshot.Mode)
                {
                    case WeddingWishListDialogMode.Receive:
                        ShowFeedback(_snapshot.ActivePane == WeddingWishListSelectionPane.GiftList
                            ? _getSelectedHandler?.Invoke()
                            : _focusPaneHandler?.Invoke(WeddingWishListSelectionPane.GiftList));
                        break;

                    case WeddingWishListDialogMode.Give:
                        if (_snapshot.ActivePane == WeddingWishListSelectionPane.Inventory)
                        {
                            ShowFeedback(_putSelectedHandler?.Invoke());
                        }
                        else
                        {
                            ShowFeedback(_focusPaneHandler?.Invoke(WeddingWishListSelectionPane.Inventory));
                        }

                        break;

                    case WeddingWishListDialogMode.Input:
                        ShowFeedback(_snapshot.ActivePane == WeddingWishListSelectionPane.WishList
                            ? _confirmHandler?.Invoke()
                            : _enterSelectedHandler?.Invoke());
                        break;
                }
            }

            if (_snapshot.Mode == WeddingWishListDialogMode.Input && Pressed(keyboardState, Keys.Delete))
            {
                ShowFeedback(_deleteSelectedHandler?.Invoke());
            }
        }

        private void HandleMouse(MouseState mouseState)
        {
            int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                WeddingWishListSelectionPane pane = ResolveScrollTargetPane(mouseState.Position);
                int direction = scrollDelta > 0 ? -1 : 1;
                ShowFeedback(_scrollPaneHandler?.Invoke(pane, direction));
            }

            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            if (!leftPressed)
            {
                return;
            }

            IReadOnlyList<Rectangle> tabBounds = GetTabBounds();
            for (int i = 0; i < tabBounds.Count; i++)
            {
                if (tabBounds[i].Contains(mouseState.Position))
                {
                    ShowFeedback(_setTabHandler?.Invoke(i));
                    return;
                }
            }

            foreach ((WeddingWishListSelectionPane pane, int index, Rectangle bounds) in EnumerateRowTargets())
            {
                if (!bounds.Contains(mouseState.Position))
                {
                    continue;
                }

                ShowFeedback(_focusPaneHandler?.Invoke(pane));
                ShowFeedback(_selectEntryHandler?.Invoke(pane, index));
                return;
            }
        }

        private void UpdateHover(MouseState mouseState)
        {
            _hoveredPane = null;
            _hoveredIndex = -1;

            foreach ((WeddingWishListSelectionPane pane, int index, Rectangle bounds) in EnumerateRowTargets())
            {
                if (!bounds.Contains(mouseState.Position))
                {
                    continue;
                }

                _hoveredPane = pane;
                _hoveredIndex = index;
                return;
            }
        }

        private void CyclePane(int delta)
        {
            WeddingWishListSelectionPane[] panes = GetAvailablePanes().ToArray();
            if (panes.Length == 0)
            {
                return;
            }

            int currentIndex = Array.IndexOf(panes, _snapshot.ActivePane);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = (currentIndex + delta + panes.Length) % panes.Length;
            ShowFeedback(_focusPaneHandler?.Invoke(panes[nextIndex]));
        }

        private IEnumerable<WeddingWishListSelectionPane> GetAvailablePanes()
        {
            switch (_snapshot.Mode)
            {
                case WeddingWishListDialogMode.Receive:
                    yield return WeddingWishListSelectionPane.GiftList;
                    yield return WeddingWishListSelectionPane.Inventory;
                    break;

                case WeddingWishListDialogMode.Give:
                    yield return WeddingWishListSelectionPane.WishList;
                    yield return WeddingWishListSelectionPane.GiftList;
                    yield return WeddingWishListSelectionPane.Inventory;
                    break;

                case WeddingWishListDialogMode.Input:
                    yield return WeddingWishListSelectionPane.Candidate;
                    yield return WeddingWishListSelectionPane.WishList;
                    break;
            }
        }

        private void MoveSelection(int delta)
        {
            ShowFeedback(_selectEntryHandler?.Invoke(_snapshot.ActivePane, GetSelectedIndex(_snapshot.ActivePane) + delta));
        }

        private void PageSelection(int direction)
        {
            WeddingWishListSelectionPane pane = _snapshot.ActivePane;
            int visibleRows = GetVisibleRowCount(pane);
            if (visibleRows <= 0)
            {
                return;
            }

            ShowFeedback(_scrollPaneHandler?.Invoke(pane, direction * visibleRows));
        }

        private void JumpSelectionToBoundary(int targetIndex)
        {
            WeddingWishListSelectionPane pane = _snapshot.ActivePane;
            IReadOnlyList<InventorySlotData> entries = GetEntries(pane);
            if (entries.Count == 0)
            {
                return;
            }

            int resolvedTarget = targetIndex < 0 ? entries.Count - 1 : targetIndex;
            ShowFeedback(_selectEntryHandler?.Invoke(pane, resolvedTarget));
        }

        private void DrawTabs(SpriteBatch sprite)
        {
            if (_snapshot.Mode == WeddingWishListDialogMode.Input)
            {
                return;
            }

            IReadOnlyList<Rectangle> tabBounds = GetTabBounds();
            for (int i = 0; i < Math.Min(_snapshot.TabLabels.Count, tabBounds.Count); i++)
            {
                Texture2D texture = i == _snapshot.SelectedTabIndex
                    ? _assets.TabEnabled.ElementAtOrDefault(i)
                    : _assets.TabDisabled.ElementAtOrDefault(i) ?? _assets.TabEnabled.ElementAtOrDefault(i);
                if (texture == null)
                {
                    continue;
                }

                Rectangle bounds = tabBounds[i];
                sprite.Draw(texture, new Rectangle(bounds.X, bounds.Y, texture.Width, texture.Height), Color.White);
            }
        }

        private void DrawRows(SpriteBatch sprite)
        {
            switch (_snapshot.Mode)
            {
                case WeddingWishListDialogMode.Receive:
                    DrawPane(sprite, WeddingWishListSelectionPane.GiftList, _snapshot.GiftEntries, 5, GetReceiveGiftArea());
                    DrawPane(sprite, WeddingWishListSelectionPane.Inventory, _snapshot.InventoryEntries, 5, GetReceiveInventoryArea());
                    break;

                case WeddingWishListDialogMode.Give:
                    DrawPane(sprite, WeddingWishListSelectionPane.WishList, _snapshot.WishEntries, 3, GetGiveWishArea());
                    DrawPane(sprite, WeddingWishListSelectionPane.GiftList, _snapshot.GiftEntries, 2, GetGiveGiftArea());
                    DrawPane(sprite, WeddingWishListSelectionPane.Inventory, _snapshot.InventoryEntries, 3, GetGiveInventoryArea());
                    break;

                case WeddingWishListDialogMode.Input:
                    DrawCandidateField(sprite);
                    DrawPane(sprite, WeddingWishListSelectionPane.WishList, _snapshot.WishEntries, 8, GetInputWishArea());
                    break;
            }
        }

        private void DrawCandidateField(SpriteBatch sprite)
        {
            Rectangle fieldBounds = GetInputCandidateFieldBounds();
            InventorySlotData candidate = GetSelectedItem(WeddingWishListSelectionPane.Candidate);
            string query = _snapshot.CandidateQuery?.Trim() ?? string.Empty;
            bool selected = _snapshot.ActivePane == WeddingWishListSelectionPane.Candidate;
            sprite.Draw(_pixel, fieldBounds, selected ? new Color(255, 247, 205) : new Color(255, 255, 255));
            sprite.Draw(_pixel, new Rectangle(fieldBounds.X, fieldBounds.Y, fieldBounds.Width, 1), new Color(91, 91, 91));
            sprite.Draw(_pixel, new Rectangle(fieldBounds.X, fieldBounds.Bottom - 1, fieldBounds.Width, 1), new Color(178, 178, 178));

            if (_font == null)
            {
                return;
            }

            Rectangle textBounds = new Rectangle(fieldBounds.X + 4, fieldBounds.Y + 1, fieldBounds.Width - 8, fieldBounds.Height - 2);
            if (!string.IsNullOrWhiteSpace(query))
            {
                string visibleQuery = TrimToWidth(query, Math.Max(24f, textBounds.Width), ItemTextScale);
                InventoryRenderUtil.DrawOutlinedText(sprite, _font, visibleQuery, new Vector2(textBounds.X, textBounds.Y), new Color(92, 56, 11), ItemTextScale);
                return;
            }

            if (candidate != null)
            {
                DrawItemLabel(sprite, candidate, textBounds, selected);
            }
        }

        private void DrawPane(SpriteBatch sprite, WeddingWishListSelectionPane pane, IReadOnlyList<InventorySlotData> entries, int visibleRowCount, Rectangle area)
        {
            if (_font == null || visibleRowCount <= 0)
            {
                return;
            }

            int selectedIndex = GetSelectedIndex(pane);
            int startIndex = Math.Clamp(GetFirstVisibleIndex(pane), 0, Math.Max(0, entries.Count - visibleRowCount));
            int rowHeight = Math.Max(1, area.Height / visibleRowCount);
            Texture2D selectionTexture = GetModeAssets()?.Selection;

            for (int row = 0; row < visibleRowCount; row++)
            {
                int entryIndex = startIndex + row;
                Rectangle rowBounds = new Rectangle(area.X, area.Y + (row * rowHeight), area.Width, rowHeight);
                bool isSelected = entryIndex == selectedIndex && pane == _snapshot.ActivePane;
                bool isHovered = entryIndex == _hoveredIndex && pane == _hoveredPane;

                if (entryIndex >= entries.Count)
                {
                    if (isSelected)
                    {
                        DrawFallbackSelection(sprite, rowBounds);
                    }

                    continue;
                }

                if (selectionTexture != null && (isSelected || isHovered))
                {
                    int selectionX = rowBounds.X + Math.Max(28, rowBounds.Width - selectionTexture.Width - 10);
                    int selectionY = rowBounds.Y + Math.Max(0, (rowBounds.Height - selectionTexture.Height) / 2);
                    sprite.Draw(selectionTexture, new Rectangle(selectionX, selectionY, selectionTexture.Width, selectionTexture.Height), Color.White);
                }
                else if (isSelected)
                {
                    DrawFallbackSelection(sprite, rowBounds);
                }

                DrawItemRow(sprite, entries[entryIndex], rowBounds, pane == _snapshot.ActivePane);
            }
        }

        private void DrawItemRow(SpriteBatch sprite, InventorySlotData item, Rectangle rowBounds, bool paneSelected)
        {
            if (item == null)
            {
                return;
            }

            Rectangle iconBounds = new Rectangle(rowBounds.X + 2, rowBounds.Y + Math.Max(0, (rowBounds.Height - RowIconSize) / 2), RowIconSize, RowIconSize);
            if (item.ItemTexture != null)
            {
                sprite.Draw(item.ItemTexture, iconBounds, Color.White);
            }
            else
            {
                sprite.Draw(_pixel, iconBounds, new Color(224, 224, 224));
            }

            if (_font == null)
            {
                return;
            }

            Rectangle textBounds = new Rectangle(iconBounds.Right + 6, rowBounds.Y + 2, rowBounds.Width - (iconBounds.Width + 12), rowBounds.Height - 4);
            DrawItemLabel(sprite, item, textBounds, paneSelected);
        }

        private void DrawItemLabel(SpriteBatch sprite, InventorySlotData item, Rectangle textBounds, bool emphasized)
        {
            string label = ResolveItemLabel(item);
            if (item.Quantity > 1)
            {
                label = $"{label} x{item.Quantity}";
            }

            label = TrimToWidth(label, Math.Max(24f, textBounds.Width), ItemTextScale);
            Color color = emphasized ? new Color(92, 56, 11) : new Color(64, 64, 64);
            InventoryRenderUtil.DrawOutlinedText(sprite, _font, label, new Vector2(textBounds.X, textBounds.Y), color, ItemTextScale);
        }

        private void DrawStatus(SpriteBatch sprite)
        {
            if (_font == null || string.IsNullOrWhiteSpace(_snapshot.StatusMessage))
            {
                return;
            }

            Rectangle statusBounds = GetStatusBounds();
            string message = TrimToWidth(_snapshot.StatusMessage, statusBounds.Width, ItemTextScale);
            InventoryRenderUtil.DrawOutlinedText(sprite, _font, message, new Vector2(statusBounds.X, statusBounds.Y), new Color(255, 241, 164), ItemTextScale);
        }

        private void DrawTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            InventorySlotData hoveredItem = GetHoveredItem();
            if (_font == null || hoveredItem == null)
            {
                return;
            }

            string title = ResolveItemLabel(hoveredItem);
            string line = $"Item ID: {hoveredItem.ItemId}";
            if (hoveredItem.Quantity > 1)
            {
                line += $"  Quantity: {hoveredItem.Quantity}";
            }

            string description = hoveredItem.Description ?? string.Empty;
            string[] wrappedDescription = WrapText(description, 210f, TooltipBodyScale);

            Vector2 titleSize = ClientTextDrawing.Measure((GraphicsDevice)null, title, TooltipTitleScale, _font);
            Vector2 lineSize = ClientTextDrawing.Measure((GraphicsDevice)null, line, TooltipBodyScale, _font);
            float descriptionWidth = wrappedDescription.Length == 0
                ? 0f
                : wrappedDescription.Max(text => ClientTextDrawing.Measure((GraphicsDevice)null, text, TooltipBodyScale, _font).X);
            int width = (int)Math.Ceiling(Math.Max(titleSize.X, Math.Max(lineSize.X, descriptionWidth))) + (TooltipPadding * 2) + RowIconSize + 10;
            int height = TooltipPadding * 2 + RowIconSize;
            if (!string.IsNullOrWhiteSpace(line))
            {
                height += (int)Math.Ceiling(lineSize.Y) + 4;
            }

            if (wrappedDescription.Length > 0)
            {
                height += (int)Math.Ceiling((_font.LineSpacing * TooltipBodyScale) * wrappedDescription.Length) + 4;
            }

            int x = Math.Min(_lastMousePosition.X + TooltipOffsetX, Math.Max(TooltipPadding, renderWidth - width - TooltipPadding));
            int y = _lastMousePosition.Y - height - TooltipOffsetY;
            if (y < TooltipPadding)
            {
                y = Math.Min(renderHeight - height - TooltipPadding, _lastMousePosition.Y + TooltipOffsetY);
            }

            Rectangle rect = new Rectangle(x, y, width, height);
            sprite.Draw(_pixel, rect, new Color(28, 23, 14, 230));
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(252, 235, 173));
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Color(121, 96, 37));
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Color(252, 235, 173));
            sprite.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Color(121, 96, 37));

            Rectangle iconBounds = new Rectangle(rect.X + TooltipPadding, rect.Y + TooltipPadding, RowIconSize, RowIconSize);
            if (hoveredItem.ItemTexture != null)
            {
                sprite.Draw(hoveredItem.ItemTexture, iconBounds, Color.White);
            }
            else
            {
                sprite.Draw(_pixel, iconBounds, new Color(224, 224, 224));
            }

            int textX = iconBounds.Right + 8;
            int currentY = rect.Y + TooltipPadding;
            InventoryRenderUtil.DrawOutlinedText(sprite, _font, title, new Vector2(textX, currentY), new Color(255, 223, 126), TooltipTitleScale);
            currentY += (int)Math.Ceiling(titleSize.Y) + 2;
            InventoryRenderUtil.DrawOutlinedText(sprite, _font, line, new Vector2(textX, currentY), Color.White, TooltipBodyScale);
            currentY += (int)Math.Ceiling(lineSize.Y) + 4;

            foreach (string wrappedLine in wrappedDescription)
            {
                InventoryRenderUtil.DrawOutlinedText(sprite, _font, wrappedLine, new Vector2(textX, currentY), new Color(220, 220, 220), TooltipBodyScale);
                currentY += (int)Math.Ceiling(_font.LineSpacing * TooltipBodyScale);
            }
        }

        private void DrawLayer(SpriteBatch sprite, Texture2D texture, Point offset)
        {
            if (texture == null)
            {
                return;
            }

            sprite.Draw(texture, new Vector2(Position.X + offset.X, Position.Y + offset.Y), Color.White);
        }

        private void DrawFallbackSelection(SpriteBatch sprite, Rectangle rowBounds)
        {
            sprite.Draw(_pixel, new Rectangle(rowBounds.X + 30, rowBounds.Y + 2, Math.Max(24, rowBounds.Width - 40), Math.Max(10, rowBounds.Height - 4)), new Color(255, 244, 195, 170));
        }

        private IReadOnlyList<Rectangle> GetTabBounds()
        {
            if (_snapshot.Mode == WeddingWishListDialogMode.Input)
            {
                return Array.Empty<Rectangle>();
            }

            List<Rectangle> bounds = new(Math.Min(_assets.TabEnabled.Length, _snapshot.TabLabels.Count));
            for (int i = 0; i < Math.Min(_assets.TabEnabled.Length, _snapshot.TabLabels.Count); i++)
            {
                Texture2D texture = _assets.TabDisabled.ElementAtOrDefault(i) ?? _assets.TabEnabled.ElementAtOrDefault(i);
                if (texture == null)
                {
                    continue;
                }

                Point offset = _assets.TabOffsets.ElementAtOrDefault(i);
                bounds.Add(new Rectangle(Position.X + offset.X, Position.Y + offset.Y, texture.Width, texture.Height));
            }

            return bounds;
        }

        private IEnumerable<Rectangle> GetInteractiveRowBounds()
        {
            foreach ((_, _, Rectangle bounds) in EnumerateRowTargets())
            {
                yield return bounds;
            }
        }

        private IEnumerable<(WeddingWishListSelectionPane Pane, int Index, Rectangle Bounds)> EnumerateRowTargets()
        {
            switch (_snapshot.Mode)
            {
                case WeddingWishListDialogMode.Receive:
                    foreach (var row in EnumeratePaneRows(WeddingWishListSelectionPane.GiftList, _snapshot.GiftEntries, 5, GetReceiveGiftArea())) yield return row;
                    foreach (var row in EnumeratePaneRows(WeddingWishListSelectionPane.Inventory, _snapshot.InventoryEntries, 5, GetReceiveInventoryArea())) yield return row;
                    break;

                case WeddingWishListDialogMode.Give:
                    foreach (var row in EnumeratePaneRows(WeddingWishListSelectionPane.WishList, _snapshot.WishEntries, 3, GetGiveWishArea())) yield return row;
                    foreach (var row in EnumeratePaneRows(WeddingWishListSelectionPane.GiftList, _snapshot.GiftEntries, 2, GetGiveGiftArea())) yield return row;
                    foreach (var row in EnumeratePaneRows(WeddingWishListSelectionPane.Inventory, _snapshot.InventoryEntries, 3, GetGiveInventoryArea())) yield return row;
                    break;

                case WeddingWishListDialogMode.Input:
                    yield return (WeddingWishListSelectionPane.Candidate, _snapshot.SelectedCandidateIndex, GetInputCandidateFieldBounds());
                    foreach (var row in EnumeratePaneRows(WeddingWishListSelectionPane.WishList, _snapshot.WishEntries, 8, GetInputWishArea())) yield return row;
                    break;
            }
        }

        private IEnumerable<(WeddingWishListSelectionPane Pane, int Index, Rectangle Bounds)> EnumeratePaneRows(
            WeddingWishListSelectionPane pane,
            IReadOnlyList<InventorySlotData> entries,
            int visibleRowCount,
            Rectangle area)
        {
            int startIndex = Math.Clamp(GetFirstVisibleIndex(pane), 0, Math.Max(0, entries.Count - visibleRowCount));
            int rowHeight = Math.Max(1, area.Height / Math.Max(1, visibleRowCount));
            int rowCount = Math.Min(entries.Count, visibleRowCount);

            for (int row = 0; row < rowCount; row++)
            {
                yield return (pane, startIndex + row, new Rectangle(area.X, area.Y + (row * rowHeight), area.Width, rowHeight));
            }
        }

        private Rectangle GetReceiveGiftArea() => new(Position.X + 10, Position.Y + 60, 190, 168);
        private Rectangle GetReceiveInventoryArea() => new(Position.X + 213, Position.Y + 60, 181, 168);
        private Rectangle GetGiveWishArea() => new(Position.X + 10, Position.Y + 54, 178, 94);
        private Rectangle GetGiveGiftArea() => new(Position.X + 10, Position.Y + 160, 178, 70);
        private Rectangle GetGiveInventoryArea() => new(Position.X + 213, Position.Y + 84, 181, 110);
        private Rectangle GetInputCandidateFieldBounds() => new(Position.X + 10, Position.Y + 31, 118, 14);
        private Rectangle GetInputWishArea() => new(Position.X + 10, Position.Y + 58, 160, 170);

        private Rectangle GetStatusBounds()
        {
            return _snapshot.Mode switch
            {
                WeddingWishListDialogMode.Input => new Rectangle(Position.X + 10, Position.Y + 242, 160, 14),
                WeddingWishListDialogMode.Give => new Rectangle(Position.X + 12, Position.Y + 248, 348, 14),
                _ => new Rectangle(Position.X + 12, Position.Y + 270, 348, 14)
            };
        }

        private int GetSelectedIndex(WeddingWishListSelectionPane pane)
        {
            return pane switch
            {
                WeddingWishListSelectionPane.GiftList => _snapshot.SelectedGiftIndex,
                WeddingWishListSelectionPane.Inventory => _snapshot.SelectedInventoryIndex,
                WeddingWishListSelectionPane.WishList => _snapshot.SelectedWishIndex,
                WeddingWishListSelectionPane.Candidate => _snapshot.SelectedCandidateIndex,
                _ => 0
            };
        }

        private int GetFirstVisibleIndex(WeddingWishListSelectionPane pane)
        {
            return pane switch
            {
                WeddingWishListSelectionPane.GiftList => _snapshot.FirstVisibleGiftIndex,
                WeddingWishListSelectionPane.Inventory => _snapshot.FirstVisibleInventoryIndex,
                WeddingWishListSelectionPane.WishList => _snapshot.FirstVisibleWishIndex,
                WeddingWishListSelectionPane.Candidate => _snapshot.FirstVisibleCandidateIndex,
                _ => 0
            };
        }

        private int GetVisibleRowCount(WeddingWishListSelectionPane pane)
        {
            return (_snapshot.Mode, pane) switch
            {
                (WeddingWishListDialogMode.Receive, WeddingWishListSelectionPane.GiftList) => 5,
                (WeddingWishListDialogMode.Receive, WeddingWishListSelectionPane.Inventory) => 5,
                (WeddingWishListDialogMode.Give, WeddingWishListSelectionPane.WishList) => 3,
                (WeddingWishListDialogMode.Give, WeddingWishListSelectionPane.GiftList) => 2,
                (WeddingWishListDialogMode.Give, WeddingWishListSelectionPane.Inventory) => 3,
                (WeddingWishListDialogMode.Input, WeddingWishListSelectionPane.WishList) => 8,
                (WeddingWishListDialogMode.Input, WeddingWishListSelectionPane.Candidate) => 1,
                _ => 0
            };
        }

        private InventorySlotData GetHoveredItem() => _hoveredPane.HasValue ? GetItem(_hoveredPane.Value, _hoveredIndex) : null;
        private InventorySlotData GetSelectedItem(WeddingWishListSelectionPane pane) => GetItem(pane, GetSelectedIndex(pane));

        private WeddingWishListSelectionPane ResolveScrollTargetPane(Point mousePosition)
        {
            foreach ((WeddingWishListSelectionPane pane, _, Rectangle bounds) in EnumerateRowTargets())
            {
                if (bounds.Contains(mousePosition))
                {
                    return pane;
                }
            }

            return _snapshot.ActivePane;
        }

        private InventorySlotData GetItem(WeddingWishListSelectionPane pane, int index)
        {
            IReadOnlyList<InventorySlotData> entries = GetEntries(pane);
            return index >= 0 && index < entries.Count ? entries[index] : null;
        }

        private IReadOnlyList<InventorySlotData> GetEntries(WeddingWishListSelectionPane pane)
        {
            return pane switch
            {
                WeddingWishListSelectionPane.GiftList => _snapshot.GiftEntries,
                WeddingWishListSelectionPane.Inventory => _snapshot.InventoryEntries,
                WeddingWishListSelectionPane.WishList => _snapshot.WishEntries,
                WeddingWishListSelectionPane.Candidate => _snapshot.CandidateEntries,
                _ => Array.Empty<InventorySlotData>()
            };
        }

        private WeddingWishListSnapshot RefreshSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new WeddingWishListSnapshot();
        }

        private void RefreshLayout()
        {
            WeddingWishListRoleAssets roleAssets = GetModeAssets()?.GetRoleAssets(_snapshot.Role);
            int width = roleAssets?.Background?.Width ?? DefaultWidth;
            int height = roleAssets?.Background?.Height ?? DefaultHeight;
            Frame = new DXObject(0, 0, CreateFilledTexture(_device, width, height, Color.Transparent), 0);
            RefreshButtonLayout();
        }

        private void RefreshButtonLayout()
        {
            ConfigureButton(_getButton, _assets.ReceiveGetButtonPosition, _snapshot.Mode == WeddingWishListDialogMode.Receive && _snapshot.IsOpen);
            ConfigureButton(_putButton, _assets.GivePutButtonPosition, _snapshot.Mode == WeddingWishListDialogMode.Give && _snapshot.IsOpen);
            ConfigureButton(_enterButton, _assets.InputEnterButtonPosition, _snapshot.Mode == WeddingWishListDialogMode.Input && _snapshot.IsOpen);
            ConfigureButton(_deleteButton, _assets.InputDeleteButtonPosition, _snapshot.Mode == WeddingWishListDialogMode.Input && _snapshot.IsOpen);
            ConfigureButton(_confirmButton, _assets.InputOkButtonPosition, _snapshot.Mode == WeddingWishListDialogMode.Input && _snapshot.IsOpen);
            ConfigureButton(_closeButton, _assets.CloseButtonPosition, (_snapshot.Mode == WeddingWishListDialogMode.Receive || _snapshot.Mode == WeddingWishListDialogMode.Give) && _snapshot.IsOpen);
        }

        private void ConfigureButton(UIObject button, Point position, bool visible)
        {
            if (button == null)
            {
                return;
            }

            button.X = position.X;
            button.Y = position.Y;
            button.SetVisible(visible);
            button.SetEnabled(visible);
            button.ButtonVisible = visible;
        }

        private WeddingWishListModeAssets GetModeAssets()
        {
            return _assets.GetModeAssets(_snapshot.Mode);
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private string TrimToWidth(string text, float maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return text ?? string.Empty;
            }

            string trimmed = text.Trim();
            if (ClientTextDrawing.Measure((GraphicsDevice)null, trimmed, scale, _font).X <= maxWidth)
            {
                return trimmed;
            }

            const string ellipsis = "...";
            while (trimmed.Length > 1 && ClientTextDrawing.Measure((GraphicsDevice)null, trimmed + ellipsis, scale, _font).X > maxWidth)
            {
                trimmed = trimmed[..^1];
            }

            return trimmed + ellipsis;
        }

        private string[] WrapText(string text, float maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            List<string> lines = new();
            string currentLine = string.Empty;
            foreach (string word in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && ClientTextDrawing.Measure((GraphicsDevice)null, candidate, scale, _font).X > maxWidth)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines.ToArray();
        }

        private IEnumerable<char> EnumerateTypedCharacters(KeyboardState keyboardState)
        {
            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in keyboardState.GetPressedKeys())
            {
                if (_previousKeyboardState.IsKeyDown(key))
                {
                    continue;
                }

                if (TryTranslateCharacter(key, shift, out char value))
                {
                    yield return value;
                }
            }
        }

        private static bool TryTranslateCharacter(Keys key, bool shift, out char value)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                int offset = key - Keys.A;
                value = (char)((shift ? 'A' : 'a') + offset);
                return true;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                string shiftedDigits = ")!@#$%^&*(";
                int offset = key - Keys.D0;
                value = shift ? shiftedDigits[offset] : (char)('0' + offset);
                return true;
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                value = (char)('0' + (key - Keys.NumPad0));
                return true;
            }

            value = key switch
            {
                Keys.Space => ' ',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPlus => shift ? '+' : '=',
                Keys.OemComma => shift ? '<' : ',',
                Keys.OemPeriod => shift ? '>' : '.',
                Keys.OemQuestion => shift ? '?' : '/',
                Keys.OemSemicolon => shift ? ':' : ';',
                Keys.OemQuotes => shift ? '"' : '\'',
                Keys.OemOpenBrackets => shift ? '{' : '[',
                Keys.OemCloseBrackets => shift ? '}' : ']',
                Keys.OemPipe => shift ? '|' : '\\',
                _ => '\0'
            };

            return value != '\0';
        }

        private static string ResolveItemLabel(InventorySlotData item)
        {
            if (item == null)
            {
                return "Unknown item";
            }

            return !string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemName : $"Item {item.ItemId}";
        }

        private static Texture2D CreateFilledTexture(GraphicsDevice device, int width, int height, Color color)
        {
            Texture2D texture = new(device, width, height);
            Color[] data = new Color[width * height];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = color;
            }

            texture.SetData(data);
            return texture;
        }
    }

    internal sealed class WeddingWishListWindowAssets
    {
        private readonly IReadOnlyDictionary<WeddingWishListDialogMode, WeddingWishListModeAssets> _modeAssets;

        internal WeddingWishListWindowAssets(
            IReadOnlyDictionary<WeddingWishListDialogMode, WeddingWishListModeAssets> modeAssets,
            Texture2D[] tabEnabled,
            Texture2D[] tabDisabled,
            Point[] tabOffsets,
            Point receiveGetButtonPosition,
            Point givePutButtonPosition,
            Point inputEnterButtonPosition,
            Point inputDeleteButtonPosition,
            Point inputOkButtonPosition,
            Point closeButtonPosition)
        {
            _modeAssets = modeAssets ?? throw new ArgumentNullException(nameof(modeAssets));
            TabEnabled = tabEnabled ?? Array.Empty<Texture2D>();
            TabDisabled = tabDisabled ?? Array.Empty<Texture2D>();
            TabOffsets = tabOffsets ?? Array.Empty<Point>();
            ReceiveGetButtonPosition = receiveGetButtonPosition;
            GivePutButtonPosition = givePutButtonPosition;
            InputEnterButtonPosition = inputEnterButtonPosition;
            InputDeleteButtonPosition = inputDeleteButtonPosition;
            InputOkButtonPosition = inputOkButtonPosition;
            CloseButtonPosition = closeButtonPosition;
        }

        internal Texture2D[] TabEnabled { get; }
        internal Texture2D[] TabDisabled { get; }
        internal Point[] TabOffsets { get; }
        internal Point ReceiveGetButtonPosition { get; }
        internal Point GivePutButtonPosition { get; }
        internal Point InputEnterButtonPosition { get; }
        internal Point InputDeleteButtonPosition { get; }
        internal Point InputOkButtonPosition { get; }
        internal Point CloseButtonPosition { get; }

        internal WeddingWishListModeAssets GetModeAssets(WeddingWishListDialogMode mode)
        {
            return _modeAssets.TryGetValue(mode, out WeddingWishListModeAssets assets)
                ? assets
                : _modeAssets.Values.FirstOrDefault();
        }
    }

    internal sealed class WeddingWishListModeAssets
    {
        private readonly IReadOnlyDictionary<WeddingWishListRole, WeddingWishListRoleAssets> _roleAssets;

        internal WeddingWishListModeAssets(
            IReadOnlyDictionary<WeddingWishListRole, WeddingWishListRoleAssets> roleAssets,
            Texture2D selection)
        {
            _roleAssets = roleAssets ?? throw new ArgumentNullException(nameof(roleAssets));
            Selection = selection;
        }

        internal Texture2D Selection { get; }

        internal WeddingWishListRoleAssets GetRoleAssets(WeddingWishListRole role)
        {
            return _roleAssets.TryGetValue(role, out WeddingWishListRoleAssets assets)
                ? assets
                : _roleAssets.Values.FirstOrDefault();
        }
    }

    internal sealed class WeddingWishListRoleAssets
    {
        internal WeddingWishListRoleAssets(Texture2D background, Texture2D foreground, Point foregroundOffset, Texture2D headerOverlay, Point headerOverlayOffset)
        {
            Background = background;
            Foreground = foreground;
            ForegroundOffset = foregroundOffset;
            HeaderOverlay = headerOverlay;
            HeaderOverlayOffset = headerOverlayOffset;
        }

        internal Texture2D Background { get; }
        internal Texture2D Foreground { get; }
        internal Point ForegroundOffset { get; }
        internal Texture2D HeaderOverlay { get; }
        internal Point HeaderOverlayOffset { get; }
    }
}
