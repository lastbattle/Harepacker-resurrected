using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    public sealed class SocialRoomRuntimeSnapshot
    {
        public SocialRoomKind Kind { get; set; }
        public string RoomTitle { get; set; }
        public string OwnerName { get; set; }
        public int MesoAmount { get; set; }
        public string StatusMessage { get; set; }
        public string RoomState { get; set; }
        public string ModeName { get; set; }
        public int MiniRoomModeIndex { get; set; }
        public int MiniRoomWagerAmount { get; set; }
        public int TradeLocalOfferMeso { get; set; }
        public int TradeRemoteOfferMeso { get; set; }
        public bool TradeLocalLocked { get; set; }
        public bool TradeRemoteLocked { get; set; }
        public bool TradeLocalAccepted { get; set; }
        public bool TradeRemoteAccepted { get; set; }
        public DateTime? EntrustedPermitExpiresAtUtc { get; set; }
        public List<SocialRoomOccupantSnapshot> Occupants { get; set; } = new();
        public List<SocialRoomItemSnapshot> Items { get; set; } = new();
        public List<string> Notes { get; set; } = new();
        public List<SocialRoomChatEntrySnapshot> ChatEntries { get; set; } = new();
        public List<string> SavedVisitors { get; set; } = new();
        public List<string> BlockedVisitors { get; set; } = new();
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
        public int Quantity { get; set; }
        public int MesoAmount { get; set; }
        public string Detail { get; set; }
        public bool IsLocked { get; set; }
        public bool IsClaimed { get; set; }
    }

    public sealed class SocialRoomChatEntrySnapshot
    {
        public string Text { get; set; }
        public SocialRoomChatTone Tone { get; set; }
    }
}
