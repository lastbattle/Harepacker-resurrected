using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    public enum SocialRoomKind
    {
        MiniRoom,
        PersonalShop,
        EntrustedShop,
        TradingRoom
    }

    public enum SocialRoomOccupantRole
    {
        Owner,
        Guest,
        Visitor,
        Merchant,
        Buyer,
        Trader
    }

    public enum SocialRoomPacketType
    {
        OpenRoom,
        CloseRoom,
        ToggleReady,
        StartSession,
        CycleMode,
        SetWager,
        SettleWager,
        AddVisitor,
        ToggleBlacklist,
        ListItem,
        AutoListItem,
        BuyItem,
        ArrangeItems,
        ClaimMesos,
        ToggleLedgerMode,
        OfferTradeItem,
        OfferTradeMeso,
        LockTrade,
        CompleteTrade,
        ResetTrade
    }

    public sealed class SocialRoomOccupant
    {
        public SocialRoomOccupant(string name, SocialRoomOccupantRole role, string detail, bool isReady = false)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
            Role = role;
            Detail = detail ?? string.Empty;
            IsReady = isReady;
        }

        public string Name { get; private set; }
        public SocialRoomOccupantRole Role { get; private set; }
        public string Detail { get; private set; }
        public bool IsReady { get; private set; }

        public void Update(string detail, bool isReady)
        {
            Detail = detail ?? string.Empty;
            IsReady = isReady;
        }

        public void Update(string name, SocialRoomOccupantRole role, string detail, bool isReady)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
            Role = role;
            Detail = detail ?? string.Empty;
            IsReady = isReady;
        }
    }

    public sealed class SocialRoomItemEntry
    {
        public SocialRoomItemEntry(
            string ownerName,
            string itemName,
            int quantity,
            int mesoAmount,
            string detail,
            bool isLocked = false,
            bool isClaimed = false)
        {
            OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "Unknown" : ownerName;
            ItemName = string.IsNullOrWhiteSpace(itemName) ? "Unknown item" : itemName;
            Quantity = Math.Max(1, quantity);
            MesoAmount = Math.Max(0, mesoAmount);
            Detail = detail ?? string.Empty;
            IsLocked = isLocked;
            IsClaimed = isClaimed;
        }

        public string OwnerName { get; }
        public string ItemName { get; }
        public int Quantity { get; private set; }
        public int MesoAmount { get; private set; }
        public string Detail { get; private set; }
        public bool IsLocked { get; private set; }
        public bool IsClaimed { get; private set; }

        public void Update(string detail, int quantity, int mesoAmount, bool isLocked, bool isClaimed)
        {
            Detail = detail ?? string.Empty;
            Quantity = Math.Max(1, quantity);
            MesoAmount = Math.Max(0, mesoAmount);
            IsLocked = isLocked;
            IsClaimed = isClaimed;
        }
    }

    public enum SocialRoomChatTone
    {
        Neutral,
        System,
        LocalSpeaker,
        RemoteSpeaker,
        Warning
    }

    public sealed class SocialRoomChatEntry
    {
        public SocialRoomChatEntry(string text, SocialRoomChatTone tone)
        {
            Text = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            Tone = tone;
        }

        public string Text { get; }
        public SocialRoomChatTone Tone { get; }
    }

    public sealed class SocialRoomRuntime
    {
        private sealed class InventoryEscrowEntry
        {
            public InventoryEscrowEntry(
                SocialRoomItemEntry entry,
                InventoryType inventoryType,
                InventorySlotData slotData,
                bool returnOnReset,
                bool returnOnClose)
            {
                Entry = entry;
                InventoryType = inventoryType;
                SlotData = slotData;
                ReturnOnReset = returnOnReset;
                ReturnOnClose = returnOnClose;
            }

            public SocialRoomItemEntry Entry { get; }
            public InventoryType InventoryType { get; }
            public InventorySlotData SlotData { get; }
            public bool ReturnOnReset { get; }
            public bool ReturnOnClose { get; }
        }

        private readonly List<SocialRoomOccupant> _occupants;
        private readonly List<SocialRoomItemEntry> _items;
        private readonly List<string> _notes;
        private readonly List<SocialRoomChatEntry> _chatEntries;
        private readonly List<string> _savedVisitors;
        private readonly List<string> _blockedVisitors;
        private readonly Queue<string> _pendingVisitorNames;
        private readonly List<InventoryEscrowEntry> _inventoryEscrow;
        private readonly List<SocialRoomItemEntry> _defaultItems;
        private readonly List<SocialRoomOccupant> _defaultOccupants;
        private int _miniRoomModeIndex;
        private int _miniRoomWagerAmount;
        private int _tradeLocalOfferMeso;
        private int _tradeRemoteOfferMeso;
        private bool _inventoryBackedRows;
        private Action _miniRoomToggleReadyHandler;
        private Action _miniRoomStartHandler;
        private Action _miniRoomModeHandler;
        private IInventoryRuntime _inventoryRuntime;

        private SocialRoomRuntime(
            SocialRoomKind kind,
            string roomTitle,
            string ownerName,
            int capacity,
            int mesoAmount,
            IEnumerable<SocialRoomOccupant> occupants,
            IEnumerable<SocialRoomItemEntry> items,
            IEnumerable<string> notes,
            IEnumerable<SocialRoomChatEntry> chatEntries,
            string statusMessage,
            string roomState,
            string modeName)
        {
            Kind = kind;
            RoomTitle = roomTitle ?? string.Empty;
            OwnerName = ownerName ?? string.Empty;
            Capacity = Math.Max(0, capacity);
            MesoAmount = Math.Max(0, mesoAmount);
            _occupants = occupants?.ToList() ?? new List<SocialRoomOccupant>();
            _items = items?.ToList() ?? new List<SocialRoomItemEntry>();
            _notes = notes?.Where(note => !string.IsNullOrWhiteSpace(note)).ToList() ?? new List<string>();
            _chatEntries = chatEntries?.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Text)).ToList() ?? new List<SocialRoomChatEntry>();
            _savedVisitors = new List<string>();
            _blockedVisitors = new List<string>();
            _pendingVisitorNames = new Queue<string>(new[] { "Rondo", "Rin", "Maya", "Pia", "Targa", "Rowen" });
            _inventoryEscrow = new List<InventoryEscrowEntry>();
            _defaultItems = items?.Select(item => new SocialRoomItemEntry(item.OwnerName, item.ItemName, item.Quantity, item.MesoAmount, item.Detail, item.IsLocked, item.IsClaimed)).ToList()
                ?? new List<SocialRoomItemEntry>();
            _defaultOccupants = occupants?.Select(occupant => new SocialRoomOccupant(occupant.Name, occupant.Role, occupant.Detail, occupant.IsReady)).ToList()
                ?? new List<SocialRoomOccupant>();
            _tradeRemoteOfferMeso = kind == SocialRoomKind.TradingRoom ? 75000 : 0;
            StatusMessage = statusMessage ?? string.Empty;
            RoomState = roomState ?? string.Empty;
            ModeName = modeName ?? string.Empty;
        }

        public SocialRoomKind Kind { get; }
        public string RoomTitle { get; private set; }
        public string OwnerName { get; private set; }
        public int Capacity { get; }
        public int MesoAmount { get; private set; }
        public string StatusMessage { get; private set; }
        public string RoomState { get; private set; }
        public string ModeName { get; private set; }
        public IReadOnlyList<SocialRoomOccupant> Occupants => _occupants;
        public IReadOnlyList<SocialRoomItemEntry> Items => _items;
        public IReadOnlyList<string> Notes => _notes;
        public IReadOnlyList<SocialRoomChatEntry> ChatEntries => _chatEntries;

        public void BindInventory(IInventoryRuntime inventoryRuntime)
        {
            _inventoryRuntime = inventoryRuntime;
        }

        public void BindMiniRoomHandlers(Action toggleReady, Action start, Action cycleMode)
        {
            _miniRoomToggleReadyHandler = toggleReady;
            _miniRoomStartHandler = start;
            _miniRoomModeHandler = cycleMode;
        }

        public string DescribeStatus()
        {
            return Kind switch
            {
                SocialRoomKind.MiniRoom => $"{RoomTitle}: state={RoomState}, mode={ModeName}, wager={_miniRoomWagerAmount:N0}, occupants={_occupants.Count}/{Capacity}",
                SocialRoomKind.PersonalShop => $"{RoomTitle}: state={RoomState}, listed={_items.Count}, savedVisitors={_savedVisitors.Count}, blacklist={_blockedVisitors.Count}, ledger={MesoAmount:N0}",
                SocialRoomKind.EntrustedShop => $"{RoomTitle}: state={RoomState}, listed={_items.Count}, ledger={MesoAmount:N0}",
                SocialRoomKind.TradingRoom => $"{RoomTitle}: state={RoomState}, localMeso={MesoAmount:N0}, remoteMeso={_tradeRemoteOfferMeso:N0}, escrowRows={_inventoryEscrow.Count}",
                _ => RoomState
            };
        }

        public bool TryDispatchPacket(
            SocialRoomPacketType packetType,
            out string message,
            int itemId = 0,
            int quantity = 1,
            int mesoAmount = 0,
            int itemIndex = -1,
            string actorName = null)
        {
            string resolvedMessage = null;
            switch (packetType)
            {
                case SocialRoomPacketType.ToggleReady:
                    ToggleMiniRoomGuestReady();
                    resolvedMessage = StatusMessage;
                    message = resolvedMessage;
                    return true;
                case SocialRoomPacketType.StartSession:
                    StartMiniRoomSession();
                    resolvedMessage = StatusMessage;
                    message = resolvedMessage;
                    return true;
                case SocialRoomPacketType.CycleMode:
                    CycleMiniRoomMode();
                    resolvedMessage = StatusMessage;
                    message = resolvedMessage;
                    return true;
                case SocialRoomPacketType.SetWager:
                    return TrySetMiniRoomWager(mesoAmount, out message);
                case SocialRoomPacketType.SettleWager:
                    return TrySettleMiniRoomWager(actorName, out message);
                case SocialRoomPacketType.AddVisitor:
                    return AddPersonalShopVisitor(actorName, out message);
                case SocialRoomPacketType.ToggleBlacklist:
                    return TogglePersonalShopBlacklist(actorName, out message);
                case SocialRoomPacketType.ListItem:
                    if (Kind == SocialRoomKind.PersonalShop)
                    {
                        return TryListPersonalShopItem(itemId, quantity, mesoAmount, out message);
                    }

                    if (Kind == SocialRoomKind.EntrustedShop)
                    {
                        return TryListEntrustedShopItem(itemId, quantity, mesoAmount, out message);
                    }

                    if (Kind == SocialRoomKind.TradingRoom)
                    {
                        return TryOfferTradeItem(itemId, quantity, out message);
                    }

                    break;
                case SocialRoomPacketType.AutoListItem:
                    if (Kind == SocialRoomKind.PersonalShop)
                    {
                        return TryAutoListPersonalShopItem(out message);
                    }

                    if (Kind == SocialRoomKind.EntrustedShop)
                    {
                        return TryAutoListEntrustedShopItem(out message);
                    }

                    break;
                case SocialRoomPacketType.BuyItem:
                    return TryBuyPersonalShopItem(itemIndex, actorName, out message);
                case SocialRoomPacketType.ArrangeItems:
                    if (Kind == SocialRoomKind.PersonalShop)
                    {
                        ArrangePersonalShopInventory();
                        resolvedMessage = StatusMessage;
                        message = resolvedMessage;
                        return true;
                    }

                    if (Kind == SocialRoomKind.EntrustedShop)
                    {
                        ArrangeEntrustedShop();
                        resolvedMessage = StatusMessage;
                        message = resolvedMessage;
                        return true;
                    }

                    break;
                case SocialRoomPacketType.ClaimMesos:
                    if (Kind == SocialRoomKind.PersonalShop)
                    {
                        ClaimPersonalShopEarnings();
                        resolvedMessage = StatusMessage;
                        message = resolvedMessage;
                        return true;
                    }

                    if (Kind == SocialRoomKind.EntrustedShop)
                    {
                        ClaimEntrustedShopEarnings();
                        resolvedMessage = StatusMessage;
                        message = resolvedMessage;
                        return true;
                    }

                    break;
                case SocialRoomPacketType.ToggleLedgerMode:
                    ToggleEntrustedLedgerMode();
                    resolvedMessage = StatusMessage;
                    message = resolvedMessage;
                    return true;
                case SocialRoomPacketType.OfferTradeItem:
                    return TryOfferTradeItem(itemId, quantity, out message);
                case SocialRoomPacketType.OfferTradeMeso:
                    return TryOfferTradeMeso(mesoAmount, out message);
                case SocialRoomPacketType.LockTrade:
                    ConfirmTradeLock();
                    resolvedMessage = StatusMessage;
                    message = resolvedMessage;
                    return true;
                case SocialRoomPacketType.CompleteTrade:
                    return TryCompleteTrade(out message);
                case SocialRoomPacketType.ResetTrade:
                    ResetTrade();
                    resolvedMessage = StatusMessage;
                    message = resolvedMessage;
                    return true;
                case SocialRoomPacketType.CloseRoom:
                    if (Kind == SocialRoomKind.PersonalShop)
                    {
                        return ClosePersonalShop(out message);
                    }

                    resolvedMessage = "Close-room packets are only modeled for the personal shop shell.";
                    message = resolvedMessage;
                    return false;
            }

            resolvedMessage = packetType switch
            {
                SocialRoomPacketType.ListItem => "This room does not accept inventory item packets.",
                SocialRoomPacketType.AutoListItem => "This room does not support auto-list packets.",
                SocialRoomPacketType.ArrangeItems => "Arrange-items packets are not modeled for this room.",
                SocialRoomPacketType.ClaimMesos => "Claim packets are not modeled for this room.",
                _ => "Unsupported social-room packet."
            };
            message = resolvedMessage;
            return false;
        }

        public void SyncMiniRoomMatchCards(
            string roomTitle,
            string ownerName,
            string guestName,
            bool ownerReady,
            bool guestReady,
            int ownerScore,
            int guestScore,
            int currentTurnIndex,
            string statusMessage,
            string roomState,
            string ownerDetail = null,
            string guestDetail = null,
            IReadOnlyList<SocialRoomOccupant> extraOccupants = null)
        {
            RoomTitle = string.IsNullOrWhiteSpace(roomTitle) ? "Mini Room" : roomTitle.Trim();
            OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "Player" : ownerName.Trim();
            ModeName = "Match Cards";
            StatusMessage = statusMessage ?? string.Empty;
            RoomState = roomState ?? string.Empty;
            MesoAmount = _miniRoomWagerAmount > 0 ? _miniRoomWagerAmount * 2 : 0;

            EnsureMiniRoomOccupant(0, OwnerName, SocialRoomOccupantRole.Owner, ownerDetail ?? BuildMiniRoomDetail(ownerScore, currentTurnIndex == 0, "Host seat"), ownerReady);
            EnsureMiniRoomOccupant(1, guestName, SocialRoomOccupantRole.Guest, guestDetail ?? BuildMiniRoomDetail(guestScore, currentTurnIndex == 1, "Guest seat"), guestReady);

            int occupantCount = 2;
            if (extraOccupants != null)
            {
                foreach (SocialRoomOccupant occupant in extraOccupants)
                {
                    if (occupant == null)
                    {
                        continue;
                    }

                    EnsureMiniRoomOccupant(occupantCount, occupant.Name, occupant.Role, occupant.Detail, occupant.IsReady);
                    occupantCount++;
                }
            }

            if (_occupants.Count > occupantCount)
            {
                _occupants.RemoveRange(occupantCount, _occupants.Count - occupantCount);
            }

            if (_items.Count > 0)
            {
                _items[0].Update("Match Cards table preview", 1, 0, false, false);
            }

            if (_items.Count > 1)
            {
                string detail = _miniRoomWagerAmount > 0
                    ? $"Match Cards board synced to the live room state | Wager {_miniRoomWagerAmount:N0} per player"
                    : "Match Cards board synced to the live room state";
                _items[1].Update(detail, 1, 0, false, false);
            }

            if (_notes.Count == 0)
            {
                _notes.Add("CMemoryGameDlg-backed Match Cards room shell.");
                _notes.Add("Room occupants and ready state now mirror the live simulator board.");
            }
            else
            {
                _notes[0] = "CMemoryGameDlg-backed Match Cards room shell.";
                if (_notes.Count == 1)
                {
                    _notes.Add("Room occupants and ready state now mirror the live simulator board.");
                }
                else
                {
                    _notes[1] = _miniRoomWagerAmount > 0
                        ? $"Room occupants mirror the live board and a {_miniRoomWagerAmount:N0}-meso wager pot is escrowed."
                        : "Room occupants and ready state now mirror the live simulator board.";
                }
            }
        }

        public void AddMiniRoomChatEntry(string text, SocialRoomChatTone tone = SocialRoomChatTone.Neutral)
        {
            if (Kind != SocialRoomKind.MiniRoom || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _chatEntries.Add(new SocialRoomChatEntry(text, tone));
            const int maxChatEntries = 48;
            if (_chatEntries.Count > maxChatEntries)
            {
                _chatEntries.RemoveRange(0, _chatEntries.Count - maxChatEntries);
            }
        }

        public void AddMiniRoomSpeakerMessage(string speakerName, string message, bool isLocalSpeaker)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string resolvedSpeaker = string.IsNullOrWhiteSpace(speakerName) ? "Unknown" : speakerName.Trim();
            AddMiniRoomChatEntry(
                $"{resolvedSpeaker} : {message.Trim()}",
                isLocalSpeaker ? SocialRoomChatTone.LocalSpeaker : SocialRoomChatTone.RemoteSpeaker);
        }

        public void AddMiniRoomSystemMessage(string message, bool isWarning = false)
        {
            AddMiniRoomChatEntry(message, isWarning ? SocialRoomChatTone.Warning : SocialRoomChatTone.System);
        }

        public static SocialRoomRuntime CreateMiniRoomSample()
        {
            return new SocialRoomRuntime(
                SocialRoomKind.MiniRoom,
                "Mini Room",
                "ExplorerGM",
                capacity: 4,
                mesoAmount: 0,
                occupants: new[]
                {
                    new SocialRoomOccupant("ExplorerGM", SocialRoomOccupantRole.Owner, "Host seat", isReady: true),
                    new SocialRoomOccupant("Rondo", SocialRoomOccupantRole.Guest, "Waiting to ready"),
                    new SocialRoomOccupant("Rin", SocialRoomOccupantRole.Visitor, "Watching from the rail"),
                },
                items: new[]
                {
                    new SocialRoomItemEntry("ExplorerGM", "Omok board", 1, 0, "Wood-stone board preview"),
                    new SocialRoomItemEntry("Rondo", "Card deck", 1, 0, "Match Cards preview set")
                },
                notes: new[]
                {
                    "Shared client surface for Omok and Match Cards.",
                    "Occupant readiness and turn-state messaging are simulator-owned for now."
                },
                chatEntries: new[]
                {
                    new SocialRoomChatEntry("System : Match Cards room shell initialized.", SocialRoomChatTone.System),
                    new SocialRoomChatEntry("Rondo : Waiting on the first ready check.", SocialRoomChatTone.RemoteSpeaker)
                },
                statusMessage: "Mini-room shell ready. Toggle the preview to compare Omok and Match Cards framing.",
                roomState: "Waiting for ready check",
                modeName: "Omok");
        }

        public static SocialRoomRuntime CreatePersonalShopSample()
        {
            return new SocialRoomRuntime(
                SocialRoomKind.PersonalShop,
                "Personal Shop",
                "ExplorerGM",
                capacity: 3,
                mesoAmount: 1250000,
                occupants: new[]
                {
                    new SocialRoomOccupant("ExplorerGM", SocialRoomOccupantRole.Owner, "Registering sale bundles", isReady: true),
                    new SocialRoomOccupant("Rondo", SocialRoomOccupantRole.Buyer, "Browsing slot 4"),
                    new SocialRoomOccupant("Rin", SocialRoomOccupantRole.Visitor, "Saved as a frequent visitor")
                },
                items: new[]
                {
                    new SocialRoomItemEntry("ExplorerGM", "Brown Work Gloves", 1, 850000, "Bundle A | Clean roll"),
                    new SocialRoomItemEntry("ExplorerGM", "White Scroll", 2, 640000, "Bundle B | Reserved by Rondo"),
                    new SocialRoomItemEntry("ExplorerGM", "Steel Pipe", 3, 175000, "Bundle C | Discount tray", isClaimed: true)
                },
                notes: new[]
                {
                    "Client-backed shop chrome now exists before full item-for-sale packet flow.",
                    "Buttons now drive a visible simulator-owned sale ledger while inventory escrow remains unmodeled."
                },
                chatEntries: null,
                statusMessage: "Personal-shop shell open. Visit, arrange, and claim actions now update simulator room state.",
                roomState: "Open for visitors",
                modeName: "Open shop");
        }

        public static SocialRoomRuntime CreateEntrustedShopSample()
        {
            return new SocialRoomRuntime(
                SocialRoomKind.EntrustedShop,
                "Entrusted Shop",
                "ExplorerGM",
                capacity: 1,
                mesoAmount: 2840000,
                occupants: new[]
                {
                    new SocialRoomOccupant("ExplorerGM", SocialRoomOccupantRole.Merchant, "Merchant permit active", isReady: true)
                },
                items: new[]
                {
                    new SocialRoomItemEntry("ExplorerGM", "Glove ATT 60%", 4, 420000, "Slot 1 | Sold overnight", isClaimed: true),
                    new SocialRoomItemEntry("ExplorerGM", "Red Whip", 1, 1250000, "Slot 2 | Display row"),
                    new SocialRoomItemEntry("ExplorerGM", "Power Elixir", 25, 180000, "Slot 3 | Restock pending")
                },
                notes: new[]
                {
                    "Hired-shop and entrusted-shop ledger controls use the dedicated entrusted button set.",
                    "Ledger and restock controls now update a visible sale list while long-term persistence remains absent."
                },
                chatEntries: null,
                statusMessage: "Entrusted-shop shell open. Arrange and coin actions now drive visible merchant state.",
                roomState: "Permit active",
                modeName: "Ledger review");
        }

        public static SocialRoomRuntime CreateTradingRoomSample()
        {
            return new SocialRoomRuntime(
                SocialRoomKind.TradingRoom,
                "Trading Room",
                "ExplorerGM",
                capacity: 2,
                mesoAmount: 150000,
                occupants: new[]
                {
                    new SocialRoomOccupant("ExplorerGM", SocialRoomOccupantRole.Trader, "Offering scroll bundle"),
                    new SocialRoomOccupant("Rondo", SocialRoomOccupantRole.Trader, "Offering ore stack")
                },
                items: new[]
                {
                    new SocialRoomItemEntry("ExplorerGM", "Chaos Scroll 60%", 1, 0, "Owner offer", isLocked: false),
                    new SocialRoomItemEntry("ExplorerGM", "Mana Elixir", 10, 0, "Owner add-on", isLocked: false),
                    new SocialRoomItemEntry("Rondo", "Dark Crystal Ore", 20, 0, "Guest offer", isLocked: false),
                    new SocialRoomItemEntry("Rondo", "Ilbi Throwing-Star", 1, 0, "Guest premium add-on", isLocked: false)
                },
                notes: new[]
                {
                    "Trade-room readiness, meso offers, and escrow rows are now visible in a dedicated shell.",
                    "No packet-driven accept handshake or cross-session persistence is simulated yet."
                },
                chatEntries: null,
                statusMessage: "Trading-room shell ready. Offer, lock, and reset actions now update the shared room state.",
                roomState: "Negotiating",
                modeName: "Open trade");
        }

        public void ToggleMiniRoomGuestReady()
        {
            if (Kind != SocialRoomKind.MiniRoom || _occupants.Count < 2)
            {
                return;
            }

            if (_miniRoomToggleReadyHandler != null)
            {
                _miniRoomToggleReadyHandler();
                return;
            }

            SocialRoomOccupant guest = _occupants[1];
            bool nextReady = !guest.IsReady;
            guest.Update(nextReady ? "Ready for the first turn" : "Waiting to ready", nextReady);
            RoomState = nextReady ? "Ready check complete" : "Waiting for ready check";
            StatusMessage = nextReady
                ? $"{guest.Name} is ready. Start {ModeName} when you want to open the board."
                : $"{guest.Name} stepped back from the ready check.";
        }

        public void CycleMiniRoomMode()
        {
            if (Kind != SocialRoomKind.MiniRoom)
            {
                return;
            }

            if (_miniRoomModeHandler != null)
            {
                _miniRoomModeHandler();
                return;
            }

            _miniRoomModeIndex = (_miniRoomModeIndex + 1) % 2;
            ModeName = _miniRoomModeIndex == 0 ? "Omok" : "Match Cards";
            if (_items.Count > 1)
            {
                _items[0].Update(
                    _miniRoomModeIndex == 0 ? "Wood-stone board preview" : "Table preview hidden",
                    1,
                    0,
                    false,
                    false);
                _items[1].Update(
                    _miniRoomModeIndex == 0 ? "Card deck preview hidden" : "Match Cards preview set",
                    1,
                    0,
                    false,
                    false);
            }

            StatusMessage = $"Mini-room preview switched to {ModeName}.";
        }

        public void StartMiniRoomSession()
        {
            if (Kind != SocialRoomKind.MiniRoom)
            {
                return;
            }

            if (_miniRoomStartHandler != null)
            {
                _miniRoomStartHandler();
                return;
            }

            bool guestReady = _occupants.Count > 1 && _occupants[1].IsReady;
            RoomState = guestReady ? $"{ModeName} in progress" : "Waiting for ready check";
            if (_items.Count > 0)
            {
                _items[0].Update(
                    guestReady ? $"{ModeName} board active" : $"{ModeName} board waiting on ready",
                    1,
                    0,
                    guestReady,
                    false);
            }

            StatusMessage = guestReady
                ? $"{ModeName} session started with ExplorerGM on the opening turn."
                : $"Cannot start {ModeName} until the guest readies up.";
        }

        public bool TrySetMiniRoomWager(int mesoAmount, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.MiniRoom)
            {
                message = "Mini-room wagers only apply to the MiniRoom shell.";
                return false;
            }

            mesoAmount = Math.Max(0, mesoAmount);
            int delta = mesoAmount - _miniRoomWagerAmount;
            if (delta > 0 && (_inventoryRuntime == null || !_inventoryRuntime.TryConsumeMeso(delta)))
            {
                message = $"Need {delta:N0} more meso in inventory to set that mini-room wager.";
                return false;
            }

            if (delta < 0)
            {
                _inventoryRuntime?.AddMeso(Math.Abs(delta));
            }

            _miniRoomWagerAmount = mesoAmount;
            MesoAmount = mesoAmount > 0 ? mesoAmount * 2 : 0;
            StatusMessage = mesoAmount > 0
                ? $"Mini-room wager set to {mesoAmount:N0} per player."
                : "Mini-room wager cleared and refunded.";
            message = StatusMessage;
            return true;
        }

        public bool TrySettleMiniRoomWager(string outcome, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.MiniRoom)
            {
                message = "Mini-room wager settlement only applies to the MiniRoom shell.";
                return false;
            }

            if (_miniRoomWagerAmount <= 0)
            {
                message = "No mini-room wager is currently escrowed.";
                return false;
            }

            string normalized = string.IsNullOrWhiteSpace(outcome) ? "owner" : outcome.Trim().ToLowerInvariant();
            int stake = _miniRoomWagerAmount;
            switch (normalized)
            {
                case "owner":
                case "host":
                case "win":
                    _inventoryRuntime?.AddMeso(stake * 2);
                    StatusMessage = $"{OwnerName} won the mini-room and collected {stake * 2:N0} meso from the wager pot.";
                    break;
                case "guest":
                case "lose":
                    StatusMessage = $"{OwnerName} lost the mini-room wager and the guest kept the pot.";
                    break;
                case "draw":
                case "refund":
                    _inventoryRuntime?.AddMeso(stake);
                    StatusMessage = $"Mini-room wager ended in a draw and refunded {stake:N0} meso to {OwnerName}.";
                    break;
                default:
                    message = "Mini-room wager outcome must be owner, guest, or draw.";
                    return false;
            }

            _miniRoomWagerAmount = 0;
            MesoAmount = 0;
            message = StatusMessage;
            return true;
        }

        public void TogglePersonalShopOpen()
        {
            if (Kind != SocialRoomKind.PersonalShop)
            {
                return;
            }

            bool isOpen = RoomState != "Open for visitors";
            RoomState = isOpen ? "Open for visitors" : "Closed for setup";
            ModeName = isOpen ? "Open shop" : "Repricing";
            foreach (SocialRoomItemEntry item in _items)
            {
                item.Update(
                    isOpen
                        ? item.IsClaimed ? $"{item.Detail} | Claim pending" : $"{item.Detail} | Visible to visitors"
                        : item.IsClaimed ? $"{item.Detail} | Claim pending" : $"{item.Detail} | Hidden during repricing",
                    item.Quantity,
                    item.MesoAmount,
                    false,
                    item.IsClaimed);
            }

            StatusMessage = isOpen
                ? "The personal shop is open and new visitors can enter."
                : "The personal shop is closed while the owner edits bundles.";
        }

        public bool ClosePersonalShop(out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.PersonalShop)
            {
                message = "Close-personal-shop only applies to the personal shop shell.";
                return false;
            }

            int refunded = ReturnEscrowRows(entry => entry.ReturnOnClose);
            _occupants.RemoveAll(occupant => occupant.Role == SocialRoomOccupantRole.Buyer || occupant.Role == SocialRoomOccupantRole.Visitor);
            if (_inventoryBackedRows)
            {
                _items.Clear();
            }

            RoomState = "Closed for setup";
            ModeName = "Repricing";
            StatusMessage = refunded > 0
                ? $"Closed the personal shop and returned {refunded} unsold bundle(s) to inventory."
                : "Closed the personal shop for setup.";
            message = StatusMessage;
            return true;
        }

        public void ArrangePersonalShopInventory()
        {
            if (Kind != SocialRoomKind.PersonalShop)
            {
                return;
            }

            _items.Sort((left, right) => right.MesoAmount.CompareTo(left.MesoAmount));
            for (int i = 0; i < _items.Count; i++)
            {
                SocialRoomItemEntry item = _items[i];
                item.Update($"Bundle {(char)('A' + i)} | Sorted by price", item.Quantity, item.MesoAmount, false, item.IsClaimed);
            }

            StatusMessage = "Shop bundles reordered by price and highlighted slot.";
            if (_occupants.Count > 0)
            {
                _occupants[0].Update("Reordered bundles and refreshed the room notice", isReady: true);
            }
        }

        public void ClaimPersonalShopEarnings()
        {
            if (Kind != SocialRoomKind.PersonalShop)
            {
                return;
            }

            int claimed = MesoAmount;
            if (claimed > 0)
            {
                _inventoryRuntime?.AddMeso(claimed);
            }

            MesoAmount = 0;
            RoomState = "Claimed sale proceeds";
            foreach (SocialRoomItemEntry item in _items)
            {
                item.Update($"{item.Detail} | Earnings settled", item.Quantity, item.MesoAmount, false, true);
            }

            StatusMessage = claimed > 0
                ? $"Claimed {claimed:N0} mesos from sold bundles into inventory."
                : "No queued mesos remain to claim from the personal shop.";
        }

        public bool AddPersonalShopVisitor(string visitorName, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.PersonalShop)
            {
                message = "Visitor persistence is only modeled for the personal shop shell.";
                return false;
            }

            string resolvedName = NormalizeName(visitorName);
            if (_blockedVisitors.Contains(resolvedName, StringComparer.OrdinalIgnoreCase))
            {
                message = $"{resolvedName} is blacklisted from the personal shop.";
                return false;
            }

            if (_occupants.Any(occupant => string.Equals(occupant.Name, resolvedName, StringComparison.OrdinalIgnoreCase)))
            {
                message = $"{resolvedName} is already inside the personal shop.";
                return true;
            }

            if (_occupants.Count >= Capacity)
            {
                message = "The personal shop is full.";
                return false;
            }

            _occupants.Add(new SocialRoomOccupant(resolvedName, SocialRoomOccupantRole.Visitor, "Browsing saved visitor slot"));
            if (!_savedVisitors.Contains(resolvedName, StringComparer.OrdinalIgnoreCase))
            {
                _savedVisitors.Add(resolvedName);
            }

            StatusMessage = $"{resolvedName} entered the personal shop and was saved to the visitor list.";
            message = StatusMessage;
            return true;
        }

        public bool TogglePersonalShopBlacklist(string visitorName, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.PersonalShop)
            {
                message = "Blacklist persistence is only modeled for the personal shop shell.";
                return false;
            }

            string resolvedName = string.IsNullOrWhiteSpace(visitorName)
                ? _savedVisitors.LastOrDefault() ?? _occupants.LastOrDefault(occupant => occupant.Role != SocialRoomOccupantRole.Owner)?.Name
                : NormalizeName(visitorName);
            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                message = "No visitor is available to blacklist.";
                return false;
            }

            if (_blockedVisitors.Contains(resolvedName, StringComparer.OrdinalIgnoreCase))
            {
                _blockedVisitors.RemoveAll(name => string.Equals(name, resolvedName, StringComparison.OrdinalIgnoreCase));
                StatusMessage = $"{resolvedName} was removed from the personal-shop blacklist.";
            }
            else
            {
                _blockedVisitors.Add(resolvedName);
                _occupants.RemoveAll(occupant => string.Equals(occupant.Name, resolvedName, StringComparison.OrdinalIgnoreCase) && occupant.Role != SocialRoomOccupantRole.Owner);
                StatusMessage = $"{resolvedName} was added to the personal-shop blacklist and removed from the room.";
            }

            message = StatusMessage;
            return true;
        }

        public bool TryAutoListPersonalShopItem(out string message)
        {
            message = null;
            if (!TryGetFirstInventoryItem(out InventorySlotData slotData, out _))
            {
                message = "No inventory item is available to list in the personal shop.";
                return false;
            }

            return TryListPersonalShopItem(slotData.ItemId, Math.Max(1, slotData.Quantity), ResolveSuggestedPrice(slotData.ItemId, slotData.Quantity), out message);
        }

        public bool TryListPersonalShopItem(int itemId, int quantity, int mesoAmount, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.PersonalShop)
            {
                message = "Listing inventory escrow only applies to the personal shop shell.";
                return false;
            }

            if (!TryConsumeInventoryItem(itemId, quantity, out InventoryType inventoryType, out InventorySlotData slotData, out message))
            {
                return false;
            }

            EnsureInventoryBackedRows();
            SocialRoomItemEntry entry = new SocialRoomItemEntry(OwnerName, slotData.ItemName, quantity, Math.Max(0, mesoAmount), $"Bundle {(char)('A' + _items.Count)} | Inventory escrowed");
            _items.Add(entry);
            _inventoryEscrow.Add(new InventoryEscrowEntry(entry, inventoryType, slotData, returnOnReset: false, returnOnClose: true));
            RoomState = "Closed for setup";
            ModeName = "Repricing";
            StatusMessage = $"Listed {slotData.ItemName} x{quantity} for {mesoAmount:N0} meso in the personal shop.";
            message = StatusMessage;
            return true;
        }

        public bool TryBuyPersonalShopItem(int itemIndex, string buyerName, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.PersonalShop)
            {
                message = "Bundle purchase simulation only applies to the personal shop shell.";
                return false;
            }

            List<SocialRoomItemEntry> availableEntries = _items.Where(item => !item.IsClaimed && item.MesoAmount > 0).ToList();
            if (availableEntries.Count == 0)
            {
                message = "No purchasable bundle is available in the personal shop.";
                return false;
            }

            SocialRoomItemEntry entry = itemIndex >= 0 && itemIndex < availableEntries.Count
                ? availableEntries[itemIndex]
                : availableEntries[0];
            string resolvedBuyer = NormalizeName(buyerName);
            if (_blockedVisitors.Contains(resolvedBuyer, StringComparer.OrdinalIgnoreCase))
            {
                message = $"{resolvedBuyer} is blacklisted and cannot buy from this personal shop.";
                return false;
            }

            entry.Update($"{entry.Detail} | Bought by {resolvedBuyer}", entry.Quantity, entry.MesoAmount, false, true);
            MesoAmount += entry.MesoAmount;
            _inventoryEscrow.RemoveAll(escrow => ReferenceEquals(escrow.Entry, entry));
            _occupants.RemoveAll(occupant => string.Equals(occupant.Name, resolvedBuyer, StringComparison.OrdinalIgnoreCase));
            _occupants.Add(new SocialRoomOccupant(resolvedBuyer, SocialRoomOccupantRole.Buyer, $"Purchased {entry.ItemName}"));
            if (!_savedVisitors.Contains(resolvedBuyer, StringComparer.OrdinalIgnoreCase))
            {
                _savedVisitors.Add(resolvedBuyer);
            }

            StatusMessage = $"{resolvedBuyer} bought {entry.ItemName} for {entry.MesoAmount:N0} meso.";
            message = StatusMessage;
            return true;
        }

        public void ToggleEntrustedLedgerMode()
        {
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                return;
            }

            bool isLedger = !string.Equals(ModeName, "Restock", StringComparison.Ordinal);
            ModeName = isLedger ? "Restock" : "Ledger review";
            RoomState = isLedger ? "Updating sale list" : "Permit active";
            foreach (SocialRoomItemEntry item in _items)
            {
                item.Update(
                    isLedger
                        ? $"{item.Detail} | Editing sale slot"
                        : item.IsClaimed ? $"{item.Detail} | Awaiting payout" : $"{item.Detail} | Permit active",
                    item.Quantity,
                    item.MesoAmount,
                    false,
                    item.IsClaimed);
            }

            StatusMessage = isLedger
                ? "Entrusted-shop view switched to restock mode."
                : "Entrusted-shop view switched back to the earnings ledger.";
        }

        public bool TryAutoListEntrustedShopItem(out string message)
        {
            message = null;
            if (!TryGetFirstInventoryItem(out InventorySlotData slotData, out _))
            {
                message = "No inventory item is available to restock in the entrusted shop.";
                return false;
            }

            return TryListEntrustedShopItem(slotData.ItemId, Math.Max(1, slotData.Quantity), ResolveSuggestedPrice(slotData.ItemId, slotData.Quantity), out message);
        }

        public bool TryListEntrustedShopItem(int itemId, int quantity, int mesoAmount, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                message = "Inventory-backed restock only applies to the entrusted shop shell.";
                return false;
            }

            if (!TryConsumeInventoryItem(itemId, quantity, out InventoryType inventoryType, out InventorySlotData slotData, out message))
            {
                return false;
            }

            EnsureInventoryBackedRows();
            SocialRoomItemEntry entry = new SocialRoomItemEntry(OwnerName, slotData.ItemName, quantity, Math.Max(0, mesoAmount), $"Slot {_items.Count + 1} | Restocked from inventory");
            _items.Add(entry);
            _inventoryEscrow.Add(new InventoryEscrowEntry(entry, inventoryType, slotData, returnOnReset: false, returnOnClose: false));
            RoomState = "Updating sale list";
            ModeName = "Restock";
            StatusMessage = $"Restocked {slotData.ItemName} x{quantity} in the entrusted-shop ledger.";
            message = StatusMessage;
            return true;
        }

        public void ArrangeEntrustedShop()
        {
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                return;
            }

            RoomState = "Rearranged sale slots";
            _items.Sort((left, right) => string.Compare(left.ItemName, right.ItemName, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < _items.Count; i++)
            {
                SocialRoomItemEntry item = _items[i];
                item.Update($"Slot {i + 1} | Rearranged for next listing", item.Quantity, item.MesoAmount, false, item.IsClaimed);
            }

            StatusMessage = "Entrusted-shop bundles rearranged for the next hiring cycle.";
        }

        public void ClaimEntrustedShopEarnings()
        {
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                return;
            }

            int claimed = MesoAmount;
            if (claimed > 0)
            {
                _inventoryRuntime?.AddMeso(claimed);
            }

            MesoAmount = 0;
            RoomState = "Ledger settled";
            bool markedClaimed = false;
            foreach (SocialRoomItemEntry item in _items)
            {
                bool claimThisItem = !markedClaimed && item.IsClaimed;
                item.Update(
                    claimThisItem ? $"{item.Detail} | Claimed into storage wallet" : item.Detail,
                    item.Quantity,
                    item.MesoAmount,
                    false,
                    claimThisItem);
                markedClaimed |= claimThisItem;
            }

            StatusMessage = claimed > 0
                ? $"Claimed {claimed:N0} mesos from the entrusted shop ledger into inventory."
                : "No entrusted-shop mesos remain to claim.";
        }

        public void IncreaseTradeOffer()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            if (TryOfferTradeMeso(50000, out string mesoMessage))
            {
                StatusMessage = mesoMessage;
                return;
            }

            MesoAmount += 50000;
            _tradeLocalOfferMeso += 50000;
            if (_occupants.Count > 0)
            {
                _occupants[0].Update($"Offering {MesoAmount:N0} mesos with the item stack", false);
            }

            StatusMessage = $"Raised the offered mesos to {MesoAmount:N0}.";
        }

        public bool TryOfferTradeMeso(int mesoAmount, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Trade meso escrow only applies to the trading-room shell.";
                return false;
            }

            if (mesoAmount <= 0)
            {
                message = "Trade meso offers must be positive.";
                return false;
            }

            if (_inventoryRuntime == null || !_inventoryRuntime.TryConsumeMeso(mesoAmount))
            {
                message = $"Need {mesoAmount:N0} more meso in inventory to add that trade offer.";
                return false;
            }

            MesoAmount += mesoAmount;
            _tradeLocalOfferMeso += mesoAmount;
            if (_occupants.Count > 0)
            {
                _occupants[0].Update($"Offering {MesoAmount:N0} mesos with the item stack", false);
            }

            message = $"Escrowed {mesoAmount:N0} meso into the trading room.";
            return true;
        }

        public bool TryOfferTradeItem(int itemId, int quantity, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Trade item escrow only applies to the trading-room shell.";
                return false;
            }

            if (!TryConsumeInventoryItem(itemId, quantity, out InventoryType inventoryType, out InventorySlotData slotData, out message))
            {
                return false;
            }

            if (!_inventoryBackedRows)
            {
                _items.RemoveAll(item => string.Equals(item.OwnerName, OwnerName, StringComparison.OrdinalIgnoreCase));
                _inventoryBackedRows = true;
            }

            SocialRoomItemEntry entry = new SocialRoomItemEntry(OwnerName, slotData.ItemName, quantity, 0, "Owner offer | Inventory escrowed");
            _items.Insert(Math.Min(2, _items.Count), entry);
            _inventoryEscrow.Add(new InventoryEscrowEntry(entry, inventoryType, slotData, returnOnReset: true, returnOnClose: true));
            RoomState = "Negotiating";
            StatusMessage = $"Added {slotData.ItemName} x{quantity} to the local trade escrow.";
            message = StatusMessage;
            return true;
        }

        public void ConfirmTradeLock()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            bool locked = RoomState != "Locked";
            RoomState = locked ? "Locked" : "Negotiating";
            StatusMessage = locked
                ? "Both traders locked the exchange and are waiting for final confirmation."
                : "Trade lock released so the offer can change again.";

            foreach (SocialRoomOccupant occupant in _occupants)
            {
                occupant.Update(locked ? "Trade locked" : "Reviewing the offer", locked);
            }

            foreach (SocialRoomItemEntry item in _items)
            {
                item.Update(item.Detail, item.Quantity, item.MesoAmount, locked, false);
            }
        }

        public void ResetTrade()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            int restoredRows = ReturnEscrowRows(entry => entry.ReturnOnReset);
            if (_tradeLocalOfferMeso > 0)
            {
                _inventoryRuntime?.AddMeso(_tradeLocalOfferMeso);
            }

            MesoAmount = 150000;
            _tradeLocalOfferMeso = 0;
            _tradeRemoteOfferMeso = 75000;
            RoomState = "Negotiating";
            StatusMessage = restoredRows > 0
                ? $"Trade offer reset and {restoredRows} escrowed item entr{(restoredRows == 1 ? "y was" : "ies were")} returned to inventory."
                : "Trade offer reset to the initial simulator draft.";
            _items.Clear();
            foreach (SocialRoomItemEntry item in _defaultItems)
            {
                _items.Add(new SocialRoomItemEntry(item.OwnerName, item.ItemName, item.Quantity, item.MesoAmount, item.Detail, false, false));
            }

            _occupants.Clear();
            foreach (SocialRoomOccupant occupant in _defaultOccupants)
            {
                _occupants.Add(new SocialRoomOccupant(occupant.Name, occupant.Role, occupant.Detail, false));
            }

            _inventoryEscrow.Clear();
            _inventoryBackedRows = false;
        }

        public bool TryCompleteTrade(out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Trade settlement only applies to the trading-room shell.";
                return false;
            }

            if (!string.Equals(RoomState, "Locked", StringComparison.Ordinal))
            {
                message = "Lock the trade before completing the settlement.";
                return false;
            }

            List<SocialRoomItemEntry> remoteEntries = _items
                .Where(item => !string.Equals(item.OwnerName, OwnerName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (SocialRoomItemEntry remoteEntry in remoteEntries)
            {
                int remoteItemId = ResolveItemIdByName(remoteEntry.ItemName);
                if (remoteItemId <= 0)
                {
                    message = $"Could not resolve trade reward '{remoteEntry.ItemName}'.";
                    return false;
                }

                InventoryType remoteType = InventoryItemMetadataResolver.ResolveInventoryType(remoteItemId);
                if (_inventoryRuntime == null || !_inventoryRuntime.CanAcceptItem(remoteType, remoteItemId, remoteEntry.Quantity))
                {
                    message = $"Inventory cannot accept trade reward '{remoteEntry.ItemName}'.";
                    return false;
                }
            }

            foreach (SocialRoomItemEntry remoteEntry in remoteEntries)
            {
                int remoteItemId = ResolveItemIdByName(remoteEntry.ItemName);
                InventoryType remoteType = InventoryItemMetadataResolver.ResolveInventoryType(remoteItemId);
                _inventoryRuntime?.AddItem(remoteType, remoteItemId, _inventoryRuntime.GetItemTexture(remoteType, remoteItemId), remoteEntry.Quantity);
                remoteEntry.Update($"{remoteEntry.Detail} | Trade settled", remoteEntry.Quantity, remoteEntry.MesoAmount, true, true);
            }

            if (_tradeRemoteOfferMeso > 0)
            {
                _inventoryRuntime?.AddMeso(_tradeRemoteOfferMeso);
            }

            foreach (SocialRoomItemEntry localEntry in _items.Where(item => string.Equals(item.OwnerName, OwnerName, StringComparison.OrdinalIgnoreCase)))
            {
                localEntry.Update($"{localEntry.Detail} | Trade settled", localEntry.Quantity, localEntry.MesoAmount, true, true);
            }

            _inventoryEscrow.Clear();
            _tradeLocalOfferMeso = 0;
            MesoAmount = 0;
            RoomState = "Trade settled";
            StatusMessage = $"Trade completed. Received {remoteEntries.Count} remote item entr{(remoteEntries.Count == 1 ? "y" : "ies")} and {_tradeRemoteOfferMeso:N0} meso.";
            message = StatusMessage;
            return true;
        }

        private void EnsureInventoryBackedRows()
        {
            if (_inventoryBackedRows)
            {
                return;
            }

            _inventoryBackedRows = true;
            _items.Clear();
        }

        private bool TryConsumeInventoryItem(int itemId, int quantity, out InventoryType inventoryType, out InventorySlotData slotData, out string message)
        {
            inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            slotData = null;
            message = null;

            if (inventoryType == InventoryType.NONE || quantity <= 0)
            {
                message = "A valid item id and quantity are required.";
                return false;
            }

            if (_inventoryRuntime == null)
            {
                message = "The simulator inventory is not attached to this room runtime.";
                return false;
            }

            if (_inventoryRuntime.GetItemCount(inventoryType, itemId) < quantity)
            {
                message = $"Inventory does not contain {quantity} of item {itemId}.";
                return false;
            }

            slotData = new InventorySlotData
            {
                ItemId = itemId,
                ItemTexture = _inventoryRuntime.GetItemTexture(inventoryType, itemId),
                Quantity = quantity,
                MaxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(inventoryType),
                GradeFrameIndex = inventoryType == InventoryType.EQUIP ? 0 : null,
                ItemName = ResolveItemName(itemId),
                ItemTypeName = ResolveItemTypeName(inventoryType, itemId),
                Description = ResolveItemDescription(itemId)
            };

            if (!_inventoryRuntime.TryConsumeItem(inventoryType, itemId, quantity))
            {
                message = $"Inventory could not escrow item {itemId} x{quantity}.";
                return false;
            }

            return true;
        }

        private bool TryGetFirstInventoryItem(out InventorySlotData slotData, out InventoryType inventoryType)
        {
            slotData = null;
            inventoryType = InventoryType.NONE;
            if (_inventoryRuntime is not InventoryUI inventoryWindow)
            {
                return false;
            }

            InventoryType[] order = { InventoryType.EQUIP, InventoryType.USE, InventoryType.SETUP, InventoryType.ETC, InventoryType.CASH };
            foreach (InventoryType type in order)
            {
                slotData = inventoryWindow.GetSlots(type).FirstOrDefault(slot => slot != null && !slot.IsDisabled && slot.ItemId > 0 && Math.Max(1, slot.Quantity) > 0);
                if (slotData != null)
                {
                    inventoryType = type;
                    return true;
                }
            }

            return false;
        }

        private int ReturnEscrowRows(Func<InventoryEscrowEntry, bool> predicate)
        {
            if (_inventoryRuntime == null)
            {
                return 0;
            }

            List<InventoryEscrowEntry> toReturn = _inventoryEscrow.Where(predicate).ToList();
            foreach (InventoryEscrowEntry escrow in toReturn)
            {
                _inventoryRuntime.AddItem(escrow.InventoryType, escrow.SlotData.ItemId, escrow.SlotData.ItemTexture, escrow.SlotData.Quantity);
                _inventoryEscrow.Remove(escrow);
            }

            return toReturn.Count;
        }

        private static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Visitor" : value.Trim();
        }

        private static int ResolveSuggestedPrice(int itemId, int quantity)
        {
            int typeBucket = itemId / 1000000;
            int baseUnitPrice = typeBucket switch
            {
                1 => 250000,
                2 => 5000,
                3 => 12000,
                4 => 8000,
                5 => 30000,
                _ => 10000
            };

            return Math.Max(1000, baseUnitPrice * Math.Max(1, quantity));
        }

        private static string ResolveItemName(int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo?.Item1)
                ? itemInfo.Item1
                : itemId.ToString();
        }

        private static string ResolveItemTypeName(InventoryType type, int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : type.ToString();
        }

        private static string ResolveItemDescription(int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo?.Item3)
                ? itemInfo.Item3
                : string.Empty;
        }

        private static int ResolveItemIdByName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName) || global::HaCreator.Program.InfoManager?.ItemNameCache == null)
            {
                return 0;
            }

            foreach (KeyValuePair<int, Tuple<string, string, string>> entry in global::HaCreator.Program.InfoManager.ItemNameCache)
            {
                if (string.Equals(entry.Value?.Item1, itemName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Key;
                }
            }

            return 0;
        }

        private void EnsureMiniRoomOccupant(int index, string name, SocialRoomOccupantRole role, string detail, bool isReady)
        {
            while (_occupants.Count <= index)
            {
                _occupants.Add(new SocialRoomOccupant(name, role, detail, isReady));
            }

            _occupants[index].Update(name, role, detail, isReady);
        }

        private static string BuildMiniRoomDetail(int score, bool isCurrentTurn, string seat)
        {
            return isCurrentTurn
                ? $"{seat} | Score {score} | Current turn"
                : $"{seat} | Score {score}";
        }
    }
}
