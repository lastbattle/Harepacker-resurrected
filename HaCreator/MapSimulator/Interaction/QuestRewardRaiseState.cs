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
        public int ManagerSessionId { get; init; }
        public int RequestId { get; init; }
        public int OwnerItemId { get; init; }
        public int QrData { get; set; }
        public int MaxDropCount { get; init; } = 1;
        public Point WindowPosition { get; set; }
        public QuestRewardRaiseWindowMode WindowMode { get; init; }
        public QuestRewardRaiseWindowMode DisplayMode { get; set; }
        public Dictionary<int, int> SelectedItemsByGroup { get; } = new Dictionary<int, int>();
        public List<QuestRewardRaisePlacedPiece> PlacedPieces { get; } = new List<QuestRewardRaisePlacedPiece>();
    }

    internal sealed class QuestRewardRaisePlacedPiece
    {
        public int RequestId { get; init; }
        public InventoryType InventoryType { get; init; }
        public int SlotIndex { get; init; }
        public int ItemId { get; init; }
        public int Quantity { get; init; } = 1;
        public string Label { get; init; } = string.Empty;
    }
}
