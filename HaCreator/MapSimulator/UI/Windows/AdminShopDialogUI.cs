using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.UI
{
    public enum AdminShopServiceMode
    {
        CashShop,
        Mts
    }

    public sealed class AdminShopDialogUI : UIWindowBase
    {
        private enum AdminShopPane
        {
            Npc,
            User
        }

        private sealed class AdminShopEntry
        {
            public string Title { get; init; } = string.Empty;
            public string Detail { get; init; } = string.Empty;
            public string Seller { get; init; } = string.Empty;
            public string PriceLabel { get; init; } = string.Empty;
            public long Price { get; init; }
            public bool SupportsWishlist { get; init; }
            public bool Wishlisted { get; set; }
        }

        private sealed class AdminShopPaneState
        {
            public List<AdminShopEntry> Entries { get; } = new();
            public int SelectedIndex { get; set; } = -1;
            public int ScrollOffset { get; set; }
        }

        private sealed class AdminShopTabVisual
        {
            public Texture2D EnabledTexture { get; set; }
            public Texture2D DisabledTexture { get; set; }
            public Point Offset { get; set; }
            public string Label { get; set; } = string.Empty;
        }

        private const int MaxVisibleRows = 5;
        private const int LeftPaneX = 17;
        private const int RightPaneX = 242;
        private const int PaneTopY = 101;
        private const int PaneRowHeight = 35;
        private const int PaneWidth = 165;
        private const int HeaderX = 18;
        private const int HeaderY = 72;
        private const int PaneLabelY = 90;
        private const int RowTextX = 14;
        private const int RowTitleY = 8;
        private const int RowDetailY = 22;
        private const int DetailX = 18;
        private const int DetailY = 278;
        private const int MoneyIconX = 335;
        private const int MoneyIconY = 299;
        private const int MoneyTextX = 353;
        private const int MoneyTextY = 296;
        private const int ScrollBarY = 131;
        private const int ScrollBarHeight = 194;
        private const int ScrollBarWidth = 12;
        private const int ScrollButtonHeight = 12;
        private const int NpcScrollBarX = 210;
        private const int UserScrollBarX = 441;
        private const int ScrollThumbMinHeight = 16;
        private const int ModalWidth = 206;
        private const int ModalHeight = 60;
        private const float ModalTextMaxWidth = 176f;
        private const int ServiceTabTextY = 4;
        private const int PaneTabTextY = 4;

        private readonly string _windowName;
        private readonly AdminShopServiceMode _defaultMode;
        private readonly IDXObject _frameOverlay;
        private readonly Point _frameOverlayOffset;
        private readonly IDXObject _contentOverlay;
        private readonly Point _contentOverlayOffset;
        private readonly Texture2D _selectionTexture;
        private readonly Texture2D _mesoTexture;
        private readonly Texture2D _pixelTexture;
        private readonly UIObject _buyButton;
        private readonly UIObject _sellButton;
        private readonly UIObject _exitButton;
        private readonly UIObject _rechargeButton;
        private readonly List<UIObject> _npcRowButtons = new();
        private readonly List<UIObject> _userRowButtons = new();
        private readonly List<UIObject> _modalButtons = new();
        private readonly Dictionary<AdminShopPane, AdminShopPaneState> _paneStates = new()
        {
            [AdminShopPane.Npc] = new AdminShopPaneState(),
            [AdminShopPane.User] = new AdminShopPaneState()
        };
        private readonly AdminShopTabVisual[] _serviceTabs = new AdminShopTabVisual[2];
        private readonly AdminShopTabVisual[] _paneTabs = new AdminShopTabVisual[2];
        private readonly Texture2D _modalTexture;
        private readonly UIObject _modalConfirmButton;
        private readonly UIObject _modalCancelButton;

        private SpriteFont _font;
        private AdminShopServiceMode _currentMode;
        private AdminShopPane _activePane = AdminShopPane.Npc;
        private string _footerMessage = string.Empty;
        private string _modalMessage = string.Empty;
        private AdminShopEntry _pendingWishlistEntry;
        private int _previousScrollWheelValue;
        private MouseState _previousMouseState;
        private AdminShopPane? _draggingScrollPane;
        private int _scrollThumbDragOffsetY;
        private bool _wishlistModalVisible;

        public AdminShopDialogUI(
            IDXObject frame,
            string windowName,
            AdminShopServiceMode defaultMode,
            IDXObject frameOverlay,
            Point frameOverlayOffset,
            IDXObject contentOverlay,
            Point contentOverlayOffset,
            Texture2D selectionTexture,
            Texture2D mesoTexture,
            UIObject buyButton,
            UIObject sellButton,
            UIObject exitButton,
            UIObject rechargeButton,
            Texture2D modalTexture,
            UIObject modalConfirmButton,
            UIObject modalCancelButton,
            GraphicsDevice device)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _defaultMode = defaultMode;
            _currentMode = defaultMode;
            _frameOverlay = frameOverlay;
            _frameOverlayOffset = frameOverlayOffset;
            _contentOverlay = contentOverlay;
            _contentOverlayOffset = contentOverlayOffset;
            _selectionTexture = selectionTexture;
            _mesoTexture = mesoTexture;
            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
            _buyButton = buyButton;
            _sellButton = sellButton;
            _exitButton = exitButton;
            _rechargeButton = rechargeButton;
            _modalTexture = modalTexture;
            _modalConfirmButton = modalConfirmButton;
            _modalCancelButton = modalCancelButton;

            if (_buyButton != null)
            {
                AddButton(_buyButton);
                _buyButton.ButtonClickReleased += OnBuyButtonClicked;
            }

            if (_sellButton != null)
            {
                AddButton(_sellButton);
                _sellButton.ButtonClickReleased += OnSellButtonClicked;
            }

            if (_exitButton != null)
            {
                AddButton(_exitButton);
                _exitButton.ButtonClickReleased += _ => Hide();
            }

            if (_rechargeButton != null)
            {
                AddButton(_rechargeButton);
                _rechargeButton.ButtonClickReleased += OnRechargeButtonClicked;
            }

            if (_modalConfirmButton != null)
            {
                AddButton(_modalConfirmButton);
                _modalButtons.Add(_modalConfirmButton);
                _modalConfirmButton.SetVisible(false);
                _modalConfirmButton.ButtonClickReleased += OnModalConfirmClicked;
            }

            if (_modalCancelButton != null)
            {
                AddButton(_modalCancelButton);
                _modalButtons.Add(_modalCancelButton);
                _modalCancelButton.SetVisible(false);
                _modalCancelButton.ButtonClickReleased += OnModalCancelClicked;
            }

            InitializeRowButtons(device);
            InitializeTabVisuals();
            ResetMode(defaultMode);
        }

        public override string WindowName => _windowName;

        public long Money { get; set; }

        public override void Show()
        {
            base.Show();
            MouseState mouseState = Mouse.GetState();
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
            _previousMouseState = mouseState;
            ResetMode(_defaultMode);
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible)
            {
                return;
            }

            MouseState mouseState = Mouse.GetState();
            int wheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;

            if (_wishlistModalVisible)
            {
                _previousMouseState = mouseState;
                return;
            }

            if (_draggingScrollPane.HasValue)
            {
                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    SetScrollOffsetFromThumb(_draggingScrollPane.Value, mouseState.Y);
                }
                else
                {
                    _draggingScrollPane = null;
                }
            }

            if (leftJustPressed)
            {
                if (TryHandleServiceTabClick(mouseState) || TryHandlePaneTabClick(mouseState) || TryHandleScrollBarMouseDown(mouseState))
                {
                    _previousMouseState = mouseState;
                    return;
                }
            }

            if (wheelDelta == 0)
            {
                _previousMouseState = mouseState;
                return;
            }

            AdminShopPane? hoveredPane = GetPaneAt(mouseState.X, mouseState.Y);
            if (!hoveredPane.HasValue)
            {
                _previousMouseState = mouseState;
                return;
            }

            AdminShopPaneState paneState = _paneStates[hoveredPane.Value];
            if (paneState.Entries.Count <= MaxVisibleRows)
            {
                _previousMouseState = mouseState;
                return;
            }

            paneState.ScrollOffset += wheelDelta > 0 ? -1 : 1;
            ClampPaneState(paneState);
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
            int windowX = Position.X;
            int windowY = Position.Y;

            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _frameOverlay, _frameOverlayOffset, windowX, windowY, drawReflectionInfo);
            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _contentOverlay, _contentOverlayOffset, windowX, windowY, drawReflectionInfo);

            if (_font == null)
            {
                return;
            }

            DrawHeader(sprite, windowX, windowY);
            DrawTabs(sprite, windowX, windowY);
            DrawPane(sprite, windowX, windowY, LeftPaneX, AdminShopPane.Npc, "NPC offers");
            DrawPane(sprite, windowX, windowY, RightPaneX, AdminShopPane.User, "User listings");
            DrawScrollBar(sprite, windowX, windowY, AdminShopPane.Npc);
            DrawScrollBar(sprite, windowX, windowY, AdminShopPane.User);
            DrawFooter(sprite, windowX, windowY);
            DrawMoney(sprite, windowX, windowY);

            if (_wishlistModalVisible)
            {
                DrawWishlistModal(sprite, windowX, windowY);
            }
        }

        private void DrawLayer(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            IDXObject layer,
            Point offset,
            int windowX,
            int windowY,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            layer?.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                windowX + offset.X,
                windowY + offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private void DrawHeader(SpriteBatch sprite, int windowX, int windowY)
        {
            string modeLabel = _currentMode == AdminShopServiceMode.CashShop ? "Cash Shop" : "MTS";
            string instruction = "BtBuy submits a request, BtSell switches service, BtRecharge opens the wish-list dialog.";

            sprite.DrawString(_font, modeLabel + " dialog", new Vector2(windowX + HeaderX, windowY + HeaderY), Color.White);
            sprite.DrawString(_font, instruction, new Vector2(windowX + HeaderX, windowY + HeaderY + 18), new Color(215, 215, 215));
        }

        private void DrawTabs(SpriteBatch sprite, int windowX, int windowY)
        {
            DrawTab(sprite, windowX, windowY, _serviceTabs[0], _currentMode == AdminShopServiceMode.CashShop, ServiceTabTextY);
            DrawTab(sprite, windowX, windowY, _serviceTabs[1], _currentMode == AdminShopServiceMode.Mts, ServiceTabTextY);
            DrawTab(sprite, windowX, windowY, _paneTabs[0], _activePane == AdminShopPane.Npc, PaneTabTextY);
            DrawTab(sprite, windowX, windowY, _paneTabs[1], _activePane == AdminShopPane.User, PaneTabTextY);
        }

        private void DrawTab(SpriteBatch sprite, int windowX, int windowY, AdminShopTabVisual tab, bool enabled, int textOffsetY)
        {
            if (tab == null)
            {
                return;
            }

            Texture2D texture = enabled ? tab.EnabledTexture ?? tab.DisabledTexture : tab.DisabledTexture ?? tab.EnabledTexture;
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(windowX + tab.Offset.X, windowY + tab.Offset.Y), Color.White);
            }

            if (_font == null || string.IsNullOrWhiteSpace(tab.Label))
            {
                return;
            }

            Vector2 textSize = _font.MeasureString(tab.Label);
            float textX = windowX + tab.Offset.X + ((texture?.Width ?? 42) - textSize.X) / 2f;
            float textY = windowY + tab.Offset.Y + textOffsetY;
            Color textColor = enabled ? new Color(66, 38, 0) : new Color(230, 226, 218);
            sprite.DrawString(_font, tab.Label, new Vector2(textX, textY), textColor);
        }

        private void DrawPane(SpriteBatch sprite, int windowX, int windowY, int paneX, AdminShopPane pane, string label)
        {
            AdminShopPaneState paneState = _paneStates[pane];
            Color labelColor = _activePane == pane ? new Color(255, 236, 166) : new Color(206, 206, 206);
            sprite.DrawString(_font, label, new Vector2(windowX + paneX, windowY + PaneLabelY), labelColor);

            int visibleCount = Math.Min(MaxVisibleRows, Math.Max(0, paneState.Entries.Count - paneState.ScrollOffset));
            for (int row = 0; row < visibleCount; row++)
            {
                int actualIndex = paneState.ScrollOffset + row;
                AdminShopEntry entry = paneState.Entries[actualIndex];
                bool isSelected = pane == _activePane && actualIndex == paneState.SelectedIndex;
                int rowX = windowX + paneX;
                int rowY = windowY + PaneTopY + (row * PaneRowHeight);

                if (_selectionTexture != null && isSelected)
                {
                    sprite.Draw(_selectionTexture, new Vector2(rowX, rowY), Color.White);
                }

                Color titleColor = isSelected ? Color.Black : Color.White;
                Color detailColor = isSelected ? new Color(42, 42, 42) : new Color(210, 210, 210);
                sprite.DrawString(_font, TrimToWidth(entry.Title, 130f), new Vector2(rowX + RowTextX, rowY + RowTitleY), titleColor);
                sprite.DrawString(_font, TrimToWidth(entry.PriceLabel, 130f), new Vector2(rowX + RowTextX, rowY + RowDetailY), detailColor);
            }

            if (paneState.Entries.Count > MaxVisibleRows)
            {
                string page = $"{paneState.ScrollOffset + 1}-{Math.Min(paneState.ScrollOffset + MaxVisibleRows, paneState.Entries.Count)}/{paneState.Entries.Count}";
                Vector2 size = _font.MeasureString(page);
                sprite.DrawString(_font, page, new Vector2(windowX + paneX + PaneWidth - size.X, windowY + PaneLabelY), new Color(190, 190, 190));
            }
        }

        private void DrawScrollBar(SpriteBatch sprite, int windowX, int windowY, AdminShopPane pane)
        {
            Rectangle barBounds = GetScrollBarBounds(windowX, windowY, pane);
            Rectangle upBounds = GetScrollUpButtonBounds(windowX, windowY, pane);
            Rectangle downBounds = GetScrollDownButtonBounds(windowX, windowY, pane);
            Rectangle trackBounds = GetScrollTrackBounds(windowX, windowY, pane);
            Rectangle thumbBounds = GetScrollThumbBounds(windowX, windowY, pane);
            bool canScroll = GetMaxScrollOffset(_paneStates[pane]) > 0;

            sprite.Draw(_pixelTexture, barBounds, new Color(16, 16, 16, 220));
            sprite.Draw(_pixelTexture, upBounds, canScroll ? new Color(88, 88, 88) : new Color(52, 52, 52));
            sprite.Draw(_pixelTexture, downBounds, canScroll ? new Color(88, 88, 88) : new Color(52, 52, 52));
            sprite.Draw(_pixelTexture, trackBounds, canScroll ? new Color(42, 42, 42) : new Color(30, 30, 30));
            sprite.Draw(_pixelTexture, thumbBounds, canScroll ? new Color(215, 177, 84) : new Color(96, 96, 96));

            DrawArrowGlyph(sprite, upBounds, true);
            DrawArrowGlyph(sprite, downBounds, false);
        }

        private void DrawArrowGlyph(SpriteBatch sprite, Rectangle bounds, bool up)
        {
            if (_font == null)
            {
                return;
            }

            string glyph = up ? "^" : "v";
            Vector2 size = _font.MeasureString(glyph);
            Vector2 position = new Vector2(
                bounds.X + (bounds.Width - size.X) / 2f,
                bounds.Y + (bounds.Height - size.Y) / 2f - 1f);
            sprite.DrawString(_font, glyph, position, Color.White);
        }

        private void DrawFooter(SpriteBatch sprite, int windowX, int windowY)
        {
            AdminShopEntry entry = GetSelectedEntry();
            if (entry == null)
            {
                sprite.DrawString(_font, "Select an NPC offer or user listing.", new Vector2(windowX + DetailX, windowY + DetailY), Color.White);
                return;
            }

            sprite.DrawString(_font, entry.Title, new Vector2(windowX + DetailX, windowY + DetailY), Color.White);
            sprite.DrawString(_font, $"{entry.Seller}  |  {entry.PriceLabel}", new Vector2(windowX + DetailX, windowY + DetailY + 18), new Color(235, 224, 164));

            float detailY = windowY + DetailY + 36;
            foreach (string line in WrapText(entry.Detail, 400f))
            {
                sprite.DrawString(_font, line, new Vector2(windowX + DetailX, detailY), new Color(218, 218, 218));
                detailY += 16f;
            }

            string wishState = entry.Wishlisted ? "Wish list: saved." : "Wish list: not saved.";
            sprite.DrawString(_font, wishState, new Vector2(windowX + DetailX, windowY + DetailY + 52), new Color(175, 220, 175));

            if (!string.IsNullOrWhiteSpace(_footerMessage))
            {
                sprite.DrawString(_font, _footerMessage, new Vector2(windowX + DetailX, windowY + DetailY + 68), new Color(255, 221, 143));
            }
        }

        private void DrawWishlistModal(SpriteBatch sprite, int windowX, int windowY)
        {
            Rectangle modalBounds = GetModalBounds(windowX, windowY);

            sprite.Draw(_pixelTexture, new Rectangle(windowX, windowY, CurrentFrame?.Width ?? 465, CurrentFrame?.Height ?? 328), new Color(0, 0, 0, 96));
            if (_modalTexture != null)
            {
                sprite.Draw(_modalTexture, new Vector2(modalBounds.X, modalBounds.Y), Color.White);
            }
            else
            {
                sprite.Draw(_pixelTexture, modalBounds, new Color(248, 244, 230));
            }

            float lineY = modalBounds.Y + 10f;
            foreach (string line in WrapText(_modalMessage, ModalTextMaxWidth))
            {
                Vector2 lineSize = _font.MeasureString(line);
                float lineX = modalBounds.X + (modalBounds.Width - lineSize.X) / 2f;
                sprite.DrawString(_font, line, new Vector2(lineX, lineY), new Color(55, 39, 15));
                lineY += 14f;
            }
        }

        private void DrawMoney(SpriteBatch sprite, int windowX, int windowY)
        {
            if (_mesoTexture != null)
            {
                sprite.Draw(_mesoTexture, new Vector2(windowX + MoneyIconX, windowY + MoneyIconY), Color.White);
            }

            sprite.DrawString(_font, Money.ToString("N0", CultureInfo.InvariantCulture), new Vector2(windowX + MoneyTextX, windowY + MoneyTextY), Color.White);
        }

        private void OnBuyButtonClicked(UIObject sender)
        {
            AdminShopEntry entry = GetSelectedEntry();
            if (entry == null)
            {
                _footerMessage = "Select an offer before sending a request.";
                UpdateActionButtonStates();
                return;
            }

            _pendingWishlistEntry = null;
            string modeLabel = _currentMode == AdminShopServiceMode.CashShop ? "Cash Shop" : "MTS";
            string targetLabel = _activePane == AdminShopPane.Npc ? "NPC" : "user";
            _footerMessage = $"{modeLabel} request sent for {entry.Title} via the {targetLabel} list.";
            UpdateActionButtonStates();
        }

        private void OnSellButtonClicked(UIObject sender)
        {
            AdminShopServiceMode nextMode = _currentMode == AdminShopServiceMode.CashShop
                ? AdminShopServiceMode.Mts
                : AdminShopServiceMode.CashShop;
            ResetMode(nextMode);
            _footerMessage = nextMode == AdminShopServiceMode.CashShop
                ? "Switched to Cash Shop offers."
                : "Switched to MTS offers.";
            UpdateActionButtonStates();
        }

        private void OnRechargeButtonClicked(UIObject sender)
        {
            AdminShopEntry entry = GetSelectedEntry();
            if (entry == null)
            {
                _footerMessage = "Select an NPC offer before confirming a wish list entry.";
                UpdateActionButtonStates();
                return;
            }

            if (!entry.SupportsWishlist)
            {
                _footerMessage = "Only NPC offers can be added to the wish list preview.";
                UpdateActionButtonStates();
                return;
            }

            if (!ReferenceEquals(_pendingWishlistEntry, entry))
            {
                OpenWishlistConfirmation(entry);
                UpdateActionButtonStates();
                return;
            }
        }

        private void InitializeRowButtons(GraphicsDevice device)
        {
            for (int row = 0; row < MaxVisibleRows; row++)
            {
                UIObject npcButton = CreateRowButton(device);
                npcButton.X = LeftPaneX;
                npcButton.Y = PaneTopY + (row * PaneRowHeight);
                int capturedNpcRow = row;
                npcButton.ButtonClickReleased += _ => SelectRow(AdminShopPane.Npc, capturedNpcRow);
                AddButton(npcButton);
                _npcRowButtons.Add(npcButton);

                UIObject userButton = CreateRowButton(device);
                userButton.X = RightPaneX;
                userButton.Y = PaneTopY + (row * PaneRowHeight);
                int capturedUserRow = row;
                userButton.ButtonClickReleased += _ => SelectRow(AdminShopPane.User, capturedUserRow);
                AddButton(userButton);
                _userRowButtons.Add(userButton);
            }
        }

        private static UIObject CreateRowButton(GraphicsDevice device)
        {
            Texture2D texture = new Texture2D(device, PaneWidth, PaneRowHeight);
            Color[] pixels = new Color[PaneWidth * PaneRowHeight];
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

        private void SelectRow(AdminShopPane pane, int rowIndex)
        {
            AdminShopPaneState paneState = _paneStates[pane];
            int actualIndex = paneState.ScrollOffset + rowIndex;
            if (actualIndex < 0 || actualIndex >= paneState.Entries.Count)
            {
                return;
            }

            _activePane = pane;
            paneState.SelectedIndex = actualIndex;
            _pendingWishlistEntry = null;
            _footerMessage = BuildSelectionMessage(paneState.Entries[actualIndex], pane);
            ClampPaneState(paneState);
            UpdateActionButtonStates();
        }

        private void ResetMode(AdminShopServiceMode mode)
        {
            _currentMode = mode;
            _activePane = AdminShopPane.Npc;
            _pendingWishlistEntry = null;
            _wishlistModalVisible = false;
            _paneStates[AdminShopPane.Npc].Entries.Clear();
            _paneStates[AdminShopPane.User].Entries.Clear();
            _paneStates[AdminShopPane.Npc].Entries.AddRange(CreateNpcEntries(mode));
            _paneStates[AdminShopPane.User].Entries.AddRange(CreateUserEntries(mode));

            foreach (AdminShopPane pane in Enum.GetValues(typeof(AdminShopPane)))
            {
                AdminShopPaneState paneState = _paneStates[pane];
                paneState.ScrollOffset = 0;
                paneState.SelectedIndex = paneState.Entries.Count > 0 ? 0 : -1;
                ClampPaneState(paneState);
            }

            _footerMessage = BuildSelectionMessage(GetSelectedEntry(), _activePane);
            UpdateRowButtons();
            UpdateActionButtonStates();
            UpdateModalButtons();
        }

        private void UpdateRowButtons()
        {
            UpdateRowButtonsForPane(_npcRowButtons, _paneStates[AdminShopPane.Npc]);
            UpdateRowButtonsForPane(_userRowButtons, _paneStates[AdminShopPane.User]);
        }

        private static void UpdateRowButtonsForPane(List<UIObject> buttons, AdminShopPaneState paneState)
        {
            for (int row = 0; row < buttons.Count; row++)
            {
                int actualIndex = paneState.ScrollOffset + row;
                bool visible = actualIndex < paneState.Entries.Count;
                buttons[row].SetVisible(visible);
                buttons[row].SetEnabled(visible);
            }
        }

        private void UpdateActionButtonStates()
        {
            AdminShopEntry entry = GetSelectedEntry();
            bool modalBlocked = _wishlistModalVisible;
            _buyButton?.SetEnabled(!modalBlocked && entry != null);
            _sellButton?.SetEnabled(!modalBlocked);
            _exitButton?.SetEnabled(!modalBlocked);
            _rechargeButton?.SetEnabled(!modalBlocked && entry?.SupportsWishlist == true);
        }

        private AdminShopEntry GetSelectedEntry()
        {
            AdminShopPaneState paneState = _paneStates[_activePane];
            if (paneState.SelectedIndex < 0 || paneState.SelectedIndex >= paneState.Entries.Count)
            {
                return null;
            }

            return paneState.Entries[paneState.SelectedIndex];
        }

        private void ClampPaneState(AdminShopPaneState paneState)
        {
            int maxScroll = GetMaxScrollOffset(paneState);
            paneState.ScrollOffset = Math.Clamp(paneState.ScrollOffset, 0, maxScroll);

            if (paneState.SelectedIndex >= 0 && paneState.SelectedIndex < paneState.ScrollOffset)
            {
                paneState.ScrollOffset = paneState.SelectedIndex;
            }
            else if (paneState.SelectedIndex >= paneState.ScrollOffset + MaxVisibleRows)
            {
                paneState.ScrollOffset = Math.Max(0, paneState.SelectedIndex - MaxVisibleRows + 1);
            }
        }

        private AdminShopPane? GetPaneAt(int mouseX, int mouseY)
        {
            Rectangle leftPane = new Rectangle(Position.X + LeftPaneX, Position.Y + PaneTopY, PaneWidth, MaxVisibleRows * PaneRowHeight);
            if (leftPane.Contains(mouseX, mouseY))
            {
                return AdminShopPane.Npc;
            }

            Rectangle rightPane = new Rectangle(Position.X + RightPaneX, Position.Y + PaneTopY, PaneWidth, MaxVisibleRows * PaneRowHeight);
            if (rightPane.Contains(mouseX, mouseY))
            {
                return AdminShopPane.User;
            }

            return null;
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

            if (_wishlistModalVisible)
            {
                foreach (UIObject button in _modalButtons)
                {
                    if (button.CheckMouseEvent(shiftCenteredX, shiftCenteredY, Position.X, Position.Y, mouseState))
                    {
                        mouseCursor?.SetMouseCursorMovedToClickableItem();
                        return true;
                    }
                }

                return ContainsPoint(mouseState.X, mouseState.Y);
            }

            return base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
        }

        private void InitializeTabVisuals()
        {
            _serviceTabs[0] = new AdminShopTabVisual { Label = "Cash", Offset = new Point(10, 91) };
            _serviceTabs[1] = new AdminShopTabVisual { Label = "MTS", Offset = new Point(53, 91) };
            _paneTabs[0] = new AdminShopTabVisual { Label = "NPC", Offset = new Point(241, 91) };
            _paneTabs[1] = new AdminShopTabVisual { Label = "User", Offset = new Point(284, 91) };
        }

        public void SetTabTextures(
            Texture2D cashEnabled,
            Texture2D cashDisabled,
            Texture2D mtsEnabled,
            Texture2D mtsDisabled,
            Texture2D npcEnabled,
            Texture2D npcDisabled,
            Texture2D userEnabled,
            Texture2D userDisabled)
        {
            _serviceTabs[0].EnabledTexture = cashEnabled;
            _serviceTabs[0].DisabledTexture = cashDisabled;
            _serviceTabs[1].EnabledTexture = mtsEnabled;
            _serviceTabs[1].DisabledTexture = mtsDisabled;
            _paneTabs[0].EnabledTexture = npcEnabled;
            _paneTabs[0].DisabledTexture = npcDisabled;
            _paneTabs[1].EnabledTexture = userEnabled;
            _paneTabs[1].DisabledTexture = userDisabled;
        }

        private bool TryHandleServiceTabClick(MouseState mouseState)
        {
            if (GetTabBounds(_serviceTabs[0]).Contains(mouseState.X, mouseState.Y))
            {
                if (_currentMode != AdminShopServiceMode.CashShop)
                {
                    ResetMode(AdminShopServiceMode.CashShop);
                    _footerMessage = "Switched to Cash Shop offers.";
                }
                return true;
            }

            if (GetTabBounds(_serviceTabs[1]).Contains(mouseState.X, mouseState.Y))
            {
                if (_currentMode != AdminShopServiceMode.Mts)
                {
                    ResetMode(AdminShopServiceMode.Mts);
                    _footerMessage = "Switched to MTS offers.";
                }
                return true;
            }

            return false;
        }

        private bool TryHandlePaneTabClick(MouseState mouseState)
        {
            if (GetTabBounds(_paneTabs[0]).Contains(mouseState.X, mouseState.Y))
            {
                _activePane = AdminShopPane.Npc;
                _footerMessage = BuildSelectionMessage(GetSelectedEntry(), _activePane);
                UpdateActionButtonStates();
                return true;
            }

            if (GetTabBounds(_paneTabs[1]).Contains(mouseState.X, mouseState.Y))
            {
                _activePane = AdminShopPane.User;
                _footerMessage = BuildSelectionMessage(GetSelectedEntry(), _activePane);
                UpdateActionButtonStates();
                return true;
            }

            return false;
        }

        private Rectangle GetTabBounds(AdminShopTabVisual tab)
        {
            int width = tab?.EnabledTexture?.Width ?? tab?.DisabledTexture?.Width ?? 42;
            int height = tab?.EnabledTexture?.Height ?? tab?.DisabledTexture?.Height ?? 19;
            return new Rectangle(Position.X + tab.Offset.X, Position.Y + tab.Offset.Y, width, height);
        }

        private bool TryHandleScrollBarMouseDown(MouseState mouseState)
        {
            foreach (AdminShopPane pane in Enum.GetValues(typeof(AdminShopPane)))
            {
                Rectangle scrollBarBounds = GetScrollBarBounds(Position.X, Position.Y, pane);
                if (!scrollBarBounds.Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                AdminShopPaneState paneState = _paneStates[pane];
                if (GetMaxScrollOffset(paneState) <= 0)
                {
                    return true;
                }

                if (GetScrollUpButtonBounds(Position.X, Position.Y, pane).Contains(mouseState.X, mouseState.Y))
                {
                    paneState.ScrollOffset--;
                    ClampPaneState(paneState);
                    UpdateRowButtons();
                    return true;
                }

                if (GetScrollDownButtonBounds(Position.X, Position.Y, pane).Contains(mouseState.X, mouseState.Y))
                {
                    paneState.ScrollOffset++;
                    ClampPaneState(paneState);
                    UpdateRowButtons();
                    return true;
                }

                Rectangle thumbBounds = GetScrollThumbBounds(Position.X, Position.Y, pane);
                if (thumbBounds.Contains(mouseState.X, mouseState.Y))
                {
                    _draggingScrollPane = pane;
                    _scrollThumbDragOffsetY = mouseState.Y - thumbBounds.Y;
                    return true;
                }

                Rectangle trackBounds = GetScrollTrackBounds(Position.X, Position.Y, pane);
                if (trackBounds.Contains(mouseState.X, mouseState.Y))
                {
                    paneState.ScrollOffset += mouseState.Y < thumbBounds.Y ? -MaxVisibleRows : MaxVisibleRows;
                    ClampPaneState(paneState);
                    UpdateRowButtons();
                    return true;
                }

                return true;
            }

            return false;
        }

        private Rectangle GetScrollBarBounds(int windowX, int windowY, AdminShopPane pane)
        {
            int x = pane == AdminShopPane.Npc ? NpcScrollBarX : UserScrollBarX;
            return new Rectangle(windowX + x, windowY + ScrollBarY, ScrollBarWidth, ScrollBarHeight);
        }

        private Rectangle GetScrollUpButtonBounds(int windowX, int windowY, AdminShopPane pane)
        {
            Rectangle bounds = GetScrollBarBounds(windowX, windowY, pane);
            return new Rectangle(bounds.X, bounds.Y, bounds.Width, ScrollButtonHeight);
        }

        private Rectangle GetScrollDownButtonBounds(int windowX, int windowY, AdminShopPane pane)
        {
            Rectangle bounds = GetScrollBarBounds(windowX, windowY, pane);
            return new Rectangle(bounds.X, bounds.Bottom - ScrollButtonHeight, bounds.Width, ScrollButtonHeight);
        }

        private Rectangle GetScrollTrackBounds(int windowX, int windowY, AdminShopPane pane)
        {
            Rectangle bounds = GetScrollBarBounds(windowX, windowY, pane);
            return new Rectangle(bounds.X, bounds.Y + ScrollButtonHeight, bounds.Width, bounds.Height - (ScrollButtonHeight * 2));
        }

        private Rectangle GetScrollThumbBounds(int windowX, int windowY, AdminShopPane pane)
        {
            Rectangle trackBounds = GetScrollTrackBounds(windowX, windowY, pane);
            AdminShopPaneState paneState = _paneStates[pane];
            int maxScroll = GetMaxScrollOffset(paneState);
            if (maxScroll <= 0)
            {
                return new Rectangle(trackBounds.X, trackBounds.Y, trackBounds.Width, trackBounds.Height);
            }

            float visibleRatio = MaxVisibleRows / (float)paneState.Entries.Count;
            int thumbHeight = Math.Max(ScrollThumbMinHeight, (int)Math.Round(trackBounds.Height * visibleRatio));
            thumbHeight = Math.Min(trackBounds.Height, thumbHeight);
            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int thumbY = trackBounds.Y + (travel == 0 ? 0 : (int)Math.Round((paneState.ScrollOffset / (float)maxScroll) * travel));
            return new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
        }

        private void SetScrollOffsetFromThumb(AdminShopPane pane, int mouseY)
        {
            Rectangle trackBounds = GetScrollTrackBounds(Position.X, Position.Y, pane);
            Rectangle thumbBounds = GetScrollThumbBounds(Position.X, Position.Y, pane);
            int travel = Math.Max(0, trackBounds.Height - thumbBounds.Height);
            int maxScroll = GetMaxScrollOffset(_paneStates[pane]);
            if (travel <= 0 || maxScroll <= 0)
            {
                return;
            }

            int thumbTop = Math.Clamp(mouseY - _scrollThumbDragOffsetY, trackBounds.Y, trackBounds.Bottom - thumbBounds.Height);
            float ratio = (thumbTop - trackBounds.Y) / (float)travel;
            _paneStates[pane].ScrollOffset = (int)Math.Round(ratio * maxScroll);
            ClampPaneState(_paneStates[pane]);
            UpdateRowButtons();
        }

        private static int GetMaxScrollOffset(AdminShopPaneState paneState)
        {
            return Math.Max(0, paneState.Entries.Count - MaxVisibleRows);
        }

        private void OpenWishlistConfirmation(AdminShopEntry entry)
        {
            _pendingWishlistEntry = entry;
            _wishlistModalVisible = true;
            _modalMessage = $"Add {entry.Title} to the wish list?";
            _footerMessage = $"Wish list dialog opened for {entry.Title}.";
            PositionModalButtons();
            UpdateModalButtons();
            UpdateActionButtonStates();
        }

        private void OnModalConfirmClicked(UIObject sender)
        {
            if (_pendingWishlistEntry == null)
            {
                CloseWishlistConfirmation("Wish list dialog closed.");
                return;
            }

            _pendingWishlistEntry.Wishlisted = true;
            string title = _pendingWishlistEntry.Title;
            CloseWishlistConfirmation($"Wish list confirmed for {title}.");
        }

        private void OnModalCancelClicked(UIObject sender)
        {
            string title = _pendingWishlistEntry?.Title;
            CloseWishlistConfirmation(string.IsNullOrWhiteSpace(title)
                ? "Wish list dialog cancelled."
                : $"Wish list dialog cancelled for {title}.");
        }

        private void CloseWishlistConfirmation(string footerMessage)
        {
            _pendingWishlistEntry = null;
            _wishlistModalVisible = false;
            _modalMessage = string.Empty;
            _footerMessage = footerMessage;
            UpdateModalButtons();
            UpdateActionButtonStates();
        }

        private void UpdateModalButtons()
        {
            bool visible = _wishlistModalVisible;
            foreach (UIObject button in _modalButtons)
            {
                button.SetVisible(visible);
                button.SetEnabled(visible);
            }

            if (visible)
            {
                PositionModalButtons();
            }
        }

        private void PositionModalButtons()
        {
            Rectangle modalBounds = GetModalBounds(Position.X, Position.Y);
            if (_modalConfirmButton != null)
            {
                _modalConfirmButton.X = modalBounds.X - Position.X + 34;
                _modalConfirmButton.Y = modalBounds.Y - Position.Y + 35;
            }

            if (_modalCancelButton != null)
            {
                _modalCancelButton.X = modalBounds.X - Position.X + 111;
                _modalCancelButton.Y = modalBounds.Y - Position.Y + 35;
            }
        }

        private Rectangle GetModalBounds(int windowX, int windowY)
        {
            int frameWidth = CurrentFrame?.Width ?? 465;
            int frameHeight = CurrentFrame?.Height ?? 328;
            int modalX = windowX + (frameWidth - ModalWidth) / 2;
            int modalY = windowY + (frameHeight - ModalHeight) / 2;
            return new Rectangle(modalX, modalY, ModalWidth, ModalHeight);
        }

        private static string BuildSelectionMessage(AdminShopEntry entry, AdminShopPane pane)
        {
            if (entry == null)
            {
                return "Select an offer to inspect request details.";
            }

            string paneLabel = pane == AdminShopPane.Npc ? "NPC offer" : "user listing";
            return $"Selected {paneLabel}: {entry.Title}.";
        }

        private static IEnumerable<AdminShopEntry> CreateNpcEntries(AdminShopServiceMode mode)
        {
            if (mode == AdminShopServiceMode.CashShop)
            {
                return new[]
                {
                    CreateEntry("Royal Hair Coupon", "Rotating salon coupon preview from the featured cash-service board.", "Cash Manager", 3400, true),
                    CreateEntry("Royal Face Coupon", "Premium face coupon entry with the same preview flow the client routes into wish-list dialogs.", "Cash Manager", 2900, true),
                    CreateEntry("Pet Snack Bundle", "Utility bundle with snack and pet-tag support for multi-pet sessions.", "Cash Manager", 1900, true),
                    CreateEntry("Storage Slot Expansion", "Convenience service for storage and shared account inventory capacity.", "Cash Manager", 2800, true),
                    CreateEntry("Hyper Teleport Rock", "Navigation-heavy service entry used to compare convenience bundles.", "Cash Manager", 900, true),
                    CreateEntry("Surprise Style Box", "Random cosmetic box surfaced through the featured rotation.", "Cash Manager", 3400, true),
                    CreateEntry("Cosmetic Lens Coupon", "Style utility item staged for wish-list confirmation tests.", "Cash Manager", 1600, true),
                    CreateEntry("Pet Equip Bundle", "Pet equipment and accessory bundle bound to the NPC-side catalog.", "Cash Manager", 2200, true)
                };
            }

            return new[]
            {
                CreateEntry("Zakum Helmet Listing", "Admin MTS preview of a high-demand helmet sold from the NPC-owned catalog view.", "MTS Clerk", 12500000, false),
                CreateEntry("Maple Kandayo", "MTS equipment board seeded to exercise request submission and price display.", "MTS Clerk", 9800000, false),
                CreateEntry("Steely Throwing-Knives", "Consumable trade board sample that mirrors a browse-first MTS flow.", "MTS Clerk", 3200000, false),
                CreateEntry("Chaos Scroll 60%", "Scroll listing preview staged for user-vs-NPC comparison.", "MTS Clerk", 21000000, false),
                CreateEntry("Brown Work Gloves", "Common MTS browse row with seller and price labels only.", "MTS Clerk", 4700000, false),
                CreateEntry("Pink Adventurer Cape", "Apparel listing in the MTS catalog pane.", "MTS Clerk", 15000000, false),
                CreateEntry("Ilbi Throwing-Stars", "Projectile listing to keep the pane scrollable.", "MTS Clerk", 6100000, false),
                CreateEntry("Bathrobe for Men", "Popular dex robe listing inside the scrollable MTS pane.", "MTS Clerk", 8700000, false)
            };
        }

        private static IEnumerable<AdminShopEntry> CreateUserEntries(AdminShopServiceMode mode)
        {
            if (mode == AdminShopServiceMode.CashShop)
            {
                return new[]
                {
                    CreateUserEntry("NX Outfit Bundle", "Preview of a user-side recommendation row surfaced next to the NPC catalog.", "FashionMuse", 5200),
                    CreateUserEntry("Pet Accessory Package", "User listing used to test trade-request submission from the secondary pane.", "PetCrafter", 2400),
                    CreateUserEntry("Chair Showcase", "Decorative listing for mixed cosmetic browsing.", "ChairMerchant", 1800),
                    CreateUserEntry("Android Coupon Pack", "Secondary-pane listing for user catalog parity.", "AndroidDealer", 4100),
                    CreateUserEntry("Damage Skin Coupon", "Cash-market listing with its own seller label and request target.", "SkinBroker", 2700),
                    CreateUserEntry("Label Ring Pair", "Small cosmetic listing that keeps the right pane scrollable.", "RingSeller", 900),
                    CreateUserEntry("Megaphone Stack", "Bulk utility listing staged for user-row browsing.", "WorldShout", 600)
                };
            }

            return new[]
            {
                CreateUserEntry("Dragon Khanjar", "Player-listed equipment sale with a direct trade-request seam.", "NightLancer", 11200000),
                CreateUserEntry("PAC 4 ATT", "Popular cape listing to stress-test page movement.", "WindDeal", 34500000),
                CreateUserEntry("Pink Gaia Cape", "Secondary-pane seller row for MTS browsing parity.", "CapeShop", 9100000),
                CreateUserEntry("Dep Star", "Accessory listing used to test selecting user rows before sending a request.", "StarFinder", 8600000),
                CreateUserEntry("Crystal Ilbis", "Projectile listing with high-price formatting.", "ThrowKing", 25500000),
                CreateUserEntry("Brown Bamboo Hat", "Lower-tier listing that still participates in request flow.", "OldSchooler", 2800000),
                CreateUserEntry("Blue Anel Cape", "Additional listing to force scrollbar use.", "CapeCollector", 6400000)
            };
        }

        private static AdminShopEntry CreateEntry(string title, string detail, string seller, long price, bool supportsWishlist)
        {
            return new AdminShopEntry
            {
                Title = title,
                Detail = detail,
                Seller = seller,
                Price = price,
                PriceLabel = FormatPriceLabel(price),
                SupportsWishlist = supportsWishlist
            };
        }

        private static AdminShopEntry CreateUserEntry(string title, string detail, string seller, long price)
        {
            return new AdminShopEntry
            {
                Title = title,
                Detail = detail,
                Seller = seller,
                Price = price,
                PriceLabel = FormatPriceLabel(price),
                SupportsWishlist = false
            };
        }

        private static string FormatPriceLabel(long price)
        {
            return price.ToString("N0", CultureInfo.InvariantCulture) + " mesos";
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
