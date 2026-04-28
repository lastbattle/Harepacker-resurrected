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
        NpcOverlay,
        InventoryItem
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
        public string UiData { get; set; } = string.Empty;
        public int IncrementExpUnit { get; set; }
        public int Grade { get; set; }
        public Point WindowPosition { get; set; }
        public QuestRewardRaiseWindowMode WindowMode { get; set; }
        public QuestRewardRaiseWindowMode DisplayMode { get; set; }
        public QuestRewardRaiseClientWindowKind ClientWindowKind { get; set; }
        public string OpenDispatchSummary { get; set; } = string.Empty;
        public string LastInboundSummary { get; set; } = string.Empty;
        public bool AwaitingConfirmAck { get; set; }
        public bool AwaitingOwnerDestroyAck { get; set; }
        public bool IsWindowDismissedLocally { get; set; }
        public bool ReusedOwnerIdentityOnOpen { get; set; }
        public Dictionary<int, int> SelectedItemsByGroup { get; } = new Dictionary<int, int>();
        public List<QuestRewardRaisePlacedPiece> PlacedPieces { get; } = new List<QuestRewardRaisePlacedPiece>();

        internal bool HasEnabledDropItemList => EnumerateEnabledDropItemIds(Prompt).Any();

        internal bool CanDropItem(int itemId, out int enabledDropItemIndex)
        {
            enabledDropItemIndex = GetEnableDropItemIndex(itemId);
            if (enabledDropItemIndex < 0)
            {
                return !HasEnabledDropItemList;
            }

            int enabledItemId = EnumerateEnabledDropItemIds(Prompt).ElementAt(enabledDropItemIndex);
            return !PlacedPieces.Any(piece => piece.ItemId == enabledItemId);
        }

        internal int GetEnableDropItemIndex(int itemId)
        {
            if (itemId <= 0)
            {
                return -1;
            }

            int index = 0;
            foreach (int enabledItemId in EnumerateEnabledDropItemIds(Prompt))
            {
                if (enabledItemId == itemId)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        internal static int CountEnabledDropItems(QuestRewardChoicePrompt prompt)
        {
            return EnumerateEnabledDropItemIds(prompt).Count();
        }

        private static IEnumerable<int> EnumerateEnabledDropItemIds(QuestRewardChoicePrompt prompt)
        {
            if (prompt?.Groups == null)
            {
                yield break;
            }

            foreach (QuestRewardChoiceGroup group in prompt.Groups)
            {
                if (group?.Options == null)
                {
                    continue;
                }

                foreach (QuestRewardChoiceOption option in group.Options)
                {
                    if (option?.ItemId > 0)
                    {
                        yield return option.ItemId;
                    }
                }
            }
        }

        internal void SyncSelectionProgressFromPayload(QuestRewardRaisePacketPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            bool preserveExistingSelectionProgress =
                payload.SelectedItemsByGroup?.Count == 0
                && payload.DisplayMode == QuestRewardRaiseWindowMode.PiecePlacement
                && payload.WindowMode == QuestRewardRaiseWindowMode.PiecePlacement
                && SelectedItemsByGroup.Count > 0;
            if (preserveExistingSelectionProgress)
            {
                return;
            }

            ReplaceSelectionProgress(payload.SelectedItemsByGroup);
        }

        internal void SyncSelectionProgress(IReadOnlyDictionary<int, int> selectedItemsByGroup)
        {
            if (selectedItemsByGroup == null || selectedItemsByGroup.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<int, int> selectedItem in selectedItemsByGroup)
            {
                if (selectedItem.Key <= 0 || selectedItem.Value <= 0)
                {
                    continue;
                }

                SelectedItemsByGroup[selectedItem.Key] = selectedItem.Value;
            }

            if (DisplayMode == QuestRewardRaiseWindowMode.PiecePlacement
                || Prompt?.Groups == null
                || Prompt.Groups.Count == 0)
            {
                return;
            }

            GroupIndex = ResolveSelectionProgressGroupIndex(Prompt, SelectedItemsByGroup);
        }

        internal void ReplaceSelectionProgress(IReadOnlyDictionary<int, int> selectedItemsByGroup)
        {
            if (selectedItemsByGroup == null)
            {
                return;
            }

            SelectedItemsByGroup.Clear();
            foreach (KeyValuePair<int, int> selectedItem in selectedItemsByGroup)
            {
                if (selectedItem.Key <= 0 || selectedItem.Value <= 0)
                {
                    continue;
                }

                SelectedItemsByGroup[selectedItem.Key] = selectedItem.Value;
            }

            if (DisplayMode == QuestRewardRaiseWindowMode.PiecePlacement
                || Prompt?.Groups == null
                || Prompt.Groups.Count == 0)
            {
                return;
            }

            GroupIndex = ResolveSelectionProgressGroupIndex(Prompt, SelectedItemsByGroup);
        }

        internal static int ResolveSelectionProgressGroupIndex(
            QuestRewardChoicePrompt prompt,
            IReadOnlyDictionary<int, int> selectedItemsByGroup)
        {
            if (prompt?.Groups == null || prompt.Groups.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < prompt.Groups.Count; i++)
            {
                QuestRewardChoiceGroup group = prompt.Groups[i];
                if (group == null || group.GroupKey <= 0)
                {
                    return i;
                }

                if (!selectedItemsByGroup.TryGetValue(group.GroupKey, out int selectedItemId) || selectedItemId <= 0)
                {
                    return i;
                }

                if (group.Options != null
                    && group.Options.Count > 0
                    && !group.Options.Any(option => option?.ItemId == selectedItemId))
                {
                    return i;
                }
            }

            return prompt.Groups.Count;
        }

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
                UiData = UiData,
                IncrementExpUnit = IncrementExpUnit,
                Grade = Grade,
                WindowPosition = WindowPosition,
                WindowMode = WindowMode,
                DisplayMode = DisplayMode,
                ClientWindowKind = ClientWindowKind,
                OpenDispatchSummary = OpenDispatchSummary,
                LastInboundSummary = LastInboundSummary,
                AwaitingConfirmAck = AwaitingConfirmAck,
                AwaitingOwnerDestroyAck = AwaitingOwnerDestroyAck,
                IsWindowDismissedLocally = IsWindowDismissedLocally,
                ReusedOwnerIdentityOnOpen = ReusedOwnerIdentityOnOpen
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

    internal static class QuestRewardRaisePieceLifecycleStateResolver
    {
        internal static QuestRewardRaisePieceLifecycleState ResolveConfirmResultLifecycle(
            QuestRewardRaisePieceLifecycleState currentState,
            bool success)
        {
            if (success)
            {
                return currentState == QuestRewardRaisePieceLifecycleState.PendingReleaseAck
                    ? QuestRewardRaisePieceLifecycleState.PendingReleaseAck
                    : QuestRewardRaisePieceLifecycleState.Confirmed;
            }

            return currentState switch
            {
                QuestRewardRaisePieceLifecycleState.PendingReleaseAck => QuestRewardRaisePieceLifecycleState.PendingReleaseAck,
                QuestRewardRaisePieceLifecycleState.Confirmed => QuestRewardRaisePieceLifecycleState.Confirmed,
                _ => QuestRewardRaisePieceLifecycleState.Active
            };
        }
    }

    internal sealed class QuestRewardRaisePlacedPiece
    {
        public int RequestId { get; set; }
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
