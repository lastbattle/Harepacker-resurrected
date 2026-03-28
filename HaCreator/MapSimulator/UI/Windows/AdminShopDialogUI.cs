using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzProperties;
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

        private enum AdminShopEntryState
        {
            Available,
            SoldOut,
            PreviewOnly,
            PendingResponse,
            RequestAccepted,
            RequestRejected
        }

        private enum AdminShopResponse
        {
            None,
            GrantItem,
            ListingSoldOut,
            SellerUnavailable,
            ListingExpired,
            InventoryFull
        }

        private enum AdminShopCategory
        {
            All,
            Equip,
            Use,
            Setup,
            Etc,
            Cash,
            Recipe,
            Scroll,
            Special,
            Package,
            Button
        }

        private enum AdminShopBrowseMode
        {
            All,
            Most,
            Sell,
            Buy,
            Rebuy
        }

        private sealed class AdminShopEntry
        {
            public string Title { get; init; } = string.Empty;
            public string Detail { get; init; } = string.Empty;
            public string Seller { get; init; } = string.Empty;
            public string PriceLabel { get; set; } = string.Empty;
            public long Price { get; set; }
            public AdminShopCategory Category { get; init; }
            public Texture2D IconTexture { get; set; }
            public bool SupportsWishlist { get; init; }
            public bool Wishlisted { get; set; }
            public AdminShopEntryState State { get; set; }
            public string StateLabel { get; set; } = string.Empty;
            public bool IsStorageExpansion { get; init; }
            public InventoryType InventoryExpansionType { get; init; } = InventoryType.NONE;
            public InventoryType RewardInventoryType { get; init; } = InventoryType.NONE;
            public int RewardItemId { get; init; }
            public int RewardQuantity { get; set; } = 1;
            public bool ConsumeOnSuccess { get; init; } = true;
            public bool LockAfterSuccess { get; init; }
            public AdminShopResponse Response { get; init; }
            public string ResponseMessage { get; init; } = string.Empty;
            public bool Featured { get; init; }
            public bool WasPurchased { get; set; }
            public int CommoditySerialNumber { get; set; }
            public bool CommodityOnSale { get; set; }
        }

        private sealed class AdminShopPaneState
        {
            public List<AdminShopEntry> SourceEntries { get; } = new();
            public List<AdminShopEntry> Entries { get; } = new();
            public int SelectedIndex { get; set; } = -1;
            public int ScrollOffset { get; set; }
        }

        private sealed class AdminShopEntrySessionState
        {
            public AdminShopEntryState State { get; init; }
            public string StateLabel { get; init; } = string.Empty;
        }

        private sealed class AdminShopTabVisual
        {
            public Texture2D EnabledTexture { get; set; }
            public Texture2D DisabledTexture { get; set; }
            public Point Offset { get; set; }
            public string Label { get; set; } = string.Empty;
        }

        private sealed class AdminShopCommodityData
        {
            public int SerialNumber { get; init; }
            public int ItemId { get; init; }
            public int Count { get; init; } = 1;
            public long Price { get; init; }
            public int Priority { get; init; }
            public int PeriodDays { get; init; }
            public bool OnSale { get; init; }
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
        private const int RowIconX = 4;
        private const int RowIconY = 1;
        private const int RowIconSize = 32;
        private const int RowTextX = 40;
        private const int RowTitleY = 8;
        private const int RowDetailY = 22;
        private const int DetailX = 18;
        private const int DetailY = 278;
        private const int DetailIconSize = 32;
        private const int DetailTextOffsetX = 40;
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
        // `TabShop` canvases do not encode per-tab placement; the client positions this strip via `CAdminShopDlg::OnCreate`.
        private const int CategoryTabStartX = 95;
        private const int CategoryTabStartY = 222;
        private const int CategoryTabColumns = 5;
        private const int CategoryTabStrideX = 42;
        private const int CategoryTabStrideY = 19;
        private static readonly object CommodityCacheLock = new();
        private static Dictionary<int, AdminShopCommodityData> _bestCommodityByItemId;
        private static Dictionary<int, AdminShopCommodityData> _commodityBySerialNumber;
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
        private readonly AdminShopTabVisual[] _browseTabs = new AdminShopTabVisual[5];
        private readonly AdminShopTabVisual[] _mtsBrowseTabs = new AdminShopTabVisual[4];
        private readonly AdminShopTabVisual[] _quickCategoryTabs = new AdminShopTabVisual[5];
        private readonly AdminShopTabVisual[] _categoryTabs = new AdminShopTabVisual[10];
        private readonly Texture2D _modalTexture;
        private readonly UIObject _modalConfirmButton;
        private readonly UIObject _modalCancelButton;
        private readonly GraphicsDevice _device;
        private readonly Dictionary<int, Texture2D> _itemIconCache = new();
        private readonly Dictionary<AdminShopServiceMode, HashSet<string>> _wishlistedEntryKeys = new()
        {
            [AdminShopServiceMode.CashShop] = new HashSet<string>(StringComparer.Ordinal),
            [AdminShopServiceMode.Mts] = new HashSet<string>(StringComparer.Ordinal)
        };
        private readonly Dictionary<AdminShopServiceMode, HashSet<string>> _purchasedEntryKeys = new()
        {
            [AdminShopServiceMode.CashShop] = new HashSet<string>(StringComparer.Ordinal),
            [AdminShopServiceMode.Mts] = new HashSet<string>(StringComparer.Ordinal)
        };
        private readonly Dictionary<AdminShopServiceMode, Dictionary<string, AdminShopEntrySessionState>> _entrySessionStates = new()
        {
            [AdminShopServiceMode.CashShop] = new Dictionary<string, AdminShopEntrySessionState>(StringComparer.Ordinal),
            [AdminShopServiceMode.Mts] = new Dictionary<string, AdminShopEntrySessionState>(StringComparer.Ordinal)
        };

        private IInventoryRuntime _inventory;
        private IStorageRuntime _storageRuntime;
        private SpriteFont _font;
        private AdminShopServiceMode _currentMode;
        private AdminShopPane _activePane = AdminShopPane.Npc;
        private AdminShopCategory _activeCategory = AdminShopCategory.All;
        private AdminShopBrowseMode _activeBrowseMode = AdminShopBrowseMode.All;
        private string _footerMessage = string.Empty;
        private string _modalMessage = string.Empty;
        private AdminShopEntry _pendingWishlistEntry;
        private AdminShopEntry _pendingRequestEntry;
        private int _previousScrollWheelValue;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private AdminShopPane? _draggingScrollPane;
        private int _scrollThumbDragOffsetY;
        private bool _wishlistModalVisible;
        private int _requestResolveTick;

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
            _device = device;
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

        public void SetInventory(IInventoryRuntime inventory)
        {
            _inventory = inventory;
            Money = _inventory?.GetMesoCount() ?? Money;
            UpdateActionButtonStates();
        }

        public void SetStorageRuntime(IStorageRuntime storageRuntime)
        {
            _storageRuntime = storageRuntime;
            UpdateActionButtonStates();
        }

        public override void Show()
        {
            base.Show();
            MouseState mouseState = Mouse.GetState();
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
            _previousMouseState = mouseState;
            _previousKeyboardState = Keyboard.GetState();
            ResetMode(_defaultMode);
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public bool TryFocusCommoditySerialNumber(int commoditySerialNumber)
        {
            if (_currentMode != AdminShopServiceMode.CashShop || commoditySerialNumber <= 0)
            {
                return false;
            }

            if (!TryGetCommodityBySerialNumber(commoditySerialNumber, out AdminShopCommodityData commodity))
            {
                _footerMessage = $"Commodity SN {commoditySerialNumber} does not resolve to a loaded Cash Shop sample row.";
                UpdateActionButtonStates();
                return false;
            }

            AdminShopEntry matchedEntry = FindCommodityEntry(commodity);
            if (matchedEntry == null)
            {
                _footerMessage = $"Commodity SN {commoditySerialNumber} resolved to item {commodity.ItemId}, but no matching sample row is present in this simulator dialog.";
                UpdateActionButtonStates();
                return false;
            }

            _activeBrowseMode = AdminShopBrowseMode.All;
            _activeCategory = ResolveCommodityCategory(matchedEntry);
            ApplyFilters();

            AdminShopPaneState paneState = _paneStates[AdminShopPane.Npc];
            int selectedIndex = paneState.Entries.IndexOf(matchedEntry);
            if (selectedIndex < 0)
            {
                _activeCategory = AdminShopCategory.All;
                ApplyFilters();
                paneState = _paneStates[AdminShopPane.Npc];
                selectedIndex = paneState.Entries.IndexOf(matchedEntry);
            }

            if (selectedIndex < 0)
            {
                _footerMessage = $"Commodity SN {commoditySerialNumber} resolved, but the row could not be focused after filter rebuild.";
                UpdateActionButtonStates();
                return false;
            }

            _activePane = AdminShopPane.Npc;
            paneState.SelectedIndex = selectedIndex;
            ClampPaneState(paneState);
            _pendingWishlistEntry = null;
            _footerMessage = $"Focused packet-owned commodity SN {commoditySerialNumber} on {matchedEntry.Title}.";
            UpdateRowButtons();
            UpdateActionButtonStates();
            return true;
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible)
            {
                return;
            }

            if (_inventory != null)
            {
                Money = _inventory.GetMesoCount();
            }

            if (_pendingRequestEntry != null && Environment.TickCount >= _requestResolveTick)
            {
                ResolvePendingRequest();
            }

            MouseState mouseState = Mouse.GetState();
            int wheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
            KeyboardState keyboardState = Keyboard.GetState();
            HandleKeyboardInput(keyboardState);
            if (!IsVisible)
            {
                _previousMouseState = mouseState;
                _previousKeyboardState = keyboardState;
                return;
            }

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;

            if (_wishlistModalVisible)
            {
                _previousMouseState = mouseState;
                _previousKeyboardState = keyboardState;
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
                if (TryHandleCategoryTabClick(mouseState) || TryHandleBrowseTabClick(mouseState) || TryHandleQuickCategoryTabClick(mouseState) || TryHandleScrollBarMouseDown(mouseState))
                {
                    _previousMouseState = mouseState;
                    _previousKeyboardState = keyboardState;
                    return;
                }
            }

            if (wheelDelta == 0)
            {
                _previousMouseState = mouseState;
                _previousKeyboardState = keyboardState;
                return;
            }

            AdminShopPane? hoveredPane = GetPaneAt(mouseState.X, mouseState.Y);
            if (!hoveredPane.HasValue)
            {
                _previousMouseState = mouseState;
                _previousKeyboardState = keyboardState;
                return;
            }

            AdminShopPaneState paneState = _paneStates[hoveredPane.Value];
            if (paneState.Entries.Count <= MaxVisibleRows)
            {
                _previousMouseState = mouseState;
                _previousKeyboardState = keyboardState;
                return;
            }

            paneState.ScrollOffset += wheelDelta > 0 ? -1 : 1;
            ClampPaneState(paneState);
            UpdateRowButtons();
            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            if (_wishlistModalVisible)
            {
                if (WasPressed(keyboardState, Keys.Enter) || WasPressed(keyboardState, Keys.Space))
                {
                    OnModalConfirmClicked(_modalConfirmButton);
                }
                else if (WasPressed(keyboardState, Keys.Escape))
                {
                    OnModalCancelClicked(_modalCancelButton);
                }

                return;
            }

            if (WasPressed(keyboardState, Keys.Escape))
            {
                Hide();
                return;
            }

            if (WasPressed(keyboardState, Keys.Tab) || WasPressed(keyboardState, Keys.Left) || WasPressed(keyboardState, Keys.Right))
            {
                SwitchActivePane();
            }

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
                MoveSelection(-MaxVisibleRows);
            }
            else if (WasPressed(keyboardState, Keys.PageDown))
            {
                MoveSelection(MaxVisibleRows);
            }
            else if (WasPressed(keyboardState, Keys.Home))
            {
                SelectAbsoluteIndex(0);
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                SelectAbsoluteIndex(_paneStates[_activePane].Entries.Count - 1);
            }

            if (WasPressed(keyboardState, Keys.Enter))
            {
                OnBuyButtonClicked(_buyButton);
            }
            else if (WasPressed(keyboardState, Keys.Space) && GetSelectedEntry()?.SupportsWishlist == true)
            {
                OnRechargeButtonClicked(_rechargeButton);
            }
        }

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private void SwitchActivePane()
        {
            AdminShopPane nextPane = _activePane == AdminShopPane.Npc ? AdminShopPane.User : AdminShopPane.Npc;
            if (_paneStates[nextPane].Entries.Count == 0)
            {
                return;
            }

            _activePane = nextPane;
            AdminShopPaneState paneState = _paneStates[_activePane];
            if (paneState.SelectedIndex < 0 && paneState.Entries.Count > 0)
            {
                paneState.SelectedIndex = 0;
            }

            ClampPaneState(paneState);
            _pendingWishlistEntry = null;
            _footerMessage = BuildSelectionMessage(GetSelectedEntry(), _activePane);
            UpdateActionButtonStates();
        }

        private void MoveSelection(int delta)
        {
            AdminShopPaneState paneState = _paneStates[_activePane];
            if (paneState.Entries.Count == 0)
            {
                return;
            }

            int targetIndex = paneState.SelectedIndex >= 0 ? paneState.SelectedIndex + delta : 0;
            SelectAbsoluteIndex(targetIndex);
        }

        private void SelectAbsoluteIndex(int index)
        {
            AdminShopPaneState paneState = _paneStates[_activePane];
            if (paneState.Entries.Count == 0)
            {
                paneState.SelectedIndex = -1;
                _footerMessage = BuildSelectionMessage(null, _activePane);
                UpdateActionButtonStates();
                return;
            }

            paneState.SelectedIndex = Math.Clamp(index, 0, paneState.Entries.Count - 1);
            _pendingWishlistEntry = null;
            ClampPaneState(paneState);
            _footerMessage = BuildSelectionMessage(paneState.Entries[paneState.SelectedIndex], _activePane);
            UpdateActionButtonStates();
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
            DrawCategoryTabs(sprite, windowX, windowY);
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
            string instruction = BuildHeaderInstruction();

            sprite.DrawString(_font, modeLabel + " dialog", new Vector2(windowX + HeaderX, windowY + HeaderY), Color.White);
            sprite.DrawString(_font, instruction, new Vector2(windowX + HeaderX, windowY + HeaderY + 18), new Color(215, 215, 215));
        }

        private void DrawCategoryTabs(SpriteBatch sprite, int windowX, int windowY)
        {
            for (int i = 0; i < _categoryTabs.Length; i++)
            {
                DrawTab(sprite, windowX, windowY, _categoryTabs[i], _activeCategory == GetFullCategory(i), ServiceTabTextY);
            }
        }

        private void DrawTabs(SpriteBatch sprite, int windowX, int windowY)
        {
            IReadOnlyList<AdminShopTabVisual> browseTabs = GetBrowseTabsForCurrentMode();
            for (int i = 0; i < browseTabs.Count; i++)
            {
                DrawTab(sprite, windowX, windowY, browseTabs[i], _activeBrowseMode == (AdminShopBrowseMode)i, ServiceTabTextY);
            }

            for (int i = 0; i < _quickCategoryTabs.Length; i++)
            {
                DrawTab(sprite, windowX, windowY, _quickCategoryTabs[i], _activeCategory == GetQuickCategory(i), PaneTabTextY);
            }
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

            if (_font == null || string.IsNullOrWhiteSpace(tab.Label) || texture != null)
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

                DrawEntryIcon(sprite, entry, rowX + RowIconX, rowY + RowIconY, RowIconSize);
                Color titleColor = GetTitleColor(entry, isSelected);
                Color detailColor = GetDetailColor(entry, isSelected);
                sprite.DrawString(_font, TrimToWidth(entry.Title, 104f), new Vector2(rowX + RowTextX, rowY + RowTitleY), titleColor);
                sprite.DrawString(_font, TrimToWidth(entry.PriceLabel, 104f), new Vector2(rowX + RowTextX, rowY + RowDetailY), detailColor);

                if (!string.IsNullOrWhiteSpace(entry.StateLabel))
                {
                    string stateLabel = TrimToWidth(entry.StateLabel, 58f);
                    Vector2 stateSize = _font.MeasureString(stateLabel);
                    Vector2 statePosition = new Vector2(rowX + PaneWidth - stateSize.X - 8f, rowY + RowTitleY);
                    sprite.DrawString(_font, stateLabel, statePosition, GetStateColor(entry, isSelected));
                }
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

            bool hasIcon = entry.IconTexture != null;
            if (hasIcon)
            {
                DrawEntryIcon(sprite, entry, windowX + DetailX, windowY + DetailY, DetailIconSize);
            }

            int detailTextX = windowX + DetailX + (hasIcon ? DetailTextOffsetX : 0);
            sprite.DrawString(_font, entry.Title, new Vector2(detailTextX, windowY + DetailY), Color.White);
            sprite.DrawString(_font, $"{entry.Seller}  |  {entry.PriceLabel}", new Vector2(detailTextX, windowY + DetailY + 18), new Color(235, 224, 164));
            sprite.DrawString(_font, BuildEntryStateText(entry), new Vector2(detailTextX, windowY + DetailY + 36), GetStateColor(entry, false));

            float detailY = windowY + DetailY + 54;
            foreach (string line in WrapText(entry.Detail, 400f))
            {
                sprite.DrawString(_font, line, new Vector2(windowX + DetailX, detailY), new Color(218, 218, 218));
                detailY += 16f;
            }

            string wishState = entry.Wishlisted ? "Wish list: saved." : "Wish list: not saved.";
            sprite.DrawString(_font, wishState, new Vector2(windowX + DetailX, windowY + DetailY + 70), new Color(175, 220, 175));

            if (!string.IsNullOrWhiteSpace(_footerMessage))
            {
                sprite.DrawString(_font, _footerMessage, new Vector2(windowX + DetailX, windowY + DetailY + 86), new Color(255, 221, 143));
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
            SubmitSelectedEntryRequest();
        }

        private void OnSellButtonClicked(UIObject sender)
        {
            SubmitSelectedEntryRequest();
        }

        private void SubmitSelectedEntryRequest()
        {
            AdminShopEntry entry = GetSelectedEntry();
            if (entry == null)
            {
                _footerMessage = "Select an offer before sending a request.";
                UpdateActionButtonStates();
                return;
            }

            if (!CanRequestEntry(entry))
            {
                _footerMessage = BuildBlockedRequestMessage(entry);
                UpdateActionButtonStates();
                return;
            }

            _pendingWishlistEntry = null;
            _pendingRequestEntry = entry;
            _requestResolveTick = Environment.TickCount + 900;
            entry.State = AdminShopEntryState.PendingResponse;
            entry.StateLabel = "Pending";
            _footerMessage = $"Submitted a {_currentMode} request for {entry.Title}. Waiting for simulator response.";
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
            _activeCategory = AdminShopCategory.All;
            _activeBrowseMode = AdminShopBrowseMode.All;
            _pendingWishlistEntry = null;
            _pendingRequestEntry = null;
            _wishlistModalVisible = false;
            _paneStates[AdminShopPane.Npc].SourceEntries.Clear();
            _paneStates[AdminShopPane.User].SourceEntries.Clear();
            _paneStates[AdminShopPane.Npc].SourceEntries.AddRange(CreateNpcEntries(mode));
            _paneStates[AdminShopPane.User].SourceEntries.AddRange(CreateUserEntries(mode));
            ApplyCommodityMetadata(_paneStates[AdminShopPane.Npc].SourceEntries, mode);
            RestoreEntryFlags(_paneStates[AdminShopPane.Npc].SourceEntries, mode);
            RestoreEntryFlags(_paneStates[AdminShopPane.User].SourceEntries, mode);
            RestoreEntryStates(_paneStates[AdminShopPane.Npc].SourceEntries, mode);
            RestoreEntryStates(_paneStates[AdminShopPane.User].SourceEntries, mode);
            PopulateEntryIcons(_paneStates[AdminShopPane.Npc].SourceEntries);
            PopulateEntryIcons(_paneStates[AdminShopPane.User].SourceEntries);
            ApplyFilters();
            _footerMessage = BuildSelectionMessage(GetSelectedEntry(), _activePane);
            UpdateRowButtons();
            UpdateActionButtonStates();
            UpdateModalButtons();
        }

        private void ApplyFilters()
        {
            foreach (AdminShopPane pane in Enum.GetValues(typeof(AdminShopPane)))
            {
                AdminShopPaneState paneState = _paneStates[pane];
                AdminShopEntry selectedEntry = paneState.SelectedIndex >= 0 && paneState.SelectedIndex < paneState.Entries.Count
                    ? paneState.Entries[paneState.SelectedIndex]
                    : null;

                paneState.Entries.Clear();
                foreach (AdminShopEntry entry in paneState.SourceEntries)
                {
                    if (ShouldIncludeEntry(entry, _activeCategory, _activeBrowseMode, pane))
                    {
                        paneState.Entries.Add(entry);
                    }
                }

                paneState.ScrollOffset = 0;
                paneState.SelectedIndex = selectedEntry != null ? paneState.Entries.IndexOf(selectedEntry) : -1;
                if (paneState.SelectedIndex < 0 && paneState.Entries.Count > 0)
                {
                    paneState.SelectedIndex = 0;
                }

                ClampPaneState(paneState);
            }

            if (_paneStates[_activePane].Entries.Count == 0)
            {
                if (_paneStates[AdminShopPane.Npc].Entries.Count > 0)
                {
                    _activePane = AdminShopPane.Npc;
                }
                else if (_paneStates[AdminShopPane.User].Entries.Count > 0)
                {
                    _activePane = AdminShopPane.User;
                }
            }
        }

        private void UpdateRowButtons()
        {
            UpdateRowButtonsForPane(_npcRowButtons, _paneStates[AdminShopPane.Npc]);
            UpdateRowButtonsForPane(_userRowButtons, _paneStates[AdminShopPane.User]);
        }

        private void PopulateEntryIcons(IEnumerable<AdminShopEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (AdminShopEntry entry in entries)
            {
                entry.IconTexture = ResolveEntryIcon(entry);
            }
        }

        private Texture2D ResolveEntryIcon(AdminShopEntry entry)
        {
            if (entry == null || entry.RewardItemId <= 0)
            {
                return null;
            }

            if (_inventory != null && entry.RewardInventoryType != InventoryType.NONE)
            {
                Texture2D inventoryTexture = _inventory.GetItemTexture(entry.RewardInventoryType, entry.RewardItemId);
                if (inventoryTexture != null)
                {
                    return inventoryTexture;
                }
            }

            if (_itemIconCache.TryGetValue(entry.RewardItemId, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D loadedTexture = LoadItemIconTexture(entry.RewardItemId);
            if (loadedTexture != null)
            {
                _itemIconCache[entry.RewardItemId] = loadedTexture;
            }

            return loadedTexture;
        }

        private Texture2D LoadItemIconTexture(int itemId)
        {
            if (_device == null || itemId <= 0 || !InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            string itemText = string.Equals(category, "Character", StringComparison.OrdinalIgnoreCase)
                ? itemId.ToString("D8", CultureInfo.InvariantCulture)
                : itemId.ToString("D7", CultureInfo.InvariantCulture);
            WzSubProperty infoProperty = (itemImage[itemText] as WzSubProperty)?["info"] as WzSubProperty;
            WzCanvasProperty iconCanvas = infoProperty?["iconRaw"] as WzCanvasProperty
                                          ?? infoProperty?["icon"] as WzCanvasProperty;
            return iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_device);
        }

        private void DrawEntryIcon(SpriteBatch sprite, AdminShopEntry entry, int x, int y, int size)
        {
            if (entry?.IconTexture == null)
            {
                return;
            }

            Rectangle destination = new Rectangle(x, y, size, size);
            sprite.Draw(entry.IconTexture, destination, Color.White);

            if (entry.RewardQuantity <= 1)
            {
                return;
            }

            string quantityText = $"x{entry.RewardQuantity}";
            Vector2 quantitySize = _font.MeasureString(quantityText);
            sprite.DrawString(
                _font,
                quantityText,
                new Vector2(destination.Right - quantitySize.X, destination.Bottom - quantitySize.Y - 1f),
                new Color(255, 235, 169));
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
            bool canSubmitRequest = !modalBlocked && entry != null && CanRequestEntry(entry);
            _buyButton?.SetEnabled(canSubmitRequest);
            _sellButton?.SetEnabled(canSubmitRequest);
            _exitButton?.SetEnabled(!modalBlocked);
            _rechargeButton?.SetEnabled(!modalBlocked && entry?.SupportsWishlist == true && entry.State == AdminShopEntryState.Available);
        }

        private void ApplyCommodityMetadata(IEnumerable<AdminShopEntry> entries, AdminShopServiceMode mode)
        {
            if (mode != AdminShopServiceMode.CashShop || entries == null)
            {
                return;
            }

            EnsureCommodityCache();
            foreach (AdminShopEntry entry in entries)
            {
                if (entry == null
                    || entry.IsStorageExpansion
                    || entry.InventoryExpansionType != InventoryType.NONE
                    || entry.RewardItemId <= 0
                    || !TryGetBestCommodityForItem(entry.RewardItemId, out AdminShopCommodityData commodity))
                {
                    continue;
                }

                entry.Price = commodity.Price;
                entry.PriceLabel = FormatPriceLabel(commodity.Price);
                entry.RewardQuantity = Math.Max(1, commodity.Count);
                entry.CommoditySerialNumber = commodity.SerialNumber;
                entry.CommodityOnSale = commodity.OnSale;
            }
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
            Point[] browseOffsets =
            {
                new Point(10, 91),
                new Point(53, 91),
                new Point(10, 91),
                new Point(53, 91),
                new Point(53, 91)
            };
            string[] browseLabels = { "ALL", "MOST", "SELL", "BUY", "RE-BUY" };
            for (int i = 0; i < _browseTabs.Length; i++)
            {
                _browseTabs[i] = new AdminShopTabVisual
                {
                    Label = browseLabels[i],
                    Offset = browseOffsets[i]
                };
            }

            string[] mtsBrowseLabels = { "ALL", "MOST", "SELL", "BUY" };
            for (int i = 0; i < _mtsBrowseTabs.Length; i++)
            {
                _mtsBrowseTabs[i] = new AdminShopTabVisual
                {
                    Label = mtsBrowseLabels[i],
                    Offset = browseOffsets[Math.Min(i, browseOffsets.Length - 1)]
                };
            }

            Point[] quickCategoryOffsets =
            {
                new Point(241, 91),
                new Point(284, 91),
                new Point(327, 91),
                new Point(370, 91),
                new Point(413, 91)
            };
            string[] quickCategoryLabels = { "Equip", "Use", "Etc", "Set-up", "Cash" };
            for (int i = 0; i < _quickCategoryTabs.Length; i++)
            {
                _quickCategoryTabs[i] = new AdminShopTabVisual
                {
                    Label = quickCategoryLabels[i],
                    Offset = quickCategoryOffsets[i]
                };
            }

            for (int i = 0; i < _categoryTabs.Length; i++)
            {
                _categoryTabs[i] = new AdminShopTabVisual
                {
                    Label = GetCategoryLabel(GetFullCategory(i)),
                    Offset = GetDefaultCategoryTabOffset(i)
                };
            }
        }

        public void SetBrowseTabTextures(
            Texture2D[] enabledTextures,
            Texture2D[] disabledTextures,
            Point[] offsets = null)
        {
            SetTabTextures(_browseTabs, enabledTextures, disabledTextures, offsets);
        }

        public void SetMtsBrowseTabTextures(
            Texture2D[] enabledTextures,
            Texture2D[] disabledTextures,
            Point[] offsets = null)
        {
            SetTabTextures(_mtsBrowseTabs, enabledTextures, disabledTextures, offsets);
        }

        public void SetQuickCategoryTabTextures(
            Texture2D[] enabledTextures,
            Texture2D[] disabledTextures,
            Point[] offsets = null)
        {
            SetTabTextures(_quickCategoryTabs, enabledTextures, disabledTextures, offsets);
        }

        public void SetCategoryTabTextures(
            Texture2D[] enabledTextures,
            Texture2D[] disabledTextures,
            Point[] offsets = null)
        {
            SetTabTextures(_categoryTabs, enabledTextures, disabledTextures, HasMeaningfulCategoryOffsets(offsets) ? offsets : null);
        }

        private static void SetTabTextures(
            IReadOnlyList<AdminShopTabVisual> tabs,
            Texture2D[] enabledTextures,
            Texture2D[] disabledTextures,
            Point[] offsets)
        {
            if (tabs == null || enabledTextures == null || disabledTextures == null)
            {
                return;
            }

            int count = Math.Min(tabs.Count, Math.Min(enabledTextures.Length, disabledTextures.Length));
            for (int i = 0; i < count; i++)
            {
                tabs[i].EnabledTexture = enabledTextures[i];
                tabs[i].DisabledTexture = disabledTextures[i];
                if (offsets != null && i < offsets.Length)
                {
                    tabs[i].Offset = offsets[i];
                }
            }
        }

        private static bool HasMeaningfulCategoryOffsets(Point[] offsets)
        {
            if (offsets == null)
            {
                return false;
            }

            foreach (Point offset in offsets)
            {
                if (offset != Point.Zero)
                {
                    return true;
                }
            }

            return false;
        }

        private static Point GetDefaultCategoryTabOffset(int tabIndex)
        {
            int column = tabIndex % CategoryTabColumns;
            int row = tabIndex / CategoryTabColumns;
            return new Point(
                CategoryTabStartX + (column * CategoryTabStrideX),
                CategoryTabStartY + (row * CategoryTabStrideY));
        }

        private bool TryHandleBrowseTabClick(MouseState mouseState)
        {
            IReadOnlyList<AdminShopTabVisual> browseTabs = GetBrowseTabsForCurrentMode();
            for (int i = 0; i < browseTabs.Count; i++)
            {
                if (!GetTabBounds(browseTabs[i]).Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                AdminShopBrowseMode browseMode = (AdminShopBrowseMode)i;
                if (_activeBrowseMode != browseMode)
                {
                    _activeBrowseMode = browseMode;
                    ApplyFilters();
                    if (browseMode == AdminShopBrowseMode.Sell)
                    {
                        _activePane = AdminShopPane.User;
                    }
                    else if (browseMode == AdminShopBrowseMode.Buy)
                    {
                        _activePane = AdminShopPane.Npc;
                    }

                    _footerMessage = BuildBrowseModeMessage(browseMode);
                    UpdateRowButtons();
                    UpdateActionButtonStates();
                }

                return true;
            }

            return false;
        }

        private IReadOnlyList<AdminShopTabVisual> GetBrowseTabsForCurrentMode()
        {
            if (_currentMode != AdminShopServiceMode.Mts)
            {
                return _browseTabs;
            }

            bool hasModeSpecificTextures = false;
            for (int i = 0; i < _mtsBrowseTabs.Length; i++)
            {
                if (_mtsBrowseTabs[i]?.EnabledTexture != null || _mtsBrowseTabs[i]?.DisabledTexture != null)
                {
                    hasModeSpecificTextures = true;
                    break;
                }
            }

            if (hasModeSpecificTextures)
            {
                return _mtsBrowseTabs;
            }

            return Array.AsReadOnly(_browseTabs[..4]);
        }

        private bool TryHandleCategoryTabClick(MouseState mouseState)
        {
            for (int i = 0; i < _categoryTabs.Length; i++)
            {
                if (!GetTabBounds(_categoryTabs[i]).Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                AdminShopCategory selectedCategory = GetFullCategory(i);
                if (_activeCategory != selectedCategory)
                {
                    _activeCategory = selectedCategory;
                    ApplyFilters();
                    _footerMessage = $"Filtered {_currentMode} catalog to {GetCategoryLabel(selectedCategory)} items.";
                    UpdateRowButtons();
                    UpdateActionButtonStates();
                }

                return true;
            }

            return false;
        }

        private bool TryHandleQuickCategoryTabClick(MouseState mouseState)
        {
            for (int i = 0; i < _quickCategoryTabs.Length; i++)
            {
                if (!GetTabBounds(_quickCategoryTabs[i]).Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                AdminShopCategory category = GetQuickCategory(i);
                if (_activeCategory != category)
                {
                    _activeCategory = category;
                    ApplyFilters();
                    _footerMessage = $"Focused {_currentMode} catalog on {GetCategoryLabel(category)} entries.";
                    UpdateRowButtons();
                    UpdateActionButtonStates();
                }

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
            _wishlistedEntryKeys[_currentMode].Add(GetEntryKey(_pendingWishlistEntry));
            PersistEntrySessionState(_pendingWishlistEntry);
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
            return $"Selected {paneLabel}: {entry.Title} ({GetCategoryLabel(entry.Category)}). {BuildEntryStateText(entry)}";
        }

        private static IEnumerable<AdminShopEntry> CreateNpcEntries(AdminShopServiceMode mode)
        {
            if (mode == AdminShopServiceMode.CashShop)
            {
                return new[]
                {
                    CreateInventoryExpansionEntry("Extending Equip Inventory", InventoryType.EQUIP, "Cash Manager"),
                    CreateInventoryExpansionEntry("Extending Use Inventory", InventoryType.USE, "Cash Manager"),
                    CreateInventoryExpansionEntry("Extending Set-Up Inventory", InventoryType.SETUP, "Cash Manager"),
                    CreateInventoryExpansionEntry("Extending Etc. Inventory", InventoryType.ETC, "Cash Manager"),
                    CreateItemEntry("Royal Hair Coupon", "Rotating salon coupon preview from the featured cash-service board.", "Cash Manager", 3400, AdminShopCategory.Special, true, InventoryType.CASH, 5050000, featured: true),
                    CreateItemEntry("Royal Face Coupon", "Premium face coupon entry with the same preview flow the client routes into wish-list dialogs.", "Cash Manager", 2900, AdminShopCategory.Special, true, InventoryType.CASH, 5150040, featured: true),
                    CreateItemEntry("Pet Snack Bundle", "Utility bundle with snack and pet-tag support for multi-pet sessions.", "Cash Manager", 1900, AdminShopCategory.Use, true, InventoryType.CASH, 2120000, 3, featured: true),
                    CreateStorageExpansionEntry("Storage Slot Expansion", "Convenience service for storage and shared account inventory capacity.", "Cash Manager"),
                    CreateItemEntry("Hyper Teleport Rock", "Navigation-heavy service entry used to compare convenience bundles.", "Cash Manager", 900, AdminShopCategory.Use, true, InventoryType.CASH, 5040004),
                    CreateItemEntry("Surprise Style Box", "Random cosmetic box surfaced through the featured rotation.", "Cash Manager", 3400, AdminShopCategory.Package, true, InventoryType.CASH, 5222000, featured: true),
                    CreateItemEntry("Cosmetic Lens Coupon", "Style utility item staged for wish-list confirmation tests.", "Cash Manager", 1600, AdminShopCategory.Button, true, InventoryType.CASH, 5152057),
                    CreateEntry("Pet Equip Bundle", "Pet equipment and accessory bundle bound to the NPC-side catalog.", "Cash Manager", 2200, AdminShopCategory.Equip, true, featured: true, state: AdminShopEntryState.SoldOut, stateLabel: "Sold out")
                };
            }

            return new[]
            {
                CreateItemEntry("Zakum Helmet Listing", "Admin MTS preview of a high-demand helmet sold from the NPC-owned catalog view.", "MTS Clerk", 12500000, AdminShopCategory.Equip, false, InventoryType.EQUIP, 1002357, featured: true, response: AdminShopResponse.ListingSoldOut, responseMessage: "The listing refreshed before the purchase could be confirmed."),
                CreateItemEntry("Maple Kandayo", "MTS equipment board seeded to exercise request submission and price display.", "MTS Clerk", 9800000, AdminShopCategory.Equip, false, InventoryType.EQUIP, 1332027, featured: true),
                CreateItemEntry("Steely Throwing-Knives", "Consumable trade board sample that mirrors a browse-first MTS flow.", "MTS Clerk", 3200000, AdminShopCategory.Use, false, InventoryType.USE, 2070005, 1, response: AdminShopResponse.InventoryFull, responseMessage: "The MTS clerk rejected delivery because the destination inventory tab is full."),
                CreateEntry("Chaos Scroll 60%", "Scroll listing preview staged for user-vs-NPC comparison.", "MTS Clerk", 21000000, AdminShopCategory.Scroll, false, state: AdminShopEntryState.SoldOut, stateLabel: "Sold out"),
                CreateItemEntry("Brown Work Gloves", "Common MTS browse row with seller and price labels only.", "MTS Clerk", 4700000, AdminShopCategory.Equip, false, InventoryType.EQUIP, 1082002),
                CreateItemEntry("Pink Adventurer Cape", "Apparel listing in the MTS catalog pane.", "MTS Clerk", 15000000, AdminShopCategory.Setup, false, InventoryType.EQUIP, 1102041, response: AdminShopResponse.SellerUnavailable, responseMessage: "The seller did not answer the trade relay request."),
                CreateItemEntry("Ilbi Throwing-Stars", "Projectile listing to keep the pane scrollable.", "MTS Clerk", 6100000, AdminShopCategory.Use, false, InventoryType.USE, 2070000),
                CreateEntry("Bathrobe for Men", "Popular dex robe listing inside the scrollable MTS pane.", "MTS Clerk", 8700000, AdminShopCategory.Equip, false, state: AdminShopEntryState.PreviewOnly, stateLabel: "Preview")
            };
        }

        private static IEnumerable<AdminShopEntry> CreateUserEntries(AdminShopServiceMode mode)
        {
            if (mode == AdminShopServiceMode.CashShop)
            {
                return new[]
                {
                    CreateUserItemEntry("NX Outfit Bundle", "Preview of a user-side recommendation row surfaced next to the NPC catalog.", "FashionMuse", 5200, AdminShopCategory.Equip, InventoryType.CASH, 1050101, featured: true, response: AdminShopResponse.SellerUnavailable, responseMessage: "The recommendation slot resolved to an offline seller profile."),
                    CreateUserItemEntry("Pet Accessory Package", "User listing used to test trade-request submission from the secondary pane.", "PetCrafter", 2400, AdminShopCategory.Equip, InventoryType.CASH, 1802000, 1, lockAfterSuccess: true),
                    CreateUserEntry("Chair Showcase", "Decorative listing for mixed cosmetic browsing.", "ChairMerchant", 1800, AdminShopCategory.Setup, state: AdminShopEntryState.PreviewOnly, stateLabel: "Preview"),
                    CreateUserItemEntry("Android Coupon Pack", "Secondary-pane listing for user catalog parity.", "AndroidDealer", 4100, AdminShopCategory.Special, InventoryType.CASH, 5680150, response: AdminShopResponse.ListingExpired, responseMessage: "The coupon pack expired before the simulator session confirmed delivery."),
                    CreateUserItemEntry("Damage Skin Coupon", "Cash-market listing with its own seller label and request target.", "SkinBroker", 2700, AdminShopCategory.Use, InventoryType.CASH, 2431965),
                    CreateUserItemEntry("Label Ring Pair", "Small cosmetic listing that keeps the right pane scrollable.", "RingSeller", 900, AdminShopCategory.Button, InventoryType.CASH, 1112900, 1, lockAfterSuccess: true),
                    CreateUserEntry("Megaphone Stack", "Bulk utility listing staged for user-row browsing.", "WorldShout", 600, AdminShopCategory.Etc, state: AdminShopEntryState.SoldOut, stateLabel: "Sold out")
                };
            }

            return new[]
            {
                CreateUserItemEntry("Dragon Khanjar", "Player-listed equipment sale with a direct trade-request seam.", "NightLancer", 11200000, AdminShopCategory.Equip, InventoryType.EQUIP, 1342008, 1, featured: true, lockAfterSuccess: true),
                CreateUserItemEntry("PAC 4 ATT", "Popular cape listing to stress-test page movement.", "WindDeal", 34500000, AdminShopCategory.Equip, InventoryType.EQUIP, 1102041, response: AdminShopResponse.ListingExpired, responseMessage: "The seller withdrew the cape before the handoff completed."),
                CreateUserItemEntry("Pink Gaia Cape", "Secondary-pane seller row for MTS browsing parity.", "CapeShop", 9100000, AdminShopCategory.Setup, InventoryType.EQUIP, 1102085),
                CreateUserItemEntry("Dep Star", "Accessory listing used to test selecting user rows before sending a request.", "StarFinder", 8600000, AdminShopCategory.Equip, InventoryType.EQUIP, 1122000, response: AdminShopResponse.SellerUnavailable, responseMessage: "The seller did not acknowledge the trade bridge."),
                CreateUserEntry("Crystal Ilbis", "Projectile listing with high-price formatting.", "ThrowKing", 25500000, AdminShopCategory.Use, state: AdminShopEntryState.SoldOut, stateLabel: "Sold out"),
                CreateUserItemEntry("Brown Bamboo Hat", "Lower-tier listing that still participates in request flow.", "OldSchooler", 2800000, AdminShopCategory.Equip, InventoryType.EQUIP, 1002019),
                CreateUserEntry("Blue Anel Cape", "Additional listing to force scrollbar use.", "CapeCollector", 6400000, AdminShopCategory.Setup, state: AdminShopEntryState.PreviewOnly, stateLabel: "Preview")
            };
        }

        private static AdminShopEntry CreateEntry(
            string title,
            string detail,
            string seller,
            long price,
            AdminShopCategory category,
            bool supportsWishlist,
            bool featured = false,
            AdminShopEntryState state = AdminShopEntryState.Available,
            string stateLabel = "")
        {
            return new AdminShopEntry
            {
                Title = title,
                Detail = detail,
                Seller = seller,
                Price = price,
                PriceLabel = FormatPriceLabel(price),
                Category = category,
                SupportsWishlist = supportsWishlist,
                Featured = featured,
                State = state,
                StateLabel = stateLabel
            };
        }

        private static AdminShopEntry CreateItemEntry(
            string title,
            string detail,
            string seller,
            long price,
            AdminShopCategory category,
            bool supportsWishlist,
            InventoryType rewardInventoryType,
            int rewardItemId,
            int rewardQuantity = 1,
            bool featured = false,
            bool lockAfterSuccess = false,
            AdminShopEntryState state = AdminShopEntryState.Available,
            string stateLabel = "",
            AdminShopResponse response = AdminShopResponse.GrantItem,
            string responseMessage = "")
        {
            return new AdminShopEntry
            {
                Title = title,
                Detail = detail,
                Seller = seller,
                Price = price,
                PriceLabel = FormatPriceLabel(price),
                Category = category,
                SupportsWishlist = supportsWishlist,
                Featured = featured,
                State = state,
                StateLabel = stateLabel,
                RewardInventoryType = rewardInventoryType,
                RewardItemId = rewardItemId,
                RewardQuantity = Math.Max(1, rewardQuantity),
                LockAfterSuccess = lockAfterSuccess,
                Response = response,
                ResponseMessage = responseMessage
            };
        }

        private static AdminShopEntry CreateInventoryExpansionEntry(string title, InventoryType inventoryType, string seller)
        {
            string inventoryLabel = inventoryType switch
            {
                InventoryType.EQUIP => "Equip",
                InventoryType.USE => "Use",
                InventoryType.SETUP => "Set-up",
                InventoryType.ETC => "Etc",
                InventoryType.CASH => "Cash",
                _ => inventoryType.ToString()
            };

            return new AdminShopEntry
            {
                Title = title,
                Detail = $"Adds 4 slots (1 row) to {inventoryLabel} Inventory.",
                Seller = seller,
                Price = 3800,
                PriceLabel = FormatPriceLabel(3800),
                Category = AdminShopCategory.Button,
                Featured = inventoryType == InventoryType.EQUIP || inventoryType == InventoryType.USE,
                SupportsWishlist = false,
                State = AdminShopEntryState.Available,
                InventoryExpansionType = inventoryType
            };
        }

        private static AdminShopEntry CreateStorageExpansionEntry(string title, string detail, string seller)
        {
            return new AdminShopEntry
            {
                Title = title,
                Detail = $"{detail} Mirrors the dedicated Cash Shop trunk-expansion path.",
                Seller = seller,
                Price = 2800,
                PriceLabel = FormatPriceLabel(2800),
                Category = AdminShopCategory.Etc,
                Featured = true,
                SupportsWishlist = true,
                State = AdminShopEntryState.Available,
                IsStorageExpansion = true
            };
        }

        private static AdminShopEntry CreateUserEntry(
            string title,
            string detail,
            string seller,
            long price,
            AdminShopCategory category,
            bool featured = false,
            AdminShopEntryState state = AdminShopEntryState.Available,
            string stateLabel = "")
        {
            return new AdminShopEntry
            {
                Title = title,
                Detail = detail,
                Seller = seller,
                Price = price,
                PriceLabel = FormatPriceLabel(price),
                Category = category,
                Featured = featured,
                SupportsWishlist = false,
                State = state,
                StateLabel = stateLabel
            };
        }

        private static AdminShopEntry CreateUserItemEntry(
            string title,
            string detail,
            string seller,
            long price,
            AdminShopCategory category,
            InventoryType rewardInventoryType,
            int rewardItemId,
            int rewardQuantity = 1,
            bool featured = false,
            bool lockAfterSuccess = false,
            AdminShopEntryState state = AdminShopEntryState.Available,
            string stateLabel = "",
            AdminShopResponse response = AdminShopResponse.GrantItem,
            string responseMessage = "")
        {
            return CreateItemEntry(
                title,
                detail,
                seller,
                price,
                category,
                false,
                rewardInventoryType,
                rewardItemId,
                rewardQuantity,
                featured,
                lockAfterSuccess,
                state,
                stateLabel,
                response,
                responseMessage);
        }

        private void ResolvePendingRequest()
        {
            if (_pendingRequestEntry == null)
            {
                return;
            }

            if (_pendingRequestEntry.IsStorageExpansion)
            {
                ResolveStorageExpansionRequest(_pendingRequestEntry);
                return;
            }

            if (_pendingRequestEntry.InventoryExpansionType != InventoryType.NONE)
            {
                ResolveInventoryExpansionRequest(_pendingRequestEntry);
                return;
            }

            ResolveCatalogRequest(_pendingRequestEntry);
        }

        private bool CanRequestEntry(AdminShopEntry entry)
        {
            if (entry == null || _pendingRequestEntry != null)
            {
                return false;
            }

            if (entry.IsStorageExpansion)
            {
                return CanRequestStorageExpansion(entry);
            }

            if (entry.InventoryExpansionType != InventoryType.NONE)
            {
                return CanRequestInventoryExpansion(entry);
            }

            return entry.State == AdminShopEntryState.Available
                   || entry.State == AdminShopEntryState.RequestAccepted
                   || entry.State == AdminShopEntryState.RequestRejected;
        }

        private string BuildBlockedRequestMessage(AdminShopEntry entry)
        {
            if (_pendingRequestEntry != null)
            {
                return $"A catalog request for {_pendingRequestEntry.Title} is already in flight.";
            }

            if (entry?.IsStorageExpansion == true)
            {
                return BuildStorageExpansionBlockedMessage(entry);
            }

            if (entry?.InventoryExpansionType != InventoryType.NONE)
            {
                return BuildInventoryExpansionBlockedMessage(entry);
            }

            return BuildEntryStateText(entry);
        }

        private bool CanRequestInventoryExpansion(AdminShopEntry entry)
        {
            if (entry == null || entry.InventoryExpansionType == InventoryType.NONE || _inventory == null)
            {
                return false;
            }

            return _inventory.CanExpandSlotLimit(entry.InventoryExpansionType)
                   && _inventory.GetMesoCount() >= entry.Price;
        }

        private bool CanRequestStorageExpansion(AdminShopEntry entry)
        {
            if (entry == null || !entry.IsStorageExpansion || _inventory == null || _storageRuntime == null)
            {
                return false;
            }

            return _storageRuntime.IsAccessSessionActive
                   && _storageRuntime.CanCurrentCharacterAccess
                   && _storageRuntime.IsClientAccountAuthorityVerified
                   && _storageRuntime.IsSecondaryPasswordVerified
                   && _storageRuntime.CanExpandSlotLimit()
                   && _inventory.GetMesoCount() >= entry.Price;
        }

        private string BuildInventoryExpansionBlockedMessage(AdminShopEntry entry)
        {
            if (_inventory == null)
            {
                return "Inventory runtime is unavailable for capacity updates.";
            }

            if (!_inventory.CanExpandSlotLimit(entry.InventoryExpansionType))
            {
                int slotLimit = _inventory.GetSlotLimit(entry.InventoryExpansionType);
                return $"{entry.Title} is already at the simulator cap ({slotLimit} slots).";
            }

            if (_inventory.GetMesoCount() < entry.Price)
            {
                return $"Need {FormatPriceLabel(entry.Price)} before extending this inventory tab.";
            }

            return BuildEntryStateText(entry);
        }

        private string BuildStorageExpansionBlockedMessage(AdminShopEntry entry)
        {
            if (_storageRuntime == null)
            {
                return "Storage runtime is unavailable for capacity updates.";
            }

            if (_inventory == null)
            {
                return "Inventory runtime is unavailable for cash-shop billing.";
            }

            if (!_storageRuntime.CanExpandSlotLimit())
            {
                return $"{entry.Title} is already at the simulator cap ({_storageRuntime.GetSlotLimit()} slots).";
            }

            if (!_storageRuntime.CanCurrentCharacterAccess)
            {
                string currentCharacterName = string.IsNullOrWhiteSpace(_storageRuntime.CurrentCharacterName)
                    ? "This character"
                    : _storageRuntime.CurrentCharacterName;
                string accountLabel = string.IsNullOrWhiteSpace(_storageRuntime.AccountLabel)
                    ? "this storage"
                    : _storageRuntime.AccountLabel;
                return $"{currentCharacterName} is not authorized to extend {accountLabel}.";
            }

            if (!_storageRuntime.IsAccessSessionActive)
            {
                return "Open storage and unlock the trunk session before purchasing storage-slot expansion.";
            }

            if (!_storageRuntime.IsClientAccountAuthorityVerified)
            {
                if (_storageRuntime.HasAccountPic && !_storageRuntime.IsAccountPicVerified)
                {
                    return "Verify the simulator account PIC before purchasing storage-slot expansion.";
                }

                if (_storageRuntime.HasAccountSecondaryPassword && !_storageRuntime.IsAccountSecondaryPasswordVerified)
                {
                    return "Verify the simulator account secondary password before purchasing storage-slot expansion.";
                }
            }

            if (!_storageRuntime.IsSecondaryPasswordVerified)
            {
                return _storageRuntime.HasSecondaryPassword
                    ? "Unlock trunk storage before purchasing storage-slot expansion."
                    : "Open storage first and create a passcode before purchasing storage-slot expansion.";
            }

            if (_inventory.GetMesoCount() < entry.Price)
            {
                return $"Need {FormatPriceLabel(entry.Price)} before extending storage capacity.";
            }

            return BuildEntryStateText(entry);
        }

        private void ResolveInventoryExpansionRequest(AdminShopEntry entry)
        {
            if (_inventory == null)
            {
                _footerMessage = "Inventory runtime is unavailable for slot expansion.";
                _pendingRequestEntry = null;
                UpdateActionButtonStates();
                return;
            }

            if (!_inventory.CanExpandSlotLimit(entry.InventoryExpansionType))
            {
                _footerMessage = BuildInventoryExpansionBlockedMessage(entry);
                _pendingRequestEntry = null;
                UpdateActionButtonStates();
                return;
            }

            if (!_inventory.TryConsumeMeso(entry.Price))
            {
                _footerMessage = $"Need {FormatPriceLabel(entry.Price)} before extending this inventory tab.";
                _pendingRequestEntry = null;
                UpdateActionButtonStates();
                return;
            }

            _inventory.TryExpandSlotLimit(entry.InventoryExpansionType);
            entry.State = AdminShopEntryState.RequestAccepted;
            entry.StateLabel = "Expanded";
            MarkEntryPurchased(entry);
            PersistEntrySessionState(entry);
            _footerMessage = $"{entry.Title} succeeded. {entry.InventoryExpansionType} inventory now has {_inventory.GetSlotLimit(entry.InventoryExpansionType)} slots.";
            _pendingRequestEntry = null;
            Money = _inventory.GetMesoCount();
            UpdateActionButtonStates();
        }

        private void ResolveStorageExpansionRequest(AdminShopEntry entry)
        {
            if (_storageRuntime == null)
            {
                _footerMessage = "Storage runtime is unavailable for slot expansion.";
                _pendingRequestEntry = null;
                UpdateActionButtonStates();
                return;
            }

            if (_inventory == null)
            {
                _footerMessage = "Inventory runtime is unavailable for cash-shop billing.";
                _pendingRequestEntry = null;
                UpdateActionButtonStates();
                return;
            }

            if (!_storageRuntime.CanExpandSlotLimit())
            {
                _footerMessage = BuildStorageExpansionBlockedMessage(entry);
                _pendingRequestEntry = null;
                UpdateActionButtonStates();
                return;
            }

            if (!_storageRuntime.CanCurrentCharacterAccess)
            {
                _footerMessage = BuildStorageExpansionBlockedMessage(entry);
                _pendingRequestEntry = null;
                UpdateActionButtonStates();
                return;
            }

            if (!_storageRuntime.IsAccessSessionActive ||
                !_storageRuntime.IsClientAccountAuthorityVerified ||
                !_storageRuntime.IsSecondaryPasswordVerified)
            {
                _footerMessage = BuildStorageExpansionBlockedMessage(entry);
                _pendingRequestEntry = null;
                UpdateActionButtonStates();
                return;
            }

            if (!_inventory.TryConsumeMeso(entry.Price))
            {
                _footerMessage = $"Need {FormatPriceLabel(entry.Price)} before extending storage capacity.";
                _pendingRequestEntry = null;
                UpdateActionButtonStates();
                return;
            }

            _storageRuntime.TryExpandSlotLimit();
            entry.State = AdminShopEntryState.RequestAccepted;
            entry.StateLabel = "Expanded";
            MarkEntryPurchased(entry);
            PersistEntrySessionState(entry);
            _footerMessage = $"{entry.Title} succeeded. Storage now has {_storageRuntime.GetSlotLimit()} slots.";
            _pendingRequestEntry = null;
            Money = _inventory.GetMesoCount();
            UpdateActionButtonStates();
        }

        private void ResolveCatalogRequest(AdminShopEntry entry)
        {
            if (_inventory == null)
            {
                entry.State = AdminShopEntryState.RequestRejected;
                entry.StateLabel = "No runtime";
                _footerMessage = "Inventory runtime is unavailable for shop delivery.";
                _pendingRequestEntry = null;
                UpdateActionButtonStates();
                return;
            }

            if (entry.ConsumeOnSuccess && !_inventory.TryConsumeMeso(entry.Price))
            {
                entry.State = AdminShopEntryState.RequestRejected;
                entry.StateLabel = "Need mesos";
                _footerMessage = $"Need {FormatPriceLabel(entry.Price)} before this request can complete.";
                _pendingRequestEntry = null;
                Money = _inventory.GetMesoCount();
                UpdateActionButtonStates();
                return;
            }

            switch (entry.Response)
            {
                case AdminShopResponse.GrantItem:
                    ResolveGrantedItemRequest(entry);
                    return;
                case AdminShopResponse.ListingSoldOut:
                    FinishRejectedRequest(entry, "Sold out", string.IsNullOrWhiteSpace(entry.ResponseMessage)
                        ? $"{entry.Title} sold out before the simulator session completed the request."
                        : entry.ResponseMessage, refundMeso: entry.ConsumeOnSuccess);
                    return;
                case AdminShopResponse.SellerUnavailable:
                    FinishRejectedRequest(entry, "No reply", string.IsNullOrWhiteSpace(entry.ResponseMessage)
                        ? $"{entry.Seller} did not answer the simulator trade relay."
                        : entry.ResponseMessage, refundMeso: entry.ConsumeOnSuccess);
                    return;
                case AdminShopResponse.ListingExpired:
                    FinishRejectedRequest(entry, "Expired", string.IsNullOrWhiteSpace(entry.ResponseMessage)
                        ? $"{entry.Title} expired before delivery could be confirmed."
                        : entry.ResponseMessage, refundMeso: entry.ConsumeOnSuccess);
                    return;
                case AdminShopResponse.InventoryFull:
                    FinishRejectedRequest(entry, "Inventory full", string.IsNullOrWhiteSpace(entry.ResponseMessage)
                        ? $"The destination {entry.RewardInventoryType} inventory tab cannot accept {entry.Title}."
                        : entry.ResponseMessage, refundMeso: entry.ConsumeOnSuccess);
                    return;
                default:
                    FinishRejectedRequest(entry, "Rejected", $"The simulator rejected the request for {entry.Title}.", refundMeso: entry.ConsumeOnSuccess);
                    return;
            }
        }

        private void ResolveGrantedItemRequest(AdminShopEntry entry)
        {
            if (entry.RewardInventoryType == InventoryType.NONE || entry.RewardItemId <= 0)
            {
                entry.State = AdminShopEntryState.RequestAccepted;
                entry.StateLabel = "Accepted";
                PersistEntrySessionState(entry);
                _footerMessage = $"Catalog response received for {entry.Title}. The simulator accepted the request.";
                _pendingRequestEntry = null;
                Money = _inventory?.GetMesoCount() ?? Money;
                UpdateActionButtonStates();
                return;
            }

            if (!_inventory.CanAcceptItem(entry.RewardInventoryType, entry.RewardItemId, entry.RewardQuantity))
            {
                FinishRejectedRequest(
                    entry,
                    "Inventory full",
                    $"The destination {entry.RewardInventoryType} inventory tab cannot accept {entry.Title}.",
                    refundMeso: entry.ConsumeOnSuccess);
                return;
            }

            Texture2D itemTexture = ResolveEntryIcon(entry);
            _inventory.AddItem(entry.RewardInventoryType, entry.RewardItemId, itemTexture, entry.RewardQuantity);
            entry.State = entry.LockAfterSuccess ? AdminShopEntryState.SoldOut : AdminShopEntryState.RequestAccepted;
            entry.StateLabel = entry.LockAfterSuccess ? "Purchased" : "Delivered";
            entry.IconTexture = itemTexture;
            MarkEntryPurchased(entry);
            PersistEntrySessionState(entry);
            _footerMessage = $"{entry.Title} delivered to {entry.RewardInventoryType} inventory.";
            _pendingRequestEntry = null;
            Money = _inventory.GetMesoCount();
            UpdateActionButtonStates();
        }

        private void FinishRejectedRequest(AdminShopEntry entry, string stateLabel, string footerMessage, bool refundMeso)
        {
            if (refundMeso && _inventory != null && entry.Price > 0)
            {
                _inventory.AddMeso(entry.Price);
            }

            entry.State = AdminShopEntryState.RequestRejected;
            entry.StateLabel = stateLabel;
            PersistEntrySessionState(entry);
            _footerMessage = footerMessage;
            _pendingRequestEntry = null;
            Money = _inventory?.GetMesoCount() ?? Money;
            UpdateActionButtonStates();
        }

        private static string BuildEntryStateText(AdminShopEntry entry)
        {
            return entry?.State switch
            {
                AdminShopEntryState.Available => "Status: ready to request.",
                AdminShopEntryState.SoldOut => "Status: sold out in the simulator catalog.",
                AdminShopEntryState.PreviewOnly => "Status: preview-only row until full session data is wired.",
                AdminShopEntryState.PendingResponse => "Status: waiting for the shop response.",
                AdminShopEntryState.RequestAccepted => "Status: request acknowledged by the simulator session.",
                AdminShopEntryState.RequestRejected => "Status: the simulator session rejected the latest request.",
                _ => "Status: unavailable."
            };
        }

        private static Color GetTitleColor(AdminShopEntry entry, bool isSelected)
        {
            if (entry?.State == AdminShopEntryState.SoldOut || entry?.State == AdminShopEntryState.PreviewOnly)
            {
                return isSelected ? new Color(64, 64, 64) : new Color(186, 186, 186);
            }

            if (entry?.State == AdminShopEntryState.PendingResponse)
            {
                return isSelected ? new Color(56, 38, 0) : new Color(255, 232, 142);
            }

            if (entry?.State == AdminShopEntryState.RequestRejected)
            {
                return isSelected ? new Color(70, 20, 20) : new Color(255, 188, 188);
            }

            return isSelected ? Color.Black : Color.White;
        }

        private static Color GetDetailColor(AdminShopEntry entry, bool isSelected)
        {
            if (entry?.State == AdminShopEntryState.SoldOut || entry?.State == AdminShopEntryState.PreviewOnly)
            {
                return isSelected ? new Color(78, 78, 78) : new Color(158, 158, 158);
            }

            if (entry?.State == AdminShopEntryState.PendingResponse)
            {
                return isSelected ? new Color(84, 58, 0) : new Color(230, 204, 104);
            }

            if (entry?.State == AdminShopEntryState.RequestRejected)
            {
                return isSelected ? new Color(94, 44, 44) : new Color(240, 175, 175);
            }

            return isSelected ? new Color(42, 42, 42) : new Color(210, 210, 210);
        }

        private static Color GetStateColor(AdminShopEntry entry, bool isSelected)
        {
            return entry?.State switch
            {
                AdminShopEntryState.Available => isSelected ? new Color(31, 79, 29) : new Color(166, 225, 161),
                AdminShopEntryState.SoldOut => isSelected ? new Color(110, 54, 54) : new Color(231, 161, 161),
                AdminShopEntryState.PreviewOnly => isSelected ? new Color(85, 74, 27) : new Color(228, 209, 142),
                AdminShopEntryState.PendingResponse => isSelected ? new Color(84, 58, 0) : new Color(255, 229, 128),
                AdminShopEntryState.RequestAccepted => isSelected ? new Color(24, 78, 88) : new Color(146, 223, 238),
                AdminShopEntryState.RequestRejected => isSelected ? new Color(105, 36, 36) : new Color(255, 170, 170),
                _ => isSelected ? new Color(42, 42, 42) : Color.White
            };
        }

        private string BuildHeaderInstruction()
        {
            if (_pendingRequestEntry != null)
            {
                return $"Waiting for catalog response on {_pendingRequestEntry.Title}.";
            }

            AdminShopEntry entry = GetSelectedEntry();
            if (entry == null)
            {
                return $"Browse {GetBrowseModeLabel(_activeBrowseMode)} rows, then use BtBuy, BtSell, or BtRecharge.";
            }

            if (_activeBrowseMode == AdminShopBrowseMode.Rebuy)
            {
                return "RE-BUY shows rows already delivered in this simulator session.";
            }

            if (_activePane == AdminShopPane.User || _activeBrowseMode == AdminShopBrowseMode.Sell)
            {
                return "BtBuy or BtSell submits a relay request for the highlighted listing.";
            }

            if (entry.SupportsWishlist)
            {
                return "BtBuy or BtSell submits the request. BtRecharge opens the wish-list confirmation.";
            }

            return "BtBuy or BtSell submits the request. BtRecharge opens the wish-list confirmation when supported.";
        }

        private static string FormatPriceLabel(long price)
        {
            return price.ToString("N0", CultureInfo.InvariantCulture) + " mesos";
        }

        private static bool ShouldIncludeEntry(AdminShopEntry entry, AdminShopCategory category, AdminShopBrowseMode browseMode, AdminShopPane pane)
        {
            if (entry == null || !MatchesCategory(entry, category))
            {
                return false;
            }

            return browseMode switch
            {
                AdminShopBrowseMode.All => true,
                AdminShopBrowseMode.Most => entry.Featured,
                AdminShopBrowseMode.Sell => pane == AdminShopPane.User,
                AdminShopBrowseMode.Buy => pane == AdminShopPane.Npc,
                AdminShopBrowseMode.Rebuy => entry.WasPurchased,
                _ => true
            };
        }

        private static string GetCategoryLabel(AdminShopCategory category)
        {
            return category switch
            {
                AdminShopCategory.All => "All",
                AdminShopCategory.Equip => "Equip",
                AdminShopCategory.Use => "Use",
                AdminShopCategory.Setup => "Set-up",
                AdminShopCategory.Etc => "Etc",
                AdminShopCategory.Cash => "Cash",
                AdminShopCategory.Recipe => "Recipe",
                AdminShopCategory.Scroll => "Scroll",
                AdminShopCategory.Special => "Special",
                AdminShopCategory.Package => "Package",
                AdminShopCategory.Button => "Button",
                _ => "All"
            };
        }

        private static bool MatchesCategory(AdminShopEntry entry, AdminShopCategory category)
        {
            if (category == AdminShopCategory.All)
            {
                return true;
            }

            if (category == AdminShopCategory.Cash)
            {
                return entry.RewardInventoryType == InventoryType.CASH
                       || entry.InventoryExpansionType == InventoryType.CASH;
            }

            return entry.Category == category;
        }

        private void RestoreEntryFlags(IEnumerable<AdminShopEntry> entries, AdminShopServiceMode mode)
        {
            foreach (AdminShopEntry entry in entries)
            {
                string key = GetEntryKey(entry);
                entry.Wishlisted = _wishlistedEntryKeys[mode].Contains(key);
                entry.WasPurchased = _purchasedEntryKeys[mode].Contains(key);
            }
        }

        private void RestoreEntryStates(IEnumerable<AdminShopEntry> entries, AdminShopServiceMode mode)
        {
            Dictionary<string, AdminShopEntrySessionState> sessionStates = _entrySessionStates[mode];
            foreach (AdminShopEntry entry in entries)
            {
                string key = GetEntryKey(entry);
                if (!sessionStates.TryGetValue(key, out AdminShopEntrySessionState sessionState))
                {
                    continue;
                }

                entry.State = sessionState.State;
                entry.StateLabel = sessionState.StateLabel;
            }
        }

        private void MarkEntryPurchased(AdminShopEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entry.WasPurchased = true;
            _purchasedEntryKeys[_currentMode].Add(GetEntryKey(entry));
            PersistEntrySessionState(entry);
        }

        private void PersistEntrySessionState(AdminShopEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            string key = GetEntryKey(entry);
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (entry.State == AdminShopEntryState.PendingResponse)
            {
                _entrySessionStates[_currentMode].Remove(key);
                return;
            }

            _entrySessionStates[_currentMode][key] = new AdminShopEntrySessionState
            {
                State = entry.State,
                StateLabel = entry.StateLabel ?? string.Empty
            };
        }

        private static string GetEntryKey(AdminShopEntry entry)
        {
            return entry == null
                ? string.Empty
                : $"{entry.Title}|{entry.Seller}|{entry.Price}|{entry.RewardItemId}|{entry.InventoryExpansionType}|{entry.IsStorageExpansion}";
        }

        private static AdminShopCategory GetQuickCategory(int tabIndex)
        {
            return tabIndex switch
            {
                0 => AdminShopCategory.Equip,
                1 => AdminShopCategory.Use,
                2 => AdminShopCategory.Etc,
                3 => AdminShopCategory.Setup,
                4 => AdminShopCategory.Cash,
                _ => AdminShopCategory.All
            };
        }

        private static AdminShopCategory GetFullCategory(int tabIndex)
        {
            return tabIndex switch
            {
                0 => AdminShopCategory.All,
                1 => AdminShopCategory.Equip,
                2 => AdminShopCategory.Use,
                3 => AdminShopCategory.Setup,
                4 => AdminShopCategory.Etc,
                5 => AdminShopCategory.Cash,
                6 => AdminShopCategory.Recipe,
                7 => AdminShopCategory.Scroll,
                8 => AdminShopCategory.Special,
                9 => AdminShopCategory.Package,
                _ => AdminShopCategory.All
            };
        }

        private string BuildBrowseModeMessage(AdminShopBrowseMode browseMode)
        {
            return browseMode switch
            {
                AdminShopBrowseMode.All => $"Showing the full {_currentMode} catalog.",
                AdminShopBrowseMode.Most => $"Showing featured {_currentMode} rows from the WZ-backed MOST tab.",
                AdminShopBrowseMode.Sell => "SELL tab now focuses the user-listing side of the simulator session.",
                AdminShopBrowseMode.Buy => "BUY tab now focuses the NPC-offer side of the simulator session.",
                AdminShopBrowseMode.Rebuy => "RE-BUY tab now filters to rows already delivered in this simulator session.",
                _ => $"Showing the full {_currentMode} catalog."
            };
        }

        private static string GetBrowseModeLabel(AdminShopBrowseMode browseMode)
        {
            return browseMode switch
            {
                AdminShopBrowseMode.All => "ALL",
                AdminShopBrowseMode.Most => "MOST",
                AdminShopBrowseMode.Sell => "SELL",
                AdminShopBrowseMode.Buy => "BUY",
                AdminShopBrowseMode.Rebuy => "RE-BUY",
                _ => "ALL"
            };
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

        private AdminShopEntry FindCommodityEntry(AdminShopCommodityData commodity)
        {
            if (commodity == null)
            {
                return null;
            }

            foreach (AdminShopEntry entry in _paneStates[AdminShopPane.Npc].SourceEntries)
            {
                if (entry == null || entry.RewardItemId != commodity.ItemId)
                {
                    continue;
                }

                if (entry.CommoditySerialNumber == commodity.SerialNumber)
                {
                    return entry;
                }
            }

            foreach (AdminShopEntry entry in _paneStates[AdminShopPane.Npc].SourceEntries)
            {
                if (entry?.RewardItemId == commodity.ItemId)
                {
                    return entry;
                }
            }

            return null;
        }

        private static AdminShopCategory ResolveCommodityCategory(AdminShopEntry entry)
        {
            if (entry == null)
            {
                return AdminShopCategory.All;
            }

            return entry.Category == AdminShopCategory.Button
                ? AdminShopCategory.All
                : entry.Category;
        }

        private static void EnsureCommodityCache()
        {
            if (_bestCommodityByItemId != null && _commodityBySerialNumber != null)
            {
                return;
            }

            lock (CommodityCacheLock)
            {
                if (_bestCommodityByItemId != null && _commodityBySerialNumber != null)
                {
                    return;
                }

                Dictionary<int, AdminShopCommodityData> bestByItemId = new();
                Dictionary<int, AdminShopCommodityData> bySerial = new();
                WzImage commodityImage = global::HaCreator.Program.FindImage("Etc", "Commodity.img");
                commodityImage?.ParseImage();
                if (commodityImage?.WzProperties != null)
                {
                    foreach (WzImageProperty property in commodityImage.WzProperties)
                    {
                        if (property is not WzSubProperty commodityProperty
                            || !TryGetIntProperty(commodityProperty, "SN", out int serialNumber)
                            || !TryGetIntProperty(commodityProperty, "ItemId", out int itemId)
                            || !TryGetIntProperty(commodityProperty, "Price", out int price))
                        {
                            continue;
                        }

                        AdminShopCommodityData commodity = new()
                        {
                            SerialNumber = serialNumber,
                            ItemId = itemId,
                            Count = TryGetIntProperty(commodityProperty, "Count", out int count) ? Math.Max(1, count) : 1,
                            Price = Math.Max(0, price),
                            Priority = TryGetIntProperty(commodityProperty, "Priority", out int priority) ? priority : 0,
                            PeriodDays = TryGetIntProperty(commodityProperty, "Period", out int periodDays) ? periodDays : 0,
                            OnSale = TryGetIntProperty(commodityProperty, "OnSale", out int onSale) && onSale != 0
                        };

                        bySerial[serialNumber] = commodity;
                        if (!bestByItemId.TryGetValue(itemId, out AdminShopCommodityData existing)
                            || IsPreferredCommodity(commodity, existing))
                        {
                            bestByItemId[itemId] = commodity;
                        }
                    }
                }

                _bestCommodityByItemId = bestByItemId;
                _commodityBySerialNumber = bySerial;
            }
        }

        private static bool TryGetBestCommodityForItem(int itemId, out AdminShopCommodityData commodity)
        {
            commodity = null;
            EnsureCommodityCache();
            return _bestCommodityByItemId != null
                   && _bestCommodityByItemId.TryGetValue(itemId, out commodity);
        }

        private static bool TryGetCommodityBySerialNumber(int serialNumber, out AdminShopCommodityData commodity)
        {
            commodity = null;
            EnsureCommodityCache();
            return _commodityBySerialNumber != null
                   && _commodityBySerialNumber.TryGetValue(serialNumber, out commodity);
        }

        private static bool IsPreferredCommodity(AdminShopCommodityData candidate, AdminShopCommodityData existing)
        {
            if (candidate == null)
            {
                return false;
            }

            if (existing == null)
            {
                return true;
            }

            if (candidate.OnSale != existing.OnSale)
            {
                return candidate.OnSale;
            }

            if (candidate.Priority != existing.Priority)
            {
                return candidate.Priority > existing.Priority;
            }

            if (candidate.PeriodDays != existing.PeriodDays)
            {
                return candidate.PeriodDays > existing.PeriodDays;
            }

            if (candidate.Price != existing.Price)
            {
                return candidate.Price < existing.Price;
            }

            return candidate.SerialNumber < existing.SerialNumber;
        }

        private static bool TryGetIntProperty(WzSubProperty property, string name, out int value)
        {
            switch (property?[name])
            {
                case WzIntProperty intProperty:
                    value = intProperty.Value;
                    return true;
                case WzShortProperty shortProperty:
                    value = shortProperty.Value;
                    return true;
                case WzLongProperty longProperty:
                    value = (int)Math.Clamp(longProperty.Value, int.MinValue, int.MaxValue);
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }
    }
}
