using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum NpcInteractionEntryKind
    {
        Talk,
        LockedQuest,
        AvailableQuest,
        InProgressQuest,
        CompletableQuest
    }

    internal sealed class NpcInteractionEntry
    {
        public int EntryId { get; init; }
        public int? QuestId { get; init; }
        public NpcInteractionEntryKind Kind { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public IReadOnlyList<NpcInteractionPage> Pages { get; init; } = Array.Empty<NpcInteractionPage>();
        public string PrimaryActionLabel { get; init; } = string.Empty;
        public bool PrimaryActionEnabled { get; init; }
    }

    internal sealed class NpcInteractionPage
    {
        public string Text { get; init; } = string.Empty;
        public IReadOnlyList<NpcInteractionChoice> Choices { get; init; } = Array.Empty<NpcInteractionChoice>();
    }

    internal sealed class NpcInteractionChoice
    {
        public string Label { get; init; } = string.Empty;
        public IReadOnlyList<NpcInteractionPage> Pages { get; init; } = Array.Empty<NpcInteractionPage>();
    }

    internal sealed class NpcInteractionState
    {
        public string NpcName { get; init; } = "NPC";
        public IReadOnlyList<NpcInteractionEntry> Entries { get; init; } = Array.Empty<NpcInteractionEntry>();
        public int SelectedEntryId { get; init; }
    }

    internal readonly struct NpcInteractionOverlayResult
    {
        public NpcInteractionOverlayResult(bool consumed, bool primaryActionRequested)
        {
            Consumed = consumed;
            PrimaryActionRequested = primaryActionRequested;
        }

        public bool Consumed { get; }
        public bool PrimaryActionRequested { get; }
    }

    internal sealed class QuestActionResult
    {
        public bool StateChanged { get; init; }
        public int? PreferredQuestId { get; init; }
        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    }
}
