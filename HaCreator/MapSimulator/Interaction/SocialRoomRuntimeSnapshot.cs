using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    public enum EntrustedShopChildDialogKind
    {
        VisitList,
        Blacklist
    }

    public sealed class SocialRoomRuntimeSnapshot
    {
        public SocialRoomKind Kind { get; set; }
        public string RoomTitle { get; set; }
        public string OwnerName { get; set; }
        public int MesoAmount { get; set; }
        public string StatusMessage { get; set; }
        public string RoomState { get; set; }
        public string ModeName { get; set; }
        public string PacketOwnerSummary { get; set; }
        public int PersonalShopTotalSoldGross { get; set; }
        public int PersonalShopTotalReceivedNet { get; set; }
        public int MiniRoomModeIndex { get; set; }
        public int MiniRoomWagerAmount { get; set; }
        public bool MiniRoomOmokInProgress { get; set; }
        public int MiniRoomOmokCurrentTurnIndex { get; set; }
        public int MiniRoomOmokWinnerIndex { get; set; } = -1;
        public int MiniRoomOmokLastMoveX { get; set; } = -1;
        public int MiniRoomOmokLastMoveY { get; set; } = -1;
        public int MiniRoomOmokTimeLeftMs { get; set; } = 30000;
        public int MiniRoomOmokTimeFloor { get; set; } = 30;
        public int MiniRoomOmokOwnerStoneValue { get; set; } = 1;
        public int MiniRoomOmokGuestStoneValue { get; set; } = 2;
        public bool MiniRoomOmokTieRequested { get; set; }
        public bool MiniRoomOmokDrawRequestSent { get; set; }
        public bool MiniRoomOmokDrawRequestSentTurn { get; set; }
        public bool MiniRoomOmokRetreatRequested { get; set; }
        public bool MiniRoomOmokRetreatRequestSent { get; set; }
        public bool MiniRoomOmokRetreatRequestSentTurn { get; set; }
        public List<int> MiniRoomOmokBoard { get; set; } = new();
        public List<SocialRoomOmokMoveSnapshot> MiniRoomOmokMoveHistory { get; set; } = new();
        public int TradeLocalOfferMeso { get; set; }
        public int TradeRemoteOfferMeso { get; set; }
        public bool TradeLocalLocked { get; set; }
        public bool TradeRemoteLocked { get; set; }
        public bool TradeLocalAccepted { get; set; }
        public bool TradeRemoteAccepted { get; set; }
        public bool TradeVerificationPending { get; set; }
        public bool TradeVerificationMismatch { get; set; }
        public bool TradeLocalVerificationReady { get; set; }
        public bool TradeRemoteVerificationReady { get; set; }
        public List<SocialRoomTradeVerificationEntrySnapshot> TradeLocalVerificationEntries { get; set; } = new();
        public List<SocialRoomTradeVerificationEntrySnapshot> TradeRemoteVerificationEntries { get; set; } = new();
        public DateTime? EntrustedPermitExpiresAtUtc { get; set; }
        public int EmployeeTemplateId { get; set; }
        public bool EmployeeUseOwnerAnchor { get; set; } = true;
        public int EmployeeAnchorOffsetX { get; set; }
        public int EmployeeAnchorOffsetY { get; set; }
        public int EmployeeWorldX { get; set; }
        public int EmployeeWorldY { get; set; }
        public bool EmployeeHasWorldPosition { get; set; }
        public bool? EmployeeFlip { get; set; }
        public bool EmployeeHasPacketData { get; set; }
        public bool EmployeePacketActorHidden { get; set; }
        public int EmployeePacketEmployerId { get; set; }
        public int EmployeePreferredEmployerId { get; set; }
        public int EmployeePacketFootholdId { get; set; }
        public string EmployeePacketNameTag { get; set; }
        public byte EmployeePacketMiniRoomType { get; set; }
        public int EmployeePacketMiniRoomSerial { get; set; }
        public string EmployeePacketBalloonTitle { get; set; }
        public byte EmployeePacketBalloonByte0 { get; set; }
        public byte EmployeePacketBalloonByte1 { get; set; }
        public byte EmployeePacketBalloonByte2 { get; set; }
        public List<SocialRoomEmployeePoolEntrySnapshot> EmployeePoolEntries { get; set; } = new();
        public List<SocialRoomOccupantSnapshot> Occupants { get; set; } = new();
        public List<SocialRoomItemSnapshot> Items { get; set; } = new();
        public List<SocialRoomSoldItemSnapshot> SoldItems { get; set; } = new();
        public List<string> Notes { get; set; } = new();
        public List<SocialRoomChatEntrySnapshot> ChatEntries { get; set; } = new();
        public List<string> SavedVisitors { get; set; } = new();
        public int EntrustedVisitListSelectedIndex { get; set; } = -1;
        public int EntrustedBlacklistSelectedIndex { get; set; } = -1;
        public string EntrustedChildDialogStatus { get; set; }
        public List<EntrustedShopVisitLogEntrySnapshot> EntrustedVisitLogEntries { get; set; } = new();
        public EntrustedShopChildDialogSnapshot EntrustedChildDialog { get; set; }
        public List<string> BlockedVisitors { get; set; } = new();
        public int RemoteInventoryMeso { get; set; }
        public List<SocialRoomRemoteInventoryEntrySnapshot> RemoteInventoryEntries { get; set; } = new();
    }

    public sealed class EntrustedShopVisitLogEntrySnapshot
    {
        public string Name { get; set; }
        public int StaySeconds { get; set; }
    }

    public sealed class EntrustedShopChildDialogEntrySnapshot
    {
        public string PrimaryText { get; set; }
        public string SecondaryText { get; set; }
        public bool IsSelected { get; set; }
    }

    public sealed class EntrustedShopBlacklistPromptRequest
    {
        public string OwnerName { get; set; }
        public string Title { get; set; }
        public string PromptText { get; set; }
        public string DefaultText { get; set; }
        public int StringPoolId { get; set; } = -1;
        public int MinimumLength { get; set; }
        public int MaximumLength { get; set; }
    }

    public sealed class EntrustedShopNoticeSnapshot
    {
        public string OwnerName { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public int StringPoolId { get; set; } = -1;
    }

    public sealed class EntrustedShopChildDialogSnapshot
    {
        public bool IsOpen { get; set; }
        public EntrustedShopChildDialogKind Kind { get; set; }
        public string OwnerName { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string StatusText { get; set; }
        public string PrimaryActionText { get; set; }
        public string SecondaryActionText { get; set; }
        public string FooterText { get; set; }
        public bool CanPrimaryAction { get; set; }
        public bool CanSecondaryAction { get; set; }
        public int SelectedIndex { get; set; } = -1;
        public List<EntrustedShopChildDialogEntrySnapshot> Entries { get; set; } = new();
        public EntrustedShopBlacklistPromptRequest BlacklistPromptRequest { get; set; }
        public EntrustedShopNoticeSnapshot BlacklistNotice { get; set; }
    }

    public sealed class SocialRoomOccupantSnapshot
    {
        public string Name { get; set; }
        public SocialRoomOccupantRole Role { get; set; }
        public string Detail { get; set; }
        public bool IsReady { get; set; }
    }

    public sealed class SocialRoomItemSnapshot
    {
        public string OwnerName { get; set; }
        public string ItemName { get; set; }
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public int MesoAmount { get; set; }
        public string Detail { get; set; }
        public bool IsLocked { get; set; }
        public bool IsClaimed { get; set; }
        public int? PacketSlotIndex { get; set; }
    }

    public sealed class SocialRoomChatEntrySnapshot
    {
        public string Text { get; set; }
        public SocialRoomChatTone Tone { get; set; }
    }

    public sealed class SocialRoomSoldItemSnapshot
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string BuyerName { get; set; }
        public int QuantitySold { get; set; }
        public int BundleCount { get; set; }
        public int BundlePrice { get; set; }
        public int GrossMeso { get; set; }
        public int TaxMeso { get; set; }
        public int NetMeso { get; set; }
        public int PacketSlotIndex { get; set; }
    }

    public sealed class SocialRoomOmokMoveSnapshot
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int StoneValue { get; set; }
        public int SeatIndex { get; set; }
    }

    public sealed class SocialRoomTradeVerificationEntrySnapshot
    {
        public int ItemId { get; set; }
        public uint Checksum { get; set; }
    }

    public sealed class SocialRoomRemoteInventoryEntrySnapshot
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class SocialRoomEmployeePoolEntrySnapshot
    {
        public int EmployerId { get; set; }
        public byte Flags { get; set; }
        public int TemplateId { get; set; }
        public short WorldX { get; set; }
        public short WorldY { get; set; }
        public short FootholdId { get; set; }
        public string NameTag { get; set; }
        public byte MiniRoomType { get; set; }
        public int MiniRoomSerial { get; set; }
        public string BalloonTitle { get; set; }
        public byte BalloonByte0 { get; set; }
        public byte BalloonByte1 { get; set; }
        public byte BalloonByte2 { get; set; }
    }
}
