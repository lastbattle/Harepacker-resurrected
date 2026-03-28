using System;
using System.Collections.Generic;
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
        public int QuestId { get; init; }
        public string Title { get; init; } = string.Empty;
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
        public int? DeliveryCashItemId { get; init; }
        public string DeliveryCashItemName { get; init; } = string.Empty;
        public QuestDetailNpcButtonStyle NpcButtonStyle { get; set; }
    }

    internal sealed class QuestWindowActionResult
    {
        public bool StateChanged { get; init; }
        public int? QuestId { get; init; }
        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    }

    internal sealed class QuestWorldMapTarget
    {
        public QuestWorldMapTargetKind Kind { get; init; }
        public int QuestId { get; init; }
        public int MapId { get; init; }
        public int? EntityId { get; init; }
        public string Label { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string FallbackNpcName { get; init; } = string.Empty;
    }

    internal sealed class QuestAlarmEntrySnapshot
    {
        public int QuestId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
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
