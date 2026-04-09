using System;
using System.Collections.Generic;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum QuestWindowActionKind
    {
        None,
        Accept,
        Complete,
        GiveUp,
        Track,
        LocateNpc,
        LocateMob,
        QuestDeliveryAccept,
        QuestDeliveryComplete
    }

    internal enum QuestDetailNpcButtonStyle
    {
        None,
        GenericNpc,
        GotoNpc,
        MarkNpc
    }

    internal enum QuestDetailDeliveryType
    {
        None,
        Accept,
        Complete
    }

    internal static class QuestDetailDeliveryTypeCodec
    {
        // `CUIQuestInfo::LoadData` writes the client `QuestInfo::nDeliveryType` as 0=accept, 1=complete, 2=none.
        public static QuestDetailDeliveryType FromClientRawValue(int rawType)
        {
            return rawType switch
            {
                0 => QuestDetailDeliveryType.Accept,
                1 => QuestDetailDeliveryType.Complete,
                2 => QuestDetailDeliveryType.None,
                _ => QuestDetailDeliveryType.None
            };
        }

        public static bool TryParseToken(string token, out QuestDetailDeliveryType deliveryType)
        {
            switch (token?.Trim().ToLowerInvariant())
            {
                case "accept":
                    deliveryType = QuestDetailDeliveryType.Accept;
                    return true;
                case "complete":
                    deliveryType = QuestDetailDeliveryType.Complete;
                    return true;
                case "none":
                    deliveryType = QuestDetailDeliveryType.None;
                    return true;
                case "0":
                    deliveryType = QuestDetailDeliveryType.Accept;
                    return true;
                case "1":
                    deliveryType = QuestDetailDeliveryType.Complete;
                    return true;
                case "2":
                    deliveryType = QuestDetailDeliveryType.None;
                    return true;
                default:
                    deliveryType = QuestDetailDeliveryType.None;
                    return false;
            }
        }
    }

    internal enum QuestWorldMapTargetKind
    {
        None,
        Npc,
        Mob,
        Item
    }

    internal sealed class QuestWindowListEntry
    {
        public int QuestId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public QuestStateType State { get; init; }
        public int CurrentProgress { get; init; }
        public int TotalProgress { get; init; }
    }

    internal sealed class QuestWindowDetailState
    {
        public const int MateNameHeaderQuestId = 4451;

        public int QuestId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string HeaderNoteText { get; init; } = string.Empty;
        public QuestStateType State { get; init; }
        public string SummaryText { get; init; } = string.Empty;
        public string RequirementText { get; init; } = string.Empty;
        public string RewardText { get; init; } = string.Empty;
        public string HintText { get; init; } = string.Empty;
        public string NpcText { get; init; } = string.Empty;
        public IReadOnlyList<QuestLogLineSnapshot> RequirementLines { get; init; } = Array.Empty<QuestLogLineSnapshot>();
        public IReadOnlyList<QuestLogLineSnapshot> RewardLines { get; init; } = Array.Empty<QuestLogLineSnapshot>();
        public int CurrentProgress { get; init; }
        public int TotalProgress { get; init; }
        public QuestWindowActionKind PrimaryAction { get; init; }
        public bool PrimaryActionEnabled { get; init; }
        public bool PrimaryActionSelected { get; init; }
        public string PrimaryActionLabel { get; init; } = string.Empty;
        public QuestWindowActionKind SecondaryAction { get; init; }
        public bool SecondaryActionEnabled { get; init; }
        public string SecondaryActionLabel { get; init; } = string.Empty;
        public QuestWindowActionKind TertiaryAction { get; init; }
        public bool TertiaryActionEnabled { get; init; }
        public string TertiaryActionLabel { get; init; } = string.Empty;
        public QuestWindowActionKind QuaternaryAction { get; init; }
        public bool QuaternaryActionEnabled { get; init; }
        public string QuaternaryActionLabel { get; init; } = string.Empty;
        public int? TargetNpcId { get; init; }
        public string TargetNpcName { get; init; } = string.Empty;
        public int? TargetMobId { get; init; }
        public string TargetMobName { get; init; } = string.Empty;
        public int? TargetItemId { get; init; }
        public string TargetItemName { get; init; } = string.Empty;
        public bool HasDetailInset { get; init; }
        public int TimeLimitSeconds { get; init; }
        public int RemainingTimeSeconds { get; init; }
        public string TimerUiKey { get; init; } = string.Empty;
        public QuestDetailDeliveryType DeliveryType { get; init; }
        public bool DeliveryActionEnabled { get; init; }
        public int? DeliveryCashItemId { get; init; }
        public string DeliveryCashItemName { get; init; } = string.Empty;
        public QuestDetailNpcButtonStyle NpcButtonStyle { get; set; }
    }

    internal sealed class QuestWindowActionResult
    {
        public bool StateChanged { get; init; }
        public int? QuestId { get; init; }
        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> PublishedScriptNames { get; init; } = Array.Empty<string>();
        public QuestRewardChoicePrompt PendingRewardChoicePrompt { get; init; }
    }

    internal sealed class QuestRewardChoicePrompt
    {
        public int QuestId { get; init; }
        public string QuestName { get; init; } = string.Empty;
        public bool CompletionPhase { get; init; }
        public string ActionLabel { get; init; } = string.Empty;
        public int? NpcId { get; init; }
        public QuestRewardRaiseOwnerContext OwnerContext { get; init; }
        public IReadOnlyList<QuestRewardChoiceGroup> Groups { get; init; } = Array.Empty<QuestRewardChoiceGroup>();
    }

    internal sealed class QuestRewardChoiceGroup
    {
        public int GroupKey { get; init; }
        public string PromptText { get; init; } = string.Empty;
        public IReadOnlyList<QuestRewardChoiceOption> Options { get; init; } = Array.Empty<QuestRewardChoiceOption>();
    }

    internal sealed class QuestRewardChoiceOption
    {
        public int ItemId { get; init; }
        public string Label { get; init; } = string.Empty;
        public string DetailText { get; init; } = string.Empty;
        public InventoryType InventoryType { get; init; } = InventoryType.NONE;
    }

    internal enum QuestRewardRaiseWindowMode
    {
        Selection,
        PiecePlacement
    }

    internal sealed class QuestRewardRaiseOwnerContext
    {
        public int OwnerItemId { get; init; }
        public QuestRewardRaiseWindowMode WindowMode { get; init; }
        public int MaxDropCount { get; init; } = 1;
        public int InitialQrData { get; init; }
    }

    internal sealed class QuestWorldMapTarget
    {
        public QuestWorldMapTargetKind Kind { get; init; }
        public int QuestId { get; init; }
        public int MapId { get; init; }
        public IReadOnlyList<int> MapIds { get; init; } = Array.Empty<int>();
        public int? EntityId { get; init; }
        public string Label { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string FallbackNpcName { get; init; } = string.Empty;
    }

    internal sealed class QuestDemandItemQueryState
    {
        public int QuestId { get; init; }
        public IReadOnlyList<int> VisibleItemIds { get; init; } = Array.Empty<int>();
        public IReadOnlyDictionary<int, IReadOnlyList<int>> VisibleItemMapIds { get; init; } =
            new Dictionary<int, IReadOnlyList<int>>();
        public int HiddenItemCount { get; init; }
        public string FallbackNpcName { get; init; } = string.Empty;
        public bool HasPacketOwnedMapResults { get; init; }
    }

    internal sealed class QuestDeliveryEntrySnapshot
    {
        public int QuestId { get; init; }
        public int DisplayQuestId { get; init; }
        public int TargetNpcId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string NpcName { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string DetailText { get; init; } = string.Empty;
        public bool CompletionPhase { get; init; }
        public bool CanConfirm { get; init; }
        public bool IsBlocked { get; init; }
        public bool IsSeriesRepresentative { get; init; }
        public int? DeliveryCashItemId { get; init; }
        public string DeliveryCashItemName { get; init; } = string.Empty;
        public int DeliveryCashItemRuntimeSlotIndex { get; init; } = -1;
        public int DeliveryCashItemClientSlotIndex { get; init; }
    }

    internal sealed class QuestAlarmEntrySnapshot
    {
        public int QuestId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public long UpdateSequence { get; init; }
        public int CurrentProgress { get; init; }
        public int TotalProgress { get; init; }
        public float ProgressRatio { get; init; }
        public bool IsReadyToComplete { get; init; }
        public bool IsRecentlyUpdated { get; init; }
        public IReadOnlyList<QuestLogLineSnapshot> RequirementLines { get; init; } = Array.Empty<QuestLogLineSnapshot>();
        public IReadOnlyList<string> IssueLines { get; init; } = Array.Empty<string>();
        public string DemandText { get; init; } = string.Empty;
    }

    internal sealed class QuestAlarmSnapshot
    {
        public IReadOnlyList<QuestAlarmEntrySnapshot> Entries { get; init; } = Array.Empty<QuestAlarmEntrySnapshot>();
        public bool HasAlertAnimation { get; init; }
    }
}
