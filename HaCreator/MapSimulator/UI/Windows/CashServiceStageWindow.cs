using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    public enum CashServiceStageKind
    {
        CashShop,
        ItemTradingCenter
    }

    public sealed class CashServiceStageWindow : UIWindowBase
    {
        public sealed class OneADayHistoryEntry
        {
            public int CommoditySerialNumber { get; init; }
            public int OriginalCommoditySerialNumber { get; init; }
            public int RawDate { get; init; }
        }

        internal sealed class OneADayPacketState
        {
            public int CurrentDate { get; init; }
            public int CurrentCommoditySerialNumber { get; init; }
            public IReadOnlyList<OneADayHistoryEntry> HistoryEntries { get; init; } = Array.Empty<OneADayHistoryEntry>();
        }

        public sealed class PacketCatalogEntry
        {
            public string Title { get; set; } = string.Empty;
            public string Detail { get; set; } = string.Empty;
            public string Seller { get; set; } = string.Empty;
            public string PriceLabel { get; set; } = string.Empty;
            public string StateLabel { get; set; } = string.Empty;
            public long SerialNumber { get; set; }
            public int ListingId { get; set; }
            public int ItemId { get; set; }
            public int Quantity { get; set; } = 1;
            public int Price { get; set; }
        }

        private sealed class CashItemInfoPacketSnapshot
        {
            public long SerialNumber { get; init; }
            public int AccountId { get; init; }
            public int CharacterId { get; init; }
            public int ItemId { get; init; }
            public int CommodityId { get; init; }
            public int Quantity { get; init; }
            public string BuyerCharacterId { get; init; } = string.Empty;
            public long RawExpireFileTime { get; init; }
            public int PaybackRate { get; init; }
            public int DiscountRate { get; init; }
        }

        private sealed class GiftListPacketSnapshot
        {
            public long SerialNumber { get; init; }
            public int ItemId { get; init; }
            public string Sender { get; init; } = string.Empty;
            public string Message { get; init; } = string.Empty;
        }

        private sealed class CashStatusSnapshot
        {
            public string StatusMessage { get; init; } = string.Empty;
            public IReadOnlyList<string> DetailLines { get; init; } = Array.Empty<string>();
        }

        private sealed class StageLayer
        {
            public StageLayer(IDXObject layer, Point offset)
            {
                Layer = layer;
                Offset = offset;
            }

            public IDXObject Layer { get; }
            public Point Offset { get; }
        }

        private sealed class StagePane
        {
            public StagePane(string name, Rectangle bounds, Func<CashServiceStageWindow, IReadOnlyList<string>> contentFactory)
            {
                Name = name;
                Bounds = bounds;
                ContentFactory = contentFactory;
            }

            public string Name { get; }
            public Rectangle Bounds { get; }
            public Func<CashServiceStageWindow, IReadOnlyList<string>> ContentFactory { get; }
        }

        private sealed class PacketRouteState
        {
            public PacketRouteState(int packetType, string label, string detail, int tickCount)
            {
                PacketType = packetType;
                Label = label ?? string.Empty;
                Detail = detail ?? string.Empty;
                TickCount = tickCount;
            }

            public int PacketType { get; }
            public string Label { get; }
            public string Detail { get; }
            public int TickCount { get; }
            public int HitCount { get; set; } = 1;
        }

        private readonly string _windowName;
        private readonly CashServiceStageKind _stageKind;
        private readonly Texture2D _pixelTexture;
        private readonly List<StageLayer> _layers = new();
        private readonly List<StagePane> _panes = new();
        private readonly Dictionary<string, UIObject> _buttons = new(StringComparer.Ordinal);
        private readonly Dictionary<int, Texture2D> _cashShopBackdropVariants = new();
        private readonly Dictionary<int, PacketRouteState> _packetRoutes = new();
        private readonly List<int> _packetRouteOrder = new();
        private readonly List<PacketCatalogEntry> _cashPacketCatalogEntries = new();
        private readonly List<PacketCatalogEntry> _cashInventoryPacketEntries = new();
        private readonly Dictionary<int, bool> _cashPurchaseRecordStates = new();
        private readonly List<OneADayHistoryEntry> _cashOneADayHistoryEntries = new();
        private readonly List<PacketCatalogEntry> _itcPacketCatalogEntries = new();
        private readonly List<PacketCatalogEntry> _itcSalePacketEntries = new();
        private readonly List<PacketCatalogEntry> _itcPurchasePacketEntries = new();
        private readonly List<PacketCatalogEntry> _itcWishPacketEntries = new();

        private SpriteFont _font;
        private CharacterBuild _build;
        private IInventoryRuntime _inventory;
        private IStorageRuntime _storageRuntime;
        private Texture2D _selectedBackdrop;
        private string _selectedBackdropLabel = "Default preview";
        private string _statusMessage = "Service stage idle.";
        private string _searchState = "No active search.";
        private string _navigationState = "Default category.";
        private string _noticeState = "No packet-authored notice.";
        private int _pendingCommoditySerialNumber;
        private int _lastOpenTick = int.MinValue;
        private int _wishlistCount;
        private int _chargeParam;
        private long _nexonCash;
        private long _maplePoint;
        private long _prepaidCash;
        private int _lastPacketType;
        private int _lastPacketTick = int.MinValue;
        private bool _hasPendingMigration;
        private int _cashItemResultSubtype = -1;
        private int _cashItemCommoditySerialNumber;
        private int _cashItemProductId;
        private int _cashItemPrice;
        private int _cashItemMutationCount;
        private int _cashLockerItemCount;
        private int _cashLockerSlotLimit;
        private int _cashCharacterSlotCount;
        private int _cashBuyCharacterCount;
        private int _cashCharacterCount;
        private int _cashOneADayItemDate;
        private int _cashOneADayItemSerialNumber;
        private readonly int[] _cashWishlistSerialNumbers = new int[10];
        private string _cashPacketPaneLabel = "Packet wishlist";
        private string _cashPacketBrowseModeLabel = "Wish";
        private string _cashItemLastSummary = "No cash-item result routed yet.";
        private string _cashGiftLastSummary = "No packet-authored gift result routed yet.";
        private string _cashPurchaseRecordSummary = "No packet-authored purchase record routed yet.";
        private string _cashCouponLastSummary = "No packet-authored coupon result routed yet.";
        private string _cashNameChangeLastSummary = "No packet-authored name-change result routed yet.";
        private string _cashTransferWorldLastSummary = "No packet-authored transfer-world result routed yet.";
        private string _cashGachaponLastSummary = "No packet-authored cash gachapon result routed yet.";
        private int _itcNormalItemSubtype = -1;
        private int _itcNormalItemPage;
        private int _itcNormalItemCategory;
        private int _itcNormalItemSubCategory;
        private int _itcNormalItemSortType = 1;
        private int _itcNormalItemSortColumn;
        private int _itcNormalItemEntryCount;
        private int _itcNormalItemPageEntryCount;
        private int _itcNormalItemSelectedListingId;
        private int _itcNormalItemSelectedPrice;
        private int _itcNormalItemMutationCount;
        private int _itcCurrentCategoryItemCount;
        private int _itcSaleItemCount;
        private int _itcPurchaseItemCount;
        private string _itcNormalItemLastSummary = "No ITC normal-item packet routed yet.";

        public CashServiceStageWindow(IDXObject frame, string windowName, CashServiceStageKind stageKind, GraphicsDevice device)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _stageKind = stageKind;
            _pixelTexture = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
            SupportsDragging = false;
            InitializePanes();
        }

        public override string WindowName => _windowName;
        public string StatusMessage => _statusMessage;
        public string SearchState => _searchState;
        public string NavigationState => _navigationState;
        public string NoticeState => _noticeState;
        public int PendingCommoditySerialNumber => _pendingCommoditySerialNumber;
        public int WishlistCount => _wishlistCount;
        public long NexonCashBalance => _nexonCash;
        public long MaplePointBalance => _maplePoint;
        public long PrepaidCashBalance => _prepaidCash;
        public int ChargeParam => _chargeParam;
        public bool HasPendingCommodityMigration => _hasPendingMigration;
        public bool IsOneADayPending => _packetRoutes.ContainsKey(395);
        public int CashOneADayItemDate => _cashOneADayItemDate;
        public int CashOneADayItemSerialNumber => _cashOneADayItemSerialNumber;
        public IReadOnlyList<OneADayHistoryEntry> CashOneADayHistoryEntries => _cashOneADayHistoryEntries;
        public int CashItemMutationCount => _cashItemMutationCount;
        public int CashLockerItemCount => _cashLockerItemCount;
        public int CashLockerSlotLimit => _cashLockerSlotLimit;
        public int CashCharacterSlotCount => _cashCharacterSlotCount;
        public int CashBuyCharacterCount => _cashBuyCharacterCount;
        public int CashCharacterCount => _cashCharacterCount;
        public IReadOnlyList<int> CashWishlistSerialNumbers => _cashWishlistSerialNumbers;
        public string CashPacketPaneLabel => _cashPacketPaneLabel;
        public string CashPacketBrowseModeLabel => _cashPacketBrowseModeLabel;
        public string CashItemLastSummary => _cashItemLastSummary;
        public string CashGiftLastSummary => _cashGiftLastSummary;
        public string CashPurchaseRecordSummary => _cashPurchaseRecordSummary;
        public string CashCouponLastSummary => _cashCouponLastSummary;
        public string CashNameChangeLastSummary => _cashNameChangeLastSummary;
        public string CashTransferWorldLastSummary => _cashTransferWorldLastSummary;
        public string CashGachaponLastSummary => _cashGachaponLastSummary;
        public IReadOnlyList<PacketCatalogEntry> CashInventoryPacketEntries => _cashInventoryPacketEntries;
        public int ItcNormalItemMutationCount => _itcNormalItemMutationCount;
        public int ItcNormalItemSubtype => _itcNormalItemSubtype;
        public int ItcNormalItemPage => _itcNormalItemPage;
        public int ItcNormalItemCategory => _itcNormalItemCategory;
        public int ItcNormalItemSubCategory => _itcNormalItemSubCategory;
        public int ItcNormalItemSortType => _itcNormalItemSortType;
        public int ItcNormalItemSortColumn => _itcNormalItemSortColumn;
        public int ItcCurrentCategoryItemCount => _itcCurrentCategoryItemCount;
        public int ItcNormalItemEntryCount => _itcNormalItemEntryCount;
        public int ItcNormalItemPageEntryCount => _itcNormalItemPageEntryCount;
        public int ItcNormalItemSelectedListingId => _itcNormalItemSelectedListingId;
        public int ItcNormalItemSelectedPrice => _itcNormalItemSelectedPrice;
        public int ItcSaleItemCount => _itcSaleItemCount;
        public int ItcPurchaseItemCount => _itcPurchaseItemCount;
        public string ItcNormalItemLastSummary => _itcNormalItemLastSummary;
        public IReadOnlyList<PacketCatalogEntry> CashPacketCatalogEntries => _cashPacketCatalogEntries;
        public IReadOnlyList<PacketCatalogEntry> ItcPacketCatalogEntries => _itcPacketCatalogEntries;
        public IReadOnlyList<PacketCatalogEntry> ItcSalePacketEntries => _itcSalePacketEntries;
        public IReadOnlyList<PacketCatalogEntry> ItcPurchasePacketEntries => _itcPurchasePacketEntries;
        public IReadOnlyList<PacketCatalogEntry> ItcWishPacketEntries => _itcWishPacketEntries;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public IReadOnlyList<string> GetRecentPacketSummaries(int maxCount = 4)
        {
            if (_packetRouteOrder.Count == 0 || maxCount <= 0)
            {
                return Array.Empty<string>();
            }

            List<string> lines = new();
            foreach (int packetType in _packetRouteOrder.TakeLast(maxCount))
            {
                PacketRouteState route = _packetRoutes[packetType];
                lines.Add($"{route.Label} x{route.HitCount}: {route.Detail}");
            }

            return lines;
        }

        public void AddLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _layers.Add(new StageLayer(layer, offset));
            }
        }

        public void AddBackdropVariant(int index, Texture2D texture)
        {
            if (texture != null)
            {
                _cashShopBackdropVariants[index] = texture;
                _selectedBackdrop ??= texture;
            }
        }

        public void RegisterButton(string key, UIObject button, Action action)
        {
            if (button == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            AddButton(button);
            _buttons[key] = button;
            if (action != null)
            {
                button.ButtonClickReleased += _ => action();
            }
        }

        public void SetCharacterBuild(CharacterBuild build)
        {
            _build = build;
            if (_stageKind == CashServiceStageKind.CashShop)
            {
                SelectCashShopBackdrop();
            }
        }

        public void SetInventory(IInventoryRuntime inventory)
        {
            _inventory = inventory;
        }

        public void SetStorageRuntime(IStorageRuntime storageRuntime)
        {
            _storageRuntime = storageRuntime;
        }

        public void BeginStageSession(CharacterBuild build, long mesoBalance, int tickCount, int pendingCommoditySerialNumber = 0)
        {
            _build = build;
            _chargeParam = 0;
            _wishlistCount = 0;
            _nexonCash = 0;
            _maplePoint = 0;
            _prepaidCash = 0;
            _noticeState = "No packet-authored notice.";
            _packetRoutes.Clear();
            _packetRouteOrder.Clear();
            _lastPacketType = 0;
            _lastPacketTick = int.MinValue;
            _lastOpenTick = tickCount;
            _cashItemResultSubtype = -1;
            _cashItemCommoditySerialNumber = 0;
            _cashItemProductId = 0;
            _cashItemPrice = 0;
            _cashItemMutationCount = 0;
            _cashItemLastSummary = "No cash-item result routed yet.";
            _cashPacketCatalogEntries.Clear();
            _cashInventoryPacketEntries.Clear();
            _cashPurchaseRecordStates.Clear();
            _cashPacketPaneLabel = "Packet wishlist";
            _cashPacketBrowseModeLabel = "Wish";
            _cashGiftLastSummary = "No packet-authored gift result routed yet.";
            _cashPurchaseRecordSummary = "No packet-authored purchase record routed yet.";
            _cashCouponLastSummary = "No packet-authored coupon result routed yet.";
            _cashNameChangeLastSummary = "No packet-authored name-change result routed yet.";
            _cashTransferWorldLastSummary = "No packet-authored transfer-world result routed yet.";
            _cashGachaponLastSummary = "No packet-authored cash gachapon result routed yet.";
            _cashOneADayItemDate = 0;
            _cashOneADayItemSerialNumber = 0;
            _cashOneADayHistoryEntries.Clear();
            _itcNormalItemSubtype = -1;
            _itcNormalItemPage = 0;
            _itcNormalItemCategory = 0;
            _itcNormalItemSubCategory = 0;
            _itcNormalItemSortType = 1;
            _itcNormalItemSortColumn = 0;
            _itcCurrentCategoryItemCount = 0;
            _itcNormalItemEntryCount = 0;
            _itcNormalItemPageEntryCount = 0;
            _itcNormalItemSelectedListingId = 0;
            _itcNormalItemSelectedPrice = 0;
            _itcNormalItemMutationCount = 0;
            _itcSaleItemCount = 0;
            _itcPurchaseItemCount = 0;
            _itcPacketCatalogEntries.Clear();
            _itcSalePacketEntries.Clear();
            _itcPurchasePacketEntries.Clear();
            _itcWishPacketEntries.Clear();
            _itcNormalItemLastSummary = "No ITC normal-item packet routed yet.";

            if (_stageKind == CashServiceStageKind.CashShop)
            {
                SelectCashShopBackdrop();
                _searchState = "Item-search owner idle.";
                _navigationState = "Category 1 / page 0 / subcategory 0 owned by CCashShop.";
                _statusMessage = "CCashShop::Init parity active: field UI cleared, wishlist and cash mirrors reset, preview art selected, and character/locker/inventory/tab/list/best/status/item-search owners created.";
                PrepareCommodityMigration(pendingCommoditySerialNumber, tickCount);
                if (!_hasPendingMigration)
                {
                    _navigationState = "Category 1 / page 0 / subcategory 0 owned by CCashShop.";
                }
            }
            else
            {
                _pendingCommoditySerialNumber = 0;
                _hasPendingMigration = false;
                _searchState = "Search disabled; search condition cleared.";
                _navigationState = "Category 1 / page 0 owned by CITC.";
                _statusMessage = "CITC::Init parity active: field UI cleared, category/search/sort state reset, NPT exception items loaded, and character/sale/purchase/inventory/tab/subtab/list/status owners created.";
                _noticeState = $"NPT exception items loaded with {mesoBalance.ToString("N0", CultureInfo.InvariantCulture)} mesos still tracked on the simulator side.";
            }
        }

        public void PrepareStageOpen(int tickCount)
        {
            _lastOpenTick = tickCount;
            if (_stageKind == CashServiceStageKind.CashShop)
            {
                SelectCashShopBackdrop();
            }
        }

        public void PrepareCommodityMigration(int commoditySerialNumber, int tickCount)
        {
            _pendingCommoditySerialNumber = Math.Max(0, commoditySerialNumber);
            _hasPendingMigration = _pendingCommoditySerialNumber > 0;
            _lastOpenTick = tickCount;
            _navigationState = _pendingCommoditySerialNumber > 0
                ? $"Pending CCSWnd_Best::GoToCommoditySN migration for SN {_pendingCommoditySerialNumber}."
                : "Commodity migration cleared.";
        }

        public bool TryGetPendingCommoditySerialNumber(out int pendingCommoditySerialNumber)
        {
            pendingCommoditySerialNumber = _pendingCommoditySerialNumber;
            return pendingCommoditySerialNumber > 0;
        }

        public bool TryFocusCommoditySerialNumber(int commoditySerialNumber)
        {
            if (_stageKind != CashServiceStageKind.CashShop || commoditySerialNumber <= 0)
            {
                return false;
            }

            PrepareCommodityMigration(commoditySerialNumber, Environment.TickCount);
            _statusMessage = $"CCashShop::Init resumed the staged catalog at commodity SN {_pendingCommoditySerialNumber}.";
            return true;
        }

        public void SetStatusMessage(string statusMessage)
        {
            _statusMessage = string.IsNullOrWhiteSpace(statusMessage) ? "Service stage idle." : statusMessage.Trim();
        }

        public bool TryApplyPacket(int packetType, byte[] payload, int tickCount, out string message)
        {
            message = _stageKind switch
            {
                CashServiceStageKind.CashShop => ApplyCashShopPacket(packetType, payload, tickCount),
                CashServiceStageKind.ItemTradingCenter => ApplyItcPacket(packetType, payload, tickCount),
                _ => $"Unsupported service stage kind {_stageKind}."
            };

            return !string.IsNullOrWhiteSpace(message);
        }

        public IReadOnlyList<string> DescribeCharacterOwnerState() => BuildCharacterPane();
        public IReadOnlyList<string> DescribeLockerOwnerState() => BuildLockerPane();
        public IReadOnlyList<string> DescribeInventoryOwnerState() => BuildInventoryPane();
        public IReadOnlyList<string> DescribeTabOwnerState() => BuildTabPane();
        public IReadOnlyList<string> DescribeSubTabOwnerState() => BuildSubTabPane();
        public IReadOnlyList<string> DescribeListOwnerState() => BuildListPane();
        public IReadOnlyList<string> DescribeBestOwnerState() => BuildBestPane();
        public IReadOnlyList<string> DescribeStatusOwnerState() => BuildStatusPane();

        public IReadOnlyList<string> GetStatusOwnerDetailLines()
        {
            return BuildStatusSnapshot().DetailLines;
        }
        public IReadOnlyList<string> DescribeSearchOwnerState() => BuildSearchPane();
        public IReadOnlyList<string> DescribeSaleOwnerState() => BuildSalePane();
        public IReadOnlyList<string> DescribePurchaseOwnerState() => BuildPurchasePane();

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
            if (_selectedBackdrop != null)
            {
                sprite.Draw(_selectedBackdrop, new Vector2(Position.X, Position.Y), Color.White);
            }

            foreach (StageLayer layer in _layers)
            {
                layer.Layer.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    Position.X + layer.Offset.X,
                    Position.Y + layer.Offset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }

            if (_font == null)
            {
                return;
            }

            DrawHeader(sprite);
            DrawPaneChrome(sprite);
            DrawPaneContent(sprite);
            DrawFooter(sprite);
        }

        private void InitializePanes()
        {
            if (_stageKind == CashServiceStageKind.CashShop)
            {
                _panes.Add(new StagePane("Character", new Rectangle(0, 0, 256, 316), window => window.BuildCharacterPane()));
                _panes.Add(new StagePane("Locker", new Rectangle(0, 318, 256, 104), window => window.BuildLockerPane()));
                _panes.Add(new StagePane("Inventory", new Rectangle(0, 426, 246, 163), window => window.BuildInventoryPane()));
                _panes.Add(new StagePane("Tabs", new Rectangle(272, 17, 508, 78), window => window.BuildTabPane()));
                _panes.Add(new StagePane("Catalog List", new Rectangle(275, 95, 412, 430), window => window.BuildListPane()));
                _panes.Add(new StagePane("Best Items", new Rectangle(690, 157, 90, 358), window => window.BuildBestPane()));
                _panes.Add(new StagePane("Status", new Rectangle(254, 530, 545, 56), window => window.BuildStatusPane()));
                _panes.Add(new StagePane("Item Search", new Rectangle(690, 97, 89, 22), window => window.BuildSearchPane()));
            }
            else
            {
                _panes.Add(new StagePane("Character", new Rectangle(0, 0, 256, 200), window => window.BuildCharacterPane()));
                _panes.Add(new StagePane("Sale", new Rectangle(0, 200, 256, 110), window => window.BuildSalePane()));
                _panes.Add(new StagePane("Purchase", new Rectangle(0, 310, 256, 108), window => window.BuildPurchasePane()));
                _panes.Add(new StagePane("Inventory", new Rectangle(0, 418, 256, 180), window => window.BuildInventoryPane()));
                _panes.Add(new StagePane("Tab", new Rectangle(272, 17, 509, 78), window => window.BuildTabPane()));
                _panes.Add(new StagePane("Subtab", new Rectangle(273, 98, 509, 48), window => window.BuildSubTabPane()));
                _panes.Add(new StagePane("List", new Rectangle(273, 145, 509, 365), window => window.BuildListPane()));
                _panes.Add(new StagePane("Status", new Rectangle(255, 531, 545, 56), window => window.BuildStatusPane()));
            }
        }

        private void DrawHeader(SpriteBatch sprite)
        {
            Vector2 titleOrigin = new(Position.X + 18, Position.Y + 18);
            Color accent = _stageKind == CashServiceStageKind.CashShop ? new Color(255, 240, 176) : new Color(196, 232, 255);
            sprite.DrawString(_font, _stageKind == CashServiceStageKind.CashShop ? "Cash Shop Stage" : "ITC Stage", titleOrigin, accent);

            string subtitle = _stageKind == CashServiceStageKind.CashShop
                ? $"Dedicated service owner with job preview {_selectedBackdropLabel.ToLowerInvariant()}."
                : "Dedicated Item Trading Center owner with separate sale, purchase, and list panes.";
            sprite.DrawString(_font, subtitle, new Vector2(titleOrigin.X, titleOrigin.Y + _font.LineSpacing), new Color(232, 232, 232));
        }

        private void DrawPaneChrome(SpriteBatch sprite)
        {
            foreach (StagePane pane in _panes)
            {
                Rectangle bounds = OffsetBounds(pane.Bounds);
                DrawRect(sprite, bounds, new Color(255, 255, 255, 36));
                DrawOutline(sprite, bounds, _stageKind == CashServiceStageKind.CashShop ? new Color(255, 212, 122) : new Color(123, 190, 255));
                sprite.DrawString(_font, pane.Name, new Vector2(bounds.X + 6, bounds.Y + 4), Color.White);
            }
        }

        private void DrawPaneContent(SpriteBatch sprite)
        {
            foreach (StagePane pane in _panes)
            {
                Rectangle bounds = OffsetBounds(pane.Bounds);
                float y = bounds.Y + 24;
                float maxWidth = Math.Max(60f, bounds.Width - 12f);
                IReadOnlyList<string> lines = pane.ContentFactory?.Invoke(this) ?? Array.Empty<string>();
                for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
                {
                    foreach (string wrapped in WrapText(lines[lineIndex], maxWidth))
                    {
                        if (y > bounds.Bottom - (_font.LineSpacing + 4))
                        {
                            return;
                        }

                        sprite.DrawString(_font, wrapped, new Vector2(bounds.X + 6, y), new Color(234, 234, 234));
                        y += _font.LineSpacing;
                    }
                }
            }
        }

        private void DrawFooter(SpriteBatch sprite)
        {
            string footer = _lastPacketTick == int.MinValue
                ? "Packet dispatch idle."
                : $"Last packet {_lastPacketType} routed {Math.Max(0, unchecked(Environment.TickCount - _lastPacketTick))} ms ago.";

            sprite.DrawString(
                _font,
                footer,
                new Vector2(Position.X + 18, Position.Y + Math.Max(564, (CurrentFrame?.Height ?? 600) - _font.LineSpacing - 10)),
                new Color(255, 244, 194));
        }

        private IReadOnlyList<string> BuildCharacterPane()
        {
            if (_build == null)
            {
                return new[]
                {
                    "No active character build.",
                    "Preview art stays on the default stage background."
                };
            }

            return new[]
            {
                $"{_build.Name} Lv.{_build.Level} {_build.JobName}",
                $"Job {_build.Job} / subjob {_build.SubJob}.",
                _stageKind == CashServiceStageKind.CashShop
                    ? $"Preview owner is using {_selectedBackdropLabel.ToLowerInvariant()}."
                    : "Character pane mirrors the standalone ITC owner."
            };
        }

        private IReadOnlyList<string> BuildLockerPane()
        {
            if (_stageKind == CashServiceStageKind.CashShop && _cashLockerSlotLimit > 0)
            {
                return new[]
                {
                    $"Cash locker items {_cashLockerItemCount.ToString(CultureInfo.InvariantCulture)}/{_cashLockerSlotLimit.ToString(CultureInfo.InvariantCulture)}.",
                    $"Character slots {_cashCharacterSlotCount.ToString(CultureInfo.InvariantCulture)}, buy-count {_cashBuyCharacterCount.ToString(CultureInfo.InvariantCulture)}, characters {_cashCharacterCount.ToString(CultureInfo.InvariantCulture)}.",
                    _storageRuntime != null
                        ? $"Shared-account trunk mirror still tracks {_storageRuntime.GetUsedSlotCount().ToString(CultureInfo.InvariantCulture)}/{_storageRuntime.GetSlotLimit().ToString(CultureInfo.InvariantCulture)} outside the cash locker."
                        : "Cash locker data is coming from packet-owned CCashShop state."
                };
            }

            if (_storageRuntime == null)
            {
                return new[] { "Locker runtime unavailable.", "Cash locker pane remains staged but empty." };
            }

            return new[]
            {
                $"Account: {_storageRuntime.AccountLabel}",
                $"Shared slots {_storageRuntime.GetUsedSlotCount()}/{_storageRuntime.GetSlotLimit()}.",
                _storageRuntime.SharedCharacterNames.Count > 0
                    ? $"Shared with {string.Join(", ", _storageRuntime.SharedCharacterNames.Take(3))}."
                    : "No shared-character list is loaded."
            };
        }

        private IReadOnlyList<string> BuildInventoryPane()
        {
            if (_inventory == null)
            {
                return new[] { "Inventory runtime unavailable." };
            }

            return new[]
            {
                $"Equip {_inventory.GetSlots(InventoryType.EQUIP).Count}, Use {_inventory.GetSlots(InventoryType.USE).Count}, Setup {_inventory.GetSlots(InventoryType.SETUP).Count}.",
                $"Etc {_inventory.GetSlots(InventoryType.ETC).Count}, Cash {_inventory.GetSlots(InventoryType.CASH).Count}.",
                $"Meso {_inventory.GetMesoCount().ToString("N0", CultureInfo.InvariantCulture)}.",
                _cashInventoryPacketEntries.Count > 0
                    ? $"{_cashInventoryPacketEntries[0].Detail}"
                    : _cashGiftLastSummary
            };
        }

        private IReadOnlyList<string> BuildTabPane()
        {
            return new[]
            {
                _navigationState,
                _stageKind == CashServiceStageKind.CashShop
                    ? "Client layout owns tab, list, best-items, and search panes separately."
                    : "Client layout owns top tab and list subtab separately."
            };
        }

        private IReadOnlyList<string> BuildSubTabPane()
        {
            return new[]
            {
                _searchState,
                _stageKind == CashServiceStageKind.CashShop
                    ? "Search mode remains owned by CCashShop."
                    : "Sort column 0 / sort type 1 remain owned by CITC."
            };
        }

        private IReadOnlyList<string> BuildListPane()
        {
            List<string> lines = new()
            {
                _stageKind == CashServiceStageKind.CashShop
                    ? (_hasPendingMigration
                        ? $"Pending commodity SN {_pendingCommoditySerialNumber} is queued for list focus."
                        : "No pending commodity migration.")
                    : "Normal-item result routing is staged here."
            };
            if (_stageKind == CashServiceStageKind.CashShop)
            {
                lines.Add(_cashItemLastSummary);
            }
            else
            {
                lines.Add(_itcNormalItemLastSummary);
                if (_itcPacketCatalogEntries.Count > 0)
                {
                    foreach (PacketCatalogEntry entry in _itcPacketCatalogEntries.Take(3))
                    {
                        lines.Add($"{entry.Title} | {entry.PriceLabel} | {entry.StateLabel}");
                    }
                }
                else if (_itcWishPacketEntries.Count > 0)
                {
                    foreach (PacketCatalogEntry entry in _itcWishPacketEntries.Take(3))
                    {
                        lines.Add($"{entry.Title} | {entry.PriceLabel} | {entry.StateLabel}");
                    }
                }
            }

            if (_packetRouteOrder.Count == 0)
            {
                lines.Add("No stage packet has been routed yet.");
                return lines;
            }

            foreach (int packetType in _packetRouteOrder.TakeLast(4))
            {
                PacketRouteState route = _packetRoutes[packetType];
                lines.Add($"{route.Label} x{route.HitCount}: {route.Detail}");
            }

            return lines;
        }

        private IReadOnlyList<string> BuildBestPane()
        {
            string wishlistLine = _cashPacketCatalogEntries.Count > 0
                ? $"Wishlist count {_wishlistCount}/10 with {_cashPacketCatalogEntries.Count.ToString(CultureInfo.InvariantCulture)} packet-owned row(s)."
                : $"Wishlist count {_wishlistCount}/10.";
            return new[]
            {
                _pendingCommoditySerialNumber > 0
                    ? $"GoToCommoditySN {_pendingCommoditySerialNumber} is waiting on the best-items owner."
                    : "No commodity serial is waiting on best-items.",
                wishlistLine,
                _cashPurchaseRecordSummary
            };
        }

        private IReadOnlyList<string> BuildStatusPane()
        {
            CashStatusSnapshot snapshot = BuildStatusSnapshot();
            string balanceLine = $"NX {_nexonCash.ToString("N0", CultureInfo.InvariantCulture)}  MP {_maplePoint.ToString("N0", CultureInfo.InvariantCulture)}  Prepaid {_prepaidCash.ToString("N0", CultureInfo.InvariantCulture)}";
            if (_chargeParam != 0)
            {
                balanceLine += $"  Charge {_chargeParam.ToString(CultureInfo.InvariantCulture)}";
            }

            List<string> lines = new()
            {
                balanceLine,
                snapshot.StatusMessage
            };
            lines.AddRange(snapshot.DetailLines);
            return lines;
        }

        private CashStatusSnapshot BuildStatusSnapshot()
        {
            List<string> detailLines = new();
            AppendStatusDetail(detailLines, _noticeState);
            if (_stageKind == CashServiceStageKind.CashShop)
            {
                AppendStatusDetail(detailLines, _cashCouponLastSummary, suppressDefaultPrefix: "No packet-authored");
                AppendStatusDetail(detailLines, _cashNameChangeLastSummary, suppressDefaultPrefix: "No packet-authored");
                AppendStatusDetail(detailLines, _cashTransferWorldLastSummary, suppressDefaultPrefix: "No packet-authored");
                AppendStatusDetail(detailLines, _cashGachaponLastSummary, suppressDefaultPrefix: "No packet-authored");
                AppendStatusDetail(detailLines, _cashPurchaseRecordSummary, suppressDefaultPrefix: "No packet-authored");
                AppendStatusDetail(detailLines, _cashGiftLastSummary, suppressDefaultPrefix: "No packet-authored");
            }
            else
            {
                AppendStatusDetail(detailLines, _itcNormalItemLastSummary, suppressDefaultPrefix: "No ITC normal-item packet");
            }

            return new CashStatusSnapshot
            {
                StatusMessage = _statusMessage,
                DetailLines = detailLines
            };
        }

        private IReadOnlyList<string> BuildSearchPane()
        {
            return new[]
            {
                _searchState
            };
        }

        private IReadOnlyList<string> BuildSalePane()
        {
            List<string> lines = new()
            {
                $"Sale owner is split from purchase owner. Packet list count {_itcSaleItemCount.ToString(CultureInfo.InvariantCulture)}.",
                _itcNormalItemLastSummary,
                _noticeState
            };

            foreach (PacketCatalogEntry entry in _itcSalePacketEntries.Take(2))
            {
                lines.Add($"{entry.Title} | {entry.PriceLabel} | {entry.StateLabel}");
            }

            if (_itcWishPacketEntries.Count > 0)
            {
                lines.Add($"Wish rows {_itcWishPacketEntries.Count.ToString(CultureInfo.InvariantCulture)} remain staged separately.");
            }

            return lines;
        }

        private IReadOnlyList<string> BuildPurchasePane()
        {
            List<string> lines = new()
            {
                $"Purchase owner remains separate from the main list. Packet list count {_itcPurchaseItemCount.ToString(CultureInfo.InvariantCulture)}.",
                _itcPurchasePacketEntries.Count > 0
                    ? $"{_itcPurchasePacketEntries[0].Title} at {_itcPurchasePacketEntries[0].PriceLabel}."
                    : (_itcWishPacketEntries.Count > 0
                        ? $"{_itcWishPacketEntries[0].Title} at {_itcWishPacketEntries[0].PriceLabel} remains in the wish-sale owner."
                    : (_itcNormalItemMutationCount > 0
                        ? $"Last listing {_itcNormalItemSelectedListingId.ToString(CultureInfo.InvariantCulture)} at {_itcNormalItemSelectedPrice.ToString("N0", CultureInfo.InvariantCulture)} mesos."
                        : "No listing payload has reached the purchase owner yet.")),
                _statusMessage
            };

            foreach (PacketCatalogEntry entry in _itcPurchasePacketEntries.Take(2))
            {
                lines.Add($"{entry.Title} | {entry.PriceLabel} | {entry.StateLabel}");
            }

            return lines;
        }

        private string ApplyCashShopPacket(int packetType, byte[] payload, int tickCount)
        {
            string detail;
            switch (packetType)
            {
                case 382:
                    _chargeParam = TryReadInt32(payload, out int chargeParam) ? chargeParam : 0;
                    detail = _chargeParam > 0
                        ? $"Charge parameter result reached CCashShop with charge param {_chargeParam.ToString(CultureInfo.InvariantCulture)}."
                        : "Charge parameter result reached the Cash Shop stage owner.";
                    break;
                case 383:
                    TryReadCashBalances(payload, out _nexonCash, out _maplePoint, out _prepaidCash);
                    detail = $"Cash balances refreshed to NX {_nexonCash:N0}, MP {_maplePoint:N0}, Prepaid {_prepaidCash:N0}.";
                    break;
                case 384:
                    ApplyCashItemResultPacket(payload);
                    _hasPendingMigration = false;
                    _navigationState = "CCSWnd_List and CCSWnd_Best own the active commodity view.";
                    detail = _cashItemLastSummary;
                    break;
                case 385:
                    detail = BuildCashPurchaseExpResult(payload);
                    break;
                case 386:
                    detail = BuildCashGiftMateResult(payload);
                    break;
                case 387:
                    detail = BuildCashDuplicateIdResult(payload);
                    break;
                case 388:
                    detail = BuildCashNameChangePacketResult(payload);
                    break;
                case 390:
                    detail = BuildCashTransferWorldPacketResult(payload);
                    break;
                case 391:
                    detail = BuildCashGachaponStampResult(payload);
                    break;
                case 392:
                case 393:
                    detail = BuildCashGachaponPacketResult(payload, packetType);
                    break;
                case 395:
                    detail = ApplyCashOneADayPacket(payload);
                    break;
                case 396:
                    _noticeState = TryReadUtf8Text(payload, out string freeItemNotice) ? freeItemNotice : "Free-item notice packet received.";
                    detail = _noticeState;
                    break;
                default:
                    detail = $"Unsupported Cash Shop packet {packetType}.";
                    break;
            }

            _statusMessage = detail;
            RecordPacketRoute(packetType, GetCashShopPacketLabel(packetType), detail, tickCount);
            return detail;
        }

        private string ApplyItcPacket(int packetType, byte[] payload, int tickCount)
        {
            string detail = packetType switch
            {
                410 => ApplyItcChargeParam(payload),
                411 => BuildItcBalanceMessage(payload),
                412 => ApplyItcNormalItemResult(payload),
                _ => $"Unsupported ITC packet {packetType}."
            };

            _statusMessage = detail;
            RecordPacketRoute(packetType, GetItcPacketLabel(packetType), detail, tickCount);
            return detail;
        }

        private string BuildItcBalanceMessage(byte[] payload)
        {
            TryReadCashBalances(payload, out _nexonCash, out _maplePoint, out long _);
            return $"ITC balance query refreshed NX {_nexonCash:N0} and MP {_maplePoint:N0}.";
        }

        private string ApplyItcChargeParam(byte[] payload)
        {
            _chargeParam = TryReadInt32(payload, out int chargeParam) ? chargeParam : 0;
            return _chargeParam > 0
                ? $"ITC charge parameter result reached CITC with charge param {_chargeParam.ToString(CultureInfo.InvariantCulture)}."
                : "ITC charge parameter result reached the dedicated stage owner.";
        }

        private string ApplyCashItemResultPacket(byte[] payload)
        {
            byte[] packetPayload = payload ?? Array.Empty<byte>();
            _cashItemResultSubtype = packetPayload.Length > 0 ? packetPayload[0] : -1;
            _cashItemMutationCount++;
            string subtypeLabel = GetCashItemResultSubtypeLabel(_cashItemResultSubtype);
            string summary = _cashItemResultSubtype switch
            {
                88 => TryApplyCashLoadLockerDone(packetPayload, out string lockerMessage)
                    ? lockerMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResLoadLockerDone", packetPayload),
                90 => TryApplyCashLoadGiftDone(packetPayload, out string giftMessage)
                    ? giftMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResLoadGiftDone", packetPayload),
                92 => TryApplyCashWishlistPacket(packetPayload, "CCashShop::OnCashItemResLoadWishDone", out string loadWishMessage)
                    ? loadWishMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResLoadWishDone", packetPayload),
                98 => TryApplyCashWishlistPacket(packetPayload, "CCashShop::OnCashItemResSetWishDone", out string setWishMessage)
                    ? setWishMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResSetWishDone", packetPayload),
                100 => TryApplyCashBuyDone(packetPayload, out string buyDoneMessage)
                    ? buyDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResBuyDone", packetPayload),
                101 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResBuyFailed"),
                102 => TryApplyCashCouponDone(packetPayload, isGiftCoupon: false, out string couponDoneMessage)
                    ? couponDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResUseCouponDone", packetPayload),
                104 => TryApplyCashCouponDone(packetPayload, isGiftCoupon: true, out string giftCouponDoneMessage)
                    ? giftCouponDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResGiftCouponDone", packetPayload),
                105 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResUseCouponFailed"),
                107 => TryApplyCashGiftDone(packetPayload, out string giftDoneMessage)
                    ? giftDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResGiftDone", packetPayload),
                -102 => TryApplyCashBuyPackageDone(packetPayload, out string buyPackageDoneMessage)
                    ? buyPackageDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResBuyPackageDone", packetPayload),
                -100 => TryApplyCashGiftPackageDone(packetPayload, out string giftPackageDoneMessage)
                    ? giftPackageDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResGiftPackageDone", packetPayload),
                -98 => TryApplyCashBuyNormalDone(packetPayload, out string buyNormalDoneMessage)
                    ? buyNormalDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResBuyNormalDone", packetPayload),
                -81 => TryApplyCashPurchaseRecord(packetPayload, out string purchaseRecordMessage)
                    ? purchaseRecordMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResPurchaseRecord", packetPayload),
                -77 => TryApplyCashNameChangeDone(packetPayload, out string nameChangeDoneMessage)
                    ? nameChangeDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemNameChangeResBuyDone", packetPayload),
                -75 => TryApplyCashTransferWorldDone(packetPayload, out string transferWorldDoneMessage)
                    ? transferWorldDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResTransferWorldDone", packetPayload),
                -74 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResTransferWorldFailed"),
                -73 => TryApplyCashGachaponDone(packetPayload, isCopyResult: false, out string gachaponOpenDoneMessage)
                    ? gachaponOpenDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResCashGachaponOpenDone", packetPayload),
                -72 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResCashGachaponOpenFailed"),
                -71 => TryApplyCashGachaponDone(packetPayload, isCopyResult: true, out string gachaponCopyDoneMessage)
                    ? gachaponCopyDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResCashGachaponCopyDone", packetPayload),
                -70 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResCashGachaponCopyFailed"),
                -69 => TryApplyCashMaplePointChangeDone(packetPayload, out string maplePointChangeDoneMessage)
                    ? maplePointChangeDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResChangeMaplePointDone", packetPayload),
                -68 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResChangeMaplePointFailed"),
                -106 => TryApplyCashRebateDone(packetPayload, out string rebateDoneMessage)
                    ? rebateDoneMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResRebateDone", packetPayload),
                109 => BuildCashSimpleResult("CCashShop::OnCashItemResIncSlotCountDone", packetPayload),
                110 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResIncSlotCountFailed"),
                111 => TryApplyCashCounterUpdate(packetPayload, "CCashShop::OnCashItemResIncTrunkCountDone", 48, value => _cashLockerSlotLimit = value, out string trunkMessage)
                    ? trunkMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResIncTrunkCountDone", packetPayload),
                112 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResIncTrunkCountFailed"),
                113 => TryApplyCashCounterUpdate(packetPayload, "CCashShop::OnCashItemResIncCharacterSlotCountDone", 15, value => _cashCharacterSlotCount = value, out string characterSlotMessage)
                    ? characterSlotMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResIncCharacterSlotCountDone", packetPayload),
                114 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResIncCharacterSlotCountFailed"),
                115 => TryApplyCashCounterUpdate(packetPayload, "CCashShop::OnCashItemResIncBuyCharacterCountDone", short.MaxValue, value => _cashBuyCharacterCount = value, out string buyCharacterMessage)
                    ? buyCharacterMessage
                    : BuildPacketDecodeFailure("CCashShop::OnCashItemResIncBuyCharacterCountDone", packetPayload),
                116 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResIncBuyCharacterCountFailed"),
                119 => BuildCashSimpleResult("CCashShop::OnCashItemResMoveLtoSDone", packetPayload),
                120 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResMoveLtoSFailed"),
                121 => BuildCashSimpleResult("CCashShop::OnCashItemResMoveStoLDone", packetPayload),
                122 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResMoveStoLFailed"),
                123 => BuildCashSimpleResult("CCashShop::OnCashItemResDestroyDone", packetPayload),
                124 => BuildCashItemFailureMessage(packetPayload, "CCashShop::OnCashItemResDestroyFailed"),
                125 => BuildCashSimpleResult("CCashShop::OnCashItemResExpireDone", packetPayload),
                _ => $"{subtypeLabel} reached CCashShop with {packetPayload.Length.ToString(CultureInfo.InvariantCulture)} byte(s) of packet-owned state."
            };
            _cashItemCommoditySerialNumber = _cashPacketCatalogEntries.FirstOrDefault()?.ListingId ?? 0;
            _cashItemProductId = _cashPacketCatalogEntries.FirstOrDefault()?.ItemId ?? 0;
            _cashItemPrice = _cashPacketCatalogEntries.FirstOrDefault()?.Price ?? 0;
            if (_pendingCommoditySerialNumber > 0 && _cashItemResultSubtype is 92 or 98 or 100)
            {
                summary += $" Pending commodity SN {_pendingCommoditySerialNumber.ToString(CultureInfo.InvariantCulture)} was resumed.";
            }

            _cashItemLastSummary = summary;
            return summary;
        }

        private string ApplyItcNormalItemResult(byte[] payload)
        {
            byte[] packetPayload = payload ?? Array.Empty<byte>();
            _itcNormalItemSubtype = packetPayload.Length > 0 ? packetPayload[0] : -1;
            _itcNormalItemMutationCount++;
            _itcNormalItemLastSummary = _itcNormalItemSubtype switch
            {
                21 => TryApplyItcCatalogList(packetPayload, isSearchResult: false, out string listMessage)
                    ? listMessage
                    : BuildPacketDecodeFailure("CITC::OnGetITCListDone", packetPayload),
                22 => BuildItcFailureMessage(packetPayload, "CITC::OnGetITCListFailed"),
                23 => TryApplyItcCatalogList(packetPayload, isSearchResult: true, out string searchMessage)
                    ? searchMessage
                    : BuildPacketDecodeFailure("CITC::OnGetSearchITCListDone", packetPayload),
                24 => BuildItcFailureMessage(packetPayload, "CITC::OnGetSearchITCListFailed"),
                29 => BuildItcSimpleResult("CITC::OnNormalItemResRegisterSaleEntryDone", "Inventory owner reset after sale-entry registration.", packetPayload),
                30 => BuildItcFailureMessage(packetPayload, "CITC::OnNormalItemResRegisterSaleEntryFailed"),
                33 => TryApplyItcUserItemList(packetPayload, isPurchaseList: true, out string purchaseMessage)
                    ? purchaseMessage
                    : BuildPacketDecodeFailure("CITC::OnGetUserPurchaseItemDone", packetPayload),
                34 => BuildItcFailureMessage(packetPayload, "CITC::OnGetUserPurchaseItemFailed"),
                35 => TryApplyItcUserItemList(packetPayload, isPurchaseList: false, out string saleMessage)
                    ? saleMessage
                    : BuildPacketDecodeFailure("CITC::OnGetUserSaleItemDone", packetPayload),
                36 => BuildItcFailureMessage(packetPayload, "CITC::OnGetUserSaleItemFailed"),
                37 => TryApplyItcCancelSaleDone(packetPayload, out string cancelSaleMessage)
                    ? cancelSaleMessage
                    : BuildPacketDecodeFailure("CITC::OnCancelSaleItemDone", packetPayload),
                38 => BuildItcFailureMessage(packetPayload, "CITC::OnCancelSaleItemFailed"),
                39 => TryApplyItcMovePurchaseItemToStorage(packetPayload, out string movePurchaseMessage)
                    ? movePurchaseMessage
                    : BuildPacketDecodeFailure("CITC::OnMoveITCPurchaseItemLtoSDone", packetPayload),
                40 => BuildItcFailureMessage(packetPayload, "CITC::OnMoveITCPurchaseItemLtoSFailed"),
                41 => TryApplyItcWishMutation("CITC::OnSetZzimDone", addSelectedCatalogEntry: true, removeCurrentWishEntry: false, out string setWishMessage)
                    ? setWishMessage
                    : BuildPacketDecodeFailure("CITC::OnSetZzimDone", packetPayload),
                42 => BuildItcFailureMessage(packetPayload, "CITC::OnSetZzimFailed"),
                43 => TryApplyItcWishMutation("CITC::OnDeleteZzimDone", addSelectedCatalogEntry: false, removeCurrentWishEntry: true, out string deleteWishMessage)
                    ? deleteWishMessage
                    : BuildPacketDecodeFailure("CITC::OnDeleteZzimDone", packetPayload),
                44 => BuildItcFailureMessage(packetPayload, "CITC::OnDeleteZzimFailed"),
                45 => TryApplyItcWishSaleList(packetPayload, out string loadWishMessage)
                    ? loadWishMessage
                    : BuildPacketDecodeFailure("CITC::OnLoadWishSaleListDone", packetPayload),
                46 => BuildItcFailureMessage(packetPayload, "CITC::OnLoadWishSaleListFailed"),
                47 => TryApplyItcBuyCatalogItemDone("CITC::OnBuyWishDone", fromWishList: true, out string buyWishMessage)
                    ? buyWishMessage
                    : BuildPacketDecodeFailure("CITC::OnBuyWishDone", packetPayload),
                48 => BuildItcFailureMessage(packetPayload, "CITC::OnBuyWishFailed"),
                49 => TryApplyItcWishMutation("CITC::OnCancelWishDone", addSelectedCatalogEntry: false, removeCurrentWishEntry: true, out string cancelWishMessage)
                    ? cancelWishMessage
                    : BuildPacketDecodeFailure("CITC::OnCancelWishDone", packetPayload),
                50 => BuildItcFailureMessage(packetPayload, "CITC::OnCancelWishFailed"),
                51 => TryApplyItcBuyCatalogItemDone("CITC::OnBuyItemDone", fromWishList: false, out string buyItemMessage)
                    ? buyItemMessage
                    : BuildPacketDecodeFailure("CITC::OnBuyItemDone", packetPayload),
                52 => BuildItcFailureMessage(packetPayload, "CITC::OnBuyItemFailed"),
                53 => TryApplyItcBuyCatalogItemDone("CITC::OnBuyZzimItemDone", fromWishList: true, out string buyWishCatalogMessage)
                    ? buyWishCatalogMessage
                    : BuildPacketDecodeFailure("CITC::OnBuyZzimItemDone", packetPayload),
                54 => BuildItcFailureMessage(packetPayload, "CITC::OnBuyZzimItemFailed"),
                55 => TryApplyItcWishMutation("CITC::OnRegisterWishItemDone", addSelectedCatalogEntry: true, removeCurrentWishEntry: false, out string registerWishMessage)
                    ? registerWishMessage
                    : BuildPacketDecodeFailure("CITC::OnRegisterWishItemDone", packetPayload),
                56 => BuildItcFailureMessage(packetPayload, "CITC::OnRegisterWishItemFailed"),
                60 => BuildItcFailureMessage(packetPayload, "CITC::OnBidAuctionFailed"),
                61 => TryApplyItcCancelWishNotification(packetPayload, out string cancelWishNoticeMessage)
                    ? cancelWishNoticeMessage
                    : BuildPacketDecodeFailure("CITC::OnNotifyCancelWishResult", packetPayload),
                62 => BuildItcSimpleResult("CITC::OnSuccessBidInfoResult", "A packet-owned bid-success notice reached CITC.", packetPayload),
                _ => $"{GetItcNormalItemSubtypeLabel(_itcNormalItemSubtype)} reached CITC with {packetPayload.Length.ToString(CultureInfo.InvariantCulture)} byte(s) of packet-owned state."
            };
            return _itcNormalItemLastSummary;
        }

        private bool TryApplyCashLoadLockerDone(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1 + sizeof(short))
            {
                return false;
            }

            _ = reader.ReadByte();
            short itemCount = reader.ReadInt16();
            if (itemCount < 0)
            {
                return false;
            }

            List<CashItemInfoPacketSnapshot> snapshots = new(Math.Max(0, (int)itemCount));
            for (int i = 0; i < itemCount; i++)
            {
                if (!TryReadCashItemInfoPacketSnapshot(reader, out CashItemInfoPacketSnapshot snapshot))
                {
                    return false;
                }

                snapshots.Add(snapshot);
            }

            if (stream.Length - stream.Position < (sizeof(short) * 4))
            {
                return false;
            }

            _cashLockerItemCount = Math.Max(0, (int)itemCount);
            _cashLockerSlotLimit = Math.Max(0, (int)reader.ReadInt16());
            _cashCharacterSlotCount = Math.Max(0, (int)reader.ReadInt16());
            _cashBuyCharacterCount = Math.Max(0, (int)reader.ReadInt16());
            _cashCharacterCount = Math.Max(0, (int)reader.ReadInt16());
            string firstLockerDetail = snapshots.Count > 0
                ? DescribeCashItemInfoPacketSnapshot(snapshots[0], includeSerialNumber: true)
                : string.Empty;
            _noticeState = snapshots.Count > 0
                ? $"Cash locker refreshed to {_cashLockerItemCount.ToString(CultureInfo.InvariantCulture)}/{_cashLockerSlotLimit.ToString(CultureInfo.InvariantCulture)} entries. {firstLockerDetail}"
                : $"Cash locker refreshed to {_cashLockerItemCount.ToString(CultureInfo.InvariantCulture)}/{_cashLockerSlotLimit.ToString(CultureInfo.InvariantCulture)} entries.";
            message =
                $"CCashShop::OnCashItemResLoadLockerDone loaded {_cashLockerItemCount.ToString(CultureInfo.InvariantCulture)} cash locker item(s), locker limit {_cashLockerSlotLimit.ToString(CultureInfo.InvariantCulture)}, character slots {_cashCharacterSlotCount.ToString(CultureInfo.InvariantCulture)}, buy-count {_cashBuyCharacterCount.ToString(CultureInfo.InvariantCulture)}, characters {_cashCharacterCount.ToString(CultureInfo.InvariantCulture)}{(string.IsNullOrWhiteSpace(firstLockerDetail) ? string.Empty : $". First row: {firstLockerDetail}")}.";
            return true;
        }

        private bool TryApplyCashLoadGiftDone(byte[] payload, out string message)
        {
            message = null;
            if (payload.Length < 1 + sizeof(short))
            {
                return false;
            }

            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            _ = reader.ReadByte();
            short giftCount = reader.ReadInt16();
            List<PacketCatalogEntry> entries = new();
            for (int i = 0; i < Math.Max(0, (int)giftCount); i++)
            {
                if (!TryReadGiftListPacketSnapshot(reader, out GiftListPacketSnapshot snapshot))
                {
                    return false;
                }

                int rowNumber = i + 1;
                string itemTitle = ResolveCashStageGiftRowTitle(snapshot.ItemId, rowNumber, "Gift row");
                string sender = SanitizePacketString(snapshot.Sender, "CCashShop gift inbox");
                string giftMessage = SanitizePacketString(snapshot.Message, "No gift message.");
                entries.Add(new PacketCatalogEntry
                {
                    Title = itemTitle,
                    Detail = $"Gift row {rowNumber.ToString(CultureInfo.InvariantCulture)} from {sender} carries item {Math.Max(0, snapshot.ItemId).ToString(CultureInfo.InvariantCulture)} / serial {snapshot.SerialNumber.ToString(CultureInfo.InvariantCulture)}. {giftMessage}",
                    Seller = sender,
                    PriceLabel = $"Item {Math.Max(0, snapshot.ItemId).ToString(CultureInfo.InvariantCulture)}",
                    StateLabel = "Pending receive",
                    ListingId = rowNumber
                });
            }

            ReplaceCashPacketCatalogEntries("Packet gifts", "Gift", entries);
            _noticeState = entries.Count > 0
                ? $"Gift inbox refreshed with {entries.Count.ToString(CultureInfo.InvariantCulture)} gift row(s). {entries[0].Detail}"
                : $"Gift inbox refreshed with {Math.Max(0, (int)giftCount).ToString(CultureInfo.InvariantCulture)} gift row(s).";
            message = $"CCashShop::OnCashItemResLoadGiftDone loaded {Math.Max(0, (int)giftCount).ToString(CultureInfo.InvariantCulture)} gift row(s) into the dedicated receive-gift flow.";
            return true;
        }

        private bool TryApplyCashWishlistPacket(byte[] payload, string ownerName, out string message)
        {
            message = null;
            if (payload.Length < 1 + (_cashWishlistSerialNumbers.Length * sizeof(int)))
            {
                return false;
            }

            _cashPacketCatalogEntries.Clear();
            _wishlistCount = 0;
            for (int i = 0; i < _cashWishlistSerialNumbers.Length; i++)
            {
                int serialNumber = BitConverter.ToInt32(payload, 1 + (i * sizeof(int)));
                _cashWishlistSerialNumbers[i] = Math.Max(0, serialNumber);
                if (_cashWishlistSerialNumbers[i] <= 0)
                {
                    continue;
                }

                _wishlistCount++;
                _cashPacketCatalogEntries.Add(new PacketCatalogEntry
                {
                    Title = $"Wishlist slot {(i + 1).ToString(CultureInfo.InvariantCulture)}",
                    Detail = $"Commodity SN {_cashWishlistSerialNumbers[i].ToString(CultureInfo.InvariantCulture)} is now owned by the packet-authored wish list.",
                    Seller = "CCashShop wishlist",
                    PriceLabel = $"SN {_cashWishlistSerialNumbers[i].ToString(CultureInfo.InvariantCulture)}",
                    StateLabel = ResolveCashPurchaseRecordStateLabel(_cashWishlistSerialNumbers[i]),
                    ListingId = _cashWishlistSerialNumbers[i]
                });
            }

            _cashPacketPaneLabel = "Packet wishlist";
            _cashPacketBrowseModeLabel = "Wish";
            _searchState = $"{ownerName} refreshed {_wishlistCount.ToString(CultureInfo.InvariantCulture)} wishlist slot(s).";
            _noticeState = _wishlistCount > 0
                ? $"Wishlist SNs: {string.Join(", ", _cashWishlistSerialNumbers.Where(value => value > 0).Take(5))}."
                : "Wishlist cleared by packet-owned CCashShop state.";
            message = $"{ownerName} refreshed {_wishlistCount.ToString(CultureInfo.InvariantCulture)} wishlist slot(s) across the dedicated list/best owners.";
            return true;
        }

        private bool TryApplyCashBuyDone(byte[] payload, out string message)
        {
            message = null;
            if (payload.Length < 1 + 55)
            {
                return false;
            }

            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (!TryReadCashItemInfoPacketSnapshot(reader, out CashItemInfoPacketSnapshot snapshot))
            {
                return false;
            }

            _cashLockerItemCount = Math.Max(0, _cashLockerItemCount + 1);
            PacketCatalogEntry entry = BuildCashItemInfoPacketEntry(snapshot, "Purchased", "CCSWnd_Locker", "Bought");
            AppendCashPacketCatalogEntry("Packet purchase", "Buy", entry);
            _noticeState = $"A purchased cash item was appended to the locker (now {_cashLockerItemCount.ToString(CultureInfo.InvariantCulture)} item(s)). {DescribeCashItemInfoPacketSnapshot(snapshot, includeSerialNumber: true)}";
            message = $"CCashShop::OnCashItemResBuyDone appended one decoded GW_CashItemInfo body to CCSWnd_Locker: {DescribeCashItemInfoPacketSnapshot(snapshot, includeSerialNumber: true)}";
            return true;
        }

        private bool TryApplyCashGiftDone(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1)
            {
                return false;
            }

            _ = reader.ReadByte();
            if (!TryReadMapleString(reader, out string recipient)
                || stream.Length - stream.Position < sizeof(int) + sizeof(short) + sizeof(int))
            {
                return false;
            }

            int itemId = reader.ReadInt32();
            int quantity = Math.Max(1, (int)reader.ReadInt16());
            int prepaidCost = Math.Max(0, reader.ReadInt32());
            _cashGiftLastSummary =
                $"Gifted item {Math.Max(0, itemId).ToString(CultureInfo.InvariantCulture)} x{quantity.ToString(CultureInfo.InvariantCulture)} to {SanitizePacketString(recipient, "gift recipient")} for {prepaidCost.ToString("N0", CultureInfo.InvariantCulture)} NX Prepaid.";
            AppendCashPacketCatalogEntry("Packet gifts", "Gift", new PacketCatalogEntry
            {
                Title = $"Gifted item {Math.Max(0, itemId).ToString(CultureInfo.InvariantCulture)}",
                Detail = _cashGiftLastSummary,
                Seller = SanitizePacketString(recipient, "gift recipient"),
                PriceLabel = prepaidCost.ToString("N0", CultureInfo.InvariantCulture),
                StateLabel = "Gift sent",
                ItemId = Math.Max(0, itemId),
                Quantity = quantity
            });
            _noticeState = _cashGiftLastSummary;
            message = $"CCashShop::OnCashItemResGiftDone confirmed {_cashGiftLastSummary}";
            return true;
        }

        private bool TryApplyCashCouponDone(byte[] payload, bool isGiftCoupon, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1)
            {
                return false;
            }

            _ = reader.ReadByte();
            string firstString = TryReadMapleString(reader, out string decodedString)
                ? SanitizePacketString(decodedString, isGiftCoupon ? "coupon recipient" : "coupon code")
                : string.Empty;
            int value = stream.Length - stream.Position >= sizeof(int)
                ? Math.Max(0, reader.ReadInt32())
                : 0;
            _cashCouponLastSummary = isGiftCoupon
                ? (string.IsNullOrWhiteSpace(firstString)
                    ? "Gift coupon registration completed inside the dedicated Cash Shop status owner."
                    : $"Gift coupon registration completed for {firstString}.")
                : (string.IsNullOrWhiteSpace(firstString)
                    ? "Coupon registration completed inside the dedicated Cash Shop status owner."
                    : $"Coupon registration completed for {firstString}.")
                    + (value > 0 ? $" Packet value {value.ToString(CultureInfo.InvariantCulture)} was acknowledged." : string.Empty);
            AppendCashPacketCatalogEntry("Packet coupons", "Coupon", new PacketCatalogEntry
            {
                Title = isGiftCoupon ? "Gift coupon" : "Coupon",
                Detail = _cashCouponLastSummary,
                Seller = string.IsNullOrWhiteSpace(firstString) ? "CCSWnd_Status" : firstString,
                PriceLabel = value > 0 ? value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                StateLabel = isGiftCoupon ? "Gift coupon" : "Coupon"
            });
            _noticeState = _cashCouponLastSummary;
            message = isGiftCoupon
                ? $"CCashShop::OnCashItemResGiftCouponDone confirmed {_cashCouponLastSummary}"
                : $"CCashShop::OnCashItemResUseCouponDone confirmed {_cashCouponLastSummary}";
            return true;
        }

        private bool TryApplyCashBuyPackageDone(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1 + sizeof(byte) + sizeof(short))
            {
                return false;
            }

            _ = reader.ReadByte();
            int itemCount = Math.Max(0, (int)reader.ReadByte());
            List<CashItemInfoPacketSnapshot> snapshots = new(itemCount);
            for (int i = 0; i < itemCount; i++)
            {
                if (!TryReadCashItemInfoPacketSnapshot(reader, out CashItemInfoPacketSnapshot snapshot))
                {
                    return false;
                }

                snapshots.Add(snapshot);
            }

            if (stream.Length - stream.Position < sizeof(short))
            {
                return false;
            }

            int bonusCount = Math.Max(0, (int)reader.ReadUInt16());
            _cashLockerItemCount = Math.Max(0, _cashLockerItemCount + itemCount);
            if (snapshots.Count == 0)
            {
                AppendCashPacketCatalogEntry("Packet package", "Package", new PacketCatalogEntry
                {
                    Title = "Package purchase",
                    Detail = $"Package purchase appended {itemCount.ToString(CultureInfo.InvariantCulture)} locker item(s){(bonusCount > 0 ? $" and reported {bonusCount.ToString(CultureInfo.InvariantCulture)} bonus count" : string.Empty)}.",
                    Seller = "CCSWnd_Locker",
                    PriceLabel = itemCount.ToString(CultureInfo.InvariantCulture),
                    StateLabel = "Package"
                });
            }
            else
            {
                for (int i = snapshots.Count - 1; i >= 0; i--)
                {
                    AppendCashPacketCatalogEntry("Packet package", "Package", BuildCashItemInfoPacketEntry(snapshots[i], "Package item", "CCSWnd_Locker", "Package"));
                }
            }

            string firstPackageDetail = snapshots.Count > 0
                ? DescribeCashItemInfoPacketSnapshot(snapshots[0], includeSerialNumber: true)
                : string.Empty;
            _noticeState = $"Package purchase appended {itemCount.ToString(CultureInfo.InvariantCulture)} locker item(s){(bonusCount > 0 ? $" and reported {bonusCount.ToString(CultureInfo.InvariantCulture)} bonus count" : string.Empty)}{(string.IsNullOrWhiteSpace(firstPackageDetail) ? "." : $". First row: {firstPackageDetail}")}";
            message =
                $"CCashShop::OnCashItemResBuyPackageDone appended {itemCount.ToString(CultureInfo.InvariantCulture)} decoded GW_CashItemInfo bod{(itemCount == 1 ? "y" : "ies")} to CCSWnd_Locker{(bonusCount > 0 ? $" and surfaced bonus count {bonusCount.ToString(CultureInfo.InvariantCulture)}" : string.Empty)}{(string.IsNullOrWhiteSpace(firstPackageDetail) ? string.Empty : $". First row: {firstPackageDetail}")}.";
            return true;
        }

        private bool TryApplyCashGiftPackageDone(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1)
            {
                return false;
            }

            _ = reader.ReadByte();
            if (!TryReadMapleString(reader, out string recipient)
                || stream.Length - stream.Position < sizeof(int) + (sizeof(short) * 2) + sizeof(int))
            {
                return false;
            }

            int itemId = reader.ReadInt32();
            int itemCount = Math.Max(0, (int)reader.ReadUInt16());
            int bonusCount = Math.Max(0, (int)reader.ReadUInt16());
            int prepaidCost = Math.Max(0, reader.ReadInt32());
            _cashGiftLastSummary =
                $"Gifted package {Math.Max(0, itemId).ToString(CultureInfo.InvariantCulture)} to {SanitizePacketString(recipient, "gift recipient")} with {itemCount.ToString(CultureInfo.InvariantCulture)} item(s), {bonusCount.ToString(CultureInfo.InvariantCulture)} bonus count, and {prepaidCost.ToString("N0", CultureInfo.InvariantCulture)} NX Prepaid.";
            AppendCashPacketCatalogEntry("Packet package", "Package", new PacketCatalogEntry
            {
                Title = $"Gifted package {Math.Max(0, itemId).ToString(CultureInfo.InvariantCulture)}",
                Detail = _cashGiftLastSummary,
                Seller = SanitizePacketString(recipient, "gift recipient"),
                PriceLabel = prepaidCost.ToString("N0", CultureInfo.InvariantCulture),
                StateLabel = "Gift package",
                ItemId = Math.Max(0, itemId),
                Quantity = Math.Max(1, itemCount)
            });
            _noticeState = _cashGiftLastSummary;
            message = $"CCashShop::OnCashItemResGiftPackageDone confirmed {_cashGiftLastSummary}";
            return true;
        }

        private bool TryApplyCashBuyNormalDone(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1 + sizeof(int))
            {
                return false;
            }

            _ = reader.ReadByte();
            int itemCount = Math.Max(0, reader.ReadInt32());
            if (stream.Length - stream.Position < itemCount * sizeof(long))
            {
                return false;
            }

            _cashInventoryPacketEntries.Clear();
            _cashPacketCatalogEntries.Clear();
            _cashPacketPaneLabel = "Packet inventory";
            _cashPacketBrowseModeLabel = "Inventory";
            for (int i = 0; i < itemCount; i++)
            {
                int slotIndex = Math.Max(0, (int)reader.ReadUInt16());
                int itemId = Math.Max(0, reader.ReadInt32());
                int inventoryTab = Math.Max(0, (itemId / 1_000_000) - 1);
                _ = reader.ReadUInt16();
                PacketCatalogEntry entry = new()
                {
                    Title = $"Inventory tab {inventoryTab.ToString(CultureInfo.InvariantCulture)} slot {slotIndex.ToString(CultureInfo.InvariantCulture)}",
                    Detail = $"CCSWnd_Inventory focused tab {inventoryTab.ToString(CultureInfo.InvariantCulture)} for item {itemId.ToString(CultureInfo.InvariantCulture)} at slot {slotIndex.ToString(CultureInfo.InvariantCulture)}.",
                    Seller = "CCSWnd_Inventory",
                    PriceLabel = $"Item {itemId.ToString(CultureInfo.InvariantCulture)}",
                    StateLabel = "Bought",
                    ListingId = slotIndex,
                    ItemId = itemId
                };
                _cashInventoryPacketEntries.Add(entry);
                _cashPacketCatalogEntries.Add(ClonePacketCatalogEntry(entry, "Inventory"));
            }

            _noticeState = _cashInventoryPacketEntries.Count > 0
                ? _cashInventoryPacketEntries[0].Detail
                : "Cash inventory purchase result completed without any decoded inventory slot rows.";
            message =
                $"CCashShop::OnCashItemResBuyNormalDone updated {_cashInventoryPacketEntries.Count.ToString(CultureInfo.InvariantCulture)} inventory slot row(s) through CCSWnd_Inventory.";
            return true;
        }

        private bool TryApplyCashPurchaseRecord(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1 + sizeof(int) + sizeof(byte))
            {
                return false;
            }

            _ = reader.ReadByte();
            int commoditySerialNumber = Math.Max(0, reader.ReadInt32());
            bool purchaseRecorded = reader.ReadByte() != 0;
            _cashPurchaseRecordSummary = commoditySerialNumber > 0
                ? $"Purchase record SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)} is {(purchaseRecorded ? "marked purchased" : "present but not purchased")}."
                : $"Global purchase record state is {(purchaseRecorded ? "enabled" : "cleared")}.";
            if (commoditySerialNumber > 0)
            {
                _cashPurchaseRecordStates[commoditySerialNumber] = purchaseRecorded;
                ApplyCashPurchaseRecordStateToCatalogEntries(commoditySerialNumber, purchaseRecorded);
            }
            else
            {
                ApplyCashPurchaseRecordStateToCatalogEntries(0, purchaseRecorded);
            }

            AppendCashPacketCatalogEntry("Packet purchase", "Buy", new PacketCatalogEntry
            {
                Title = commoditySerialNumber > 0
                    ? $"Purchase record {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)}"
                    : "Purchase record",
                Detail = _cashPurchaseRecordSummary,
                Seller = "CCashShop",
                PriceLabel = commoditySerialNumber > 0 ? $"SN {commoditySerialNumber.ToString(CultureInfo.InvariantCulture)}" : string.Empty,
                StateLabel = purchaseRecorded ? "Purchased" : "Wish",
                ListingId = commoditySerialNumber
            });
            _noticeState = _cashPurchaseRecordSummary;
            message = $"CCashShop::OnCashItemResPurchaseRecord updated {_cashPurchaseRecordSummary}";
            return true;
        }

        private bool TryApplyCashNameChangeDone(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1)
            {
                return false;
            }

            _ = reader.ReadByte();
            string requestedName = TryReadMapleString(reader, out string decodedName)
                ? SanitizePacketString(decodedName, "requested name")
                : string.Empty;
            _cashNameChangeLastSummary = string.IsNullOrWhiteSpace(requestedName)
                ? "Name-change purchase completed inside the dedicated Cash Shop stage."
                : $"Name-change purchase completed for {requestedName}.";
            AppendCashPacketCatalogEntry("Packet rename", "Name", new PacketCatalogEntry
            {
                Title = "Name change",
                Detail = _cashNameChangeLastSummary,
                Seller = string.IsNullOrWhiteSpace(requestedName) ? "CCashShop" : requestedName,
                PriceLabel = requestedName,
                StateLabel = "Renamed"
            });
            _noticeState = _cashNameChangeLastSummary;
            message = $"CCashShop::OnCashItemNameChangeResBuyDone confirmed {_cashNameChangeLastSummary}";
            return true;
        }

        private bool TryApplyCashTransferWorldDone(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1)
            {
                return false;
            }

            _ = reader.ReadByte();
            int worldId = stream.Length - stream.Position >= sizeof(int)
                ? Math.Max(0, reader.ReadInt32())
                : 0;
            string targetName = TryReadMapleString(reader, out string decodedName)
                ? SanitizePacketString(decodedName, "transfer target")
                : string.Empty;
            _cashTransferWorldLastSummary = worldId > 0 || !string.IsNullOrWhiteSpace(targetName)
                ? $"Transfer-world purchase completed{(worldId > 0 ? $" for world {worldId.ToString(CultureInfo.InvariantCulture)}" : string.Empty)}{(!string.IsNullOrWhiteSpace(targetName) ? $" on {targetName}" : string.Empty)}."
                : "Transfer-world purchase completed inside the dedicated Cash Shop stage.";
            AppendCashPacketCatalogEntry("Packet transfer", "Transfer", new PacketCatalogEntry
            {
                Title = "Transfer world",
                Detail = _cashTransferWorldLastSummary,
                Seller = string.IsNullOrWhiteSpace(targetName) ? "CCashShop" : targetName,
                PriceLabel = worldId > 0 ? $"World {worldId.ToString(CultureInfo.InvariantCulture)}" : string.Empty,
                StateLabel = "Transferred"
            });
            _noticeState = _cashTransferWorldLastSummary;
            message = $"CCashShop::OnCashItemResTransferWorldDone confirmed {_cashTransferWorldLastSummary}";
            return true;
        }

        private bool TryApplyCashGachaponDone(byte[] payload, bool isCopyResult, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1)
            {
                return false;
            }

            _ = reader.ReadByte();
            int itemId = stream.Length - stream.Position >= sizeof(int)
                ? Math.Max(0, reader.ReadInt32())
                : 0;
            int count = stream.Length - stream.Position >= sizeof(short)
                ? Math.Max(1, (int)reader.ReadInt16())
                : 1;
            _cashGachaponLastSummary = itemId > 0
                ? $"{(isCopyResult ? "Cash gachapon copy" : "Cash gachapon open")} yielded item {itemId.ToString(CultureInfo.InvariantCulture)} x{count.ToString(CultureInfo.InvariantCulture)}."
                : $"{(isCopyResult ? "Cash gachapon copy" : "Cash gachapon open")} completed inside the dedicated stage.";
            AppendCashPacketCatalogEntry("Packet gachapon", "Gachapon", new PacketCatalogEntry
            {
                Title = isCopyResult ? "Gachapon copy" : "Gachapon open",
                Detail = _cashGachaponLastSummary,
                Seller = "CCashShop gachapon",
                PriceLabel = itemId > 0 ? $"Item {itemId.ToString(CultureInfo.InvariantCulture)}" : string.Empty,
                StateLabel = isCopyResult ? "Copied" : "Opened",
                ItemId = itemId,
                Quantity = count
            });
            _noticeState = _cashGachaponLastSummary;
            message = isCopyResult
                ? $"CCashShop::OnCashItemResCashGachaponCopyDone confirmed {_cashGachaponLastSummary}"
                : $"CCashShop::OnCashItemResCashGachaponOpenDone confirmed {_cashGachaponLastSummary}";
            return true;
        }

        private bool TryApplyCashMaplePointChangeDone(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 1 + sizeof(int))
            {
                return false;
            }

            int maplePoint = Math.Max(0, BitConverter.ToInt32(payload, 1));
            _maplePoint = maplePoint;
            _noticeState = $"Cash-service Maple Point balance changed to {_maplePoint.ToString("N0", CultureInfo.InvariantCulture)}.";
            message = $"CCashShop::OnCashItemResChangeMaplePointDone updated the dedicated status owner to {_maplePoint.ToString("N0", CultureInfo.InvariantCulture)} Maple Points.";
            return true;
        }

        private bool TryApplyCashRebateDone(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1 + sizeof(long) + sizeof(int))
            {
                return false;
            }

            _ = reader.ReadByte();
            long serialNumber = reader.ReadInt64();
            int prepaidRefund = Math.Max(0, reader.ReadInt32());
            _cashLockerItemCount = Math.Max(0, _cashLockerItemCount - 1);
            _noticeState =
                $"Rebate removed locker serial {serialNumber.ToString(CultureInfo.InvariantCulture)} and returned {prepaidRefund.ToString("N0", CultureInfo.InvariantCulture)} NX Prepaid.";
            message = $"CCashShop::OnCashItemResRebateDone updated CCSWnd_Locker with {_noticeState}";
            return true;
        }

        private bool TryApplyCashCounterUpdate(byte[] payload, string ownerName, int maxValue, Action<int> applyValue, out string message)
        {
            message = null;
            if (payload.Length < 1 + sizeof(short))
            {
                return false;
            }

            int value = Math.Max(0, (int)BitConverter.ToInt16(payload, 1));
            if (maxValue > 0)
            {
                value = Math.Min(maxValue, value);
            }

            applyValue(value);
            _noticeState = $"{ownerName} updated the packet-owned counter to {value.ToString(CultureInfo.InvariantCulture)}.";
            message = _noticeState;
            return true;
        }

        private string BuildCashItemFailureMessage(byte[] payload, string ownerName)
        {
            int reason = payload.Length >= 2 ? payload[1] : -1;
            string failureMessage = reason >= 0
                ? $"{ownerName} failed with reason {reason.ToString(CultureInfo.InvariantCulture)}."
                : $"{ownerName} failed before a reason byte could be decoded.";
            _noticeState = failureMessage;

            if (ownerName.Contains("Coupon", StringComparison.Ordinal))
            {
                _cashCouponLastSummary = failureMessage;
            }
            else if (ownerName.Contains("NameChange", StringComparison.Ordinal))
            {
                _cashNameChangeLastSummary = failureMessage;
            }
            else if (ownerName.Contains("TransferWorld", StringComparison.Ordinal))
            {
                _cashTransferWorldLastSummary = failureMessage;
            }
            else if (ownerName.Contains("Gachapon", StringComparison.Ordinal))
            {
                _cashGachaponLastSummary = failureMessage;
            }

            return failureMessage;
        }

        private string BuildCashSimpleResult(string ownerName, byte[] payload)
        {
            _noticeState = $"{ownerName} completed with {payload.Length.ToString(CultureInfo.InvariantCulture)} byte(s) of packet-owned state.";
            return _noticeState;
        }

        private string BuildCashPurchaseExpResult(byte[] payload)
        {
            int value = payload != null && payload.Length >= sizeof(int)
                ? Math.Max(0, BitConverter.ToInt32(payload, 0))
                : 0;
            _noticeState = value > 0
                ? $"Purchase-exp packet updated the dedicated Cash Shop stage with value {value.ToString(CultureInfo.InvariantCulture)}."
                : "Purchase-exp update routed through Cash Shop packet ownership.";
            AppendCashPacketCatalogEntry("Packet purchase", "Buy", new PacketCatalogEntry
            {
                Title = "Purchase EXP",
                Detail = _noticeState,
                Seller = "CCashShop",
                PriceLabel = value > 0 ? value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                StateLabel = "Purchase EXP"
            });
            return _noticeState;
        }

        private string BuildCashGiftMateResult(byte[] payload)
        {
            string accountName = TryReadUtf8Text(payload, out string decodedText)
                ? SanitizePacketString(decodedText, "gift mate")
                : string.Empty;
            _noticeState = string.IsNullOrWhiteSpace(accountName)
                ? "Gift-mate result stayed inside Cash Shop packet ownership."
                : $"Gift-mate result refreshed the packet-owned recipient state for {accountName}.";
            AppendCashPacketCatalogEntry("Packet gifts", "Gift", new PacketCatalogEntry
            {
                Title = "Gift mate",
                Detail = _noticeState,
                Seller = string.IsNullOrWhiteSpace(accountName) ? "CCashShop" : accountName,
                PriceLabel = accountName,
                StateLabel = "Recipient"
            });
            return _noticeState;
        }

        private string BuildCashDuplicateIdResult(byte[] payload)
        {
            string duplicateId = TryReadUtf8Text(payload, out string decodedText)
                ? SanitizePacketString(decodedText, "duplicate id")
                : string.Empty;
            _noticeState = string.IsNullOrWhiteSpace(duplicateId)
                ? "Duplicate-id result stayed inside Cash Shop packet ownership."
                : $"Duplicate-id result checked {duplicateId} inside the dedicated Cash Shop stage.";
            AppendCashPacketCatalogEntry("Packet rename", "Name", new PacketCatalogEntry
            {
                Title = "Duplicate ID check",
                Detail = _noticeState,
                Seller = string.IsNullOrWhiteSpace(duplicateId) ? "CCashShop" : duplicateId,
                PriceLabel = duplicateId,
                StateLabel = "Checked"
            });
            return _noticeState;
        }

        private string BuildCashNameChangePacketResult(byte[] payload)
        {
            string requestedName = TryReadUtf8Text(payload, out string decodedText)
                ? SanitizePacketString(decodedText, "requested name")
                : string.Empty;
            _cashNameChangeLastSummary = string.IsNullOrWhiteSpace(requestedName)
                ? "Name-change result stayed inside Cash Shop packet ownership."
                : $"Name-change packet refreshed the dedicated owner for {requestedName}.";
            AppendCashPacketCatalogEntry("Packet rename", "Name", new PacketCatalogEntry
            {
                Title = "Name change",
                Detail = _cashNameChangeLastSummary,
                Seller = string.IsNullOrWhiteSpace(requestedName) ? "CCashShop" : requestedName,
                PriceLabel = requestedName,
                StateLabel = "Pending"
            });
            _noticeState = _cashNameChangeLastSummary;
            return _cashNameChangeLastSummary;
        }

        private string BuildCashTransferWorldPacketResult(byte[] payload)
        {
            int worldId = payload != null && payload.Length >= sizeof(int)
                ? Math.Max(0, BitConverter.ToInt32(payload, 0))
                : 0;
            _cashTransferWorldLastSummary = worldId > 0
                ? $"Transfer-world packet refreshed world target {worldId.ToString(CultureInfo.InvariantCulture)} inside the dedicated stage."
                : "Transfer-world result stayed inside Cash Shop packet ownership.";
            AppendCashPacketCatalogEntry("Packet transfer", "Transfer", new PacketCatalogEntry
            {
                Title = "Transfer world",
                Detail = _cashTransferWorldLastSummary,
                Seller = "CCashShop",
                PriceLabel = worldId > 0 ? $"World {worldId.ToString(CultureInfo.InvariantCulture)}" : string.Empty,
                StateLabel = "Pending"
            });
            _noticeState = _cashTransferWorldLastSummary;
            return _cashTransferWorldLastSummary;
        }

        private string BuildCashGachaponStampResult(byte[] payload)
        {
            int stampCount = payload != null && payload.Length >= sizeof(int)
                ? Math.Max(0, BitConverter.ToInt32(payload, 0))
                : 0;
            _cashGachaponLastSummary = stampCount > 0
                ? $"CashShop gachapon stamp result reported {stampCount.ToString(CultureInfo.InvariantCulture)} stamp(s) on the dedicated stage."
                : "CashShop gachapon stamp result reached the dedicated stage.";
            AppendCashPacketCatalogEntry("Packet gachapon", "Gachapon", new PacketCatalogEntry
            {
                Title = "Gachapon stamp",
                Detail = _cashGachaponLastSummary,
                Seller = "CCashShop gachapon",
                PriceLabel = stampCount > 0 ? stampCount.ToString(CultureInfo.InvariantCulture) : string.Empty,
                StateLabel = "Stamp"
            });
            _noticeState = _cashGachaponLastSummary;
            return _cashGachaponLastSummary;
        }

        private string BuildCashGachaponPacketResult(byte[] payload, int packetType)
        {
            int itemId = payload != null && payload.Length >= sizeof(int)
                ? Math.Max(0, BitConverter.ToInt32(payload, 0))
                : 0;
            _cashGachaponLastSummary = itemId > 0
                ? $"CashShop gachapon packet {packetType.ToString(CultureInfo.InvariantCulture)} staged item {itemId.ToString(CultureInfo.InvariantCulture)}."
                : "CashShop gachapon result reached the dedicated stage.";
            AppendCashPacketCatalogEntry("Packet gachapon", "Gachapon", new PacketCatalogEntry
            {
                Title = $"Gachapon packet {packetType.ToString(CultureInfo.InvariantCulture)}",
                Detail = _cashGachaponLastSummary,
                Seller = "CCashShop gachapon",
                PriceLabel = itemId > 0 ? $"Item {itemId.ToString(CultureInfo.InvariantCulture)}" : string.Empty,
                StateLabel = "Pending",
                ItemId = itemId
            });
            _noticeState = _cashGachaponLastSummary;
            return _cashGachaponLastSummary;
        }

        private string ApplyCashOneADayPacket(byte[] payload)
        {
            if (!TryDecodeOneADayPayload(payload, out OneADayPacketState state))
            {
                _cashOneADayItemDate = 0;
                _cashOneADayItemSerialNumber = 0;
                _cashOneADayHistoryEntries.Clear();
                _noticeState = "One-a-day owner received an empty packet payload.";
                return "CCashShop::OnOneADay cleared the current item and previous history from an empty payload.";
            }

            _cashOneADayItemDate = state.CurrentDate;
            _cashOneADayItemSerialNumber = state.CurrentCommoditySerialNumber;
            _cashOneADayHistoryEntries.Clear();
            _cashOneADayHistoryEntries.AddRange(state.HistoryEntries);

            string currentLabel = _cashOneADayItemSerialNumber > 0
                ? $"current SN {_cashOneADayItemSerialNumber.ToString(CultureInfo.InvariantCulture)}"
                : "no current item";
            _noticeState =
                $"One-a-day owner loaded {currentLabel}, date {_cashOneADayItemDate.ToString(CultureInfo.InvariantCulture)}, and {_cashOneADayHistoryEntries.Count.ToString(CultureInfo.InvariantCulture)} previous slot(s).";
            return
                $"CCashShop::OnOneADay decoded current date {_cashOneADayItemDate.ToString(CultureInfo.InvariantCulture)}, current SN {_cashOneADayItemSerialNumber.ToString(CultureInfo.InvariantCulture)}, and {_cashOneADayHistoryEntries.Count.ToString(CultureInfo.InvariantCulture)} previous entry(ies).";
        }

        internal static bool TryDecodeOneADayPayload(byte[] payload, out OneADayPacketState state)
        {
            state = null;
            if (payload == null || payload.Length < sizeof(int) * 3)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                int currentDate = reader.ReadInt32();
                int currentCommoditySerialNumber = reader.ReadInt32();
                int historyCount = Math.Max(0, reader.ReadInt32());
                if (stream.Length - stream.Position < historyCount * 12L)
                {
                    return false;
                }

                List<OneADayHistoryEntry> historyEntries = new(historyCount);
                for (int i = 0; i < historyCount; i++)
                {
                    historyEntries.Add(new OneADayHistoryEntry
                    {
                        CommoditySerialNumber = reader.ReadInt32(),
                        OriginalCommoditySerialNumber = reader.ReadInt32(),
                        RawDate = reader.ReadInt32()
                    });
                }

                state = new OneADayPacketState
                {
                    CurrentDate = currentDate,
                    CurrentCommoditySerialNumber = currentCommoditySerialNumber,
                    HistoryEntries = historyEntries
                };
                return true;
            }
            catch (EndOfStreamException)
            {
                state = null;
                return false;
            }
            catch (IOException)
            {
                state = null;
                return false;
            }
        }

        private bool TryApplyItcCatalogList(byte[] payload, bool isSearchResult, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1 + (sizeof(int) * 5) + (sizeof(byte) * 2))
            {
                return false;
            }

            _ = reader.ReadByte();
            _itcCurrentCategoryItemCount = Math.Max(0, reader.ReadInt32());
            _itcNormalItemPageEntryCount = Math.Max(0, reader.ReadInt32());
            _itcNormalItemCategory = Math.Max(0, reader.ReadInt32());
            _itcNormalItemSubCategory = Math.Max(0, reader.ReadInt32());
            _itcNormalItemPage = Math.Max(0, reader.ReadInt32());
            _itcNormalItemSortType = Math.Max(0, (int)reader.ReadByte());
            _itcNormalItemSortColumn = Math.Max(0, (int)reader.ReadByte());
            _itcNormalItemEntryCount = _itcCurrentCategoryItemCount;
            _itcPacketCatalogEntries.Clear();

            for (int i = 0; i < _itcNormalItemPageEntryCount; i++)
            {
                if (!TryReadItcItemEntry(reader, out PacketCatalogEntry entry))
                {
                    return false;
                }

                _itcPacketCatalogEntries.Add(entry);
            }

            if (stream.Position < stream.Length)
            {
                _ = reader.ReadByte();
            }

            PacketCatalogEntry selectedEntry = _itcPacketCatalogEntries.FirstOrDefault();
            _itcNormalItemSelectedListingId = selectedEntry?.ListingId ?? 0;
            _itcNormalItemSelectedPrice = selectedEntry?.Price ?? 0;
            _navigationState =
                $"CITC category {_itcNormalItemCategory.ToString(CultureInfo.InvariantCulture)} / subcategory {_itcNormalItemSubCategory.ToString(CultureInfo.InvariantCulture)} / page {_itcNormalItemPage.ToString(CultureInfo.InvariantCulture)} owned by {(isSearchResult ? "search" : "list")} results.";
            _searchState =
                $"Sort column {_itcNormalItemSortColumn.ToString(CultureInfo.InvariantCulture)} / sort type {_itcNormalItemSortType.ToString(CultureInfo.InvariantCulture)} with {_itcNormalItemPageEntryCount.ToString(CultureInfo.InvariantCulture)} visible row(s).";
            _noticeState = selectedEntry != null
                ? $"{selectedEntry.Title} by {selectedEntry.Seller} is the packet-owned focused row."
                : "The packet-owned CITC list is empty.";
            message =
                $"{(isSearchResult ? "CITC::OnGetSearchITCListDone" : "CITC::OnGetITCListDone")} loaded {_itcNormalItemPageEntryCount.ToString(CultureInfo.InvariantCulture)} row(s) out of {_itcCurrentCategoryItemCount.ToString(CultureInfo.InvariantCulture)} for category {_itcNormalItemCategory.ToString(CultureInfo.InvariantCulture)}/{_itcNormalItemSubCategory.ToString(CultureInfo.InvariantCulture)} page {_itcNormalItemPage.ToString(CultureInfo.InvariantCulture)}.";
            return true;
        }

        private bool TryApplyItcUserItemList(byte[] payload, bool isPurchaseList, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1 + sizeof(int))
            {
                return false;
            }

            _ = reader.ReadByte();
            int count = Math.Max(0, reader.ReadInt32());
            List<PacketCatalogEntry> target = isPurchaseList ? _itcPurchasePacketEntries : _itcSalePacketEntries;
            target.Clear();
            for (int i = 0; i < count; i++)
            {
                if (!TryReadItcItemEntry(reader, out PacketCatalogEntry entry))
                {
                    return false;
                }

                target.Add(entry);
            }

            int limitedCount = 0;
            if (isPurchaseList && stream.Length - stream.Position >= sizeof(int))
            {
                limitedCount = Math.Max(0, reader.ReadInt32());
            }

            if (stream.Position < stream.Length)
            {
                _ = reader.ReadByte();
            }

            if (isPurchaseList)
            {
                _itcPurchaseItemCount = count;
                _noticeState = limitedCount > 0
                    ? $"Purchase owner loaded {_itcPurchaseItemCount.ToString(CultureInfo.InvariantCulture)} row(s) with {limitedCount.ToString(CultureInfo.InvariantCulture)} limited result(s) still outstanding."
                    : $"Purchase owner loaded {_itcPurchaseItemCount.ToString(CultureInfo.InvariantCulture)} row(s).";
                message = $"CITC::OnGetUserPurchaseItemDone loaded {_itcPurchaseItemCount.ToString(CultureInfo.InvariantCulture)} purchase row(s){(limitedCount > 0 ? $" and {limitedCount.ToString(CultureInfo.InvariantCulture)} limited-item notice row(s)" : string.Empty)}.";
            }
            else
            {
                _itcSaleItemCount = count;
                _noticeState = $"Sale owner loaded {_itcSaleItemCount.ToString(CultureInfo.InvariantCulture)} packet-authored row(s).";
                message = $"CITC::OnGetUserSaleItemDone loaded {_itcSaleItemCount.ToString(CultureInfo.InvariantCulture)} sale row(s).";
            }

            PacketCatalogEntry selectedEntry = target.FirstOrDefault();
            _itcNormalItemSelectedListingId = selectedEntry?.ListingId ?? _itcNormalItemSelectedListingId;
            _itcNormalItemSelectedPrice = selectedEntry?.Price ?? _itcNormalItemSelectedPrice;
            return true;
        }

        private bool TryApplyItcWishSaleList(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1 + sizeof(int))
            {
                return false;
            }

            _ = reader.ReadByte();
            int count = Math.Max(0, reader.ReadInt32());
            _itcWishPacketEntries.Clear();
            for (int i = 0; i < count; i++)
            {
                if (!TryReadItcItemEntry(reader, out PacketCatalogEntry entry))
                {
                    return false;
                }

                entry.StateLabel = string.IsNullOrWhiteSpace(entry.StateLabel)
                    ? "Wish"
                    : $"{entry.StateLabel} / Wish";
                _itcWishPacketEntries.Add(entry);
            }

            _noticeState = _itcWishPacketEntries.Count > 0
                ? $"Wish-sale owner loaded {_itcWishPacketEntries.Count.ToString(CultureInfo.InvariantCulture)} packet-authored row(s)."
                : "Wish-sale owner loaded no packet-authored rows.";
            message = $"CITC::OnLoadWishSaleListDone loaded {_itcWishPacketEntries.Count.ToString(CultureInfo.InvariantCulture)} packet-authored wish-sale row(s).";
            return true;
        }

        private bool TryApplyItcCancelSaleDone(byte[] payload, out string message)
        {
            RemovePrimaryEntry(_itcSalePacketEntries, out PacketCatalogEntry removedEntry);
            _itcSaleItemCount = Math.Max(0, _itcSalePacketEntries.Count);
            _noticeState = removedEntry != null
                ? $"Cancelled sale listing {removedEntry.ListingId.ToString(CultureInfo.InvariantCulture)} from the packet-owned sale owner."
                : "Cancelled the focused packet-owned sale listing.";
            message = $"CITC::OnCancelSaleItemDone removed {(removedEntry != null ? $"listing {removedEntry.ListingId.ToString(CultureInfo.InvariantCulture)}" : "the focused sale row")} from the sale owner.";
            return true;
        }

        private bool TryApplyItcMovePurchaseItemToStorage(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1 + (sizeof(int) * 2))
            {
                return false;
            }

            _ = reader.ReadByte();
            int inventoryTab = Math.Max(0, reader.ReadInt32());
            int slotIndex = Math.Max(0, reader.ReadInt32());
            RemovePrimaryEntry(_itcPurchasePacketEntries, out PacketCatalogEntry movedEntry);
            _itcPurchaseItemCount = Math.Max(0, _itcPurchasePacketEntries.Count);
            _noticeState =
                $"Purchase listing {(movedEntry?.ListingId ?? _itcNormalItemSelectedListingId).ToString(CultureInfo.InvariantCulture)} moved into inventory tab {inventoryTab.ToString(CultureInfo.InvariantCulture)} slot {slotIndex.ToString(CultureInfo.InvariantCulture)}.";
            message =
                $"CITC::OnMoveITCPurchaseItemLtoSDone moved {(movedEntry != null ? $"listing {movedEntry.ListingId.ToString(CultureInfo.InvariantCulture)}" : "the focused purchase row")} into tab {inventoryTab.ToString(CultureInfo.InvariantCulture)} slot {slotIndex.ToString(CultureInfo.InvariantCulture)}.";
            UpdateItcSelectionFromPrimaryList(_itcPurchasePacketEntries);
            return true;
        }

        private bool TryApplyItcWishMutation(
            string ownerName,
            bool addSelectedCatalogEntry,
            bool removeCurrentWishEntry,
            out string message)
        {
            PacketCatalogEntry focusedCatalogEntry = _itcPacketCatalogEntries.FirstOrDefault();
            if (addSelectedCatalogEntry && focusedCatalogEntry != null)
            {
                UpsertWishEntry(_itcWishPacketEntries, ClonePacketCatalogEntry(focusedCatalogEntry, "Wish"));
            }

            PacketCatalogEntry removedWishEntry = null;
            if (removeCurrentWishEntry)
            {
                RemovePrimaryEntry(_itcWishPacketEntries, out removedWishEntry);
            }

            _noticeState = ownerName switch
            {
                "CITC::OnSetZzimDone" or "CITC::OnRegisterWishItemDone" => focusedCatalogEntry != null
                    ? $"Wish owner now tracks listing {focusedCatalogEntry.ListingId.ToString(CultureInfo.InvariantCulture)}."
                    : "Wish owner accepted the focused catalog row.",
                _ => removedWishEntry != null
                    ? $"Wish owner removed listing {removedWishEntry.ListingId.ToString(CultureInfo.InvariantCulture)}."
                    : "Wish owner cleared the focused row."
            };

            message = $"{ownerName} left {_itcWishPacketEntries.Count.ToString(CultureInfo.InvariantCulture)} packet-authored wish row(s) active.";
            return true;
        }

        private bool TryApplyItcBuyCatalogItemDone(string ownerName, bool fromWishList, out string message)
        {
            List<PacketCatalogEntry> source = fromWishList ? _itcWishPacketEntries : _itcPacketCatalogEntries;
            RemovePrimaryEntry(source, out PacketCatalogEntry removedEntry);
            if (!fromWishList && removedEntry != null)
            {
                _itcCurrentCategoryItemCount = Math.Max(0, _itcCurrentCategoryItemCount - 1);
                _itcNormalItemEntryCount = Math.Max(0, _itcCurrentCategoryItemCount);
            }

            if (fromWishList && removedEntry != null)
            {
                RemoveEntryByListingId(_itcPacketCatalogEntries, removedEntry.ListingId);
            }

            _noticeState = removedEntry != null
                ? $"Purchased listing {removedEntry.ListingId.ToString(CultureInfo.InvariantCulture)} from the {(fromWishList ? "wish-sale" : "main list")} owner."
                : $"Purchased the focused {(fromWishList ? "wish-sale" : "main-list")} row.";
            message = $"{ownerName} completed for {(removedEntry != null ? $"listing {removedEntry.ListingId.ToString(CultureInfo.InvariantCulture)}" : "the focused row")} and refreshed the packet-owned {(fromWishList ? "wish-sale" : "catalog")} owner.";
            UpdateItcSelectionFromPrimaryList(fromWishList ? _itcWishPacketEntries : _itcPacketCatalogEntries);
            return true;
        }

        private bool TryApplyItcCancelWishNotification(byte[] payload, out string message)
        {
            message = null;
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            if (stream.Length < 1 + (sizeof(int) * 2))
            {
                return false;
            }

            _ = reader.ReadByte();
            int reason = reader.ReadInt32();
            int itemCount = reader.ReadInt32();
            _noticeState = $"Wish cancellation notice reported reason {reason.ToString(CultureInfo.InvariantCulture)} with {itemCount.ToString(CultureInfo.InvariantCulture)} item(s) still pending.";
            message = $"CITC::OnNotifyCancelWishResult surfaced reason {reason.ToString(CultureInfo.InvariantCulture)} for {itemCount.ToString(CultureInfo.InvariantCulture)} packet-owned item(s).";
            return true;
        }

        private bool TryReadItcItemEntry(BinaryReader reader, out PacketCatalogEntry entry)
        {
            entry = null;
            if (!TryReadItemAttachment(reader, out int itemId, out int quantity, out _))
            {
                return false;
            }

            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < (sizeof(int) * 3) + sizeof(long))
            {
                return false;
            }

            int listingId = reader.ReadInt32();
            int price = reader.ReadInt32();
            _ = reader.ReadInt32();
            if (!TryReadMapleString(reader, out _)
                || !TryReadMapleString(reader, out _))
            {
                return false;
            }

            stream.Position += sizeof(long);
            if (!TryReadMapleString(reader, out string userId)
                || !TryReadMapleString(reader, out string gameId)
                || !TryReadMapleString(reader, out string comment))
            {
                return false;
            }

            if (stream.Length - stream.Position < (sizeof(int) * 6) + sizeof(short))
            {
                return false;
            }

            int bidCount = reader.ReadInt32();
            int bidRange = reader.ReadInt32();
            int bidPrice = reader.ReadInt32();
            int minPrice = reader.ReadInt32();
            int maxPrice = reader.ReadInt32();
            int unitPrice = reader.ReadInt32();
            short processStatus = reader.ReadInt16();
            string seller = string.IsNullOrWhiteSpace(gameId) ? userId : gameId;
            if (string.IsNullOrWhiteSpace(seller))
            {
                seller = "ITC seller";
            }

            string title = $"Item {Math.Max(0, itemId).ToString(CultureInfo.InvariantCulture)} x{Math.Max(1, quantity).ToString(CultureInfo.InvariantCulture)}";
            string detail = string.IsNullOrWhiteSpace(comment)
                ? $"Listing {listingId.ToString(CultureInfo.InvariantCulture)} for {price.ToString("N0", CultureInfo.InvariantCulture)} mesos."
                : comment;
            string stateLabel = $"State {processStatus.ToString(CultureInfo.InvariantCulture)} / bids {bidCount.ToString(CultureInfo.InvariantCulture)}";
            if (bidPrice > 0)
            {
                stateLabel += $" @ {bidPrice.ToString("N0", CultureInfo.InvariantCulture)}";
            }
            else if (unitPrice > 0)
            {
                stateLabel += $" / unit {unitPrice.ToString("N0", CultureInfo.InvariantCulture)}";
            }

            if (bidRange > 0 || minPrice > 0 || maxPrice > 0)
            {
                detail += $" Range {Math.Max(0, minPrice).ToString("N0", CultureInfo.InvariantCulture)}-{Math.Max(0, maxPrice).ToString("N0", CultureInfo.InvariantCulture)} (+{Math.Max(0, bidRange).ToString("N0", CultureInfo.InvariantCulture)}).";
            }

            entry = new PacketCatalogEntry
            {
                Title = title,
                Detail = detail,
                Seller = seller,
                PriceLabel = price.ToString("N0", CultureInfo.InvariantCulture),
                StateLabel = stateLabel,
                ListingId = Math.Max(0, listingId),
                ItemId = Math.Max(0, itemId),
                Quantity = Math.Max(1, quantity),
                Price = Math.Max(0, price)
            };
            return true;
        }

        private static bool TryReadItemAttachment(BinaryReader reader, out int itemId, out int quantity, out string error)
        {
            itemId = 0;
            quantity = 0;
            error = null;

            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < sizeof(byte) + sizeof(int) + sizeof(byte) + sizeof(long))
            {
                error = "ITC item payload is too short to contain a GW_ItemSlotBase body.";
                return false;
            }

            byte slotType = reader.ReadByte();
            if (slotType is not 1 and not 2 and not 3)
            {
                error = $"Unsupported GW_ItemSlotBase type {slotType.ToString(CultureInfo.InvariantCulture)}.";
                return false;
            }

            itemId = reader.ReadInt32();
            bool hasCashSerialNumber = reader.ReadByte() != 0;
            if (hasCashSerialNumber)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    error = "ITC item payload ended before the cash serial number.";
                    return false;
                }

                _ = reader.ReadInt64();
            }

            if (stream.Length - stream.Position < sizeof(long))
            {
                error = "ITC item payload ended before the item serial number.";
                return false;
            }

            _ = reader.ReadInt64();

            quantity = 1;
            return slotType switch
            {
                1 => TryReadEquipBody(reader, hasCashSerialNumber, out error),
                2 => TryReadBundleBody(reader, itemId, out quantity, out error),
                3 => TryReadPetBody(reader, out error),
                _ => false
            };
        }

        private static bool TryReadEquipBody(BinaryReader reader, bool hasCashSerialNumber, out string error)
        {
            error = null;
            Stream stream = reader.BaseStream;
            const int equipStatsByteLength = (sizeof(byte) * 2) + (sizeof(short) * 15);
            if (stream.Length - stream.Position < equipStatsByteLength)
            {
                error = "ITC equip payload ended before the stat block.";
                return false;
            }

            stream.Position += equipStatsByteLength;
            if (!TryReadMapleString(reader, out _))
            {
                error = "ITC equip payload ended before the title string.";
                return false;
            }

            const int tailLength = sizeof(short) + (sizeof(byte) * 2) + (sizeof(int) * 3) + (sizeof(byte) * 2) + (sizeof(short) * 5);
            if (stream.Length - stream.Position < tailLength + sizeof(long) + sizeof(int))
            {
                error = "ITC equip payload ended before the tail block.";
                return false;
            }

            stream.Position += tailLength;
            if (!hasCashSerialNumber)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    error = "ITC equip payload ended before the non-cash serial number.";
                    return false;
                }

                stream.Position += sizeof(long);
            }

            stream.Position += sizeof(long) + sizeof(int);
            return true;
        }

        private static bool TryReadBundleBody(BinaryReader reader, int itemId, out int quantity, out string error)
        {
            quantity = 1;
            error = null;
            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < sizeof(ushort))
            {
                error = "ITC bundle payload ended before the quantity field.";
                return false;
            }

            quantity = Math.Max(1, (int)reader.ReadUInt16());
            if (!TryReadMapleString(reader, out _))
            {
                error = "ITC bundle payload ended before the title string.";
                return false;
            }

            if (stream.Length - stream.Position < sizeof(short))
            {
                error = "ITC bundle payload ended before the attribute field.";
                return false;
            }

            _ = reader.ReadInt16();
            if (itemId / 10000 is 207 or 233)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    error = "ITC bundle payload ended before the recharge serial number.";
                    return false;
                }

                _ = reader.ReadInt64();
            }

            return true;
        }

        private static bool TryReadPetBody(BinaryReader reader, out string error)
        {
            error = null;
            Stream stream = reader.BaseStream;
            const int petNameLength = 13;
            const int petTailLength = sizeof(byte) + sizeof(short) + sizeof(byte) + sizeof(long) + sizeof(short) + sizeof(ushort) + sizeof(int) + sizeof(short);
            if (stream.Length - stream.Position < petNameLength + petTailLength)
            {
                error = "ITC pet payload ended before the pet body finished.";
                return false;
            }

            stream.Position += petNameLength + petTailLength;
            return true;
        }

        private static bool TryReadMapleString(BinaryReader reader, out string value)
        {
            value = string.Empty;
            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < sizeof(short))
            {
                return false;
            }

            short length = reader.ReadInt16();
            if (length < 0 || stream.Length - stream.Position < length)
            {
                return false;
            }

            value = Encoding.ASCII.GetString(reader.ReadBytes(length)).TrimEnd('\0', ' ');
            return true;
        }

        private static string BuildPacketDecodeFailure(string ownerName, byte[] payload)
        {
            return $"{ownerName} reached the simulator, but the packet body could not be decoded from {payload.Length.ToString(CultureInfo.InvariantCulture)} byte(s).";
        }

        private static string SanitizePacketString(string value, string fallback)
        {
            string trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }

        private static void AppendStatusDetail(List<string> lines, string value, string suppressDefaultPrefix = null)
        {
            if (lines == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string trimmed = value.Trim();
            if (!string.IsNullOrWhiteSpace(suppressDefaultPrefix)
                && trimmed.StartsWith(suppressDefaultPrefix, StringComparison.Ordinal))
            {
                return;
            }

            if (!lines.Contains(trimmed, StringComparer.Ordinal))
            {
                lines.Add(trimmed);
            }
        }

        private static bool TryReadCashItemInfoPacketSnapshot(BinaryReader reader, out CashItemInfoPacketSnapshot snapshot)
        {
            snapshot = null;
            if (reader == null)
            {
                return false;
            }

            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < 55)
            {
                return false;
            }

            snapshot = new CashItemInfoPacketSnapshot
            {
                SerialNumber = reader.ReadInt64(),
                AccountId = reader.ReadInt32(),
                CharacterId = reader.ReadInt32(),
                ItemId = reader.ReadInt32(),
                CommodityId = reader.ReadInt32(),
                Quantity = Math.Max(1, (int)reader.ReadInt16()),
                BuyerCharacterId = ReadFixedPacketString(reader, 13),
                RawExpireFileTime = reader.ReadInt64(),
                PaybackRate = Math.Max(0, reader.ReadInt32()),
                DiscountRate = Math.Max(0, reader.ReadInt32())
            };
            return true;
        }

        private static bool TryReadGiftListPacketSnapshot(BinaryReader reader, out GiftListPacketSnapshot snapshot)
        {
            snapshot = null;
            if (reader == null)
            {
                return false;
            }

            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < 98)
            {
                return false;
            }

            snapshot = new GiftListPacketSnapshot
            {
                SerialNumber = reader.ReadInt64(),
                ItemId = Math.Max(0, reader.ReadInt32()),
                Sender = ReadFixedPacketString(reader, 13),
                Message = ReadFixedPacketString(reader, 73)
            };
            return true;
        }

        private static string ReadFixedPacketString(BinaryReader reader, int length)
        {
            if (reader == null || length <= 0)
            {
                return string.Empty;
            }

            byte[] bytes = reader.ReadBytes(length);
            int terminatorIndex = Array.IndexOf(bytes, (byte)0);
            int count = terminatorIndex >= 0 ? terminatorIndex : bytes.Length;
            return count <= 0
                ? string.Empty
                : Encoding.ASCII.GetString(bytes, 0, count).Trim();
        }

        private static PacketCatalogEntry BuildCashItemInfoPacketEntry(CashItemInfoPacketSnapshot snapshot, string titlePrefix, string seller, string stateLabel)
        {
            int itemId = Math.Max(0, snapshot?.ItemId ?? 0);
            int commodityId = Math.Max(0, snapshot?.CommodityId ?? 0);
            int quantity = Math.Max(1, snapshot?.Quantity ?? 1);
            string buyerCharacterId = SanitizePacketString(snapshot?.BuyerCharacterId, "Cash Shop");
            string title = ResolveCashStageItemTitle(itemId, commodityId, titlePrefix);
            string expireLabel = FormatPacketFileTime(snapshot?.RawExpireFileTime ?? 0);
            string detail = DescribeCashItemInfoPacketSnapshot(snapshot, includeSerialNumber: true);
            return new PacketCatalogEntry
            {
                Title = title,
                Detail = detail,
                Seller = string.IsNullOrWhiteSpace(seller) ? buyerCharacterId : seller,
                PriceLabel = commodityId > 0 ? $"SN {commodityId.ToString(CultureInfo.InvariantCulture)}" : $"Item {itemId.ToString(CultureInfo.InvariantCulture)}",
                StateLabel = string.IsNullOrWhiteSpace(expireLabel) ? stateLabel : $"{stateLabel} / {expireLabel}",
                SerialNumber = snapshot?.SerialNumber ?? 0,
                ListingId = commodityId,
                ItemId = itemId,
                Quantity = quantity
            };
        }

        private static string DescribeCashItemInfoPacketSnapshot(CashItemInfoPacketSnapshot snapshot, bool includeSerialNumber)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            List<string> parts = new();
            if (includeSerialNumber && snapshot.SerialNumber > 0)
            {
                parts.Add($"Serial {snapshot.SerialNumber.ToString(CultureInfo.InvariantCulture)}");
            }

            int itemId = Math.Max(0, snapshot.ItemId);
            int commodityId = Math.Max(0, snapshot.CommodityId);
            parts.Add(ResolveCashStageItemTitle(itemId, commodityId, "Item"));
            if (commodityId > 0)
            {
                parts.Add($"SN {commodityId.ToString(CultureInfo.InvariantCulture)}");
            }

            parts.Add($"Qty {Math.Max(1, snapshot.Quantity).ToString(CultureInfo.InvariantCulture)}");
            string buyer = SanitizePacketString(snapshot.BuyerCharacterId, string.Empty);
            if (!string.IsNullOrWhiteSpace(buyer))
            {
                parts.Add($"Buyer {buyer}");
            }

            string expireLabel = FormatPacketFileTime(snapshot.RawExpireFileTime);
            if (!string.IsNullOrWhiteSpace(expireLabel))
            {
                parts.Add(expireLabel);
            }

            if (snapshot.DiscountRate > 0)
            {
                parts.Add($"Discount {snapshot.DiscountRate.ToString(CultureInfo.InvariantCulture)}%");
            }

            if (snapshot.PaybackRate > 0)
            {
                parts.Add($"Payback {snapshot.PaybackRate.ToString(CultureInfo.InvariantCulture)}%");
            }

            return string.Join(" / ", parts);
        }

        private static string ResolveCashStageItemTitle(int itemId, int fallbackId, string fallbackPrefix)
        {
            int resolvedId = itemId > 0 ? itemId : Math.Max(0, fallbackId);
            if (resolvedId > 0 && InventoryItemMetadataResolver.TryResolveItemName(resolvedId, out string itemName) && !string.IsNullOrWhiteSpace(itemName))
            {
                return itemName.Trim();
            }

            if (resolvedId > 0)
            {
                return $"{fallbackPrefix} {resolvedId.ToString(CultureInfo.InvariantCulture)}";
            }

            return fallbackPrefix;
        }

        private static string ResolveCashStageGiftRowTitle(int itemId, int rowNumber, string fallbackPrefix)
        {
            if (itemId > 0 && InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName) && !string.IsNullOrWhiteSpace(itemName))
            {
                return itemName.Trim();
            }

            return itemId > 0
                ? $"Item {itemId.ToString(CultureInfo.InvariantCulture)}"
                : $"{fallbackPrefix} {rowNumber.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string FormatPacketFileTime(long rawFileTime)
        {
            if (rawFileTime <= 0 || rawFileTime == long.MaxValue)
            {
                return string.Empty;
            }

            try
            {
                DateTime timestamp = DateTime.FromFileTimeUtc(rawFileTime);
                if (timestamp.Year < 2000 || timestamp.Year > 2100)
                {
                    return string.Empty;
                }

                return $"Expire {timestamp:yyyy-MM-dd}";
            }
            catch (ArgumentOutOfRangeException)
            {
                return string.Empty;
            }
        }

        private string ResolveCashPurchaseRecordStateLabel(int commoditySerialNumber)
        {
            if (commoditySerialNumber > 0 && _cashPurchaseRecordStates.TryGetValue(commoditySerialNumber, out bool purchased))
            {
                return purchased ? "Purchased" : "Wish";
            }

            return "Wish";
        }

        private void ApplyCashPurchaseRecordStateToCatalogEntries(int commoditySerialNumber, bool purchased)
        {
            foreach (PacketCatalogEntry entry in _cashPacketCatalogEntries)
            {
                if (commoditySerialNumber <= 0 && entry.ListingId <= 0)
                {
                    continue;
                }

                if (commoditySerialNumber > 0 && entry.ListingId != commoditySerialNumber)
                {
                    continue;
                }

                entry.StateLabel = purchased ? "Purchased" : "Wish";
            }
        }

        private void ReplaceCashPacketCatalogEntries(string paneLabel, string browseModeLabel, IReadOnlyList<PacketCatalogEntry> entries)
        {
            _cashPacketCatalogEntries.Clear();
            if (entries != null && entries.Count > 0)
            {
                _cashPacketCatalogEntries.AddRange(entries);
            }

            _cashPacketPaneLabel = string.IsNullOrWhiteSpace(paneLabel) ? "Packet wishlist" : paneLabel;
            _cashPacketBrowseModeLabel = string.IsNullOrWhiteSpace(browseModeLabel) ? "Wish" : browseModeLabel;
        }

        private void AppendCashPacketCatalogEntry(string paneLabel, string browseModeLabel, PacketCatalogEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (!string.Equals(_cashPacketPaneLabel, paneLabel, StringComparison.Ordinal)
                || !string.Equals(_cashPacketBrowseModeLabel, browseModeLabel, StringComparison.Ordinal))
            {
                _cashPacketCatalogEntries.Clear();
            }

            _cashPacketPaneLabel = string.IsNullOrWhiteSpace(paneLabel) ? "Packet wishlist" : paneLabel;
            _cashPacketBrowseModeLabel = string.IsNullOrWhiteSpace(browseModeLabel) ? "Wish" : browseModeLabel;
            _cashPacketCatalogEntries.Insert(0, entry);
        }

        private static PacketCatalogEntry ClonePacketCatalogEntry(PacketCatalogEntry source, string appendedStateLabel)
        {
            if (source == null)
            {
                return null;
            }

            string stateLabel = string.IsNullOrWhiteSpace(source.StateLabel)
                ? appendedStateLabel
                : source.StateLabel.Contains(appendedStateLabel, StringComparison.Ordinal)
                    ? source.StateLabel
                    : $"{source.StateLabel} / {appendedStateLabel}";
            return new PacketCatalogEntry
            {
                Title = source.Title,
                Detail = source.Detail,
                Seller = source.Seller,
                PriceLabel = source.PriceLabel,
                StateLabel = stateLabel,
                ListingId = source.ListingId,
                ItemId = source.ItemId,
                Quantity = source.Quantity,
                Price = source.Price
            };
        }

        private static void UpsertWishEntry(List<PacketCatalogEntry> target, PacketCatalogEntry entry)
        {
            if (target == null || entry == null)
            {
                return;
            }

            int existingIndex = target.FindIndex(candidate => candidate.ListingId > 0 && candidate.ListingId == entry.ListingId);
            if (existingIndex >= 0)
            {
                target[existingIndex] = entry;
                return;
            }

            target.Insert(0, entry);
        }

        private static void RemovePrimaryEntry(List<PacketCatalogEntry> target, out PacketCatalogEntry removedEntry)
        {
            removedEntry = null;
            if (target == null || target.Count == 0)
            {
                return;
            }

            removedEntry = target[0];
            target.RemoveAt(0);
        }

        private static void RemoveEntryByListingId(List<PacketCatalogEntry> target, int listingId)
        {
            if (target == null || listingId <= 0)
            {
                return;
            }

            int index = target.FindIndex(candidate => candidate.ListingId == listingId);
            if (index >= 0)
            {
                target.RemoveAt(index);
            }
        }

        private void UpdateItcSelectionFromPrimaryList(IReadOnlyList<PacketCatalogEntry> entries)
        {
            PacketCatalogEntry selectedEntry = entries?.FirstOrDefault();
            _itcNormalItemSelectedListingId = selectedEntry?.ListingId ?? 0;
            _itcNormalItemSelectedPrice = selectedEntry?.Price ?? 0;
        }

        private string BuildItcFailureMessage(byte[] payload, string ownerName)
        {
            int reason = payload.Length >= 2 ? payload[1] : -1;
            _noticeState = reason >= 0
                ? $"{ownerName} failed with reason {reason.ToString(CultureInfo.InvariantCulture)}."
                : $"{ownerName} failed before a reason byte could be decoded.";
            return _noticeState;
        }

        private string BuildItcSimpleResult(string ownerName, string fallbackMessage, byte[] payload)
        {
            _noticeState = fallbackMessage;
            return $"{ownerName} completed with {payload.Length.ToString(CultureInfo.InvariantCulture)} byte(s). {fallbackMessage}";
        }

        private void RecordPacketRoute(int packetType, string label, string detail, int tickCount)
        {
            _lastPacketType = packetType;
            _lastPacketTick = tickCount;

            if (_packetRoutes.TryGetValue(packetType, out PacketRouteState existing))
            {
                existing.HitCount++;
                _packetRouteOrder.Remove(packetType);
                _packetRouteOrder.Add(packetType);
                _packetRoutes[packetType] = new PacketRouteState(packetType, label, detail, tickCount)
                {
                    HitCount = existing.HitCount
                };
                return;
            }

            _packetRoutes[packetType] = new PacketRouteState(packetType, label, detail, tickCount);
            _packetRouteOrder.Add(packetType);
        }

        private void SelectCashShopBackdrop()
        {
            int variantIndex = ResolveCashShopBackdropVariant();
            if (_cashShopBackdropVariants.TryGetValue(variantIndex, out Texture2D selected))
            {
                _selectedBackdrop = selected;
            }

            _selectedBackdropLabel = variantIndex switch
            {
                1 => "warrior-themed preview",
                2 => "aran-themed preview",
                3 => "evan-themed preview",
                4 => "resistance-themed preview",
                5 => "dual-blade-themed preview",
                _ => "default preview"
            };
        }

        private int ResolveCashShopBackdropVariant()
        {
            if (_build == null)
            {
                return 0;
            }

            int job = Math.Abs(_build.Job);
            int jobBranch = job / 100;
            int jobFamily = job / 1000;
            if (jobFamily == 1)
            {
                return 1;
            }

            if (jobBranch == 21 || job == 2000)
            {
                return 2;
            }

            if (jobBranch == 22 || job == 2001)
            {
                return 3;
            }

            if (jobFamily == 3)
            {
                return 4;
            }

            if (jobFamily == 0 && _build.SubJob == 1)
            {
                return 5;
            }

            return 0;
        }

        private static void TryReadCashBalances(byte[] payload, out long cash, out long maplePoint, out long prepaid)
        {
            cash = 0;
            maplePoint = 0;
            prepaid = 0;

            if (payload == null || payload.Length < 4)
            {
                return;
            }

            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);

            if (stream.Length >= 4)
            {
                cash = reader.ReadInt32();
            }

            if (stream.Position <= stream.Length - 4)
            {
                maplePoint = reader.ReadInt32();
            }

            if (stream.Position <= stream.Length - 4)
            {
                prepaid = reader.ReadInt32();
            }
        }

        private static bool TryReadInt32(byte[] payload, out int value)
        {
            value = 0;
            if (payload == null || payload.Length < sizeof(int))
            {
                return false;
            }

            value = BitConverter.ToInt32(payload, 0);
            return true;
        }

        private static bool TryReadInt32At(byte[] payload, int offset, out int value)
        {
            value = 0;
            if (payload == null || offset < 0 || payload.Length < offset + sizeof(int))
            {
                return false;
            }

            value = BitConverter.ToInt32(payload, offset);
            return true;
        }

        private static bool TryReadUtf8Text(byte[] payload, out string text)
        {
            text = null;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            try
            {
                text = Encoding.UTF8.GetString(payload).TrimEnd('\0');
                return !string.IsNullOrWhiteSpace(text);
            }
            catch
            {
                return false;
            }
        }

        private static string GetCashShopPacketLabel(int packetType)
        {
            return packetType switch
            {
                382 => "ChargeParam",
                383 => "QueryCash",
                384 => "CashItem",
                385 => "PurchaseExp",
                386 => "GiftMate",
                387 => "DuplicateId",
                388 => "NameChange",
                390 => "TransferWorld",
                391 => "GachaponStamp",
                392 => "GachaponResult",
                393 => "GachaponResult",
                395 => "OneADay",
                396 => "FreeItemNotice",
                _ => $"Packet {packetType}"
            };
        }

        private static string GetItcPacketLabel(int packetType)
        {
            return packetType switch
            {
                410 => "ChargeParam",
                411 => "QueryCash",
                412 => "NormalItem",
                _ => $"Packet {packetType}"
            };
        }

        private static string GetCashItemResultSubtypeLabel(int subtype)
        {
            return subtype switch
            {
                84 => "CCashShop::OnCashItemResLimitGoodsCountChanged",
                88 => "CCashShop::OnCashItemResLoadLockerDone",
                89 => "CCashShop::OnCashItemResLoadLockerFailed",
                90 => "CCashShop::OnCashItemResLoadGiftDone",
                91 => "CCashShop::OnCashItemResLoadGiftFailed",
                92 => "CCashShop::OnCashItemResLoadWishDone",
                93 => "CCashShop::OnCashItemResLoadWishFailed",
                98 => "CCashShop::OnCashItemResSetWishDone",
                99 => "CCashShop::OnCashItemResSetWishFailed",
                100 => "CCashShop::OnCashItemResBuyDone",
                101 => "CCashShop::OnCashItemResBuyFailed",
                102 => "CCashShop::OnCashItemResUseCouponDone",
                104 => "CCashShop::OnCashItemResGiftCouponDone",
                105 => "CCashShop::OnCashItemResUseCouponFailed",
                107 => "CCashShop::OnCashItemResGiftDone",
                108 => "CCashShop::OnCashItemResGiftFailed",
                109 => "CCashShop::OnCashItemResIncSlotCountDone",
                110 => "CCashShop::OnCashItemResIncSlotCountFailed",
                111 => "CCashShop::OnCashItemResIncTrunkCountDone",
                112 => "CCashShop::OnCashItemResIncTrunkCountFailed",
                113 => "CCashShop::OnCashItemResIncCharacterSlotCountDone",
                114 => "CCashShop::OnCashItemResIncCharacterSlotCountFailed",
                115 => "CCashShop::OnCashItemResIncBuyCharacterCountDone",
                116 => "CCashShop::OnCashItemResIncBuyCharacterCountFailed",
                117 => "CCashShop::OnCashItemResEnableEquipSlotExtDone",
                118 => "CCashShop::OnCashItemResEnableEquipSlotExtFailed",
                119 => "CCashShop::OnCashItemResMoveLtoSDone",
                120 => "CCashShop::OnCashItemResMoveLtoSFailed",
                121 => "CCashShop::OnCashItemResMoveStoLDone",
                122 => "CCashShop::OnCashItemResMoveStoLFailed",
                123 => "CCashShop::OnCashItemResDestroyDone",
                124 => "CCashShop::OnCashItemResDestroyFailed",
                125 => "CCashShop::OnCashItemResExpireDone",
                -106 => "CCashShop::OnCashItemResRebateDone",
                -105 => "CCashShop::OnCashItemResRebateFailed",
                -104 => "CCashShop::OnCashItemResCoupleDone",
                -103 => "CCashShop::OnCashItemResCoupleFailed",
                -102 => "CCashShop::OnCashItemResBuyPackageDone",
                -101 => "CCashShop::OnCashItemResBuyPackageFailed",
                -100 => "CCashShop::OnCashItemResGiftPackageDone",
                -99 => "CCashShop::OnCashItemResGiftPackageFailed",
                -98 => "CCashShop::OnCashItemResBuyNormalDone",
                -97 => "CCashShop::OnCashItemResBuyNormalFailed",
                -94 => "CCashShop::OnCashItemResFriendShipDone",
                -93 => "CCashShop::OnCashItemResFriendShipFailed",
                -86 => "CCashShop::OnCashItemResFreeCashItemDone",
                -81 => "CCashShop::OnCashItemResPurchaseRecord",
                -80 => "CCashShop::OnCashItemResPurchaseRecordFailed",
                -77 => "CCashShop::OnCashItemNameChangeResBuyDone",
                -75 => "CCashShop::OnCashItemResTransferWorldDone",
                -74 => "CCashShop::OnCashItemResTransferWorldFailed",
                -73 => "CCashShop::OnCashItemResCashGachaponOpenDone",
                -72 => "CCashShop::OnCashItemResCashGachaponOpenFailed",
                -71 => "CCashShop::OnCashItemResCashGachaponCopyDone",
                -70 => "CCashShop::OnCashItemResCashGachaponCopyFailed",
                -69 => "CCashShop::OnCashItemResChangeMaplePointDone",
                -68 => "CCashShop::OnCashItemResChangeMaplePointFailed",
                _ => $"Cash item subtype {subtype.ToString(CultureInfo.InvariantCulture)}"
            };
        }

        private static string GetItcNormalItemSubtypeLabel(int subtype)
        {
            return subtype switch
            {
                21 => "CITC::OnGetITCListDone",
                22 => "CITC::OnGetITCListFailed",
                23 => "CITC::OnGetSearchITCListDone",
                24 => "CITC::OnGetSearchITCListFailed",
                29 => "CITC::OnNormalItemResRegisterSaleEntryDone",
                30 => "CITC::OnNormalItemResRegisterSaleEntryFailed",
                31 => "CITC::OnSaleCurrentItemToWishDone",
                32 => "CITC::OnSaleCurrentItemToWishFailed",
                33 => "CITC::OnGetUserPurchaseItemDone",
                34 => "CITC::OnGetUserPurchaseItemFailed",
                35 => "CITC::OnGetUserSaleItemDone",
                36 => "CITC::OnGetUserSaleItemFailed",
                37 => "CITC::OnCancelSaleItemDone",
                38 => "CITC::OnCancelSaleItemFailed",
                39 => "CITC::OnMoveITCPurchaseItemLtoSDone",
                40 => "CITC::OnMoveITCPurchaseItemLtoSFailed",
                41 => "CITC::OnSetZzimDone",
                42 => "CITC::OnSetZzimFailed",
                43 => "CITC::OnDeleteZzimDone",
                44 => "CITC::OnDeleteZzimFailed",
                45 => "CITC::OnLoadWishSaleListDone",
                46 => "CITC::OnLoadWishSaleListFailed",
                47 => "CITC::OnBuyWishDone",
                48 => "CITC::OnBuyWishFailed",
                49 => "CITC::OnCancelWishDone",
                50 => "CITC::OnCancelWishFailed",
                51 => "CITC::OnBuyItemDone",
                52 => "CITC::OnBuyItemFailed",
                53 => "CITC::OnBuyZzimItemDone",
                54 => "CITC::OnBuyZzimItemFailed",
                55 => "CITC::OnRegisterWishItemDone",
                56 => "CITC::OnRegisterWishItemFailed",
                60 => "CITC::OnBidAuctionFailed",
                61 => "CITC::OnNotifyCancelWishResult",
                62 => "CITC::OnSuccessBidInfoResult",
                _ => $"ITC normal-item subtype {subtype.ToString(CultureInfo.InvariantCulture)}"
            };
        }

        private Rectangle OffsetBounds(Rectangle bounds)
        {
            return new Rectangle(Position.X + bounds.X, Position.Y + bounds.Y, bounds.Width, bounds.Height);
        }

        private void DrawRect(SpriteBatch sprite, Rectangle bounds, Color color)
        {
            sprite.Draw(_pixelTexture, bounds, color);
        }

        private void DrawOutline(SpriteBatch sprite, Rectangle bounds, Color color)
        {
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
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
    }
}
