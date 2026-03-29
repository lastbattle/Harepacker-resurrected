using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using MapleLib.PacketLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
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

    public enum SocialRoomFieldActorTemplate
    {
        Merchant,
        StoreBanker,
        CashEmployee
    }

    public sealed class SocialRoomFieldActorSnapshot
    {
        public SocialRoomFieldActorSnapshot(
            SocialRoomKind kind,
            SocialRoomFieldActorTemplate template,
            string headline,
            string detail,
            string stateKey,
            int templateId = 0,
            bool useOwnerAnchor = true,
            int anchorOffsetX = 0,
            int anchorOffsetY = 0,
            int worldX = 0,
            int worldY = 0,
            bool hasWorldPosition = false,
            bool? flip = null)
        {
            Kind = kind;
            Template = template;
            Headline = headline ?? string.Empty;
            Detail = detail ?? string.Empty;
            StateKey = stateKey ?? string.Empty;
            TemplateId = Math.Max(0, templateId);
            UseOwnerAnchor = useOwnerAnchor;
            AnchorOffsetX = anchorOffsetX;
            AnchorOffsetY = anchorOffsetY;
            WorldX = worldX;
            WorldY = worldY;
            HasWorldPosition = hasWorldPosition;
            Flip = flip;
        }

        public SocialRoomKind Kind { get; }
        public SocialRoomFieldActorTemplate Template { get; }
        public string Headline { get; }
        public string Detail { get; }
        public string StateKey { get; }
        public int TemplateId { get; }
        public bool UseOwnerAnchor { get; }
        public int AnchorOffsetX { get; }
        public int AnchorOffsetY { get; }
        public int WorldX { get; }
        public int WorldY { get; }
        public bool HasWorldPosition { get; }
        public bool? Flip { get; }
    }

    public sealed class SocialRoomOccupant
    {
        public SocialRoomOccupant(string name, SocialRoomOccupantRole role, string detail, bool isReady = false, CharacterBuild avatarBuild = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
            Role = role;
            Detail = detail ?? string.Empty;
            IsReady = isReady;
            AvatarBuild = avatarBuild;
        }

        public string Name { get; private set; }
        public SocialRoomOccupantRole Role { get; private set; }
        public string Detail { get; private set; }
        public bool IsReady { get; private set; }
        public CharacterBuild AvatarBuild { get; private set; }

        public void Update(string detail, bool isReady, CharacterBuild avatarBuild = null)
        {
            Detail = detail ?? string.Empty;
            IsReady = isReady;
            AvatarBuild = avatarBuild;
        }

        public void Update(string name, SocialRoomOccupantRole role, string detail, bool isReady, CharacterBuild avatarBuild = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
            Role = role;
            Detail = detail ?? string.Empty;
            IsReady = isReady;
            AvatarBuild = avatarBuild;
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
            bool isClaimed = false,
            int itemId = 0,
            int? packetSlotIndex = null)
        {
            OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "Unknown" : ownerName;
            ItemId = itemId > 0 ? itemId : ResolveItemIdByName(itemName);
            ItemName = string.IsNullOrWhiteSpace(itemName) ? "Unknown item" : itemName;
            Quantity = Math.Max(1, quantity);
            MesoAmount = Math.Max(0, mesoAmount);
            Detail = detail ?? string.Empty;
            IsLocked = isLocked;
            IsClaimed = isClaimed;
            PacketSlotIndex = packetSlotIndex > 0 ? packetSlotIndex : null;
        }

        public string OwnerName { get; }
        public int ItemId { get; private set; }
        public string ItemName { get; }
        public int Quantity { get; private set; }
        public int MesoAmount { get; private set; }
        public string Detail { get; private set; }
        public bool IsLocked { get; private set; }
        public bool IsClaimed { get; private set; }
        public int? PacketSlotIndex { get; private set; }

        public void Update(string detail, int quantity, int mesoAmount, bool isLocked, bool isClaimed)
        {
            Detail = detail ?? string.Empty;
            Quantity = Math.Max(1, quantity);
            MesoAmount = Math.Max(0, mesoAmount);
            IsLocked = isLocked;
            IsClaimed = isClaimed;
        }

        public void UpdatePacketIdentity(int itemId, int? packetSlotIndex)
        {
            if (itemId > 0)
            {
                ItemId = itemId;
            }

            PacketSlotIndex = packetSlotIndex > 0 ? packetSlotIndex : null;
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
        private const int MiniRoomOmokBoardSize = 15;
        private const byte TradingRoomPutItemPacketType = 15;
        private const byte TradingRoomPutMoneyPacketType = 16;
        private const byte TradingRoomTradePacketType = 17;
        private const byte TradingRoomExceedLimitPacketType = 21;
        private const byte PersonalShopBuyResultPacketType = 24;
        private const byte PersonalShopBasePacketType = 25;
        private const byte PersonalShopSoldItemResultPacketType = 26;
        private const byte PersonalShopMoveItemToInventoryPacketType = 27;
        private const byte EntrustedShopArrangeItemResultPacketType = 40;
        private const byte EntrustedShopWithdrawAllResultPacketType = 42;
        private const byte EntrustedShopWithdrawMoneyResultPacketType = 44;
        private const byte EntrustedShopVisitListResultPacketType = 46;
        private const byte EntrustedShopBlackListResultPacketType = 47;
        private const byte OmokTieRequestPacketType = 50;
        private const byte OmokTieResultPacketType = 51;
        private const byte OmokRetreatRequestPacketType = 54;
        private const byte OmokRetreatResultPacketType = 55;
        private const byte OmokReadyPacketType = 58;
        private const byte OmokCancelReadyPacketType = 59;
        private const byte OmokStartPacketType = 61;
        private const byte OmokGameResultPacketType = 62;
        private const byte OmokTimeOverPacketType = 63;
        private const byte OmokPutStonePacketType = 64;
        private const byte OmokPutStoneErrorPacketType = 65;

        public sealed class SocialRoomRemoteInventoryEntry
        {
            public SocialRoomRemoteInventoryEntry(int itemId, string itemName, int quantity)
            {
                ItemId = Math.Max(0, itemId);
                ItemName = string.IsNullOrWhiteSpace(itemName) ? ResolveItemName(ItemId) : itemName.Trim();
                Quantity = Math.Max(0, quantity);
            }

            public int ItemId { get; }
            public string ItemName { get; private set; }
            public int Quantity { get; private set; }

            public void Update(int quantity, string itemName = null)
            {
                Quantity = Math.Max(0, quantity);
                if (!string.IsNullOrWhiteSpace(itemName))
                {
                    ItemName = itemName.Trim();
                }
            }
        }

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

        private readonly record struct PacketOwnedTradeItem(byte SlotType, int ItemId, int Quantity, InventoryType InventoryType);

        private readonly List<SocialRoomOccupant> _occupants;
        private readonly List<SocialRoomItemEntry> _items;
        private readonly List<string> _notes;
        private readonly List<SocialRoomChatEntry> _chatEntries;
        private readonly List<string> _savedVisitors;
        private readonly List<string> _blockedVisitors;
        private readonly Queue<string> _pendingVisitorNames;
        private readonly List<InventoryEscrowEntry> _inventoryEscrow;
        private readonly List<SocialRoomRemoteInventoryEntry> _remoteInventoryEntries;
        private readonly List<SocialRoomItemEntry> _defaultItems;
        private readonly List<SocialRoomOccupant> _defaultOccupants;
        private readonly List<SocialRoomRemoteInventoryEntry> _defaultRemoteInventoryEntries;
        private readonly SocialRoomRuntimeSnapshot _defaultSnapshot;
        private readonly int[] _miniRoomOmokBoard;
        private int _miniRoomModeIndex;
        private int _miniRoomWagerAmount;
        private int _miniRoomOmokCurrentTurnIndex;
        private int _miniRoomOmokWinnerIndex;
        private int _miniRoomOmokLastMoveX;
        private int _miniRoomOmokLastMoveY;
        private int _miniRoomOmokOwnerStoneValue;
        private int _miniRoomOmokGuestStoneValue;
        private int _tradeLocalOfferMeso;
        private int _tradeRemoteOfferMeso;
        private int _remoteInventoryMeso;
        private bool _miniRoomOmokInProgress;
        private bool _miniRoomOmokTieRequested;
        private bool _tradeLocalLocked;
        private bool _tradeRemoteLocked;
        private bool _tradeLocalAccepted;
        private bool _tradeRemoteAccepted;
        private DateTime? _entrustedPermitExpiresAtUtc;
        private int _employeeTemplateId;
        private bool _employeeUseOwnerAnchor = true;
        private int _employeeAnchorOffsetX;
        private int _employeeAnchorOffsetY;
        private int _employeeWorldX;
        private int _employeeWorldY;
        private bool _employeeHasWorldPosition;
        private bool? _employeeFlip;
        private bool _inventoryBackedRows;
        private bool _suspendPersistence;
        private string _persistenceKey;
        private string _lastPacketOwnerSummary;
        private Action _miniRoomToggleReadyHandler;
        private Action _miniRoomStartHandler;
        private Action _miniRoomModeHandler;
        private Action<string, SocialRoomRuntimeSnapshot> _persistStateHandler;
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
            _remoteInventoryEntries = new List<SocialRoomRemoteInventoryEntry>();
            _defaultItems = items?.Select(item => new SocialRoomItemEntry(item.OwnerName, item.ItemName, item.Quantity, item.MesoAmount, item.Detail, item.IsLocked, item.IsClaimed, item.ItemId, item.PacketSlotIndex)).ToList()
                ?? new List<SocialRoomItemEntry>();
            _defaultOccupants = occupants?.Select(occupant => new SocialRoomOccupant(occupant.Name, occupant.Role, occupant.Detail, occupant.IsReady, occupant.AvatarBuild)).ToList()
                ?? new List<SocialRoomOccupant>();
            _defaultRemoteInventoryEntries = new List<SocialRoomRemoteInventoryEntry>();
            _miniRoomOmokBoard = new int[MiniRoomOmokBoardSize * MiniRoomOmokBoardSize];
            _miniRoomOmokCurrentTurnIndex = 0;
            _miniRoomOmokWinnerIndex = -1;
            _miniRoomOmokLastMoveX = -1;
            _miniRoomOmokLastMoveY = -1;
            _miniRoomOmokOwnerStoneValue = 1;
            _miniRoomOmokGuestStoneValue = 2;
            _tradeRemoteOfferMeso = kind == SocialRoomKind.TradingRoom ? 75000 : 0;
            _remoteInventoryMeso = kind == SocialRoomKind.TradingRoom ? 325000 : 0;
            _entrustedPermitExpiresAtUtc = kind == SocialRoomKind.EntrustedShop ? DateTime.UtcNow.AddHours(24) : null;
            _employeeAnchorOffsetX = kind switch
            {
                SocialRoomKind.EntrustedShop => 72,
                SocialRoomKind.PersonalShop => 56,
                _ => 0
            };
            _employeeAnchorOffsetY = 0;
            StatusMessage = statusMessage ?? string.Empty;
            RoomState = roomState ?? string.Empty;
            ModeName = modeName ?? string.Empty;
            _lastPacketOwnerSummary = BuildDefaultPacketOwnerSummary();
            SeedDefaultRemoteInventory();
            if (kind == SocialRoomKind.MiniRoom && string.Equals(ModeName, "Omok", StringComparison.OrdinalIgnoreCase))
            {
                SyncMiniRoomOmokPresentation();
            }
            _defaultSnapshot = BuildSnapshot();
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
        public IReadOnlyList<SocialRoomRemoteInventoryEntry> RemoteInventoryEntries => _remoteInventoryEntries;
        public int RemoteInventoryMeso => _remoteInventoryMeso;
        public int MiniRoomOmokBoardSizeValue => MiniRoomOmokBoardSize;
        public bool IsMiniRoomOmokActive => Kind == SocialRoomKind.MiniRoom && _miniRoomModeIndex == 0;
        public bool IsMiniRoomOmokInProgress => _miniRoomOmokInProgress;
        public int MiniRoomOmokCurrentTurnIndex => _miniRoomOmokCurrentTurnIndex;
        public int MiniRoomOmokWinnerIndex => _miniRoomOmokWinnerIndex;
        public int MiniRoomOmokLastMoveX => _miniRoomOmokLastMoveX;
        public int MiniRoomOmokLastMoveY => _miniRoomOmokLastMoveY;

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

        public void ConfigurePersistence(string key, Action<string, SocialRoomRuntimeSnapshot> persistStateHandler, SocialRoomRuntimeSnapshot snapshot = null)
        {
            _persistenceKey = string.IsNullOrWhiteSpace(key) ? null : key.Trim();
            _persistStateHandler = persistStateHandler;

            if (snapshot == null)
            {
                ResetToDefaults();
                return;
            }

            RestoreSnapshot(snapshot);
        }

        public SocialRoomRuntimeSnapshot BuildSnapshot()
        {
            return new SocialRoomRuntimeSnapshot
            {
                Kind = Kind,
                RoomTitle = RoomTitle,
                OwnerName = OwnerName,
                MesoAmount = MesoAmount,
                StatusMessage = StatusMessage,
                RoomState = RoomState,
                ModeName = ModeName,
                MiniRoomModeIndex = _miniRoomModeIndex,
                MiniRoomWagerAmount = _miniRoomWagerAmount,
                MiniRoomOmokInProgress = _miniRoomOmokInProgress,
                MiniRoomOmokCurrentTurnIndex = _miniRoomOmokCurrentTurnIndex,
                MiniRoomOmokWinnerIndex = _miniRoomOmokWinnerIndex,
                MiniRoomOmokLastMoveX = _miniRoomOmokLastMoveX,
                MiniRoomOmokLastMoveY = _miniRoomOmokLastMoveY,
                MiniRoomOmokOwnerStoneValue = _miniRoomOmokOwnerStoneValue,
                MiniRoomOmokGuestStoneValue = _miniRoomOmokGuestStoneValue,
                MiniRoomOmokTieRequested = _miniRoomOmokTieRequested,
                MiniRoomOmokBoard = _miniRoomOmokBoard.ToList(),
                TradeLocalOfferMeso = _tradeLocalOfferMeso,
                TradeRemoteOfferMeso = _tradeRemoteOfferMeso,
                TradeLocalLocked = _tradeLocalLocked,
                TradeRemoteLocked = _tradeRemoteLocked,
                TradeLocalAccepted = _tradeLocalAccepted,
                TradeRemoteAccepted = _tradeRemoteAccepted,
                EntrustedPermitExpiresAtUtc = _entrustedPermitExpiresAtUtc,
                EmployeeTemplateId = _employeeTemplateId,
                EmployeeUseOwnerAnchor = _employeeUseOwnerAnchor,
                EmployeeAnchorOffsetX = _employeeAnchorOffsetX,
                EmployeeAnchorOffsetY = _employeeAnchorOffsetY,
                EmployeeWorldX = _employeeWorldX,
                EmployeeWorldY = _employeeWorldY,
                EmployeeHasWorldPosition = _employeeHasWorldPosition,
                EmployeeFlip = _employeeFlip,
                Occupants = _occupants
                    .Select(occupant => new SocialRoomOccupantSnapshot
                    {
                        Name = occupant.Name,
                        Role = occupant.Role,
                        Detail = occupant.Detail,
                        IsReady = occupant.IsReady
                    })
                    .ToList(),
                Items = _items
                    .Select(item => new SocialRoomItemSnapshot
                    {
                        OwnerName = item.OwnerName,
                        ItemName = item.ItemName,
                        ItemId = item.ItemId,
                        Quantity = item.Quantity,
                        MesoAmount = item.MesoAmount,
                        Detail = item.Detail,
                        IsLocked = item.IsLocked,
                        IsClaimed = item.IsClaimed,
                        PacketSlotIndex = item.PacketSlotIndex
                    })
                    .ToList(),
                Notes = _notes.ToList(),
                ChatEntries = _chatEntries
                    .Select(entry => new SocialRoomChatEntrySnapshot
                    {
                        Text = entry.Text,
                        Tone = entry.Tone
                    })
                    .ToList(),
                SavedVisitors = _savedVisitors.ToList(),
                BlockedVisitors = _blockedVisitors.ToList(),
                RemoteInventoryMeso = _remoteInventoryMeso,
                RemoteInventoryEntries = _remoteInventoryEntries
                    .Select(entry => new SocialRoomRemoteInventoryEntrySnapshot
                    {
                        ItemId = entry.ItemId,
                        ItemName = entry.ItemName,
                        Quantity = entry.Quantity
                    })
                    .ToList()
            };
        }

        public void RestoreSnapshot(SocialRoomRuntimeSnapshot snapshot)
        {
            SocialRoomRuntimeSnapshot source = snapshot?.Kind == Kind
                ? snapshot
                : _defaultSnapshot;

            _suspendPersistence = true;
            try
            {
                _inventoryEscrow.Clear();
                _inventoryBackedRows = false;

                RoomTitle = source?.RoomTitle ?? _defaultSnapshot.RoomTitle;
                OwnerName = source?.OwnerName ?? _defaultSnapshot.OwnerName;
                MesoAmount = Math.Max(0, source?.MesoAmount ?? _defaultSnapshot.MesoAmount);
                StatusMessage = source?.StatusMessage ?? _defaultSnapshot.StatusMessage;
                RoomState = source?.RoomState ?? _defaultSnapshot.RoomState;
                ModeName = source?.ModeName ?? _defaultSnapshot.ModeName;
                _miniRoomModeIndex = source?.MiniRoomModeIndex ?? _defaultSnapshot.MiniRoomModeIndex;
                _miniRoomWagerAmount = Math.Max(0, source?.MiniRoomWagerAmount ?? _defaultSnapshot.MiniRoomWagerAmount);
                _miniRoomOmokInProgress = source?.MiniRoomOmokInProgress ?? _defaultSnapshot.MiniRoomOmokInProgress;
                _miniRoomOmokCurrentTurnIndex = Math.Clamp(source?.MiniRoomOmokCurrentTurnIndex ?? _defaultSnapshot.MiniRoomOmokCurrentTurnIndex, 0, 1);
                _miniRoomOmokWinnerIndex = source?.MiniRoomOmokWinnerIndex ?? _defaultSnapshot.MiniRoomOmokWinnerIndex;
                _miniRoomOmokLastMoveX = source?.MiniRoomOmokLastMoveX ?? _defaultSnapshot.MiniRoomOmokLastMoveX;
                _miniRoomOmokLastMoveY = source?.MiniRoomOmokLastMoveY ?? _defaultSnapshot.MiniRoomOmokLastMoveY;
                _miniRoomOmokOwnerStoneValue = NormalizeOmokStoneValue(source?.MiniRoomOmokOwnerStoneValue ?? _defaultSnapshot.MiniRoomOmokOwnerStoneValue, fallback: 1);
                _miniRoomOmokGuestStoneValue = NormalizeOmokStoneValue(source?.MiniRoomOmokGuestStoneValue ?? _defaultSnapshot.MiniRoomOmokGuestStoneValue, fallback: _miniRoomOmokOwnerStoneValue == 1 ? 2 : 1);
                if (_miniRoomOmokOwnerStoneValue == _miniRoomOmokGuestStoneValue)
                {
                    _miniRoomOmokGuestStoneValue = _miniRoomOmokOwnerStoneValue == 1 ? 2 : 1;
                }
                _miniRoomOmokTieRequested = source?.MiniRoomOmokTieRequested ?? _defaultSnapshot.MiniRoomOmokTieRequested;
                _tradeLocalOfferMeso = Math.Max(0, source?.TradeLocalOfferMeso ?? _defaultSnapshot.TradeLocalOfferMeso);
                _tradeRemoteOfferMeso = Math.Max(0, source?.TradeRemoteOfferMeso ?? _defaultSnapshot.TradeRemoteOfferMeso);
                _tradeLocalLocked = source?.TradeLocalLocked ?? _defaultSnapshot.TradeLocalLocked;
                _tradeRemoteLocked = source?.TradeRemoteLocked ?? _defaultSnapshot.TradeRemoteLocked;
                _tradeLocalAccepted = source?.TradeLocalAccepted ?? _defaultSnapshot.TradeLocalAccepted;
                _tradeRemoteAccepted = source?.TradeRemoteAccepted ?? _defaultSnapshot.TradeRemoteAccepted;
                _entrustedPermitExpiresAtUtc = source?.EntrustedPermitExpiresAtUtc ?? _defaultSnapshot.EntrustedPermitExpiresAtUtc;
                _employeeTemplateId = Math.Max(0, source?.EmployeeTemplateId ?? _defaultSnapshot.EmployeeTemplateId);
                _employeeUseOwnerAnchor = source?.EmployeeUseOwnerAnchor ?? _defaultSnapshot.EmployeeUseOwnerAnchor;
                _employeeAnchorOffsetX = source?.EmployeeAnchorOffsetX ?? _defaultSnapshot.EmployeeAnchorOffsetX;
                _employeeAnchorOffsetY = source?.EmployeeAnchorOffsetY ?? _defaultSnapshot.EmployeeAnchorOffsetY;
                _employeeWorldX = source?.EmployeeWorldX ?? _defaultSnapshot.EmployeeWorldX;
                _employeeWorldY = source?.EmployeeWorldY ?? _defaultSnapshot.EmployeeWorldY;
                _employeeHasWorldPosition = source?.EmployeeHasWorldPosition ?? _defaultSnapshot.EmployeeHasWorldPosition;
                _employeeFlip = source?.EmployeeFlip ?? _defaultSnapshot.EmployeeFlip;
                _remoteInventoryMeso = Math.Max(0, source?.RemoteInventoryMeso ?? _defaultSnapshot.RemoteInventoryMeso);

                _occupants.Clear();
                foreach (SocialRoomOccupantSnapshot occupant in (source?.Occupants?.Count > 0 ? source.Occupants : _defaultSnapshot.Occupants) ?? Enumerable.Empty<SocialRoomOccupantSnapshot>())
                {
                    _occupants.Add(new SocialRoomOccupant(occupant.Name, occupant.Role, occupant.Detail, occupant.IsReady));
                }

                _items.Clear();
                foreach (SocialRoomItemSnapshot item in (source?.Items?.Count > 0 ? source.Items : _defaultSnapshot.Items) ?? Enumerable.Empty<SocialRoomItemSnapshot>())
                {
                    _items.Add(new SocialRoomItemEntry(item.OwnerName, item.ItemName, item.Quantity, item.MesoAmount, item.Detail, item.IsLocked, item.IsClaimed, item.ItemId, item.PacketSlotIndex));
                }

                _notes.Clear();
                foreach (string note in (source?.Notes?.Count > 0 ? source.Notes : _defaultSnapshot.Notes) ?? Enumerable.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(note))
                    {
                        _notes.Add(note);
                    }
                }

                _chatEntries.Clear();
                foreach (SocialRoomChatEntrySnapshot entry in (source?.ChatEntries?.Count > 0 ? source.ChatEntries : _defaultSnapshot.ChatEntries) ?? Enumerable.Empty<SocialRoomChatEntrySnapshot>())
                {
                    if (!string.IsNullOrWhiteSpace(entry?.Text))
                    {
                        _chatEntries.Add(new SocialRoomChatEntry(entry.Text, entry.Tone));
                    }
                }

                _savedVisitors.Clear();
                foreach (string visitor in source?.SavedVisitors ?? Enumerable.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(visitor))
                    {
                        _savedVisitors.Add(visitor);
                    }
                }

                _blockedVisitors.Clear();
                foreach (string visitor in source?.BlockedVisitors ?? Enumerable.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(visitor))
                    {
                        _blockedVisitors.Add(visitor);
                    }
                }

                int[] boardSource = ((source?.MiniRoomOmokBoard?.Count ?? 0) == _miniRoomOmokBoard.Length
                    ? source.MiniRoomOmokBoard
                    : _defaultSnapshot.MiniRoomOmokBoard)?.ToArray()
                    ?? new int[_miniRoomOmokBoard.Length];
                Array.Copy(boardSource, _miniRoomOmokBoard, _miniRoomOmokBoard.Length);

                _remoteInventoryEntries.Clear();
                IEnumerable<SocialRoomRemoteInventoryEntrySnapshot> remoteEntries = source?.RemoteInventoryEntries?.Count > 0
                    ? source.RemoteInventoryEntries
                    : _defaultSnapshot.RemoteInventoryEntries;
                foreach (SocialRoomRemoteInventoryEntrySnapshot entry in remoteEntries ?? Enumerable.Empty<SocialRoomRemoteInventoryEntrySnapshot>())
                {
                    if (entry == null || entry.ItemId <= 0 || entry.Quantity <= 0)
                    {
                        continue;
                    }

                    _remoteInventoryEntries.Add(new SocialRoomRemoteInventoryEntry(entry.ItemId, entry.ItemName, entry.Quantity));
                }

                RefreshRemoteInventoryNotes();
            }
            finally
            {
                _suspendPersistence = false;
            }
        }

        public void ResetToDefaults()
        {
            RestoreSnapshot(_defaultSnapshot);
        }

        public string DescribeStatus()
        {
            RefreshTimedState(DateTime.UtcNow);
            return Kind switch
            {
                SocialRoomKind.MiniRoom => $"{RoomTitle}: state={RoomState}, mode={ModeName}, wager={_miniRoomWagerAmount:N0}, occupants={_occupants.Count}/{Capacity}, omokTurn={ResolveOmokTurnName()}, winner={ResolveOmokWinnerName()}",
                SocialRoomKind.PersonalShop => $"{RoomTitle}: state={RoomState}, listed={_items.Count}, savedVisitors={_savedVisitors.Count}, blacklist={_blockedVisitors.Count}, ledger={MesoAmount:N0}, employee={DescribeEmployeeState()}, packet={_lastPacketOwnerSummary}",
                SocialRoomKind.EntrustedShop => $"{RoomTitle}: state={RoomState}, listed={_items.Count}, ledger={MesoAmount:N0}, permit={FormatPermitStatus(DateTime.UtcNow)}, employee={DescribeEmployeeState()}, packet={_lastPacketOwnerSummary}",
                SocialRoomKind.TradingRoom => $"{RoomTitle}: state={RoomState}, localMeso={MesoAmount:N0}, remoteMeso={_tradeRemoteOfferMeso:N0}, remoteWallet={_remoteInventoryMeso:N0}, remoteItems={_remoteInventoryEntries.Sum(entry => entry.Quantity)}, lock={FormatTradePartyState(_tradeLocalLocked, _tradeRemoteLocked)}, accept={FormatTradePartyState(_tradeLocalAccepted, _tradeRemoteAccepted)}, escrowRows={_inventoryEscrow.Count}",
                _ => RoomState
            };
        }

        public string DescribePacketOwnerStatus()
        {
            return _lastPacketOwnerSummary;
        }

        public void RefreshTimedState(DateTime utcNow)
        {
            if (Kind == SocialRoomKind.EntrustedShop)
            {
                string previousState = RoomState;
                string previousStatus = StatusMessage;
                UpdateEntrustedPermitState(utcNow);
                if (!string.Equals(previousState, RoomState, StringComparison.Ordinal) ||
                    !string.Equals(previousStatus, StatusMessage, StringComparison.Ordinal))
                {
                    PersistState();
                }
            }
        }

        public SocialRoomFieldActorSnapshot GetFieldActorSnapshot(DateTime utcNow)
        {
            RefreshTimedState(utcNow);

            if (Kind == SocialRoomKind.PersonalShop)
            {
                bool usesCashEmployee = _employeeTemplateId > 0;
                return new SocialRoomFieldActorSnapshot(
                    Kind,
                    usesCashEmployee ? SocialRoomFieldActorTemplate.CashEmployee : SocialRoomFieldActorTemplate.Merchant,
                    string.IsNullOrWhiteSpace(RoomTitle) ? "Hired Merchant" : RoomTitle,
                    $"{OwnerName} | {RoomState}",
                    $"{(usesCashEmployee ? "cash" : "merchant")}|{_employeeTemplateId}|{ModeName}|{RoomState}",
                    templateId: _employeeTemplateId,
                    useOwnerAnchor: _employeeUseOwnerAnchor,
                    anchorOffsetX: _employeeAnchorOffsetX,
                    anchorOffsetY: _employeeAnchorOffsetY,
                    worldX: _employeeWorldX,
                    worldY: _employeeWorldY,
                    hasWorldPosition: _employeeHasWorldPosition,
                    flip: _employeeFlip);
            }

            if (Kind != SocialRoomKind.EntrustedShop)
            {
                return null;
            }

            string permitStatus = FormatPermitStatus(utcNow);
            if (IsEntrustedPermitExpired(utcNow))
            {
                return new SocialRoomFieldActorSnapshot(
                    Kind,
                    SocialRoomFieldActorTemplate.StoreBanker,
                    "Contract expired",
                    "Claim items and mesos at Fredrick.",
                    $"expired|{RoomState}|{StatusMessage}",
                    useOwnerAnchor: _employeeUseOwnerAnchor,
                    anchorOffsetX: _employeeAnchorOffsetX,
                    anchorOffsetY: _employeeAnchorOffsetY,
                    worldX: _employeeWorldX,
                    worldY: _employeeWorldY,
                    hasWorldPosition: _employeeHasWorldPosition,
                    flip: _employeeFlip);
            }

            bool usesCashEmployeeForEntrusted = _employeeTemplateId > 0;
            return new SocialRoomFieldActorSnapshot(
                Kind,
                usesCashEmployeeForEntrusted ? SocialRoomFieldActorTemplate.CashEmployee : SocialRoomFieldActorTemplate.Merchant,
                string.IsNullOrWhiteSpace(RoomTitle) ? "Entrusted Shop" : RoomTitle,
                $"{OwnerName} | {RoomState} | {permitStatus}",
                $"{(usesCashEmployeeForEntrusted ? "cash" : "merchant")}|{_employeeTemplateId}|{ModeName}|{RoomState}|{permitStatus}",
                templateId: _employeeTemplateId,
                useOwnerAnchor: _employeeUseOwnerAnchor,
                anchorOffsetX: _employeeAnchorOffsetX,
                anchorOffsetY: _employeeAnchorOffsetY,
                worldX: _employeeWorldX,
                worldY: _employeeWorldY,
                hasWorldPosition: _employeeHasWorldPosition,
                flip: _employeeFlip);
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
                    return ToggleTradeLock(out message);
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

        public bool TryDispatchPacketBytes(byte[] packetBytes, int tickCount, out string message)
        {
            message = null;
            byte[] payload = NormalizePacketPayload(packetBytes);
            if (payload.Length == 0)
            {
                message = "Social-room packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                byte packetType = reader.ReadByte();
                return Kind switch
                {
                    SocialRoomKind.MiniRoom => TryDispatchMiniRoomPacket(reader, packetType, tickCount, out message),
                    SocialRoomKind.PersonalShop => TryDispatchPersonalShopDialogPacket(reader, packetType, tickCount, out message),
                    SocialRoomKind.EntrustedShop => TryDispatchEntrustedShopDialogPacket(reader, packetType, tickCount, out message),
                    SocialRoomKind.TradingRoom => TryDispatchTradingRoomPacket(payload, reader, packetType, out message),
                    _ => FailPacket(packetType, out message)
                };
            }
            catch (EndOfStreamException)
            {
                message = $"Social-room packet ended unexpectedly: {BitConverter.ToString(payload)}";
                return false;
            }
        }

        private bool TryDispatchPersonalShopDialogPacket(PacketReader reader, byte packetType, int tickCount, out string message)
        {
            if (!TryDispatchPersonalShopPacket(reader, packetType, out message))
            {
                TrackPacketOwnerSummary("CPersonalShopDlg::OnPacket", packetType, tickCount, handled: false, message);
                return false;
            }

            TrackPacketOwnerSummary("CPersonalShopDlg::OnPacket", packetType, tickCount, handled: true, message);
            return true;
        }

        private bool TryDispatchEntrustedShopDialogPacket(PacketReader reader, byte packetType, int tickCount, out string message)
        {
            bool handled;
            string detail;
            string forwardedOwner = "CPersonalShopDlg::OnPacket";
            switch (packetType)
            {
                case EntrustedShopArrangeItemResultPacketType:
                    ApplyEntrustedArrangeResult(reader.ReadInt());
                    handled = true;
                    detail = $"{StatusMessage} Forwarded through {forwardedOwner}.";
                    break;
                case EntrustedShopWithdrawAllResultPacketType:
                    ApplyEntrustedWithdrawAllResult(reader.ReadByte());
                    handled = true;
                    detail = $"{StatusMessage} Forwarded through {forwardedOwner}.";
                    break;
                case EntrustedShopWithdrawMoneyResultPacketType:
                    ApplyEntrustedWithdrawMoneyResult();
                    handled = true;
                    detail = $"{StatusMessage} Forwarded through {forwardedOwner}.";
                    break;
                case EntrustedShopVisitListResultPacketType:
                    handled = TryApplyEntrustedVisitListPacket(reader, out detail);
                    if (handled)
                    {
                        detail = $"{detail} Forwarded through {forwardedOwner}.";
                    }

                    break;
                case EntrustedShopBlackListResultPacketType:
                    handled = TryApplyEntrustedBlackListPacket(reader, out detail);
                    if (handled)
                    {
                        detail = $"{detail} Forwarded through {forwardedOwner}.";
                    }

                    break;
                case PersonalShopBuyResultPacketType:
                case PersonalShopBasePacketType:
                case PersonalShopSoldItemResultPacketType:
                case PersonalShopMoveItemToInventoryPacketType:
                    handled = TryDispatchPersonalShopPacket(reader, packetType, out detail);
                    if (handled)
                    {
                        detail = $"CEntrustedShopDlg::OnPacket forwarded packet {packetType} to {forwardedOwner}. {detail}";
                    }

                    break;
                default:
                    detail = $"Entrusted-shop packet {packetType} is not modeled for the dialog-owned dispatcher.";
                    TrackPacketOwnerSummary("CEntrustedShopDlg::OnPacket", packetType, tickCount, handled: false, detail);
                    message = detail;
                    return false;
            }

            TrackPacketOwnerSummary("CEntrustedShopDlg::OnPacket -> CPersonalShopDlg::OnPacket", packetType, tickCount, handled, detail);
            message = detail;
            return handled;
        }

        private bool TryDispatchMiniRoomPacket(PacketReader reader, byte packetType, int tickCount, out string message)
        {
            if (!IsMiniRoomOmokActive)
            {
                message = $"MiniRoom packet {packetType} is only modeled while the room is in Omok mode.";
                return false;
            }

            switch (packetType)
            {
                case OmokReadyPacketType:
                    SetMiniRoomGuestReady(isReady: true, persistState: true);
                    message = StatusMessage;
                    return true;
                case OmokCancelReadyPacketType:
                    SetMiniRoomGuestReady(isReady: false, persistState: true);
                    message = StatusMessage;
                    return true;
                case OmokStartPacketType:
                {
                    int firstTurnSeat = reader.ReadByte();
                    StartMiniRoomOmokSessionFromPacket(firstTurnSeat);
                    message = StatusMessage;
                    return true;
                }
                case OmokPutStonePacketType:
                {
                    int x = reader.ReadInt();
                    int y = reader.ReadInt();
                    int stoneValue = NormalizeOmokStoneValue(reader.ReadByte(), fallback: ResolveOmokStoneValueForSeat(_miniRoomOmokCurrentTurnIndex));
                    return TryApplyOmokStonePacket(x, y, stoneValue, out message);
                }
                case OmokPutStoneErrorPacketType:
                {
                    int errorCode = reader.ReadByte();
                    _miniRoomOmokTieRequested = false;
                    StatusMessage = errorCode == 67
                        ? "The Omok stone packet was rejected because that point is already occupied."
                        : $"The Omok stone packet was rejected (code {errorCode}).";
                    AddMiniRoomSystemMessage($"System : {StatusMessage}", isWarning: true);
                    SyncMiniRoomOmokPresentation();
                    PersistState();
                    message = StatusMessage;
                    return true;
                }
                case OmokTimeOverPacketType:
                    _miniRoomOmokCurrentTurnIndex = Math.Clamp((int)reader.ReadByte(), 0, 1);
                    _miniRoomOmokTieRequested = false;
                    RoomState = _miniRoomOmokInProgress ? "Omok in progress" : RoomState;
                    StatusMessage = $"{ResolveMiniRoomSeatName(_miniRoomOmokCurrentTurnIndex)}'s Omok turn.";
                    SyncMiniRoomOmokPresentation();
                    PersistState();
                    message = StatusMessage;
                    return true;
                case OmokTieRequestPacketType:
                    _miniRoomOmokTieRequested = true;
                    StatusMessage = $"{ResolveRemoteTraderName()} requested an Omok draw.";
                    AddMiniRoomSystemMessage($"System : {StatusMessage}");
                    SyncMiniRoomOmokPresentation();
                    PersistState();
                    message = StatusMessage;
                    return true;
                case OmokTieResultPacketType:
                    _miniRoomOmokTieRequested = false;
                    StatusMessage = "The Omok draw request was answered. Waiting for the next result packet.";
                    AddMiniRoomSystemMessage($"System : {StatusMessage}");
                    SyncMiniRoomOmokPresentation();
                    PersistState();
                    message = StatusMessage;
                    return true;
                case OmokRetreatRequestPacketType:
                    StatusMessage = $"{ResolveRemoteTraderName()} requested an Omok retreat.";
                    AddMiniRoomSystemMessage($"System : {StatusMessage}", isWarning: true);
                    PersistState();
                    message = StatusMessage;
                    return true;
                case OmokRetreatResultPacketType:
                {
                    bool accepted = reader.ReadByte() != 0;
                    if (!accepted)
                    {
                        StatusMessage = "The Omok retreat request was declined.";
                        AddMiniRoomSystemMessage($"System : {StatusMessage}", isWarning: true);
                        PersistState();
                        message = StatusMessage;
                        return true;
                    }

                    int removedStoneCount = reader.ReadByte();
                    int nextTurnSeat = reader.ReadByte();
                    ApplyOmokRetreatPacket(removedStoneCount, nextTurnSeat);
                    message = StatusMessage;
                    return true;
                }
                case OmokGameResultPacketType:
                {
                    int resultType = reader.ReadByte();
                    int winnerSeat = resultType == 1 ? -1 : reader.ReadByte();
                    ApplyOmokGameResultPacket(resultType, winnerSeat);
                    message = StatusMessage;
                    return true;
                }
                default:
                    return FailPacket(packetType, out message);
            }
        }

        private bool TryDispatchPersonalShopPacket(PacketReader reader, byte packetType, out string message)
        {
            switch (packetType)
            {
                case PersonalShopBuyResultPacketType:
                    ApplyPersonalShopBuyResult(reader.ReadByte());
                    message = StatusMessage;
                    return true;
                case PersonalShopSoldItemResultPacketType:
                    return TryApplyPersonalShopSoldItemPacket(reader, out message);
                case PersonalShopMoveItemToInventoryPacketType:
                    return TryApplyPersonalShopMoveItemPacket(reader, out message);
                case PersonalShopBasePacketType:
                    StatusMessage = "Received a personal-shop base lifecycle packet through the dialog-owned owner. The deeper base dialog flow is still partial.";
                    PersistState();
                    message = StatusMessage;
                    return true;
                default:
                    return FailPacket(packetType, out message);
            }
        }

        private bool TryDispatchEntrustedShopPacket(PacketReader reader, byte packetType, out string message)
        {
            switch (packetType)
            {
                case EntrustedShopArrangeItemResultPacketType:
                    ApplyEntrustedArrangeResult(reader.ReadInt());
                    message = StatusMessage;
                    return true;
                case EntrustedShopWithdrawAllResultPacketType:
                    ApplyEntrustedWithdrawAllResult(reader.ReadByte());
                    message = StatusMessage;
                    return true;
                case EntrustedShopWithdrawMoneyResultPacketType:
                    ApplyEntrustedWithdrawMoneyResult();
                    message = StatusMessage;
                    return true;
                case EntrustedShopVisitListResultPacketType:
                    return TryApplyEntrustedVisitListPacket(reader, out message);
                case EntrustedShopBlackListResultPacketType:
                    return TryApplyEntrustedBlackListPacket(reader, out message);
                case PersonalShopBuyResultPacketType:
                case PersonalShopBasePacketType:
                case PersonalShopSoldItemResultPacketType:
                case PersonalShopMoveItemToInventoryPacketType:
                    return TryDispatchPersonalShopPacket(reader, packetType, out message);
                default:
                    return FailPacket(packetType, out message);
            }
        }

        private bool TryDispatchTradingRoomPacket(byte[] payload, PacketReader reader, byte packetType, out string message)
        {
            switch (packetType)
            {
                case TradingRoomPutMoneyPacketType:
                {
                    int traderIndex = reader.ReadByte();
                    int offeredMeso = reader.ReadInt();
                    ApplyTradingRoomMesoPacket(traderIndex, offeredMeso);
                    message = StatusMessage;
                    return true;
                }
                case TradingRoomPutItemPacketType:
                    return TryApplyTradingRoomItemPacket(payload, out message);
                case TradingRoomTradePacketType:
                    ApplyTradingRoomTradePacket();
                    message = StatusMessage;
                    return true;
                case TradingRoomExceedLimitPacketType:
                    ApplyTradingRoomExceedLimitPacket();
                    message = StatusMessage;
                    return true;
                default:
                    return FailPacket(packetType, out message);
            }
        }

        private bool TryApplyTradingRoomItemPacket(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 5)
            {
                message = "Trading-room put-item packet is too short to decode.";
                return false;
            }

            int traderIndex = payload[1];
            int slotIndex = Math.Max(1, (int)payload[2]);
            if (!TryDecodePacketOwnedTradeItem(payload.AsSpan(3), out PacketOwnedTradeItem item, out string error))
            {
                message = error;
                return false;
            }

            ApplyTradingRoomItemPacket(traderIndex, slotIndex, item);
            message = StatusMessage;
            return true;
        }

        private void ApplyTradingRoomTradePacket()
        {
            bool localAlreadyLocked = _tradeLocalLocked;
            _tradeRemoteLocked = true;
            if (localAlreadyLocked)
            {
                _tradeLocalAccepted = true;
                _tradeRemoteAccepted = true;
                RoomState = "Awaiting settlement";
                StatusMessage = "Trading-room packet requested CRC settlement for a fully locked trade.";
            }
            else
            {
                _tradeLocalAccepted = false;
                _tradeRemoteAccepted = false;
                RoomState = "Negotiating";
                StatusMessage = $"{ResolveRemoteTraderName()} locked the trade and requested CRC verification.";
            }

            RefreshTradeOccupantsAndRows();
            PersistState();
        }

        private void ApplyTradingRoomExceedLimitPacket()
        {
            _tradeLocalLocked = false;
            _tradeLocalAccepted = false;
            _tradeRemoteAccepted = false;
            PersistState();
            RoomState = "Negotiating";
            RefreshTradeOccupantsAndRows();
            StatusMessage = "Trading-room packet reported a limit failure and unlocked the local trade button state.";
            PersistState();
        }

        private void ApplyTradingRoomItemPacket(int traderIndex, int slotIndex, PacketOwnedTradeItem item)
        {
            ClearTradeHandshake();
            string ownerName = ResolveTradeOwnerName(traderIndex);
            string ownerLabel = traderIndex == 0 ? "Owner" : "Guest";
            SocialRoomItemEntry entry = FindTradingRoomPacketEntry(ownerName, slotIndex);
            if (entry == null)
            {
                entry = new SocialRoomItemEntry(
                    ownerName,
                    ResolveItemLabel(item.ItemId),
                    item.Quantity,
                    mesoAmount: 0,
                    detail: $"{ownerLabel} offer | Packet slot {slotIndex} | {item.InventoryType}",
                    itemId: item.ItemId,
                    packetSlotIndex: slotIndex);
                _items.Add(entry);
            }
            else
            {
                entry.UpdatePacketIdentity(item.ItemId, slotIndex);
                entry.Update($"{ownerLabel} offer | Packet slot {slotIndex} | {item.InventoryType}", item.Quantity, 0, false, false);
            }

            SortTradeRoomItems();
            RoomState = "Negotiating";
            RefreshTradeOccupantsAndRows();
            StatusMessage = $"{ownerName} placed packet-backed {ResolveItemLabel(item.ItemId)} x{item.Quantity} into trade slot {slotIndex}.";
            PersistState();
        }

        private SocialRoomItemEntry FindTradingRoomPacketEntry(string ownerName, int slotIndex)
        {
            SocialRoomItemEntry exact = _items.FirstOrDefault(item =>
                string.Equals(item.OwnerName, ownerName, StringComparison.OrdinalIgnoreCase) &&
                item.PacketSlotIndex == slotIndex);
            if (exact != null)
            {
                return exact;
            }

            List<SocialRoomItemEntry> ownerEntries = _items
                .Where(item => string.Equals(item.OwnerName, ownerName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.PacketSlotIndex ?? int.MaxValue)
                .ThenBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            int ordinalIndex = slotIndex - 1;
            return ordinalIndex >= 0 && ordinalIndex < ownerEntries.Count
                ? ownerEntries[ordinalIndex]
                : null;
        }

        private void SortTradeRoomItems()
        {
            _items.Sort((left, right) =>
            {
                bool leftLocal = string.Equals(left.OwnerName, OwnerName, StringComparison.OrdinalIgnoreCase);
                bool rightLocal = string.Equals(right.OwnerName, OwnerName, StringComparison.OrdinalIgnoreCase);
                int ownerComparison = leftLocal == rightLocal ? 0 : leftLocal ? -1 : 1;
                if (ownerComparison != 0)
                {
                    return ownerComparison;
                }

                int slotComparison = Nullable.Compare(left.PacketSlotIndex, right.PacketSlotIndex);
                if (slotComparison != 0)
                {
                    return slotComparison;
                }

                return string.Compare(left.ItemName, right.ItemName, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool TryDecodePacketOwnedTradeItem(ReadOnlySpan<byte> payload, out PacketOwnedTradeItem item, out string error)
        {
            item = default;
            error = null;
            if (payload.Length < 5)
            {
                error = "Trading-room item payload is too short to contain a GW_ItemSlotBase body.";
                return false;
            }

            byte slotType = payload[0];
            if (slotType is not 1 and not 2 and not 3)
            {
                error = $"Trading-room item payload used unsupported GW_ItemSlotBase type {slotType}.";
                return false;
            }

            if (!TryFindPacketOwnedItemId(payload.Slice(1), out int itemId, out int itemIdOffset))
            {
                error = "Trading-room item payload did not contain a recognizable MapleStory item id.";
                return false;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            if (inventoryType == InventoryType.NONE)
            {
                error = $"Trading-room item payload resolved unsupported item id {itemId}.";
                return false;
            }

            int quantity = slotType == 1
                ? 1
                : ResolvePacketOwnedTradeQuantity(payload.Slice(1), itemIdOffset);
            item = new PacketOwnedTradeItem(slotType, itemId, quantity, inventoryType);
            return true;
        }

        private static bool TryFindPacketOwnedItemId(ReadOnlySpan<byte> payload, out int itemId, out int itemIdOffset)
        {
            itemId = 0;
            itemIdOffset = -1;

            foreach (int offset in EnumerateLikelyPacketItemIdOffsets(payload.Length))
            {
                if (offset < 0 || offset + sizeof(int) > payload.Length)
                {
                    continue;
                }

                int candidate = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
                if (candidate <= 0 || InventoryItemMetadataResolver.ResolveInventoryType(candidate) == InventoryType.NONE)
                {
                    continue;
                }

                itemId = candidate;
                itemIdOffset = offset;
                return true;
            }

            return false;
        }

        private static IEnumerable<int> EnumerateLikelyPacketItemIdOffsets(int payloadLength)
        {
            int[] preferredOffsets = { 8, 0, 4, 12, 16, 20, 24, 28, 32 };
            foreach (int offset in preferredOffsets)
            {
                if (offset + sizeof(int) <= payloadLength)
                {
                    yield return offset;
                }
            }

            for (int offset = 0; offset + sizeof(int) <= payloadLength && offset <= 40; offset++)
            {
                if (!preferredOffsets.Contains(offset))
                {
                    yield return offset;
                }
            }
        }

        private static int ResolvePacketOwnedTradeQuantity(ReadOnlySpan<byte> payload, int itemIdOffset)
        {
            int[] candidateOffsets =
            {
                itemIdOffset + sizeof(int),
                itemIdOffset + sizeof(int) + sizeof(short),
                itemIdOffset - sizeof(short),
                itemIdOffset + 8,
                0
            };

            foreach (int offset in candidateOffsets)
            {
                if (offset < 0 || offset + sizeof(short) > payload.Length)
                {
                    continue;
                }

                short quantity = BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(offset, sizeof(short)));
                if (quantity > 0 && quantity <= short.MaxValue)
                {
                    return quantity;
                }
            }

            for (int offset = 0; offset + sizeof(short) <= payload.Length && offset <= 24; offset++)
            {
                short quantity = BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(offset, sizeof(short)));
                if (quantity > 0 && quantity <= short.MaxValue)
                {
                    return quantity;
                }
            }

            return 1;
        }

        private string ResolveTradeOwnerName(int traderIndex)
        {
            return traderIndex == 0 ? OwnerName : ResolveRemoteTraderName();
        }

        private static string ResolveItemLabel(int itemId)
        {
            if (itemId <= 0)
            {
                return "Unknown item";
            }

            if (global::HaCreator.Program.InfoManager?.ItemNameCache != null &&
                global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo) &&
                !string.IsNullOrWhiteSpace(itemInfo?.Item2))
            {
                return itemInfo.Item2.Trim();
            }

            return itemId.ToString();
        }

        private static bool FailPacket(byte packetType, out string message)
        {
            message = $"Social-room packet {packetType} is not modeled for this room.";
            return false;
        }

        private void TrackPacketOwnerSummary(string ownerName, byte packetType, int tickCount, bool handled, string detail)
        {
            string result = handled ? "handled" : "rejected";
            string suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail}";
            _lastPacketOwnerSummary = $"{ownerName} {result} type {packetType} at tick {tickCount}{suffix}";
        }

        private string BuildDefaultPacketOwnerSummary()
        {
            return Kind switch
            {
                SocialRoomKind.PersonalShop => "CPersonalShopDlg::OnPacket idle.",
                SocialRoomKind.EntrustedShop => "CEntrustedShopDlg::OnPacket idle.",
                _ => "No dedicated packet-owned dialog for this room."
            };
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
            CharacterBuild ownerBuild = null,
            CharacterBuild guestBuild = null,
            IReadOnlyList<SocialRoomOccupant> extraOccupants = null)
        {
            RoomTitle = string.IsNullOrWhiteSpace(roomTitle) ? "Mini Room" : roomTitle.Trim();
            OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "Player" : ownerName.Trim();
            ModeName = "Match Cards";
            StatusMessage = statusMessage ?? string.Empty;
            RoomState = roomState ?? string.Empty;
            MesoAmount = _miniRoomWagerAmount > 0 ? _miniRoomWagerAmount * 2 : 0;

            EnsureMiniRoomOccupant(0, OwnerName, SocialRoomOccupantRole.Owner, ownerDetail ?? BuildMiniRoomDetail(ownerScore, currentTurnIndex == 0, "Host seat"), ownerReady, ownerBuild);
            EnsureMiniRoomOccupant(1, guestName, SocialRoomOccupantRole.Guest, guestDetail ?? BuildMiniRoomDetail(guestScore, currentTurnIndex == 1, "Guest seat"), guestReady, guestBuild);

            int occupantCount = 2;
            if (extraOccupants != null)
            {
                foreach (SocialRoomOccupant occupant in extraOccupants)
                {
                    if (occupant == null)
                    {
                        continue;
                    }

                    EnsureMiniRoomOccupant(occupantCount, occupant.Name, occupant.Role, occupant.Detail, occupant.IsReady, occupant.AvatarBuild);
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

            PersistState();
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

            PersistState();
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

        private void SetMiniRoomGuestReady(bool isReady, bool persistState)
        {
            if (Kind != SocialRoomKind.MiniRoom || _occupants.Count < 2)
            {
                return;
            }

            SocialRoomOccupant guest = _occupants[1];
            guest.Update(isReady ? "Ready for the first turn" : "Waiting to ready", isReady);
            RoomState = isReady ? "Ready check complete" : "Waiting for ready check";
            StatusMessage = isReady
                ? $"{guest.Name} is ready. Start {ModeName} when you want to open the board."
                : $"{guest.Name} stepped back from the ready check.";
            if (persistState)
            {
                PersistState();
            }
        }

        private void StartMiniRoomOmokSessionFromPacket(int firstTurnSeat)
        {
            EnsureMiniRoomOccupant(0, OwnerName, SocialRoomOccupantRole.Owner, BuildOmokSeatDetail(0, "Host seat"), false);
            EnsureMiniRoomOccupant(1, ResolveMiniRoomSeatName(1), SocialRoomOccupantRole.Guest, BuildOmokSeatDetail(1, "Guest seat"), false);
            SetOmokSeatStoneValues(Math.Clamp(firstTurnSeat, 0, 1));
            ResetOmokBoard();
            _miniRoomOmokInProgress = true;
            _miniRoomOmokCurrentTurnIndex = Math.Clamp(firstTurnSeat, 0, 1);
            _miniRoomOmokWinnerIndex = -1;
            _miniRoomOmokTieRequested = false;
            RoomState = "Omok in progress";
            StatusMessage = $"{ResolveMiniRoomSeatName(_miniRoomOmokCurrentTurnIndex)} opened the Omok round from a packet-backed start.";
            SyncMiniRoomOmokPresentation();
            AddMiniRoomSystemMessage("System : Omok round started from packet state.");
            PersistState();
        }

        private bool TryApplyOmokStonePacket(int x, int y, int stoneValue, out string message)
        {
            message = null;
            if (!TryValidateOmokCoordinates(x, y, out message))
            {
                return false;
            }

            int boardIndex = GetOmokBoardIndex(x, y);
            if (_miniRoomOmokBoard[boardIndex] != 0)
            {
                message = $"Omok point {x},{y} is already occupied.";
                return false;
            }

            int seatIndex = ResolveOmokSeatIndexByStoneValue(stoneValue);
            if (seatIndex < 0)
            {
                seatIndex = _miniRoomOmokCurrentTurnIndex;
            }

            _miniRoomOmokInProgress = true;
            _miniRoomOmokBoard[boardIndex] = stoneValue;
            _miniRoomOmokLastMoveX = x;
            _miniRoomOmokLastMoveY = y;
            _miniRoomOmokTieRequested = false;
            AddMiniRoomSpeakerMessage(ResolveMiniRoomSeatName(seatIndex), $"placed a {ResolveOmokStoneName(stoneValue)} stone at {x},{y}.", seatIndex == 0);

            if (HasFiveInRow(x, y, stoneValue))
            {
                _miniRoomOmokInProgress = false;
                _miniRoomOmokWinnerIndex = seatIndex;
                RoomState = "Omok result";
                StatusMessage = $"{ResolveMiniRoomSeatName(seatIndex)} completed five in a row and won the Omok round.";
            }
            else
            {
                _miniRoomOmokCurrentTurnIndex = seatIndex == 0 ? 1 : 0;
                RoomState = "Omok in progress";
                StatusMessage = $"{ResolveMiniRoomSeatName(_miniRoomOmokCurrentTurnIndex)}'s Omok turn.";
            }

            SyncMiniRoomOmokPresentation();
            PersistState();
            message = StatusMessage;
            return true;
        }

        private void ApplyOmokRetreatPacket(int removedStoneCount, int nextTurnSeat)
        {
            int remainingToRemove = Math.Max(0, removedStoneCount);
            if (remainingToRemove > 0)
            {
                for (int y = MiniRoomOmokBoardSize - 1; y >= 0 && remainingToRemove > 0; y--)
                {
                    for (int x = MiniRoomOmokBoardSize - 1; x >= 0 && remainingToRemove > 0; x--)
                    {
                        int boardIndex = GetOmokBoardIndex(x, y);
                        if (_miniRoomOmokBoard[boardIndex] == 0)
                        {
                            continue;
                        }

                        _miniRoomOmokBoard[boardIndex] = 0;
                        remainingToRemove--;
                    }
                }
            }

            _miniRoomOmokCurrentTurnIndex = Math.Clamp(nextTurnSeat, 0, 1);
            _miniRoomOmokWinnerIndex = -1;
            _miniRoomOmokTieRequested = false;
            RoomState = "Omok in progress";
            StatusMessage = removedStoneCount > 0
                ? $"Omok retreat packet removed {removedStoneCount} recent stone(s)."
                : "Omok retreat packet resolved without removing stones.";
            SyncMiniRoomOmokPresentation();
            PersistState();
        }

        private void ApplyOmokGameResultPacket(int resultType, int winnerSeat)
        {
            _miniRoomOmokInProgress = false;
            _miniRoomOmokTieRequested = false;
            if (resultType == 1)
            {
                _miniRoomOmokWinnerIndex = -1;
                RoomState = "Omok draw";
                StatusMessage = "The Omok round ended in a draw.";
            }
            else
            {
                _miniRoomOmokWinnerIndex = Math.Clamp(winnerSeat, 0, 1);
                RoomState = "Omok result";
                StatusMessage = $"{ResolveMiniRoomSeatName(_miniRoomOmokWinnerIndex)} won the Omok round from packet state.";
            }

            SyncMiniRoomOmokPresentation();
            PersistState();
        }

        private void ApplyPersonalShopBuyResult(int resultCode)
        {
            RoomState = "Buy result received";
            StatusMessage = resultCode switch
            {
                0 => "Personal-shop buy request resolved without a visible state change.",
                1 => "Personal-shop buy request was blocked by the server result.",
                _ => $"Personal-shop buy result code {resultCode} was applied."
            };
            PersistState();
        }

        private bool TryApplyPersonalShopSoldItemPacket(PacketReader reader, out string message)
        {
            int soldIndex = reader.ReadByte();
            int purchasedBundles = reader.ReadShort();
            string buyerName = NormalizeName(reader.ReadMapleString());
            List<SocialRoomItemEntry> availableEntries = _items.Where(item => !item.IsClaimed).ToList();
            if (availableEntries.Count == 0)
            {
                message = "No personal-shop bundle is available to apply the sold-item packet.";
                return false;
            }

            SocialRoomItemEntry entry = soldIndex >= 0 && soldIndex < availableEntries.Count
                ? availableEntries[soldIndex]
                : availableEntries[0];
            int quantitySold = Math.Max(1, purchasedBundles) * Math.Max(1, entry.Quantity);
            int mesosReceived = Math.Max(0, Math.Max(1, purchasedBundles) * entry.MesoAmount);
            entry.Update($"{entry.Detail} | Packet sold to {buyerName}", quantitySold, entry.MesoAmount, false, true);
            MesoAmount += mesosReceived;
            _inventoryEscrow.RemoveAll(escrow => ReferenceEquals(escrow.Entry, entry));
            _occupants.RemoveAll(occupant => string.Equals(occupant.Name, buyerName, StringComparison.OrdinalIgnoreCase));
            _occupants.Add(new SocialRoomOccupant(buyerName, SocialRoomOccupantRole.Buyer, $"Bought {entry.ItemName} from packet state"));
            if (!_savedVisitors.Contains(buyerName, StringComparer.OrdinalIgnoreCase))
            {
                _savedVisitors.Add(buyerName);
            }

            RoomState = "Sold item recorded";
            StatusMessage = $"{buyerName} bought {entry.ItemName} from the personal shop packet feed.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        private bool TryApplyPersonalShopMoveItemPacket(PacketReader reader, out string message)
        {
            int remainingItemCount = reader.ReadByte();
            int removedIndex = reader.ReadShort();
            if (_items.Count == 0)
            {
                message = "No personal-shop bundle exists to apply the move-to-inventory packet.";
                return false;
            }

            int normalizedIndex = Math.Clamp(removedIndex, 0, _items.Count - 1);
            SocialRoomItemEntry entry = _items[normalizedIndex];
            _items.RemoveAt(normalizedIndex);
            _inventoryEscrow.RemoveAll(escrow => ReferenceEquals(escrow.Entry, entry));
            RoomState = "Closed for setup";
            ModeName = "Repricing";
            StatusMessage = $"Moved {entry.ItemName} back to inventory from packet state. {remainingItemCount} bundle(s) remain.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        private void ApplyEntrustedArrangeResult(int mesoAmount)
        {
            MesoAmount = Math.Max(0, mesoAmount);
            RoomState = "Ledger review";
            ModeName = "Ledger review";
            StatusMessage = $"Entrusted-shop arrange packet refreshed the ledger to {MesoAmount:N0} meso.";
            PersistState();
        }

        private void ApplyEntrustedWithdrawAllResult(int resultCode)
        {
            RoomState = "Withdraw-all result";
            StatusMessage = resultCode switch
            {
                5 => "Entrusted-shop withdraw-all packet left the ledger untouched.",
                0 => "Entrusted-shop withdraw-all packet resolved successfully.",
                _ => $"Entrusted-shop withdraw-all result code {resultCode} was applied."
            };
            PersistState();
        }

        private void ApplyEntrustedWithdrawMoneyResult()
        {
            if (MesoAmount > 0)
            {
                _inventoryRuntime?.AddMeso(MesoAmount);
            }

            MesoAmount = 0;
            RoomState = "Ledger settled";
            StatusMessage = "Entrusted-shop withdraw-money packet moved all mesos out of the ledger.";
            PersistState();
        }

        private bool TryApplyEntrustedVisitListPacket(PacketReader reader, out string message)
        {
            int count = reader.ReadShort();
            _savedVisitors.Clear();
            List<string> visitNotes = new();
            for (int i = 0; i < count; i++)
            {
                string name = NormalizeName(reader.ReadMapleString());
                int staySeconds = reader.ReadInt();
                _savedVisitors.Add(name);
                visitNotes.Add($"{name} stayed {staySeconds}s");
            }

            while (_notes.Count < 4)
            {
                _notes.Add(string.Empty);
            }

            _notes[0] = count > 0
                ? $"Entrusted-shop visit log: {string.Join(", ", visitNotes)}."
                : "Entrusted-shop visit log: no entries.";
            StatusMessage = $"Applied entrusted-shop visit-list packet with {count} visitor entr{(count == 1 ? "y" : "ies")}.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        private bool TryApplyEntrustedBlackListPacket(PacketReader reader, out string message)
        {
            int count = reader.ReadShort();
            _blockedVisitors.Clear();
            for (int i = 0; i < count; i++)
            {
                _blockedVisitors.Add(NormalizeName(reader.ReadMapleString()));
            }

            while (_notes.Count < 4)
            {
                _notes.Add(string.Empty);
            }

            _notes[1] = _blockedVisitors.Count > 0
                ? $"Entrusted-shop blacklist: {string.Join(", ", _blockedVisitors)}."
                : "Entrusted-shop blacklist: empty.";
            StatusMessage = $"Applied entrusted-shop blacklist packet with {_blockedVisitors.Count} entr{(_blockedVisitors.Count == 1 ? "y" : "ies")}.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        private void ApplyTradingRoomMesoPacket(int traderIndex, int offeredMeso)
        {
            int normalizedTraderIndex = Math.Clamp(traderIndex, 0, 1);
            int normalizedMeso = Math.Max(0, offeredMeso);
            if (normalizedTraderIndex == 0)
            {
                _tradeLocalOfferMeso = normalizedMeso;
                MesoAmount = normalizedMeso;
            }
            else
            {
                _tradeRemoteOfferMeso = normalizedMeso;
            }

            RoomState = "Negotiating";
            RefreshTradeOccupantsAndRows();
            StatusMessage = $"{(normalizedTraderIndex == 0 ? OwnerName : ResolveRemoteTraderName())} set a packet-backed meso offer of {normalizedMeso:N0}.";
            PersistState();
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
                    "Ledger, restock, and a simulator-owned permit timer now update the merchant ledger while server uptime remains unmodeled."
                },
                chatEntries: null,
                statusMessage: "Entrusted-shop shell open. Arrange, claim, and permit actions now drive visible merchant uptime.",
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
                    "Lock and accept state now track each trader separately while packet-authored trade sessions remain unmodeled."
                },
                chatEntries: null,
                statusMessage: "Trading-room shell ready. Offer, lock, accept, and reset actions now update the shared room state.",
                roomState: "Negotiating",
                modeName: "Open trade");
        }

        public void ToggleMiniRoomGuestReady()
        {
            if (Kind != SocialRoomKind.MiniRoom || _occupants.Count < 2)
            {
                return;
            }

            if (_miniRoomModeIndex == 1 && _miniRoomToggleReadyHandler != null)
            {
                _miniRoomToggleReadyHandler();
                return;
            }

            SetMiniRoomGuestReady(!_occupants[1].IsReady, persistState: true);
        }

        public void CycleMiniRoomMode()
        {
            if (Kind != SocialRoomKind.MiniRoom)
            {
                return;
            }

            _miniRoomModeIndex = (_miniRoomModeIndex + 1) % 2;
            ModeName = _miniRoomModeIndex == 0 ? "Omok" : "Match Cards";
            if (_miniRoomModeIndex == 0)
            {
                SyncMiniRoomOmokPresentation();
            }
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
            PersistState();
        }

        public void StartMiniRoomSession()
        {
            if (Kind != SocialRoomKind.MiniRoom)
            {
                return;
            }

            if (_miniRoomModeIndex == 1 && _miniRoomStartHandler != null)
            {
                _miniRoomStartHandler();
                return;
            }

            bool guestReady = _occupants.Count > 1 && _occupants[1].IsReady;
            if (_miniRoomModeIndex == 0)
            {
                if (!guestReady)
                {
                    RoomState = "Waiting for ready check";
                    StatusMessage = $"Cannot start {ModeName} until the guest readies up.";
                    PersistState();
                    return;
                }

                SetOmokSeatStoneValues(0);
                ResetOmokBoard();
                _miniRoomOmokInProgress = true;
                _miniRoomOmokCurrentTurnIndex = 0;
                _miniRoomOmokWinnerIndex = -1;
                _miniRoomOmokTieRequested = false;
                RoomState = "Omok in progress";
                StatusMessage = $"{OwnerName} has the first Omok turn with black stones.";
                SyncMiniRoomOmokPresentation();
                AddMiniRoomSystemMessage("System : Omok round started.");
                PersistState();
                return;
            }

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
            PersistState();
        }

        public bool TryPlaceOmokStone(int x, int y, out string message)
        {
            message = null;
            if (!IsMiniRoomOmokActive)
            {
                message = "Omok moves only apply while the mini-room is set to Omok.";
                return false;
            }

            if (!_miniRoomOmokInProgress)
            {
                message = "Start the Omok round before placing stones.";
                return false;
            }

            if (!TryValidateOmokCoordinates(x, y, out message))
            {
                return false;
            }

            int boardIndex = GetOmokBoardIndex(x, y);
            if (_miniRoomOmokBoard[boardIndex] != 0)
            {
                message = $"Omok point {x},{y} is already occupied.";
                return false;
            }

            int playerIndex = _miniRoomOmokCurrentTurnIndex;
            int stoneValue = ResolveOmokStoneValueForSeat(playerIndex);
            _miniRoomOmokBoard[boardIndex] = stoneValue;
            _miniRoomOmokLastMoveX = x;
            _miniRoomOmokLastMoveY = y;
            _miniRoomOmokTieRequested = false;

            string playerName = ResolveMiniRoomSeatName(playerIndex);
            string stoneName = ResolveOmokStoneName(stoneValue);
            AddMiniRoomSpeakerMessage(playerName, $"placed a {stoneName} stone at {x},{y}.", playerIndex == 0);

            if (HasFiveInRow(x, y, stoneValue))
            {
                _miniRoomOmokInProgress = false;
                _miniRoomOmokWinnerIndex = playerIndex;
                RoomState = "Omok result";
                StatusMessage = $"{playerName} completed five in a row and won the Omok round.";
                if (_miniRoomWagerAmount > 0)
                {
                    string outcome = playerIndex == 0 ? "owner" : "guest";
                    TrySettleMiniRoomWager(outcome, out _);
                }

                SyncMiniRoomOmokPresentation();
                PersistState();
                message = StatusMessage;
                return true;
            }

            _miniRoomOmokCurrentTurnIndex = playerIndex == 0 ? 1 : 0;
            RoomState = "Omok in progress";
            StatusMessage = $"{ResolveMiniRoomSeatName(_miniRoomOmokCurrentTurnIndex)}'s Omok turn.";
            SyncMiniRoomOmokPresentation();
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TryRequestMiniRoomTie(out string message)
        {
            message = null;
            if (!IsMiniRoomOmokActive)
            {
                message = "Mini-room tie requests are only modeled for Omok.";
                return false;
            }

            if (!_miniRoomOmokInProgress)
            {
                message = "No Omok round is active.";
                return false;
            }

            if (_miniRoomOmokTieRequested)
            {
                _miniRoomOmokInProgress = false;
                _miniRoomOmokWinnerIndex = -1;
                RoomState = "Omok draw";
                StatusMessage = "The Omok round ended in a draw.";
                AddMiniRoomSystemMessage("System : Omok draw request accepted.");
                if (_miniRoomWagerAmount > 0)
                {
                    TrySettleMiniRoomWager("draw", out _);
                }
            }
            else
            {
                _miniRoomOmokTieRequested = true;
                StatusMessage = $"{ResolveMiniRoomSeatName(_miniRoomOmokCurrentTurnIndex)} requested an Omok draw.";
                AddMiniRoomSystemMessage($"System : {StatusMessage}");
            }

            SyncMiniRoomOmokPresentation();
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TryForfeitMiniRoom(string loserSeat, out string message)
        {
            message = null;
            if (!IsMiniRoomOmokActive)
            {
                message = "Mini-room forfeits are only modeled for Omok.";
                return false;
            }

            if (!_miniRoomOmokInProgress)
            {
                message = "No Omok round is active.";
                return false;
            }

            int loserIndex = string.Equals(loserSeat, "guest", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            int winnerIndex = loserIndex == 0 ? 1 : 0;
            _miniRoomOmokInProgress = false;
            _miniRoomOmokWinnerIndex = winnerIndex;
            _miniRoomOmokTieRequested = false;
            RoomState = "Omok forfeit";
            StatusMessage = $"{ResolveMiniRoomSeatName(loserIndex)} forfeited. {ResolveMiniRoomSeatName(winnerIndex)} won the Omok round.";
            AddMiniRoomSystemMessage($"System : {StatusMessage}", isWarning: true);
            if (_miniRoomWagerAmount > 0)
            {
                TrySettleMiniRoomWager(winnerIndex == 0 ? "owner" : "guest", out _);
            }

            SyncMiniRoomOmokPresentation();
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool AddMiniRoomVisitor(string visitorName, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.MiniRoom)
            {
                message = "Mini-room visitor flow only applies to the mini-room shell.";
                return false;
            }

            if (_occupants.Count >= Capacity)
            {
                message = "The mini-room has no open visitor seats.";
                return false;
            }

            string resolvedName = NormalizeName(visitorName);
            if (_occupants.Any(occupant => string.Equals(occupant.Name, resolvedName, StringComparison.OrdinalIgnoreCase)))
            {
                message = $"{resolvedName} is already inside the mini-room.";
                return false;
            }

            string detail = $"Visitor seat {_occupants.Count} | Watching {ModeName}";
            _occupants.Add(new SocialRoomOccupant(resolvedName, SocialRoomOccupantRole.Visitor, detail));
            StatusMessage = $"{resolvedName} entered the mini-room as a visitor.";
            AddMiniRoomSystemMessage($"System : {StatusMessage}");
            if (_notes.Count > 1)
            {
                _notes[1] = $"Visitor seats active: {Math.Max(0, _occupants.Count - 2)} / {Math.Max(0, Capacity - 2)}.";
            }

            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool RemoveMiniRoomOccupant(string occupantName, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.MiniRoom)
            {
                message = "Mini-room leave flow only applies to the mini-room shell.";
                return false;
            }

            string resolvedName = string.IsNullOrWhiteSpace(occupantName)
                ? _occupants.LastOrDefault(occupant => occupant.Role == SocialRoomOccupantRole.Visitor)?.Name
                : occupantName.Trim();
            SocialRoomOccupant occupant = _occupants.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, resolvedName, StringComparison.OrdinalIgnoreCase));
            if (occupant == null || occupant.Role == SocialRoomOccupantRole.Owner)
            {
                message = "No removable mini-room visitor or guest was found.";
                return false;
            }

            bool removedGuest = occupant.Role == SocialRoomOccupantRole.Guest;
            _occupants.Remove(occupant);
            if (removedGuest)
            {
                SocialRoomOccupant replacementGuest = new SocialRoomOccupant("Opponent", SocialRoomOccupantRole.Guest, "Waiting to ready");
                if (_occupants.Count == 0)
                {
                    _occupants.Add(new SocialRoomOccupant(OwnerName, SocialRoomOccupantRole.Owner, BuildOmokSeatDetail(0, "Host seat"), false));
                }

                if (_occupants.Count == 1)
                {
                    _occupants.Add(replacementGuest);
                }
                else
                {
                    _occupants.Insert(1, replacementGuest);
                }

                if (_miniRoomOmokInProgress)
                {
                    _miniRoomOmokInProgress = false;
                    _miniRoomOmokWinnerIndex = 0;
                    RoomState = "Omok ended";
                    StatusMessage = $"{resolvedName} left the mini-room. {OwnerName} kept the room.";
                }
            }
            else
            {
                StatusMessage = $"{resolvedName} left the mini-room visitor rail.";
            }

            AddMiniRoomSystemMessage($"System : {StatusMessage}", isWarning: removedGuest);
            SyncMiniRoomOmokPresentation();
            PersistState();
            message = StatusMessage;
            return true;
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
            PersistState();
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
            PersistState();
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
            PersistState();
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
            PersistState();
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

            PersistState();
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
            PersistState();
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
            PersistState();
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

            PersistState();
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
            PersistState();
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
            PersistState();
            message = StatusMessage;
            return true;
        }

        public void ToggleEntrustedLedgerMode()
        {
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                return;
            }

            UpdateEntrustedPermitState(DateTime.UtcNow);

            bool isLedger = !string.Equals(ModeName, "Restock", StringComparison.Ordinal);
            ModeName = isLedger ? "Restock" : "Ledger review";
            RoomState = isLedger ? "Updating sale list" : IsEntrustedPermitExpired(DateTime.UtcNow) ? "Permit expired" : "Permit active";
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
            PersistState();
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

            if (!EnsureEntrustedPermitActive(out message))
            {
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
            PersistState();
            message = StatusMessage;
            return true;
        }

        public void ArrangeEntrustedShop()
        {
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                return;
            }

            if (!EnsureEntrustedPermitActive(out _))
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
            PersistState();
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
            PersistState();
        }

        public bool TryRenewEntrustedPermit(int minutes, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                message = "Permit uptime only applies to the entrusted shop shell.";
                return false;
            }

            int normalizedMinutes = Math.Clamp(minutes <= 0 ? 24 * 60 : minutes, 1, 7 * 24 * 60);
            _entrustedPermitExpiresAtUtc = DateTime.UtcNow.AddMinutes(normalizedMinutes);
            RoomState = "Permit active";
            if (string.Equals(ModeName, "Permit expired", StringComparison.Ordinal))
            {
                ModeName = "Ledger review";
            }

            StatusMessage = $"Entrusted-shop permit renewed for {normalizedMinutes} minute{(normalizedMinutes == 1 ? string.Empty : "s")}.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool ExpireEntrustedPermit(out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                message = "Permit uptime only applies to the entrusted shop shell.";
                return false;
            }

            _entrustedPermitExpiresAtUtc = DateTime.UtcNow;
            UpdateEntrustedPermitState(DateTime.UtcNow);
            PersistState();
            message = StatusMessage;
            return true;
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
            ClearTradeHandshake();
            RefreshTradeOccupantsAndRows();

            StatusMessage = $"Raised the offered mesos to {MesoAmount:N0}.";
            PersistState();
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

            ClearTradeHandshake();
            MesoAmount += mesoAmount;
            _tradeLocalOfferMeso += mesoAmount;
            RefreshTradeOccupantsAndRows();
            StatusMessage = $"Escrowed {mesoAmount:N0} meso into the trading room.";
            PersistState();
            message = StatusMessage;
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

            ClearTradeHandshake();
            SocialRoomItemEntry entry = new SocialRoomItemEntry(OwnerName, slotData.ItemName, quantity, 0, "Owner offer | Inventory escrowed");
            _items.Insert(Math.Min(2, _items.Count), entry);
            _inventoryEscrow.Add(new InventoryEscrowEntry(entry, inventoryType, slotData, returnOnReset: true, returnOnClose: true));
            RoomState = "Negotiating";
            RefreshTradeOccupantsAndRows();
            StatusMessage = $"Added {slotData.ItemName} x{quantity} to the local trade escrow.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TryOfferRemoteTradeItem(int itemId, int quantity, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Remote trade preview only applies to the trading-room shell.";
                return false;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            if (inventoryType == InventoryType.NONE || quantity <= 0)
            {
                message = "A valid remote trade item id and quantity are required.";
                return false;
            }

            if (!TryConsumeRemoteInventoryItem(itemId, quantity, out SocialRoomRemoteInventoryEntry remoteInventoryEntry, out message))
            {
                return false;
            }

            ClearTradeHandshake();
            string remoteName = ResolveRemoteTraderName();
            SocialRoomItemEntry entry = new SocialRoomItemEntry(remoteName, remoteInventoryEntry.ItemName, quantity, 0, "Guest offer | Remote inventory escrowed");
            int insertIndex = _items.FindLastIndex(item => string.Equals(item.OwnerName, OwnerName, StringComparison.OrdinalIgnoreCase));
            _items.Insert(insertIndex < 0 ? _items.Count : insertIndex + 1, entry);
            RoomState = "Negotiating";
            RefreshTradeOccupantsAndRows();
            StatusMessage = $"Remote trader added {entry.ItemName} x{quantity} to the preview offer.";
            RefreshRemoteInventoryNotes();
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TryOfferRemoteTradeMeso(int mesoAmount, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Remote trade preview only applies to the trading-room shell.";
                return false;
            }

            if (mesoAmount <= 0)
            {
                message = "Remote trade meso offers must be positive.";
                return false;
            }

            if (_remoteInventoryMeso < mesoAmount)
            {
                message = $"Remote trader only has {_remoteInventoryMeso:N0} meso available in the simulator wallet.";
                return false;
            }

            ClearTradeHandshake();
            _remoteInventoryMeso -= mesoAmount;
            _tradeRemoteOfferMeso += mesoAmount;
            RoomState = "Negotiating";
            RefreshTradeOccupantsAndRows();
            StatusMessage = $"Remote trader escrowed {mesoAmount:N0} meso into the preview offer.";
            RefreshRemoteInventoryNotes();
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool ToggleTradeLock(out string message, bool remoteParty = false)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Trade lock only applies to the trading-room shell.";
                return false;
            }

            if (remoteParty)
            {
                _tradeRemoteLocked = !_tradeRemoteLocked;
            }
            else
            {
                _tradeLocalLocked = !_tradeLocalLocked;
            }

            _tradeLocalAccepted = false;
            _tradeRemoteAccepted = false;
            RoomState = _tradeLocalLocked && _tradeRemoteLocked ? "Locked" : "Negotiating";
            RefreshTradeOccupantsAndRows();
            string actor = remoteParty ? ResolveRemoteTraderName() : OwnerName;
            bool locked = remoteParty ? _tradeRemoteLocked : _tradeLocalLocked;
            StatusMessage = locked
                ? $"{actor} locked their side of the trade."
                : $"{actor} unlocked their side of the trade.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool ToggleTradeAcceptance(out string message, bool remoteParty = false)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Trade acceptance only applies to the trading-room shell.";
                return false;
            }

            if (!_tradeLocalLocked || !_tradeRemoteLocked)
            {
                message = "Both traders must lock the exchange before final acceptance.";
                return false;
            }

            if (remoteParty)
            {
                _tradeRemoteAccepted = !_tradeRemoteAccepted;
            }
            else
            {
                _tradeLocalAccepted = !_tradeLocalAccepted;
            }

            RoomState = _tradeLocalAccepted && _tradeRemoteAccepted ? "Awaiting settlement" : "Locked";
            RefreshTradeOccupantsAndRows();
            string actor = remoteParty ? ResolveRemoteTraderName() : OwnerName;
            bool accepted = remoteParty ? _tradeRemoteAccepted : _tradeLocalAccepted;
            StatusMessage = accepted
                ? $"{actor} accepted the locked trade."
                : $"{actor} canceled their final trade acceptance.";
            PersistState();
            message = StatusMessage;
            return true;
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
            _tradeLocalLocked = false;
            _tradeRemoteLocked = false;
            _tradeLocalAccepted = false;
            _tradeRemoteAccepted = false;
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
                _occupants.Add(new SocialRoomOccupant(occupant.Name, occupant.Role, occupant.Detail, false, occupant.AvatarBuild));
            }

            _inventoryEscrow.Clear();
            _inventoryBackedRows = false;
            RestoreDefaultRemoteInventory();
            RefreshRemoteInventoryNotes();
            PersistState();
        }

        public bool TryCompleteTrade(out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Trade settlement only applies to the trading-room shell.";
                return false;
            }

            if (!_tradeLocalLocked || !_tradeRemoteLocked)
            {
                message = "Both traders must lock the exchange before completing the settlement.";
                return false;
            }

            if (!_tradeLocalAccepted || !_tradeRemoteAccepted)
            {
                message = "Both traders must accept the locked trade before settlement.";
                return false;
            }

            List<SocialRoomItemEntry> remoteEntries = _items
                .Where(item => !string.Equals(item.OwnerName, OwnerName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (SocialRoomItemEntry remoteEntry in remoteEntries)
            {
                int remoteItemId = remoteEntry.ItemId > 0 ? remoteEntry.ItemId : ResolveItemIdByName(remoteEntry.ItemName);
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
                int remoteItemId = remoteEntry.ItemId > 0 ? remoteEntry.ItemId : ResolveItemIdByName(remoteEntry.ItemName);
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
            int receivedRemoteMeso = _tradeRemoteOfferMeso;
            _tradeRemoteOfferMeso = 0;
            _tradeLocalLocked = true;
            _tradeRemoteLocked = true;
            _tradeLocalAccepted = true;
            _tradeRemoteAccepted = true;
            MesoAmount = 0;
            RoomState = "Trade settled";
            RefreshTradeOccupantsAndRows();
            StatusMessage = $"Trade completed. Received {remoteEntries.Count} remote item entr{(remoteEntries.Count == 1 ? "y" : "ies")} and {receivedRemoteMeso:N0} meso.";
            RefreshRemoteInventoryNotes();
            PersistState();
            message = StatusMessage;
            return true;
        }

        public string DescribeRemoteTradeInventory()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return "Remote trade inventory is only modeled for the trading-room shell.";
            }

            string itemSummary = _remoteInventoryEntries.Count == 0
                ? "no items"
                : string.Join(", ", _remoteInventoryEntries.Select(entry => $"{entry.ItemName} x{entry.Quantity}"));
            return $"Remote trader wallet={_remoteInventoryMeso:N0}, catalog={itemSummary}.";
        }

        public bool TrySeedRemoteTradeInventoryItem(int itemId, int quantity, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Remote trade inventory seeding only applies to the trading-room shell.";
                return false;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            if (inventoryType == InventoryType.NONE || quantity <= 0)
            {
                message = "A valid item id and quantity are required to seed the remote trade inventory.";
                return false;
            }

            AddRemoteInventoryItem(itemId, quantity);
            RefreshRemoteInventoryNotes();
            PersistState();
            message = $"Added {ResolveItemName(itemId)} x{quantity} to the remote trade inventory.";
            return true;
        }

        public bool TrySeedRemoteTradeInventoryMeso(int mesoAmount, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Remote trade inventory seeding only applies to the trading-room shell.";
                return false;
            }

            if (mesoAmount <= 0)
            {
                message = "A positive meso amount is required to seed the remote trade wallet.";
                return false;
            }

            _remoteInventoryMeso += mesoAmount;
            RefreshRemoteInventoryNotes();
            PersistState();
            message = $"Added {mesoAmount:N0} meso to the remote trade wallet.";
            return true;
        }

        public string ConfigureTradeInviteTarget(string traderName, CharacterBuild avatarBuild = null)
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return "Trade invite targeting only applies to the trading-room shell.";
            }

            string resolvedName = NormalizeName(traderName);
            if (_occupants.Count == 0)
            {
                _occupants.Add(new SocialRoomOccupant(OwnerName, SocialRoomOccupantRole.Trader, BuildTradeOccupantDetail(true, _tradeLocalLocked, _tradeLocalAccepted), false));
            }

            string previousRemoteName = ResolveRemoteTraderName();
            if (_occupants.Count == 1)
            {
                _occupants.Add(new SocialRoomOccupant(resolvedName, SocialRoomOccupantRole.Trader, BuildTradeOccupantDetail(false, _tradeRemoteLocked, _tradeRemoteAccepted), false, avatarBuild));
            }
            else
            {
                _occupants[1].Update(resolvedName, SocialRoomOccupantRole.Trader, BuildTradeOccupantDetail(false, _tradeRemoteLocked, _tradeRemoteAccepted), false, avatarBuild);
            }

            if (!string.IsNullOrWhiteSpace(previousRemoteName) && !string.Equals(previousRemoteName, resolvedName, StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    SocialRoomItemEntry item = _items[i];
                    if (string.Equals(item.OwnerName, previousRemoteName, StringComparison.OrdinalIgnoreCase))
                    {
                        _items[i] = new SocialRoomItemEntry(
                            resolvedName,
                            item.ItemName,
                            item.Quantity,
                            item.MesoAmount,
                            item.Detail,
                            item.IsLocked,
                            item.IsClaimed,
                            item.ItemId,
                            item.PacketSlotIndex);
                    }
                }
            }

            RoomState = "Invite pending";
            ClearTradeHandshake();
            RefreshTradeOccupantsAndRows();
            StatusMessage = $"Prepared a simulated trade invite for {resolvedName}. Server accept or reject flow still remains outside this owner.";
            PersistState();
            return StatusMessage;
        }

        public bool TrySetEmployeeTemplate(int templateId, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.PersonalShop && Kind != SocialRoomKind.EntrustedShop)
            {
                message = "Employee template changes only apply to personal-shop and entrusted-shop rooms.";
                return false;
            }

            int normalizedTemplateId = Math.Max(0, templateId);
            if (normalizedTemplateId > 0 && !HasEmployeeTemplate(normalizedTemplateId))
            {
                message = $"Employee template {normalizedTemplateId} was not found under Item/Cash.";
                return false;
            }

            _employeeTemplateId = normalizedTemplateId;
            if (_employeeAnchorOffsetX == 0 && _employeeAnchorOffsetY == 0)
            {
                _employeeAnchorOffsetX = Kind == SocialRoomKind.EntrustedShop ? 72 : 56;
            }

            StatusMessage = normalizedTemplateId > 0
                ? $"Employee template set to {normalizedTemplateId}."
                : "Employee template cleared; room will fall back to the legacy NPC representative.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TrySetEmployeeAnchorOffset(int offsetX, int offsetY, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.PersonalShop && Kind != SocialRoomKind.EntrustedShop)
            {
                message = "Employee placement only applies to personal-shop and entrusted-shop rooms.";
                return false;
            }

            _employeeUseOwnerAnchor = true;
            _employeeHasWorldPosition = false;
            _employeeAnchorOffsetX = offsetX;
            _employeeAnchorOffsetY = offsetY;
            StatusMessage = $"Employee anchor offset set to ({offsetX}, {offsetY}) relative to the owner.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TrySetEmployeeWorldPosition(int worldX, int worldY, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.PersonalShop && Kind != SocialRoomKind.EntrustedShop)
            {
                message = "Employee placement only applies to personal-shop and entrusted-shop rooms.";
                return false;
            }

            _employeeUseOwnerAnchor = false;
            _employeeHasWorldPosition = true;
            _employeeWorldX = worldX;
            _employeeWorldY = worldY;
            StatusMessage = $"Employee world placement set to ({worldX}, {worldY}).";
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TrySetEmployeeFlip(bool? flip, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.PersonalShop && Kind != SocialRoomKind.EntrustedShop)
            {
                message = "Employee facing only applies to personal-shop and entrusted-shop rooms.";
                return false;
            }

            _employeeFlip = flip;
            StatusMessage = flip.HasValue
                ? $"Employee facing locked to {(flip.Value ? "left" : "right")}."
                : "Employee facing returned to action-driven random selection.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        public void ResetEmployeePlacement()
        {
            if (Kind != SocialRoomKind.PersonalShop && Kind != SocialRoomKind.EntrustedShop)
            {
                return;
            }

            _employeeUseOwnerAnchor = true;
            _employeeHasWorldPosition = false;
            _employeeAnchorOffsetX = Kind == SocialRoomKind.EntrustedShop ? 72 : 56;
            _employeeAnchorOffsetY = 0;
            _employeeWorldX = 0;
            _employeeWorldY = 0;
            _employeeFlip = null;
            StatusMessage = "Employee placement reset to the owner-anchored default.";
            PersistState();
        }

        public void ClearRemoteTradeInventory()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            _remoteInventoryEntries.Clear();
            _remoteInventoryMeso = 0;
            RefreshRemoteInventoryNotes();
            PersistState();
        }

        private void SeedDefaultRemoteInventory()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            _defaultRemoteInventoryEntries.Clear();
            _defaultRemoteInventoryEntries.Add(new SocialRoomRemoteInventoryEntry(4004004, ResolveItemName(4004004), 40));
            _defaultRemoteInventoryEntries.Add(new SocialRoomRemoteInventoryEntry(2070011, ResolveItemName(2070011), 2));
            _defaultRemoteInventoryEntries.Add(new SocialRoomRemoteInventoryEntry(2044704, ResolveItemName(2044704), 1));
            RestoreDefaultRemoteInventory();
        }

        private void RestoreDefaultRemoteInventory()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            _remoteInventoryEntries.Clear();
            foreach (SocialRoomRemoteInventoryEntry entry in _defaultRemoteInventoryEntries)
            {
                _remoteInventoryEntries.Add(new SocialRoomRemoteInventoryEntry(entry.ItemId, entry.ItemName, entry.Quantity));
            }

            _remoteInventoryMeso = _defaultSnapshot?.RemoteInventoryMeso ?? 325000;
        }

        private bool TryConsumeRemoteInventoryItem(int itemId, int quantity, out SocialRoomRemoteInventoryEntry entry, out string message)
        {
            entry = _remoteInventoryEntries.FirstOrDefault(candidate => candidate.ItemId == itemId && candidate.Quantity >= quantity);
            if (entry == null)
            {
                int available = _remoteInventoryEntries.FirstOrDefault(candidate => candidate.ItemId == itemId)?.Quantity ?? 0;
                message = available > 0
                    ? $"Remote trader only has {available} of item {itemId} available."
                    : $"Remote trader does not have item {itemId} in the simulator inventory.";
                return false;
            }

            entry.Update(entry.Quantity - quantity);
            if (entry.Quantity <= 0)
            {
                _remoteInventoryEntries.Remove(entry);
            }

            message = null;
            return true;
        }

        private void AddRemoteInventoryItem(int itemId, int quantity)
        {
            if (itemId <= 0 || quantity <= 0)
            {
                return;
            }

            SocialRoomRemoteInventoryEntry existing = _remoteInventoryEntries.FirstOrDefault(entry => entry.ItemId == itemId);
            if (existing != null)
            {
                existing.Update(existing.Quantity + quantity);
                return;
            }

            _remoteInventoryEntries.Add(new SocialRoomRemoteInventoryEntry(itemId, ResolveItemName(itemId), quantity));
        }

        private void RefreshRemoteInventoryNotes()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            string walletNote = $"Remote trade wallet: {_remoteInventoryMeso:N0} meso.";
            string itemNote = _remoteInventoryEntries.Count == 0
                ? "Remote trade catalog: empty."
                : $"Remote trade catalog: {string.Join(", ", _remoteInventoryEntries.Select(entry => $"{entry.ItemName} x{entry.Quantity}"))}.";

            while (_notes.Count < 4)
            {
                _notes.Add(string.Empty);
            }

            _notes[2] = walletNote;
            _notes[3] = itemNote;
        }

        private void PersistState()
        {
            if (_suspendPersistence || string.IsNullOrWhiteSpace(_persistenceKey) || _persistStateHandler == null)
            {
                return;
            }

            _persistStateHandler(_persistenceKey, BuildSnapshot());
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

        private bool EnsureEntrustedPermitActive(out string message)
        {
            UpdateEntrustedPermitState(DateTime.UtcNow);
            if (!IsEntrustedPermitExpired(DateTime.UtcNow))
            {
                message = null;
                return true;
            }

            message = "The entrusted-shop permit expired. Renew it before restocking or rearranging sale rows.";
            return false;
        }

        private void UpdateEntrustedPermitState(DateTime utcNow)
        {
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                return;
            }

            string permitNote = $"Simulator permit timer: {FormatPermitStatus(utcNow)}.";
            if (_notes.Count < 3)
            {
                _notes.Add(permitNote);
            }
            else
            {
                _notes[2] = permitNote;
            }

            if (!IsEntrustedPermitExpired(utcNow))
            {
                if (string.Equals(RoomState, "Permit expired", StringComparison.Ordinal))
                {
                    RoomState = "Permit active";
                    StatusMessage = $"Entrusted-shop permit restored. {FormatPermitStatus(utcNow)}.";
                }

                return;
            }

            RoomState = "Permit expired";
            if (string.Equals(ModeName, "Restock", StringComparison.Ordinal))
            {
                ModeName = "Permit expired";
            }

            StatusMessage = "Entrusted-shop permit expired. Claim proceeds or renew the permit to keep selling.";
        }

        private bool IsEntrustedPermitExpired(DateTime utcNow)
        {
            return Kind == SocialRoomKind.EntrustedShop &&
                   _entrustedPermitExpiresAtUtc.HasValue &&
                   utcNow >= _entrustedPermitExpiresAtUtc.Value;
        }

        private string FormatPermitStatus(DateTime utcNow)
        {
            if (Kind != SocialRoomKind.EntrustedShop || !_entrustedPermitExpiresAtUtc.HasValue)
            {
                return "no timer";
            }

            TimeSpan remaining = _entrustedPermitExpiresAtUtc.Value - utcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return "expired";
            }

            return remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m left"
                : $"{Math.Max(1, remaining.Minutes)}m left";
        }

        private string DescribeEmployeeState()
        {
            string templateText = _employeeTemplateId > 0 ? _employeeTemplateId.ToString() : "legacy";
            if (_employeeHasWorldPosition && !_employeeUseOwnerAnchor)
            {
                return $"{templateText}@world({_employeeWorldX},{_employeeWorldY})";
            }

            return $"{templateText}@owner({_employeeAnchorOffsetX},{_employeeAnchorOffsetY})";
        }

        private static bool HasEmployeeTemplate(int templateId)
        {
            if (templateId <= 0)
            {
                return false;
            }

            string folderName = ResolveEmployeeItemFolder(templateId);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return false;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage("Item", $"{folderName}/{templateId / 10000:D4}.img");
            return itemImage?[templateId.ToString("D8")]?["employee"] != null;
        }

        private static string ResolveEmployeeItemFolder(int templateId)
        {
            int category = templateId / 1000000;
            return category switch
            {
                5 => "Cash",
                _ => null
            };
        }

        private void ClearTradeHandshake()
        {
            _tradeLocalLocked = false;
            _tradeRemoteLocked = false;
            _tradeLocalAccepted = false;
            _tradeRemoteAccepted = false;
            RefreshTradeOccupantsAndRows();
        }

        private void RefreshTradeOccupantsAndRows()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            for (int i = 0; i < _occupants.Count; i++)
            {
                bool isLocal = i == 0;
                bool locked = isLocal ? _tradeLocalLocked : _tradeRemoteLocked;
                bool accepted = isLocal ? _tradeLocalAccepted : _tradeRemoteAccepted;
                _occupants[i].Update(BuildTradeOccupantDetail(isLocal, locked, accepted), locked || accepted, _occupants[i].AvatarBuild);
            }

            foreach (SocialRoomItemEntry item in _items)
            {
                bool isLocalItem = string.Equals(item.OwnerName, OwnerName, StringComparison.OrdinalIgnoreCase);
                bool isLocked = isLocalItem ? _tradeLocalLocked : _tradeRemoteLocked;
                item.Update(item.Detail, item.Quantity, item.MesoAmount, isLocked, item.IsClaimed);
            }
        }

        private string BuildTradeOccupantDetail(bool isLocal, bool locked, bool accepted)
        {
            string partyName = isLocal ? OwnerName : ResolveRemoteTraderName();
            int meso = isLocal ? _tradeLocalOfferMeso : _tradeRemoteOfferMeso;
            int itemCount = _items.Count(item => string.Equals(item.OwnerName, partyName, StringComparison.OrdinalIgnoreCase));
            string stage = accepted
                ? "Accepted"
                : locked
                    ? "Locked"
                    : "Reviewing";
            return $"{stage} | {itemCount} item entr{(itemCount == 1 ? "y" : "ies")} | {meso:N0} mesos";
        }

        private string ResolveRemoteTraderName()
        {
            return _occupants.Skip(1).FirstOrDefault()?.Name ?? "Trader";
        }

        private static string FormatTradePartyState(bool localState, bool remoteState)
        {
            return $"{(localState ? "Y" : "N")}/{(remoteState ? "Y" : "N")}";
        }

        public int GetMiniRoomOmokStoneAt(int x, int y)
        {
            return x >= 0 && x < MiniRoomOmokBoardSize && y >= 0 && y < MiniRoomOmokBoardSize
                ? _miniRoomOmokBoard[GetOmokBoardIndex(x, y)]
                : 0;
        }

        private void SyncMiniRoomOmokPresentation()
        {
            if (Kind != SocialRoomKind.MiniRoom || _miniRoomModeIndex != 0)
            {
                return;
            }

            if (_items.Count > 0)
            {
                _items[0].Update(
                    _miniRoomOmokInProgress ? "Omok board active" : "Omok board waiting on ready",
                    1,
                    0,
                    _miniRoomOmokInProgress,
                    false);
            }

            if (_items.Count > 1)
            {
                string detail = _miniRoomOmokInProgress
                    ? $"Last move: {FormatOmokLastMove()} | Turn {ResolveOmokTurnName()}"
                    : "Match Cards preview hidden";
                _items[1].Update(detail, 1, 0, false, false);
            }

            EnsureMiniRoomOccupant(0, OwnerName, SocialRoomOccupantRole.Owner, BuildOmokSeatDetail(0, "Host seat"), _miniRoomOmokCurrentTurnIndex == 0 && _miniRoomOmokInProgress);
            EnsureMiniRoomOccupant(1, ResolveMiniRoomSeatName(1), SocialRoomOccupantRole.Guest, BuildOmokSeatDetail(1, "Guest seat"), _miniRoomOmokCurrentTurnIndex == 1 && _miniRoomOmokInProgress);

            for (int i = 2; i < _occupants.Count; i++)
            {
                SocialRoomOccupant visitor = _occupants[i];
                visitor.Update(visitor.Name, visitor.Role, $"Visitor seat {i} | Watching {ModeName}", visitor.IsReady, visitor.AvatarBuild);
            }

            if (_notes.Count == 0)
            {
                _notes.Add("Shared client surface for Omok and Match Cards.");
            }

            if (_notes.Count == 1)
            {
                _notes.Add(string.Empty);
            }

            _notes[0] = "WZ-backed Omok shell uses UIWindow(.2).img/Minigame/Omok art and stone frames.";
            _notes[1] = $"Visitor seats active: {Math.Max(0, _occupants.Count - 2)} / {Math.Max(0, Capacity - 2)}.";
        }

        private void ResetOmokBoard()
        {
            Array.Clear(_miniRoomOmokBoard, 0, _miniRoomOmokBoard.Length);
            _miniRoomOmokLastMoveX = -1;
            _miniRoomOmokLastMoveY = -1;
            _miniRoomOmokWinnerIndex = -1;
            _miniRoomOmokTieRequested = false;
        }

        private void SetOmokSeatStoneValues(int blackSeatIndex)
        {
            if (blackSeatIndex == 0)
            {
                _miniRoomOmokOwnerStoneValue = 1;
                _miniRoomOmokGuestStoneValue = 2;
                return;
            }

            _miniRoomOmokOwnerStoneValue = 2;
            _miniRoomOmokGuestStoneValue = 1;
        }

        private bool TryValidateOmokCoordinates(int x, int y, out string message)
        {
            if (x < 0 || x >= MiniRoomOmokBoardSize || y < 0 || y >= MiniRoomOmokBoardSize)
            {
                message = $"Omok coordinates must be between 0 and {MiniRoomOmokBoardSize - 1}.";
                return false;
            }

            message = null;
            return true;
        }

        private bool HasFiveInRow(int x, int y, int stoneValue)
        {
            (int dx, int dy)[] directions =
            {
                (1, 0),
                (0, 1),
                (1, 1),
                (1, -1)
            };

            foreach ((int dx, int dy) in directions)
            {
                int count = 1 + CountDirection(x, y, dx, dy, stoneValue) + CountDirection(x, y, -dx, -dy, stoneValue);
                if (count >= 5)
                {
                    return true;
                }
            }

            return false;
        }

        private int CountDirection(int x, int y, int dx, int dy, int stoneValue)
        {
            int count = 0;
            for (int nextX = x + dx, nextY = y + dy;
                nextX >= 0 && nextX < MiniRoomOmokBoardSize && nextY >= 0 && nextY < MiniRoomOmokBoardSize;
                nextX += dx, nextY += dy)
            {
                if (_miniRoomOmokBoard[GetOmokBoardIndex(nextX, nextY)] != stoneValue)
                {
                    break;
                }

                count++;
            }

            return count;
        }

        private static int GetOmokBoardIndex(int x, int y)
        {
            return (y * MiniRoomOmokBoardSize) + x;
        }

        private string BuildOmokSeatDetail(int playerIndex, string seatLabel)
        {
            string stoneName = ResolveOmokStoneName(ResolveOmokStoneValueForSeat(playerIndex));
            string state = _miniRoomOmokWinnerIndex == playerIndex
                ? "Winner"
                : _miniRoomOmokInProgress && _miniRoomOmokCurrentTurnIndex == playerIndex
                    ? "Current turn"
                    : _miniRoomOmokInProgress
                        ? "Waiting"
                        : "Ready";
            return $"{seatLabel} | {stoneName} stones | {state}";
        }

        private string ResolveMiniRoomSeatName(int index)
        {
            if (index >= 0 && index < _occupants.Count && !string.IsNullOrWhiteSpace(_occupants[index].Name))
            {
                return _occupants[index].Name;
            }

            return index == 0 ? OwnerName : "Opponent";
        }

        private string ResolveOmokTurnName()
        {
            if (!IsMiniRoomOmokActive)
            {
                return "n/a";
            }

            if (_miniRoomOmokWinnerIndex >= 0)
            {
                return ResolveMiniRoomSeatName(_miniRoomOmokWinnerIndex);
            }

            return _miniRoomOmokInProgress ? ResolveMiniRoomSeatName(_miniRoomOmokCurrentTurnIndex) : "idle";
        }

        private string ResolveOmokWinnerName()
        {
            return _miniRoomOmokWinnerIndex >= 0 ? ResolveMiniRoomSeatName(_miniRoomOmokWinnerIndex) : "none";
        }

        private string FormatOmokLastMove()
        {
            return _miniRoomOmokLastMoveX >= 0 && _miniRoomOmokLastMoveY >= 0
                ? $"{_miniRoomOmokLastMoveX},{_miniRoomOmokLastMoveY}"
                : "none";
        }

        private static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Visitor" : value.Trim();
        }

        private static byte[] NormalizePacketPayload(byte[] packetBytes)
        {
            if (packetBytes == null || packetBytes.Length == 0)
            {
                return Array.Empty<byte>();
            }

            if (IsKnownPacketType(packetBytes[0]))
            {
                return (byte[])packetBytes.Clone();
            }

            if (packetBytes.Length > sizeof(ushort) && IsKnownPacketType(packetBytes[sizeof(ushort)]))
            {
                byte[] trimmed = new byte[packetBytes.Length - sizeof(ushort)];
                Buffer.BlockCopy(packetBytes, sizeof(ushort), trimmed, 0, trimmed.Length);
                return trimmed;
            }

            return (byte[])packetBytes.Clone();
        }

        private static bool IsKnownPacketType(byte packetType)
        {
            return packetType is
                TradingRoomPutItemPacketType or
                TradingRoomPutMoneyPacketType or
                TradingRoomTradePacketType or
                TradingRoomExceedLimitPacketType or
                PersonalShopBuyResultPacketType or
                PersonalShopBasePacketType or
                PersonalShopSoldItemResultPacketType or
                PersonalShopMoveItemToInventoryPacketType or
                EntrustedShopArrangeItemResultPacketType or
                EntrustedShopWithdrawAllResultPacketType or
                EntrustedShopWithdrawMoneyResultPacketType or
                EntrustedShopVisitListResultPacketType or
                EntrustedShopBlackListResultPacketType or
                OmokTieRequestPacketType or
                OmokTieResultPacketType or
                OmokRetreatRequestPacketType or
                OmokRetreatResultPacketType or
                OmokReadyPacketType or
                OmokCancelReadyPacketType or
                OmokStartPacketType or
                OmokGameResultPacketType or
                OmokTimeOverPacketType or
                OmokPutStonePacketType or
                OmokPutStoneErrorPacketType;
        }

        private static int NormalizeOmokStoneValue(int stoneValue, int fallback)
        {
            return stoneValue == 1 || stoneValue == 2 ? stoneValue : fallback;
        }

        private int ResolveOmokStoneValueForSeat(int seatIndex)
        {
            return seatIndex == 0 ? _miniRoomOmokOwnerStoneValue : _miniRoomOmokGuestStoneValue;
        }

        private int ResolveOmokSeatIndexByStoneValue(int stoneValue)
        {
            if (_miniRoomOmokOwnerStoneValue == stoneValue)
            {
                return 0;
            }

            if (_miniRoomOmokGuestStoneValue == stoneValue)
            {
                return 1;
            }

            return -1;
        }

        private static string ResolveOmokStoneName(int stoneValue)
        {
            return stoneValue == 1 ? "Black" : "White";
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

        private void EnsureMiniRoomOccupant(int index, string name, SocialRoomOccupantRole role, string detail, bool isReady, CharacterBuild avatarBuild = null)
        {
            while (_occupants.Count <= index)
            {
                _occupants.Add(new SocialRoomOccupant(name, role, detail, isReady, avatarBuild));
            }

            _occupants[index].Update(name, role, detail, isReady, avatarBuild);
        }

        private static string BuildMiniRoomDetail(int score, bool isCurrentTurn, string seat)
        {
            return isCurrentTurn
                ? $"{seat} | Score {score} | Current turn"
                : $"{seat} | Score {score}";
        }
    }
}
