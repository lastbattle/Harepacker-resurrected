using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using HaCreator.MapSimulator.Interaction;
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
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public enum AdminShopServiceMode
    {
        CashShop,
        Mts
    }

    public sealed class AdminShopDialogUI : UIWindowBase
    {
        public sealed class WishlistPriceRange
        {
            public int Index { get; init; }
            public long MinimumPrice { get; init; }
            public long MaximumPrice { get; init; }
            public string Label { get; init; } = string.Empty;
        }

        public sealed class WishlistSearchResult
        {
            public string EntryKey { get; init; } = string.Empty;
            public string Title { get; init; } = string.Empty;
            public string Seller { get; init; } = string.Empty;
            public string PriceLabel { get; init; } = string.Empty;
            public string Detail { get; init; } = string.Empty;
            public string CategoryLabel { get; init; } = string.Empty;
            public int RewardItemId { get; init; }
            public Texture2D IconTexture { get; init; }
            public bool AlreadyWishlisted { get; init; }
            public int Score { get; init; }
        }

        public sealed class WishlistCategoryNode
        {
            public string Key { get; init; } = string.Empty;
            public string Label { get; init; } = string.Empty;
            public string ParentKey { get; init; } = string.Empty;
            public int ClientStringPoolId { get; init; } = -1;
            public int ClientChildStageIndex { get; init; } = -1;
            public IReadOnlyList<WishlistCategoryNode> Children { get; init; } = Array.Empty<WishlistCategoryNode>();
        }

        public sealed class OwnerEntrySnapshot
        {
            public string Title { get; init; } = string.Empty;
            public string Detail { get; init; } = string.Empty;
            public string Seller { get; init; } = string.Empty;
            public string PriceLabel { get; init; } = string.Empty;
            public string StateLabel { get; init; } = string.Empty;
            public bool IsSelected { get; init; }
            public int CommoditySerialNumber { get; init; }
            public int RewardItemId { get; init; }
            public int RewardQuantity { get; init; } = 1;
            public bool CommodityOnSale { get; init; }
        }

        public sealed class ListOwnerSnapshot
        {
            public string PaneLabel { get; init; } = string.Empty;
            public string BrowseModeLabel { get; init; } = string.Empty;
            public string CategoryLabel { get; init; } = string.Empty;
            public string FooterMessage { get; init; } = string.Empty;
            public int SelectedIndex { get; init; } = -1;
            public int ScrollOffset { get; init; }
            public int TotalCount { get; init; }
            public IReadOnlyList<OwnerEntrySnapshot> VisibleEntries { get; init; } = Array.Empty<OwnerEntrySnapshot>();
        }

        public sealed class LockerOwnerSnapshot
        {
            public string AccountLabel { get; init; } = string.Empty;
            public int UsedSlotCount { get; init; }
            public int SlotLimit { get; init; }
            public bool CanExpand { get; init; }
            public IReadOnlyList<string> SharedCharacterNames { get; init; } = Array.Empty<string>();
        }

        public sealed class InventoryOwnerSnapshot
        {
            public int EquipCount { get; init; }
            public int UseCount { get; init; }
            public int SetupCount { get; init; }
            public int EtcCount { get; init; }
            public int CashCount { get; init; }
            public string SelectedEntryTitle { get; init; } = string.Empty;
        }

        public sealed class StorageExpansionResolution
        {
            public int CashItemResultSubtype { get; init; }
            public int CommoditySerialNumber { get; init; }
            public int ResultSubtype { get; init; }
            public int FailureReason { get; init; }
            public long NxPrice { get; init; }
            public int SlotLimitAfterResult { get; init; }
            public bool IsPacketOwned { get; init; }
            public int PacketType { get; init; }
            public bool CashAlreadySettled { get; init; }
            public string Message { get; init; } = string.Empty;
        }

        public sealed class PacketOwnedStorageExpansionResult
        {
            public int PacketType { get; init; }
            public int CashItemResultSubtype { get; init; }
            public int CommoditySerialNumber { get; init; }
            public int ResultSubtype { get; init; }
            public int FailureReason { get; init; }
            public long NxPrice { get; init; }
            public int SlotLimitAfterResult { get; init; }
            public bool ConsumeCash { get; init; } = true;
            public string Message { get; init; } = string.Empty;
        }

        public sealed class PacketOwnedAdminShopCommoditySnapshot
        {
            public int SerialNumber { get; init; }
            public int ItemId { get; init; }
            public int Price { get; init; }
            public byte SaleState { get; init; }
            public int MaxPerSlot { get; init; }
        }

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
            InventoryFull,
            MissingSourceItem
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

        private enum AdminShopModalMode
        {
            None,
            WishlistOwnerConfirm,
            RequestConfirm,
            RequestQuantity
        }

        private static class StorageExpansionResultSubtype
        {
            public const int Success = 1;
            public const int Rejected = 2;
        }

        private static class PacketOwnedAdminShopOutboundMode
        {
            public const int Reopen = 0;
            public const int TradeRequest = 1;
            public const int Close = 2;
            public const int RegisterWishlistItem = 3;
        }

        private static class StorageExpansionFailureReason
        {
            public const int None = 0;
            public const int RuntimeUnavailable = 1;
            public const int SlotCapReached = 2;
            public const int UnauthorizedCharacter = 3;
            public const int SessionLocked = 4;
            public const int MissingAccountAuthority = 5;
            public const int MissingStoragePasscode = 6;
            public const int NotEnoughCash = 7;
            public const int ExpansionFailed = 8;
        }

        private sealed class AdminShopEntry
        {
            public string Title { get; set; } = string.Empty;
            public string Detail { get; set; } = string.Empty;
            public string Seller { get; init; } = string.Empty;
            public string PriceLabel { get; set; } = string.Empty;
            public long Price { get; set; }
            public AdminShopCategory Category { get; set; }
            public Texture2D IconTexture { get; set; }
            public bool SupportsWishlist { get; set; }
            public bool Wishlisted { get; set; }
            public AdminShopEntryState State { get; set; }
            public string StateLabel { get; set; } = string.Empty;
            public bool IsStorageExpansion { get; init; }
            public InventoryType InventoryExpansionType { get; init; } = InventoryType.NONE;
            public InventoryType RewardInventoryType { get; init; } = InventoryType.NONE;
            public int RewardItemId { get; init; }
            public int RewardQuantity { get; set; } = 1;
            public int RewardMaxStackSize { get; set; } = 1;
            public bool ConsumeOnSuccess { get; init; } = true;
            public bool LockAfterSuccess { get; init; }
            public AdminShopResponse Response { get; init; }
            public string ResponseMessage { get; init; } = string.Empty;
            public bool Featured { get; init; }
            public bool WasPurchased { get; set; }
            public int CommoditySerialNumber { get; set; }
            public bool CommodityOnSale { get; set; }
            public int MaxRequestCount { get; init; } = 1;
            public long SuccessMesoReward { get; init; }
            public InventoryType SourceInventoryType { get; init; } = InventoryType.NONE;
            public int SourceItemId { get; init; }
            public int SourceItemQuantity { get; init; }
            public InventoryType DisplayInventoryType { get; init; } = InventoryType.NONE;
            public int DisplayItemId { get; init; }
            public int DisplayQuantity { get; init; } = 1;
            public int InventorySlotIndex { get; init; } = -1;
            public int PacketSerialNumber { get; init; }
            public byte PacketSaleState { get; init; }
            public bool IsPacketOwnedSnapshotRow { get; init; }
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

        private sealed class AdminShopBrowseSurfaceState
        {
            public int ScrollOffset { get; set; }
            public string SelectedEntryKey { get; set; } = string.Empty;
        }

        private readonly record struct SourceInventoryStackResolution(
            int SlotIndex,
            int StackQuantity,
            int TotalOwnedQuantity);

        private sealed class WishlistCategoryLeafDefinition
        {
            public string Key { get; init; } = string.Empty;
            public string Label { get; init; } = string.Empty;
            public Func<AdminShopEntry, bool> Matches { get; init; }
        }

        private sealed class WishlistCategoryClientDefinition
        {
            public string Key { get; init; } = string.Empty;
            public string FallbackLabel { get; init; } = string.Empty;
            public int StringPoolId { get; init; } = -1;
            public int ChildStageIndex { get; init; } = -1;
            public IReadOnlyList<WishlistCategoryClientDefinition> Children { get; init; } = Array.Empty<WishlistCategoryClientDefinition>();
        }

        private sealed class WishlistSearchIndexEntry
        {
            public string CombinedText { get; init; } = string.Empty;
            public string CollapsedText { get; init; } = string.Empty;
            public IReadOnlyList<string> Terms { get; init; } = Array.Empty<string>();
        }

        private const int MaxVisibleRows = 5;
        private const int PacketOwnedStorageExpansionTimeoutMs = 4000;
        private const int LeftPaneX = 17;
        private const int RightPaneX = 242;
        private const int PaneTopY = 101;
        private const int PaneRowHeight = 35;
        private const int PaneWidth = 165;
        private const int HeaderX = 18;
        private const int HeaderY = 72;
        private const int PaneLabelY = 90;
        private const int RowIconX = 4;
        private static readonly IReadOnlyList<WishlistCategoryNode> s_wishlistCategoryTree = BuildWishlistCategoryTree();
        private static readonly Dictionary<string, WishlistCategoryNode> s_wishlistCategoryNodesByKey = BuildWishlistCategoryNodeLookup(s_wishlistCategoryTree);
        private static readonly IReadOnlyDictionary<string, WishlistCategoryLeafDefinition> s_wishlistCategoryLeaves = BuildWishlistCategoryLeaves();
        private static readonly object WishlistSearchIndexLock = new();
        private static IReadOnlyDictionary<int, WishlistSearchIndexEntry> _wishlistSearchIndexByItemId;
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
        private const int FooterWishStateY = 70;
        private const int FooterMessageY = 86;
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
        private const int DoubleClickThresholdMilliseconds = 500;
        // `TabShop` canvases do not encode per-tab placement; the client positions this strip via `CAdminShopDlg::OnCreate`.
        private const int CategoryTabStartX = 95;
        private const int CategoryTabStartY = 222;
        private const int CategoryTabColumns = 5;
        private const int CategoryTabStrideX = 42;
        private const int CategoryTabStrideY = 19;
        private static readonly int[] DefaultCommoditySeedItemIds =
        {
            5050000,
            5150040,
            2120000,
            5040004,
            5222000,
            5152057
        };
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
        private readonly UIObject _modalPreviousButton;
        private readonly UIObject _modalNextButton;
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
        private readonly Dictionary<string, AdminShopBrowseSurfaceState> _browseSurfaceStates = new(StringComparer.Ordinal);
        private readonly IReadOnlyList<WishlistPriceRange> _wishlistPriceRanges = BuildWishlistPriceRanges();
        private readonly List<PacketOwnedAdminShopCommoditySnapshot> _packetOwnedAdminShopRows = new();
        private readonly List<AdminShopEntry> _packetOwnedAdminShopSellTemplates = new();
        private readonly List<AdminShopUserSellMutationRow> _pendingPacketOwnedUserSellSnapshotRows = new();
        private readonly AdminShopPacketOwnedSessionContract _packetOwnedAdminShopSession = new();

        private IInventoryRuntime _inventory;
        private IStorageRuntime _storageRuntime;
        private SpriteFont _font;
        private AdminShopServiceMode _currentMode;
        private AdminShopPane _activePane = AdminShopPane.Npc;
        private AdminShopCategory _activeCategory = AdminShopCategory.All;
        private AdminShopBrowseMode _activeBrowseMode = AdminShopBrowseMode.All;
        private string _footerMessage = string.Empty;
        private string _modalMessage = string.Empty;
        private AdminShopEntry _pendingModalEntry;
        private AdminShopEntry _pendingRequestEntry;
        private int _pendingRequestQuantity = 1;
        private AdminShopEntryState _pendingRequestPreviousState;
        private string _pendingRequestPreviousStateLabel = string.Empty;
        private bool _pendingStorageExpansionAwaitingPacketResult;
        private int _pendingStorageExpansionCommoditySerialNumber;
        private string _lastClickedEntryKey = string.Empty;
        private int _previousScrollWheelValue;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private AdminShopPane? _draggingScrollPane;
        private int _scrollThumbDragOffsetY;
        private bool _modalVisible;
        private AdminShopModalMode _modalMode;
        private int _modalQuantity = 1;
        private int _modalQuantityMin = 1;
        private int _modalQuantityMax = 1;
        private AdminShopPane? _lastClickedPane;
        private int _lastRowClickTick;
        private int _requestResolveTick;
        private long _nexonCash;
        private long _maplePoint;
        private long _prepaidCash;
        private InventoryType _pendingPacketOwnedUserSellSnapshotInventoryType = InventoryType.NONE;
        private int _pendingPacketOwnedUserSellSnapshotScrollOffset;
        private bool _pendingPacketOwnedAdminShopResult;
        private AdminShopEntry _pendingPacketOwnedWishlistRegisterEntry;
        private AdminShopCategory _pendingPacketOwnedWishlistRegisterCategory = AdminShopCategory.All;
        public Action<AdminShopDialogUI> WishlistWindowRequested { get; set; }
        public Action<AdminShopDialogUI> WindowHidden { get; set; }
        public Func<long, bool> TryConsumeCashBalance { get; set; }
        public Func<int> ResolveStorageExpansionCommoditySerialNumber { get; set; }
        public Func<string> GetStorageExpansionStatusSummary { get; set; }
        public Action<StorageExpansionResolution> StorageExpansionResolved { get; set; }
        internal Func<PacketOwnedNpcUtilityOutboundRequest, string> DispatchPacketOwnedAdminShopOutboundRequest { get; set; }
        public bool HasPendingStorageExpansionRequest => _pendingRequestEntry?.IsStorageExpansion == true;
        private const int PacketOwnedAdminShopResultMode = PacketOwnedAdminShopOutboundMode.Reopen;
        private const int PacketOwnedAdminShopTradeRequestMode = PacketOwnedAdminShopOutboundMode.TradeRequest;
        private const int PacketOwnedAdminShopCloseMode = PacketOwnedAdminShopOutboundMode.Close;
        private const int PacketOwnedAdminShopWishlistRegisterMode = PacketOwnedAdminShopOutboundMode.RegisterWishlistItem;

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
            UIObject modalPreviousButton,
            UIObject modalNextButton,
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
            _modalPreviousButton = modalPreviousButton;
            _modalNextButton = modalNextButton;

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
                _exitButton.ButtonClickReleased += _ => CloseFromUserInput();
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

            if (_modalPreviousButton != null)
            {
                AddButton(_modalPreviousButton);
                _modalButtons.Add(_modalPreviousButton);
                _modalPreviousButton.SetVisible(false);
                _modalPreviousButton.ButtonClickReleased += OnModalPreviousClicked;
            }

            if (_modalNextButton != null)
            {
                AddButton(_modalNextButton);
                _modalButtons.Add(_modalNextButton);
                _modalNextButton.SetVisible(false);
                _modalNextButton.ButtonClickReleased += OnModalNextClicked;
            }

            InitializeRowButtons(device);
            InitializeTabVisuals();
            ResetMode(defaultMode);
        }

        public override string WindowName => _windowName;

        public long Money { get; set; }

        public void SetCashBalances(long nexonCash, long maplePoint = 0, long prepaidCash = 0)
        {
            _nexonCash = Math.Max(0L, nexonCash);
            _maplePoint = Math.Max(0L, maplePoint);
            _prepaidCash = Math.Max(0L, prepaidCash);
            UpdateActionButtonStates();
        }

        public void SetInventory(IInventoryRuntime inventory)
        {
            _inventory = inventory;
            Money = _inventory?.GetMesoCount() ?? Money;
            RefreshDynamicUserEntries();
            UpdateActionButtonStates();
        }

        public bool HasPacketOwnedAdminShopSession => _packetOwnedAdminShopSession.IsActive;

        public int PacketOwnedAdminShopNpcTemplateId => _packetOwnedAdminShopSession.NpcTemplateId;

        public bool HasPacketOwnedAdminShopCatalog => UsesPacketOwnedAdminShopCatalog(AdminShopServiceMode.CashShop);

        public IReadOnlyList<CashServiceStageWindow.PacketCatalogEntry> GetPacketOwnedCashShopStageCatalogEntries()
        {
            if (!UsesPacketOwnedAdminShopCatalog(AdminShopServiceMode.CashShop))
            {
                return Array.Empty<CashServiceStageWindow.PacketCatalogEntry>();
            }

            List<CashServiceStageWindow.PacketCatalogEntry> entries = new(_packetOwnedAdminShopRows.Count);
            foreach (PacketOwnedAdminShopCommoditySnapshot row in _packetOwnedAdminShopRows)
            {
                AdminShopEntry entry = row.Price > 0
                    ? CreatePacketOwnedCommodityEntry(row)
                    : CreatePacketOwnedSellTemplateEntry(row);
                if (entry == null)
                {
                    continue;
                }

                entries.Add(new CashServiceStageWindow.PacketCatalogEntry
                {
                    Title = entry.Title ?? string.Empty,
                    Detail = entry.Detail ?? string.Empty,
                    Seller = entry.Seller ?? string.Empty,
                    PriceLabel = entry.PriceLabel ?? string.Empty,
                    StateLabel = string.IsNullOrWhiteSpace(entry.StateLabel)
                        ? "Packet-owned shop"
                        : entry.StateLabel,
                    ListingId = entry.PacketSerialNumber > 0 ? entry.PacketSerialNumber : row.SerialNumber,
                    ItemId = entry.RewardItemId > 0 ? entry.RewardItemId : row.ItemId,
                    Quantity = Math.Max(1, entry.RewardQuantity),
                    Price = (int)Math.Clamp(Math.Abs((long)entry.Price), 0L, int.MaxValue)
                });
            }

            return entries;
        }

        public string GetPacketOwnedCashShopStageCatalogSummary()
        {
            return _packetOwnedAdminShopSession.HasObservableState
                ? BuildPacketOwnedAdminShopStateSummary()
                : string.Empty;
        }

        public bool TryBeginPacketOwnedAdminShopSession(byte[] payload, out string message)
        {
            message = "Packet-owned admin-shop payload could not be decoded.";
            if (!AdminShopPacketOwnedOpenCodec.TryDecode(payload, out AdminShopPacketOwnedOpenPayloadSnapshot snapshot))
            {
                return false;
            }

            return TryBeginPacketOwnedAdminShopSession(snapshot, out message);
        }

        internal bool TryBeginPacketOwnedAdminShopSession(AdminShopPacketOwnedOpenPayloadSnapshot snapshot, out string message)
        {
            message = "Packet-owned admin-shop payload could not be decoded.";
            if (snapshot == null)
            {
                return false;
            }

            bool preservePacketOwnedUserSelection = TryCapturePacketOwnedSetUserItemsSelection(
                out InventoryType preservedUserSelectionInventoryType,
                out int preservedUserSelectionItemId,
                out int preservedUserSelectionSlotPosition,
                out int preservedUserSelectionScrollOffset);
            _packetOwnedAdminShopSession.BeginOpen(
                snapshot,
                "CAdminShopDlg::OnPacket reused the admin-shop unique-modeless owner surface.");
            _packetOwnedAdminShopRows.Clear();
            _packetOwnedAdminShopRows.AddRange(snapshot.Rows);
            ClearPendingPacketOwnedUserSellSnapshot();
            ClearPendingPacketOwnedWishlistRegister();
            ResetMode(AdminShopServiceMode.CashShop);
            RestorePacketOwnedOpenViewState(AdminShopPacketOwnedOpenViewParity.ResolveDefaultForSetAdminShopDlg());
            if (preservePacketOwnedUserSelection)
            {
                TryApplyPacketOwnedSetUserItemsOpenParity(
                    preservedUserSelectionInventoryType,
                    preservedUserSelectionItemId,
                    preservedUserSelectionSlotPosition,
                    preservedUserSelectionScrollOffset);
            }
            _footerMessage = _packetOwnedAdminShopSession.NpcTemplateId > 0
                ? $"CAdminShopDlg::SetAdminShopDlg opened the packet-owned admin-shop owner for NPC {_packetOwnedAdminShopSession.NpcTemplateId} with {_packetOwnedAdminShopSession.DecodedItemCount} decoded row(s); wishlist prompt={(_packetOwnedAdminShopSession.AskItemWishlist ? "on" : "off")}."
                : $"CAdminShopDlg::SetAdminShopDlg opened the packet-owned admin-shop owner with {_packetOwnedAdminShopSession.DecodedItemCount} decoded row(s); wishlist prompt={(_packetOwnedAdminShopSession.AskItemWishlist ? "on" : "off")}.";
            UpdateActionButtonStates();
            message = _footerMessage;
            return true;
        }

        internal string BuildPacketOwnedAdminShopOwnerFooter()
        {
            return _packetOwnedAdminShopSession.HasObservableState
                ? BuildPacketOwnedAdminShopStateSummary()
                : _packetOwnedAdminShopSession.BuildTransportSummary();
        }

        internal void RecordPacketOwnedAdminShopOwnerSurfaceShown()
        {
            if (!_packetOwnedAdminShopSession.HasObservableState)
            {
                return;
            }

            _packetOwnedAdminShopSession.RecordOwnerSurfaceShown(
                "CAdminShopDlg unique-modeless owner surface is visible.");
        }

        internal void RecordPacketOwnedAdminShopOwnerSurfaceHidden(
            string ownerState,
            AdminShopPacketOwnedOwnerVisibilityState visibilityState = AdminShopPacketOwnedOwnerVisibilityState.Hidden)
        {
            if (!_packetOwnedAdminShopSession.HasObservableState)
            {
                return;
            }

            _packetOwnedAdminShopSession.RecordOwnerSurfaceHidden(ownerState, visibilityState);
        }

        internal string ApplyPacketOwnedAdminShopBlockedByUniqueModelessOwner(string blockingOwner, AdminShopPacketOwnedOpenPayloadSnapshot snapshot)
        {
            _packetOwnedAdminShopSession.RecordBlockedByOwner(snapshot, blockingOwner);
            _footerMessage = string.IsNullOrWhiteSpace(blockingOwner)
                ? "Packet 367 arrived while another unique-modeless owner was active, so the admin-shop owner stayed unchanged."
                : $"Packet 367 arrived while {blockingOwner} owned the unique-modeless slot, so the admin-shop owner stayed unchanged.";
            UpdateActionButtonStates();
            return _footerMessage;
        }

        public string ApplyPacketOwnedAdminShopOpenRejected(string noticeText)
        {
            _packetOwnedAdminShopRows.Clear();
            _packetOwnedAdminShopSellTemplates.Clear();
            string outboundSummary = DispatchPacketOwnedAdminShopOutbound(PacketOwnedAdminShopCloseMode, 0);
            _packetOwnedAdminShopSession.RejectOpen(
                noticeText,
                outboundSummary,
                "Packet 367 refusal closed the admin-shop unique-modeless owner.");
            ClearPendingPacketOwnedUserSellSnapshot();
            ClearPendingPacketOwnedWishlistRegister();
            ResetPendingRequestState();
            _footerMessage = string.IsNullOrWhiteSpace(noticeText)
                ? $"Packet 367 rejected the admin-shop open request. {outboundSummary}"
                : $"{noticeText} {outboundSummary}";
            UpdateActionButtonStates();
            return _footerMessage;
        }

        public bool TryApplyPacketOwnedAdminShopResult(byte subtype, byte resultCode, out string message, out string noticeText, out bool reopenRequested)
        {
            message = "Packet-owned admin-shop result could not be applied.";
            noticeText = string.Empty;
            reopenRequested = false;
            _packetOwnedAdminShopSession.RecordResultPacket(subtype, resultCode);
            _packetOwnedAdminShopSession.ClearWaitingForResult();

            if (!_packetOwnedAdminShopSession.IsActive)
            {
                _packetOwnedAdminShopSession.MarkDisconnectHazard();
                _packetOwnedAdminShopSession.SetLastOwnerState("Packet 366 arrived without a live admin-shop unique-modeless owner.");
                message = "Packet 366 arrived without an active packet-owned admin-shop session. The v95 client would disconnect on this owner-state mismatch.";
                _footerMessage = message;
                UpdateActionButtonStates();
                return true;
            }

            if (_pendingRequestEntry == null)
            {
                if (_pendingPacketOwnedWishlistRegisterEntry != null)
                {
                    return TryApplyPacketOwnedWishlistRegisterResult(subtype, resultCode, out message, out noticeText, out reopenRequested);
                }

                _packetOwnedAdminShopSession.MarkDisconnectHazard();
                _packetOwnedAdminShopSession.SetLastOwnerState("Packet 366 arrived after the admin-shop owner had already cleared m_bShopRequestSent.");
                message = "Packet 366 arrived without a pending admin-shop request. The v95 client throws CDisconnectException when m_bShopRequestSent is clear.";
                _footerMessage = message;
                UpdateActionButtonStates();
                return true;
            }

            _pendingPacketOwnedAdminShopResult = false;
            AdminShopEntry entry = _pendingRequestEntry;

            if (!AdminShopDialogClientParityText.HandlesResultSubtype(subtype))
            {
                RestorePendingRequestState(entry);
                message = AdminShopDialogClientParityText.BuildUnsupportedResultMessage(subtype, resultCode);
                ResetPendingRequestState();
                ClearPendingPacketOwnedUserSellSnapshot();
                _packetOwnedAdminShopSession.SetLastOwnerState("Packet 366 subtype was ignored by the admin-shop owner.");
                _footerMessage = message;
                UpdateActionButtonStates();
                return true;
            }

            if (resultCode == 0)
            {
                TryReselectPacketOwnedPendingEntry(entry);
                ResolveCatalogRequest(entry);
                _packetOwnedAdminShopSession.ClearLastNotice();
                _packetOwnedAdminShopSession.SetLastOwnerState("Packet 366 applied against the admin-shop owner and left the session open.");
                message = _footerMessage;
                return true;
            }

            if (AdminShopDialogClientParityText.TryGetResultNotice(resultCode, out string resolvedNotice, out reopenRequested))
            {
                noticeText = resolvedNotice;
                _packetOwnedAdminShopSession.SetLastNotice(resolvedNotice);
            }
            else
            {
                _packetOwnedAdminShopSession.ClearLastNotice();
            }

            if (reopenRequested)
            {
                RestorePendingRequestState(entry);
                ResetPendingRequestState();
                ClearPendingPacketOwnedUserSellSnapshot();
                string outboundSummary = DispatchPacketOwnedAdminShopOutbound(
                    PacketOwnedAdminShopResultMode,
                    _packetOwnedAdminShopSession.NpcTemplateId);
                _packetOwnedAdminShopSession.SetLastOutboundSummary(outboundSummary);
                _packetOwnedAdminShopSession.SetLastOwnerState("Packet 366 requested the admin-shop owner to reopen through opcode 74 mode 0.");
                _footerMessage = string.IsNullOrWhiteSpace(noticeText)
                    ? $"CAdminShopDlg result {resultCode} requested an authoritative packet 367 refresh. {outboundSummary}"
                    : $"CAdminShopDlg result {resultCode} requested an authoritative packet 367 refresh. {noticeText} {outboundSummary}";
            }
            else
            {
                string stateLabel = AdminShopDialogClientParityText.BuildResultStateLabel(resultCode);
                FinishRejectedRequest(
                    entry,
                    stateLabel,
                    $"CAdminShopDlg result {resultCode} rejected the request for {entry.Title}.",
                    refundAmount: 0L);
                if (!string.IsNullOrWhiteSpace(noticeText))
                {
                    _packetOwnedAdminShopSession.SetLastOwnerState("Packet 366 left the admin-shop owner open without a reopen acknowledgement.");
                    _footerMessage = $"{_footerMessage} {noticeText}";
                }
            }

            message = _footerMessage;
            UpdateActionButtonStates();
            return true;
        }

        public override void Hide()
        {
            bool wasVisible = IsVisible;
            if (wasVisible
                && _packetOwnedAdminShopSession.HasObservableState
                && _packetOwnedAdminShopSession.OwnerVisibilityState == AdminShopPacketOwnedOwnerVisibilityState.Visible)
            {
                _packetOwnedAdminShopSession.RecordOwnerSurfaceHidden(
                    "CAdminShopDlg unique-modeless owner surface is hidden.",
                    AdminShopPacketOwnedOwnerVisibilityState.Hidden);
            }

            base.Hide();
            if (wasVisible && !IsVisible)
            {
                WindowHidden?.Invoke(this);
            }
        }

        private void CloseFromUserInput()
        {
            if (_packetOwnedAdminShopSession.IsActive)
            {
                int npcTemplateId = _packetOwnedAdminShopSession.NpcTemplateId;
                string outboundSummary = DispatchPacketOwnedAdminShopOutbound(PacketOwnedAdminShopCloseMode, npcTemplateId);
                _packetOwnedAdminShopSession.RejectOpen(
                    string.Empty,
                    outboundSummary,
                    "The admin-shop unique-modeless owner was closed locally.",
                    AdminShopPacketOwnedOwnerVisibilityState.Hidden);
                _pendingPacketOwnedAdminShopResult = false;
                _packetOwnedAdminShopRows.Clear();
                _packetOwnedAdminShopSellTemplates.Clear();
                ClearPendingPacketOwnedUserSellSnapshot();
                ResetPendingRequestState();
                _footerMessage = string.IsNullOrWhiteSpace(outboundSummary)
                    ? "Closed the packet-owned admin-shop owner."
                    : $"Closed the packet-owned admin-shop owner. {outboundSummary}";
            }

            Hide();
        }

        public AdminShopAvatarPreviewSelection GetAvatarPreviewSelection()
        {
            AdminShopEntry entry = GetSelectedEntry();
            if (entry == null)
            {
                return null;
            }

            return new AdminShopAvatarPreviewSelection
            {
                Title = entry.Title ?? string.Empty,
                Detail = entry.Detail ?? string.Empty,
                RewardInventoryType = entry.RewardInventoryType,
                RewardItemId = entry.RewardItemId,
                IsUserListing = _activePane == AdminShopPane.User
            };
        }

        public IReadOnlyList<string> DescribeListOwnerState()
        {
            AdminShopPaneState activePaneState = _paneStates[_activePane];
            AdminShopEntry entry = GetSelectedEntry();
            List<string> lines = new()
            {
                $"{_currentMode} {_activePane} pane: {activePaneState.Entries.Count} row(s), browse {GetBrowseModeLabel(_activeBrowseMode)}, category {_activeCategory}.",
                entry == null
                    ? "No catalog entry is currently selected."
                    : $"{entry.Title} | {entry.PriceLabel} | {entry.StateLabel}",
                entry == null
                    ? "Select a Cash Shop row to preview client-owned list details."
                    : entry.Detail
            };

            if (!string.IsNullOrWhiteSpace(_footerMessage))
            {
                lines.Add(_footerMessage);
            }

            if (_packetOwnedAdminShopSession.HasObservableState)
            {
                lines.Add(BuildPacketOwnedAdminShopStateSummary());
            }

            return lines;
        }

        public ListOwnerSnapshot GetListOwnerSnapshot()
        {
            AdminShopPaneState activePaneState = _paneStates[_activePane];
            int scrollOffset = Math.Clamp(activePaneState.ScrollOffset, 0, Math.Max(0, activePaneState.Entries.Count - 1));
            int selectedIndex = activePaneState.SelectedIndex;
            List<OwnerEntrySnapshot> visibleEntries = new();
            for (int row = 0; row < MaxVisibleRows; row++)
            {
                int entryIndex = scrollOffset + row;
                if (entryIndex >= activePaneState.Entries.Count)
                {
                    break;
                }

                AdminShopEntry entry = activePaneState.Entries[entryIndex];
                visibleEntries.Add(new OwnerEntrySnapshot
                {
                    Title = entry.Title ?? string.Empty,
                    Detail = entry.Detail ?? string.Empty,
                    Seller = entry.Seller ?? string.Empty,
                    PriceLabel = entry.PriceLabel ?? string.Empty,
                    StateLabel = entry.StateLabel ?? string.Empty,
                    IsSelected = entryIndex == selectedIndex,
                    CommoditySerialNumber = Math.Max(0, entry.CommoditySerialNumber),
                    RewardItemId = Math.Max(0, entry.RewardItemId),
                    RewardQuantity = Math.Max(1, entry.RewardQuantity),
                    CommodityOnSale = entry.CommodityOnSale
                });
            }

            return new ListOwnerSnapshot
            {
                PaneLabel = _activePane.ToString(),
                BrowseModeLabel = GetBrowseModeLabel(_activeBrowseMode),
                CategoryLabel = _activeCategory.ToString(),
                FooterMessage = _footerMessage ?? string.Empty,
                SelectedIndex = selectedIndex,
                ScrollOffset = scrollOffset,
                TotalCount = activePaneState.Entries.Count,
                VisibleEntries = visibleEntries
            };
        }

        public IReadOnlyList<string> DescribeLockerOwnerState()
        {
            if (_storageRuntime == null)
            {
                return new[]
                {
                    "Locker runtime unavailable.",
                    "The dedicated locker owner is staged but has no shared-account storage snapshot."
                };
            }

            return new[]
            {
                $"Account {_storageRuntime.AccountLabel} uses {_storageRuntime.GetUsedSlotCount()}/{_storageRuntime.GetSlotLimit()} shared slots.",
                _storageRuntime.SharedCharacterNames.Count > 0
                    ? $"Shared with {string.Join(", ", _storageRuntime.SharedCharacterNames.Take(3))}."
                    : "No shared character names are loaded for the locker owner.",
                _storageRuntime.CanExpandSlotLimit()
                    ? "Locker expansion remains available."
                    : $"Locker expansion has reached the simulator cap at {_storageRuntime.GetSlotLimit()} slots."
            };
        }

        public LockerOwnerSnapshot GetLockerOwnerSnapshot()
        {
            if (_storageRuntime == null)
            {
                return new LockerOwnerSnapshot();
            }

            return new LockerOwnerSnapshot
            {
                AccountLabel = _storageRuntime.AccountLabel ?? string.Empty,
                UsedSlotCount = _storageRuntime.GetUsedSlotCount(),
                SlotLimit = _storageRuntime.GetSlotLimit(),
                CanExpand = _storageRuntime.CanExpandSlotLimit(),
                SharedCharacterNames = _storageRuntime.SharedCharacterNames?.ToArray() ?? Array.Empty<string>()
            };
        }

        public IReadOnlyList<string> DescribeInventoryOwnerState()
        {
            if (_inventory == null)
            {
                return new[]
                {
                    "Inventory runtime unavailable.",
                    "CCSWnd_Inventory remains present but has no item runtime to enumerate."
                };
            }

            return new[]
            {
                $"Equip {_inventory.GetSlots(InventoryType.EQUIP).Count}, Use {_inventory.GetSlots(InventoryType.USE).Count}, Setup {_inventory.GetSlots(InventoryType.SETUP).Count}.",
                $"Etc {_inventory.GetSlots(InventoryType.ETC).Count}, Cash {_inventory.GetSlots(InventoryType.CASH).Count}.",
                $"Selected cash row: {GetSelectedEntry()?.Title ?? "none"}."
            };
        }

        public InventoryOwnerSnapshot GetInventoryOwnerSnapshot()
        {
            if (_inventory == null)
            {
                return new InventoryOwnerSnapshot();
            }

            return new InventoryOwnerSnapshot
            {
                EquipCount = _inventory.GetSlots(InventoryType.EQUIP).Count,
                UseCount = _inventory.GetSlots(InventoryType.USE).Count,
                SetupCount = _inventory.GetSlots(InventoryType.SETUP).Count,
                EtcCount = _inventory.GetSlots(InventoryType.ETC).Count,
                CashCount = _inventory.GetSlots(InventoryType.CASH).Count,
                SelectedEntryTitle = GetSelectedEntry()?.Title ?? string.Empty
            };
        }

        public string SubmitSelectedEntryPreviewRequest()
        {
            SubmitSelectedEntryRequest();
            return _footerMessage;
        }

        public string ExecuteCashStageListAction(string actionKey)
        {
            AdminShopEntry entry = GetSelectedEntry();
            if (entry == null)
            {
                _footerMessage = "Select a Cash Shop row before using the dedicated list-owner buttons.";
                UpdateActionButtonStates();
                return _footerMessage;
            }

            switch (actionKey)
            {
                case "BtBuy":
                    SubmitSelectedEntryRequest();
                    return _footerMessage;
                case "BtGift":
                    _footerMessage = _activePane == AdminShopPane.User
                        ? $"CCSWnd_List staged the selected user listing for the client gift-preview path: {entry.Title}."
                        : $"CCSWnd_List staged the selected NPC offer for the client gift-preview path: {entry.Title}.";
                    UpdateActionButtonStates();
                    return _footerMessage;
                case "BtReserve":
                    return SetSelectedEntryWishlistState(true);
                case "BtRemove":
                    return SetSelectedEntryWishlistState(false);
                default:
                    _footerMessage = $"Unknown cash-stage list action '{actionKey}'.";
                    UpdateActionButtonStates();
                    return _footerMessage;
            }
        }

        public string MoveListOwnerSelection(int delta)
        {
            if (delta == 0)
            {
                return _footerMessage;
            }

            MoveSelection(delta);
            return _footerMessage;
        }

        public string SelectListOwnerVisibleRow(int visibleRowIndex)
        {
            AdminShopPaneState paneState = _paneStates[_activePane];
            if (paneState.Entries.Count == 0)
            {
                _footerMessage = "CCSWnd_List has no entries to select on the active pane.";
                UpdateActionButtonStates();
                return _footerMessage;
            }

            int clampedRow = Math.Clamp(visibleRowIndex, 0, MaxVisibleRows - 1);
            int absoluteIndex = paneState.ScrollOffset + clampedRow;
            if (absoluteIndex < 0 || absoluteIndex >= paneState.Entries.Count)
            {
                _footerMessage = $"CCSWnd_List row {visibleRowIndex.ToString(CultureInfo.InvariantCulture)} is outside the active list range.";
                UpdateActionButtonStates();
                return _footerMessage;
            }

            SelectAbsoluteIndex(absoluteIndex);
            return _footerMessage;
        }

        public string MoveListOwnerSelectionByPage(int direction)
        {
            if (direction == 0)
            {
                return _footerMessage;
            }

            MoveSelection(direction * MaxVisibleRows);
            return _footerMessage;
        }

        public string SelectListOwnerBoundary(bool toEnd)
        {
            AdminShopPaneState paneState = _paneStates[_activePane];
            if (paneState.Entries.Count == 0)
            {
                _footerMessage = "CCSWnd_List has no entries to select on the active pane.";
                UpdateActionButtonStates();
                return _footerMessage;
            }

            SelectAbsoluteIndex(toEnd ? paneState.Entries.Count - 1 : 0);
            return _footerMessage;
        }

        public string ScrollListOwnerToOffset(int scrollOffset, int focusRowIndex)
        {
            AdminShopPaneState paneState = _paneStates[_activePane];
            if (paneState.Entries.Count == 0)
            {
                _footerMessage = "CCSWnd_List has no entries to scroll on the active pane.";
                UpdateActionButtonStates();
                return _footerMessage;
            }

            int maxScroll = Math.Max(0, paneState.Entries.Count - MaxVisibleRows);
            paneState.ScrollOffset = Math.Clamp(scrollOffset, 0, maxScroll);
            int visibleCount = Math.Min(MaxVisibleRows, Math.Max(0, paneState.Entries.Count - paneState.ScrollOffset));
            int clampedFocusRow = Math.Clamp(focusRowIndex, 0, Math.Max(0, visibleCount - 1));
            paneState.SelectedIndex = Math.Clamp(paneState.ScrollOffset + clampedFocusRow, 0, paneState.Entries.Count - 1);
            _pendingModalEntry = null;
            ClampPaneState(paneState);
            PersistBrowseSurfaceState(_activePane);
            _footerMessage = BuildSelectionMessage(paneState.Entries[paneState.SelectedIndex], _activePane);
            UpdateActionButtonStates();
            return $"CCSWnd_List dragged the dedicated selector to rows {paneState.ScrollOffset + 1}-{Math.Min(paneState.ScrollOffset + MaxVisibleRows, paneState.Entries.Count)} on the {_activePane} pane. {_footerMessage}";
        }

        public string ToggleListOwnerPane()
        {
            AdminShopPane previousPane = _activePane;
            SwitchActivePane();
            return previousPane == _activePane
                ? "CCSWnd_List could not switch panes because the alternate owner is empty."
                : _footerMessage;
        }

        public void SetStorageRuntime(IStorageRuntime storageRuntime)
        {
            _storageRuntime = storageRuntime;
            UpdateActionButtonStates();
        }

        public string GetWishlistSuggestedQuery()
        {
            AdminShopEntry entry = GetSelectedEntry();
            return entry?.SupportsWishlist == true ? entry.Title : string.Empty;
        }

        public int GetWishlistSuggestedCategoryIndex()
        {
            for (int i = 0; i < _categoryTabs.Length; i++)
            {
                if (GetFullCategory(i) == _activeCategory)
                {
                    return i;
                }
            }

            return 0;
        }

        public int GetWishlistSuggestedPriceRangeIndex()
        {
            AdminShopEntry entry = GetSelectedEntry();
            if (entry == null)
            {
                return 0;
            }

            for (int i = 0; i < _wishlistPriceRanges.Count; i++)
            {
                if (MatchesWishlistPriceRange(entry, i))
                {
                    return i;
                }
            }

            return 0;
        }

        public string GetWishlistServiceName()
        {
            return _currentMode == AdminShopServiceMode.CashShop ? "Cash Shop" : "MTS";
        }

        public IReadOnlyList<WishlistPriceRange> GetWishlistPriceRanges()
        {
            return _wishlistPriceRanges;
        }

        public IReadOnlyList<WishlistCategoryNode> GetWishlistCategoryTree()
        {
            return s_wishlistCategoryTree;
        }

        public string GetWishlistSuggestedCategoryKey()
        {
            return GetWishlistCategoryKey(GetWishlistSuggestedCategoryIndex());
        }

        public string GetWishlistCategoryLabel(string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
            {
                return "All";
            }

            return s_wishlistCategoryNodesByKey.TryGetValue(categoryKey, out WishlistCategoryNode node)
                ? node.Label
                : GetCategoryLabel(ResolveWishlistCategory(categoryKey));
        }

        public IReadOnlyList<WishlistSearchResult> SearchWishlistEntries(string query, int categoryIndex, int priceRangeIndex, out string message)
        {
            return SearchWishlistEntries(query, GetWishlistCategoryKey(categoryIndex), priceRangeIndex, out message);
        }

        public IReadOnlyList<WishlistSearchResult> SearchWishlistEntries(string query, string categoryKey, int priceRangeIndex, out string message)
        {
            message = "Enter an item name before searching the wish list.";
            string trimmedQuery = query?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedQuery))
            {
                _footerMessage = message;
                UpdateActionButtonStates();
                return Array.Empty<WishlistSearchResult>();
            }

            string clientSearchQuery = BuildClientWishlistSearchQuery(trimmedQuery);
            if (string.IsNullOrWhiteSpace(clientSearchQuery))
            {
                message = "Enter an item name before searching the wish list.";
                _footerMessage = message;
                UpdateActionButtonStates();
                return Array.Empty<WishlistSearchResult>();
            }

            AdminShopCategory requestedCategory = ResolveWishlistCategory(categoryKey);
            string requestedCategoryLabel = GetWishlistCategoryLabel(categoryKey);
            List<(AdminShopEntry Entry, int Score)> matches = _paneStates[AdminShopPane.Npc]
                .SourceEntries
                .Where(entry => entry.SupportsWishlist
                                && MatchesWishlistCategory(entry, categoryKey)
                                && MatchesWishlistPriceRange(entry, priceRangeIndex))
                .Select(entry => (Entry: entry, Score: ScoreWishlistEntry(entry, clientSearchQuery)))
                .Where(match => match.Score > 0)
                .OrderByDescending(match => match.Score)
                .ThenBy(match => match.Entry.Price)
                .ThenBy(match => match.Entry.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int alreadyWishlistedCount = matches.Count(match =>
                match.Entry.Wishlisted || _wishlistedEntryKeys[_currentMode].Contains(GetEntryKey(match.Entry)));
            if (matches.Count == 0)
            {
                message = alreadyWishlistedCount > 0
                    ? $"SearchItemName only found rows that are already saved for \"{trimmedQuery}\" in {requestedCategoryLabel} / {GetWishlistPriceRangeLabel(priceRangeIndex)}."
                    : $"No wish-list results were found for \"{trimmedQuery}\" in {requestedCategoryLabel} / {GetWishlistPriceRangeLabel(priceRangeIndex)}.";
                _footerMessage = message;
                UpdateActionButtonStates();
                return Array.Empty<WishlistSearchResult>();
            }

            List<WishlistSearchResult> results = matches
                .Select(match => BuildWishlistSearchResult(match.Entry, match.Score))
                .ToList();

            message = alreadyWishlistedCount > 0
                ? $"SearchItemName staged {results.Count} result(s) for {GetWishlistServiceName()} in {requestedCategoryLabel} / {GetWishlistPriceRangeLabel(priceRangeIndex)}; {alreadyWishlistedCount} row(s) are already saved."
                : $"SearchItemName staged {results.Count} result(s) for {GetWishlistServiceName()} in {requestedCategoryLabel} / {GetWishlistPriceRangeLabel(priceRangeIndex)}.";
            _footerMessage = message;
            UpdateActionButtonStates();
            return results;
        }

        public IReadOnlyList<WishlistSearchResult> GetWishlistCategoryResults(string categoryKey, out string message)
        {
            AdminShopCategory requestedCategory = ResolveWishlistCategory(categoryKey);
            string requestedCategoryLabel = GetWishlistCategoryLabel(categoryKey);
            List<WishlistSearchResult> results = _paneStates[AdminShopPane.Npc]
                .SourceEntries
                .Where(entry => entry.SupportsWishlist && MatchesWishlistCategory(entry, categoryKey))
                .OrderBy(entry => entry.Price)
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                .Select(entry => BuildWishlistSearchResult(entry, 0))
                .Where(result => result != null)
                .ToList();

            if (results.Count == 0)
            {
                message = $"No wish-list category results were found for {requestedCategoryLabel}.";
                _footerMessage = message;
                UpdateActionButtonStates();
                return Array.Empty<WishlistSearchResult>();
            }

            int alreadyWishlistedCount = results.Count(result => result.AlreadyWishlisted);
            message = alreadyWishlistedCount > 0
                ? $"CUIAdminShopWishListCategory staged {results.Count} {requestedCategoryLabel} result(s); {alreadyWishlistedCount} row(s) are already saved."
                : $"CUIAdminShopWishListCategory staged {results.Count} {requestedCategoryLabel} result(s).";
            _footerMessage = message;
            UpdateActionButtonStates();
            _ = requestedCategory;
            return results;
        }

        public bool TryResolveWishlistSearchResult(string entryKey, out WishlistSearchResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(entryKey))
            {
                return false;
            }

            AdminShopEntry matchedEntry = _paneStates[AdminShopPane.Npc]
                .SourceEntries
                .FirstOrDefault(entry => string.Equals(GetEntryKey(entry), entryKey, StringComparison.Ordinal));
            if (matchedEntry == null)
            {
                return false;
            }

            if (!matchedEntry.SupportsWishlist)
            {
                return false;
            }

            result = BuildWishlistSearchResult(matchedEntry, 0);
            return result != null;
        }

        public bool TryApplyWishlistSearchResult(string entryKey, int categoryIndex, out string message)
        {
            return TryApplyWishlistSearchResult(entryKey, GetWishlistCategoryKey(categoryIndex), out message);
        }

        public bool TryApplyWishlistSearchResult(string entryKey, string categoryKey, out string message)
        {
            message = "Wish-list result selection is unavailable.";
            if (string.IsNullOrWhiteSpace(entryKey))
            {
                _footerMessage = message;
                UpdateActionButtonStates();
                return false;
            }

            AdminShopEntry matchedEntry = _paneStates[AdminShopPane.Npc]
                .SourceEntries
                .FirstOrDefault(entry => string.Equals(GetEntryKey(entry), entryKey, StringComparison.Ordinal));
            if (matchedEntry == null)
            {
                message = "The selected wish-list result no longer maps to an NPC catalog row.";
                _footerMessage = message;
                UpdateActionButtonStates();
                return false;
            }

            if (!matchedEntry.SupportsWishlist)
            {
                message = "The selected wish-list result is no longer available for wish-list registration.";
                _footerMessage = message;
                UpdateActionButtonStates();
                return false;
            }

            return TryFocusWishlistEntry(matchedEntry, ResolveWishlistCategory(categoryKey), out message);
        }

        public string GetWishlistSearchCatalogSessionSignature()
        {
            return BuildWishlistSearchCatalogSessionSignature(_paneStates[AdminShopPane.Npc].SourceEntries, _currentMode);
        }

        public string GetWishlistSearchServiceStateSignature()
        {
            string catalogSignature = GetWishlistSearchCatalogSessionSignature();
            return _packetOwnedAdminShopSession.HasObservableState
                ? string.Concat(catalogSignature, "|", _packetOwnedAdminShopSession.BuildWishlistSearchSessionSignature())
                : catalogSignature;
        }

        public string GetWishlistSearchServiceStateSummary()
        {
            return _packetOwnedAdminShopSession.BuildWishlistSearchStateSummary();
        }

        public IReadOnlyList<string> GetWishlistSearchServiceStateDetailLines()
        {
            return _packetOwnedAdminShopSession.BuildWishlistSearchStateDetailLines();
        }

        public bool TrySubmitWishlistSearch(string query, int categoryIndex, out string message)
        {
            IReadOnlyList<WishlistSearchResult> results = SearchWishlistEntries(query, categoryIndex, 0, out message);
            if (results.Count == 0)
            {
                return false;
            }

            return TryApplyWishlistSearchResult(results[0].EntryKey, categoryIndex, out message);
        }

        private string SetSelectedEntryWishlistState(bool wishlisted)
        {
            AdminShopEntry entry = GetSelectedEntry();
            if (entry == null)
            {
                _footerMessage = "No Cash Shop entry is selected for the wish-list owner.";
                UpdateActionButtonStates();
                return _footerMessage;
            }

            if (_activePane != AdminShopPane.Npc || !entry.SupportsWishlist)
            {
                _footerMessage = $"{entry.Title} does not route through the client wish-list owner seam.";
                UpdateActionButtonStates();
                return _footerMessage;
            }

            string entryKey = GetEntryKey(entry);
            HashSet<string> wishlistedEntries = _wishlistedEntryKeys[_currentMode];
            if (wishlisted)
            {
                entry.Wishlisted = true;
                wishlistedEntries.Add(entryKey);
                _footerMessage = $"CCSWnd_List staged {entry.Title} for the dedicated wish-list owner.";
            }
            else
            {
                entry.Wishlisted = false;
                wishlistedEntries.Remove(entryKey);
                _footerMessage = $"CCSWnd_List cleared {entry.Title} from the dedicated wish-list owner.";
            }

            PersistEntrySessionState(entry);
            UpdateActionButtonStates();
            return _footerMessage;
        }

        public override void Show()
        {
            base.Show();
            MouseState mouseState = Mouse.GetState();
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
            _previousMouseState = mouseState;
            _previousKeyboardState = Keyboard.GetState();

            if (_packetOwnedAdminShopSession.IsActive)
            {
                _footerMessage = BuildPacketOwnedAdminShopStateSummary();
                UpdateRowButtons();
                UpdateActionButtonStates();
                UpdateModalButtons();
                return;
            }

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
                matchedEntry = CreateSyntheticCommodityEntry(commodity);
                if (matchedEntry == null)
                {
                    _footerMessage = $"Commodity SN {commoditySerialNumber} resolved to item {commodity.ItemId}, but the simulator could not build a WZ-backed catalog row.";
                    UpdateActionButtonStates();
                    return false;
                }

                _paneStates[AdminShopPane.Npc].SourceEntries.Add(matchedEntry);
                RestoreEntryFlags(new[] { matchedEntry }, _currentMode);
                RestoreEntryStates(new[] { matchedEntry }, _currentMode);
                PopulateEntryIcons(new[] { matchedEntry });
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
            _pendingModalEntry = null;
            _footerMessage = $"Focused packet-owned commodity SN {commoditySerialNumber} on {matchedEntry.Title}.";
            PersistBrowseSurfaceState(AdminShopPane.Npc);
            UpdateRowButtons();
            UpdateActionButtonStates();
            return true;
        }

        public static bool TryResolveBestCommoditySerialNumberForItem(int itemId, out int commoditySerialNumber, out long price)
        {
            commoditySerialNumber = 0;
            price = 0;
            if (itemId <= 0 || !TryGetBestCommodityForItem(itemId, out AdminShopCommodityData commodity) || commodity == null)
            {
                return false;
            }

            commoditySerialNumber = Math.Max(0, commodity.SerialNumber);
            price = Math.Max(0L, commodity.Price);
            return commoditySerialNumber > 0;
        }

        public static bool TryResolveCommodityBySerialNumber(int commoditySerialNumber, out int itemId, out long price, out int count, out bool onSale)
        {
            itemId = 0;
            price = 0;
            count = 0;
            onSale = false;
            if (commoditySerialNumber <= 0 || !TryGetCommodityBySerialNumber(commoditySerialNumber, out AdminShopCommodityData commodity) || commodity == null)
            {
                return false;
            }

            itemId = Math.Max(0, commodity.ItemId);
            price = Math.Max(0L, commodity.Price);
            count = Math.Max(1, commodity.Count);
            onSale = commodity.OnSale;
            return itemId > 0;
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

            if (_pendingRequestEntry != null
                && !_packetOwnedAdminShopSession.IsWaitingForResult
                && Environment.TickCount >= _requestResolveTick)
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

            if (_modalVisible)
            {
                if (_modalMode == AdminShopModalMode.RequestQuantity
                    && wheelDelta != 0
                    && GetModalBounds(Position.X, Position.Y).Contains(mouseState.Position))
                {
                    AdjustModalQuantity(wheelDelta > 0 ? 1 : -1);
                }

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
            PersistBrowseSurfaceState(hoveredPane.Value);
            UpdateRowButtons();
            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            if (_modalVisible)
            {
                if (_modalMode == AdminShopModalMode.RequestQuantity)
                {
                    if (WasPressed(keyboardState, Keys.Left) || WasPressed(keyboardState, Keys.Down))
                    {
                        AdjustModalQuantity(-1);
                    }
                    else if (WasPressed(keyboardState, Keys.Right) || WasPressed(keyboardState, Keys.Up))
                    {
                        AdjustModalQuantity(1);
                    }
                    else if (WasPressed(keyboardState, Keys.PageUp))
                    {
                        AdjustModalQuantity(-5);
                    }
                    else if (WasPressed(keyboardState, Keys.PageDown))
                    {
                        AdjustModalQuantity(5);
                    }
                    else if (WasPressed(keyboardState, Keys.Home))
                    {
                        SetModalQuantity(_modalQuantityMin);
                    }
                    else if (WasPressed(keyboardState, Keys.End))
                    {
                        SetModalQuantity(_modalQuantityMax);
                    }
                }

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
                CloseFromUserInput();
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
            _pendingModalEntry = null;
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
            _pendingModalEntry = null;
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
            DrawPane(sprite, windowX, windowY, RightPaneX, AdminShopPane.User, "User items");
            DrawScrollBar(sprite, windowX, windowY, AdminShopPane.Npc);
            DrawScrollBar(sprite, windowX, windowY, AdminShopPane.User);
            DrawFooter(sprite, windowX, windowY);
            DrawMoney(sprite, windowX, windowY);

            if (_modalVisible)
            {
                DrawModal(sprite, windowX, windowY);
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
                sprite.DrawString(_font, "Select an NPC offer or user item.", new Vector2(windowX + DetailX, windowY + DetailY), Color.White);
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
            sprite.DrawString(_font, GetEntryStateText(entry), new Vector2(detailTextX, windowY + DetailY + 36), GetStateColor(entry, false));

            float detailY = windowY + DetailY + 54;
            foreach (string line in WrapText(entry.Detail, 400f))
            {
                sprite.DrawString(_font, line, new Vector2(windowX + DetailX, detailY), new Color(218, 218, 218));
                detailY += 16f;
            }

            string wishState = RequiresInventorySource(entry)
                ? BuildSourceRequirementStatus(entry)
                : entry.Wishlisted ? "Wish list: saved." : "Wish list: not saved.";
            sprite.DrawString(_font, wishState, new Vector2(windowX + DetailX, windowY + DetailY + 70), new Color(175, 220, 175));

            if (!string.IsNullOrWhiteSpace(_footerMessage))
            {
                sprite.DrawString(_font, _footerMessage, new Vector2(windowX + DetailX, windowY + DetailY + 86), new Color(255, 221, 143));
            }
        }

        private void DrawModal(SpriteBatch sprite, int windowX, int windowY)
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

            float lineY = modalBounds.Y + 8f;
            foreach (string line in WrapText(_modalMessage, ModalTextMaxWidth))
            {
                Vector2 lineSize = _font.MeasureString(line);
                float lineX = modalBounds.X + (modalBounds.Width - lineSize.X) / 2f;
                sprite.DrawString(_font, line, new Vector2(lineX, lineY), new Color(55, 39, 15));
                lineY += 14f;
            }

            if (_modalMode == AdminShopModalMode.RequestQuantity && _pendingModalEntry != null)
            {
                string quantityText = $"Qty: {_modalQuantity} / {_modalQuantityMax}";
                Vector2 quantitySize = _font.MeasureString(quantityText);
                sprite.DrawString(
                    _font,
                    quantityText,
                    new Vector2(modalBounds.X + (modalBounds.Width - quantitySize.X) / 2f, modalBounds.Y + 24f),
                    new Color(48, 68, 113));

                string totalText = BuildRequestQuantitySummary(_pendingModalEntry, _modalQuantity);
                Vector2 totalSize = _font.MeasureString(totalText);
                sprite.DrawString(
                    _font,
                    totalText,
                    new Vector2(modalBounds.X + (modalBounds.Width - totalSize.X) / 2f, modalBounds.Y + 38f),
                    new Color(102, 61, 23));
            }
        }

        private void DrawMoney(SpriteBatch sprite, int windowX, int windowY)
        {
            if (_mesoTexture != null)
            {
                sprite.Draw(_mesoTexture, new Vector2(windowX + MoneyIconX, windowY + MoneyIconY), Color.White);
            }

            long displayedBalance = _currentMode == AdminShopServiceMode.CashShop && GetSelectedEntry()?.IsStorageExpansion == true
                ? _nexonCash
                : Money;
            sprite.DrawString(_font, displayedBalance.ToString("N0", CultureInfo.InvariantCulture), new Vector2(windowX + MoneyTextX, windowY + MoneyTextY), Color.White);
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

            if (TryOpenRequestQuantityModal(entry))
            {
                return;
            }

            if (TryOpenRequestConfirmationModal(entry))
            {
                return;
            }

            BeginPendingRequest(entry, 1);
        }

        private void OnRechargeButtonClicked(UIObject sender)
        {
            AdminShopEntry entry = GetSelectedEntry();
            if (entry == null)
            {
                _footerMessage = "Select an NPC offer before opening the wish-list owner.";
                UpdateActionButtonStates();
                return;
            }

            if (!entry.SupportsWishlist)
            {
                _footerMessage = "Only NPC offers can be searched through the wish-list owner.";
                UpdateActionButtonStates();
                return;
            }

            if (WishlistWindowRequested == null)
            {
                _footerMessage = "Wish-list owner is unavailable for this simulator session.";
                UpdateActionButtonStates();
                return;
            }

            OpenWishlistConfirmation(entry);
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

            bool wasSelected = _activePane == pane && paneState.SelectedIndex == actualIndex;
            _activePane = pane;
            paneState.SelectedIndex = actualIndex;
            _pendingModalEntry = null;
            AdminShopEntry selectedEntry = paneState.Entries[actualIndex];
            string selectedEntryKey = GetEntryKey(selectedEntry);
            bool isDoubleClick = wasSelected
                && _lastClickedPane == pane
                && string.Equals(_lastClickedEntryKey, selectedEntryKey, StringComparison.Ordinal)
                && unchecked(Environment.TickCount - _lastRowClickTick) <= DoubleClickThresholdMilliseconds;
            _footerMessage = BuildSelectionMessage(selectedEntry, pane);
            ClampPaneState(paneState);
            PersistBrowseSurfaceState(pane);
            UpdateActionButtonStates();
            _lastClickedPane = pane;
            _lastClickedEntryKey = selectedEntryKey;
            _lastRowClickTick = Environment.TickCount;

            if (isDoubleClick)
            {
                SubmitSelectedEntryRequest();
            }
        }

        private void ResetMode(AdminShopServiceMode mode)
        {
            _currentMode = mode;
            _activePane = AdminShopPane.Npc;
            _activeCategory = AdminShopCategory.All;
            _activeBrowseMode = AdminShopBrowseMode.All;
            _pendingModalEntry = null;
            _modalVisible = false;
            ResetPendingRequestState();
            _modalMode = AdminShopModalMode.None;
            _modalQuantity = 1;
            _modalQuantityMin = 1;
            _modalQuantityMax = 1;
            ClearPendingPacketOwnedUserSellSnapshot();
            _packetOwnedAdminShopSellTemplates.Clear();
            _paneStates[AdminShopPane.Npc].SourceEntries.Clear();
            _paneStates[AdminShopPane.User].SourceEntries.Clear();
            PopulateSourceEntries(mode);
            if (!UsesPacketOwnedAdminShopCatalog(mode))
            {
                ApplyCommodityMetadata(_paneStates[AdminShopPane.Npc].SourceEntries, mode);
            }
            RestoreEntryFlags(_paneStates[AdminShopPane.Npc].SourceEntries, mode);
            RestoreEntryFlags(_paneStates[AdminShopPane.User].SourceEntries, mode);
            RestoreEntryStates(_paneStates[AdminShopPane.Npc].SourceEntries, mode);
            RestoreEntryStates(_paneStates[AdminShopPane.User].SourceEntries, mode);
            PopulateEntryIcons(_paneStates[AdminShopPane.Npc].SourceEntries);
            PopulateEntryIcons(_paneStates[AdminShopPane.User].SourceEntries);
            ApplyFilters();
            _footerMessage = _packetOwnedAdminShopSession.HasObservableState
                ? BuildPacketOwnedAdminShopStateSummary()
                : BuildSelectionMessage(GetSelectedEntry(), _activePane);
            UpdateRowButtons();
            UpdateActionButtonStates();
            UpdateModalButtons();
        }

        private void ApplyFilters(bool preserveActivePane = false)
        {
            foreach (AdminShopPane pane in Enum.GetValues(typeof(AdminShopPane)))
            {
                AdminShopPaneState paneState = _paneStates[pane];
                AdminShopEntry selectedEntry = paneState.SelectedIndex >= 0 && paneState.SelectedIndex < paneState.Entries.Count
                    ? paneState.Entries[paneState.SelectedIndex]
                    : null;
                string selectedEntryKey = GetEntryKey(selectedEntry);

                paneState.Entries.Clear();
                foreach (AdminShopEntry entry in paneState.SourceEntries)
                {
                    if (ShouldIncludeEntry(entry, _activeCategory, _activeBrowseMode, pane))
                    {
                        paneState.Entries.Add(entry);
                    }
                }

                TryRestoreBrowseSurfaceState(pane, paneState);
                if (paneState.SelectedIndex < 0 && selectedEntry != null)
                {
                    paneState.SelectedIndex = paneState.Entries.IndexOf(selectedEntry);
                }

                if (paneState.SelectedIndex < 0 && !string.IsNullOrWhiteSpace(selectedEntryKey))
                {
                    paneState.SelectedIndex = paneState.Entries.FindIndex(entry =>
                        string.Equals(GetEntryKey(entry), selectedEntryKey, StringComparison.Ordinal));
                }

                if (paneState.SelectedIndex < 0 && paneState.Entries.Count > 0)
                {
                    paneState.SelectedIndex = 0;
                }

                ClampPaneState(paneState);
                PersistBrowseSurfaceState(pane);
            }

            if (!preserveActivePane && _paneStates[_activePane].Entries.Count == 0)
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
            if (entry == null)
            {
                return null;
            }

            int displayItemId = entry.DisplayItemId > 0
                ? entry.DisplayItemId
                : entry.RewardItemId;
            InventoryType displayInventoryType = entry.DisplayInventoryType != InventoryType.NONE
                ? entry.DisplayInventoryType
                : entry.RewardInventoryType;
            if (displayItemId <= 0)
            {
                return null;
            }

            if (_inventory != null && displayInventoryType != InventoryType.NONE)
            {
                Texture2D inventoryTexture = _inventory.GetItemTexture(displayInventoryType, displayItemId);
                if (inventoryTexture != null)
                {
                    return inventoryTexture;
                }
            }

            if (_itemIconCache.TryGetValue(displayItemId, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D loadedTexture = LoadItemIconTexture(displayItemId);
            if (loadedTexture != null)
            {
                _itemIconCache[displayItemId] = loadedTexture;
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

            int displayQuantity = ResolveDisplayQuantity(entry);
            if (displayQuantity <= 1)
            {
                return;
            }

            string quantityText = $"x{displayQuantity}";
            Vector2 quantitySize = _font.MeasureString(quantityText);
            sprite.DrawString(
                _font,
                quantityText,
                new Vector2(destination.Right - quantitySize.X, destination.Bottom - quantitySize.Y - 1f),
                new Color(255, 235, 169));
        }

        private static int ResolveDisplayQuantity(AdminShopEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            return entry.DisplayQuantity > 0
                ? entry.DisplayQuantity
                : entry.RewardQuantity;
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
            bool modalBlocked = _modalVisible;
            bool canSubmitRequest = !modalBlocked && entry != null && CanRequestEntry(entry);
            bool canOpenWishlist = !modalBlocked
                                   && WishlistWindowRequested != null
                                   && entry?.SupportsWishlist == true
                                   && entry.State == AdminShopEntryState.Available;
            _buyButton?.SetEnabled(canSubmitRequest);
            _sellButton?.SetEnabled(canSubmitRequest);
            _exitButton?.SetEnabled(!modalBlocked);
            _rechargeButton?.SetEnabled(canOpenWishlist);
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

                ApplyCommodityMetadata(entry, commodity);
            }
        }

        private void ApplyCommodityMetadata(AdminShopEntry entry, AdminShopCommodityData commodity)
        {
            if (entry == null || commodity == null)
            {
                return;
            }

            entry.Price = commodity.Price;
            entry.PriceLabel = FormatPriceLabel(commodity.Price);
            entry.RewardQuantity = Math.Max(1, commodity.Count);
            entry.CommoditySerialNumber = commodity.SerialNumber;
            entry.CommodityOnSale = commodity.OnSale;
            entry.SupportsWishlist &= commodity.OnSale;

            if (commodity.OnSale)
            {
                if (entry.State == AdminShopEntryState.PreviewOnly
                    && string.Equals(entry.StateLabel, "Off sale", StringComparison.Ordinal))
                {
                    entry.State = AdminShopEntryState.Available;
                    entry.StateLabel = string.Empty;
                }
            }
            else if (entry.State == AdminShopEntryState.Available)
            {
                entry.State = AdminShopEntryState.PreviewOnly;
                entry.StateLabel = "Off sale";
            }

            if (commodity.PeriodDays > 0
                && !entry.Detail.Contains("Period:", StringComparison.OrdinalIgnoreCase))
            {
                entry.Detail = string.IsNullOrWhiteSpace(entry.Detail)
                    ? $"Period: {commodity.PeriodDays} day(s)."
                    : $"{entry.Detail} Period: {commodity.PeriodDays} day(s).";
            }

            if (InventoryItemMetadataResolver.TryResolveMaxStackForItem(entry.RewardItemId, out int maxStackSize))
            {
                entry.RewardMaxStackSize = maxStackSize;
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

            if (_modalVisible)
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
            string[] quickCategoryLabels = { "Equip", "Use", "Set-up", "Etc", "Cash" };
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
                    PersistBrowseSurfaceState(pane);
                    UpdateRowButtons();
                    return true;
                }

                if (GetScrollDownButtonBounds(Position.X, Position.Y, pane).Contains(mouseState.X, mouseState.Y))
                {
                    paneState.ScrollOffset++;
                    ClampPaneState(paneState);
                    PersistBrowseSurfaceState(pane);
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
                    PersistBrowseSurfaceState(pane);
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
            PersistBrowseSurfaceState(pane);
            UpdateRowButtons();
        }

        private static int GetMaxScrollOffset(AdminShopPaneState paneState)
        {
            return Math.Max(0, paneState.Entries.Count - MaxVisibleRows);
        }

        private void OpenWishlistConfirmation(AdminShopEntry entry)
        {
            _pendingModalEntry = entry;
            _modalMode = AdminShopModalMode.WishlistOwnerConfirm;
            _modalVisible = true;
            _modalQuantity = 1;
            _modalQuantityMin = 1;
            _modalQuantityMax = 1;
            _modalMessage = $"Open the wish list for {entry.Title}?";
            _footerMessage = $"Wish-list confirmation opened for {entry.Title}.";
            PositionModalButtons();
            UpdateModalButtons();
            UpdateActionButtonStates();
        }

        private void OnModalConfirmClicked(UIObject sender)
        {
            if (_pendingModalEntry == null)
            {
                CloseModal("Shop dialog closed.");
                return;
            }

            string title = _pendingModalEntry.Title;
            if (_modalMode == AdminShopModalMode.RequestQuantity)
            {
                AdminShopEntry entry = _pendingModalEntry;
                int requestQuantity = _modalQuantity;
                CloseModal(string.Empty);
                BeginPendingRequest(entry, requestQuantity);
                return;
            }

            if (_modalMode == AdminShopModalMode.RequestConfirm)
            {
                AdminShopEntry entry = _pendingModalEntry;
                CloseModal(string.Empty);
                BeginPendingRequest(entry, 1);
                return;
            }

            if (_modalMode == AdminShopModalMode.WishlistOwnerConfirm && WishlistWindowRequested != null)
            {
                WishlistWindowRequested.Invoke(this);
                CloseModal($"Opened the dedicated wish-list owner for {title}.");
                return;
            }

            CloseModal($"Wish-list request confirmed for {title}.");
        }

        private void OnModalCancelClicked(UIObject sender)
        {
            string title = _pendingModalEntry?.Title;
            string footerMessage = _modalMode switch
            {
                AdminShopModalMode.RequestQuantity => string.IsNullOrWhiteSpace(title)
                    ? "Item-count prompt cancelled."
                    : $"Item-count prompt cancelled for {title}.",
                AdminShopModalMode.RequestConfirm => string.IsNullOrWhiteSpace(title)
                    ? "Request confirmation cancelled."
                    : $"Request confirmation cancelled for {title}.",
                _ => string.IsNullOrWhiteSpace(title)
                    ? "Wish-list confirmation cancelled."
                    : $"Wish-list confirmation cancelled for {title}."
            };
            CloseModal(footerMessage);
        }

        private void OnModalPreviousClicked(UIObject sender)
        {
            AdjustModalQuantity(-1);
        }

        private void OnModalNextClicked(UIObject sender)
        {
            AdjustModalQuantity(1);
        }

        private void CloseModal(string footerMessage)
        {
            _pendingModalEntry = null;
            _modalVisible = false;
            _modalMode = AdminShopModalMode.None;
            _modalMessage = string.Empty;
            _modalQuantity = 1;
            _modalQuantityMin = 1;
            _modalQuantityMax = 1;
            if (!string.IsNullOrWhiteSpace(footerMessage))
            {
                _footerMessage = footerMessage;
            }
            UpdateModalButtons();
            UpdateActionButtonStates();
        }

        private void UpdateModalButtons()
        {
            bool visible = _modalVisible;
            foreach (UIObject button in _modalButtons)
            {
                button.SetVisible(visible);
                button.SetEnabled(visible);
            }

            bool quantityButtonsVisible = visible && _modalMode == AdminShopModalMode.RequestQuantity;
            _modalPreviousButton?.SetVisible(quantityButtonsVisible);
            _modalPreviousButton?.SetEnabled(quantityButtonsVisible && _modalQuantity > _modalQuantityMin);
            _modalNextButton?.SetVisible(quantityButtonsVisible);
            _modalNextButton?.SetEnabled(quantityButtonsVisible && _modalQuantity < _modalQuantityMax);

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

            if (_modalPreviousButton != null)
            {
                _modalPreviousButton.X = modalBounds.X - Position.X + 18;
                _modalPreviousButton.Y = modalBounds.Y - Position.Y + 21;
            }

            if (_modalNextButton != null)
            {
                int nextButtonWidth = _modalNextButton.CanvasSnapshotWidth;
                _modalNextButton.X = modalBounds.Right - Position.X - nextButtonWidth - 18;
                _modalNextButton.Y = modalBounds.Y - Position.Y + 21;
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

        private bool TryOpenRequestQuantityModal(AdminShopEntry entry)
        {
            int maxPromptQuantity = ResolveModalRequestQuantityCap(entry);
            if (maxPromptQuantity <= 1)
            {
                return false;
            }

            _pendingModalEntry = entry;
            _modalMode = AdminShopModalMode.RequestQuantity;
            _modalVisible = true;
            _modalQuantityMin = 1;
            _modalQuantityMax = maxPromptQuantity;
            _modalQuantity = 1;
            _modalMessage = $"Select a request count for {entry.Title}.";
            _footerMessage = $"Opened the item-count prompt for {entry.Title}.";
            PositionModalButtons();
            UpdateModalButtons();
            UpdateActionButtonStates();
            return true;
        }

        private bool TryOpenRequestConfirmationModal(AdminShopEntry entry)
        {
            if (!RequiresSingleItemRequestConfirmation(entry))
            {
                return false;
            }

            _pendingModalEntry = entry;
            _modalMode = AdminShopModalMode.RequestConfirm;
            _modalVisible = true;
            _modalQuantityMin = 1;
            _modalQuantityMax = 1;
            _modalQuantity = 1;
            _modalMessage = BuildRequestConfirmationMessage(entry);
            _footerMessage = $"Opened the request confirmation for {entry.Title}.";
            PositionModalButtons();
            UpdateModalButtons();
            UpdateActionButtonStates();
            return true;
        }

        private int ResolveModalRequestQuantityCap(AdminShopEntry entry)
        {
            if (entry == null || entry.MaxRequestCount <= 1)
            {
                return 1;
            }

            int maxPromptQuantity = entry.MaxRequestCount;
            bool supportsMultiCountPrompt = SupportsQuantityPrompt(entry);
            if (!supportsMultiCountPrompt)
            {
                return 1;
            }

            if (entry.ConsumeOnSuccess && entry.Price > 0 && _inventory != null)
            {
                maxPromptQuantity = (int)Math.Min(maxPromptQuantity, _inventory.GetMesoCount() / entry.Price);
            }

            if (RequiresInventorySource(entry) && _inventory != null)
            {
                if (TryResolveSourceInventoryStack(entry, out SourceInventoryStackResolution sourceResolution))
                {
                    maxPromptQuantity = Math.Min(
                        maxPromptQuantity,
                        sourceResolution.StackQuantity / Math.Max(1, entry.SourceItemQuantity));
                }
                else
                {
                    maxPromptQuantity = 1;
                }
            }

            if (_inventory != null)
            {
                while (maxPromptQuantity > 1
                       && !_inventory.CanAcceptItem(entry.RewardInventoryType, entry.RewardItemId, ComputeDeliveredQuantity(entry, maxPromptQuantity), ResolveRewardMaxStack(entry)))
                {
                    maxPromptQuantity--;
                }
            }

            return Math.Max(1, maxPromptQuantity);
        }

        private void BeginPendingRequest(AdminShopEntry entry, int requestQuantity)
        {
            _pendingModalEntry = null;
            _pendingRequestEntry = entry;
            _pendingRequestQuantity = Math.Max(1, requestQuantity);
            _pendingRequestPreviousState = entry?.State ?? default;
            _pendingRequestPreviousStateLabel = entry?.StateLabel ?? string.Empty;
            CapturePendingPacketOwnedUserSellSnapshot(entry);
            _pendingStorageExpansionCommoditySerialNumber = entry?.IsStorageExpansion == true
                ? ResolveStorageExpansionCommoditySerialNumberForEntry(entry)
                : 0;
            _pendingStorageExpansionAwaitingPacketResult = entry?.IsStorageExpansion == true
                && _pendingStorageExpansionCommoditySerialNumber > 0;
            bool shouldAwaitPacketOwnedResult = _packetOwnedAdminShopSession.IsActive
                && !_pendingStorageExpansionAwaitingPacketResult
                && entry?.InventoryExpansionType == InventoryType.NONE
                && entry?.IsStorageExpansion != true;
            string packetOwnedTradeSummary = string.Empty;
            _pendingPacketOwnedAdminShopResult = shouldAwaitPacketOwnedResult
                && TryDispatchPacketOwnedAdminShopTradeRequest(entry, _pendingRequestQuantity, out packetOwnedTradeSummary);
            if (_pendingPacketOwnedAdminShopResult)
            {
                ApplyPendingPacketOwnedSetUserItemsParity(entry);
            }
            else if (shouldAwaitPacketOwnedResult && !string.IsNullOrWhiteSpace(packetOwnedTradeSummary))
            {
                _packetOwnedAdminShopSession.SetLastOwnerState("CAdminShopDlg::SendTradeRequest could not build opcode 74 mode 1, so the owner stayed on the local simulator request seam.");
            }

            _packetOwnedAdminShopSession.SetWaitingForResult(_pendingPacketOwnedAdminShopResult);
            _requestResolveTick = _pendingPacketOwnedAdminShopResult
                ? int.MaxValue
                : Environment.TickCount + (_pendingStorageExpansionAwaitingPacketResult
                    ? PacketOwnedStorageExpansionTimeoutMs
                    : 900);
            entry.State = AdminShopEntryState.PendingResponse;
            entry.StateLabel = "Pending";
            long totalPrice = ComputeRequestPrice(entry, _pendingRequestQuantity);
            string quantityLabel = _pendingRequestQuantity > 1 ? $" x{_pendingRequestQuantity}" : string.Empty;
            string priceLabel = totalPrice > 0 ? $" for {FormatPriceLabel(totalPrice)}" : string.Empty;
            _footerMessage = _pendingStorageExpansionAwaitingPacketResult
                ? $"Submitted a {_currentMode} request for {entry.Title}{quantityLabel}{priceLabel}. Waiting for packet-authored storage-expansion result on SN {_pendingStorageExpansionCommoditySerialNumber.ToString(CultureInfo.InvariantCulture)}."
                : _packetOwnedAdminShopSession.IsWaitingForResult
                    ? string.IsNullOrWhiteSpace(packetOwnedTradeSummary)
                        ? $"Submitted a packet-owned admin-shop request for {entry.Title}{quantityLabel}{priceLabel}. Waiting for packet 366 subtype 4."
                        : $"Submitted a packet-owned admin-shop request for {entry.Title}{quantityLabel}{priceLabel}. {packetOwnedTradeSummary} Waiting for packet 366 subtype 4."
                    : string.IsNullOrWhiteSpace(packetOwnedTradeSummary)
                        ? $"Submitted a {_currentMode} request for {entry.Title}{quantityLabel}{priceLabel}. Waiting for simulator response."
                        : $"Submitted a {_currentMode} request for {entry.Title}{quantityLabel}{priceLabel}. {packetOwnedTradeSummary} Waiting for simulator response.";
            UpdateActionButtonStates();
        }

        private void ApplyPendingPacketOwnedSetUserItemsParity(AdminShopEntry entry)
        {
            if (!_packetOwnedAdminShopSession.IsActive
                || entry == null
                || !RequiresInventorySource(entry)
                || entry.SourceInventoryType == InventoryType.NONE)
            {
                return;
            }

            AdminShopUserSellMutationRow requestedRow = new(
                entry.SourceInventoryType,
                entry.SourceItemId,
                entry.InventorySlotIndex + 1,
                Math.Max(0, entry.DisplayQuantity));
            _activePane = AdminShopPane.User;
            _activeBrowseMode = AdminShopBrowseMode.All;
            _activeCategory = ResolveCategoryForInventoryType(entry.SourceInventoryType);
            RefreshDynamicUserEntries();

            AdminShopPaneState paneState = _paneStates[AdminShopPane.User];
            int selectedIndex = AdminShopUserSellMutationParity.FindMatchingEntryIndex(
                BuildUserSellMutationRows(entry.SourceInventoryType),
                requestedRow);
            paneState.SelectedIndex = selectedIndex >= 0
                ? selectedIndex
                : paneState.Entries.Count == 0 ? -1 : Math.Clamp(paneState.SelectedIndex, 0, paneState.Entries.Count - 1);
            paneState.ScrollOffset = selectedIndex < 0
                ? Math.Max(0, paneState.ScrollOffset)
                : AdminShopUserSellMutationParity.ComputeScrollOffset(
                    paneState.ScrollOffset,
                    selectedIndex,
                    MaxVisibleRows);
            ClampPaneState(paneState);
            PersistBrowseSurfaceState(AdminShopPane.User);
            UpdateRowButtons();
        }

        private bool TryCapturePacketOwnedSetUserItemsSelection(
            out InventoryType inventoryType,
            out int itemId,
            out int slotPosition,
            out int scrollOffset)
        {
            inventoryType = InventoryType.NONE;
            itemId = 0;
            slotPosition = 0;
            scrollOffset = 0;
            if (!_packetOwnedAdminShopSession.IsActive || _activePane != AdminShopPane.User)
            {
                return false;
            }

            AdminShopEntry entry = GetSelectedEntry();
            if (entry == null
                || !entry.IsPacketOwnedSnapshotRow
                || !RequiresInventorySource(entry)
                || entry.SourceInventoryType == InventoryType.NONE
                || entry.SourceItemId <= 0
                || entry.InventorySlotIndex < 0)
            {
                return false;
            }

            inventoryType = entry.SourceInventoryType;
            itemId = entry.SourceItemId;
            slotPosition = entry.InventorySlotIndex + 1;
            scrollOffset = Math.Max(0, _paneStates[AdminShopPane.User].ScrollOffset);
            return true;
        }

        private bool TryApplyPacketOwnedSetUserItemsOpenParity(
            InventoryType inventoryType,
            int itemId,
            int slotPosition,
            int scrollOffset)
        {
            if (inventoryType == InventoryType.NONE || itemId <= 0 || slotPosition <= 0)
            {
                return false;
            }

            AdminShopPacketOwnedOpenViewState refocusViewState = AdminShopPacketOwnedOpenViewParity.ResolveUserSellRefocus(
                (int)ResolveCategoryForInventoryType(inventoryType));
            _activePane = AdminShopPacketOwnedOpenViewParity.ClampPaneIndex(refocusViewState.ActivePaneIndex) == AdminShopPacketOwnedOpenViewParity.UserPaneIndex
                ? AdminShopPane.User
                : AdminShopPane.Npc;
            _activeBrowseMode = (AdminShopBrowseMode)AdminShopPacketOwnedOpenViewParity.ClampBrowseModeIndex(refocusViewState.BrowseModeIndex);
            _activeCategory = (AdminShopCategory)AdminShopPacketOwnedOpenViewParity.ClampCategoryIndex(refocusViewState.CategoryIndex);
            ApplyFilters(preserveActivePane: true);

            AdminShopPaneState paneState = _paneStates[AdminShopPane.User];
            AdminShopUserSellMutationResolution resolution = AdminShopPacketOwnedSetUserItemsParity.Resolve(
                BuildUserSellMutationRows(paneState.Entries, inventoryType),
                inventoryType,
                itemId,
                slotPosition,
                scrollOffset,
                MaxVisibleRows);
            if (resolution.SelectedIndex < 0)
            {
                return false;
            }

            paneState.SelectedIndex = resolution.SelectedIndex;
            paneState.ScrollOffset = resolution.ScrollOffset;
            ClampPaneState(paneState);
            PersistBrowseSurfaceState(AdminShopPane.User);
            UpdateRowButtons();
            return true;
        }

        private void AdjustModalQuantity(int delta)
        {
            if (_modalMode != AdminShopModalMode.RequestQuantity)
            {
                return;
            }

            SetModalQuantity(_modalQuantity + delta);
        }

        private void SetModalQuantity(int quantity)
        {
            _modalQuantity = Math.Clamp(quantity, _modalQuantityMin, _modalQuantityMax);
            UpdateModalButtons();
        }

        private static int ComputeDeliveredQuantity(AdminShopEntry entry, int requestQuantity)
        {
            if (entry == null)
            {
                return 0;
            }

            long quantity = (long)Math.Max(1, entry.RewardQuantity) * Math.Max(1, requestQuantity);
            return (int)Math.Min(int.MaxValue, quantity);
        }

        private static long ComputeRequestPrice(AdminShopEntry entry, int requestQuantity)
        {
            if (entry == null)
            {
                return 0;
            }

            return Math.Max(0, entry.Price) * Math.Max(1, requestQuantity);
        }

        private bool TryFocusWishlistEntry(AdminShopEntry matchedEntry, AdminShopCategory requestedCategory, out string message)
        {
            return TryFocusWishlistEntry(matchedEntry, requestedCategory, out message, dispatchWishlistRegisterPacket: true);
        }

        private bool TryFocusWishlistEntry(AdminShopEntry matchedEntry, AdminShopCategory requestedCategory, out string message, bool dispatchWishlistRegisterPacket)
        {
            message = "Wish-list selection could not be focused.";
            if (matchedEntry == null)
            {
                _footerMessage = message;
                UpdateActionButtonStates();
                return false;
            }

            string entryKey = GetEntryKey(matchedEntry);
            bool alreadyWishlisted = matchedEntry.Wishlisted || _wishlistedEntryKeys[_currentMode].Contains(entryKey);

            _activePane = AdminShopPane.Npc;
            _activeBrowseMode = AdminShopBrowseMode.All;
            _activeCategory = requestedCategory == AdminShopCategory.All ? matchedEntry.Category : requestedCategory;
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
                message = $"Wish-list search found {matchedEntry.Title}, but the simulator could not focus the matching row.";
                _footerMessage = message;
                UpdateActionButtonStates();
                return false;
            }

            string wishlistRegisterSummary = string.Empty;
            if (!alreadyWishlisted)
            {
                if (_packetOwnedAdminShopSession.IsActive && dispatchWishlistRegisterPacket)
                {
                    wishlistRegisterSummary = BeginPacketOwnedWishlistRegister(matchedEntry, requestedCategory);
                    message = $"Wish-list search submitted {matchedEntry.Title} to the packet-owned register path. {wishlistRegisterSummary} Waiting for packet 366 subtype 4.";
                    _footerMessage = message;
                    UpdateActionButtonStates();
                    return true;
                }

                if (dispatchWishlistRegisterPacket)
                {
                    wishlistRegisterSummary = DispatchPacketOwnedAdminShopWishlistRegister(matchedEntry);
                    _packetOwnedAdminShopSession.SetLastOwnerState("CUIAdminShopWishListSearchResult::BtRegist confirmed the selected result, sent CUIAdminShopWishList::SendRegisterPacket opcode 74 mode 3, and closed the wishlist owner.");
                }
            }

            matchedEntry.Wishlisted = true;
            _wishlistedEntryKeys[_currentMode].Add(entryKey);
            PersistEntrySessionState(matchedEntry);
            paneState.SelectedIndex = selectedIndex;
            ClampPaneState(paneState);
            PersistBrowseSurfaceState(AdminShopPane.Npc);
            UpdateRowButtons();
            UpdateActionButtonStates();

            message = alreadyWishlisted
                ? $"Wish-list search focused {matchedEntry.Title}; the entry was already saved."
                : $"Wish-list search saved {matchedEntry.Title} and focused the matching catalog row.";
            if (!string.IsNullOrWhiteSpace(wishlistRegisterSummary))
            {
                message = $"{message} {wishlistRegisterSummary}";
            }

            _footerMessage = message;
            return true;
        }

        private string BeginPacketOwnedWishlistRegister(AdminShopEntry entry, AdminShopCategory requestedCategory)
        {
            _pendingPacketOwnedWishlistRegisterEntry = entry;
            _pendingPacketOwnedWishlistRegisterCategory = requestedCategory;
            string registerSummary = DispatchPacketOwnedAdminShopWishlistRegister(entry);
            _packetOwnedAdminShopSession.SetWaitingForResult(true);
            _packetOwnedAdminShopSession.SetLastOwnerState("CUIAdminShopWishListSearchResult::BtRegist confirmed the selected result, sent CUIAdminShopWishList::SendRegisterPacket opcode 74 mode 3, and closed the wishlist owner while waiting for packet 366 subtype 4.");
            return registerSummary;
        }

        private bool TryApplyPacketOwnedWishlistRegisterResult(byte subtype, byte resultCode, out string message, out string noticeText, out bool reopenRequested)
        {
            message = "Packet-owned wish-list register result could not be applied.";
            noticeText = string.Empty;
            reopenRequested = false;
            AdminShopEntry entry = _pendingPacketOwnedWishlistRegisterEntry;
            AdminShopCategory requestedCategory = _pendingPacketOwnedWishlistRegisterCategory;

            if (!AdminShopDialogClientParityText.HandlesResultSubtype(subtype))
            {
                ClearPendingPacketOwnedWishlistRegister();
                message = AdminShopDialogClientParityText.BuildUnsupportedResultMessage(subtype, resultCode);
                _packetOwnedAdminShopSession.SetLastOwnerState("Packet 366 subtype was ignored by the wish-list register owner path.");
                _footerMessage = message;
                UpdateActionButtonStates();
                return true;
            }

            if (entry == null || !_paneStates[AdminShopPane.Npc].SourceEntries.Contains(entry))
            {
                ClearPendingPacketOwnedWishlistRegister();
                _packetOwnedAdminShopSession.MarkDisconnectHazard();
                _packetOwnedAdminShopSession.SetLastOwnerState("Packet 366 arrived after the pending wish-list register row was no longer present in the admin-shop catalog.");
                message = "Packet 366 arrived for a wish-list register request whose live NPC catalog row was no longer available.";
                _footerMessage = message;
                UpdateActionButtonStates();
                return true;
            }

            if (resultCode == 0)
            {
                ClearPendingPacketOwnedWishlistRegister();
                bool focused = TryFocusWishlistEntry(entry, requestedCategory, out message, dispatchWishlistRegisterPacket: false);
                _packetOwnedAdminShopSession.ClearLastNotice();
                _packetOwnedAdminShopSession.SetLastOwnerState("Packet 366 accepted the wish-list register request and the selected catalog row was saved locally.");
                if (!focused)
                {
                    _footerMessage = message;
                    UpdateActionButtonStates();
                }

                return true;
            }

            if (AdminShopDialogClientParityText.TryGetResultNotice(resultCode, out string resolvedNotice, out reopenRequested))
            {
                noticeText = resolvedNotice;
                _packetOwnedAdminShopSession.SetLastNotice(resolvedNotice);
            }
            else
            {
                _packetOwnedAdminShopSession.ClearLastNotice();
            }

            ClearPendingPacketOwnedWishlistRegister();
            if (reopenRequested)
            {
                string outboundSummary = DispatchPacketOwnedAdminShopOutbound(
                    PacketOwnedAdminShopResultMode,
                    _packetOwnedAdminShopSession.NpcTemplateId);
                _packetOwnedAdminShopSession.SetLastOutboundSummary(outboundSummary);
                _packetOwnedAdminShopSession.SetLastOwnerState("Packet 366 rejected the wish-list register request and requested an admin-shop owner reopen through opcode 74 mode 0.");
                _footerMessage = string.IsNullOrWhiteSpace(noticeText)
                    ? $"CUIAdminShopWishList register result {resultCode} requested an authoritative packet 367 refresh. {outboundSummary}"
                    : $"CUIAdminShopWishList register result {resultCode} requested an authoritative packet 367 refresh. {noticeText} {outboundSummary}";
            }
            else
            {
                string stateLabel = AdminShopDialogClientParityText.BuildResultStateLabel(resultCode);
                _packetOwnedAdminShopSession.SetLastOwnerState("Packet 366 rejected the wish-list register request and left the admin-shop owner open.");
                _footerMessage = string.IsNullOrWhiteSpace(noticeText)
                    ? $"CUIAdminShopWishList register result {resultCode} ({stateLabel}) rejected the request for {entry.Title}."
                    : $"CUIAdminShopWishList register result {resultCode} ({stateLabel}) rejected the request for {entry.Title}. {noticeText}";
            }

            message = _footerMessage;
            UpdateActionButtonStates();
            return true;
        }

        private void ClearPendingPacketOwnedWishlistRegister()
        {
            _pendingPacketOwnedWishlistRegisterEntry = null;
            _pendingPacketOwnedWishlistRegisterCategory = AdminShopCategory.All;
        }

        private WishlistSearchResult BuildWishlistSearchResult(AdminShopEntry entry, int score)
        {
            if (entry == null)
            {
                return null;
            }

            string entryKey = GetEntryKey(entry);
            return new WishlistSearchResult
            {
                EntryKey = entryKey,
                Title = entry.Title,
                Seller = entry.Seller,
                PriceLabel = entry.PriceLabel,
                Detail = entry.Detail,
                CategoryLabel = GetCategoryLabel(entry.Category),
                RewardItemId = entry.RewardItemId,
                IconTexture = entry.IconTexture,
                AlreadyWishlisted = entry.Wishlisted || _wishlistedEntryKeys[_currentMode].Contains(entryKey),
                Score = score
            };
        }

        private static string BuildWishlistSearchCatalogSessionSignature(IEnumerable<AdminShopEntry> entries, AdminShopServiceMode mode)
        {
            const ulong fnvOffsetBasis = 14695981039346656037UL;
            ulong hash = fnvOffsetBasis;
            HashWishlistSignature(ref hash, (int)mode);

            foreach (AdminShopEntry entry in entries ?? Enumerable.Empty<AdminShopEntry>())
            {
                if (entry == null || !entry.SupportsWishlist)
                {
                    continue;
                }

                HashWishlistSignature(ref hash, BuildWishlistSearchResultSessionSignature(entry));
            }

            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        private static string BuildWishlistSearchResultSessionSignature(AdminShopEntry entry)
        {
            const ulong fnvOffsetBasis = 14695981039346656037UL;
            ulong hash = fnvOffsetBasis;
            if (entry == null)
            {
                return hash.ToString("X16", CultureInfo.InvariantCulture);
            }

            HashWishlistSignature(ref hash, GetEntryKey(entry));
            HashWishlistSignature(ref hash, entry.Detail);
            HashWishlistSignature(ref hash, (int)entry.Category);
            HashWishlistSignature(ref hash, entry.StateLabel);
            HashWishlistSignature(ref hash, (int)entry.State);
            HashWishlistSignature(ref hash, entry.SupportsWishlist ? 1 : 0);
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        private static void HashWishlistSignature(ref ulong hash, string value)
        {
            unchecked
            {
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 1099511628211UL;
                }

                hash ^= 0xFF;
                hash *= 1099511628211UL;
            }
        }

        private static void HashWishlistSignature(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 1099511628211UL;
            }
        }

        private static int ScoreWishlistEntry(AdminShopEntry entry, string query)
        {
            if (entry == null || string.IsNullOrWhiteSpace(query))
            {
                return 0;
            }

            string normalizedQuery = NormalizeWishlistSearchText(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return 0;
            }

            string collapsedQuery = CollapseWishlistSearchText(query);
            int score = 0;
            score = Math.Max(score, ScoreWishlistField(entry.Title, query, normalizedQuery, 500, 400, 300));
            score = Math.Max(score, ScoreWishlistField(entry.Detail, query, normalizedQuery, 220, 200, 180));
            score = Math.Max(score, ScoreWishlistField(entry.Seller, query, normalizedQuery, 120, 110, 90));
            score = Math.Max(score, ScoreWishlistCollapsedField(entry.Title, collapsedQuery, 490, 370, 290));
            score = Math.Max(score, ScoreWishlistCollapsedField(entry.Detail, collapsedQuery, 210, 190, 170));
            score = Math.Max(score, ScoreWishlistCollapsedField(entry.Seller, collapsedQuery, 110, 100, 80));

            if (TryGetWishlistSearchIndexEntry(entry, out WishlistSearchIndexEntry searchIndexEntry))
            {
                score = Math.Max(score, ScoreWishlistField(searchIndexEntry.CombinedText, query, normalizedQuery, 480, 360, 280));
                score = Math.Max(score, ScoreWishlistCollapsedField(searchIndexEntry.CollapsedText, collapsedQuery, 470, 350, 270));
            }

            string aggregateSearchText = BuildWishlistAggregateSearchText(entry, out IReadOnlyList<string> indexTerms);
            score += ScoreWishlistTokenCoverage(aggregateSearchText, normalizedQuery);
            score += ScoreWishlistIndexTermCoverage(indexTerms, normalizedQuery);
            return score;
        }

        private static string BuildClientWishlistSearchQuery(string query)
        {
            return string.IsNullOrEmpty(query)
                ? string.Empty
                : query.Replace(" ", string.Empty);
        }

        private static int ScoreWishlistField(string fieldValue, string rawQuery, string normalizedQuery, int exactScore, int startsWithScore, int containsScore)
        {
            if (string.IsNullOrWhiteSpace(fieldValue))
            {
                return 0;
            }

            if (string.Equals(fieldValue.Trim(), rawQuery, StringComparison.OrdinalIgnoreCase))
            {
                return exactScore;
            }

            if (fieldValue.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase))
            {
                return startsWithScore;
            }

            if (fieldValue.IndexOf(rawQuery, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return containsScore;
            }

            string normalizedFieldValue = NormalizeWishlistSearchText(fieldValue);
            if (string.IsNullOrWhiteSpace(normalizedFieldValue))
            {
                return 0;
            }

            if (string.Equals(normalizedFieldValue, normalizedQuery, StringComparison.Ordinal))
            {
                return exactScore - 10;
            }

            if (normalizedFieldValue.StartsWith(normalizedQuery, StringComparison.Ordinal))
            {
                return startsWithScore - 10;
            }

            return normalizedFieldValue.Contains(normalizedQuery, StringComparison.Ordinal)
                ? containsScore - 10
                : 0;
        }

        private static int ScoreWishlistCollapsedField(string fieldValue, string collapsedQuery, int exactScore, int startsWithScore, int containsScore)
        {
            if (string.IsNullOrWhiteSpace(fieldValue) || string.IsNullOrWhiteSpace(collapsedQuery))
            {
                return 0;
            }

            string collapsedFieldValue = CollapseWishlistSearchText(fieldValue);
            if (string.IsNullOrWhiteSpace(collapsedFieldValue))
            {
                return 0;
            }

            if (string.Equals(collapsedFieldValue, collapsedQuery, StringComparison.Ordinal))
            {
                return exactScore;
            }

            if (collapsedFieldValue.StartsWith(collapsedQuery, StringComparison.Ordinal))
            {
                return startsWithScore;
            }

            return collapsedFieldValue.Contains(collapsedQuery, StringComparison.Ordinal)
                ? containsScore
                : 0;
        }

        private static int ScoreWishlistTokenCoverage(string aggregateSearchText, string normalizedQuery)
        {
            string[] tokens = SplitWishlistSearchTokens(normalizedQuery);
            if (tokens.Length <= 1 || string.IsNullOrWhiteSpace(aggregateSearchText))
            {
                return 0;
            }

            int matchedTokens = tokens.Count(token => aggregateSearchText.Contains(token, StringComparison.Ordinal));
            if (matchedTokens == 0)
            {
                return 0;
            }

            int coverageScore = matchedTokens * 30;
            if (matchedTokens == tokens.Length)
            {
                coverageScore += 40;
            }

            return coverageScore;
        }

        private static int ScoreWishlistIndexTermCoverage(IReadOnlyList<string> indexTerms, string normalizedQuery)
        {
            string[] tokens = SplitWishlistSearchTokens(normalizedQuery);
            if (tokens.Length == 0 || indexTerms == null || indexTerms.Count == 0)
            {
                return 0;
            }

            int bestScore = 0;
            foreach (string term in indexTerms)
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    continue;
                }

                int matchedTokens = tokens.Count(token => term.Contains(token, StringComparison.Ordinal));
                if (matchedTokens == 0)
                {
                    continue;
                }

                int score = matchedTokens * 25;
                if (matchedTokens == tokens.Length)
                {
                    score += 35;
                }

                bestScore = Math.Max(bestScore, score);
            }

            return bestScore;
        }

        private static string BuildWishlistAggregateSearchText(AdminShopEntry entry, out IReadOnlyList<string> indexTerms)
        {
            indexTerms = Array.Empty<string>();
            List<string> components = new()
            {
                NormalizeWishlistSearchText(entry?.Title),
                NormalizeWishlistSearchText(entry?.Detail),
                NormalizeWishlistSearchText(entry?.Seller)
            };

            if (TryGetWishlistSearchIndexEntry(entry, out WishlistSearchIndexEntry searchIndexEntry))
            {
                indexTerms = searchIndexEntry.Terms;
                components.Add(searchIndexEntry.CombinedText);
            }

            return string.Join(" ", components.Where(component => !string.IsNullOrWhiteSpace(component)));
        }

        private static string NormalizeWishlistSearchText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] buffer = value.Trim().ToLowerInvariant().ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (!char.IsLetterOrDigit(buffer[i]))
                {
                    buffer[i] = ' ';
                }
            }

            return string.Join(" ", new string(buffer).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string CollapseWishlistSearchText(string value)
        {
            string normalized = NormalizeWishlistSearchText(value);
            return string.IsNullOrWhiteSpace(normalized)
                ? string.Empty
                : string.Concat(normalized.Where(char.IsLetterOrDigit));
        }

        private static string[] SplitWishlistSearchTokens(string normalizedQuery)
        {
            return string.IsNullOrWhiteSpace(normalizedQuery)
                ? Array.Empty<string>()
                : normalizedQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private string BuildSelectionMessage(AdminShopEntry entry, AdminShopPane pane)
        {
            if (entry == null)
            {
                return "Select an offer to inspect request details.";
            }

            string paneLabel = pane == AdminShopPane.Npc ? "NPC offer" : "user item";
            return $"Selected {paneLabel}: {entry.Title} ({GetCategoryLabel(entry.Category)}). {GetEntryStateText(entry)}";
        }

        private void PersistBrowseSurfaceState(AdminShopPane pane)
        {
            AdminShopPaneState paneState = _paneStates[pane];
            AdminShopEntry selectedEntry = paneState.SelectedIndex >= 0 && paneState.SelectedIndex < paneState.Entries.Count
                ? paneState.Entries[paneState.SelectedIndex]
                : null;
            _browseSurfaceStates[BuildBrowseSurfaceStateKey(pane)] = new AdminShopBrowseSurfaceState
            {
                ScrollOffset = paneState.ScrollOffset,
                SelectedEntryKey = selectedEntry == null ? string.Empty : GetEntryKey(selectedEntry)
            };
        }

        private void TryRestoreBrowseSurfaceState(AdminShopPane pane, AdminShopPaneState paneState)
        {
            paneState.ScrollOffset = 0;
            paneState.SelectedIndex = -1;
            if (!_browseSurfaceStates.TryGetValue(BuildBrowseSurfaceStateKey(pane), out AdminShopBrowseSurfaceState savedState))
            {
                return;
            }

            paneState.ScrollOffset = savedState.ScrollOffset;
            if (string.IsNullOrWhiteSpace(savedState.SelectedEntryKey))
            {
                return;
            }

            for (int i = 0; i < paneState.Entries.Count; i++)
            {
                if (!string.Equals(GetEntryKey(paneState.Entries[i]), savedState.SelectedEntryKey, StringComparison.Ordinal))
                {
                    continue;
                }

                paneState.SelectedIndex = i;
                return;
            }
        }

        private string BuildBrowseSurfaceStateKey(AdminShopPane pane)
        {
            return FormattableString.Invariant($"{_currentMode}:{pane}:{_activeBrowseMode}:{_activeCategory}");
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
                    CreateSourceTradeEntry("Steel Ore Exchange", "Zero-price catalog row that mirrors the client inventory-source branch by validating live Steel Ore stock before the request is accepted.", "Cash Manager", AdminShopCategory.Etc, true, InventoryType.ETC, 4010001, 5, InventoryType.CASH, 5152057, featured: true, maxRequestCount: 6),
                    CreateItemEntry("Royal Hair Coupon", "Rotating salon coupon preview from the featured cash-service board.", "Cash Manager", 3400, AdminShopCategory.Special, true, InventoryType.CASH, 5050000, featured: true),
                    CreateItemEntry("Royal Face Coupon", "Premium face coupon entry with the same preview flow the client routes into wish-list dialogs.", "Cash Manager", 2900, AdminShopCategory.Special, true, InventoryType.CASH, 5150040, featured: true),
                    CreateItemEntry("Pet Snack Bundle", "Utility bundle with snack and pet-tag support for multi-pet sessions.", "Cash Manager", 1900, AdminShopCategory.Use, true, InventoryType.CASH, 2120000, 3, featured: true, maxRequestCount: 5),
                    CreateStorageExpansionEntry("Storage Slot Expansion", "Convenience service for storage and shared account inventory capacity.", "Cash Manager"),
                    CreateItemEntry("Hyper Teleport Rock", "Navigation-heavy service entry used to compare convenience bundles.", "Cash Manager", 900, AdminShopCategory.Use, true, InventoryType.CASH, 5040004, maxRequestCount: 10),
                    CreateItemEntry("Surprise Style Box", "Random cosmetic box surfaced through the featured rotation.", "Cash Manager", 3400, AdminShopCategory.Package, true, InventoryType.CASH, 5222000, featured: true),
                    CreateItemEntry("Cosmetic Lens Coupon", "Style utility item staged for wish-list confirmation tests.", "Cash Manager", 1600, AdminShopCategory.Button, true, InventoryType.CASH, 5152057),
                    CreateEntry("Pet Equip Bundle", "Pet equipment and accessory bundle bound to the NPC-side catalog.", "Cash Manager", 2200, AdminShopCategory.Equip, true, featured: true, state: AdminShopEntryState.SoldOut, stateLabel: "Sold out")
                };
            }

            return new[]
            {
                CreateSourceTradeEntry("Mithril Relay Ticket", "Zero-price MTS sample row that validates live Mithril Ore stock before the simulator relays the trade request.", "MTS Clerk", AdminShopCategory.Etc, false, InventoryType.ETC, 4010002, 4, InventoryType.USE, 2070000, featured: true, maxRequestCount: 5),
                CreateItemEntry("Zakum Helmet Listing", "Admin MTS preview of a high-demand helmet sold from the NPC-owned catalog view.", "MTS Clerk", 12500000, AdminShopCategory.Equip, false, InventoryType.EQUIP, 1002357, featured: true, response: AdminShopResponse.ListingSoldOut, responseMessage: "The listing refreshed before the purchase could be confirmed."),
                CreateItemEntry("Maple Kandayo", "MTS equipment board seeded to exercise request submission and price display.", "MTS Clerk", 9800000, AdminShopCategory.Equip, false, InventoryType.EQUIP, 1332027, featured: true),
                CreateItemEntry("Steely Throwing-Knives", "Consumable trade board sample that mirrors a browse-first MTS flow.", "MTS Clerk", 3200000, AdminShopCategory.Use, false, InventoryType.USE, 2070005, 1, response: AdminShopResponse.InventoryFull, responseMessage: "The MTS clerk rejected delivery because the destination inventory tab is full.", maxRequestCount: 10),
                CreateEntry("Chaos Scroll 60%", "Scroll listing preview staged for user-vs-NPC comparison.", "MTS Clerk", 21000000, AdminShopCategory.Scroll, false, state: AdminShopEntryState.SoldOut, stateLabel: "Sold out"),
                CreateItemEntry("Brown Work Gloves", "Common MTS browse row with seller and price labels only.", "MTS Clerk", 4700000, AdminShopCategory.Equip, false, InventoryType.EQUIP, 1082002),
                CreateItemEntry("Pink Adventurer Cape", "Apparel listing in the MTS catalog pane.", "MTS Clerk", 15000000, AdminShopCategory.Setup, false, InventoryType.EQUIP, 1102041, response: AdminShopResponse.SellerUnavailable, responseMessage: "The seller did not answer the trade relay request."),
                CreateItemEntry("Ilbi Throwing-Stars", "Projectile listing to keep the pane scrollable.", "MTS Clerk", 6100000, AdminShopCategory.Use, false, InventoryType.USE, 2070000, maxRequestCount: 10),
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
            string responseMessage = "",
            int maxRequestCount = 1)
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
                ResponseMessage = responseMessage,
                MaxRequestCount = Math.Max(1, maxRequestCount)
            };
        }

        private void PopulateSourceEntries(AdminShopServiceMode mode)
        {
            if (UsesPacketOwnedAdminShopCatalog(mode))
            {
                foreach (PacketOwnedAdminShopCommoditySnapshot row in _packetOwnedAdminShopRows)
                {
                    AdminShopEntry entry = row.Price > 0
                        ? CreatePacketOwnedCommodityEntry(row)
                        : CreatePacketOwnedSellTemplateEntry(row);
                    if (entry == null)
                    {
                        continue;
                    }

                    if (RequiresInventorySource(entry))
                    {
                        _packetOwnedAdminShopSellTemplates.Add(entry);
                        continue;
                    }

                    _paneStates[AdminShopPane.Npc].SourceEntries.Add(entry);
                }

                foreach (AdminShopEntry userEntry in CreateInventoryBackedUserEntries(_packetOwnedAdminShopSellTemplates))
                {
                    _paneStates[AdminShopPane.User].SourceEntries.Add(userEntry);
                }

                return;
            }

            List<AdminShopEntry> npcEntries = CreateNpcEntries(mode).ToList();
            _paneStates[AdminShopPane.Npc].SourceEntries.AddRange(npcEntries);
            _paneStates[AdminShopPane.User].SourceEntries.AddRange(CreateInventoryBackedUserEntries(npcEntries));
            if (_inventory == null)
            {
                _paneStates[AdminShopPane.User].SourceEntries.AddRange(CreateUserEntries(mode));
            }
        }

        private void RefreshDynamicUserEntries()
        {
            List<AdminShopEntry> userCatalogEntries = ResolveUserPaneCatalogEntries()
                .Where(entry => entry != null)
                .ToList();

            _paneStates[AdminShopPane.User].SourceEntries.Clear();
            _paneStates[AdminShopPane.User].SourceEntries.AddRange(CreateInventoryBackedUserEntries(userCatalogEntries));
            if (_inventory == null && !UsesPacketOwnedAdminShopCatalog(_currentMode))
            {
                _paneStates[AdminShopPane.User].SourceEntries.AddRange(CreateUserEntries(_currentMode));
            }

            RestoreEntryFlags(_paneStates[AdminShopPane.User].SourceEntries, _currentMode);
            RestoreEntryStates(_paneStates[AdminShopPane.User].SourceEntries, _currentMode);
            PopulateEntryIcons(_paneStates[AdminShopPane.User].SourceEntries);
            ApplyFilters();
            UpdateRowButtons();
        }

        private IEnumerable<AdminShopEntry> ResolveUserPaneCatalogEntries()
        {
            if (UsesPacketOwnedAdminShopCatalog(_currentMode) && _packetOwnedAdminShopSellTemplates.Count > 0)
            {
                return _packetOwnedAdminShopSellTemplates;
            }

            return _paneStates[AdminShopPane.Npc].SourceEntries;
        }

        private IEnumerable<AdminShopEntry> CreateInventoryBackedUserEntries(IEnumerable<AdminShopEntry> npcEntries)
        {
            if (_inventory == null || npcEntries == null)
            {
                yield break;
            }

            if (UsesPacketOwnedAdminShopCatalog(_currentMode))
            {
                foreach (AdminShopEntry entry in CreatePacketOwnedInventoryBackedUserEntries(npcEntries))
                {
                    yield return entry;
                }

                yield break;
            }

            foreach (AdminShopEntry catalogEntry in npcEntries)
            {
                if (!RequiresInventorySource(catalogEntry))
                {
                    continue;
                }

                IReadOnlyList<InventorySlotData> slots = _inventory.GetSlots(catalogEntry.SourceInventoryType);
                if (slots == null || slots.Count == 0)
                {
                    continue;
                }

                for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                {
                    InventorySlotData slot = slots[slotIndex];
                    if (slot == null
                        || slot.IsDisabled
                        || slot.ItemId != catalogEntry.SourceItemId)
                    {
                        continue;
                    }

                    if (InventoryItemMetadataResolver.TryResolveTradeRestrictionFlags(slot.ItemId, out bool isCashItem, out bool isNotForSale, out bool isQuestItem)
                        && (isCashItem || isNotForSale || isQuestItem))
                    {
                        continue;
                    }

                    yield return CreateInventoryBackedUserEntry(catalogEntry, slot, slotIndex);
                }
            }
        }

        private IEnumerable<AdminShopEntry> CreatePacketOwnedInventoryBackedUserEntries(IEnumerable<AdminShopEntry> npcEntries)
        {
            Dictionary<InventoryType, Dictionary<int, List<AdminShopEntry>>> sellTemplatesByType = BuildPacketOwnedSellTemplateLookup(npcEntries);
            foreach ((InventoryType inventoryType, Dictionary<int, List<AdminShopEntry>> templatesByItemId) in sellTemplatesByType)
            {
                IReadOnlyList<InventorySlotData> slots = _inventory.GetSlots(inventoryType);
                if (slots == null || slots.Count == 0)
                {
                    continue;
                }

                for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                {
                    InventorySlotData slot = slots[slotIndex];
                    if (slot == null
                        || slot.IsDisabled
                        || !templatesByItemId.TryGetValue(slot.ItemId, out List<AdminShopEntry> catalogEntries))
                    {
                        continue;
                    }

                    if (InventoryItemMetadataResolver.TryResolveTradeRestrictionFlags(slot.ItemId, out bool isCashItem, out bool isNotForSale, out bool isQuestItem)
                        && (isCashItem || isNotForSale || isQuestItem))
                    {
                        continue;
                    }

                    foreach (AdminShopEntry catalogEntry in catalogEntries)
                    {
                        if (catalogEntry == null)
                        {
                            continue;
                        }

                        yield return CreateInventoryBackedUserEntry(catalogEntry, slot, slotIndex);
                    }
                }
            }
        }

        private static Dictionary<InventoryType, Dictionary<int, List<AdminShopEntry>>> BuildPacketOwnedSellTemplateLookup(IEnumerable<AdminShopEntry> npcEntries)
        {
            Dictionary<InventoryType, Dictionary<int, List<AdminShopEntry>>> lookup = new();
            foreach (AdminShopEntry entry in npcEntries)
            {
                if (!RequiresInventorySource(entry)
                    || !entry.IsPacketOwnedSnapshotRow
                    || entry.PacketSaleState != 0
                    || entry.SourceInventoryType == InventoryType.NONE
                    || entry.SourceItemId <= 0)
                {
                    continue;
                }

                if (!lookup.TryGetValue(entry.SourceInventoryType, out Dictionary<int, List<AdminShopEntry>> entriesByItemId))
                {
                    entriesByItemId = new Dictionary<int, List<AdminShopEntry>>();
                    lookup[entry.SourceInventoryType] = entriesByItemId;
                }

                if (!entriesByItemId.TryGetValue(entry.SourceItemId, out List<AdminShopEntry> entries))
                {
                    entries = new List<AdminShopEntry>();
                    entriesByItemId[entry.SourceItemId] = entries;
                }

                entries.Add(entry);
            }

            return lookup;
        }

        private static AdminShopEntry CreateInventoryBackedUserEntry(AdminShopEntry catalogEntry, InventorySlotData slot, int slotIndex)
        {
            int stackQuantity = Math.Max(1, slot.Quantity);
            string sourceItemName = !string.IsNullOrWhiteSpace(slot.ItemName)
                ? slot.ItemName
                : ResolveSourceItemLabel(catalogEntry);
            string slotLabel = $"Slot {slotIndex + 1}";
            string detail = $"{catalogEntry.Detail} Mirrors CAdminShopDlg::SetUserItems by binding the request to {slotLabel} carrying {sourceItemName} x{stackQuantity}.";
            string priceLabel = $"Stock x{stackQuantity}";
            if (stackQuantity < Math.Max(1, catalogEntry.SourceItemQuantity))
            {
                priceLabel = $"Need x{catalogEntry.SourceItemQuantity} ({stackQuantity} ready)";
            }

            return new AdminShopEntry
            {
                Title = catalogEntry.Title,
                Detail = detail,
                Seller = slotLabel,
                Price = catalogEntry.Price,
                PriceLabel = priceLabel,
                Category = ResolveCategoryForInventoryType(catalogEntry.SourceInventoryType),
                SupportsWishlist = false,
                Featured = catalogEntry.Featured,
                State = catalogEntry.State,
                StateLabel = catalogEntry.StateLabel,
                RewardInventoryType = catalogEntry.RewardInventoryType,
                RewardItemId = catalogEntry.RewardItemId,
                RewardQuantity = catalogEntry.RewardQuantity,
                RewardMaxStackSize = catalogEntry.RewardMaxStackSize,
                ConsumeOnSuccess = catalogEntry.ConsumeOnSuccess,
                LockAfterSuccess = catalogEntry.LockAfterSuccess,
                Response = catalogEntry.Response,
                ResponseMessage = catalogEntry.ResponseMessage,
                WasPurchased = catalogEntry.WasPurchased,
                CommoditySerialNumber = catalogEntry.CommoditySerialNumber,
                CommodityOnSale = catalogEntry.CommodityOnSale,
                MaxRequestCount = catalogEntry.MaxRequestCount,
                SuccessMesoReward = catalogEntry.SuccessMesoReward,
                SourceInventoryType = catalogEntry.SourceInventoryType,
                SourceItemId = catalogEntry.SourceItemId,
                SourceItemQuantity = catalogEntry.SourceItemQuantity,
                DisplayInventoryType = catalogEntry.SourceInventoryType,
                DisplayItemId = catalogEntry.SourceItemId,
                DisplayQuantity = stackQuantity,
                InventorySlotIndex = slotIndex,
                PacketSerialNumber = catalogEntry.PacketSerialNumber,
                PacketSaleState = catalogEntry.PacketSaleState,
                IsPacketOwnedSnapshotRow = catalogEntry.IsPacketOwnedSnapshotRow
            };
        }

        private static AdminShopCategory ResolveCategoryForInventoryType(InventoryType inventoryType)
        {
            return inventoryType switch
            {
                InventoryType.EQUIP => AdminShopCategory.Equip,
                InventoryType.USE => AdminShopCategory.Use,
                InventoryType.SETUP => AdminShopCategory.Setup,
                InventoryType.ETC => AdminShopCategory.Etc,
                InventoryType.CASH => AdminShopCategory.Cash,
                _ => AdminShopCategory.All
            };
        }

        private bool UsesPacketOwnedAdminShopCatalog(AdminShopServiceMode mode)
        {
            return mode == AdminShopServiceMode.CashShop
                && _packetOwnedAdminShopSession.IsActive
                && _packetOwnedAdminShopRows.Count > 0;
        }

        private static AdminShopEntry CreateSourceTradeEntry(
            string title,
            string detail,
            string seller,
            AdminShopCategory category,
            bool supportsWishlist,
            InventoryType sourceInventoryType,
            int sourceItemId,
            int sourceItemQuantity,
            InventoryType rewardInventoryType,
            int rewardItemId,
            int rewardQuantity = 1,
            bool featured = false,
            bool lockAfterSuccess = false,
            AdminShopEntryState state = AdminShopEntryState.Available,
            string stateLabel = "",
            AdminShopResponse response = AdminShopResponse.GrantItem,
            string responseMessage = "",
            int maxRequestCount = 1)
        {
            return new AdminShopEntry
            {
                Title = title,
                Detail = detail,
                Seller = seller,
                Price = 0,
                PriceLabel = "Trade item",
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
                ResponseMessage = responseMessage,
                MaxRequestCount = Math.Max(1, maxRequestCount),
                SuccessMesoReward = 0,
                SourceInventoryType = sourceInventoryType,
                SourceItemId = sourceItemId,
                SourceItemQuantity = Math.Max(1, sourceItemQuantity)
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

            if (_packetOwnedAdminShopSession.IsWaitingForResult)
            {
                return;
            }

            if (_pendingRequestEntry.IsStorageExpansion)
            {
                if (_pendingStorageExpansionAwaitingPacketResult)
                {
                    _footerMessage = $"No packet-authored storage-expansion result arrived for SN {_pendingStorageExpansionCommoditySerialNumber.ToString(CultureInfo.InvariantCulture)} before the simulator timeout. Falling back to the local Cash Shop seam.";
                    _pendingStorageExpansionAwaitingPacketResult = false;
                }

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

            if (entry.State != AdminShopEntryState.Available
                && entry.State != AdminShopEntryState.RequestAccepted
                && entry.State != AdminShopEntryState.RequestRejected)
            {
                return false;
            }

            return !RequiresInventorySource(entry)
                   || TryValidateInventorySourceRequest(entry, 1, out _);
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

            if (RequiresInventorySource(entry) && !TryValidateInventorySourceRequest(entry, 1, out string sourceMessage))
            {
                return sourceMessage;
            }

            return GetEntryStateText(entry);
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
            if (entry == null || !entry.IsStorageExpansion || _storageRuntime == null)
            {
                return false;
            }

            return _storageRuntime.IsAccessSessionActive
                   && _storageRuntime.CanCurrentCharacterAccess
                   && _storageRuntime.IsClientAccountAuthorityVerified
                   && _storageRuntime.IsSecondaryPasswordVerified
                   && _storageRuntime.CanExpandSlotLimit()
                   && _nexonCash >= entry.Price;
        }

        private bool TryConsumeStorageExpansionCash(long amount)
        {
            long normalizedAmount = Math.Max(0L, amount);
            if (normalizedAmount <= 0)
            {
                return true;
            }

            if (_nexonCash < normalizedAmount)
            {
                return false;
            }

            if (TryConsumeCashBalance != null)
            {
                return TryConsumeCashBalance(normalizedAmount);
            }

            _nexonCash -= normalizedAmount;
            return true;
        }

        private static string FormatCashPriceLabel(long amount)
        {
            return $"{FormatPriceLabel(amount)} NX";
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

            return GetEntryStateText(entry);
        }

        private string BuildStorageExpansionBlockedMessage(AdminShopEntry entry)
        {
            if (_storageRuntime == null)
            {
                return "Storage runtime is unavailable for capacity updates.";
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

            if (_nexonCash < entry.Price)
            {
                return $"Need {FormatCashPriceLabel(entry.Price)} before extending storage capacity.";
            }

            return GetEntryStateText(entry);
        }

        private void ResolveInventoryExpansionRequest(AdminShopEntry entry)
        {
            if (_inventory == null)
            {
                _footerMessage = "Inventory runtime is unavailable for slot expansion.";
                ResetPendingRequestState();
                UpdateActionButtonStates();
                return;
            }

            if (!_inventory.CanExpandSlotLimit(entry.InventoryExpansionType))
            {
                _footerMessage = BuildInventoryExpansionBlockedMessage(entry);
                ResetPendingRequestState();
                UpdateActionButtonStates();
                return;
            }

            if (!_inventory.TryConsumeMeso(entry.Price))
            {
                _footerMessage = $"Need {FormatPriceLabel(entry.Price)} before extending this inventory tab.";
                ResetPendingRequestState();
                UpdateActionButtonStates();
                return;
            }

            _inventory.TryExpandSlotLimit(entry.InventoryExpansionType);
            entry.State = AdminShopEntryState.RequestAccepted;
            entry.StateLabel = "Expanded";
            MarkEntryPurchased(entry);
            PersistEntrySessionState(entry);
            _footerMessage = $"{entry.Title} succeeded. {entry.InventoryExpansionType} inventory now has {_inventory.GetSlotLimit(entry.InventoryExpansionType)} slots.";
            ResetPendingRequestState();
            Money = _inventory.GetMesoCount();
            UpdateActionButtonStates();
        }

        private void ResolveStorageExpansionRequest(AdminShopEntry entry)
        {
            if (_storageRuntime == null)
            {
                CompleteStorageExpansionRequest(
                    entry,
                    AdminShopEntryState.RequestRejected,
                    "No runtime",
                    "Storage runtime is unavailable for slot expansion.",
                    StorageExpansionResultSubtype.Rejected,
                    StorageExpansionFailureReason.RuntimeUnavailable);
                return;
            }

            if (!_storageRuntime.CanExpandSlotLimit())
            {
                CompleteStorageExpansionRequest(
                    entry,
                    AdminShopEntryState.RequestRejected,
                    "At cap",
                    BuildStorageExpansionBlockedMessage(entry),
                    StorageExpansionResultSubtype.Rejected,
                    StorageExpansionFailureReason.SlotCapReached);
                return;
            }

            if (!_storageRuntime.CanCurrentCharacterAccess)
            {
                CompleteStorageExpansionRequest(
                    entry,
                    AdminShopEntryState.RequestRejected,
                    "No access",
                    BuildStorageExpansionBlockedMessage(entry),
                    StorageExpansionResultSubtype.Rejected,
                    StorageExpansionFailureReason.UnauthorizedCharacter);
                return;
            }

            if (!_storageRuntime.IsAccessSessionActive ||
                !_storageRuntime.IsClientAccountAuthorityVerified ||
                !_storageRuntime.IsSecondaryPasswordVerified)
            {
                int failureReason = !_storageRuntime.IsAccessSessionActive
                    ? StorageExpansionFailureReason.SessionLocked
                    : !_storageRuntime.IsClientAccountAuthorityVerified
                        ? StorageExpansionFailureReason.MissingAccountAuthority
                        : StorageExpansionFailureReason.MissingStoragePasscode;
                CompleteStorageExpansionRequest(
                    entry,
                    AdminShopEntryState.RequestRejected,
                    "Locked",
                    BuildStorageExpansionBlockedMessage(entry),
                    StorageExpansionResultSubtype.Rejected,
                    failureReason);
                return;
            }

            if (!TryConsumeStorageExpansionCash(entry.Price))
            {
                CompleteStorageExpansionRequest(
                    entry,
                    AdminShopEntryState.RequestRejected,
                    "Need NX",
                    $"Need {FormatCashPriceLabel(entry.Price)} before extending storage capacity.",
                    StorageExpansionResultSubtype.Rejected,
                    StorageExpansionFailureReason.NotEnoughCash);
                return;
            }

            if (!_storageRuntime.TryExpandSlotLimit())
            {
                CompleteStorageExpansionRequest(
                    entry,
                    AdminShopEntryState.RequestRejected,
                    "Retry",
                    "The storage expansion request reached the simulator Cash Shop seam, but the slot limit did not advance.",
                    StorageExpansionResultSubtype.Rejected,
                    StorageExpansionFailureReason.ExpansionFailed);
                return;
            }

            CompleteStorageExpansionRequest(
                entry,
                AdminShopEntryState.RequestAccepted,
                "Expanded",
                $"{entry.Title} succeeded. Storage now has {_storageRuntime.GetSlotLimit()} slots and {FormatCashPriceLabel(_nexonCash)} remains on the account.",
                StorageExpansionResultSubtype.Success,
                StorageExpansionFailureReason.None,
                markPurchased: true);
        }

        private void ResolveCatalogRequest(AdminShopEntry entry)
        {
            if (_inventory == null)
            {
                entry.State = AdminShopEntryState.RequestRejected;
                entry.StateLabel = "No runtime";
                _footerMessage = "Inventory runtime is unavailable for shop delivery.";
                ResetPendingRequestState();
                UpdateActionButtonStates();
                return;
            }

            if (!TryValidateInventorySourceRequest(entry, _pendingRequestQuantity, out string sourceValidationMessage))
            {
                entry.State = AdminShopEntryState.RequestRejected;
                entry.StateLabel = "Need item";
                PersistEntrySessionState(entry);
                _footerMessage = sourceValidationMessage;
                ResetPendingRequestState();
                UpdateActionButtonStates();
                return;
            }

            long totalPrice = ComputeRequestPrice(entry, _pendingRequestQuantity);
            if (entry.ConsumeOnSuccess && !_inventory.TryConsumeMeso(totalPrice))
            {
                entry.State = AdminShopEntryState.RequestRejected;
                entry.StateLabel = "Need mesos";
                _footerMessage = $"Need {FormatPriceLabel(totalPrice)} before this request can complete.";
                ResetPendingRequestState();
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
                        : entry.ResponseMessage, refundAmount: entry.ConsumeOnSuccess ? totalPrice : 0L);
                    return;
                case AdminShopResponse.SellerUnavailable:
                    FinishRejectedRequest(entry, "No reply", string.IsNullOrWhiteSpace(entry.ResponseMessage)
                        ? $"{entry.Seller} did not answer the simulator trade relay."
                        : entry.ResponseMessage, refundAmount: entry.ConsumeOnSuccess ? totalPrice : 0L);
                    return;
                case AdminShopResponse.ListingExpired:
                    FinishRejectedRequest(entry, "Expired", string.IsNullOrWhiteSpace(entry.ResponseMessage)
                        ? $"{entry.Title} expired before delivery could be confirmed."
                        : entry.ResponseMessage, refundAmount: entry.ConsumeOnSuccess ? totalPrice : 0L);
                    return;
                case AdminShopResponse.InventoryFull:
                    FinishRejectedRequest(entry, "Inventory full", string.IsNullOrWhiteSpace(entry.ResponseMessage)
                        ? $"The destination {entry.RewardInventoryType} inventory tab cannot accept {entry.Title}."
                        : entry.ResponseMessage, refundAmount: entry.ConsumeOnSuccess ? totalPrice : 0L);
                    return;
                case AdminShopResponse.MissingSourceItem:
                    FinishRejectedRequest(entry, "Need item", string.IsNullOrWhiteSpace(entry.ResponseMessage)
                        ? BuildMissingSourceItemMessage(entry, _pendingRequestQuantity, default)
                        : entry.ResponseMessage, refundAmount: entry.ConsumeOnSuccess ? totalPrice : 0L);
                    return;
                default:
                    FinishRejectedRequest(entry, "Rejected", $"The simulator rejected the request for {entry.Title}.", refundAmount: entry.ConsumeOnSuccess ? totalPrice : 0L);
                    return;
            }
        }

        private void ResolveGrantedItemRequest(AdminShopEntry entry)
        {
            int deliveredQuantity = ComputeDeliveredQuantity(entry, _pendingRequestQuantity);
            long mesoReward = ComputeSuccessMesoReward(entry, _pendingRequestQuantity);
            long totalPrice = ComputeRequestPrice(entry, _pendingRequestQuantity);
            if (entry.RewardInventoryType == InventoryType.NONE || entry.RewardItemId <= 0)
            {
                if (!TryConsumeInventorySource(entry, _pendingRequestQuantity, out string sourceConsumeMessage))
                {
                    FinishRejectedRequest(
                        entry,
                        "Need item",
                        sourceConsumeMessage,
                        refundAmount: entry.ConsumeOnSuccess ? totalPrice : 0L);
                    return;
                }

                if (mesoReward > 0 && _inventory != null)
                {
                    _inventory.AddMeso(mesoReward);
                }

                entry.State = AdminShopEntryState.RequestAccepted;
                entry.StateLabel = "Accepted";
                PersistEntrySessionState(entry);
                _footerMessage = mesoReward > 0
                    ? $"Catalog response received for {entry.Title}. Sold for {FormatPriceLabel(mesoReward)}."
                    : $"Catalog response received for {entry.Title}. The simulator accepted the request.";
                ResetPendingRequestState();
                Money = _inventory?.GetMesoCount() ?? Money;
                RefreshDynamicUserEntries();
                ApplyPendingPacketOwnedUserSellMutationParity();
                UpdateActionButtonStates();
                return;
            }

            if (!_inventory.CanAcceptItem(entry.RewardInventoryType, entry.RewardItemId, deliveredQuantity, ResolveRewardMaxStack(entry)))
            {
                FinishRejectedRequest(
                    entry,
                    "Inventory full",
                    $"The destination {entry.RewardInventoryType} inventory tab cannot accept {entry.Title}.",
                    refundAmount: entry.ConsumeOnSuccess ? totalPrice : 0L);
                return;
            }

            if (!TryConsumeInventorySource(entry, _pendingRequestQuantity, out string sourceConsumeFailureMessage))
            {
                FinishRejectedRequest(
                    entry,
                    "Need item",
                    sourceConsumeFailureMessage,
                    refundAmount: entry.ConsumeOnSuccess ? totalPrice : 0L);
                return;
            }

            Texture2D itemTexture = ResolveEntryIcon(entry);
            _inventory.AddItem(entry.RewardInventoryType, entry.RewardItemId, itemTexture, deliveredQuantity);
            entry.State = entry.LockAfterSuccess ? AdminShopEntryState.SoldOut : AdminShopEntryState.RequestAccepted;
            entry.StateLabel = entry.LockAfterSuccess ? "Purchased" : "Delivered";
            entry.IconTexture = itemTexture;
            MarkEntryPurchased(entry);
            PersistEntrySessionState(entry);
            string deliveryLabel = deliveredQuantity > 1 ? $" x{deliveredQuantity}" : string.Empty;
            _footerMessage = $"{entry.Title}{deliveryLabel} delivered to {entry.RewardInventoryType} inventory.";
            ResetPendingRequestState();
            Money = _inventory.GetMesoCount();
            RefreshDynamicUserEntries();
            ApplyPendingPacketOwnedUserSellMutationParity();
            UpdateActionButtonStates();
        }

        private void FinishRejectedRequest(AdminShopEntry entry, string stateLabel, string footerMessage, long refundAmount)
        {
            if (refundAmount > 0 && _inventory != null)
            {
                _inventory.AddMeso(refundAmount);
            }

            entry.State = AdminShopEntryState.RequestRejected;
            entry.StateLabel = stateLabel;
            PersistEntrySessionState(entry);
            _footerMessage = footerMessage;
            ResetPendingRequestState();
            ClearPendingPacketOwnedUserSellSnapshot();
            Money = _inventory?.GetMesoCount() ?? Money;
            UpdateActionButtonStates();
        }

        private void ResetPendingRequestState()
        {
            _pendingRequestEntry = null;
            _pendingRequestQuantity = 1;
            _pendingRequestPreviousState = default;
            _pendingRequestPreviousStateLabel = string.Empty;
            _pendingStorageExpansionAwaitingPacketResult = false;
            _pendingStorageExpansionCommoditySerialNumber = 0;
            _pendingPacketOwnedAdminShopResult = false;
            _packetOwnedAdminShopSession.ClearWaitingForResult();
            _requestResolveTick = 0;
        }

        private void RestorePendingRequestState(AdminShopEntry entry)
        {
            if (entry?.State != AdminShopEntryState.PendingResponse)
            {
                return;
            }

            entry.State = _pendingRequestPreviousState;
            entry.StateLabel = _pendingRequestPreviousStateLabel ?? string.Empty;
        }

        private void CapturePendingPacketOwnedUserSellSnapshot(AdminShopEntry entry)
        {
            ClearPendingPacketOwnedUserSellSnapshot();
            if (!_packetOwnedAdminShopSession.IsActive
                || entry == null
                || !RequiresInventorySource(entry)
                || entry.InventoryExpansionType != InventoryType.NONE
                || entry.IsStorageExpansion
                || entry.SourceInventoryType == InventoryType.NONE)
            {
                return;
            }

            _pendingPacketOwnedUserSellSnapshotInventoryType = entry.SourceInventoryType;
            _pendingPacketOwnedUserSellSnapshotScrollOffset = Math.Max(0, _paneStates[AdminShopPane.User].ScrollOffset);
            _pendingPacketOwnedUserSellSnapshotRows.AddRange(BuildUserSellMutationRows(entry.SourceInventoryType));
        }

        private void ApplyPendingPacketOwnedUserSellMutationParity()
        {
            if (_pendingPacketOwnedUserSellSnapshotInventoryType == InventoryType.NONE)
            {
                return;
            }

            IReadOnlyList<AdminShopUserSellMutationRow> currentRows = BuildUserSellMutationRows(_pendingPacketOwnedUserSellSnapshotInventoryType);
            AdminShopUserSellMutationResolution resolution = AdminShopUserSellMutationParity.Resolve(
                _pendingPacketOwnedUserSellSnapshotRows,
                currentRows,
                _pendingPacketOwnedUserSellSnapshotScrollOffset,
                MaxVisibleRows);

            _activePane = AdminShopPane.User;
            _activeBrowseMode = AdminShopBrowseMode.All;
            _activeCategory = ResolveCategoryForInventoryType(_pendingPacketOwnedUserSellSnapshotInventoryType);
            ApplyFilters();

            AdminShopPaneState paneState = _paneStates[AdminShopPane.User];
            paneState.SelectedIndex = resolution.SelectedIndex >= 0 && resolution.SelectedIndex < paneState.Entries.Count
                ? resolution.SelectedIndex
                : paneState.Entries.Count == 0 ? -1 : Math.Clamp(resolution.SelectedIndex, 0, paneState.Entries.Count - 1);
            paneState.ScrollOffset = resolution.SelectedIndex < 0
                ? Math.Max(0, paneState.ScrollOffset)
                : resolution.ScrollOffset;
            ClampPaneState(paneState);
            PersistBrowseSurfaceState(AdminShopPane.User);
            UpdateRowButtons();
            _footerMessage = string.IsNullOrWhiteSpace(_footerMessage)
                ? "Applied packet-owned sell-list mutation parity."
                : $"{_footerMessage} Rebuilt the user pane through the recovered CmpSellItem-style selection and scrollbar mutation.";
            ClearPendingPacketOwnedUserSellSnapshot();
        }

        private List<AdminShopUserSellMutationRow> BuildUserSellMutationRows(InventoryType inventoryType)
        {
            return BuildUserSellMutationRows(_paneStates[AdminShopPane.User].SourceEntries, inventoryType);
        }

        private static List<AdminShopUserSellMutationRow> BuildUserSellMutationRows(
            IEnumerable<AdminShopEntry> entries,
            InventoryType inventoryType)
        {
            List<AdminShopUserSellMutationRow> rows = new();
            if (inventoryType == InventoryType.NONE || entries == null)
            {
                return rows;
            }

            foreach (AdminShopEntry entry in entries)
            {
                if (entry == null
                    || !entry.IsPacketOwnedSnapshotRow
                    || entry.SourceInventoryType != inventoryType
                    || entry.SourceItemId <= 0
                    || entry.InventorySlotIndex < 0)
                {
                    continue;
                }

                rows.Add(new AdminShopUserSellMutationRow(
                    inventoryType,
                    entry.SourceItemId,
                    entry.InventorySlotIndex + 1,
                    Math.Max(0, entry.DisplayQuantity)));
            }

            return rows;
        }

        private void ClearPendingPacketOwnedUserSellSnapshot()
        {
            _pendingPacketOwnedUserSellSnapshotRows.Clear();
            _pendingPacketOwnedUserSellSnapshotInventoryType = InventoryType.NONE;
            _pendingPacketOwnedUserSellSnapshotScrollOffset = 0;
        }

        private void TryReselectPacketOwnedPendingEntry(AdminShopEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            foreach (AdminShopPane pane in Enum.GetValues(typeof(AdminShopPane)))
            {
                AdminShopPaneState paneState = _paneStates[pane];
                int index = paneState.Entries.IndexOf(entry);
                if (index < 0)
                {
                    continue;
                }

                _activePane = pane;
                paneState.SelectedIndex = index;
                ClampPaneState(paneState);
                PersistBrowseSurfaceState(pane);
                UpdateRowButtons();
                return;
            }
        }

        private void ResetPacketOwnedAdminShopSelectionState()
        {
            foreach (AdminShopPane pane in Enum.GetValues(typeof(AdminShopPane)))
            {
                AdminShopPaneState paneState = _paneStates[pane];
                paneState.SelectedIndex = -1;
                paneState.ScrollOffset = 0;
                PersistBrowseSurfaceState(pane);
            }

            UpdateRowButtons();
        }

        private void RestorePacketOwnedOpenViewState(AdminShopPacketOwnedOpenViewState viewState)
        {
            _activePane = AdminShopPacketOwnedOpenViewParity.ClampPaneIndex(viewState.ActivePaneIndex) == AdminShopPacketOwnedOpenViewParity.UserPaneIndex
                ? AdminShopPane.User
                : AdminShopPane.Npc;
            _activeBrowseMode = (AdminShopBrowseMode)AdminShopPacketOwnedOpenViewParity.ClampBrowseModeIndex(viewState.BrowseModeIndex);
            _activeCategory = (AdminShopCategory)AdminShopPacketOwnedOpenViewParity.ClampCategoryIndex(viewState.CategoryIndex);
            ApplyFilters(preserveActivePane: true);

            foreach (AdminShopPane pane in Enum.GetValues(typeof(AdminShopPane)))
            {
                AdminShopPaneState paneState = _paneStates[pane];
                paneState.SelectedIndex = -1;
                paneState.ScrollOffset = 0;
                ClampPaneState(paneState);
                PersistBrowseSurfaceState(pane);
            }

            UpdateRowButtons();
        }

        private static string BuildPacketOwnedAdminShopOutboundSummary(int mode, int value)
        {
            return mode switch
            {
                PacketOwnedAdminShopCloseMode => "Mirrored CAdminShopDlg outbound opcode 74 mode 2.",
                PacketOwnedAdminShopTradeRequestMode when value > 0 => $"Mirrored CAdminShopDlg::SendTradeRequest opcode 74 mode 1 for SN {value}.",
                PacketOwnedAdminShopTradeRequestMode => "Mirrored CAdminShopDlg::SendTradeRequest opcode 74 mode 1.",
                PacketOwnedAdminShopWishlistRegisterMode when value > 0 => $"Mirrored CUIAdminShopWishList::SendRegisterPacket opcode 74 mode 3 for item {value}.",
                PacketOwnedAdminShopWishlistRegisterMode => "Mirrored CUIAdminShopWishList::SendRegisterPacket opcode 74 mode 3.",
                _ when value > 0 => $"Mirrored CAdminShopDlg outbound opcode 74 mode 0 for NPC {value}.",
                _ => "Mirrored CAdminShopDlg outbound opcode 74 mode 0."
            };
        }

        private string BuildPacketOwnedAdminShopStateSummary()
        {
            return _packetOwnedAdminShopSession.BuildStateSummary(_packetOwnedAdminShopRows);
        }

        private string GetEntryStateText(AdminShopEntry entry)
        {
            string baseText;
            if (RequiresInventorySource(entry) && entry?.State == AdminShopEntryState.Available)
            {
                baseText = "Status: ready to submit once the matching inventory source item is present.";
            }
            else
            {
                baseText = entry?.State switch
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

            string storageSummary = entry?.IsStorageExpansion == true
                ? GetStorageExpansionStatusSummary?.Invoke()
                : null;
            string packetSummary = _packetOwnedAdminShopSession.IsActive && _packetOwnedAdminShopSession.IsWaitingForResult
                ? "Packet-owned admin-shop result is pending."
                : string.Empty;
            string combined = string.IsNullOrWhiteSpace(storageSummary)
                ? baseText
                : $"{baseText} {storageSummary}";
            return string.IsNullOrWhiteSpace(packetSummary)
                ? combined
                : $"{combined} {packetSummary}";
        }

        private void CompleteStorageExpansionRequest(
            AdminShopEntry entry,
            AdminShopEntryState entryState,
            string stateLabel,
            string footerMessage,
            int resultSubtype,
            int failureReason,
            bool isPacketOwned = false,
            int packetType = 0,
            int cashItemResultSubtype = 0,
            bool cashAlreadySettled = false,
            bool markPurchased = false)
        {
            if (entry != null)
            {
                entry.State = entryState;
                entry.StateLabel = stateLabel ?? string.Empty;
                if (markPurchased)
                {
                    MarkEntryPurchased(entry);
                }

                PersistEntrySessionState(entry);
            }

            StorageExpansionResolved?.Invoke(new StorageExpansionResolution
            {
                CashItemResultSubtype = Math.Max(0, cashItemResultSubtype),
                CommoditySerialNumber = ResolveStorageExpansionCommoditySerialNumber?.Invoke() ?? entry?.CommoditySerialNumber ?? 0,
                ResultSubtype = resultSubtype,
                FailureReason = failureReason,
                NxPrice = Math.Max(0L, entry?.Price ?? 0L),
                SlotLimitAfterResult = _storageRuntime?.GetSlotLimit() ?? 0,
                IsPacketOwned = isPacketOwned,
                PacketType = Math.Max(0, packetType),
                CashAlreadySettled = cashAlreadySettled,
                Message = footerMessage ?? string.Empty
            });

            _footerMessage = footerMessage;
            ResetPendingRequestState();
            UpdateActionButtonStates();
        }

        public bool TryApplyPacketOwnedStorageExpansionResult(PacketOwnedStorageExpansionResult packetResult, out string message)
        {
            message = "Packet-owned storage-expansion result could not be applied.";
            if (packetResult == null)
            {
                message = "Packet-owned storage-expansion result data is missing.";
                return false;
            }

            if (_pendingRequestEntry == null || !_pendingRequestEntry.IsStorageExpansion)
            {
                message = "No pending storage-expansion request is waiting on the Cash Shop seam.";
                return false;
            }

            int expectedCommoditySerialNumber = _pendingStorageExpansionCommoditySerialNumber > 0
                ? _pendingStorageExpansionCommoditySerialNumber
                : ResolveStorageExpansionCommoditySerialNumberForEntry(_pendingRequestEntry);
            if (expectedCommoditySerialNumber > 0
                && packetResult.CommoditySerialNumber > 0
                && packetResult.CommoditySerialNumber != expectedCommoditySerialNumber)
            {
                message = $"Packet-owned storage-expansion result for SN {packetResult.CommoditySerialNumber.ToString(CultureInfo.InvariantCulture)} does not match the pending request SN {expectedCommoditySerialNumber.ToString(CultureInfo.InvariantCulture)}.";
                return false;
            }

            long nxPrice = Math.Max(0L, packetResult.NxPrice > 0 ? packetResult.NxPrice : _pendingRequestEntry.Price);
            int slotLimitBeforeResult = _storageRuntime?.GetSlotLimit() ?? 0;
            if (packetResult.ResultSubtype == StorageExpansionResultSubtype.Success)
            {
                if (_storageRuntime == null)
                {
                    CompleteStorageExpansionRequest(
                        _pendingRequestEntry,
                        AdminShopEntryState.RequestRejected,
                        "No runtime",
                        "Storage runtime is unavailable for packet-authored slot expansion.",
                        StorageExpansionResultSubtype.Rejected,
                        StorageExpansionFailureReason.RuntimeUnavailable);
                    message = _footerMessage;
                    return false;
                }

                if (packetResult.ConsumeCash && !TryConsumeStorageExpansionCash(nxPrice))
                {
                    CompleteStorageExpansionRequest(
                        _pendingRequestEntry,
                        AdminShopEntryState.RequestRejected,
                        "Need NX",
                        $"Packet-owned storage-expansion success for SN {(packetResult.CommoditySerialNumber > 0 ? packetResult.CommoditySerialNumber.ToString(CultureInfo.InvariantCulture) : "local seam")} was rejected locally because the account does not have {FormatCashPriceLabel(nxPrice)} available.",
                        StorageExpansionResultSubtype.Rejected,
                        StorageExpansionFailureReason.NotEnoughCash);
                    message = _footerMessage;
                    return false;
                }

                int requestedSlotLimit = Math.Max(0, packetResult.SlotLimitAfterResult);
                if (requestedSlotLimit > 0)
                {
                    _storageRuntime.SetSlotLimit(requestedSlotLimit);
                }
                else if (_storageRuntime.CanExpandSlotLimit())
                {
                    _storageRuntime.TryExpandSlotLimit();
                }

                int slotLimitAfterResult = _storageRuntime.GetSlotLimit();
                if (slotLimitAfterResult <= slotLimitBeforeResult && requestedSlotLimit <= slotLimitBeforeResult)
                {
                    CompleteStorageExpansionRequest(
                        _pendingRequestEntry,
                        AdminShopEntryState.RequestRejected,
                        "Retry",
                        "The packet-authored storage-expansion result did not advance the storage slot limit.",
                        StorageExpansionResultSubtype.Rejected,
                        StorageExpansionFailureReason.ExpansionFailed,
                        isPacketOwned: true,
                        packetType: packetResult.PacketType,
                        cashItemResultSubtype: packetResult.CashItemResultSubtype,
                        cashAlreadySettled: !packetResult.ConsumeCash);
                    message = _footerMessage;
                    return false;
                }

                string footerMessage = string.IsNullOrWhiteSpace(packetResult.Message)
                    ? $"Packet-owned storage-expansion result accepted for SN {(expectedCommoditySerialNumber > 0 ? expectedCommoditySerialNumber.ToString(CultureInfo.InvariantCulture) : "local seam")}. Storage now has {slotLimitAfterResult.ToString(CultureInfo.InvariantCulture)} slots."
                    : packetResult.Message;
                CompleteStorageExpansionRequest(
                    _pendingRequestEntry,
                    AdminShopEntryState.RequestAccepted,
                    "Expanded",
                    footerMessage,
                    StorageExpansionResultSubtype.Success,
                    StorageExpansionFailureReason.None,
                    isPacketOwned: true,
                    packetType: packetResult.PacketType,
                    cashItemResultSubtype: packetResult.CashItemResultSubtype,
                    cashAlreadySettled: !packetResult.ConsumeCash,
                    markPurchased: true);
                message = _footerMessage;
                return true;
            }

            string rejectionMessage = string.IsNullOrWhiteSpace(packetResult.Message)
                ? BuildPacketOwnedStorageExpansionFailureMessage(packetResult.FailureReason, expectedCommoditySerialNumber, nxPrice)
                : packetResult.Message;
            CompleteStorageExpansionRequest(
                _pendingRequestEntry,
                AdminShopEntryState.RequestRejected,
                "Rejected",
                rejectionMessage,
                StorageExpansionResultSubtype.Rejected,
                Math.Max(StorageExpansionFailureReason.None, packetResult.FailureReason),
                isPacketOwned: true,
                packetType: packetResult.PacketType,
                cashItemResultSubtype: packetResult.CashItemResultSubtype,
                cashAlreadySettled: !packetResult.ConsumeCash);
            message = _footerMessage;
            return true;
        }

        private int ResolveStorageExpansionCommoditySerialNumberForEntry(AdminShopEntry entry)
        {
            if (entry == null || !entry.IsStorageExpansion)
            {
                return 0;
            }

            int resolvedSerialNumber = ResolveStorageExpansionCommoditySerialNumber?.Invoke() ?? 0;
            if (resolvedSerialNumber > 0)
            {
                return resolvedSerialNumber;
            }

            return Math.Max(0, entry.CommoditySerialNumber);
        }

        private string BuildPacketOwnedStorageExpansionFailureMessage(int failureReason, int commoditySerialNumber, long nxPrice)
        {
            string commodityLabel = commoditySerialNumber > 0
                ? $"SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)}"
                : "the pending storage-expansion seam";

            return failureReason switch
            {
                StorageExpansionFailureReason.SlotCapReached => $"{commodityLabel} was rejected because storage is already at the current slot cap.",
                StorageExpansionFailureReason.UnauthorizedCharacter => $"{commodityLabel} was rejected because the current character is not authorized for this storage account.",
                StorageExpansionFailureReason.SessionLocked => $"{commodityLabel} was rejected because the trunk session is no longer active.",
                StorageExpansionFailureReason.MissingAccountAuthority => $"{commodityLabel} was rejected because the account PIC or secondary password was not verified.",
                StorageExpansionFailureReason.MissingStoragePasscode => $"{commodityLabel} was rejected because the storage passcode was not verified.",
                StorageExpansionFailureReason.NotEnoughCash => $"{commodityLabel} was rejected because the account does not have {FormatCashPriceLabel(nxPrice)} available.",
                StorageExpansionFailureReason.ExpansionFailed => $"{commodityLabel} reached the packet-owned Cash Shop seam, but the storage slot limit did not advance.",
                StorageExpansionFailureReason.RuntimeUnavailable => "The packet-owned storage-expansion result could not be applied because the storage runtime is unavailable.",
                _ => $"{commodityLabel} was rejected by the packet-owned Cash Shop result seam."
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
            if (_packetOwnedAdminShopSession.IsWaitingForResult && _pendingRequestEntry != null)
            {
                return $"Waiting for packet-owned CAdminShopDlg::OnPacket result on {_pendingRequestEntry.Title}.";
            }

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

            if (RequiresInventorySource(entry))
            {
                return "BtBuy or BtSell submits a zero-price trade request after validating the required inventory item.";
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

        private AdminShopCategory ResolveWishlistCategory(int categoryIndex)
        {
            return ResolveWishlistCategory(GetWishlistCategoryKey(categoryIndex));
        }

        private static string GetWishlistCategoryKey(int categoryIndex)
        {
            return categoryIndex switch
            {
                0 => "all",
                1 => "equip",
                2 => "use",
                3 => "setup",
                4 => "etc",
                5 => "cash",
                6 => "recipe",
                7 => "scroll",
                8 => "special",
                9 => "package",
                _ => "all"
            };
        }

        private static AdminShopCategory ResolveWishlistCategory(string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
            {
                return AdminShopCategory.All;
            }

            return categoryKey.ToLowerInvariant() switch
            {
                "equip" or "equip.hat" or "equip.face" or "equip.eye" or "equip.earring" or "equip.top" or "equip.bottom" or "equip.shoes" or "equip.glove" or "equip.cape" or "equip.weapon" => AdminShopCategory.Equip,
                "use" or "use.coupon" or "use.travel" or "use.projectile" or "use.pet" or "use.utility" => AdminShopCategory.Use,
                "setup" or "setup.chair" or "setup.mount" or "setup.decor" or "setup.effect" => AdminShopCategory.Setup,
                "etc" or "etc.crafting" or "etc.utility" => AdminShopCategory.Etc,
                "cash" or "cash.expand" or "cash.consume" => AdminShopCategory.Cash,
                "recipe" or "recipe.anvil" => AdminShopCategory.Recipe,
                "scroll" or "scroll.enhance" => AdminShopCategory.Scroll,
                "special" or "special.hair" or "special.face" or "special.skin" or "special.lens" or "special.style" or "special.utility" => AdminShopCategory.Special,
                "package" or "package.bundle" => AdminShopCategory.Package,
                _ => AdminShopCategory.All
            };
        }

        private static bool MatchesWishlistCategory(AdminShopEntry entry, string categoryKey)
        {
            if (entry == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(categoryKey) || string.Equals(categoryKey, "all", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (s_wishlistCategoryLeaves.TryGetValue(categoryKey, out WishlistCategoryLeafDefinition leaf))
            {
                return leaf.Matches?.Invoke(entry) == true;
            }

            return MatchesCategory(entry, ResolveWishlistCategory(categoryKey));
        }

        private bool MatchesWishlistPriceRange(AdminShopEntry entry, int priceRangeIndex)
        {
            if (entry == null)
            {
                return false;
            }

            int normalizedIndex = Math.Clamp(priceRangeIndex, 0, _wishlistPriceRanges.Count - 1);
            WishlistPriceRange range = _wishlistPriceRanges[normalizedIndex];
            if (normalizedIndex == _wishlistPriceRanges.Count - 1)
            {
                return entry.Price >= range.MinimumPrice && entry.Price <= range.MaximumPrice;
            }

            return entry.Price >= range.MinimumPrice && entry.Price < range.MaximumPrice;
        }

        private string GetWishlistPriceRangeLabel(int priceRangeIndex)
        {
            int normalizedIndex = Math.Clamp(priceRangeIndex, 0, _wishlistPriceRanges.Count - 1);
            return _wishlistPriceRanges[normalizedIndex].Label;
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
                : $"{entry.Title}|{entry.Seller}|{entry.Price}|{entry.RewardItemId}|{entry.InventoryExpansionType}|{entry.IsStorageExpansion}|{entry.SourceInventoryType}|{entry.SourceItemId}|{entry.SourceItemQuantity}|{entry.InventorySlotIndex}|{entry.PacketSerialNumber}|{entry.PacketSaleState}|{entry.IsPacketOwnedSnapshotRow}";
        }

        private static AdminShopCategory GetQuickCategory(int tabIndex)
        {
            return tabIndex switch
            {
                0 => AdminShopCategory.Equip,
                1 => AdminShopCategory.Use,
                2 => AdminShopCategory.Setup,
                3 => AdminShopCategory.Etc,
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

        private static IReadOnlyList<WishlistCategoryNode> BuildWishlistCategoryTree()
        {
            // CUIAdminShopWishListCategory::OnCreate pushes 11 top-level rows and then
            // four child stages sourced from StringPool ids:
            // stage 1 = 5 rows, stage 2 = 4 rows, stage 3 = 6 rows, stage 4 = 10 rows.
            // MapleStoryStringPool now supplies the recovered client labels where the
            // extracted table has them, with the authored English strings kept only
            // as local fallbacks for unresolved ids.
            WishlistCategoryClientDefinition[] definitions =
            {
                new WishlistCategoryClientDefinition { Key = "all", FallbackLabel = "All", StringPoolId = 634 },
                new WishlistCategoryClientDefinition
                {
                    Key = "use",
                    FallbackLabel = "Use",
                    StringPoolId = 637,
                    ChildStageIndex = 1,
                    Children = new[]
                    {
                        new WishlistCategoryClientDefinition { Key = "use.coupon", FallbackLabel = "Coupon", StringPoolId = 653 },
                        new WishlistCategoryClientDefinition { Key = "use.travel", FallbackLabel = "Travel", StringPoolId = 654 },
                        new WishlistCategoryClientDefinition { Key = "use.projectile", FallbackLabel = "Projectile", StringPoolId = 655 },
                        new WishlistCategoryClientDefinition { Key = "use.pet", FallbackLabel = "Pet", StringPoolId = 656 },
                        new WishlistCategoryClientDefinition { Key = "use.utility", FallbackLabel = "Utility", StringPoolId = 6755 }
                    }
                },
                new WishlistCategoryClientDefinition
                {
                    Key = "setup",
                    FallbackLabel = "Set-up",
                    StringPoolId = 639,
                    ChildStageIndex = 2,
                    Children = new[]
                    {
                        new WishlistCategoryClientDefinition { Key = "setup.chair", FallbackLabel = "Chair", StringPoolId = 657 },
                        new WishlistCategoryClientDefinition { Key = "setup.mount", FallbackLabel = "Mount", StringPoolId = 658 },
                        new WishlistCategoryClientDefinition { Key = "setup.decor", FallbackLabel = "Decor", StringPoolId = 659 },
                        new WishlistCategoryClientDefinition { Key = "setup.effect", FallbackLabel = "Effect", StringPoolId = 660 }
                    }
                },
                new WishlistCategoryClientDefinition { Key = "etc", FallbackLabel = "Etc", StringPoolId = 640 },
                new WishlistCategoryClientDefinition { Key = "cash", FallbackLabel = "Cash", StringPoolId = 649 },
                new WishlistCategoryClientDefinition { Key = "recipe", FallbackLabel = "Recipe", StringPoolId = 642 },
                new WishlistCategoryClientDefinition { Key = "scroll", FallbackLabel = "Scroll", StringPoolId = 641 },
                new WishlistCategoryClientDefinition { Key = "package", FallbackLabel = "Package", StringPoolId = 644 },
                new WishlistCategoryClientDefinition { Key = "pet", FallbackLabel = "Pet", StringPoolId = 643 },
                new WishlistCategoryClientDefinition
                {
                    Key = "special",
                    FallbackLabel = "Special",
                    StringPoolId = 651,
                    ChildStageIndex = 3,
                    Children = new[]
                    {
                        new WishlistCategoryClientDefinition { Key = "special.hair", FallbackLabel = "Hair", StringPoolId = 618 },
                        new WishlistCategoryClientDefinition { Key = "special.face", FallbackLabel = "Face", StringPoolId = 619 },
                        new WishlistCategoryClientDefinition { Key = "special.skin", FallbackLabel = "Skin", StringPoolId = 620 },
                        new WishlistCategoryClientDefinition { Key = "special.lens", FallbackLabel = "Lens", StringPoolId = 621 },
                        new WishlistCategoryClientDefinition { Key = "special.style", FallbackLabel = "Style", StringPoolId = 622 },
                        new WishlistCategoryClientDefinition { Key = "special.utility", FallbackLabel = "Utility", StringPoolId = 623 }
                    }
                },
                new WishlistCategoryClientDefinition
                {
                    Key = "equip",
                    FallbackLabel = "Equip",
                    StringPoolId = 652,
                    ChildStageIndex = 4,
                    Children = new[]
                    {
                        new WishlistCategoryClientDefinition { Key = "equip.hat", FallbackLabel = "Hat", StringPoolId = 624 },
                        new WishlistCategoryClientDefinition { Key = "equip.face", FallbackLabel = "Face accessory", StringPoolId = 625 },
                        new WishlistCategoryClientDefinition { Key = "equip.eye", FallbackLabel = "Eye accessory", StringPoolId = 626 },
                        new WishlistCategoryClientDefinition { Key = "equip.earring", FallbackLabel = "Earring", StringPoolId = 627 },
                        new WishlistCategoryClientDefinition { Key = "equip.top", FallbackLabel = "Top / Overall", StringPoolId = 628 },
                        new WishlistCategoryClientDefinition { Key = "equip.bottom", FallbackLabel = "Bottom", StringPoolId = 629 },
                        new WishlistCategoryClientDefinition { Key = "equip.shoes", FallbackLabel = "Shoes", StringPoolId = 630 },
                        new WishlistCategoryClientDefinition { Key = "equip.glove", FallbackLabel = "Glove", StringPoolId = 631 },
                        new WishlistCategoryClientDefinition { Key = "equip.cape", FallbackLabel = "Cape", StringPoolId = 632 },
                        new WishlistCategoryClientDefinition { Key = "equip.weapon", FallbackLabel = "Weapon", StringPoolId = 633 }
                    }
                }
            };

            return definitions.Select(definition => CreateWishlistCategoryNode(definition, string.Empty)).ToArray();
        }

        private static IReadOnlyList<WishlistPriceRange> BuildWishlistPriceRanges()
        {
            static IReadOnlyList<WishlistPriceRange> BuildDefaultRanges()
            {
                return new[]
                {
                    new WishlistPriceRange { Index = 0, MinimumPrice = 0, MaximumPrice = 1000, Label = "0 - 999 mesos" },
                    new WishlistPriceRange { Index = 1, MinimumPrice = 1000, MaximumPrice = 3000, Label = "1,000 - 2,999 mesos" },
                    new WishlistPriceRange { Index = 2, MinimumPrice = 3000, MaximumPrice = 5000, Label = "3,000 - 4,999 mesos" },
                    new WishlistPriceRange { Index = 3, MinimumPrice = 5000, MaximumPrice = 50000, Label = "5,000 - 50,000 mesos" }
                };
            }

            static bool TryReadPriceBound(WzImageProperty rangeProperty, string boundName, out long value)
            {
                value = 0;
                if (rangeProperty is not WzSubProperty rangeSubProperty)
                {
                    return false;
                }

                WzImageProperty boundProperty = rangeSubProperty[boundName];
                if (boundProperty is WzIntProperty intProperty)
                {
                    value = intProperty.Value;
                    return true;
                }

                if (boundProperty is WzShortProperty shortProperty)
                {
                    value = shortProperty.Value;
                    return true;
                }

                if (boundProperty is WzLongProperty longProperty)
                {
                    value = longProperty.Value;
                    return true;
                }

                return false;
            }

            WzImage cashShopImage = global::HaCreator.Program.FindImage("UI", "CashShop.img");
            if (cashShopImage == null)
            {
                return BuildDefaultRanges();
            }

            cashShopImage.ParseImage();
            WzSubProperty priceProperty = ((cashShopImage["CSItemSearch"] as WzSubProperty)?["Price"]) as WzSubProperty;
            if (priceProperty == null)
            {
                return BuildDefaultRanges();
            }

            List<(int Index, long Minimum, long Maximum)> stagedRanges = priceProperty.WzProperties
                .OfType<WzImageProperty>()
                .Select(property =>
                {
                    bool hasIndex = int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedIndex);
                    bool hasMin = TryReadPriceBound(property, "0", out long minimumPrice);
                    bool hasMax = TryReadPriceBound(property, "1", out long maximumPrice);
                    return new
                    {
                        hasIndex,
                        parsedIndex,
                        hasMin,
                        minimumPrice,
                        hasMax,
                        maximumPrice
                    };
                })
                .Where(entry => entry.hasIndex
                                && entry.parsedIndex >= 0
                                && entry.hasMin
                                && entry.hasMax
                                && entry.maximumPrice > entry.minimumPrice)
                .OrderBy(entry => entry.parsedIndex)
                .Select(entry => (entry.parsedIndex, entry.minimumPrice, entry.maximumPrice))
                .ToList();

            if (stagedRanges.Count == 0)
            {
                return BuildDefaultRanges();
            }

            List<WishlistPriceRange> resolvedRanges = new(stagedRanges.Count);
            for (int i = 0; i < stagedRanges.Count; i++)
            {
                (int _, long minimumPrice, long maximumPrice) = stagedRanges[i];
                bool isLast = i == stagedRanges.Count - 1;
                long displayMaximum = isLast ? maximumPrice : maximumPrice - 1;
                string label = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:N0} - {1:N0} mesos",
                    minimumPrice,
                    displayMaximum);
                resolvedRanges.Add(new WishlistPriceRange
                {
                    Index = i,
                    MinimumPrice = minimumPrice,
                    MaximumPrice = maximumPrice,
                    Label = label
                });
            }

            return resolvedRanges;
        }

        private static WishlistCategoryNode CreateWishlistCategoryNode(WishlistCategoryClientDefinition definition, string parentKey)
        {
            if (definition == null)
            {
                return null;
            }

            return new WishlistCategoryNode
            {
                Key = definition.Key,
                Label = MapleStoryStringPool.GetOrFallback(definition.StringPoolId, definition.FallbackLabel),
                ParentKey = parentKey ?? string.Empty,
                ClientStringPoolId = definition.StringPoolId,
                ClientChildStageIndex = definition.ChildStageIndex,
                Children = definition.Children?.Select(child => CreateWishlistCategoryNode(child, definition.Key)).Where(child => child != null).ToArray()
                           ?? Array.Empty<WishlistCategoryNode>()
            };
        }

        private static IReadOnlyDictionary<string, WishlistCategoryLeafDefinition> BuildWishlistCategoryLeaves()
        {
            WishlistCategoryLeafDefinition[] leaves =
            {
                new WishlistCategoryLeafDefinition
                {
                    Key = "use.coupon",
                    Label = "Coupon",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Use) && ContainsAny(entry, "coupon", "ticket")
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "use.travel",
                    Label = "Travel",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Use) && (ContainsAny(entry, "teleport", "rock", "travel") || IsItemIdInRange(entry.RewardItemId, 5040000, 5049999))
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "use.projectile",
                    Label = "Projectile",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Use) && (IsItemIdInRange(entry.RewardItemId, 2060000, 2079999) || ContainsAny(entry, "star", "knife"))
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "use.pet",
                    Label = "Pet",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Use) && IsPetEntry(entry)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "use.utility",
                    Label = "Utility",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Use) && !ContainsAny(entry, "coupon", "ticket", "teleport", "rock", "travel", "star", "knife", "pet", "snack")
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "setup.chair",
                    Label = "Chair",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Setup) && (IsItemIdInRange(entry.RewardItemId, 3010000, 3019999) || ContainsAny(entry, "chair"))
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "setup.mount",
                    Label = "Mount",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Setup) && (IsItemIdInRange(entry.RewardItemId, 1900000, 1919999) || ContainsAny(entry, "mount", "saddle"))
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "setup.decor",
                    Label = "Decor",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Setup) && !IsItemIdInRange(entry.RewardItemId, 3010000, 3019999) && !ContainsAny(entry, "chair", "mount", "saddle", "effect")
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "setup.effect",
                    Label = "Effect",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Setup) && ContainsAny(entry, "effect", "label ring", "name tag", "weather")
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "etc.crafting",
                    Label = "Crafting",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Etc) && IsCraftingEntry(entry)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "etc.utility",
                    Label = "Utility",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Etc) && !IsCraftingEntry(entry)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "cash.expand",
                    Label = "Expansion",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Cash) && (entry.IsStorageExpansion || entry.InventoryExpansionType != InventoryType.NONE || ContainsAny(entry, "inventory"))
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "cash.consume",
                    Label = "Cash use",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Cash) && !(entry.IsStorageExpansion || entry.InventoryExpansionType != InventoryType.NONE)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "recipe.anvil",
                    Label = "Recipe item",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Recipe)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "scroll.enhance",
                    Label = "Enhance",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Scroll)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "pet",
                    Label = "Pet",
                    Matches = entry => IsPetEntry(entry)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "special.hair",
                    Label = "Hair",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Special) && (IsItemIdInRange(entry.RewardItemId, 5150000, 5159999) || ContainsAny(entry, "hair"))
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "special.face",
                    Label = "Face",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Special) && (IsItemIdInRange(entry.RewardItemId, 5160000, 5169999) || ContainsAny(entry, "face"))
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "special.skin",
                    Label = "Skin",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Special) && (IsItemIdInRange(entry.RewardItemId, 5170000, 5179999) || ContainsAny(entry, "skin"))
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "special.lens",
                    Label = "Lens",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Special) && ContainsAny(entry, "lens", "eye color", "color lens")
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "special.style",
                    Label = "Style",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Special) && ContainsAny(entry, "style", "cosmetic", "makeover")
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "special.utility",
                    Label = "Utility",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Special) && !ContainsAny(entry, "hair", "face", "skin", "lens", "eye color", "style", "cosmetic", "makeover")
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "package.bundle",
                    Label = "Bundle",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Package) && (entry.MaxRequestCount > 1 || ContainsAny(entry, "bundle", "pack", "box"))
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "equip.hat",
                    Label = "Hat",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Equip) && HasItemPrefix(entry, 100)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "equip.face",
                    Label = "Face accessory",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Equip) && HasItemPrefix(entry, 101)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "equip.eye",
                    Label = "Eye accessory",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Equip) && HasItemPrefix(entry, 102)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "equip.earring",
                    Label = "Earring",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Equip) && HasItemPrefix(entry, 103)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "equip.top",
                    Label = "Top / Overall",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Equip) && HasItemPrefix(entry, 104, 105)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "equip.bottom",
                    Label = "Bottom",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Equip) && HasItemPrefix(entry, 106)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "equip.shoes",
                    Label = "Shoes",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Equip) && HasItemPrefix(entry, 107)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "equip.glove",
                    Label = "Glove",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Equip) && HasItemPrefix(entry, 108)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "equip.cape",
                    Label = "Cape",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Equip) && HasItemPrefix(entry, 109, 110)
                },
                new WishlistCategoryLeafDefinition
                {
                    Key = "equip.weapon",
                    Label = "Weapon",
                    Matches = entry => MatchesCategory(entry, AdminShopCategory.Equip) && IsWeaponEntry(entry)
                }
            };

            return leaves.ToDictionary(leaf => leaf.Key, StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, WishlistCategoryNode> BuildWishlistCategoryNodeLookup(IEnumerable<WishlistCategoryNode> roots)
        {
            Dictionary<string, WishlistCategoryNode> lookup = new(StringComparer.OrdinalIgnoreCase);
            if (roots == null)
            {
                return lookup;
            }

            void AddNode(WishlistCategoryNode node)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Key))
                {
                    return;
                }

                lookup[node.Key] = node;
                if (node.Children == null)
                {
                    return;
                }

                foreach (WishlistCategoryNode child in node.Children)
                {
                    AddNode(child);
                }
            }

            foreach (WishlistCategoryNode root in roots)
            {
                AddNode(root);
            }

            return lookup;
        }

        private static bool IsWeaponEntry(AdminShopEntry entry)
        {
            int itemId = entry?.RewardItemId ?? 0;
            int prefix = itemId / 10000;
            return itemId > 0 && ((prefix >= 130 && prefix <= 159) || (prefix >= 170 && prefix <= 171) || (prefix >= 121 && prefix <= 124));
        }

        private static bool IsAccessoryEntry(AdminShopEntry entry)
        {
            int itemId = entry?.RewardItemId ?? 0;
            int prefix = itemId / 10000;
            return itemId > 0 && prefix is 101 or 102 or 103 or 111 or 112 or 113 or 114 or 115 or 116 or 167;
        }

        private static bool IsPetEntry(AdminShopEntry entry)
        {
            return entry != null && (IsItemIdInRange(entry.RewardItemId, 1800000, 1809999)
                                     || IsItemIdInRange(entry.RewardItemId, 5000000, 5009999)
                                     || ContainsAny(entry, "pet", "snack"));
        }

        private static bool HasItemPrefix(AdminShopEntry entry, params int[] prefixes)
        {
            int itemId = entry?.RewardItemId ?? 0;
            if (itemId <= 0 || prefixes == null || prefixes.Length == 0)
            {
                return false;
            }

            int prefix = itemId / 10000;
            return prefixes.Contains(prefix);
        }

        private static bool IsCraftingEntry(AdminShopEntry entry)
        {
            return entry != null && (entry.SourceInventoryType == InventoryType.ETC
                                     || IsItemIdInRange(entry.SourceItemId, 4000000, 4029999)
                                     || IsItemIdInRange(entry.RewardItemId, 4000000, 4029999)
                                     || ContainsAny(entry, "ore", "craft", "mithril", "steel"));
        }

        private static bool IsItemIdInRange(int itemId, int minInclusive, int maxInclusive)
        {
            return itemId >= minInclusive && itemId <= maxInclusive;
        }

        private static bool ContainsAny(AdminShopEntry entry, params string[] terms)
        {
            if (entry == null || terms == null || terms.Length == 0)
            {
                return false;
            }

            string haystack = $"{entry.Title} {entry.Detail} {entry.Seller}";
            for (int i = 0; i < terms.Length; i++)
            {
                if (haystack.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetWishlistSearchIndexEntry(AdminShopEntry entry, out WishlistSearchIndexEntry searchIndexEntry)
        {
            searchIndexEntry = null;
            if (entry?.RewardItemId <= 0)
            {
                return false;
            }

            return GetWishlistSearchIndexByItemId().TryGetValue(entry.RewardItemId, out searchIndexEntry);
        }

        private static IReadOnlyDictionary<int, WishlistSearchIndexEntry> GetWishlistSearchIndexByItemId()
        {
            if (_wishlistSearchIndexByItemId != null)
            {
                return _wishlistSearchIndexByItemId;
            }

            lock (WishlistSearchIndexLock)
            {
                if (_wishlistSearchIndexByItemId != null)
                {
                    return _wishlistSearchIndexByItemId;
                }

                Dictionary<int, WishlistSearchIndexEntry> searchIndex = new();
                WzImage searchImage = global::HaCreator.Program.FindImage("String", "CashItemSearch.img");
                foreach (WzSubProperty itemSearchProperty in searchImage?.WzProperties?.OfType<WzSubProperty>() ?? Enumerable.Empty<WzSubProperty>())
                {
                    if (!int.TryParse(itemSearchProperty.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId) || itemId <= 0)
                    {
                        continue;
                    }

                    HashSet<string> normalizedTerms = new(StringComparer.Ordinal);
                    foreach (WzImageProperty property in itemSearchProperty.WzProperties)
                    {
                        string searchTerm = ResolveWishlistSearchIndexTerm(property);
                        if (string.IsNullOrWhiteSpace(searchTerm))
                        {
                            continue;
                        }

                        string normalizedTerm = NormalizeWishlistSearchText(searchTerm);
                        if (!string.IsNullOrWhiteSpace(normalizedTerm))
                        {
                            normalizedTerms.Add(normalizedTerm);
                        }
                    }

                    if (normalizedTerms.Count == 0)
                    {
                        continue;
                    }

                    string[] orderedTerms = normalizedTerms
                        .OrderBy(term => term.Length)
                        .ThenBy(term => term, StringComparer.Ordinal)
                        .ToArray();
                    searchIndex[itemId] = new WishlistSearchIndexEntry
                    {
                        CombinedText = string.Join(" ", orderedTerms),
                        CollapsedText = string.Concat(orderedTerms.Select(CollapseWishlistSearchText).Where(term => !string.IsNullOrWhiteSpace(term))),
                        Terms = orderedTerms
                    };
                }

                _wishlistSearchIndexByItemId = searchIndex;
                return _wishlistSearchIndexByItemId;
            }
        }

        private static string ResolveWishlistSearchIndexTerm(WzImageProperty property)
        {
            return property switch
            {
                WzStringProperty stringProperty => stringProperty.GetString(),
                WzSubProperty subProperty => string.Join(
                    " ",
                    subProperty.WzProperties
                        .Select(ResolveWishlistSearchIndexTerm)
                        .Where(value => !string.IsNullOrWhiteSpace(value))),
                _ => property?.GetString()
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
                        bestByItemId.TryGetValue(itemId, out AdminShopCommodityData existing);
                        if (existing == null || IsPreferredCommodity(commodity, existing))
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

        private static bool RequiresInventorySource(AdminShopEntry entry)
        {
            return entry?.SourceInventoryType != InventoryType.NONE
                   && entry.SourceItemId > 0
                   && entry.SourceItemQuantity > 0;
        }

        private static int ComputeRequiredSourceQuantity(AdminShopEntry entry, int requestQuantity)
        {
            if (!RequiresInventorySource(entry))
            {
                return 0;
            }

            long quantity = (long)Math.Max(1, entry.SourceItemQuantity) * Math.Max(1, requestQuantity);
            return (int)Math.Min(int.MaxValue, quantity);
        }

        private bool TryValidateInventorySourceRequest(AdminShopEntry entry, int requestQuantity, out string message)
        {
            message = string.Empty;
            if (!RequiresInventorySource(entry))
            {
                return true;
            }

            if (_inventory == null)
            {
                message = "Inventory runtime is unavailable for the zero-price trade request.";
                return false;
            }

            if (InventoryItemMetadataResolver.TryResolveTradeRestrictionFlags(entry.SourceItemId, out bool isCashItem, out bool isNotForSale, out bool isQuestItem)
                && (isCashItem || isNotForSale || isQuestItem))
            {
                message = BuildTradeRestrictedSourceItemMessage(entry, isCashItem, isNotForSale, isQuestItem);
                return false;
            }

            if (!TryResolveSourceInventoryStack(entry, out SourceInventoryStackResolution sourceResolution)
                || sourceResolution.StackQuantity < ComputeRequiredSourceQuantity(entry, requestQuantity))
            {
                message = BuildMissingSourceItemMessage(entry, requestQuantity, sourceResolution);
                return false;
            }

            return true;
        }

        private bool TryConsumeInventorySource(AdminShopEntry entry, int requestQuantity, out string message)
        {
            message = string.Empty;
            if (!RequiresInventorySource(entry))
            {
                return true;
            }

            if (_inventory != null
                && TryResolveSourceInventoryStack(entry, out SourceInventoryStackResolution sourceResolution)
                && _inventory.TryConsumeItemAtSlot(
                    entry.SourceInventoryType,
                    sourceResolution.SlotIndex,
                    entry.SourceItemId,
                    ComputeRequiredSourceQuantity(entry, requestQuantity)))
            {
                return true;
            }

            message = BuildMissingSourceItemMessage(entry, requestQuantity, default);
            return false;
        }

        private string BuildSourceRequirementStatus(AdminShopEntry entry)
        {
            bool hasEligibleStack = TryResolveSourceInventoryStack(entry, out SourceInventoryStackResolution sourceResolution);
            int ownedQuantity = sourceResolution.TotalOwnedQuantity;
            string sourceLabel = ResolveSourceItemLabel(entry);
            int maxRequestCount = ResolveOwnedSourceRequestCount(entry, hasEligibleStack ? sourceResolution.StackQuantity : 0);
            string slotLabel = sourceResolution.SlotIndex >= 0
                ? $"slot {sourceResolution.SlotIndex + 1}"
                : "selected slot";
            return hasEligibleStack && sourceResolution.StackQuantity >= Math.Max(1, entry.SourceItemQuantity)
                ? $"Source: {sourceLabel} ready ({ownedQuantity} owned, {slotLabel} holds {sourceResolution.StackQuantity}, request up to {maxRequestCount})."
                : $"Source: need {sourceLabel} ({ownedQuantity} owned, {slotLabel} is not large enough yet).";
        }

        private string BuildMissingSourceItemMessage(AdminShopEntry entry, int requestQuantity, SourceInventoryStackResolution sourceResolution)
        {
            int ownedQuantity = sourceResolution.TotalOwnedQuantity > 0
                ? sourceResolution.TotalOwnedQuantity
                : _inventory?.GetItemCount(entry.SourceInventoryType, entry.SourceItemId) ?? 0;
            int selectedStackQuantity = Math.Max(0, sourceResolution.StackQuantity);
            string slotLabel = sourceResolution.SlotIndex >= 0
                ? $"selected slot {sourceResolution.SlotIndex + 1}"
                : "selected slot";
            return selectedStackQuantity > 0
                ? $"Need {ResolveSourceItemLabel(entry, ComputeRequiredSourceQuantity(entry, requestQuantity))} before {entry.Title} can be requested ({ownedQuantity} owned, {slotLabel} only has {selectedStackQuantity})."
                : $"Need {ResolveSourceItemLabel(entry, ComputeRequiredSourceQuantity(entry, requestQuantity))} before {entry.Title} can be requested ({ownedQuantity} owned).";
        }

        private static string BuildTradeRestrictedSourceItemMessage(AdminShopEntry entry, bool isCashItem, bool isNotForSale, bool isQuestItem)
        {
            string reason = isCashItem
                ? "cash"
                : isNotForSale
                    ? "not-for-sale"
                    : isQuestItem
                        ? "quest"
                        : "restricted";
            return $"{ResolveSourceItemLabel(entry)} is marked as a {reason} item in WZ info and cannot satisfy this zero-price trade request.";
        }

        private static string ResolveSourceItemLabel(AdminShopEntry entry, int? overrideQuantity = null)
        {
            int quantity = Math.Max(1, overrideQuantity ?? entry?.SourceItemQuantity ?? 1);
            int itemId = entry?.SourceItemId ?? 0;
            string itemName = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedName)
                ? resolvedName
                : itemId.ToString(CultureInfo.InvariantCulture);
            return quantity > 1 ? $"{itemName} x{quantity}" : itemName;
        }

        private int ResolveRewardMaxStack(AdminShopEntry entry)
        {
            if (entry == null)
            {
                return 1;
            }

            if (entry.RewardMaxStackSize > 0)
            {
                return entry.RewardMaxStackSize;
            }

            if (entry.RewardItemId > 0
                && InventoryItemMetadataResolver.TryResolveMaxStackForItem(entry.RewardItemId, out int maxStackSize))
            {
                entry.RewardMaxStackSize = maxStackSize;
                return maxStackSize;
            }

            entry.RewardMaxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(entry.RewardInventoryType);
            return entry.RewardMaxStackSize;
        }

        private bool SupportsQuantityPrompt(AdminShopEntry entry)
        {
            if (entry == null || entry.MaxRequestCount <= 1)
            {
                return false;
            }

            if (RequiresInventorySource(entry))
            {
                if (TryResolveSourceInventoryStack(entry, out SourceInventoryStackResolution sourceResolution)
                    && sourceResolution.StackQuantity >= Math.Max(1, entry.SourceItemQuantity) * 2)
                {
                    return true;
                }
            }

            return entry.RewardInventoryType != InventoryType.NONE
                   && entry.RewardItemId > 0
                   && ResolveRewardMaxStack(entry) > 1;
        }

        private bool RequiresSingleItemRequestConfirmation(AdminShopEntry entry)
        {
            if (entry == null
                || entry.IsStorageExpansion
                || entry.InventoryExpansionType != InventoryType.NONE
                || SupportsQuantityPrompt(entry))
            {
                return false;
            }

            if (RequiresInventorySource(entry))
            {
                return true;
            }

            return entry.RewardItemId > 0
                   && entry.RewardInventoryType != InventoryType.NONE
                   && ResolveRewardMaxStack(entry) <= 1;
        }

        private int ResolveOwnedSourceRequestCount(AdminShopEntry entry, int ownedQuantity)
        {
            if (!RequiresInventorySource(entry))
            {
                return 0;
            }

            int requestUnit = Math.Max(1, entry.SourceItemQuantity);
            int requestCount = ownedQuantity / requestUnit;
            return Math.Max(0, Math.Min(entry.MaxRequestCount, requestCount));
        }

        private bool TryResolveSourceInventoryStack(AdminShopEntry entry, out SourceInventoryStackResolution resolution)
        {
            resolution = default;
            if (!RequiresInventorySource(entry) || _inventory == null)
            {
                return false;
            }

            IReadOnlyList<InventorySlotData> slots = _inventory.GetSlots(entry.SourceInventoryType);
            if (slots == null || slots.Count == 0)
            {
                return false;
            }

            int resolvedSlotIndex = -1;
            int resolvedStackQuantity = 0;
            int totalOwnedQuantity = 0;
            bool requiresSelectedSlot = entry.InventorySlotIndex >= 0;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData slot = slots[i];
                if (slot == null
                    || slot.IsDisabled
                    || slot.ItemId != entry.SourceItemId)
                {
                    continue;
                }

                int stackQuantity = Math.Max(1, slot.Quantity);
                totalOwnedQuantity += stackQuantity;
                if (requiresSelectedSlot && i != entry.InventorySlotIndex)
                {
                    continue;
                }

                if (resolvedSlotIndex >= 0)
                {
                    continue;
                }

                resolvedSlotIndex = i;
                resolvedStackQuantity = stackQuantity;
            }

            resolution = new SourceInventoryStackResolution(resolvedSlotIndex, resolvedStackQuantity, totalOwnedQuantity);
            return resolvedSlotIndex >= 0;
        }

        private string BuildRequestQuantitySummary(AdminShopEntry entry, int requestQuantity)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            long totalPrice = ComputeRequestPrice(entry, requestQuantity);
            if (totalPrice > 0)
            {
                return $"Total: {FormatPriceLabel(totalPrice)}";
            }

            if (RequiresInventorySource(entry))
            {
                return $"Use: {ResolveSourceItemLabel(entry, ComputeRequiredSourceQuantity(entry, requestQuantity))}";
            }

            return $"Deliver: x{ComputeDeliveredQuantity(entry, requestQuantity)}";
        }

        private string BuildRequestConfirmationMessage(AdminShopEntry entry)
        {
            if (entry == null)
            {
                return "Send this shop request?";
            }

            if (RequiresInventorySource(entry))
            {
                return $"Trade {ResolveSourceItemLabel(entry)} for {entry.Title}?";
            }

            long totalPrice = ComputeRequestPrice(entry, 1);
            return totalPrice > 0
                ? $"Request {entry.Title} for {FormatPriceLabel(totalPrice)}?"
                : $"Request {entry.Title}?";
        }

        private AdminShopEntry CreateSyntheticCommodityEntry(AdminShopCommodityData commodity)
        {
            if (commodity == null || commodity.ItemId <= 0)
            {
                return null;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(commodity.ItemId);
            if (inventoryType == InventoryType.NONE)
            {
                return null;
            }

            string title = InventoryItemMetadataResolver.TryResolveItemName(commodity.ItemId, out string itemName)
                ? itemName
                : $"Commodity SN {commodity.SerialNumber}";
            string detail = InventoryItemMetadataResolver.TryResolveItemDescription(commodity.ItemId, out string description)
                ? description
                : "Packet-targeted Cash Shop commodity surfaced from Etc/Commodity.img.";
            if (commodity.PeriodDays > 0)
            {
                detail = string.IsNullOrWhiteSpace(detail)
                    ? $"Period: {commodity.PeriodDays} day(s)."
                    : $"{detail} Period: {commodity.PeriodDays} day(s).";
            }

            int rewardMaxStackSize = InventoryItemMetadataResolver.TryResolveMaxStackForItem(commodity.ItemId, out int resolvedMaxStack)
                ? resolvedMaxStack
                : InventoryItemMetadataResolver.ResolveMaxStack(inventoryType);

            return new AdminShopEntry
            {
                Title = title,
                Detail = detail,
                Seller = "Cash Manager",
                Price = commodity.Price,
                PriceLabel = FormatPriceLabel(commodity.Price),
                Category = ResolveCommodityCategory(inventoryType),
                SupportsWishlist = commodity.OnSale,
                State = commodity.OnSale ? AdminShopEntryState.Available : AdminShopEntryState.PreviewOnly,
                StateLabel = commodity.OnSale ? string.Empty : "Off sale",
                RewardInventoryType = inventoryType,
                RewardItemId = commodity.ItemId,
                RewardQuantity = Math.Max(1, commodity.Count),
                RewardMaxStackSize = rewardMaxStackSize,
                CommoditySerialNumber = commodity.SerialNumber,
                CommodityOnSale = commodity.OnSale
            };
        }

        private AdminShopEntry CreatePacketOwnedCommodityEntry(PacketOwnedAdminShopCommoditySnapshot commodity)
        {
            if (commodity == null || commodity.ItemId <= 0)
            {
                return null;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(commodity.ItemId);
            AdminShopCategory category = ResolveCommodityCategory(inventoryType);
            string title = InventoryItemMetadataResolver.TryResolveItemName(commodity.ItemId, out string itemName)
                ? itemName
                : $"Item {commodity.ItemId.ToString(CultureInfo.InvariantCulture)}";
            string detail = InventoryItemMetadataResolver.TryResolveItemDescription(commodity.ItemId, out string description)
                ? description
                : "Packet-authored admin-shop row staged directly from CAdminShopDlg::SetAdminShopDlg.";
            string packetSummary = $"SN {commodity.SerialNumber.ToString(CultureInfo.InvariantCulture)}, saleState {commodity.SaleState.ToString(CultureInfo.InvariantCulture)}, maxPerSlot {Math.Max(1, commodity.MaxPerSlot).ToString(CultureInfo.InvariantCulture)}.";
            detail = string.IsNullOrWhiteSpace(detail)
                ? packetSummary
                : $"{detail} {packetSummary}";

            bool isBuyRow = commodity.Price > 0;
            bool isAvailable = isBuyRow && commodity.SaleState == 0;
            string stateLabel = isBuyRow
                ? commodity.SaleState == 0
                    ? string.Empty
                    : $"SaleState {commodity.SaleState.ToString(CultureInfo.InvariantCulture)}"
                : "Sell-side";
            AdminShopEntryState state = isBuyRow
                ? commodity.SaleState == 0 ? AdminShopEntryState.Available : AdminShopEntryState.PreviewOnly
                : AdminShopEntryState.PreviewOnly;
            int maxPerSlot = Math.Max(1, commodity.MaxPerSlot);
            int rewardMaxStackSize = InventoryItemMetadataResolver.TryResolveMaxStackForItem(commodity.ItemId, out int resolvedMaxStack)
                ? resolvedMaxStack
                : InventoryItemMetadataResolver.ResolveMaxStack(inventoryType);

            return new AdminShopEntry
            {
                Title = title,
                Detail = detail,
                Seller = _packetOwnedAdminShopSession.NpcTemplateId > 0
                    ? $"NPC {_packetOwnedAdminShopSession.NpcTemplateId.ToString(CultureInfo.InvariantCulture)}"
                    : "Packet-owned shop",
                Price = Math.Abs(commodity.Price),
                PriceLabel = isBuyRow
                    ? FormatPriceLabel(Math.Abs(commodity.Price))
                    : $"Sell {FormatPriceLabel(Math.Abs(commodity.Price))}",
                Category = category,
                SupportsWishlist = _packetOwnedAdminShopSession.AskItemWishlist && isAvailable,
                State = state,
                StateLabel = stateLabel,
                RewardInventoryType = inventoryType,
                RewardItemId = commodity.ItemId,
                RewardQuantity = 1,
                RewardMaxStackSize = Math.Max(1, rewardMaxStackSize),
                Response = isAvailable ? AdminShopResponse.GrantItem : AdminShopResponse.None,
                MaxRequestCount = maxPerSlot,
                PacketSerialNumber = commodity.SerialNumber,
                PacketSaleState = commodity.SaleState,
                IsPacketOwnedSnapshotRow = true
            };
        }

        private AdminShopEntry CreatePacketOwnedSellTemplateEntry(PacketOwnedAdminShopCommoditySnapshot commodity)
        {
            if (commodity == null || commodity.ItemId <= 0)
            {
                return null;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(commodity.ItemId);
            AdminShopCategory category = ResolveCommodityCategory(inventoryType);
            string title = InventoryItemMetadataResolver.TryResolveItemName(commodity.ItemId, out string itemName)
                ? itemName
                : $"Item {commodity.ItemId.ToString(CultureInfo.InvariantCulture)}";
            string detail = InventoryItemMetadataResolver.TryResolveItemDescription(commodity.ItemId, out string description)
                ? description
                : "Packet-authored admin-shop sell template staged directly from CAdminShopDlg::SetAdminShopDlg.";
            long mesoReward = Math.Abs((long)commodity.Price);
            string packetSummary = $"SN {commodity.SerialNumber.ToString(CultureInfo.InvariantCulture)}, saleState {commodity.SaleState.ToString(CultureInfo.InvariantCulture)}, sellPrice {FormatPriceLabel(mesoReward)}, maxPerSlot {Math.Max(1, commodity.MaxPerSlot).ToString(CultureInfo.InvariantCulture)}.";
            detail = string.IsNullOrWhiteSpace(detail)
                ? packetSummary
                : $"{detail} {packetSummary}";

            bool available = commodity.SaleState == 0;
            return new AdminShopEntry
            {
                Title = title,
                Detail = $"{detail} Mirrors the client NPC sell template that CAdminShopDlg::SetUserItems binds to matching live inventory rows.",
                Seller = _packetOwnedAdminShopSession.NpcTemplateId > 0
                    ? $"NPC {_packetOwnedAdminShopSession.NpcTemplateId.ToString(CultureInfo.InvariantCulture)}"
                    : "Packet-owned shop",
                Price = -mesoReward,
                PriceLabel = $"Sell {FormatPriceLabel(mesoReward)}",
                Category = category,
                SupportsWishlist = false,
                State = available ? AdminShopEntryState.Available : AdminShopEntryState.PreviewOnly,
                StateLabel = available ? string.Empty : $"SaleState {commodity.SaleState.ToString(CultureInfo.InvariantCulture)}",
                RewardInventoryType = InventoryType.NONE,
                RewardItemId = 0,
                RewardQuantity = 1,
                Response = available ? AdminShopResponse.GrantItem : AdminShopResponse.None,
                MaxRequestCount = Math.Max(1, commodity.MaxPerSlot),
                SuccessMesoReward = mesoReward,
                SourceInventoryType = inventoryType,
                SourceItemId = commodity.ItemId,
                SourceItemQuantity = 1,
                PacketSerialNumber = commodity.SerialNumber,
                PacketSaleState = commodity.SaleState,
                IsPacketOwnedSnapshotRow = true
            };
        }

        private static long ComputeSuccessMesoReward(AdminShopEntry entry, int requestQuantity)
        {
            if (entry == null || entry.SuccessMesoReward <= 0)
            {
                return 0L;
            }

            return entry.SuccessMesoReward * Math.Max(1, requestQuantity);
        }

        private string DispatchPacketOwnedAdminShopOutbound(int mode, int npcTemplateId)
        {
            PacketOwnedNpcUtilityOutboundRequest request = BuildPacketOwnedAdminShopOutboundRequest(mode, npcTemplateId);
            return DispatchPacketOwnedAdminShopOutbound(request);
        }

        private string DispatchPacketOwnedAdminShopOutbound(PacketOwnedNpcUtilityOutboundRequest request)
        {
            if (DispatchPacketOwnedAdminShopOutboundRequest == null)
            {
                string retainedSummary = $"{request.Summary} No packet-owner bridge was available, so the simulator retained the outbound acknowledgement locally.";
                _packetOwnedAdminShopSession.RecordOutboundRequest(request.Opcode, request.Payload?.ToArray(), retainedSummary);
                return retainedSummary;
            }

            string dispatchSummary = DispatchPacketOwnedAdminShopOutboundRequest(request);
            string resolvedSummary = string.IsNullOrWhiteSpace(dispatchSummary)
                ? request.Summary
                : dispatchSummary;
            _packetOwnedAdminShopSession.RecordOutboundRequest(request.Opcode, request.Payload?.ToArray(), resolvedSummary);
            return resolvedSummary;
        }

        private bool TryDispatchPacketOwnedAdminShopTradeRequest(AdminShopEntry entry, int requestQuantity, out string summary)
        {
            summary = string.Empty;
            if (entry == null)
            {
                return false;
            }

            if (!TryBuildPacketOwnedAdminShopTradeRequest(entry, requestQuantity, ResolvePacketOwnedTradeRequestPosition(entry), out PacketOwnedNpcUtilityOutboundRequest request, out string error))
            {
                summary = error ?? string.Empty;
                return false;
            }

            summary = DispatchPacketOwnedAdminShopOutbound(request);
            return true;
        }

        private int ResolvePacketOwnedTradeRequestPosition(AdminShopEntry entry)
        {
            if (!RequiresInventorySource(entry))
            {
                return 0;
            }

            if (entry.InventorySlotIndex >= 0)
            {
                return entry.InventorySlotIndex + 1;
            }

            return TryResolveSourceInventoryStack(entry, out SourceInventoryStackResolution sourceResolution)
                ? sourceResolution.SlotIndex + 1
                : 0;
        }

        private string DispatchPacketOwnedAdminShopWishlistRegister(AdminShopEntry entry)
        {
            int itemId = ResolveWishlistRegisterItemId(entry);
            if (itemId <= 0)
            {
                return "CUIAdminShopWishList::SendRegisterPacket could not mirror opcode 74 mode 3 because the selected row has no item id.";
            }

            return DispatchPacketOwnedAdminShopOutbound(PacketOwnedAdminShopWishlistRegisterMode, itemId);
        }

        private static int ResolveWishlistRegisterItemId(AdminShopEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            if (entry.RewardItemId > 0)
            {
                return entry.RewardItemId;
            }

            return entry.DisplayItemId > 0 ? entry.DisplayItemId : 0;
        }

        internal static PacketOwnedNpcUtilityOutboundRequest BuildPacketOwnedAdminShopOutboundRequest(int mode, int value)
        {
            bool hasIntPayload = mode == PacketOwnedAdminShopResultMode
                || mode == PacketOwnedAdminShopWishlistRegisterMode;
            List<byte> payload = new(1 + (hasIntPayload ? sizeof(int) : 0))
            {
                (byte)mode
            };
            if (hasIntPayload)
            {
                payload.AddRange(BitConverter.GetBytes(value));
            }

            return new PacketOwnedNpcUtilityOutboundRequest(
                74,
                payload,
                BuildPacketOwnedAdminShopOutboundSummary(mode, value));
        }

        internal static bool TryBuildPacketOwnedAdminShopTradeRequest(
            int commoditySerialNumber,
            int requestCount,
            int position,
            out PacketOwnedNpcUtilityOutboundRequest request,
            out string error)
        {
            request = default;
            error = string.Empty;
            if (commoditySerialNumber <= 0)
            {
                error = "CAdminShopDlg::SendTradeRequest could not mirror opcode 74 mode 1 because the selected row has no commodity serial number.";
                return false;
            }

            int normalizedCount = Math.Clamp(requestCount, 1, ushort.MaxValue);
            int normalizedPosition = Math.Clamp(position, 0, ushort.MaxValue);
            byte[] payload =
            [
                (byte)PacketOwnedAdminShopTradeRequestMode,
                .. BitConverter.GetBytes(commoditySerialNumber),
                .. BitConverter.GetBytes((ushort)normalizedCount),
                .. BitConverter.GetBytes((ushort)normalizedPosition)
            ];
            request = new PacketOwnedNpcUtilityOutboundRequest(
                74,
                payload,
                $"Mirrored CAdminShopDlg::SendTradeRequest opcode 74 mode 1 for SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)} count {normalizedCount.ToString(CultureInfo.InvariantCulture)} pos {normalizedPosition.ToString(CultureInfo.InvariantCulture)}.");
            return true;
        }

        private static bool TryBuildPacketOwnedAdminShopTradeRequest(
            AdminShopEntry entry,
            int requestCount,
            int position,
            out PacketOwnedNpcUtilityOutboundRequest request,
            out string error)
        {
            request = default;
            error = string.Empty;
            if (entry == null)
            {
                error = "CAdminShopDlg::SendTradeRequest could not mirror opcode 74 mode 1 because no row is selected.";
                return false;
            }

            return TryBuildPacketOwnedAdminShopTradeRequest(
                entry.PacketSerialNumber,
                requestCount,
                position,
                out request,
                out error);
        }

        private static AdminShopCategory ResolveCommodityCategory(InventoryType inventoryType)
        {
            return inventoryType switch
            {
                InventoryType.EQUIP => AdminShopCategory.Equip,
                InventoryType.USE => AdminShopCategory.Use,
                InventoryType.SETUP => AdminShopCategory.Setup,
                InventoryType.ETC => AdminShopCategory.Etc,
                InventoryType.CASH => AdminShopCategory.Cash,
                _ => AdminShopCategory.All
            };
        }
    }
}
