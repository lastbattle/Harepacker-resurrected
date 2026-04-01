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

        public override void SetFont(SpriteFont font)
        {
            _font = font;
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
                $"Meso {_inventory.GetMesoCount().ToString("N0", CultureInfo.InvariantCulture)}."
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
            return new[]
            {
                _pendingCommoditySerialNumber > 0
                    ? $"GoToCommoditySN {_pendingCommoditySerialNumber} is waiting on the best-items owner."
                    : "No commodity serial is waiting on best-items.",
                $"Wishlist count {_wishlistCount}/10."
            };
        }

        private IReadOnlyList<string> BuildStatusPane()
        {
            string balanceLine = $"NX {_nexonCash.ToString("N0", CultureInfo.InvariantCulture)}  MP {_maplePoint.ToString("N0", CultureInfo.InvariantCulture)}  Prepaid {_prepaidCash.ToString("N0", CultureInfo.InvariantCulture)}";
            if (_chargeParam != 0)
            {
                balanceLine += $"  Charge {_chargeParam.ToString(CultureInfo.InvariantCulture)}";
            }

            return new[]
            {
                balanceLine,
                _statusMessage
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
            return new[]
            {
                "Sale owner is split from purchase owner.",
                _noticeState
            };
        }

        private IReadOnlyList<string> BuildPurchasePane()
        {
            return new[]
            {
                "Purchase owner remains separate from the main list.",
                _statusMessage
            };
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
                    _hasPendingMigration = false;
                    _navigationState = "CCSWnd_List and CCSWnd_Best own the active commodity view.";
                    detail = _pendingCommoditySerialNumber > 0
                        ? $"Cash item result resumed around commodity SN {_pendingCommoditySerialNumber}."
                        : "Cash item result reached the dedicated stage owner.";
                    break;
                case 385:
                    detail = "Purchase-exp update routed through Cash Shop packet ownership.";
                    break;
                case 386:
                    detail = "Gift-mate result stayed inside Cash Shop packet ownership.";
                    break;
                case 387:
                    detail = "Duplicate-id result stayed inside Cash Shop packet ownership.";
                    break;
                case 388:
                    detail = "Name-change result stayed inside Cash Shop packet ownership.";
                    break;
                case 390:
                    detail = "Transfer-world result stayed inside Cash Shop packet ownership.";
                    break;
                case 391:
                    detail = "CashShop gachapon stamp result reached the dedicated stage.";
                    break;
                case 392:
                case 393:
                    detail = "CashShop gachapon result reached the dedicated stage.";
                    break;
                case 395:
                    detail = "One-a-day result reached the dedicated stage.";
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
                412 => "Normal-item result remained inside the ITC stage owner.",
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
