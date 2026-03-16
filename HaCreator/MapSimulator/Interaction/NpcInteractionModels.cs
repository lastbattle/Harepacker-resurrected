using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum NpcInteractionEntryKind
    {
        Talk,
        Storage,
        Utility,
        LockedQuest,
        AvailableQuest,
        InProgressQuest,
        CompletableQuest
    }

    internal enum NpcInteractionActionKind
    {
        None,
        QuestPrimary,
        OpenTrunk,
        OpenItemMaker
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
        public NpcInteractionActionKind PrimaryActionKind { get; init; }
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

    internal readonly struct NpcInlineSelection
    {
        public NpcInlineSelection(int selectionId, string label)
        {
            SelectionId = selectionId;
            Label = label ?? string.Empty;
        }

        public int SelectionId { get; }
        public string Label { get; }
    }

    internal sealed class NpcInteractionState
    {
        public string NpcName { get; init; } = "NPC";
        public IReadOnlyList<NpcInteractionEntry> Entries { get; init; } = Array.Empty<NpcInteractionEntry>();
        public int SelectedEntryId { get; init; }
    }

    internal readonly struct NpcInteractionOverlayResult
    {
        public NpcInteractionOverlayResult(bool consumed, NpcInteractionEntry primaryActionEntry)
        {
            Consumed = consumed;
            PrimaryActionEntry = primaryActionEntry;
        }

        public bool Consumed { get; }
        public NpcInteractionEntry PrimaryActionEntry { get; }
    }

    internal sealed class QuestActionResult
    {
        public bool StateChanged { get; init; }
        public int? PreferredQuestId { get; init; }
        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    }
}
