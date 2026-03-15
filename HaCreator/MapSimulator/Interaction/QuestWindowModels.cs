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
        Track
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
        public int CurrentProgress { get; init; }
        public int TotalProgress { get; init; }
        public QuestWindowActionKind PrimaryAction { get; init; }
        public bool PrimaryActionEnabled { get; init; }
        public string PrimaryActionLabel { get; init; } = string.Empty;
        public QuestWindowActionKind SecondaryAction { get; init; }
        public bool SecondaryActionEnabled { get; init; }
        public string SecondaryActionLabel { get; init; } = string.Empty;
    }

    internal sealed class QuestWindowActionResult
    {
        public bool StateChanged { get; init; }
        public int? QuestId { get; init; }
        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    }
}
