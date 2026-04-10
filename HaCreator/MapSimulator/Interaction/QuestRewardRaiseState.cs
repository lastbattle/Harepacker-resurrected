using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum QuestRewardRaiseSourceKind
    {
        QuestWindow,
        NpcOverlay
    }

    internal sealed class QuestRewardRaiseState
    {
        public QuestRewardRaiseSourceKind Source { get; init; }
        public QuestRewardChoicePrompt Prompt { get; init; }
        public int GroupIndex { get; set; }
        public int ManagerSessionId { get; set; }
        public int RequestId { get; set; }
        public int OwnerItemId { get; set; }
        public int QrData { get; set; }
        public int MaxDropCount { get; set; } = 1;
        public Point WindowPosition { get; set; }
        public QuestRewardRaiseWindowMode WindowMode { get; set; }
        public QuestRewardRaiseWindowMode DisplayMode { get; set; }
        public string OpenDispatchSummary { get; set; } = string.Empty;
        public string LastInboundSummary { get; set; } = string.Empty;
        public bool AwaitingConfirmAck { get; set; }
        public bool AwaitingOwnerDestroyAck { get; set; }
        public Dictionary<int, int> SelectedItemsByGroup { get; } = new Dictionary<int, int>();
        public List<QuestRewardRaisePlacedPiece> PlacedPieces { get; } = new List<QuestRewardRaisePlacedPiece>();
    }

    internal enum QuestRewardRaisePieceLifecycleState
    {
        PendingAddAck,
        Active,
        PendingReleaseAck,
        PendingConfirmAck,
        Confirmed
    }

    internal sealed class QuestRewardRaisePlacedPiece
    {
        public int RequestId { get; init; }
        public InventoryType InventoryType { get; init; }
        public int SlotIndex { get; init; }
        public int ItemId { get; init; }
        public int Quantity { get; init; } = 1;
        public string Label { get; init; } = string.Empty;
        public int PacketOpcode { get; set; }
        public byte[] PacketPayload { get; set; } = Array.Empty<byte>();
        public string DispatchSummary { get; set; } = string.Empty;
        public int LastInboundPacketType { get; set; } = -1;
        public byte[] LastInboundPayload { get; set; } = Array.Empty<byte>();
        public string LastInboundSummary { get; set; } = string.Empty;
        public QuestRewardRaisePieceLifecycleState LifecycleState { get; set; } = QuestRewardRaisePieceLifecycleState.PendingAddAck;
    }
}
