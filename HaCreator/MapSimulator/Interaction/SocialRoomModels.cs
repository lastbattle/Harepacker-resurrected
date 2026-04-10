using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.PacketLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

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
            bool? flip = null,
            byte miniRoomType = 0,
            int miniRoomSerial = 0,
            string miniRoomBalloonTitle = null,
            byte miniRoomBalloonByte0 = 0,
            byte miniRoomBalloonByte1 = 0,
            byte miniRoomBalloonByte2 = 0)
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
            MiniRoomType = miniRoomType;
            MiniRoomSerial = Math.Max(0, miniRoomSerial);
            MiniRoomBalloonTitle = miniRoomBalloonTitle ?? string.Empty;
            MiniRoomBalloonByte0 = miniRoomBalloonByte0;
            MiniRoomBalloonByte1 = miniRoomBalloonByte1;
            MiniRoomBalloonByte2 = miniRoomBalloonByte2;
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
        public byte MiniRoomType { get; }
        public int MiniRoomSerial { get; }
        public string MiniRoomBalloonTitle { get; }
        public byte MiniRoomBalloonByte0 { get; }
        public byte MiniRoomBalloonByte1 { get; }
        public byte MiniRoomBalloonByte2 { get; }
        public bool HasMiniRoomBalloon => MiniRoomType != 0 && !string.IsNullOrWhiteSpace(MiniRoomBalloonTitle);
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
            PacketSlotIndex = packetSlotIndex >= 0 ? packetSlotIndex : null;
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

            PacketSlotIndex = packetSlotIndex >= 0 ? packetSlotIndex : null;
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

    public sealed class SocialRoomSoldItemEntry
    {
        public SocialRoomSoldItemEntry(
            int itemId,
            string itemName,
            string buyerName,
            int quantitySold,
            int bundleCount,
            int bundlePrice,
            int grossMeso,
            int taxMeso,
            int netMeso,
            int packetSlotIndex)
        {
            ItemId = Math.Max(0, itemId);
            ItemName = string.IsNullOrWhiteSpace(itemName) ? "Unknown item" : itemName;
            BuyerName = string.IsNullOrWhiteSpace(buyerName) ? "Visitor" : buyerName;
            QuantitySold = Math.Max(1, quantitySold);
            BundleCount = Math.Max(1, bundleCount);
            BundlePrice = Math.Max(0, bundlePrice);
            GrossMeso = Math.Max(0, grossMeso);
            TaxMeso = Math.Max(0, taxMeso);
            NetMeso = Math.Max(0, netMeso);
            PacketSlotIndex = Math.Max(0, packetSlotIndex);
        }

        public int ItemId { get; }
        public string ItemName { get; }
        public string BuyerName { get; }
        public int QuantitySold { get; }
        public int BundleCount { get; }
        public int BundlePrice { get; }
        public int GrossMeso { get; }
        public int TaxMeso { get; }
        public int NetMeso { get; }
        public int PacketSlotIndex { get; }
    }

    public sealed partial class SocialRoomRuntime
    {
        private static readonly Dictionary<string, string> EmployeeNpcFuncHeadlineCache = new(StringComparer.Ordinal);
        private const int MiniRoomOmokBoardSize = 15;
        private const int TradingRoomClientItemSlotCount = 9;
        private const byte TradingRoomPutItemPacketType = 15;
        private const byte TradingRoomPutMoneyPacketType = 16;
        private const byte TradingRoomTradePacketType = 17;
        private const byte TradingRoomItemCrcPacketType = 20;
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
        private const int OmokWinStringPoolId = 0x1D4;
        private const int OmokTieStringPoolId = 0x1D5;
        private const int OmokLoseStringPoolId = 0x1D6;
        private const int OmokIncomingTiePromptStringPoolId = 0x1D9;
        private const int OmokOutgoingTiePromptStringPoolId = 0x1DA;
        private const int OmokTieDeclinedStringPoolId = 0x1DB;
        private const int OmokIncomingRetreatPromptStringPoolId = 0x1DD;
        private const int OmokOutgoingRetreatPromptStringPoolId = 0x1DE;
        private const int OmokRetreatDeclinedStringPoolId = 0x1DF;
        private const int OmokTimerTextStringPoolId = 0x1E5;
        private const int OmokTurnTextStringPoolId = 0x1E6;
        private const int OmokSoundDrawStringPoolId = 0x645;
        private const int OmokSoundWinStringPoolId = 0x646;
        private const int OmokSoundLoseStringPoolId = 0x647;
        private const int OmokSoundTimerStringPoolId = 0x648;
        private const int OmokSoundBlackStoneStringPoolId = 0x64B;
        private const int OmokSoundWhiteStoneStringPoolId = 0x64C;
        private const byte MiniRoomBaseInvitePacketSubType = 2;
        private const byte MiniRoomBaseInviteResultPacketSubType = 3;
        private const byte MiniRoomBaseEnterPacketSubType = 4;
        private const byte MiniRoomBaseEnterResultPacketSubType = 5;
        private const byte MiniRoomBaseUpdatePacketSubType = 6;
        private const byte MiniRoomBaseChatPacketSubType = 7;
        private const byte MiniRoomBaseChatAltPacketSubType = 8;
        private const byte MiniRoomBaseAvatarPacketSubType = 9;
        private const byte MiniRoomBaseLeavePacketSubType = 10;
        private const byte MiniRoomBaseCheckSsnPacketSubType = 14;
        private const ushort EmployeeEnterFieldOpcode = 319;
        private const ushort EmployeeLeaveFieldOpcode = 320;
        private const ushort EmployeeMiniRoomBalloonOpcode = 321;

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

        private readonly record struct PacketOwnedTradeItem(
            byte SlotType,
            long? BaseExpirationTime,
            int ItemId,
            int Quantity,
            InventoryType InventoryType,
            bool HasCashSerialNumber,
            long CashSerialNumber,
            string Title,
            string MetadataSummary,
            long? NonCashSerialNumber,
            long? ExpirationTime,
            int? TailValue,
            string TailMetadataSummary);
        private readonly record struct MerchantPacketItemRow(short Number, short Set, int Price, PacketOwnedTradeItem Item);
        private readonly record struct MiniRoomBaseEnterResultPayload(int RoomType, int ResultCode, int MaxUsers, int MyPosition, int OccupantCount);
        private readonly record struct OmokMoveHistoryEntry(int X, int Y, int StoneValue, int SeatIndex);
        private readonly record struct TradeVerificationEntry(int ItemId, uint Checksum);

        private readonly List<SocialRoomOccupant> _occupants;
        private readonly List<SocialRoomItemEntry> _items;
        private readonly List<string> _notes;
        private readonly List<SocialRoomChatEntry> _chatEntries;
        private readonly List<string> _savedVisitors;
        private readonly List<string> _blockedVisitors;
        private readonly List<EntrustedShopVisitLogEntrySnapshot> _entrustedVisitLogEntries;
        private readonly Queue<string> _pendingVisitorNames;
        private readonly List<InventoryEscrowEntry> _inventoryEscrow;
        private readonly List<SocialRoomRemoteInventoryEntry> _remoteInventoryEntries;
        private readonly List<SocialRoomSoldItemEntry> _soldItems;
        private readonly List<SocialRoomItemEntry> _defaultItems;
        private readonly List<SocialRoomOccupant> _defaultOccupants;
        private readonly List<SocialRoomRemoteInventoryEntry> _defaultRemoteInventoryEntries;
        private readonly List<OmokMoveHistoryEntry> _miniRoomOmokMoveHistory;
        private readonly List<TradeVerificationEntry> _tradeLocalVerificationEntries;
        private readonly List<TradeVerificationEntry> _tradeRemoteVerificationEntries;
        private readonly SocialRoomRuntimeSnapshot _defaultSnapshot;
        private readonly int[] _miniRoomOmokBoard;
        private int _miniRoomModeIndex;
        private int _miniRoomWagerAmount;
        private int _miniRoomLocalSeatIndex;
        private int _miniRoomOmokCurrentTurnIndex;
        private int _miniRoomOmokWinnerIndex;
        private int _miniRoomOmokLastMoveX;
        private int _miniRoomOmokLastMoveY;
        private int _miniRoomOmokTimeLeftMs;
        private int _miniRoomOmokTimeFloor;
        private int _miniRoomOmokStoneAnimationTimeLeftMs;
        private int _miniRoomOmokDialogEffectTimeLeftMs;
        private int _miniRoomOmokOwnerStoneValue;
        private int _miniRoomOmokGuestStoneValue;
        private int _tradeLocalOfferMeso;
        private int _tradeRemoteOfferMeso;
        private int _remoteInventoryMeso;
        private bool _miniRoomOmokInProgress;
        private bool _miniRoomOmokTieRequested;
        private bool _miniRoomOmokDrawRequestSent;
        private bool _miniRoomOmokDrawRequestSentTurn;
        private bool _miniRoomOmokRetreatRequested;
        private bool _miniRoomOmokRetreatRequestSent;
        private bool _miniRoomOmokRetreatRequestSentTurn;
        private DateTime? _miniRoomOmokLastTimedStateUtc;
        private string _miniRoomOmokPendingPromptText = string.Empty;
        private string _miniRoomOmokLastClientSoundPath = string.Empty;
        private string _miniRoomOmokLastOutboundPacketSummary = string.Empty;
        private bool _tradeLocalLocked;
        private bool _tradeRemoteLocked;
        private bool _tradeLocalAccepted;
        private bool _tradeRemoteAccepted;
        private bool _tradeVerificationPending;
        private bool _tradeLocalVerificationReady;
        private bool _tradeRemoteVerificationReady;
        private bool _tradeAutoCrcReplyPending;
        private DateTime? _entrustedPermitExpiresAtUtc;
        private EntrustedShopChildDialogKind? _entrustedChildDialogKind;
        private int _entrustedVisitListSelectedIndex = -1;
        private int _entrustedBlacklistSelectedIndex = -1;
        private EntrustedShopBlacklistPromptRequest _entrustedBlacklistPromptRequest;
        private EntrustedShopNoticeSnapshot _entrustedBlacklistNotice;
        private string _miniRoomOmokDialogStatus = string.Empty;
        private string _entrustedChildDialogStatus = string.Empty;
        private int _employeeTemplateId;
        private bool _employeeUseOwnerAnchor = true;
        private int _employeeAnchorOffsetX;
        private int _employeeAnchorOffsetY;
        private int _employeeWorldX;
        private int _employeeWorldY;
        private bool _employeeHasWorldPosition;
        private bool? _employeeFlip;
        private int _employeePacketEmployerId;
        private readonly SocialRoomEmployeePoolRuntime _employeePoolRuntime = new();
        private bool _inventoryBackedRows;
        private bool _suspendPersistence;
        private string _persistenceKey;
        private string _lastPacketOwnerSummary;
        private int _personalShopTotalSoldGross;
        private int _personalShopTotalReceivedNet;
        private DateTime? _miniRoomOmokLastTimerUtc;
        private Action _miniRoomToggleReadyHandler;
        private Action _miniRoomStartHandler;
        private Action _miniRoomModeHandler;
        private Action<string, SocialRoomRuntimeSnapshot> _persistStateHandler;
        private readonly ShopDialogPacketOwner _shopDialogPacketOwner;
        private IInventoryRuntime _inventoryRuntime;
        public Func<LoginAvatarLook, CharacterBuild> AvatarBuildResolver { get; set; }
        public Action<string, int> SocialChatObserved { get; set; }

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
            _entrustedVisitLogEntries = new List<EntrustedShopVisitLogEntrySnapshot>();
            _pendingVisitorNames = new Queue<string>(new[] { "Rondo", "Rin", "Maya", "Pia", "Targa", "Rowen" });
            _inventoryEscrow = new List<InventoryEscrowEntry>();
            _remoteInventoryEntries = new List<SocialRoomRemoteInventoryEntry>();
            _soldItems = new List<SocialRoomSoldItemEntry>();
            _defaultItems = items?.Select(item => new SocialRoomItemEntry(item.OwnerName, item.ItemName, item.Quantity, item.MesoAmount, item.Detail, item.IsLocked, item.IsClaimed, item.ItemId, item.PacketSlotIndex)).ToList()
                ?? new List<SocialRoomItemEntry>();
            _defaultOccupants = occupants?.Select(occupant => new SocialRoomOccupant(occupant.Name, occupant.Role, occupant.Detail, occupant.IsReady, occupant.AvatarBuild)).ToList()
                ?? new List<SocialRoomOccupant>();
            _defaultRemoteInventoryEntries = new List<SocialRoomRemoteInventoryEntry>();
            _miniRoomOmokMoveHistory = new List<OmokMoveHistoryEntry>();
            _tradeLocalVerificationEntries = new List<TradeVerificationEntry>();
            _tradeRemoteVerificationEntries = new List<TradeVerificationEntry>();
            _miniRoomLocalSeatIndex = 0;
            _miniRoomOmokBoard = new int[MiniRoomOmokBoardSize * MiniRoomOmokBoardSize];
            _miniRoomOmokCurrentTurnIndex = 0;
            _miniRoomOmokWinnerIndex = -1;
            _miniRoomOmokLastMoveX = -1;
            _miniRoomOmokLastMoveY = -1;
            _miniRoomOmokTimeLeftMs = 30000;
            _miniRoomOmokTimeFloor = 30;
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
            _shopDialogPacketOwner = CreateShopDialogPacketOwner();
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
        public IReadOnlyList<SocialRoomSoldItemEntry> SoldItems => _soldItems;
        public IReadOnlyList<SocialRoomRemoteInventoryEntry> RemoteInventoryEntries => _remoteInventoryEntries;
        public IReadOnlyList<EntrustedShopVisitLogEntrySnapshot> EntrustedVisitLogEntries => _entrustedVisitLogEntries;
        public int RemoteInventoryMeso => _remoteInventoryMeso;
        public string RemoteTraderName => ResolveRemoteTraderName();
        public int TradeLocalOfferMeso => _tradeLocalOfferMeso;
        public int TradeRemoteOfferMeso => _tradeRemoteOfferMeso;
        public bool TradeLocalLocked => _tradeLocalLocked;
        public bool TradeRemoteLocked => _tradeRemoteLocked;
        public bool TradeLocalAccepted => _tradeLocalAccepted;
        public bool TradeRemoteAccepted => _tradeRemoteAccepted;
        public bool TradeVerificationPending => _tradeVerificationPending;
        public bool TradeLocalVerificationReady => _tradeLocalVerificationReady;
        public bool TradeRemoteVerificationReady => _tradeRemoteVerificationReady;
        public int TradeLocalVerificationCount => _tradeLocalVerificationEntries.Count;
        public int TradeRemoteVerificationCount => _tradeRemoteVerificationEntries.Count;
        public int PersonalShopTotalSoldGross => _personalShopTotalSoldGross;
        public int PersonalShopTotalReceivedNet => _personalShopTotalReceivedNet;
        public int MiniRoomOmokBoardSizeValue => MiniRoomOmokBoardSize;
        public bool IsMiniRoomOmokActive => Kind == SocialRoomKind.MiniRoom && _miniRoomModeIndex == 0;
        public bool IsMiniRoomOmokInProgress => _miniRoomOmokInProgress;
        public int MiniRoomOmokCurrentTurnIndex => _miniRoomOmokCurrentTurnIndex;
        public int MiniRoomOmokWinnerIndex => _miniRoomOmokWinnerIndex;
        public int MiniRoomOmokLastMoveX => _miniRoomOmokLastMoveX;
        public int MiniRoomOmokLastMoveY => _miniRoomOmokLastMoveY;
        public int MiniRoomOmokTimeLeftMs => _miniRoomOmokTimeLeftMs;
        public int MiniRoomOmokTimeFloor => _miniRoomOmokTimeFloor;
        public int MiniRoomLocalSeatIndex => _miniRoomLocalSeatIndex;
        public bool MiniRoomOmokTieRequested => _miniRoomOmokTieRequested;
        public bool MiniRoomOmokDrawRequestSent => _miniRoomOmokDrawRequestSent;
        public bool MiniRoomOmokRetreatRequested => _miniRoomOmokRetreatRequested;
        public bool MiniRoomOmokRetreatRequestSent => _miniRoomOmokRetreatRequestSent;
        public int MiniRoomOmokStoneAnimationTimeLeftMs => _miniRoomOmokStoneAnimationTimeLeftMs;
        public int MiniRoomOmokDialogEffectTimeLeftMs => _miniRoomOmokDialogEffectTimeLeftMs;
        public string MiniRoomOmokDialogStatus => _miniRoomOmokDialogStatus;
        public string MiniRoomOmokPendingPromptText => _miniRoomOmokPendingPromptText;
        public string MiniRoomOmokLastClientSoundPath => _miniRoomOmokLastClientSoundPath;
        public string MiniRoomOmokLastOutboundPacketSummary => _miniRoomOmokLastOutboundPacketSummary;
        public EntrustedShopChildDialogSnapshot EntrustedChildDialog => BuildEntrustedChildDialogSnapshot();
        public Func<EntrustedShopBlacklistPromptRequest, bool> EntrustedBlacklistPromptRequested { get; set; }
        public Action<EntrustedShopNoticeSnapshot> EntrustedBlacklistNoticeRequested { get; set; }

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
                PacketOwnerSummary = _lastPacketOwnerSummary,
                PersonalShopTotalSoldGross = _personalShopTotalSoldGross,
                PersonalShopTotalReceivedNet = _personalShopTotalReceivedNet,
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
                MiniRoomOmokDrawRequestSent = _miniRoomOmokDrawRequestSent,
                MiniRoomOmokDrawRequestSentTurn = _miniRoomOmokDrawRequestSentTurn,
                MiniRoomOmokRetreatRequested = _miniRoomOmokRetreatRequested,
                MiniRoomOmokRetreatRequestSent = _miniRoomOmokRetreatRequestSent,
                MiniRoomOmokRetreatRequestSentTurn = _miniRoomOmokRetreatRequestSentTurn,
                MiniRoomOmokTimeLeftMs = _miniRoomOmokTimeLeftMs,
                MiniRoomOmokTimeFloor = _miniRoomOmokTimeFloor,
                MiniRoomOmokStoneAnimationTimeLeftMs = _miniRoomOmokStoneAnimationTimeLeftMs,
                MiniRoomOmokDialogEffectTimeLeftMs = _miniRoomOmokDialogEffectTimeLeftMs,
                MiniRoomOmokDialogStatus = _miniRoomOmokDialogStatus,
                MiniRoomOmokBoard = _miniRoomOmokBoard.ToList(),
                MiniRoomOmokMoveHistory = _miniRoomOmokMoveHistory
                    .Select(move => new SocialRoomOmokMoveSnapshot
                    {
                        X = move.X,
                        Y = move.Y,
                        StoneValue = move.StoneValue,
                        SeatIndex = move.SeatIndex
                    })
                    .ToList(),
                TradeLocalOfferMeso = _tradeLocalOfferMeso,
                TradeRemoteOfferMeso = _tradeRemoteOfferMeso,
                TradeLocalLocked = _tradeLocalLocked,
                TradeRemoteLocked = _tradeRemoteLocked,
                TradeLocalAccepted = _tradeLocalAccepted,
                TradeRemoteAccepted = _tradeRemoteAccepted,
                TradeVerificationPending = _tradeVerificationPending,
                TradeLocalVerificationReady = _tradeLocalVerificationReady,
                TradeRemoteVerificationReady = _tradeRemoteVerificationReady,
                TradeLocalVerificationEntries = _tradeLocalVerificationEntries
                    .Select(entry => new SocialRoomTradeVerificationEntrySnapshot
                    {
                        ItemId = entry.ItemId,
                        Checksum = entry.Checksum
                    })
                    .ToList(),
                TradeRemoteVerificationEntries = _tradeRemoteVerificationEntries
                    .Select(entry => new SocialRoomTradeVerificationEntrySnapshot
                    {
                        ItemId = entry.ItemId,
                        Checksum = entry.Checksum
                    })
                    .ToList(),
                EntrustedPermitExpiresAtUtc = _entrustedPermitExpiresAtUtc,
                EmployeeTemplateId = _employeeTemplateId,
                EmployeeUseOwnerAnchor = _employeeUseOwnerAnchor,
                EmployeeAnchorOffsetX = _employeeAnchorOffsetX,
                EmployeeAnchorOffsetY = _employeeAnchorOffsetY,
                EmployeeWorldX = _employeeWorldX,
                EmployeeWorldY = _employeeWorldY,
                EmployeeHasWorldPosition = _employeeHasWorldPosition,
                EmployeeFlip = _employeeFlip,
                EmployeePoolEntries = _employeePoolRuntime.BuildSnapshots().ToList(),
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
                SoldItems = _soldItems
                    .Select(item => new SocialRoomSoldItemSnapshot
                    {
                        ItemId = item.ItemId,
                        ItemName = item.ItemName,
                        BuyerName = item.BuyerName,
                        QuantitySold = item.QuantitySold,
                        BundleCount = item.BundleCount,
                        BundlePrice = item.BundlePrice,
                        GrossMeso = item.GrossMeso,
                        TaxMeso = item.TaxMeso,
                        NetMeso = item.NetMeso,
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
                EntrustedVisitListSelectedIndex = _entrustedVisitListSelectedIndex,
                EntrustedBlacklistSelectedIndex = _entrustedBlacklistSelectedIndex,
                EntrustedChildDialogStatus = _entrustedChildDialogStatus,
                EntrustedVisitLogEntries = _entrustedVisitLogEntries
                    .Select(entry => new EntrustedShopVisitLogEntrySnapshot
                    {
                        Name = entry.Name,
                        StaySeconds = entry.StaySeconds
                    })
                    .ToList(),
                EntrustedChildDialog = BuildEntrustedChildDialogSnapshot(),
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
                _lastPacketOwnerSummary = source?.PacketOwnerSummary ?? _defaultSnapshot.PacketOwnerSummary ?? BuildDefaultPacketOwnerSummary();
                _personalShopTotalSoldGross = Math.Max(0, source?.PersonalShopTotalSoldGross ?? _defaultSnapshot.PersonalShopTotalSoldGross);
                _personalShopTotalReceivedNet = Math.Max(0, source?.PersonalShopTotalReceivedNet ?? _defaultSnapshot.PersonalShopTotalReceivedNet);
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
                _miniRoomOmokDrawRequestSent = source?.MiniRoomOmokDrawRequestSent ?? _defaultSnapshot.MiniRoomOmokDrawRequestSent;
                _miniRoomOmokDrawRequestSentTurn = source?.MiniRoomOmokDrawRequestSentTurn ?? _defaultSnapshot.MiniRoomOmokDrawRequestSentTurn;
                _miniRoomOmokRetreatRequested = source?.MiniRoomOmokRetreatRequested ?? _defaultSnapshot.MiniRoomOmokRetreatRequested;
                _miniRoomOmokRetreatRequestSent = source?.MiniRoomOmokRetreatRequestSent ?? _defaultSnapshot.MiniRoomOmokRetreatRequestSent;
                _miniRoomOmokRetreatRequestSentTurn = source?.MiniRoomOmokRetreatRequestSentTurn ?? _defaultSnapshot.MiniRoomOmokRetreatRequestSentTurn;
                _miniRoomOmokTimeLeftMs = Math.Max(0, source?.MiniRoomOmokTimeLeftMs ?? _defaultSnapshot.MiniRoomOmokTimeLeftMs);
                _miniRoomOmokTimeFloor = Math.Max(0, source?.MiniRoomOmokTimeFloor ?? _defaultSnapshot.MiniRoomOmokTimeFloor);
                _miniRoomOmokStoneAnimationTimeLeftMs = Math.Max(0, source?.MiniRoomOmokStoneAnimationTimeLeftMs ?? _defaultSnapshot.MiniRoomOmokStoneAnimationTimeLeftMs);
                _miniRoomOmokDialogEffectTimeLeftMs = Math.Max(0, source?.MiniRoomOmokDialogEffectTimeLeftMs ?? _defaultSnapshot.MiniRoomOmokDialogEffectTimeLeftMs);
                _miniRoomOmokDialogStatus = source?.MiniRoomOmokDialogStatus ?? _defaultSnapshot.MiniRoomOmokDialogStatus ?? string.Empty;
                _miniRoomOmokLastTimedStateUtc = null;
                _tradeLocalOfferMeso = Math.Max(0, source?.TradeLocalOfferMeso ?? _defaultSnapshot.TradeLocalOfferMeso);
                _tradeRemoteOfferMeso = Math.Max(0, source?.TradeRemoteOfferMeso ?? _defaultSnapshot.TradeRemoteOfferMeso);
                _tradeLocalLocked = source?.TradeLocalLocked ?? _defaultSnapshot.TradeLocalLocked;
                _tradeRemoteLocked = source?.TradeRemoteLocked ?? _defaultSnapshot.TradeRemoteLocked;
                _tradeLocalAccepted = source?.TradeLocalAccepted ?? _defaultSnapshot.TradeLocalAccepted;
                _tradeRemoteAccepted = source?.TradeRemoteAccepted ?? _defaultSnapshot.TradeRemoteAccepted;
                _tradeVerificationPending = source?.TradeVerificationPending ?? _defaultSnapshot.TradeVerificationPending;
                _tradeLocalVerificationReady = source?.TradeLocalVerificationReady ?? _defaultSnapshot.TradeLocalVerificationReady;
                _tradeRemoteVerificationReady = source?.TradeRemoteVerificationReady ?? _defaultSnapshot.TradeRemoteVerificationReady;
                _entrustedPermitExpiresAtUtc = source?.EntrustedPermitExpiresAtUtc ?? _defaultSnapshot.EntrustedPermitExpiresAtUtc;
                _entrustedVisitListSelectedIndex = source?.EntrustedVisitListSelectedIndex ?? _defaultSnapshot.EntrustedVisitListSelectedIndex;
                _entrustedBlacklistSelectedIndex = source?.EntrustedBlacklistSelectedIndex ?? _defaultSnapshot.EntrustedBlacklistSelectedIndex;
                _entrustedChildDialogStatus = source?.EntrustedChildDialogStatus ?? _defaultSnapshot.EntrustedChildDialogStatus ?? string.Empty;
                _entrustedChildDialogKind = source?.EntrustedChildDialog?.IsOpen == true
                    ? source.EntrustedChildDialog.Kind
                    : _defaultSnapshot.EntrustedChildDialog?.IsOpen == true ? _defaultSnapshot.EntrustedChildDialog.Kind : null;
                _employeeTemplateId = Math.Max(0, source?.EmployeeTemplateId ?? _defaultSnapshot.EmployeeTemplateId);
                _employeeUseOwnerAnchor = source?.EmployeeUseOwnerAnchor ?? _defaultSnapshot.EmployeeUseOwnerAnchor;
                _employeeAnchorOffsetX = source?.EmployeeAnchorOffsetX ?? _defaultSnapshot.EmployeeAnchorOffsetX;
                _employeeAnchorOffsetY = source?.EmployeeAnchorOffsetY ?? _defaultSnapshot.EmployeeAnchorOffsetY;
                _employeeWorldX = source?.EmployeeWorldX ?? _defaultSnapshot.EmployeeWorldX;
                _employeeWorldY = source?.EmployeeWorldY ?? _defaultSnapshot.EmployeeWorldY;
                _employeeHasWorldPosition = source?.EmployeeHasWorldPosition ?? _defaultSnapshot.EmployeeHasWorldPosition;
                _employeeFlip = source?.EmployeeFlip ?? _defaultSnapshot.EmployeeFlip;
                _employeePacketEmployerId = 0;
                _employeePoolRuntime.Restore(source?.EmployeePoolEntries);
                if (_employeePoolRuntime.PreferredEmployerId > 0)
                {
                    _employeePacketEmployerId = _employeePoolRuntime.PreferredEmployerId;
                }
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

                _soldItems.Clear();
                foreach (SocialRoomSoldItemSnapshot item in source?.SoldItems ?? Enumerable.Empty<SocialRoomSoldItemSnapshot>())
                {
                    _soldItems.Add(new SocialRoomSoldItemEntry(
                        item.ItemId,
                        item.ItemName,
                        item.BuyerName,
                        item.QuantitySold,
                        item.BundleCount,
                        item.BundlePrice,
                        item.GrossMeso,
                        item.TaxMeso,
                        item.NetMeso,
                        item.PacketSlotIndex));
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

                _entrustedVisitLogEntries.Clear();
                foreach (EntrustedShopVisitLogEntrySnapshot entry in source?.EntrustedVisitLogEntries ?? Enumerable.Empty<EntrustedShopVisitLogEntrySnapshot>())
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                    {
                        continue;
                    }

                    _entrustedVisitLogEntries.Add(new EntrustedShopVisitLogEntrySnapshot
                    {
                        Name = NormalizeName(entry.Name),
                        StaySeconds = Math.Max(0, entry.StaySeconds)
                    });
                }

                _blockedVisitors.Clear();
                foreach (string visitor in source?.BlockedVisitors ?? Enumerable.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(visitor))
                    {
                        _blockedVisitors.Add(visitor);
                    }
                }

                _entrustedVisitListSelectedIndex = NormalizeEntrustedDialogSelectionIndex(_entrustedVisitListSelectedIndex, _entrustedVisitLogEntries.Count);
                _entrustedBlacklistSelectedIndex = NormalizeEntrustedDialogSelectionIndex(_entrustedBlacklistSelectedIndex, _blockedVisitors.Count);

                int[] boardSource = ((source?.MiniRoomOmokBoard?.Count ?? 0) == _miniRoomOmokBoard.Length
                    ? source.MiniRoomOmokBoard
                    : _defaultSnapshot.MiniRoomOmokBoard)?.ToArray()
                    ?? new int[_miniRoomOmokBoard.Length];
                Array.Copy(boardSource, _miniRoomOmokBoard, _miniRoomOmokBoard.Length);

                _miniRoomOmokMoveHistory.Clear();
                IEnumerable<SocialRoomOmokMoveSnapshot> moveHistory = source?.MiniRoomOmokMoveHistory?.Count > 0
                    ? source.MiniRoomOmokMoveHistory
                    : _defaultSnapshot.MiniRoomOmokMoveHistory;
                foreach (SocialRoomOmokMoveSnapshot move in moveHistory ?? Enumerable.Empty<SocialRoomOmokMoveSnapshot>())
                {
                    if (!IsValidOmokCoordinate(move?.X ?? -1, move?.Y ?? -1))
                    {
                        continue;
                    }

                    _miniRoomOmokMoveHistory.Add(new OmokMoveHistoryEntry(
                        move.X,
                        move.Y,
                        NormalizeOmokStoneValue(move.StoneValue, fallback: ResolveOmokStoneValueForSeat(move.SeatIndex)),
                        Math.Clamp(move.SeatIndex, 0, 1)));
                }

                _tradeLocalVerificationEntries.Clear();
                foreach (SocialRoomTradeVerificationEntrySnapshot entry in source?.TradeLocalVerificationEntries ?? Enumerable.Empty<SocialRoomTradeVerificationEntrySnapshot>())
                {
                    if (entry?.ItemId > 0)
                    {
                        _tradeLocalVerificationEntries.Add(new TradeVerificationEntry(entry.ItemId, entry.Checksum));
                    }
                }

                _tradeRemoteVerificationEntries.Clear();
                foreach (SocialRoomTradeVerificationEntrySnapshot entry in source?.TradeRemoteVerificationEntries ?? Enumerable.Empty<SocialRoomTradeVerificationEntrySnapshot>())
                {
                    if (entry?.ItemId > 0)
                    {
                        _tradeRemoteVerificationEntries.Add(new TradeVerificationEntry(entry.ItemId, entry.Checksum));
                    }
                }

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

        public EntrustedShopChildDialogSnapshot GetEntrustedChildDialogSnapshot()
        {
            return BuildEntrustedChildDialogSnapshot();
        }

        public string DescribeStatus()
        {
            RefreshTimedState(DateTime.UtcNow);
            return Kind switch
            {
                SocialRoomKind.MiniRoom => $"{RoomTitle}: state={RoomState}, mode={ModeName}, wager={_miniRoomWagerAmount:N0}, occupants={_occupants.Count}/{Capacity}, omokTurn={ResolveOmokTurnName()}, winner={ResolveOmokWinnerName()}",
                SocialRoomKind.PersonalShop => $"{RoomTitle}: state={RoomState}, listed={_items.Count}, savedVisitors={_savedVisitors.Count}, blacklist={_blockedVisitors.Count}, ledger={MesoAmount:N0}, employee={DescribeEmployeeState()}, packet={_lastPacketOwnerSummary}, dispatcher={_shopDialogPacketOwner?.OwnerName ?? "none"}",
                SocialRoomKind.EntrustedShop => $"{RoomTitle}: state={RoomState}, listed={_items.Count}, ledger={MesoAmount:N0}, permit={FormatPermitStatus(DateTime.UtcNow)}, employee={DescribeEmployeeState()}, packet={_lastPacketOwnerSummary}, dispatcher={_shopDialogPacketOwner?.OwnerName ?? "none"}",
                SocialRoomKind.TradingRoom => $"{RoomTitle}: state={RoomState}, localMeso={MesoAmount:N0}, remoteMeso={_tradeRemoteOfferMeso:N0}, remoteWallet={_remoteInventoryMeso:N0}, remoteItems={_remoteInventoryEntries.Sum(entry => entry.Quantity)}, lock={FormatTradePartyState(_tradeLocalLocked, _tradeRemoteLocked)}, accept={FormatTradePartyState(_tradeLocalAccepted, _tradeRemoteAccepted)}, crc={DescribeTradeVerificationStatus()}, escrowRows={_inventoryEscrow.Count}, packet={_lastPacketOwnerSummary}, dispatcher={_shopDialogPacketOwner?.OwnerName ?? "none"}",
                _ => RoomState
            };
        }

        private EntrustedShopChildDialogSnapshot BuildEntrustedChildDialogSnapshot()
        {
            if (Kind != SocialRoomKind.EntrustedShop || !_entrustedChildDialogKind.HasValue)
            {
                return null;
            }

            EntrustedShopChildDialogKind kind = _entrustedChildDialogKind.Value;
            int selectedIndex = kind == EntrustedShopChildDialogKind.VisitList
                ? NormalizeEntrustedDialogSelectionIndex(_entrustedVisitListSelectedIndex, _entrustedVisitLogEntries.Count)
                : NormalizeEntrustedDialogSelectionIndex(_entrustedBlacklistSelectedIndex, _blockedVisitors.Count);
            List<EntrustedShopChildDialogEntrySnapshot> entries = kind == EntrustedShopChildDialogKind.VisitList
                ? _entrustedVisitLogEntries
                    .Select((entry, index) => new EntrustedShopChildDialogEntrySnapshot
                    {
                        PrimaryText = entry.Name,
                        SecondaryText = $"{entry.StaySeconds}s",
                        IsSelected = index == selectedIndex
                    })
                    .ToList()
                : _blockedVisitors
                    .Select((entry, index) => new EntrustedShopChildDialogEntrySnapshot
                    {
                        PrimaryText = entry,
                        SecondaryText = $"{index + 1:00}",
                        IsSelected = index == selectedIndex
                    })
                    .ToList();
            bool canPrimaryAction = kind == EntrustedShopChildDialogKind.VisitList
                ? HasValidEntrustedVisitListSelection()
                : _blockedVisitors.Count < 20;
            bool canSecondaryAction = kind == EntrustedShopChildDialogKind.Blacklist && HasValidEntrustedBlacklistSelection();

            return new EntrustedShopChildDialogSnapshot
            {
                IsOpen = true,
                Kind = kind,
                OwnerName = ResolveEntrustedChildDialogOwnerName(kind),
                Title = kind == EntrustedShopChildDialogKind.VisitList ? "Visit List" : "Blacklist",
                Subtitle = kind == EntrustedShopChildDialogKind.VisitList
                    ? $"{_entrustedVisitLogEntries.Count} visitor entr{(_entrustedVisitLogEntries.Count == 1 ? "y" : "ies")}"
                    : $"{_blockedVisitors.Count}/20 blocked entr{(_blockedVisitors.Count == 1 ? "y" : "ies")}",
                StatusText = string.IsNullOrWhiteSpace(_entrustedChildDialogStatus)
                    ? kind == EntrustedShopChildDialogKind.VisitList
                        ? "Select a visit row to enable Save Name."
                        : "Add remains available until the blacklist reaches 20 entries."
                    : _entrustedChildDialogStatus,
                PrimaryActionText = kind == EntrustedShopChildDialogKind.VisitList ? "Save Name" : "Add",
                SecondaryActionText = kind == EntrustedShopChildDialogKind.VisitList ? "Close" : "Delete",
                FooterText = kind == EntrustedShopChildDialogKind.VisitList
                    ? "Save Name copies the selected visitor name to the clipboard."
                    : "Delete only enables when the selected blacklist row is valid.",
                CanPrimaryAction = canPrimaryAction,
                CanSecondaryAction = canSecondaryAction,
                SelectedIndex = selectedIndex,
                Entries = entries,
                BlacklistPromptRequest = kind == EntrustedShopChildDialogKind.Blacklist
                    ? CloneEntrustedBlacklistPromptRequest(_entrustedBlacklistPromptRequest)
                    : null,
                BlacklistNotice = kind == EntrustedShopChildDialogKind.Blacklist
                    ? CloneEntrustedBlacklistNotice(_entrustedBlacklistNotice)
                    : null
            };
        }

        private static int NormalizeEntrustedDialogSelectionIndex(int index, int count)
        {
            return count <= 0 || index < 0 || index >= count ? -1 : index;
        }

        private bool HasValidEntrustedVisitListSelection()
        {
            return NormalizeEntrustedDialogSelectionIndex(_entrustedVisitListSelectedIndex, _entrustedVisitLogEntries.Count) >= 0;
        }

        private bool HasValidEntrustedBlacklistSelection()
        {
            return NormalizeEntrustedDialogSelectionIndex(_entrustedBlacklistSelectedIndex, _blockedVisitors.Count) >= 0;
        }

        private static string ResolveEntrustedChildDialogOwnerName(EntrustedShopChildDialogKind kind)
        {
            return kind == EntrustedShopChildDialogKind.VisitList
                ? "CEntrustedShopDlg::CVisitListDlg"
                : "CEntrustedShopDlg::CBlackListDlg";
        }

        private static EntrustedShopBlacklistPromptRequest CloneEntrustedBlacklistPromptRequest(EntrustedShopBlacklistPromptRequest source)
        {
            if (source == null)
            {
                return null;
            }

            return new EntrustedShopBlacklistPromptRequest
            {
                OwnerName = source.OwnerName,
                Title = source.Title,
                PromptText = source.PromptText,
                DefaultText = source.DefaultText,
                CurrentText = source.CurrentText,
                StringPoolId = source.StringPoolId,
                MinimumLength = source.MinimumLength,
                MaximumLength = source.MaximumLength
            };
        }

        private static EntrustedShopNoticeSnapshot CloneEntrustedBlacklistNotice(EntrustedShopNoticeSnapshot source)
        {
            if (source == null)
            {
                return null;
            }

            return new EntrustedShopNoticeSnapshot
            {
                OwnerName = source.OwnerName,
                Title = source.Title,
                Text = source.Text,
                StringPoolId = source.StringPoolId
            };
        }

        public string DescribePacketOwnerStatus()
        {
            return _shopDialogPacketOwner?.DescribeStatus(_lastPacketOwnerSummary) ?? _lastPacketOwnerSummary;
        }

        public void RefreshTimedState(DateTime utcNow)
        {
            if (IsMiniRoomOmokActive)
            {
                RefreshOmokTimedState(utcNow);
            }

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

        private void RefreshOmokTimedState(DateTime utcNow)
        {
            if (_miniRoomOmokLastTimedStateUtc == null)
            {
                _miniRoomOmokLastTimedStateUtc = utcNow;
                return;
            }

            int elapsedMilliseconds = (int)Math.Clamp((utcNow - _miniRoomOmokLastTimedStateUtc.Value).TotalMilliseconds, 0d, 1000d);
            _miniRoomOmokLastTimedStateUtc = utcNow;
            if (elapsedMilliseconds <= 0)
            {
                return;
            }

            _miniRoomOmokStoneAnimationTimeLeftMs = Math.Max(0, _miniRoomOmokStoneAnimationTimeLeftMs - elapsedMilliseconds);
            _miniRoomOmokDialogEffectTimeLeftMs = Math.Max(0, _miniRoomOmokDialogEffectTimeLeftMs - elapsedMilliseconds);
            if (_miniRoomOmokDialogEffectTimeLeftMs == 0 && !_miniRoomOmokInProgress && _miniRoomOmokWinnerIndex < 0)
            {
                _miniRoomOmokDialogStatus = string.Empty;
            }

            if (!_miniRoomOmokInProgress || _miniRoomOmokWinnerIndex >= 0)
            {
                return;
            }

            _miniRoomOmokTimeLeftMs = Math.Max(0, _miniRoomOmokTimeLeftMs - elapsedMilliseconds);
            _miniRoomOmokTimeFloor = (_miniRoomOmokTimeLeftMs + 999) / 1000;
        }

        public SocialRoomFieldActorSnapshot GetFieldActorSnapshot(DateTime utcNow)
        {
            return GetFieldActorSnapshot(utcNow, null);
        }

        internal SocialRoomFieldActorSnapshot GetFieldActorSnapshot(DateTime utcNow, SocialRoomEmployeePoolEntryState pooledEmployeeOverride)
        {
            RefreshTimedState(utcNow);

            SocialRoomEmployeePoolEntryState pooledEmployee = pooledEmployeeOverride;
            bool hasPooledEmployee = pooledEmployee != null && pooledEmployee.IsVisible;
            if (!hasPooledEmployee)
            {
                hasPooledEmployee = TryGetVisibleEmployeePoolEntry(out pooledEmployee);
            }
            if (_employeePoolRuntime.HasEntries
                && !hasPooledEmployee
                && (Kind == SocialRoomKind.PersonalShop || Kind == SocialRoomKind.EntrustedShop))
            {
                return null;
            }

            if (Kind == SocialRoomKind.PersonalShop)
            {
                int templateId = hasPooledEmployee ? pooledEmployee.TemplateId : _employeeTemplateId;
                bool usesCashEmployee = templateId > 0;
                string headline = ResolveEmployeeDisplayHeadline(ResolveEmployeeFuncHeadline(SocialRoomFieldActorTemplate.Merchant, "Merchant"));
                string detail = $"{ResolveEmployeeDisplayOwnerName(pooledEmployee)} | {RoomState}";
                return new SocialRoomFieldActorSnapshot(
                    Kind,
                    usesCashEmployee ? SocialRoomFieldActorTemplate.CashEmployee : SocialRoomFieldActorTemplate.Merchant,
                    headline,
                    detail,
                    $"{(usesCashEmployee ? "cash" : "merchant")}|{templateId}|{ModeName}|{RoomState}{BuildEmployeePacketStateKeySuffix()}",
                    templateId: templateId,
                    useOwnerAnchor: hasPooledEmployee ? false : _employeeUseOwnerAnchor,
                    anchorOffsetX: _employeeAnchorOffsetX,
                    anchorOffsetY: _employeeAnchorOffsetY,
                    worldX: hasPooledEmployee ? pooledEmployee.WorldX : _employeeWorldX,
                    worldY: hasPooledEmployee ? pooledEmployee.WorldY : _employeeWorldY,
                    hasWorldPosition: hasPooledEmployee || _employeeHasWorldPosition,
                    flip: _employeeFlip,
                    miniRoomType: hasPooledEmployee ? pooledEmployee.MiniRoomType : (byte)0,
                    miniRoomSerial: hasPooledEmployee ? pooledEmployee.MiniRoomSerial : 0,
                    miniRoomBalloonTitle: hasPooledEmployee ? pooledEmployee.BalloonTitle : string.Empty,
                    miniRoomBalloonByte0: hasPooledEmployee ? pooledEmployee.BalloonByte0 : (byte)0,
                    miniRoomBalloonByte1: hasPooledEmployee ? pooledEmployee.BalloonByte1 : (byte)0,
                    miniRoomBalloonByte2: hasPooledEmployee ? pooledEmployee.BalloonByte2 : (byte)0);
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

            int entrustedTemplateId = hasPooledEmployee ? pooledEmployee.TemplateId : _employeeTemplateId;
            byte entrustedMiniRoomType = hasPooledEmployee ? pooledEmployee.MiniRoomType : (byte)0;
            SocialRoomFieldActorTemplate entrustedTemplate = ResolveEntrustedFieldActorTemplate(entrustedTemplateId, entrustedMiniRoomType);
            string entrustedHeadline = ResolveEmployeeDisplayHeadline(
                ResolveEmployeeFuncHeadline(
                    entrustedTemplate == SocialRoomFieldActorTemplate.StoreBanker
                        ? SocialRoomFieldActorTemplate.StoreBanker
                        : SocialRoomFieldActorTemplate.Merchant,
                    entrustedTemplate == SocialRoomFieldActorTemplate.StoreBanker ? "Store Banker" : "Merchant"));
            string entrustedDetail = $"{ResolveEmployeeDisplayOwnerName(pooledEmployee)} | {RoomState} | {permitStatus}";
            return new SocialRoomFieldActorSnapshot(
                Kind,
                entrustedTemplate,
                entrustedHeadline,
                entrustedDetail,
                $"{ResolveEntrustedStateKeyTemplate(entrustedTemplate)}|{entrustedTemplateId}|{ModeName}|{RoomState}|{permitStatus}{BuildEmployeePacketStateKeySuffix()}",
                templateId: entrustedTemplateId,
                useOwnerAnchor: hasPooledEmployee ? false : _employeeUseOwnerAnchor,
                anchorOffsetX: _employeeAnchorOffsetX,
                anchorOffsetY: _employeeAnchorOffsetY,
                worldX: hasPooledEmployee ? pooledEmployee.WorldX : _employeeWorldX,
                worldY: hasPooledEmployee ? pooledEmployee.WorldY : _employeeWorldY,
                hasWorldPosition: hasPooledEmployee || _employeeHasWorldPosition,
                flip: _employeeFlip,
                miniRoomType: entrustedMiniRoomType,
                miniRoomSerial: hasPooledEmployee ? pooledEmployee.MiniRoomSerial : 0,
                miniRoomBalloonTitle: hasPooledEmployee ? pooledEmployee.BalloonTitle : string.Empty,
                miniRoomBalloonByte0: hasPooledEmployee ? pooledEmployee.BalloonByte0 : (byte)0,
                miniRoomBalloonByte1: hasPooledEmployee ? pooledEmployee.BalloonByte1 : (byte)0,
                miniRoomBalloonByte2: hasPooledEmployee ? pooledEmployee.BalloonByte2 : (byte)0);
        }

        private static SocialRoomFieldActorTemplate ResolveEntrustedFieldActorTemplate(int templateId, byte miniRoomType)
        {
            if (templateId > 0)
            {
                return SocialRoomFieldActorTemplate.CashEmployee;
            }

            return miniRoomType == 5
                ? SocialRoomFieldActorTemplate.StoreBanker
                : SocialRoomFieldActorTemplate.Merchant;
        }

        private static string ResolveEntrustedStateKeyTemplate(SocialRoomFieldActorTemplate template)
        {
            return template switch
            {
                SocialRoomFieldActorTemplate.CashEmployee => "cash",
                SocialRoomFieldActorTemplate.StoreBanker => "banker",
                _ => "merchant"
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
            if (TryExtractEmployeePoolPacket(packetBytes, out ushort employeeOpcode, out byte[] employeePayload))
            {
                return TryDispatchEmployeePoolPacket(employeeOpcode, employeePayload, tickCount, out message);
            }

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
                    SocialRoomKind.PersonalShop or SocialRoomKind.EntrustedShop or SocialRoomKind.TradingRoom => _shopDialogPacketOwner?.TryDispatch(payload, reader, packetType, tickCount, out message) == true,
                    _ => FailPacket(packetType, out message)
                };
            }
            catch (EndOfStreamException)
            {
                message = $"Social-room packet ended unexpectedly: {BitConverter.ToString(payload)}";
                return false;
            }
        }

        private bool TryDispatchEmployeePoolPacket(ushort opcode, byte[] payload, int tickCount, out string message)
        {
            message = $"Employee-pool opcode {opcode} is packet-owned. Apply it through {nameof(PacketOwnedEmployeePoolDispatcher)} and sync room presentation from pooled snapshots.";
            TrackPacketOwnerSummary("CEmployeePool::OnPacket", opcode, tickCount, handled: false, message);
            return false;
        }

        public bool TryDispatchSyntheticDialogPacket(byte packetType, byte[] payload, int tickCount, out string message)
        {
            byte[] packetBytes = new byte[1 + (payload?.Length ?? 0)];
            packetBytes[0] = packetType;
            if (payload?.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, packetBytes, 1, payload.Length);
            }

            return TryDispatchPacketBytes(packetBytes, tickCount, out message);
        }

        public bool TryDispatchTradingRoomPacketOwnedMeso(int traderIndex, int offeredMeso, int tickCount, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Trading-room meso packets only apply to the trading-room shell.";
                return false;
            }

            if (offeredMeso < 0)
            {
                message = "Trading-room meso packets require a non-negative offer amount.";
                return false;
            }

            using MemoryStream stream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write((byte)Math.Clamp(traderIndex, 0, 1));
                writer.Write(offeredMeso);
            }

            return TryDispatchSyntheticDialogPacket(TradingRoomPutMoneyPacketType, stream.ToArray(), tickCount, out message);
        }

        public bool TryDispatchTradingRoomPacketOwnedTrade(int tickCount, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Trading-room trade packets only apply to the trading-room shell.";
                return false;
            }

            return TryDispatchSyntheticDialogPacket(TradingRoomTradePacketType, Array.Empty<byte>(), tickCount, out message);
        }

        public bool TryDispatchTradingRoomPacketOwnedExceedLimit(int tickCount, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Trading-room exceed-limit packets only apply to the trading-room shell.";
                return false;
            }

            return TryDispatchSyntheticDialogPacket(TradingRoomExceedLimitPacketType, Array.Empty<byte>(), tickCount, out message);
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
                    ClearOmokDialogRequests();
                    StatusMessage = errorCode == 67
                        ? "The Omok stone packet was rejected because that point is already occupied."
                        : $"The Omok stone packet was rejected (code {errorCode}).";
                    SetOmokDialogStatus(StatusMessage, 1500);
                    AddMiniRoomSystemMessage($"System : {StatusMessage}", isWarning: true);
                    SyncMiniRoomOmokPresentation();
                    PersistState();
                    message = StatusMessage;
                    return true;
                }
                case OmokTimeOverPacketType:
                    _miniRoomOmokCurrentTurnIndex = Math.Clamp((int)reader.ReadByte(), 0, 1);
                    ClearOmokDialogRequests();
                    ResetOmokTurnClock();
                    RoomState = _miniRoomOmokInProgress ? "Omok in progress" : RoomState;
                    StatusMessage = FormatOmokTurnStatus(_miniRoomOmokCurrentTurnIndex);
                    SetOmokDialogStatus($"Time over. {StatusMessage}", 1200);
                    RecordOmokSoundByStringPoolId(OmokSoundTimerStringPoolId);
                    SyncMiniRoomOmokPresentation();
                    PersistState();
                    message = StatusMessage;
                    return true;
                case OmokTieRequestPacketType:
                    _miniRoomOmokTieRequested = true;
                    _miniRoomOmokDrawRequestSent = false;
                    _miniRoomOmokDrawRequestSentTurn = false;
                    _miniRoomOmokPendingPromptText = ResolveOmokString(
                        OmokIncomingTiePromptStringPoolId,
                        "Your opponent requests a tie.\r\nWill you accept it?");
                    StatusMessage = _miniRoomOmokPendingPromptText;
                    SetOmokDialogStatus("COmokDlg::OnTieRequest opened the client Yes/No dialog and is waiting to send packet 51.", 2200);
                    AddMiniRoomSystemMessage($"System : {ResolveRemoteTraderName()} requested an Omok draw.");
                    SyncMiniRoomOmokPresentation();
                    PersistState();
                    message = StatusMessage;
                    return true;
                case OmokTieResultPacketType:
                    _miniRoomOmokTieRequested = false;
                    _miniRoomOmokDrawRequestSent = false;
                    _miniRoomOmokDrawRequestSentTurn = false;
                    ClearOmokPendingPrompt();
                    StatusMessage = ResolveOmokString(
                        OmokTieDeclinedStringPoolId,
                        "Your opponent denied your request for a tie.");
                    SetOmokDialogStatus(StatusMessage, 1500);
                    AddMiniRoomSystemMessage($"System : {StatusMessage}");
                    SyncMiniRoomOmokPresentation();
                    PersistState();
                    message = StatusMessage;
                    return true;
                case OmokRetreatRequestPacketType:
                    _miniRoomOmokRetreatRequested = true;
                    _miniRoomOmokRetreatRequestSent = false;
                    _miniRoomOmokRetreatRequestSentTurn = false;
                    _miniRoomOmokPendingPromptText = ResolveOmokString(
                        OmokIncomingRetreatPromptStringPoolId,
                        "Your oppentent has requested to \r\nwithdraw his/her last move.\r\nDo you accept?");
                    StatusMessage = _miniRoomOmokPendingPromptText;
                    SetOmokDialogStatus("COmokDlg::OnRetreatRequest opened the client Yes/No dialog and is waiting to send packet 55.", 2200);
                    AddMiniRoomSystemMessage($"System : {ResolveRemoteTraderName()} requested an Omok retreat.", isWarning: true);
                    SyncMiniRoomOmokPresentation();
                    PersistState();
                    message = StatusMessage;
                    return true;
                case OmokRetreatResultPacketType:
                {
                    bool accepted = reader.ReadByte() != 0;
                    if (!accepted)
                    {
                        _miniRoomOmokRetreatRequested = false;
                        _miniRoomOmokRetreatRequestSent = false;
                        _miniRoomOmokRetreatRequestSentTurn = false;
                        ClearOmokPendingPrompt();
                        StatusMessage = ResolveOmokString(
                            OmokRetreatDeclinedStringPoolId,
                            "Your opponent denied your request.");
                        SetOmokDialogStatus(StatusMessage, 1500);
                        AddMiniRoomSystemMessage($"System : {StatusMessage}", isWarning: true);
                        SyncMiniRoomOmokPresentation();
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
                case PersonalShopBasePacketType:
                {
                    bool handled = TryDispatchMiniRoomBasePacket(reader, tickCount, out message);
                    TrackPacketOwnerSummary("CMiniRoomBaseDlg::OnPacketBase", PersonalShopBasePacketType, tickCount, handled, message);
                    return handled;
                }
                default:
                    return FailPacket(packetType, out message);
            }
        }

        private bool TryDispatchPersonalShopPacket(PacketReader reader, byte packetType, int tickCount, out string message)
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
                    return TryDispatchMiniRoomBasePacket(reader, tickCount, out message);
                default:
                    return FailPacket(packetType, out message);
            }
        }

        private bool TryDispatchEntrustedShopPacket(PacketReader reader, byte packetType, int tickCount, out string message)
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
                    return TryDispatchPersonalShopPacket(reader, packetType, tickCount, out message);
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
                    return payload != null && payload.Length >= 5
                        ? TryApplyTradingRoomItemPacket(payload, out message)
                        : TryApplyTradingRoomItemPacket(reader, out message);
                case TradingRoomTradePacketType:
                    ApplyTradingRoomTradePacket();
                    message = StatusMessage;
                    return true;
                case TradingRoomItemCrcPacketType:
                    return TryApplyTradingRoomCrcPacket(reader, out message);
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

        private bool TryApplyTradingRoomItemPacket(PacketReader reader, out string message)
        {
            message = null;
            if (reader == null || reader.Remaining < 3)
            {
                message = "Trading-room put-item packet is too short to decode.";
                return false;
            }

            int traderIndex = reader.ReadByte();
            int slotIndex = Math.Max(1, (int)reader.ReadByte());
            byte[] itemPayload = reader.ReadBytes(reader.Remaining);
            if (!TryDecodePacketOwnedTradeItem(itemPayload, out PacketOwnedTradeItem item, out string error))
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
            _tradeRemoteLocked = true;
            _tradeRemoteAccepted = false;
            _tradeLocalAccepted = false;
            _tradeRemoteVerificationReady = false;
            _tradeRemoteVerificationEntries.Clear();
            _tradeLocalVerificationEntries.Clear();
            _tradeLocalVerificationEntries.AddRange(BuildTradeVerificationEntries(isLocalParty: true));
            _tradeLocalVerificationReady = true;
            _tradeAutoCrcReplyPending = true;
            _tradeVerificationPending = true;
            RoomState = "CRC verification";
            StatusMessage = $"Trading-room packet requested CRC verification. Prepared {_tradeLocalVerificationEntries.Count} local item checksum entr{(_tradeLocalVerificationEntries.Count == 1 ? "y" : "ies")} for subtype {TradingRoomItemCrcPacketType}, including zero-row replies when no local offer items are staged.";

            RefreshTradeOccupantsAndRows();
            PersistState();
        }

        private bool TryApplyTradingRoomCrcPacket(PacketReader reader, out string message)
        {
            message = null;
            int count = reader.ReadByte();
            if (count < 0)
            {
                message = "Trading-room CRC packet contained an invalid row count.";
                return false;
            }

            List<TradeVerificationEntry> entries = new List<TradeVerificationEntry>(count);
            for (int i = 0; i < count; i++)
            {
                int itemId = reader.ReadInt();
                uint checksum = unchecked((uint)reader.ReadInt());
                if (itemId <= 0)
                {
                    message = $"Trading-room CRC packet row {i} contained an invalid item id.";
                    return false;
                }

                entries.Add(new TradeVerificationEntry(itemId, checksum));
            }

            _tradeRemoteVerificationEntries.Clear();
            _tradeRemoteVerificationEntries.AddRange(entries);
            bool remoteVerificationMatched = TryValidateTradeVerificationEntries(isLocalParty: false, entries, out string verificationDetail);
            _tradeRemoteVerificationReady = remoteVerificationMatched;
            if (_tradeLocalLocked && _tradeRemoteLocked && _tradeLocalVerificationEntries.Count == 0)
            {
                _tradeLocalVerificationEntries.AddRange(BuildTradeVerificationEntries(isLocalParty: true));
                _tradeLocalVerificationReady = true;
            }

            _tradeVerificationPending = !_tradeLocalVerificationReady
                || !_tradeRemoteVerificationReady
                || !remoteVerificationMatched;
            _tradeAutoCrcReplyPending = false;
            RoomState = _tradeVerificationPending
                ? remoteVerificationMatched ? "CRC verification" : "CRC mismatch"
                : "Locked";
            RefreshTradeOccupantsAndRows();
            StatusMessage = entries.Count == 0
                ? remoteVerificationMatched
                    ? "Trading-room CRC packet verified an empty remote checksum list against the current preview offer."
                    : $"Trading-room CRC packet carried an empty remote checksum list, but verification is still pending: {verificationDetail}"
                : remoteVerificationMatched
                    ? $"Trading-room CRC packet verified {entries.Count} remote checksum entr{(entries.Count == 1 ? "y" : "ies")} against the current preview offer."
                    : $"Trading-room CRC packet synced {entries.Count} remote checksum entr{(entries.Count == 1 ? "y" : "ies")}, but verification is still pending: {verificationDetail}";
            PersistState();
            message = StatusMessage;
            return true;
        }

        private void ApplyTradingRoomExceedLimitPacket()
        {
            _tradeLocalLocked = false;
            _tradeLocalAccepted = false;
            _tradeRemoteAccepted = false;
            _tradeVerificationPending = false;
            _tradeAutoCrcReplyPending = false;
            _tradeLocalVerificationReady = false;
            _tradeRemoteVerificationReady = false;
            _tradeLocalVerificationEntries.Clear();
            _tradeRemoteVerificationEntries.Clear();
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
                    detail: BuildTradingRoomPacketItemDetail(ownerLabel, slotIndex, item),
                    itemId: item.ItemId,
                    packetSlotIndex: slotIndex);
                _items.Add(entry);
            }
            else
            {
                entry.UpdatePacketIdentity(item.ItemId, slotIndex);
                entry.Update(BuildTradingRoomPacketItemDetail(ownerLabel, slotIndex, item), item.Quantity, 0, false, false);
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
            if (payload.Length < 1)
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

            if (!TryDecodePacketOwnedItemBody(
                payload.Slice(1),
                slotType,
                out long serialNumber,
            out int itemId,
            out int quantity,
            out bool hasCashSerialNumber,
            out long cashSerialNumber,
            out string title,
            out string metadataSummary,
            out long? nonCashSerialNumber,
            out long? baseExpirationTime,
            out long? expirationTime,
            out int? tailValue,
            out string tailMetadataSummary))
            {
                error = "Trading-room item payload did not contain a recognizable MapleStory item row.";
                return false;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            if (inventoryType == InventoryType.NONE)
            {
                error = $"Trading-room item payload resolved unsupported item id {itemId}.";
                return false;
            }

            item = new PacketOwnedTradeItem(
                slotType,
                baseExpirationTime,
                itemId,
                quantity,
                inventoryType,
                hasCashSerialNumber,
                cashSerialNumber,
                title,
                metadataSummary,
                nonCashSerialNumber,
                expirationTime,
                tailValue,
                tailMetadataSummary);
            return true;
        }

        private static bool TryDecodePacketOwnedItemBody(
            ReadOnlySpan<byte> payload,
            byte slotType,
            out long serialNumber,
            out int itemId,
            out int quantity,
            out bool hasCashSerialNumber,
            out long cashSerialNumber,
            out string title,
            out string metadataSummary,
            out long? nonCashSerialNumber,
            out long? baseExpirationTime,
            out long? expirationTime,
            out int? tailValue,
            out string tailMetadataSummary)
        {
            serialNumber = 0;
            itemId = 0;
            quantity = 1;
            hasCashSerialNumber = false;
            cashSerialNumber = 0;
            title = string.Empty;
            metadataSummary = string.Empty;
            nonCashSerialNumber = null;
            baseExpirationTime = null;
            expirationTime = null;
            tailValue = null;
            tailMetadataSummary = string.Empty;

            using MemoryStream stream = new MemoryStream(payload.ToArray(), writable: false);
            using BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
            if (stream.Length - stream.Position < sizeof(int) + sizeof(byte) + sizeof(long))
            {
                return false;
            }

            itemId = reader.ReadInt32();
            if (itemId <= 0 || InventoryItemMetadataResolver.ResolveInventoryType(itemId) == InventoryType.NONE)
            {
                return false;
            }

            hasCashSerialNumber = reader.ReadByte() != 0;
            if (hasCashSerialNumber)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    return false;
                }

                cashSerialNumber = reader.ReadInt64();
            }

            if (stream.Length - stream.Position < sizeof(long))
            {
                return false;
            }

            baseExpirationTime = reader.ReadInt64();
            return slotType switch
            {
                1 => TryDecodePacketOwnedEquipBody(reader, hasCashSerialNumber, out title, out metadataSummary, out nonCashSerialNumber, out expirationTime, out tailValue, out tailMetadataSummary),
                2 => TryDecodePacketOwnedBundleBody(reader, itemId, out quantity, out title, out metadataSummary, out tailMetadataSummary),
                3 => TryDecodePacketOwnedPetBody(reader, out title, out metadataSummary, out expirationTime, out tailMetadataSummary),
                _ => false
            };
        }

        private static string BuildTradingRoomPacketItemDetail(string ownerLabel, int slotIndex, PacketOwnedTradeItem item)
        {
            string baseExpirationText = BuildTradeDateDetail("Expire", item.BaseExpirationTime);
            string cashText = item.HasCashSerialNumber && item.CashSerialNumber > 0
                ? $" | liCashItemSN {item.CashSerialNumber}"
                : string.Empty;
            string titleText = string.IsNullOrWhiteSpace(item.Title)
                ? string.Empty
                : $" | sTitle {item.Title}";
            string metadataText = string.IsNullOrWhiteSpace(item.MetadataSummary)
                ? string.Empty
                : $" | {item.MetadataSummary}";
            string nonCashText = item.NonCashSerialNumber.HasValue && item.NonCashSerialNumber.Value > 0
                ? $" | liSN {item.NonCashSerialNumber.Value}"
                : string.Empty;
            string tailText = item.TailValue.HasValue && item.TailValue.Value != 0
                ? $" | nPrevBonusExpRate {item.TailValue.Value}"
                : string.Empty;
            string tailMetadataText = string.IsNullOrWhiteSpace(item.TailMetadataSummary)
                ? string.Empty
                : $" | {item.TailMetadataSummary}";
            return $"{ownerLabel} offer | Packet slot {slotIndex} | {item.InventoryType}{baseExpirationText}{cashText}{titleText}{metadataText}{nonCashText}{tailText}{tailMetadataText}";
        }

        private static bool TryDecodePacketOwnedEquipBody(
            BinaryReader reader,
            bool hasCashSerialNumber,
            out string title,
            out string metadataSummary,
            out long? nonCashSerialNumber,
            out long? expirationTime,
            out int? tailValue,
            out string tailMetadataSummary)
        {
            title = string.Empty;
            metadataSummary = string.Empty;
            nonCashSerialNumber = null;
            expirationTime = null;
            tailValue = null;
            tailMetadataSummary = string.Empty;
            Stream stream = reader.BaseStream;
            const int equipStatsByteLength = (sizeof(byte) * 2) + (sizeof(short) * 15);
            if (stream.Length - stream.Position < equipStatsByteLength)
            {
                return false;
            }

            byte remainingUpgradeCount = reader.ReadByte();
            byte upgradeCount = reader.ReadByte();
            short strength = reader.ReadInt16();
            short dexterity = reader.ReadInt16();
            short intelligence = reader.ReadInt16();
            short luck = reader.ReadInt16();
            short hp = reader.ReadInt16();
            short mp = reader.ReadInt16();
            short weaponAttack = reader.ReadInt16();
            short magicAttack = reader.ReadInt16();
            short weaponDefense = reader.ReadInt16();
            short magicDefense = reader.ReadInt16();
            short accuracy = reader.ReadInt16();
            short avoidability = reader.ReadInt16();
            short hands = reader.ReadInt16();
            short speed = reader.ReadInt16();
            short jump = reader.ReadInt16();
            if (!TryReadTradePacketMapleString(reader, out title))
            {
                return false;
            }

            const int tailLength = sizeof(short) + (sizeof(byte) * 2) + (sizeof(int) * 3) + (sizeof(byte) * 2) + (sizeof(short) * 5);
            int requiredTailLength = tailLength + (hasCashSerialNumber ? 0 : sizeof(long)) + sizeof(long) + sizeof(int);
            if (stream.Length - stream.Position < requiredTailLength)
            {
                return false;
            }

            short attribute = reader.ReadInt16();
            byte levelUpType = reader.ReadByte();
            byte level = reader.ReadByte();
            int experience = reader.ReadInt32();
            int durability = reader.ReadInt32();
            int itemUpgradeCount = reader.ReadInt32();
            byte grade = reader.ReadByte();
            byte bonusUpgradeCount = reader.ReadByte();
            short option1 = reader.ReadInt16();
            short option2 = reader.ReadInt16();
            short option3 = reader.ReadInt16();
            short socket1 = reader.ReadInt16();
            short socket2 = reader.ReadInt16();
            if (!hasCashSerialNumber)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    return false;
                }

                nonCashSerialNumber = reader.ReadInt64();
            }

            expirationTime = reader.ReadInt64();
            tailValue = reader.ReadInt32();
            metadataSummary = BuildEquipTradeMetadataSummary(
                remainingUpgradeCount,
                upgradeCount,
                strength,
                dexterity,
                intelligence,
                luck,
                hp,
                mp,
                weaponAttack,
                magicAttack,
                weaponDefense,
                magicDefense,
                accuracy,
                avoidability,
                hands,
                speed,
                jump,
                attribute,
                levelUpType,
                level,
                experience,
                durability,
                itemUpgradeCount,
                grade,
                bonusUpgradeCount,
                option1,
                option2,
                option3,
                socket1,
                socket2,
                expirationTime,
                tailValue);
            tailMetadataSummary = BuildResidualTradePacketSummary(reader, "EquipTail");
            return true;
        }

        private static bool TryDecodePacketOwnedBundleBody(BinaryReader reader, int itemId, out int quantity, out string title, out string metadataSummary, out string tailMetadataSummary)
        {
            quantity = 1;
            title = string.Empty;
            metadataSummary = string.Empty;
            tailMetadataSummary = string.Empty;
            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < sizeof(ushort))
            {
                return false;
            }

            quantity = Math.Max(1, (int)reader.ReadUInt16());
            if (!TryReadTradePacketMapleString(reader, out title))
            {
                return false;
            }

            if (stream.Length - stream.Position < sizeof(short))
            {
                return false;
            }

            short attribute = reader.ReadInt16();
            long rechargeableSerialNumber = 0;
            if (itemId / 10000 is 207 or 233)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    return false;
                }

                rechargeableSerialNumber = reader.ReadInt64();
            }

            metadataSummary = BuildBundleTradeMetadataSummary(attribute, rechargeableSerialNumber);
            tailMetadataSummary = BuildResidualTradePacketSummary(reader, "BundleTail");
            return true;
        }

        private static bool TryDecodePacketOwnedPetBody(BinaryReader reader, out string title, out string metadataSummary, out long? expirationTime, out string tailMetadataSummary)
        {
            title = string.Empty;
            metadataSummary = string.Empty;
            expirationTime = null;
            tailMetadataSummary = string.Empty;
            Stream stream = reader.BaseStream;
            const int petNameLength = 13;
            const int petTailLength = sizeof(byte) + sizeof(short) + sizeof(byte) + sizeof(long) + sizeof(short) + sizeof(ushort) + sizeof(int) + sizeof(short);
            if (stream.Length - stream.Position < petNameLength + petTailLength)
            {
                return false;
            }

            title = Encoding.ASCII.GetString(reader.ReadBytes(petNameLength)).TrimEnd('\0', ' ');
            byte level = reader.ReadByte();
            short closeness = reader.ReadInt16();
            byte fullness = reader.ReadByte();
            expirationTime = reader.ReadInt64();
            short attribute = reader.ReadInt16();
            ushort skill = reader.ReadUInt16();
            int remainingLife = reader.ReadInt32();
            short itemAttribute = reader.ReadInt16();
            metadataSummary = BuildPetTradeMetadataSummary(level, closeness, fullness, attribute, skill, remainingLife, itemAttribute, expirationTime);
            tailMetadataSummary = BuildResidualTradePacketSummary(reader, "PetTail");
            return true;
        }

        private static string BuildResidualTradePacketSummary(BinaryReader reader, string label)
        {
            if (reader?.BaseStream == null)
            {
                return string.Empty;
            }

            Stream stream = reader.BaseStream;
            long remaining = stream.Length - stream.Position;
            if (remaining <= 0)
            {
                return string.Empty;
            }

            byte[] leftover = reader.ReadBytes((int)remaining);
            if (leftover.Length == 0)
            {
                return string.Empty;
            }

            const int previewByteCount = 24;
            string preview = BitConverter.ToString(leftover, 0, Math.Min(previewByteCount, leftover.Length));
            return leftover.Length > previewByteCount
                ? $"{label} {leftover.Length}B {preview}..."
                : $"{label} {leftover.Length}B {preview}";
        }

        private static string BuildEquipTradeMetadataSummary(
            byte remainingUpgradeCount,
            byte upgradeCount,
            short strength,
            short dexterity,
            short intelligence,
            short luck,
            short hp,
            short mp,
            short weaponAttack,
            short magicAttack,
            short weaponDefense,
            short magicDefense,
            short accuracy,
            short avoidability,
            short hands,
            short speed,
            short jump,
            short attribute,
            byte levelUpType,
            byte level,
            int experience,
            int durability,
            int itemUpgradeCount,
            byte grade,
            byte bonusUpgradeCount,
            short option1,
            short option2,
            short option3,
            short socket1,
            short socket2,
            long? expirationTime,
            int? previousBonusExpRate)
        {
            List<string> parts = new List<string>();
            parts.Add($"RUC {remainingUpgradeCount}");
            if (upgradeCount > 0)
            {
                parts.Add($"CUC {upgradeCount}");
            }

            AppendStatPart(parts, "STR", strength);
            AppendStatPart(parts, "DEX", dexterity);
            AppendStatPart(parts, "INT", intelligence);
            AppendStatPart(parts, "LUK", luck);
            AppendStatPart(parts, "HP", hp);
            AppendStatPart(parts, "MP", mp);
            AppendStatPart(parts, "PAD", weaponAttack);
            AppendStatPart(parts, "MAD", magicAttack);
            AppendStatPart(parts, "PDD", weaponDefense);
            AppendStatPart(parts, "MDD", magicDefense);
            AppendStatPart(parts, "ACC", accuracy);
            AppendStatPart(parts, "EVA", avoidability);
            AppendStatPart(parts, "Hands", hands);
            AppendStatPart(parts, "Speed", speed);
            AppendStatPart(parts, "Jump", jump);

            if (attribute != 0)
            {
                parts.Add($"Attr 0x{(ushort)attribute:X4}");
            }

            if (levelUpType > 0)
            {
                parts.Add($"LvType {levelUpType}");
            }

            if (level > 0)
            {
                parts.Add($"Lv {level}");
            }

            if (experience > 0)
            {
                parts.Add($"EXP {experience}");
            }

            if (durability > 0)
            {
                parts.Add($"Dur {durability}");
            }

            if (itemUpgradeCount > 0)
            {
                parts.Add($"IUC {itemUpgradeCount}");
            }

            if (grade > 0)
            {
                parts.Add($"Grade {grade}");
            }

            if (bonusUpgradeCount > 0)
            {
                parts.Add($"CHUC {bonusUpgradeCount}");
            }

            if (option1 != 0 || option2 != 0 || option3 != 0)
            {
                parts.Add($"Opt {option1}/{option2}/{option3}");
            }

            if (socket1 != 0 || socket2 != 0)
            {
                parts.Add($"Socket {socket1}/{socket2}");
            }

            string expirationText = FormatTradePacketFileTime(expirationTime);
            if (!string.IsNullOrWhiteSpace(expirationText))
            {
                parts.Add($"ftEquipped {expirationText}");
            }

            if (previousBonusExpRate.HasValue && previousBonusExpRate.Value != 0)
            {
                parts.Add($"nPrevBonusExpRate {previousBonusExpRate.Value}");
            }

            return string.Join(", ", parts);
        }

        private static string BuildBundleTradeMetadataSummary(short attribute, long rechargeableSerialNumber)
        {
            List<string> parts = new List<string>();
            if (attribute != 0)
            {
                parts.Add($"Attr 0x{(ushort)attribute:X4}");
            }

            if (rechargeableSerialNumber > 0)
            {
                parts.Add($"RechargeSN {rechargeableSerialNumber}");
            }

            return string.Join(", ", parts);
        }

        private static string BuildPetTradeMetadataSummary(
            byte level,
            short closeness,
            byte fullness,
            short attribute,
            ushort skill,
            int remainingLife,
            short itemAttribute,
            long? expirationTime)
        {
            List<string> parts = new List<string>();
            parts.Add($"PetLv {level}");
            parts.Add($"Closeness {closeness}");
            parts.Add($"Fullness {fullness}");
            if (attribute != 0)
            {
                parts.Add($"Attr 0x{(ushort)attribute:X4}");
            }

            if (skill > 0)
            {
                parts.Add($"Skill 0x{skill:X4}");
            }

            if (remainingLife > 0)
            {
                parts.Add($"Life {remainingLife}");
            }

            if (itemAttribute > 0)
            {
                parts.Add($"Fatigue {itemAttribute}");
            }

            string expirationText = FormatTradePacketFileTime(expirationTime);
            if (!string.IsNullOrWhiteSpace(expirationText))
            {
                parts.Add($"dateDead {expirationText}");
            }

            if (itemAttribute != 0)
            {
                parts.Add($"ItemAttr 0x{(ushort)itemAttribute:X4}");
            }

            return string.Join(", ", parts);
        }

        private static string BuildTradeDateDetail(string label, long? fileTime)
        {
            string dateText = FormatTradePacketFileTime(fileTime);
            return string.IsNullOrWhiteSpace(dateText)
                ? string.Empty
                : $" | {label} {dateText}";
        }

        private static string FormatTradePacketFileTime(long? fileTime)
        {
            if (!fileTime.HasValue)
            {
                return null;
            }

            long value = fileTime.Value;
            if (value <= 0 || value == long.MaxValue)
            {
                return null;
            }

            try
            {
                return DateTime.FromFileTimeUtc(value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void AppendStatPart(List<string> parts, string label, short value)
        {
            if (value != 0)
            {
                parts.Add($"{label} {value:+#;-#;0}");
            }
        }

        private static bool TryReadTradePacketMapleString(BinaryReader reader, out string value)
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

        private string ResolveTradeOwnerName(int traderIndex)
        {
            return traderIndex == 0 ? OwnerName : ResolveRemoteTraderName();
        }

        private bool TryDispatchMiniRoomBasePacket(PacketReader reader, int tickCount, out string message)
        {
            message = null;
            byte packetSubType = reader.ReadByte();
            switch (packetSubType)
            {
                case MiniRoomBaseInvitePacketSubType:
                    return TryApplyMiniRoomBaseInvitePacket(reader, out message);
                case MiniRoomBaseInviteResultPacketSubType:
                    return TryApplyMiniRoomBaseInviteResultPacket(reader, out message);
                case MiniRoomBaseEnterPacketSubType:
                    return TryApplyMiniRoomBaseEnterPacket(reader, out message);
                case MiniRoomBaseEnterResultPacketSubType:
                    return TryApplyMiniRoomBaseEnterResultPacket(reader, out message);
                case MiniRoomBaseUpdatePacketSubType:
                    return TryDispatchMiniRoomBaseTypeSpecificPacket(reader, tickCount, out message);
                case MiniRoomBaseChatPacketSubType:
                case MiniRoomBaseChatAltPacketSubType:
                    return TryApplyMiniRoomBaseChatPacket(packetSubType, reader, out message);
                case MiniRoomBaseAvatarPacketSubType:
                    return TryApplyMiniRoomBaseAvatarPacket(reader, out message);
                case MiniRoomBaseLeavePacketSubType:
                    return TryApplyMiniRoomBaseLeavePacket(reader, out message);
                case MiniRoomBaseCheckSsnPacketSubType:
                    return TryApplyMiniRoomBaseCheckSsnPacket(reader, out message);
                default:
                    message = $"Mini-room base packet subtype {packetSubType} is not modeled for this room.";
                    return false;
            }
        }

        private bool TryApplyMiniRoomBaseInvitePacket(PacketReader reader, out string message)
        {
            int roomType = reader.ReadByte();
            string inviterName = NormalizeName(reader.ReadMapleString());
            int invitationSerial = reader.ReadInt();
            RoomState = "Invite received";
            StatusMessage = $"{inviterName} sent a {ResolveMiniRoomTypeLabel(roomType)} invite (serial {invitationSerial}) through CMiniRoomBaseDlg::OnInviteStatic.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        private bool TryApplyMiniRoomBaseChatPacket(byte packetSubType, PacketReader reader, out string message)
        {
            int chatType = reader.ReadByte();
            if (chatType == 7)
            {
                return TryApplyMiniRoomBaseGameMessagePacket(packetSubType, reader, out message);
            }

            return TryApplyMiniRoomBaseSpeakerChatPacket(packetSubType, chatType, reader, out message);
        }

        private bool TryApplyMiniRoomBaseGameMessagePacket(byte packetSubType, PacketReader reader, out string message)
        {
            int messageCode = reader.ReadByte();
            string characterName = NormalizeName(reader.ReadMapleString());
            int? stringPoolId = ResolveMiniRoomGameMessageStringPoolId(messageCode);
            string chatText = ResolveMiniRoomBaseGameMessageText(messageCode, characterName, stringPoolId);
            AppendSocialRoomChatEntry(chatText, SocialRoomChatTone.System, persistState: false);
            if (!string.IsNullOrWhiteSpace(chatText))
            {
                NotifySocialChatObserved(chatText);
            }

            RoomState = "Chat update";
            StatusMessage = stringPoolId.HasValue
                ? $"CMiniRoomBaseDlg::OnChat applied shared base subtype {packetSubType} game message code {messageCode} through StringPool id 0x{stringPoolId.Value:X}."
                : $"CMiniRoomBaseDlg::OnChat applied shared base subtype {packetSubType} game message code {messageCode} without a recovered StringPool id.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        private bool TryApplyMiniRoomBaseSpeakerChatPacket(byte packetSubType, int seatIndex, PacketReader reader, out string message)
        {
            string text = reader.ReadMapleString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                message = $"Mini-room base chat subtype {packetSubType} did not include a room-chat line.";
                return false;
            }

            AppendSocialRoomChatEntry(text, ResolveMiniRoomBaseSpeakerChatTone(seatIndex), persistState: false);
            string observedText = ExtractObservedChatMessage(text);
            if (!string.IsNullOrWhiteSpace(observedText))
            {
                NotifySocialChatObserved(observedText);
            }

            RoomState = "Chat update";
            StatusMessage = $"CMiniRoomBaseDlg::OnChat appended shared base subtype {packetSubType} room chat from seat {Math.Max(0, seatIndex)}.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        private bool TryApplyMiniRoomBaseInviteResultPacket(PacketReader reader, out string message)
        {
            int resultCode = reader.ReadByte();
            string targetName = resultCode is 2 or 3 or 4
                ? NormalizeName(reader.ReadMapleString())
                : null;

            RoomState = "Invite result";
            StatusMessage = resultCode switch
            {
                1 => "Mini-room invite result code 1 followed the client status-bar branch for StringPool id 0x17B.",
                2 => $"{targetName} followed the client invite-result branch for StringPool id 0x1A2.",
                3 => $"{targetName} followed the client invite-result branch for StringPool id 0x1A3.",
                4 => $"{targetName} followed the client invite-result branch for StringPool id 0x1A4.",
                _ => $"Mini-room invite result code {resultCode} reached the shared invite-result seam without a named client notice branch."
            };
            PersistState();
            message = StatusMessage;
            return true;
        }

        private bool TryApplyMiniRoomBaseEnterResultPacket(PacketReader reader, out string message)
        {
            switch (Kind)
            {
                case SocialRoomKind.PersonalShop:
                    return TryApplyPersonalShopEnterResultPacket(reader, out message);
                case SocialRoomKind.EntrustedShop:
                    return TryApplyEntrustedShopEnterResultPacket(reader, out message);
                default:
                    if (!TryDecodeMiniRoomBaseEnterResultPayload(reader, out MiniRoomBaseEnterResultPayload payload, out message))
                    {
                        return false;
                    }

                    ApplyGenericMiniRoomBaseEnterResultStatus(payload);
                    message = StatusMessage;
                    return true;
            }
        }

        private bool TryApplyPersonalShopEnterResultPacket(PacketReader reader, out string message)
        {
            string title = reader.ReadMapleString();
            int itemMaxCount = reader.ReadByte();
            if (!TryDecodeMiniRoomBaseEnterResultPayload(reader, out MiniRoomBaseEnterResultPayload payload, out message))
            {
                return false;
            }

            RoomTitle = string.IsNullOrWhiteSpace(title) ? RoomTitle : title.Trim();
            _miniRoomLocalSeatIndex = Math.Max(0, payload.MyPosition);
            EnsureMerchantPacketNotes();
            _notes[0] = $"CPersonalShopDlg::OnEnterResult refreshed the room title to '{RoomTitle}' with client item cap {Math.Max(0, itemMaxCount)}.";
            if (payload.RoomType <= 0)
            {
                RoomState = "Enter result";
                StatusMessage = $"CPersonalShopDlg::OnEnterResult refreshed title '{RoomTitle}' and item cap {Math.Max(0, itemMaxCount)}, then followed the shared client result branch for room type {payload.RoomType}. {ResolveMiniRoomEnterResultStatusMessage(payload.ResultCode)}";
            }
            else
            {
                RoomState = "Visitor update";
                ModeName = payload.MyPosition == 0 ? "Open shop" : "Browsing";
                StatusMessage = $"CPersonalShopDlg::OnEnterResult refreshed title '{RoomTitle}', item cap {Math.Max(0, itemMaxCount)}, and synchronized {payload.OccupantCount} occupant entr{(payload.OccupantCount == 1 ? "y" : "ies")} for {ResolveMiniRoomTypeLabel(payload.RoomType)}. Local seat {Math.Max(0, payload.MyPosition)} with client max-users {Math.Max(0, payload.MaxUsers)}.";
            }

            PersistState();
            message = StatusMessage;
            return true;
        }

        private bool TryApplyEntrustedShopEnterResultPacket(PacketReader reader, out string message)
        {
            int chatCount = reader.ReadShort();
            List<SocialRoomChatEntry> decodedChatEntries = new();
            for (int i = 0; i < Math.Max(0, chatCount); i++)
            {
                decodedChatEntries.Add(DecodeEntrustedShopEnterChatEntry(reader));
            }

            string employerName = NormalizeName(reader.ReadMapleString());
            int branchStart = reader.Position;
            MiniRoomBaseEnterResultPayload payload = default;
            string title = null;
            int itemMaxCount = 0;
            string ownerPayloadError = null;
            string ownerDecodeError = null;
            bool decodedOwnerLedger = TryDecodeEntrustedShopOwnerEnterLedger(
                    reader,
                    out int permitData,
                    out bool soldDialogVisible,
                    out List<SocialRoomSoldItemEntry> soldItems,
                    out long totalReceived,
                    out ownerDecodeError)
                && TryDecodeEntrustedShopTitleAndBasePayload(
                    reader,
                    out title,
                    out itemMaxCount,
                    out payload,
                    out ownerPayloadError);

            if (!decodedOwnerLedger)
            {
                reader.Reset(branchStart);
                if (!TryDecodeEntrustedShopTitleAndBasePayload(reader, out title, out itemMaxCount, out payload, out message))
                {
                    message = ownerDecodeError ?? ownerPayloadError ?? message;
                    return false;
                }

                permitData = 0;
                soldDialogVisible = false;
                soldItems = null;
                totalReceived = 0;
            }

            if (!string.IsNullOrWhiteSpace(employerName))
            {
                OwnerName = employerName;
            }

            _chatEntries.Clear();
            foreach (SocialRoomChatEntry entry in decodedChatEntries)
            {
                AppendSocialRoomChatEntry(entry.Text, entry.Tone, persistState: false);
            }

            if (decodedOwnerLedger)
            {
                _soldItems.Clear();
                _soldItems.AddRange(soldItems);
            }

            RoomTitle = string.IsNullOrWhiteSpace(title) ? RoomTitle : title.Trim();
            _miniRoomLocalSeatIndex = Math.Max(0, payload.MyPosition);
            ResetEntrustedChildDialogStateForEnterResult(payload.MyPosition, decodedOwnerLedger, soldDialogVisible);

            EnsureMerchantPacketNotes();
            _notes[0] = decodedChatEntries.Count > 0
                ? $"Entrusted-shop enter-result restored {decodedChatEntries.Count} packet-owned chat entr{(decodedChatEntries.Count == 1 ? "y" : "ies")}."
                : "Entrusted-shop enter-result restored an empty packet-owned chat history.";
            _notes[1] = string.IsNullOrWhiteSpace(employerName)
                ? $"Entrusted-shop enter-result refreshed title '{RoomTitle}'."
                : $"Entrusted-shop employer '{employerName}' refreshed title '{RoomTitle}'.";
            if (decodedOwnerLedger)
            {
                _notes[2] = _soldItems.Count > 0
                    ? $"Entrusted-shop owner ledger restored {_soldItems.Count} sold entr{(_soldItems.Count == 1 ? "y" : "ies")} with total received {Math.Max(0L, totalReceived):N0} meso."
                    : $"Entrusted-shop owner ledger restored an empty sold list with total received {Math.Max(0L, totalReceived):N0} meso.";
                _notes[3] = $"Entrusted-shop owner enter-result kept permit field {permitData} and {(soldDialogVisible ? "opened" : "suppressed")} the sold-item dialog.";
            }
            else
            {
                _notes[2] = "Entrusted-shop visitor enter-result did not include the owner-only sold ledger preamble.";
                _notes[3] = $"Entrusted-shop enter-result refreshed the client item cap to {Math.Max(0, itemMaxCount)}.";
            }

            if (payload.RoomType <= 0)
            {
                RoomState = "Enter result";
                StatusMessage = decodedOwnerLedger
                    ? $"CEntrustedShopDlg::OnEnterResult restored employer '{OwnerName}', title '{RoomTitle}', {_soldItems.Count} sold entr{(_soldItems.Count == 1 ? "y" : "ies")}, and {decodedChatEntries.Count} chat entr{(decodedChatEntries.Count == 1 ? "y" : "ies")} before following the shared client result branch for room type {payload.RoomType}. {ResolveMiniRoomEnterResultStatusMessage(payload.ResultCode)}"
                    : $"CEntrustedShopDlg::OnEnterResult restored employer '{OwnerName}', title '{RoomTitle}', and {decodedChatEntries.Count} chat entr{(decodedChatEntries.Count == 1 ? "y" : "ies")} before following the shared client result branch for room type {payload.RoomType}. {ResolveMiniRoomEnterResultStatusMessage(payload.ResultCode)}";
            }
            else
            {
                if (decodedOwnerLedger)
                {
                    RoomState = soldDialogVisible ? "Ledger review" : "Permit active";
                    ModeName = soldDialogVisible ? "Ledger review" : "Open shop";
                    StatusMessage = soldDialogVisible
                        ? $"CEntrustedShopDlg::OnEnterResult restored employer '{OwnerName}', title '{RoomTitle}', {_soldItems.Count} sold entr{(_soldItems.Count == 1 ? "y" : "ies")}, total received {Math.Max(0L, totalReceived):N0} meso, opened the sold-item ledger, and synchronized {payload.OccupantCount} occupant entr{(payload.OccupantCount == 1 ? "y" : "ies")} for {ResolveMiniRoomTypeLabel(payload.RoomType)}. Local seat {Math.Max(0, payload.MyPosition)} with client max-users {Math.Max(0, payload.MaxUsers)}."
                        : $"CEntrustedShopDlg::OnEnterResult restored employer '{OwnerName}', title '{RoomTitle}', {_soldItems.Count} sold entr{(_soldItems.Count == 1 ? "y" : "ies")}, total received {Math.Max(0L, totalReceived):N0} meso, reopened the live entrusted shop, and synchronized {payload.OccupantCount} occupant entr{(payload.OccupantCount == 1 ? "y" : "ies")} for {ResolveMiniRoomTypeLabel(payload.RoomType)}. Local seat {Math.Max(0, payload.MyPosition)} with client max-users {Math.Max(0, payload.MaxUsers)}.";
                }
                else
                {
                    RoomState = "Ledger review";
                    ModeName = "Open shop";
                    StatusMessage = $"CEntrustedShopDlg::OnEnterResult restored employer '{OwnerName}', title '{RoomTitle}', and synchronized {payload.OccupantCount} occupant entr{(payload.OccupantCount == 1 ? "y" : "ies")} for {ResolveMiniRoomTypeLabel(payload.RoomType)}. Local seat {Math.Max(0, payload.MyPosition)} with client max-users {Math.Max(0, payload.MaxUsers)}.";
                }
            }

            PersistState();
            message = StatusMessage;
            return true;
        }

        private void ResetEntrustedChildDialogStateForEnterResult(int localSeatIndex, bool decodedOwnerLedger, bool soldDialogVisible)
        {
            _entrustedChildDialogKind = null;
            _entrustedVisitListSelectedIndex = -1;
            _entrustedBlacklistSelectedIndex = -1;
            _entrustedBlacklistPromptRequest = null;
            _entrustedBlacklistNotice = null;

            if (localSeatIndex == 0)
            {
                _entrustedChildDialogStatus = decodedOwnerLedger && soldDialogVisible
                    ? "CEntrustedShopDlg::OnEnterResult refreshed the owner shell and left visit-list or blacklist child owners closed while the sold-item ledger branch is active."
                    : "CEntrustedShopDlg::OnEnterResult refreshed the owner shell and re-armed visit-list or blacklist access on the parent entrusted-shop dialog.";
                return;
            }

            _entrustedChildDialogStatus = "CEntrustedShopDlg::OnEnterResult refreshed the visitor shell and cleared owner-only visit-list or blacklist child owners.";
        }

        private bool TryDecodeEntrustedShopTitleAndBasePayload(
            PacketReader reader,
            out string title,
            out int itemMaxCount,
            out MiniRoomBaseEnterResultPayload payload,
            out string message)
        {
            title = reader.ReadMapleString();
            itemMaxCount = reader.ReadByte();
            return TryDecodeMiniRoomBaseEnterResultPayload(reader, out payload, out message);
        }

        private SocialRoomChatEntry DecodeEntrustedShopEnterChatEntry(PacketReader reader)
        {
            string rawText = reader.ReadMapleString();
            byte toneByte = reader.ReadByte();
            return new SocialRoomChatEntry(
                NormalizeEntrustedShopEnterChatText(rawText),
                ResolveEntrustedShopEnterChatTone(toneByte));
        }

        private static string NormalizeEntrustedShopEnterChatText(string rawText)
        {
            const string ChatDelimiter = " : ";

            string normalized = string.IsNullOrWhiteSpace(rawText) ? string.Empty : rawText.Trim();
            int delimiterIndex = normalized.IndexOf(ChatDelimiter, StringComparison.Ordinal);
            if (delimiterIndex < 0)
            {
                return normalized;
            }

            string speaker = normalized[..delimiterIndex].Trim();
            string message = normalized[(delimiterIndex + ChatDelimiter.Length)..].Trim();
            if (speaker.Length == 0 || message.Length == 0)
            {
                return normalized;
            }

            return $"{speaker}{ChatDelimiter}{message}";
        }

        private static SocialRoomChatTone ResolveEntrustedShopEnterChatTone(byte toneByte)
        {
            return toneByte switch
            {
                1 => SocialRoomChatTone.LocalSpeaker,
                2 => SocialRoomChatTone.Warning,
                3 => SocialRoomChatTone.System,
                _ => SocialRoomChatTone.Neutral
            };
        }

        private bool TryDecodeMiniRoomBaseEnterResultPayload(PacketReader reader, out MiniRoomBaseEnterResultPayload payload, out string message)
        {
            int roomType = reader.ReadByte();
            if (roomType <= 0)
            {
                payload = new MiniRoomBaseEnterResultPayload(roomType, reader.ReadByte(), 0, 0, 0);
                message = null;
                return true;
            }

            int maxUsers = reader.ReadByte();
            int myPosition = reader.ReadByte();
            _occupants.Clear();
            _savedVisitors.Clear();

            int occupantCount = 0;
            while (true)
            {
                int seatIndex = (sbyte)reader.ReadByte();
                if (seatIndex < 0)
                {
                    break;
                }

                if (RequiresMerchantSeatBaseStub(seatIndex))
                {
                    int merchantId = reader.ReadInt();
                    string merchantName = NormalizeName(reader.ReadMapleString());
                    ApplyMiniRoomBaseEnterResultMerchantSeat(seatIndex, merchantId, merchantName);
                    occupantCount++;
                    continue;
                }

                if (!TryReadPacketOwnedAvatarLook(reader, out LoginAvatarLook avatarLook, out string error))
                {
                    payload = default;
                    message = error;
                    return false;
                }

                string occupantName = NormalizeName(reader.ReadMapleString());
                int jobCode = reader.ReadShort();
                ApplyMiniRoomBaseEnterResultSeat(seatIndex, occupantName, jobCode, avatarLook);
                occupantCount++;
            }

            payload = new MiniRoomBaseEnterResultPayload(roomType, 0, maxUsers, myPosition, occupantCount);
            message = null;
            return true;
        }

        private void ApplyGenericMiniRoomBaseEnterResultStatus(MiniRoomBaseEnterResultPayload payload)
        {
            _miniRoomLocalSeatIndex = Math.Max(0, payload.MyPosition);
            if (payload.RoomType <= 0)
            {
                RoomState = "Enter result";
                StatusMessage = ResolveMiniRoomEnterResultStatusMessage(payload.ResultCode);
            }
            else
            {
                RoomState = Kind == SocialRoomKind.MiniRoom ? "Enter result" : "Visitor update";
                StatusMessage = $"Mini-room enter-result synchronized {payload.OccupantCount} occupant entr{(payload.OccupantCount == 1 ? "y" : "ies")} for {ResolveMiniRoomTypeLabel(payload.RoomType)}. Local seat {Math.Max(0, payload.MyPosition)} with client max-users {Math.Max(0, payload.MaxUsers)}.";
            }

            PersistState();
        }

        private string ResolveMiniRoomEnterResultStatusMessage(int resultCode)
        {
            return resultCode switch
            {
                1 => "Mini-room enter result code 1 followed the client notice branch for StringPool id 0x199.",
                2 => "Mini-room enter result code 2 followed the client notice branch for StringPool id 0x19A.",
                3 => "Mini-room enter result code 3 followed the client notice branch for StringPool id 0x19B.",
                4 => "Mini-room enter result code 4 followed the client notice branch for StringPool id 0x19C.",
                5 => "Mini-room enter result code 5 followed the client notice branch for StringPool id 0x19D.",
                6 => "Mini-room enter result code 6 followed the client notice branch for StringPool id 0x19E.",
                7 => "Mini-room enter result code 7 followed the client notice branch for StringPool id 0x19F.",
                9 => "Mini-room enter result code 9 followed the client notice branch for StringPool id 0x1A1.",
                10 => "Mini-room enter result code 10 followed the client notice branch for StringPool id 0xDB5.",
                11 => "Mini-room enter result code 11 followed the client notice branch for StringPool id 0x1C1.",
                12 => "Mini-room enter result code 12 followed the client notice branch for StringPool id 0x14A2.",
                13 => "Mini-room enter result code 13 followed the client notice branch for StringPool id 0x1A0.",
                14 => "Mini-room enter result code 14 followed the client notice branch for StringPool id 0x1C1.",
                15 => "Mini-room enter result code 15 followed the client notice branch for StringPool id 0x1C2.",
                16 => "Mini-room enter result code 16 followed the client notice branch for StringPool id 0x1C3.",
                17 => "Mini-room enter result code 17 followed the client notice branch for StringPool id 0x1BB.",
                18 => "Mini-room enter result code 18 followed the client notice branch for StringPool id 0xDAE.",
                19 => "Mini-room enter result code 19 followed the client notice branch for StringPool id 0x1E7.",
                20 => "Mini-room enter result code 20 followed the client notice branch for StringPool id 0x19F.",
                21 => "Mini-room enter result code 21 followed the client notice branch for StringPool id 0x1C0.",
                22 => "Mini-room enter result code 22 followed the client notice branch for StringPool id 0x1A68.",
                24 => "Mini-room enter result code 24 followed the client notice branch for StringPool id 0x14DB.",
                25 => "Mini-room enter result code 25 followed the client notice branch for StringPool id 0x116.",
                _ => $"Mini-room enter result code {resultCode} reached the shared enter-result seam without a named client notice branch."
            };
        }

        private static int? ResolveMiniRoomGameMessageStringPoolId(int messageCode)
        {
            return messageCode switch
            {
                0 => 0x1C8,
                1 => 0x1CD,
                2 => 0x1CA,
                3 => 0x1CB,
                4 => 0x1C5,
                5 => 0x1C6,
                6 => 0x1C7,
                7 => 0x1C4,
                8 => 0x1CF,
                9 => 0x1CE,
                101 => 0x1D2,
                102 => 0x1D0,
                103 => 0x1D1,
                _ => null
            };
        }

        private static string ResolveMiniRoomBaseGameMessageText(int messageCode, string characterName, int? stringPoolId)
        {
            string trimmedName = string.IsNullOrWhiteSpace(characterName) ? "Unknown" : characterName.Trim();
            return messageCode switch
            {
                101 or 102 or 103 => stringPoolId.HasValue
                    ? $"CMiniRoomBaseDlg::MakeGameMessage code {messageCode} followed StringPool id 0x{stringPoolId.Value:X}."
                    : $"CMiniRoomBaseDlg::MakeGameMessage code {messageCode} reached an unmapped shared chat branch.",
                _ => stringPoolId.HasValue
                    ? $"{trimmedName} triggered CMiniRoomBaseDlg::MakeGameMessage code {messageCode} (StringPool id 0x{stringPoolId.Value:X})."
                    : $"{trimmedName} triggered CMiniRoomBaseDlg::MakeGameMessage code {messageCode} without a recovered StringPool id."
            };
        }

        private SocialRoomChatTone ResolveMiniRoomBaseSpeakerChatTone(int seatIndex)
        {
            if (seatIndex <= 0)
            {
                return SocialRoomChatTone.LocalSpeaker;
            }

            return Kind == SocialRoomKind.MiniRoom
                ? SocialRoomChatTone.RemoteSpeaker
                : SocialRoomChatTone.Neutral;
        }

        private static string ExtractObservedChatMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            int separatorIndex = text.IndexOf(" : ", StringComparison.Ordinal);
            return separatorIndex >= 0 && separatorIndex + 3 < text.Length
                ? text[(separatorIndex + 3)..].Trim()
                : text.Trim();
        }

        private bool TryDecodeEntrustedShopOwnerEnterLedger(
            PacketReader reader,
            out int permitData,
            out bool soldDialogVisible,
            out List<SocialRoomSoldItemEntry> soldItems,
            out long totalReceived,
            out string message)
        {
            permitData = reader.ReadInt();
            soldDialogVisible = reader.ReadByte() != 0;
            if (!TryDecodeEntrustedSoldItemList(reader, out soldItems, out totalReceived, out message))
            {
                return false;
            }

            message = null;
            return true;
        }

        private bool TryDecodeEntrustedSoldItemList(PacketReader reader, out List<SocialRoomSoldItemEntry> soldItems, out long totalReceived, out string message)
        {
            soldItems = new List<SocialRoomSoldItemEntry>();
            int count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                int itemId = reader.ReadInt();
                int quantitySold = Math.Max((short) 1, reader.ReadShort());
                int grossMeso = Math.Max(0, reader.ReadInt());
                string buyerName = NormalizeName(reader.ReadMapleString());
                int taxMeso = GetPersonalShopTax(grossMeso);
                int netMeso = Math.Max(0, grossMeso - taxMeso);
                soldItems.Add(new SocialRoomSoldItemEntry(
                    itemId,
                    ResolveItemName(itemId),
                    buyerName,
                    quantitySold,
                    1,
                    grossMeso,
                    grossMeso,
                    taxMeso,
                    netMeso,
                    i));
            }

            totalReceived = Math.Max(0L, reader.ReadLong());
            message = null;
            return true;
        }

        private bool TryApplyMiniRoomBaseCheckSsnPacket(PacketReader reader, out string message)
        {
            int checkType = reader.ReadByte();
            int roomType = reader.ReadByte();
            bool hasStoredPassword = reader.ReadByte() != 0;
            RoomState = "SSN check";
            StatusMessage = $"Mini-room base SSN-check packet reached the shared dialog seam for {ResolveMiniRoomTypeLabel(roomType)} (checkType={checkType}, hasStoredPassword={hasStoredPassword.ToString().ToLowerInvariant()}).";
            PersistState();
            message = StatusMessage;
            return true;
        }

        private bool ApplyMiniRoomBaseStaticMessage(string statusMessage, out string message)
        {
            RoomState = Kind == SocialRoomKind.MiniRoom ? "MiniRoomBase" : "Shop base";
            StatusMessage = statusMessage;
            PersistState();
            message = StatusMessage;
            return true;
        }

        private bool TryDispatchMiniRoomBaseTypeSpecificPacket(PacketReader reader, int tickCount, out string message)
        {
            message = null;
            if (reader == null)
            {
                message = "Mini-room base update packet did not include a readable payload.";
                return false;
            }

            byte[] nestedPayload = reader.ReadBytes(reader.Remaining);
            if (nestedPayload.Length == 0)
            {
                message = "Mini-room base update packet did not include a nested room payload.";
                return false;
            }

            PacketReader nestedReader = new(nestedPayload);
            byte nestedPacketType;
            try
            {
                nestedPacketType = nestedReader.ReadByte();
            }
            catch (EndOfStreamException)
            {
                message = "Mini-room base update packet did not include a nested room packet type.";
                return false;
            }
            bool handled;
            string ownerName;
            if (nestedPacketType == PersonalShopBasePacketType)
            {
                ownerName = "CMiniRoomBaseDlg::OnPacketBase";
                handled = TryDispatchMiniRoomBasePacket(nestedReader, tickCount, out message);
            }
            else if (IsMiniRoomBasePacketSubType(nestedPacketType))
            {
                ownerName = "CMiniRoomBaseDlg::OnPacketBase";
                PacketReader baseReader = new(nestedPayload);
                handled = TryDispatchMiniRoomBasePacket(baseReader, tickCount, out message);
                nestedReader = baseReader;
            }
            else
            {
                if (Kind is SocialRoomKind.PersonalShop or SocialRoomKind.EntrustedShop or SocialRoomKind.TradingRoom)
                {
                    ownerName = _shopDialogPacketOwner?.OwnerName ?? "room-specific owner";
                    handled = _shopDialogPacketOwner?.TryDispatch(nestedPayload, nestedReader, nestedPacketType, tickCount, out message) == true;
                }
                else
                {
                    handled = Kind == SocialRoomKind.MiniRoom &&
                        TryDispatchMiniRoomPacket(nestedReader, nestedPacketType, tickCount, out message);
                    ownerName = Kind == SocialRoomKind.MiniRoom
                        ? "CMiniRoomBaseDlg-derived OnPacket"
                        : "room-specific owner";
                }
            }

            string payloadPreview = BuildPacketHexPreview(nestedPayload);
            string detail = handled
                ? $"CMiniRoomBaseDlg::OnPacketBase subtype 6 forwarded nested packet {nestedPacketType} into {ownerName}. {message} | payload={payloadPreview} | remaining={nestedReader.Remaining}"
                : $"CMiniRoomBaseDlg::OnPacketBase subtype 6 forwarded nested packet {nestedPacketType} into {ownerName}, but it was not modeled. {message} | payload={payloadPreview} | remaining={nestedReader.Remaining}";
            _lastPacketOwnerSummary = detail;
            message = detail;
            return handled;
        }

        private static bool IsMiniRoomBasePacketSubType(byte packetType)
        {
            return packetType is
                MiniRoomBaseInvitePacketSubType or
                MiniRoomBaseInviteResultPacketSubType or
                MiniRoomBaseEnterPacketSubType or
                MiniRoomBaseEnterResultPacketSubType or
                MiniRoomBaseUpdatePacketSubType or
                MiniRoomBaseChatPacketSubType or
                MiniRoomBaseChatAltPacketSubType or
                MiniRoomBaseAvatarPacketSubType or
                MiniRoomBaseLeavePacketSubType or
                MiniRoomBaseCheckSsnPacketSubType;
        }

        private bool TryApplyMiniRoomBaseEnterPacket(PacketReader reader, out string message)
        {
            int seatIndex = reader.ReadByte();
            if (RequiresMerchantSeatBaseStub(seatIndex))
            {
                int merchantId = reader.ReadInt();
                string merchantName = NormalizeName(reader.ReadMapleString());
                ApplyMiniRoomBaseMerchantSeatEnterPacket(seatIndex, merchantId, merchantName);
                message = StatusMessage;
                return true;
            }

            if (!TryReadPacketOwnedAvatarLook(reader, out LoginAvatarLook avatarLook, out string error))
            {
                message = error;
                return false;
            }

            string occupantName = NormalizeName(reader.ReadMapleString());
            int jobCode = reader.ReadShort();
            ApplyMiniRoomBaseSeatEnterPacket(seatIndex, occupantName, jobCode, avatarLook);
            message = StatusMessage;
            return true;
        }

        private bool TryApplyMiniRoomBaseAvatarPacket(PacketReader reader, out string message)
        {
            int seatIndex = reader.ReadByte();
            if (!TryReadPacketOwnedAvatarLook(reader, out LoginAvatarLook avatarLook, out string error))
            {
                message = error;
                return false;
            }

            ApplyMiniRoomBaseAvatarPacket(seatIndex, avatarLook);
            message = StatusMessage;
            return true;
        }

        private bool TryApplyMiniRoomBaseLeavePacket(PacketReader reader, out string message)
        {
            int seatIndex = reader.ReadByte();
            ApplyMiniRoomBaseSeatLeavePacket(seatIndex);
            message = StatusMessage;
            return true;
        }

        private static bool TryReadPacketOwnedAvatarLook(PacketReader reader, out LoginAvatarLook avatarLook, out string message)
        {
            if (!LoginAvatarLookCodec.TryDecode(reader, out avatarLook, out string error))
            {
                message = $"Mini-room AvatarLook payload could not be decoded: {error}";
                return false;
            }

            message = null;
            return true;
        }

        private void ApplyMiniRoomBaseSeatEnterPacket(int seatIndex, string occupantName, int jobCode, LoginAvatarLook avatarLook)
        {
            int normalizedSeatIndex = Math.Max(0, seatIndex);
            SocialRoomOccupantRole role = ResolveBasePacketOccupantRole(normalizedSeatIndex);
            string seatLabel = ResolveBasePacketSeatLabel(normalizedSeatIndex);
            string detail = BuildBasePacketOccupantDetail(normalizedSeatIndex, seatLabel, jobCode, avatarLook);
            if (normalizedSeatIndex == 0)
            {
                OwnerName = occupantName;
            }

            CharacterBuild avatarBuild = ResolvePacketOwnedAvatarBuild(avatarLook, normalizedSeatIndex);
            EnsureOccupantSlot(normalizedSeatIndex, occupantName, role, detail, isReady: false, avatarBuild: avatarBuild);
            RoomState = Kind == SocialRoomKind.MiniRoom ? "Seat update" : "Visitor update";
            StatusMessage = $"{occupantName} entered {seatLabel.ToLowerInvariant()} through CMiniRoomBaseDlg::OnEnterBase.";
            if (Kind == SocialRoomKind.MiniRoom)
            {
                AddMiniRoomSystemMessage($"System : {StatusMessage}");
                SyncMiniRoomOmokPresentation();
            }
            else if (normalizedSeatIndex > 0 && !_savedVisitors.Contains(occupantName, StringComparer.OrdinalIgnoreCase))
            {
                _savedVisitors.Add(occupantName);
            }

            PersistState();
        }

        private void ApplyMiniRoomBaseAvatarPacket(int seatIndex, LoginAvatarLook avatarLook)
        {
            int normalizedSeatIndex = Math.Max(0, seatIndex);
            string occupantName = ResolveMiniRoomSeatName(normalizedSeatIndex);
            if (normalizedSeatIndex < _occupants.Count)
            {
                SocialRoomOccupant occupant = _occupants[normalizedSeatIndex];
                CharacterBuild avatarBuild = ResolvePacketOwnedAvatarBuild(avatarLook, normalizedSeatIndex);
                occupant.Update(
                    occupant.Name,
                    occupant.Role,
                    $"{occupant.Detail} | AvatarLook refreshed through CMiniRoomBaseDlg::OnAvatar.",
                    occupant.IsReady,
                    avatarBuild);
            }

            RoomState = Kind == SocialRoomKind.MiniRoom ? "Avatar refresh" : "Visitor avatar refresh";
            StatusMessage = $"{occupantName} refreshed seat {normalizedSeatIndex} through CMiniRoomBaseDlg::OnAvatar.";
            PersistState();
        }

        private void ApplyMiniRoomBaseEnterResultMerchantSeat(int seatIndex, int merchantId, string merchantName)
        {
            int normalizedSeatIndex = Math.Max(0, seatIndex);
            string seatLabel = ResolveBasePacketSeatLabel(normalizedSeatIndex);
            string detail = $"{seatLabel} | Merchant id {merchantId}";
            if (normalizedSeatIndex == 0)
            {
                OwnerName = merchantName;
                _employeePacketEmployerId = Math.Max(0, merchantId);
                _employeePoolRuntime.SetPreferredEmployerId(_employeePacketEmployerId);
            }

            EnsureOccupantSlot(normalizedSeatIndex, merchantName, ResolveBasePacketOccupantRole(normalizedSeatIndex), detail, isReady: false, avatarBuild: null);
        }

        private void ApplyMiniRoomBaseMerchantSeatEnterPacket(int seatIndex, int merchantId, string merchantName)
        {
            int normalizedSeatIndex = Math.Max(0, seatIndex);
            ApplyMiniRoomBaseEnterResultMerchantSeat(normalizedSeatIndex, merchantId, merchantName);
            RoomState = "Visitor update";
            StatusMessage = $"{merchantName} entered {ResolveBasePacketSeatLabel(normalizedSeatIndex).ToLowerInvariant()} through CMiniRoomBaseDlg::OnEnterBase with merchant id {merchantId}.";
            PersistState();
        }

        private void ApplyMiniRoomBaseEnterResultSeat(int seatIndex, string occupantName, int jobCode, LoginAvatarLook avatarLook)
        {
            int normalizedSeatIndex = Math.Max(0, seatIndex);
            SocialRoomOccupantRole role = ResolveBasePacketOccupantRole(normalizedSeatIndex);
            string seatLabel = ResolveBasePacketSeatLabel(normalizedSeatIndex);
            string detail = BuildBasePacketOccupantDetail(normalizedSeatIndex, seatLabel, jobCode, avatarLook);
            if (normalizedSeatIndex == 0)
            {
                OwnerName = occupantName;
            }
            else if (!_savedVisitors.Contains(occupantName, StringComparer.OrdinalIgnoreCase))
            {
                _savedVisitors.Add(occupantName);
            }

            CharacterBuild avatarBuild = ResolvePacketOwnedAvatarBuild(avatarLook, normalizedSeatIndex);
            EnsureOccupantSlot(normalizedSeatIndex, occupantName, role, detail, isReady: false, avatarBuild: avatarBuild);
        }

        private bool RequiresMerchantSeatBaseStub(int seatIndex)
        {
            return Kind == SocialRoomKind.EntrustedShop && seatIndex == 0;
        }

        private string ResolveMiniRoomTypeLabel(int roomType)
        {
            return roomType switch
            {
                1 => "mini-room",
                2 => "trade room",
                3 => "personal shop",
                4 => "entrusted shop",
                5 => "store bank",
                6 => "cash trade",
                _ => $"room type {roomType}"
            };
        }

        private void ApplyMiniRoomBaseSeatLeavePacket(int seatIndex)
        {
            int normalizedSeatIndex = Math.Max(0, seatIndex);
            string leavingName = ResolveMiniRoomSeatName(normalizedSeatIndex);
            if (normalizedSeatIndex <= 0)
            {
                if (Kind == SocialRoomKind.EntrustedShop)
                {
                    _employeePacketEmployerId = 0;
                    _employeePoolRuntime.SetPreferredEmployerId(0);
                }

                RoomState = "Closed";
                StatusMessage = $"{leavingName} left the room through CMiniRoomBaseDlg::OnLeaveBase.";
                if (Kind == SocialRoomKind.MiniRoom)
                {
                    AddMiniRoomSystemMessage($"System : {StatusMessage}", isWarning: true);
                }

                PersistState();
                return;
            }

            if (Kind == SocialRoomKind.MiniRoom)
            {
                RemoveMiniRoomOccupant(leavingName, out _);
                StatusMessage = $"{leavingName} left seat {normalizedSeatIndex} through CMiniRoomBaseDlg::OnLeaveBase.";
                AddMiniRoomSystemMessage($"System : {StatusMessage}", isWarning: normalizedSeatIndex == 1);
                SyncMiniRoomOmokPresentation();
            }
            else
            {
                if (normalizedSeatIndex < _occupants.Count)
                {
                    _occupants.RemoveAt(normalizedSeatIndex);
                }

                RoomState = "Visitor update";
                StatusMessage = $"{leavingName} left seat {normalizedSeatIndex} through CMiniRoomBaseDlg::OnLeaveBase.";
            }

            PersistState();
        }

        private SocialRoomOccupantRole ResolveBasePacketOccupantRole(int seatIndex)
        {
            if (seatIndex <= 0)
            {
                return Kind == SocialRoomKind.EntrustedShop
                    ? SocialRoomOccupantRole.Merchant
                    : SocialRoomOccupantRole.Owner;
            }

            return Kind switch
            {
                SocialRoomKind.MiniRoom when seatIndex == 1 => SocialRoomOccupantRole.Guest,
                SocialRoomKind.TradingRoom => SocialRoomOccupantRole.Trader,
                _ => SocialRoomOccupantRole.Visitor
            };
        }

        private string ResolveBasePacketSeatLabel(int seatIndex)
        {
            if (seatIndex <= 0)
            {
                return Kind switch
                {
                    SocialRoomKind.EntrustedShop => "Merchant seat",
                    _ => "Host seat"
                };
            }

            return Kind == SocialRoomKind.MiniRoom && seatIndex == 1
                ? "Guest seat"
                : $"Visitor seat {seatIndex}";
        }

        private string BuildBasePacketOccupantDetail(int seatIndex, string seatLabel, int jobCode, LoginAvatarLook avatarLook)
        {
            if (Kind == SocialRoomKind.MiniRoom && IsMiniRoomOmokActive && seatIndex <= 1)
            {
                return $"{BuildOmokSeatDetail(seatIndex, seatLabel)} | Job {jobCode}";
            }

            string lookSummary = avatarLook == null
                ? "look unavailable"
                : $"{avatarLook.Gender} | Skin {(int)avatarLook.Skin} | Face {avatarLook.FaceId}";
            return $"{seatLabel} | Job {jobCode} | {lookSummary}";
        }

        private CharacterBuild GetExistingAvatarBuild(int seatIndex)
        {
            return seatIndex >= 0 && seatIndex < _occupants.Count
                ? _occupants[seatIndex].AvatarBuild
                : null;
        }

        private CharacterBuild ResolvePacketOwnedAvatarBuild(LoginAvatarLook avatarLook, int seatIndex)
        {
            CharacterBuild existingBuild = GetExistingAvatarBuild(seatIndex);
            if (avatarLook == null || AvatarBuildResolver == null)
            {
                return existingBuild;
            }

            try
            {
                return AvatarBuildResolver(LoginAvatarLookCodec.CloneLook(avatarLook)) ?? existingBuild;
            }
            catch
            {
                return existingBuild;
            }
        }

        private void EnsureOccupantSlot(int index, string name, SocialRoomOccupantRole role, string detail, bool isReady, CharacterBuild avatarBuild)
        {
            while (_occupants.Count <= index)
            {
                int placeholderIndex = _occupants.Count;
                _occupants.Add(new SocialRoomOccupant(
                    placeholderIndex == index ? name : ResolveBasePacketSeatLabel(placeholderIndex),
                    ResolveBasePacketOccupantRole(placeholderIndex),
                    BuildBasePacketOccupantDetail(placeholderIndex, ResolveBasePacketSeatLabel(placeholderIndex), 0, null),
                    false));
            }

            _occupants[index].Update(name, role, detail, isReady, avatarBuild);
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

        private void TrackPacketOwnerSummary(string ownerName, int packetType, int tickCount, bool handled, string detail)
        {
            string result = handled ? "handled" : "rejected";
            string suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail}";
            _lastPacketOwnerSummary = $"{ownerName} {result} type {packetType} at tick {tickCount}{suffix}";
        }

        private string BuildDefaultPacketOwnerSummary()
        {
            return Kind switch
            {
                SocialRoomKind.PersonalShop => "CPersonalShopDlg::OnPacket idle. Waiting for shop dialog result packets before forwarding base type 25 into the shared mini-room owner.",
                SocialRoomKind.EntrustedShop => "CEntrustedShopDlg::OnPacket idle. Waiting for entrusted result packets before forwarding shared shop traffic into CPersonalShopDlg::OnPacket.",
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
            AppendSocialRoomChatEntry(text, tone, persistState: true);
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
            NotifySocialChatObserved(message);
        }

        public void AddMiniRoomSystemMessage(string message, bool isWarning = false)
        {
            AddMiniRoomChatEntry(message, isWarning ? SocialRoomChatTone.Warning : SocialRoomChatTone.System);
            NotifySocialChatObserved(message);
        }

        private void NotifySocialChatObserved(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            SocialChatObserved?.Invoke(message.Trim(), Environment.TickCount);
        }

        private void AppendSocialRoomChatEntry(string text, SocialRoomChatTone tone, bool persistState)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _chatEntries.Add(new SocialRoomChatEntry(text.Trim(), tone));
            const int maxChatEntries = 48;
            if (_chatEntries.Count > maxChatEntries)
            {
                _chatEntries.RemoveRange(0, _chatEntries.Count - maxChatEntries);
            }

            if (persistState)
            {
                PersistState();
            }
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
            ClearOmokDialogRequests();
            ResetOmokTurnClock();
            RoomState = "Omok in progress";
            StatusMessage = $"{ResolveMiniRoomSeatName(_miniRoomOmokCurrentTurnIndex)} opened the Omok round from a packet-backed start.";
            SetOmokDialogStatus("COmokDlg::OnUserStart rebuilt the Omok board from packet state.", 1500);
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
            ClearOmokDialogRequests();
            StartOmokStoneAnimation();
            ResetOmokTurnClock();
            _miniRoomOmokMoveHistory.Add(new OmokMoveHistoryEntry(x, y, stoneValue, seatIndex));
            AddMiniRoomSpeakerMessage(ResolveMiniRoomSeatName(seatIndex), $"placed a {ResolveOmokStoneName(stoneValue)} stone at {x},{y}.", seatIndex == 0);
            RecordOmokSoundByStringPoolId(stoneValue == 1 ? OmokSoundBlackStoneStringPoolId : OmokSoundWhiteStoneStringPoolId);

            if (HasFiveInRow(x, y, stoneValue))
            {
                _miniRoomOmokInProgress = false;
                _miniRoomOmokWinnerIndex = seatIndex;
                RoomState = "Omok result";
                StatusMessage = $"{ResolveMiniRoomSeatName(seatIndex)} completed five in a row and won the Omok round.";
                SetOmokDialogStatus(StatusMessage, 2200);
            }
            else
            {
                _miniRoomOmokCurrentTurnIndex = seatIndex == 0 ? 1 : 0;
                RoomState = "Omok in progress";
                StatusMessage = FormatOmokTurnStatus(_miniRoomOmokCurrentTurnIndex);
                SetOmokDialogStatus(
                    $"{ResolveMiniRoomSeatName(seatIndex)} placed a {ResolveOmokStoneName(stoneValue)} stone. {_miniRoomOmokTimeFloor}s remain on the dialog timer.",
                    900);
            }

            SyncMiniRoomOmokPresentation();
            PersistState();
            message = StatusMessage;
            return true;
        }

        private void ApplyOmokRetreatPacket(int removedStoneCount, int nextTurnSeat)
        {
            int remainingToRemove = Math.Max(0, removedStoneCount);
            int removedByHistory = 0;
            while (remainingToRemove > 0 && TryPopOmokMoveHistory(out OmokMoveHistoryEntry move))
            {
                int boardIndex = GetOmokBoardIndex(move.X, move.Y);
                _miniRoomOmokBoard[boardIndex] = 0;
                remainingToRemove--;
                removedByHistory++;
            }

            while (remainingToRemove > 0 && TryRemoveNewestOmokStoneFromBoardFallback())
            {
                remainingToRemove--;
            }

            _miniRoomOmokCurrentTurnIndex = Math.Clamp(nextTurnSeat, 0, 1);
            _miniRoomOmokWinnerIndex = -1;
            ClearOmokDialogRequests();
            ResetOmokTurnClock();
            StartOmokStoneAnimation();
            UpdateLastOmokMoveFromHistory();
            RoomState = "Omok in progress";
            StatusMessage = removedStoneCount > 0
                ? $"Omok retreat packet removed {removedStoneCount} recent stone(s){(removedByHistory > 0 ? " from preserved stone history" : string.Empty)}."
                : "Omok retreat packet resolved without removing stones.";
            SetOmokDialogStatus($"COmokDlg retreat flow restored {ResolveMiniRoomSeatName(_miniRoomOmokCurrentTurnIndex)}'s turn.", 1500);
            SyncMiniRoomOmokPresentation();
            PersistState();
        }

        private void ApplyOmokGameResultPacket(int resultType, int winnerSeat)
        {
            _miniRoomOmokInProgress = false;
            ClearOmokDialogRequests();
            ClearOmokPendingPrompt();
            if (resultType == 1)
            {
                _miniRoomOmokWinnerIndex = -1;
                RoomState = "Omok draw";
                StatusMessage = ResolveOmokString(OmokTieStringPoolId, "It's a tie.");
                SetOmokDialogStatus("COmokDlg::OnGameResult closed the round as a draw.", 2200);
                RecordOmokSoundByStringPoolId(OmokSoundDrawStringPoolId);
            }
            else
            {
                _miniRoomOmokWinnerIndex = Math.Clamp(winnerSeat, 0, 1);
                RoomState = "Omok result";
                bool localWon = _miniRoomOmokWinnerIndex == _miniRoomLocalSeatIndex;
                StatusMessage = ResolveOmokString(
                    localWon ? OmokWinStringPoolId : OmokLoseStringPoolId,
                    localWon ? "You win." : "You lost.");
                SetOmokDialogStatus(
                    $"{ResolveMiniRoomSeatName(_miniRoomOmokWinnerIndex)} won the Omok round from packet state.",
                    2200);
                RecordOmokSoundByStringPoolId(localWon ? OmokSoundWinStringPoolId : OmokSoundLoseStringPoolId);
            }

            SyncMiniRoomOmokPresentation();
            PersistState();
        }

        private void ApplyPersonalShopBuyResult(int resultCode)
        {
            RoomState = "Buy result received";
            StatusMessage = resultCode switch
            {
                0 => "Personal-shop buy result code 0 completed without opening a client notice.",
                1 => "Personal-shop buy result code 1 followed the client notice branch for StringPool id 0x365.",
                2 => "Personal-shop buy result code 2 followed the client notice branch for StringPool id 0x1A8B.",
                3 => "Personal-shop buy result code 3 followed the client notice branch for StringPool id 0xB9D.",
                4 => "Personal-shop buy result code 4 followed the client notice branch for StringPool id 0xB9C.",
                5 => "Personal-shop buy result code 5 followed the client notice branch for StringPool id 0x366.",
                6 => "Personal-shop buy result code 6 followed the client notice branch for StringPool id 0x367.",
                7 => "Personal-shop buy result code 7 followed the client notice branch for StringPool id 0x1A2B.",
                8 => "Personal-shop buy result code 8 followed the client notice branch for StringPool id 0x1A6D.",
                9 => "Personal-shop buy result code 9 followed the client notice branch for StringPool id 0x14B1.",
                10 => "Personal-shop buy result code 10 followed the client notice branch for StringPool id 0x14B0.",
                11 => "Personal-shop buy result code 11 followed the client notice branch for StringPool id 0x19E.",
                12 => "Personal-shop buy result code 12 followed the client notice branch for StringPool id 0x1A67.",
                14 => "Personal-shop buy result code 14 followed the client notice branch for StringPool id 0xFB2.",
                _ => $"Personal-shop buy result code {resultCode} followed the client fallback notice branch for StringPool id 0x369."
            };
            PersistState();
        }

        private bool TryApplyPersonalShopSoldItemPacket(PacketReader reader, out string message)
        {
            int soldIndex = reader.ReadByte();
            int purchasedBundles = reader.ReadShort();
            string buyerName = NormalizeName(reader.ReadMapleString());
            SocialRoomItemEntry entry = FindActiveMerchantPacketEntry(soldIndex);
            if (entry == null)
            {
                message = $"No active personal-shop bundle exists at packet slot {soldIndex}.";
                return false;
            }

            int normalizedBundles = Math.Max(1, purchasedBundles);
            int quantitySold = normalizedBundles * Math.Max(1, entry.Quantity);
            int grossMesosReceived = Math.Max(0, normalizedBundles * entry.MesoAmount);
            int taxMesos = GetPersonalShopTax(grossMesosReceived);
            int netMesosReceived = Math.Max(0, grossMesosReceived - taxMesos);
            entry.Update($"{entry.Detail} | Packet sold {normalizedBundles} bundle(s) to {buyerName}", entry.Quantity, entry.MesoAmount, entry.IsLocked, entry.IsClaimed);
            _personalShopTotalSoldGross += grossMesosReceived;
            _personalShopTotalReceivedNet += netMesosReceived;
            MesoAmount += netMesosReceived;
            _soldItems.Add(new SocialRoomSoldItemEntry(
                entry.ItemId,
                entry.ItemName,
                buyerName,
                quantitySold,
                normalizedBundles,
                entry.MesoAmount,
                grossMesosReceived,
                taxMesos,
                netMesosReceived,
                soldIndex));
            SocialRoomOccupant buyer = _occupants.FirstOrDefault(occupant => string.Equals(occupant.Name, buyerName, StringComparison.OrdinalIgnoreCase));
            if (buyer == null)
            {
                _occupants.Add(new SocialRoomOccupant(buyerName, SocialRoomOccupantRole.Buyer, $"Bought {entry.ItemName} from packet state"));
            }
            else
            {
                buyer.Update(buyerName, SocialRoomOccupantRole.Buyer, $"Bought {entry.ItemName} from packet state", isReady: false, buyer.AvatarBuild);
            }

            if (!_savedVisitors.Contains(buyerName, StringComparer.OrdinalIgnoreCase))
            {
                _savedVisitors.Add(buyerName);
            }

            EnsureMerchantPacketNotes();
            _notes[2] = $"{buyerName} bought {quantitySold} of {entry.ItemName} across {normalizedBundles} bundle(s) for {grossMesosReceived:N0} meso gross and {netMesosReceived:N0} net.";
            _notes[3] = $"Personal-shop packet totals: gross {_personalShopTotalSoldGross:N0}, tax {_personalShopTotalSoldGross - _personalShopTotalReceivedNet:N0}, net {_personalShopTotalReceivedNet:N0}, claimable {MesoAmount:N0}.";
            RoomState = "Sold-item result received";
            StatusMessage = $"{buyerName} bought {quantitySold} of {entry.ItemName} across {normalizedBundles} bundle(s) from packet slot {soldIndex}. Gross {grossMesosReceived:N0}, tax {taxMesos:N0}, net {netMesosReceived:N0}.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        private bool TryApplyPersonalShopMoveItemPacket(PacketReader reader, out string message)
        {
            int remainingItemCount = reader.ReadByte();
            int removedIndex = reader.ReadShort();
            SocialRoomItemEntry entry = FindActiveMerchantPacketEntry(removedIndex);
            if (entry == null)
            {
                message = $"No active personal-shop bundle exists at packet slot {removedIndex}.";
                return false;
            }

            _items.Remove(entry);
            _inventoryEscrow.RemoveAll(escrow => ReferenceEquals(escrow.Entry, entry));
            int trimmedRows = TrimActiveMerchantPacketRows(Math.Max(0, remainingItemCount));
            NormalizeActiveMerchantPacketSlots();
            RoomState = "Closed for setup";
            ModeName = "Repricing";
            StatusMessage = trimmedRows > 0
                ? $"Moved {entry.ItemName} back to inventory from packet slot {removedIndex}. {Math.Max(0, remainingItemCount)} bundle(s) remain in the client packet array after trimming {trimmedRows} stale row(s)."
                : $"Moved {entry.ItemName} back to inventory from packet slot {removedIndex}. {Math.Max(0, remainingItemCount)} bundle(s) remain in the client packet array.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        private void ApplyEntrustedArrangeResult(int mesoAmount)
        {
            int removedRows = RemoveClaimedMerchantRows();
            MesoAmount = Math.Max(0, mesoAmount);
            RoomState = "Ledger review";
            ModeName = "Ledger review";
            StatusMessage = removedRows > 0
                ? $"Entrusted-shop arrange packet compacted {removedRows} sold or empty row(s) and refreshed the ledger to {MesoAmount:N0} meso."
                : $"Entrusted-shop arrange packet refreshed the ledger to {MesoAmount:N0} meso.";
            PersistState();
        }

        private void ApplyEntrustedWithdrawAllResult(int resultCode)
        {
            RoomState = "Withdraw-all result";
            StatusMessage = resultCode switch
            {
                0 => "Entrusted-shop withdraw-all result code 0 followed the client notice branch for StringPool id 0xDB8.",
                1 => "Entrusted-shop withdraw-all result code 1 followed the client notice branch for StringPool id 0xDB9.",
                2 => "Entrusted-shop withdraw-all result code 2 followed the client notice branch for StringPool id 0xDBA.",
                3 => "Entrusted-shop withdraw-all result code 3 followed the client notice branch for StringPool id 0xDBB.",
                4 => "Entrusted-shop withdraw-all result code 4 followed the client notice branch for StringPool id 0xDBC.",
                5 => "Entrusted-shop withdraw-all result code 5 completed without opening a client notice.",
                _ => $"Entrusted-shop withdraw-all result code {resultCode} followed the client fallback notice path."
            };
            PersistState();
        }

        private void ApplyEntrustedWithdrawMoneyResult()
        {
            MesoAmount = 0;
            RoomState = "Ledger settled";
            StatusMessage = "Entrusted-shop withdraw-money packet cleared the ledger meso total and refreshed the dialog state.";
            PersistState();
        }

        private bool TryApplyEntrustedVisitListPacket(PacketReader reader, out string message)
        {
            int count = reader.ReadShort();
            _savedVisitors.Clear();
            _entrustedVisitLogEntries.Clear();
            List<string> visitNotes = new();
            for (int i = 0; i < count; i++)
            {
                string name = NormalizeName(reader.ReadMapleString());
                int staySeconds = reader.ReadInt();
                _savedVisitors.Add(name);
                _entrustedVisitLogEntries.Add(new EntrustedShopVisitLogEntrySnapshot
                {
                    Name = name,
                    StaySeconds = Math.Max(0, staySeconds)
                });
                visitNotes.Add($"{name} stayed {staySeconds}s");
            }

            while (_notes.Count < 4)
            {
                _notes.Add(string.Empty);
            }

            _notes[0] = count > 0
                ? $"Entrusted-shop visit log: {string.Join(", ", visitNotes)}."
                : "Entrusted-shop visit log: no entries.";
            _entrustedVisitListSelectedIndex = NormalizeEntrustedDialogSelectionIndex(_entrustedVisitListSelectedIndex, _entrustedVisitLogEntries.Count);
            _entrustedChildDialogKind = EntrustedShopChildDialogKind.VisitList;
            _entrustedChildDialogStatus = count > 0
                ? "CVisitListDlg::OnCreate opened the dedicated visit-list owner. Save Name remains disabled until a visit row is selected."
                : "CVisitListDlg::OnCreate opened the dedicated visit-list owner with an empty visit log.";
            StatusMessage = $"Applied entrusted-shop visit-list packet with {count} visitor entr{(count == 1 ? "y" : "ies")} and opened {ResolveEntrustedChildDialogOwnerName(EntrustedShopChildDialogKind.VisitList)}.";
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
            _entrustedBlacklistSelectedIndex = NormalizeEntrustedDialogSelectionIndex(_entrustedBlacklistSelectedIndex, _blockedVisitors.Count);
            _entrustedChildDialogKind = EntrustedShopChildDialogKind.Blacklist;
            _entrustedChildDialogStatus = _blockedVisitors.Count < 20
                ? "CBlackListDlg::OnCreate opened the dedicated blacklist owner. Add stays enabled while the client-side count remains below 20."
                : "CBlackListDlg::OnCreate opened the dedicated blacklist owner at the client-side 20-name add limit.";
            StatusMessage = $"Applied entrusted-shop blacklist packet with {_blockedVisitors.Count} entr{(_blockedVisitors.Count == 1 ? "y" : "ies")} and opened {ResolveEntrustedChildDialogOwnerName(EntrustedShopChildDialogKind.Blacklist)}.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TryOpenEntrustedChildDialog(EntrustedShopChildDialogKind kind, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                message = "Entrusted-shop child dialogs only apply to the entrusted shop shell.";
                return false;
            }

            if (_miniRoomLocalSeatIndex != 0)
            {
                message = $"{ResolveEntrustedChildDialogOwnerName(kind)} is only available while the local seat owns the entrusted shop.";
                return false;
            }

            _entrustedChildDialogKind = kind;
            if (kind == EntrustedShopChildDialogKind.VisitList)
            {
                _entrustedVisitListSelectedIndex = NormalizeEntrustedDialogSelectionIndex(_entrustedVisitListSelectedIndex, _entrustedVisitLogEntries.Count);
                _entrustedChildDialogStatus = _entrustedVisitLogEntries.Count > 0
                    ? "CVisitListDlg reopened from the existing entrusted-shop visit log."
                    : "CVisitListDlg reopened without packet-fed visit rows.";
            }
            else
            {
                _entrustedBlacklistSelectedIndex = NormalizeEntrustedDialogSelectionIndex(_entrustedBlacklistSelectedIndex, _blockedVisitors.Count);
                _entrustedChildDialogStatus = _blockedVisitors.Count < 20
                    ? "CBlackListDlg reopened from the existing entrusted-shop blacklist."
                    : "CBlackListDlg reopened at the client-side 20-name add limit.";
            }

            StatusMessage = $"{ResolveEntrustedChildDialogOwnerName(kind)} reopened from the entrusted-shop runtime snapshot.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TryRequestPersonalShopOwnerInfo(out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.PersonalShop)
            {
                message = "Owner-info requests only apply to the personal shop shell.";
                return false;
            }

            string targetName = NormalizeName(OwnerName);
            RoomState = "Owner info request";
            StatusMessage = $"CPersonalShopDlg::OnButtonClicked requested character info for {targetName} through CWvsContext::SendCharacterInfoRequest.";
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool CloseEntrustedChildDialog(out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.EntrustedShop || !_entrustedChildDialogKind.HasValue)
            {
                message = "No entrusted-shop child dialog is currently open.";
                return false;
            }

            EntrustedShopChildDialogKind closingKind = _entrustedChildDialogKind.Value;
            _entrustedChildDialogKind = null;
            _entrustedBlacklistPromptRequest = null;
            _entrustedBlacklistNotice = null;
            _entrustedChildDialogStatus = $"{ResolveEntrustedChildDialogOwnerName(closingKind)} closed through SetRet and returned to the parent entrusted-shop shell.";
            StatusMessage = _entrustedChildDialogStatus;
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TrySelectEntrustedChildDialogEntry(int index, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.EntrustedShop || !_entrustedChildDialogKind.HasValue)
            {
                message = "No entrusted-shop child dialog is available to select.";
                return false;
            }

            if (_entrustedChildDialogKind == EntrustedShopChildDialogKind.VisitList)
            {
                _entrustedVisitListSelectedIndex = NormalizeEntrustedDialogSelectionIndex(index, _entrustedVisitLogEntries.Count);
                if (_entrustedVisitListSelectedIndex < 0)
                {
                    message = "Visit-list selection cleared.";
                    return false;
                }

                EntrustedShopVisitLogEntrySnapshot selectedEntry = _entrustedVisitLogEntries[_entrustedVisitListSelectedIndex];
                _entrustedChildDialogStatus = $"Selected visit row {_entrustedVisitListSelectedIndex + 1}: {selectedEntry.Name} stayed {selectedEntry.StaySeconds}s.";
                StatusMessage = _entrustedChildDialogStatus;
                PersistState();
                message = StatusMessage;
                return true;
            }

            _entrustedBlacklistSelectedIndex = NormalizeEntrustedDialogSelectionIndex(index, _blockedVisitors.Count);
            if (_entrustedBlacklistSelectedIndex < 0)
            {
                message = "Blacklist selection cleared.";
                return false;
            }

            string blockedName = _blockedVisitors[_entrustedBlacklistSelectedIndex];
            _entrustedChildDialogStatus = $"Selected blacklist row {_entrustedBlacklistSelectedIndex + 1}: {blockedName}.";
            StatusMessage = _entrustedChildDialogStatus;
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TryCopySelectedEntrustedVisitName(out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.EntrustedShop || _entrustedChildDialogKind != EntrustedShopChildDialogKind.VisitList)
            {
                message = "Visit-list Save Name only applies while the entrusted visit-list dialog is open.";
                return false;
            }

            if (!HasValidEntrustedVisitListSelection())
            {
                message = "Save Name stays disabled until the selected visit-list cell is valid.";
                return false;
            }

            EntrustedShopVisitLogEntrySnapshot selectedEntry = _entrustedVisitLogEntries[_entrustedVisitListSelectedIndex];
            bool clipboardUpdated = false;
            try
            {
                System.Windows.Forms.Clipboard.SetText(selectedEntry.Name);
                clipboardUpdated = true;
            }
            catch
            {
                clipboardUpdated = false;
            }

            _entrustedChildDialogStatus = clipboardUpdated
                ? $"Save Name copied {selectedEntry.Name} to the clipboard."
                : $"Save Name resolved {selectedEntry.Name}, but clipboard access is not available in this environment.";
            StatusMessage = _entrustedChildDialogStatus;
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TryCreateEntrustedBlacklistPromptRequest(out EntrustedShopBlacklistPromptRequest request, out string message)
        {
            request = null;
            message = null;
            if (Kind != SocialRoomKind.EntrustedShop || _entrustedChildDialogKind != EntrustedShopChildDialogKind.Blacklist)
            {
                message = "Blacklist add only applies while the entrusted blacklist dialog is open.";
                return false;
            }

            if (_blockedVisitors.Count >= 20)
            {
                message = "Blacklist add is disabled because the client-side 20-name limit has been reached.";
                return false;
            }

            request = new EntrustedShopBlacklistPromptRequest
            {
                OwnerName = ResolveEntrustedChildDialogOwnerName(EntrustedShopChildDialogKind.Blacklist),
                Title = "Blacklist",
                PromptText = EntrustedShopBlacklistDialogText.GetAddPromptText(),
                DefaultText = string.Empty,
                StringPoolId = EntrustedShopBlacklistDialogText.AddPromptStringPoolId,
                MinimumLength = 4,
                MaximumLength = 12
            };

            _entrustedBlacklistPromptRequest = CloneEntrustedBlacklistPromptRequest(request);
            _entrustedBlacklistNotice = null;

            _entrustedChildDialogStatus =
                $"CBlackListDlg::AddBlackList opened the shared UtilDlgEx prompt (StringPool 0x{request.StringPoolId:X}, {request.MinimumLength}-{request.MaximumLength} chars).";
            StatusMessage = _entrustedChildDialogStatus;
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TryAddEntrustedBlacklistEntry(string visitorName, out string message)
        {
            if (string.IsNullOrWhiteSpace(visitorName))
            {
                return TryOpenEntrustedBlacklistPrompt(out message);
            }

            return TrySubmitEntrustedBlacklistPrompt(visitorName, out message, out _);
        }

        public bool TryOpenEntrustedBlacklistPrompt(out string message)
        {
            message = null;
            if (!TryCreateEntrustedBlacklistPromptRequest(out EntrustedShopBlacklistPromptRequest request, out string requestMessage))
            {
                message = requestMessage;
                return false;
            }

            if (EntrustedBlacklistPromptRequested == null)
            {
                message = "Entrusted-shop blacklist add requires a registered UtilDlgEx prompt owner.";
                return false;
            }

            bool shown = false;
            try
            {
                shown = EntrustedBlacklistPromptRequested.Invoke(CloneEntrustedBlacklistPromptRequest(request));
            }
            catch
            {
                shown = false;
            }

            if (!shown)
            {
                _entrustedBlacklistPromptRequest = null;
                message = "Entrusted-shop blacklist add prompt could not be opened.";
                return false;
            }

            message = requestMessage;
            return true;
        }

        public bool TrySubmitEntrustedBlacklistPrompt(string visitorName, out string message, out int? noticeStringPoolId)
        {
            message = null;
            noticeStringPoolId = null;
            if (Kind != SocialRoomKind.EntrustedShop || _entrustedChildDialogKind != EntrustedShopChildDialogKind.Blacklist)
            {
                message = "Blacklist add only applies while the entrusted blacklist dialog is open.";
                return false;
            }

            _entrustedBlacklistPromptRequest = null;
            if (_blockedVisitors.Count >= 20)
            {
                message = "Blacklist add is disabled because the client-side 20-name limit has been reached.";
                return false;
            }

            string resolvedName = NormalizeName(visitorName);
            if (!IsValidEntrustedBlacklistName(resolvedName))
            {
                noticeStringPoolId = EntrustedShopBlacklistDialogText.InvalidNameNoticeStringPoolId;
                message = EntrustedShopBlacklistDialogText.GetInvalidNameNotice();
                PublishEntrustedBlacklistNotice(message, noticeStringPoolId.Value);
                _entrustedChildDialogStatus =
                    $"CBlackListDlg::AddBlackList rejected the submitted name and raised StringPool 0x{noticeStringPoolId.Value:X}.";
                StatusMessage = _entrustedChildDialogStatus;
                PersistState();
                return false;
            }

            if (string.Equals(resolvedName, OwnerName, StringComparison.OrdinalIgnoreCase))
            {
                noticeStringPoolId = EntrustedShopBlacklistDialogText.OwnerNoticeStringPoolId;
                message = EntrustedShopBlacklistDialogText.GetOwnerNotice();
                PublishEntrustedBlacklistNotice(message, noticeStringPoolId.Value);
                _entrustedChildDialogStatus =
                    $"CBlackListDlg::AddBlackList rejected owner name {resolvedName} and raised StringPool 0x{noticeStringPoolId.Value:X}.";
                StatusMessage = _entrustedChildDialogStatus;
                PersistState();
                return false;
            }

            if (_blockedVisitors.Contains(resolvedName, StringComparer.OrdinalIgnoreCase))
            {
                noticeStringPoolId = EntrustedShopBlacklistDialogText.DuplicateNoticeStringPoolId;
                message = EntrustedShopBlacklistDialogText.GetDuplicateNotice();
                PublishEntrustedBlacklistNotice(message, noticeStringPoolId.Value);
                _entrustedChildDialogStatus =
                    $"CBlackListDlg::AddBlackList rejected duplicate name {resolvedName} and raised StringPool 0x{noticeStringPoolId.Value:X}.";
                StatusMessage = _entrustedChildDialogStatus;
                PersistState();
                return false;
            }

            _entrustedBlacklistNotice = null;
            _blockedVisitors.Add(resolvedName);
            _entrustedBlacklistSelectedIndex = _blockedVisitors.Count - 1;
            EnsureMerchantPacketNotes();
            _notes[1] = $"Entrusted-shop blacklist: {string.Join(", ", _blockedVisitors)}.";
            _entrustedChildDialogStatus =
                $"CBlackListDlg::AddBlackList added {resolvedName} to the blacklist. Add remains enabled while the count stays below 20.";
            StatusMessage = _entrustedChildDialogStatus;
            PersistState();
            message = StatusMessage;
            return true;
        }

        public void DismissEntrustedBlacklistNotice()
        {
            _entrustedBlacklistNotice = null;
            PersistState();
        }

        public void CancelEntrustedBlacklistPrompt()
        {
            _entrustedBlacklistPromptRequest = null;
            PersistState();
        }

        public bool TryDeleteSelectedEntrustedBlacklistEntry(out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.EntrustedShop || _entrustedChildDialogKind != EntrustedShopChildDialogKind.Blacklist)
            {
                message = "Blacklist delete only applies while the entrusted blacklist dialog is open.";
                return false;
            }

            if (!HasValidEntrustedBlacklistSelection())
            {
                message = "Delete stays disabled until the selected blacklist cell is valid.";
                return false;
            }

            string removedName = _blockedVisitors[_entrustedBlacklistSelectedIndex];
            _blockedVisitors.RemoveAt(_entrustedBlacklistSelectedIndex);
            _entrustedBlacklistSelectedIndex = NormalizeEntrustedDialogSelectionIndex(_entrustedBlacklistSelectedIndex, _blockedVisitors.Count);
            EnsureMerchantPacketNotes();
            _notes[1] = _blockedVisitors.Count > 0
                ? $"Entrusted-shop blacklist: {string.Join(", ", _blockedVisitors)}."
                : "Entrusted-shop blacklist: empty.";
            _entrustedChildDialogStatus = $"Deleted {removedName} from the entrusted-shop blacklist.";
            StatusMessage = _entrustedChildDialogStatus;
            PersistState();
            message = StatusMessage;
            return true;
        }

        private static bool IsValidEntrustedBlacklistName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (normalized.Length < 4 || normalized.Length > 12)
            {
                return false;
            }

            return normalized.All(character => char.IsLetterOrDigit(character));
        }

        private void EnsureMerchantPacketNotes()
        {
            while (_notes.Count < 4)
            {
                _notes.Add(string.Empty);
            }
        }

        private static bool IsClientVisibleMerchantPacketEntry(SocialRoomItemEntry entry)
        {
            return entry != null &&
                !entry.IsClaimed &&
                entry.Quantity > 0;
        }

        private SocialRoomItemEntry FindActiveMerchantPacketEntry(int packetSlotIndex)
        {
            if (packetSlotIndex < 0)
            {
                return null;
            }

            SocialRoomItemEntry exact = _items.FirstOrDefault(item =>
                IsClientVisibleMerchantPacketEntry(item) &&
                item.PacketSlotIndex == packetSlotIndex);
            if (exact != null)
            {
                return exact;
            }

            List<SocialRoomItemEntry> activeEntries = NormalizeActiveMerchantPacketSlots();
            return packetSlotIndex < activeEntries.Count
                ? activeEntries[packetSlotIndex]
                : null;
        }

        private List<SocialRoomItemEntry> NormalizeActiveMerchantPacketSlots()
        {
            List<SocialRoomItemEntry> activeEntries = new();
            int packetSlotIndex = 0;
            foreach (SocialRoomItemEntry entry in _items)
            {
                if (!IsClientVisibleMerchantPacketEntry(entry))
                {
                    entry.UpdatePacketIdentity(entry.ItemId, null);
                    continue;
                }

                entry.UpdatePacketIdentity(entry.ItemId, packetSlotIndex++);
                activeEntries.Add(entry);
            }

            return activeEntries;
        }

        private int TrimActiveMerchantPacketRows(int activeRowCount)
        {
            int normalizedActiveRowCount = Math.Max(0, activeRowCount);
            if (normalizedActiveRowCount == 0)
            {
                int removedAllRows = _items.RemoveAll(item => IsClientVisibleMerchantPacketEntry(item));
                if (removedAllRows > 0)
                {
                    _inventoryEscrow.RemoveAll(escrow => IsClientVisibleMerchantPacketEntry(escrow.Entry));
                }

                return removedAllRows;
            }

            List<SocialRoomItemEntry> activeEntries = NormalizeActiveMerchantPacketSlots();
            if (activeEntries.Count <= normalizedActiveRowCount)
            {
                return 0;
            }

            List<SocialRoomItemEntry> staleEntries = activeEntries
                .Skip(normalizedActiveRowCount)
                .ToList();
            if (staleEntries.Count == 0)
            {
                return 0;
            }

            _items.RemoveAll(item => staleEntries.Contains(item));
            _inventoryEscrow.RemoveAll(escrow => staleEntries.Contains(escrow.Entry));
            return staleEntries.Count;
        }

        private int RemoveClaimedMerchantRows()
        {
            List<SocialRoomItemEntry> removedEntries = _items
                .Where(item => !IsClientVisibleMerchantPacketEntry(item))
                .ToList();
            if (removedEntries.Count == 0)
            {
                NormalizeActiveMerchantPacketSlots();
                return 0;
            }

            int removedRows = _items.RemoveAll(item => removedEntries.Contains(item));
            if (removedRows > 0)
            {
                _inventoryEscrow.RemoveAll(escrow => removedEntries.Contains(escrow.Entry));
            }

            NormalizeActiveMerchantPacketSlots();
            return removedRows;
        }

        private static int GetPersonalShopTax(int mesoAmount)
        {
            if (mesoAmount >= 100000000)
            {
                return (int)(mesoAmount * 0.03d);
            }

            if (mesoAmount >= 25000000)
            {
                return (int)(mesoAmount * 0.025d);
            }

            if (mesoAmount >= 10000000)
            {
                return (int)(mesoAmount * 0.02d);
            }

            if (mesoAmount >= 5000000)
            {
                return (int)(mesoAmount * 0.015d);
            }

            if (mesoAmount >= 1000000)
            {
                return (int)(mesoAmount * 0.009d);
            }

            if (mesoAmount >= 100000)
            {
                return (int)(mesoAmount * 0.004d);
            }

            return 0;
        }

        private void ApplyTradingRoomMesoPacket(int traderIndex, int offeredMeso)
        {
            int normalizedTraderIndex = Math.Clamp(traderIndex, 0, 1);
            int normalizedMeso = Math.Max(0, offeredMeso);
            ClearTradeHandshake();
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
                ClearOmokDialogRequests();
                ResetOmokTurnClock();
                RoomState = "Omok in progress";
                StatusMessage = $"{OwnerName} has the first Omok turn with black stones.";
                SetOmokDialogStatus("COmokDlg::OnUserStart opened the round and assigned black stones to the host.", 1500);
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
            ClearOmokDialogRequests();
            StartOmokStoneAnimation();
            ResetOmokTurnClock();
            _miniRoomOmokMoveHistory.Add(new OmokMoveHistoryEntry(x, y, stoneValue, playerIndex));

            string playerName = ResolveMiniRoomSeatName(playerIndex);
            string stoneName = ResolveOmokStoneName(stoneValue);
            AddMiniRoomSpeakerMessage(playerName, $"placed a {stoneName} stone at {x},{y}.", playerIndex == 0);

            if (HasFiveInRow(x, y, stoneValue))
            {
                _miniRoomOmokInProgress = false;
                _miniRoomOmokWinnerIndex = playerIndex;
                RoomState = "Omok result";
                StatusMessage = $"{playerName} completed five in a row and won the Omok round.";
                SetOmokDialogStatus($"{playerName} locked the Omok result layer with five in a row.", 2200);
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
            SetOmokDialogStatus($"{playerName} placed a {stoneName} stone. {_miniRoomOmokTimeFloor}s remain on the dialog timer.", 900);
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
                return TryRespondMiniRoomTieRequest(accept: true, out message);
            }

            _miniRoomOmokDrawRequestSent = true;
            _miniRoomOmokDrawRequestSentTurn = true;
            _miniRoomOmokPendingPromptText = ResolveOmokString(
                OmokOutgoingTiePromptStringPoolId,
                "Will you request a tie?");
            RoomState = "Omok tie request";
            StatusMessage = _miniRoomOmokPendingPromptText;
            SetOmokDialogStatus("COmokDlg prepared packet 50 for a local tie request and is waiting on packet 51/62.", 2200);
            _miniRoomOmokLastOutboundPacketSummary = "COmokDlg::OnTieRequest would send mini-room packet 50 after the local Yes/No prompt is accepted.";
            AddMiniRoomSystemMessage("System : Tie request prompt opened. Confirming it would send packet 50.");
            SyncMiniRoomOmokPresentation();
            PersistState();
            message = StatusMessage;
            return true;
        }

        private void PublishEntrustedBlacklistNotice(string text, int stringPoolId)
        {
            _entrustedBlacklistNotice = new EntrustedShopNoticeSnapshot
            {
                OwnerName = ResolveEntrustedChildDialogOwnerName(EntrustedShopChildDialogKind.Blacklist),
                Title = "Blacklist",
                Text = text ?? string.Empty,
                StringPoolId = stringPoolId
            };

            try
            {
                EntrustedBlacklistNoticeRequested?.Invoke(CloneEntrustedBlacklistNotice(_entrustedBlacklistNotice));
            }
            catch
            {
            }
        }

        public bool TryRespondMiniRoomTieRequest(bool accept, out string message)
        {
            message = null;
            if (!IsMiniRoomOmokActive || !_miniRoomOmokInProgress || !_miniRoomOmokTieRequested)
            {
                message = "No incoming Omok tie request is waiting for a reply.";
                return false;
            }

            _miniRoomOmokTieRequested = false;
            _miniRoomOmokDrawRequestSent = false;
            _miniRoomOmokDrawRequestSentTurn = false;
            ClearOmokPendingPrompt();
            RoomState = "Omok tie response";
            StatusMessage = accept
                ? "Accepted the incoming Omok tie request. Waiting for packet 62 to close the round."
                : "Declined the incoming Omok tie request. The round stays live until packet 62 or the next turn packet arrives.";
            SetOmokDialogStatus(StatusMessage, 2000);
            _miniRoomOmokLastOutboundPacketSummary = $"COmokDlg::OnTieRequest would send mini-room packet 51 with accept={(accept ? 1 : 0)}.";
            AddMiniRoomSystemMessage(
                $"System : {(accept ? "Tie request accepted" : "Tie request declined")} and packet 51 would be sent.");
            SyncMiniRoomOmokPresentation();
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TryRequestMiniRoomRetreat(out string message)
        {
            message = null;
            if (!IsMiniRoomOmokActive)
            {
                message = "Mini-room retreat requests are only modeled for Omok.";
                return false;
            }

            if (!_miniRoomOmokInProgress)
            {
                message = "No Omok round is active.";
                return false;
            }

            if (_miniRoomOmokRetreatRequested)
            {
                return TryRespondMiniRoomRetreatRequest(accept: true, out message);
            }

            _miniRoomOmokRetreatRequestSent = true;
            _miniRoomOmokRetreatRequestSentTurn = true;
            _miniRoomOmokPendingPromptText = ResolveOmokString(
                OmokOutgoingRetreatPromptStringPoolId,
                "Request to withdraw your last move?");
            RoomState = "Omok retreat request";
            StatusMessage = _miniRoomOmokPendingPromptText;
            SetOmokDialogStatus("COmokDlg prepared packet 54 for a local retreat request and is waiting on packet 55.", 2200);
            _miniRoomOmokLastOutboundPacketSummary = "COmokDlg::OnRetreatRequest would send mini-room packet 54 after the local Yes/No prompt is accepted.";
            AddMiniRoomSystemMessage("System : Retreat request prompt opened. Confirming it would send packet 54.", isWarning: true);
            SyncMiniRoomOmokPresentation();
            PersistState();
            message = StatusMessage;
            return true;
        }

        public bool TryRespondMiniRoomRetreatRequest(bool accept, out string message)
        {
            message = null;
            if (!IsMiniRoomOmokActive || !_miniRoomOmokInProgress || !_miniRoomOmokRetreatRequested)
            {
                message = "No incoming Omok retreat request is waiting for a reply.";
                return false;
            }

            _miniRoomOmokRetreatRequested = false;
            _miniRoomOmokRetreatRequestSent = false;
            _miniRoomOmokRetreatRequestSentTurn = false;
            ClearOmokPendingPrompt();
            RoomState = "Omok retreat response";
            StatusMessage = accept
                ? "Accepted the incoming Omok retreat request. Waiting for packet 55 to remove the last stones."
                : "Declined the incoming Omok retreat request. The round stays live until the next authoritative packet arrives.";
            SetOmokDialogStatus(StatusMessage, 2000);
            _miniRoomOmokLastOutboundPacketSummary = $"COmokDlg::OnRetreatRequest would send mini-room packet 55 with accept={(accept ? 1 : 0)}.";
            AddMiniRoomSystemMessage(
                $"System : {(accept ? "Retreat request accepted" : "Retreat request declined")} and packet 55 would be sent.",
                isWarning: true);
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
            ClearOmokDialogRequests();
            RoomState = "Omok forfeit";
            StatusMessage = $"{ResolveMiniRoomSeatName(loserIndex)} forfeited. {ResolveMiniRoomSeatName(winnerIndex)} won the Omok round.";
            SetOmokDialogStatus(StatusMessage, 2200);
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
            _tradeVerificationPending = _tradeLocalLocked && _tradeRemoteLocked && (_tradeLocalVerificationEntries.Count > 0 || _tradeRemoteVerificationEntries.Count > 0);
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

            if (_tradeVerificationPending)
            {
                message = "The trade is still waiting on CRC verification payloads.";
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

            _tradeVerificationPending = false;
            _tradeAutoCrcReplyPending = false;
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
            _tradeVerificationPending = false;
            _tradeAutoCrcReplyPending = false;
            _tradeLocalVerificationReady = false;
            _tradeRemoteVerificationReady = false;
            _tradeLocalVerificationEntries.Clear();
            _tradeRemoteVerificationEntries.Clear();
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

            if (_tradeVerificationPending)
            {
                message = "The trade is still waiting on CRC verification payloads.";
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
            _tradeVerificationPending = false;
            _tradeAutoCrcReplyPending = false;
            _tradeLocalVerificationReady = false;
            _tradeRemoteVerificationReady = false;
            _tradeLocalVerificationEntries.Clear();
            _tradeRemoteVerificationEntries.Clear();
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
            if (TryGetVisibleEmployeePoolEntry(out SocialRoomEmployeePoolEntryState pooledEmployee))
            {
                string pooledTemplateText = pooledEmployee.TemplateId > 0 ? pooledEmployee.TemplateId.ToString() : "legacy";
                string pooledBalloonText = string.IsNullOrWhiteSpace(pooledEmployee.BalloonTitle)
                    ? $"type={pooledEmployee.MiniRoomType}"
                    : pooledEmployee.BalloonTitle;
                string pooledOwnerText = string.IsNullOrWhiteSpace(pooledEmployee.NameTag) ? OwnerName : pooledEmployee.NameTag;
                return $"{pooledTemplateText}@world({pooledEmployee.WorldX},{pooledEmployee.WorldY}), pkt(owner={pooledOwnerText}, employer={pooledEmployee.EmployerId}, fh={pooledEmployee.FootholdId}, balloon={pooledBalloonText})";
            }

            string templateText = _employeeTemplateId > 0 ? _employeeTemplateId.ToString() : "legacy";
            string placement = _employeeHasWorldPosition && !_employeeUseOwnerAnchor
                ? $"world({_employeeWorldX},{_employeeWorldY})"
                : $"owner({_employeeAnchorOffsetX},{_employeeAnchorOffsetY})";
            if (_employeePoolRuntime.HasEntries)
            {
                return $"{templateText}@hidden, pkt(employer={ResolveEmployeePoolEmployerId()})";
            }

            return $"{templateText}@{placement}";
        }

        private string ResolveEmployeeDisplayHeadline(string fallbackHeadline)
        {
            string headline = RoomTitle;
            return string.IsNullOrWhiteSpace(headline) ? fallbackHeadline : headline;
        }

        private static string ResolveEmployeeFuncHeadline(SocialRoomFieldActorTemplate template, string fallbackHeadline)
        {
            string npcId = template == SocialRoomFieldActorTemplate.StoreBanker ? "9030000" : "9071001";
            if (EmployeeNpcFuncHeadlineCache.TryGetValue(npcId, out string cachedHeadline))
            {
                return string.IsNullOrWhiteSpace(cachedHeadline) ? fallbackHeadline : cachedHeadline;
            }

            string headline = fallbackHeadline;
            WzImage npcStringImage = global::HaCreator.Program.FindImage("String", "Npc.img");
            if (npcStringImage != null)
            {
                npcStringImage.ParseImage();
                if (npcStringImage[npcId]?["func"] is WzStringProperty funcProperty
                    && !string.IsNullOrWhiteSpace(funcProperty.Value))
                {
                    headline = funcProperty.Value.Trim();
                }
            }

            EmployeeNpcFuncHeadlineCache[npcId] = headline ?? string.Empty;
            return string.IsNullOrWhiteSpace(headline) ? fallbackHeadline : headline;
        }

        private string ResolveEmployeeDisplayOwnerName(SocialRoomEmployeePoolEntryState pooledEmployee = null)
        {
            if (pooledEmployee != null && !string.IsNullOrWhiteSpace(pooledEmployee.NameTag))
            {
                return pooledEmployee.NameTag;
            }

            if (pooledEmployee == null
                && TryGetVisibleEmployeePoolEntry(out SocialRoomEmployeePoolEntryState visiblePooledEmployee)
                && !string.IsNullOrWhiteSpace(visiblePooledEmployee.NameTag))
            {
                return visiblePooledEmployee.NameTag;
            }

            return string.IsNullOrWhiteSpace(OwnerName) ? "Owner" : OwnerName;
        }

        private string BuildEmployeePacketStateKeySuffix()
        {
            if (TryGetVisibleEmployeePoolEntry(out SocialRoomEmployeePoolEntryState pooledEmployee))
            {
                return $"|pkt|{pooledEmployee.EmployerId}|{pooledEmployee.FootholdId}|{pooledEmployee.MiniRoomType}|{pooledEmployee.MiniRoomSerial}|{pooledEmployee.BalloonTitle}|{pooledEmployee.BalloonByte0}|{pooledEmployee.BalloonByte1}|{pooledEmployee.BalloonByte2}";
            }

            return string.Empty;
        }

        private bool TryGetVisibleEmployeePoolEntry(out SocialRoomEmployeePoolEntryState state)
        {
            return _employeePoolRuntime.TryGetPrimaryEntry(out state) && state != null && state.IsVisible;
        }

        internal bool MatchesEmployeeEmployerId(int employerId)
        {
            int normalizedEmployerId = Math.Max(0, employerId);
            if (normalizedEmployerId <= 0
                || (Kind != SocialRoomKind.PersonalShop && Kind != SocialRoomKind.EntrustedShop))
            {
                return false;
            }

            return _employeePacketEmployerId == normalizedEmployerId
                || _employeePoolRuntime.PreferredEmployerId == normalizedEmployerId
                || _employeePoolRuntime.HasEmployer(normalizedEmployerId);
        }

        internal int ScoreEmployeeRoutingHint(SocialRoomEmployeePoolCodec.RoutingHint hint)
        {
            if (Kind != SocialRoomKind.PersonalShop && Kind != SocialRoomKind.EntrustedShop)
            {
                return 0;
            }

            int score = _employeePoolRuntime.ScoreRoutingHint(hint);

            if (hint.MiniRoomType != 0)
            {
                bool kindMatches = (hint.MiniRoomType == 3 && Kind == SocialRoomKind.PersonalShop)
                    || ((hint.MiniRoomType == 4 || hint.MiniRoomType == 5) && Kind == SocialRoomKind.EntrustedShop);
                score += kindMatches ? 30 : -20;
            }

            if (!string.IsNullOrWhiteSpace(hint.OwnerName))
            {
                string normalizedHintOwner = NormalizeName(hint.OwnerName);
                string normalizedRuntimeOwner = NormalizeName(OwnerName);
                if (string.Equals(normalizedRuntimeOwner, normalizedHintOwner, StringComparison.OrdinalIgnoreCase))
                {
                    score += 35;
                }
                else if (!string.IsNullOrWhiteSpace(OwnerName))
                {
                    score -= 5;
                }
            }

            if (!string.IsNullOrWhiteSpace(hint.BalloonTitle))
            {
                string normalizedHintTitle = hint.BalloonTitle.Trim();
                if (!string.IsNullOrWhiteSpace(RoomTitle)
                    && string.Equals(RoomTitle.Trim(), normalizedHintTitle, StringComparison.OrdinalIgnoreCase))
                {
                    score += 20;
                }
                else if (!string.IsNullOrWhiteSpace(RoomTitle))
                {
                    score -= 5;
                }
            }

            return Math.Max(0, score);
        }

        internal void ApplyPacketOwnedEmployeePoolState(
            IReadOnlyList<SocialRoomEmployeePoolEntrySnapshot> snapshots,
            string statusMessage,
            bool persistState)
        {
            if (Kind != SocialRoomKind.PersonalShop && Kind != SocialRoomKind.EntrustedShop)
            {
                return;
            }

            _employeePoolRuntime.Restore(snapshots);
            if (_employeePoolRuntime.PreferredEmployerId > 0)
            {
                _employeePacketEmployerId = _employeePoolRuntime.PreferredEmployerId;
            }

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                StatusMessage = statusMessage;
            }

            if (persistState)
            {
                PersistState();
            }
        }

        private int ResolveEmployeePoolEmployerId()
        {
            if (TryGetVisibleEmployeePoolEntry(out SocialRoomEmployeePoolEntryState pooledEmployee))
            {
                return pooledEmployee.EmployerId;
            }

            if (_employeePoolRuntime.PreferredEmployerId > 0)
            {
                return _employeePoolRuntime.PreferredEmployerId;
            }

            return Math.Max(0, _employeePacketEmployerId);
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

        private static string NormalizePacketText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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
            _tradeVerificationPending = false;
            _tradeAutoCrcReplyPending = false;
            _tradeLocalVerificationReady = false;
            _tradeRemoteVerificationReady = false;
            _tradeLocalVerificationEntries.Clear();
            _tradeRemoteVerificationEntries.Clear();
            RefreshTradeOccupantsAndRows();
        }

        private static string BuildPacketHexPreview(byte[] payload, int previewByteCount = 24)
        {
            if (payload == null || payload.Length == 0)
            {
                return "empty";
            }

            int count = Math.Min(previewByteCount, payload.Length);
            string preview = BitConverter.ToString(payload, 0, count);
            return payload.Length > count
                ? $"{preview}..."
                : preview;
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
                    ? _tradeVerificationPending
                        ? "Verifying"
                        : "Locked"
                    : "Reviewing";
            string verification = isLocal
                ? DescribeTradeVerificationSide(_tradeLocalVerificationEntries, _tradeLocalVerificationReady)
                : DescribeTradeVerificationSide(_tradeRemoteVerificationEntries, _tradeRemoteVerificationReady);
            return $"{stage} | {itemCount} item entr{(itemCount == 1 ? "y" : "ies")} | {meso:N0} mesos | {verification}";
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
                    ? $"Last move: {FormatOmokLastMove()} | Turn {ResolveOmokTurnName()} | Clock {_miniRoomOmokTimeFloor}s"
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

            _notes[0] = "WZ-backed Omok shell uses UIWindow(.2).img/Minigame/Omok art and the 12-frame stone families.";
            _notes[1] = BuildOmokDialogOwnerNote();
        }

        private void ResetOmokBoard()
        {
            Array.Clear(_miniRoomOmokBoard, 0, _miniRoomOmokBoard.Length);
            _miniRoomOmokMoveHistory.Clear();
            _miniRoomOmokLastMoveX = -1;
            _miniRoomOmokLastMoveY = -1;
            _miniRoomOmokWinnerIndex = -1;
            _miniRoomOmokStoneAnimationTimeLeftMs = 0;
            _miniRoomOmokDialogEffectTimeLeftMs = 0;
            _miniRoomOmokDialogStatus = string.Empty;
            ClearOmokDialogRequests();
            ResetOmokTurnClock();
        }

        private static bool IsValidOmokCoordinate(int x, int y)
        {
            return x >= 0 && x < MiniRoomOmokBoardSize && y >= 0 && y < MiniRoomOmokBoardSize;
        }

        private bool TryPopOmokMoveHistory(out OmokMoveHistoryEntry move)
        {
            if (_miniRoomOmokMoveHistory.Count > 0)
            {
                move = _miniRoomOmokMoveHistory[^1];
                _miniRoomOmokMoveHistory.RemoveAt(_miniRoomOmokMoveHistory.Count - 1);
                return true;
            }

            move = default;
            return false;
        }

        private bool TryRemoveNewestOmokStoneFromBoardFallback()
        {
            for (int y = MiniRoomOmokBoardSize - 1; y >= 0; y--)
            {
                for (int x = MiniRoomOmokBoardSize - 1; x >= 0; x--)
                {
                    int boardIndex = GetOmokBoardIndex(x, y);
                    if (_miniRoomOmokBoard[boardIndex] == 0)
                    {
                        continue;
                    }

                    _miniRoomOmokBoard[boardIndex] = 0;
                    return true;
                }
            }

            return false;
        }

        private void UpdateLastOmokMoveFromHistory()
        {
            if (_miniRoomOmokMoveHistory.Count == 0)
            {
                _miniRoomOmokLastMoveX = -1;
                _miniRoomOmokLastMoveY = -1;
                return;
            }

            OmokMoveHistoryEntry lastMove = _miniRoomOmokMoveHistory[^1];
            _miniRoomOmokLastMoveX = lastMove.X;
            _miniRoomOmokLastMoveY = lastMove.Y;
        }

        private List<TradeVerificationEntry> BuildTradeVerificationEntries(bool isLocalParty)
        {
            string ownerName = isLocalParty ? OwnerName : ResolveRemoteTraderName();
            List<TradeVerificationEntry> entries = new();
            foreach (SocialRoomItemEntry item in _items
                .Where(entry => string.Equals(entry.OwnerName, ownerName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.PacketSlotIndex ?? int.MaxValue)
                .Take(TradingRoomClientItemSlotCount))
            {
                int itemId = item.ItemId;
                if (itemId <= 0)
                {
                    continue;
                }

                entries.Add(new TradeVerificationEntry(itemId, ResolveTradeVerificationChecksum(itemId)));
            }

            return entries;
        }

        private bool TryValidateTradeVerificationEntries(
            bool isLocalParty,
            IReadOnlyList<TradeVerificationEntry> receivedEntries,
            out string detail)
        {
            detail = null;
            List<TradeVerificationEntry> expectedEntries = BuildTradeVerificationEntries(isLocalParty);
            if (receivedEntries == null)
            {
                detail = "the packet did not provide any checksum rows";
                return false;
            }

            if (receivedEntries.Count != expectedEntries.Count)
            {
                detail = $"expected {expectedEntries.Count} entr{(expectedEntries.Count == 1 ? "y" : "ies")} but received {receivedEntries.Count}";
                return false;
            }

            for (int i = 0; i < expectedEntries.Count; i++)
            {
                TradeVerificationEntry expected = expectedEntries[i];
                TradeVerificationEntry received = receivedEntries[i];
                if (expected.ItemId != received.ItemId)
                {
                    detail = $"row {i + 1} expected item {expected.ItemId} but received {received.ItemId}";
                    return false;
                }

                if (expected.Checksum != received.Checksum)
                {
                    detail = $"row {i + 1} for item {expected.ItemId} expected CRC 0x{expected.Checksum:X8} but received 0x{received.Checksum:X8}";
                    return false;
                }
            }

            return true;
        }

        private static uint ResolveTradeVerificationChecksum(int itemId)
        {
            if (InventoryItemMetadataResolver.TryResolveClientItemCrc(itemId, out uint crc) && crc != 0)
            {
                return crc;
            }

            Span<byte> hashInput = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(hashInput, itemId);
            return ComputeFallbackTradeVerificationChecksum(hashInput);
        }

        private static uint ComputeFallbackTradeVerificationChecksum(ReadOnlySpan<byte> payload)
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;
            uint hash = offsetBasis;
            foreach (byte value in payload)
            {
                hash ^= value;
                hash *= prime;
            }

            return hash;
        }

        internal static uint ComputeTradeVerificationChecksumForTest(int itemId)
        {
            return ResolveTradeVerificationChecksum(itemId);
        }

        internal static bool TryDecodePacketOwnedTradeItemForTest(
            byte[] payload,
            out byte slotType,
            out long baseExpirationTime,
            out int itemId,
            out int quantity,
            out InventoryType inventoryType,
            out string title,
            out string metadataSummary,
            out long? nonCashSerialNumber,
            out long? expirationTime,
            out int? tailValue,
            out string tailMetadataSummary,
            out string error)
        {
            slotType = 0;
            baseExpirationTime = 0;
            itemId = 0;
            quantity = 0;
            inventoryType = InventoryType.NONE;
            title = string.Empty;
            metadataSummary = string.Empty;
            nonCashSerialNumber = null;
            expirationTime = null;
            tailValue = null;
            tailMetadataSummary = string.Empty;

            if (!TryDecodePacketOwnedTradeItem(payload, out PacketOwnedTradeItem item, out error))
            {
                return false;
            }

            slotType = item.SlotType;
            baseExpirationTime = item.BaseExpirationTime ?? 0;
            itemId = item.ItemId;
            quantity = item.Quantity;
            inventoryType = item.InventoryType;
            title = item.Title;
            metadataSummary = item.MetadataSummary;
            nonCashSerialNumber = item.NonCashSerialNumber;
            expirationTime = item.ExpirationTime;
            tailValue = item.TailValue;
            tailMetadataSummary = item.TailMetadataSummary;
            return true;
        }

        private string DescribeTradeVerificationStatus()
        {
            if (_tradeLocalVerificationEntries.Count == 0 && _tradeRemoteVerificationEntries.Count == 0 && !_tradeVerificationPending)
            {
                return "idle";
            }

            return $"pending={_tradeVerificationPending.ToString().ToLowerInvariant()}, local={_tradeLocalVerificationEntries.Count}, remote={_tradeRemoteVerificationEntries.Count}";
        }

        private static string DescribeTradeVerificationSide(IReadOnlyCollection<TradeVerificationEntry> entries, bool ready)
        {
            if (entries == null || entries.Count == 0)
            {
                return ready ? "CRC ready" : "CRC idle";
            }

            return ready ? $"CRC {entries.Count} ready" : $"CRC {entries.Count} pending";
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

        private void ResetOmokTurnClock(int timeLeftMs = 30000)
        {
            _miniRoomOmokTimeLeftMs = Math.Max(0, timeLeftMs);
            _miniRoomOmokTimeFloor = (_miniRoomOmokTimeLeftMs + 999) / 1000;
            _miniRoomOmokLastTimedStateUtc = null;
        }

        private void StartOmokStoneAnimation()
        {
            _miniRoomOmokStoneAnimationTimeLeftMs = 450;
        }

        private void SetOmokDialogStatus(string status, int effectTimeLeftMs = 1800)
        {
            _miniRoomOmokDialogStatus = status ?? string.Empty;
            _miniRoomOmokDialogEffectTimeLeftMs = Math.Max(0, effectTimeLeftMs);
        }

        private void ClearOmokPendingPrompt()
        {
            _miniRoomOmokPendingPromptText = string.Empty;
        }

        private void RecordOmokSoundByStringPoolId(int stringPoolId)
        {
            _miniRoomOmokLastClientSoundPath = ResolveOmokSoundLabel(stringPoolId);
        }

        private void ClearOmokDialogRequests()
        {
            _miniRoomOmokTieRequested = false;
            _miniRoomOmokDrawRequestSent = false;
            _miniRoomOmokDrawRequestSentTurn = false;
            _miniRoomOmokRetreatRequested = false;
            _miniRoomOmokRetreatRequestSent = false;
            _miniRoomOmokRetreatRequestSentTurn = false;
            _miniRoomOmokLastOutboundPacketSummary = string.Empty;
        }

        private static string ResolveOmokString(int stringPoolId, string fallbackText)
        {
            return MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackText);
        }

        private static string FormatOmokString(int stringPoolId, string fallbackFormat, params object[] args)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                args?.Length ?? 0,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, args ?? Array.Empty<object>());
        }

        private static string ResolveOmokSoundLabel(int stringPoolId)
        {
            string soundName = ResolveOmokString(stringPoolId, MapleStoryStringPool.FormatFallbackLabel(stringPoolId, 3));
            return soundName.Contains('/')
                ? soundName
                : $"Sound/MiniGame.img/{soundName}";
        }

        private string FormatOmokTurnStatus(int seatIndex)
        {
            return FormatOmokString(
                OmokTurnTextStringPoolId,
                "It's [ {0} ]'s turn.",
                ResolveMiniRoomSeatName(seatIndex));
        }

        private string BuildOmokDialogOwnerNote()
        {
            if (!IsMiniRoomOmokActive)
            {
                return string.Empty;
            }

            if (_miniRoomOmokTieRequested)
            {
                return "COmokDlg owner status: draw request waiting on reply.";
            }

            if (_miniRoomOmokRetreatRequested)
            {
                return "COmokDlg owner status: retreat request waiting on reply.";
            }

            if (!string.IsNullOrWhiteSpace(_miniRoomOmokPendingPromptText))
            {
                return $"COmokDlg owner prompt: {_miniRoomOmokPendingPromptText.Replace('\r', ' ').Replace('\n', ' ')}";
            }

            if (!string.IsNullOrWhiteSpace(_miniRoomOmokLastOutboundPacketSummary))
            {
                return _miniRoomOmokLastOutboundPacketSummary;
            }

            if (!string.IsNullOrWhiteSpace(_miniRoomOmokDialogStatus))
            {
                return $"COmokDlg owner status: {_miniRoomOmokDialogStatus}";
            }

            if (_miniRoomOmokInProgress)
            {
                return $"COmokDlg owner status: live round, {ResolveMiniRoomSeatName(_miniRoomOmokCurrentTurnIndex)} on move, {_miniRoomOmokTimeFloor}s on the client timer.";
            }

            return "COmokDlg owner status: waiting for ready/start flow.";
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

        private static bool TryExtractEmployeePoolPacket(byte[] packetBytes, out ushort opcode, out byte[] payload)
        {
            opcode = 0;
            payload = Array.Empty<byte>();
            if (packetBytes == null || packetBytes.Length <= sizeof(ushort))
            {
                return false;
            }

            opcode = BitConverter.ToUInt16(packetBytes, 0);
            if (opcode != EmployeeEnterFieldOpcode
                && opcode != EmployeeLeaveFieldOpcode
                && opcode != EmployeeMiniRoomBalloonOpcode)
            {
                opcode = 0;
                return false;
            }

            payload = new byte[packetBytes.Length - sizeof(ushort)];
            Buffer.BlockCopy(packetBytes, sizeof(ushort), payload, 0, payload.Length);
            return true;
        }

        private static bool IsKnownPacketType(byte packetType)
        {
            return packetType is
                TradingRoomPutItemPacketType or
                TradingRoomPutMoneyPacketType or
                TradingRoomTradePacketType or
                TradingRoomItemCrcPacketType or
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
