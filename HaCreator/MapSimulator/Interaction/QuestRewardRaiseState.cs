using System;
using System.Collections.Generic;
using System.Linq;
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
        public QuestRewardRaiseSourceKind Source { get; set; }
        public QuestRewardChoicePrompt Prompt { get; set; }
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
        public bool IsWindowDismissedLocally { get; set; }
        public Dictionary<int, int> SelectedItemsByGroup { get; } = new Dictionary<int, int>();
        public List<QuestRewardRaisePlacedPiece> PlacedPieces { get; } = new List<QuestRewardRaisePlacedPiece>();

        public QuestRewardRaiseState CloneShallow()
        {
            QuestRewardRaiseState clone = new()
            {
                Source = Source,
                Prompt = Prompt,
                GroupIndex = GroupIndex,
                ManagerSessionId = ManagerSessionId,
                RequestId = RequestId,
                OwnerItemId = OwnerItemId,
                QrData = QrData,
                MaxDropCount = MaxDropCount,
                WindowPosition = WindowPosition,
                WindowMode = WindowMode,
                DisplayMode = DisplayMode,
                OpenDispatchSummary = OpenDispatchSummary,
                LastInboundSummary = LastInboundSummary,
                AwaitingConfirmAck = AwaitingConfirmAck,
                AwaitingOwnerDestroyAck = AwaitingOwnerDestroyAck,
                IsWindowDismissedLocally = IsWindowDismissedLocally
            };

            foreach (KeyValuePair<int, int> selectedItem in SelectedItemsByGroup)
            {
                clone.SelectedItemsByGroup[selectedItem.Key] = selectedItem.Value;
            }

            foreach (QuestRewardRaisePlacedPiece piece in PlacedPieces)
            {
                clone.PlacedPieces.Add(piece.Clone());
            }

            return clone;
        }
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

        public QuestRewardRaisePlacedPiece Clone()
        {
            return new QuestRewardRaisePlacedPiece
            {
                RequestId = RequestId,
                InventoryType = InventoryType,
                SlotIndex = SlotIndex,
                ItemId = ItemId,
                Quantity = Quantity,
                Label = Label,
                PacketOpcode = PacketOpcode,
                PacketPayload = PacketPayload?.ToArray() ?? Array.Empty<byte>(),
                DispatchSummary = DispatchSummary,
                LastInboundPacketType = LastInboundPacketType,
                LastInboundPayload = LastInboundPayload?.ToArray() ?? Array.Empty<byte>(),
                LastInboundSummary = LastInboundSummary,
                LifecycleState = LifecycleState
            };
        }
    }
}
