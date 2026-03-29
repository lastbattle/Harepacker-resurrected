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
        OpenItemMaker,
        OpenItemUpgrade
    }

    internal enum NpcInteractionPresentationStyle
    {
        Default,
        PacketScriptUtilDialog
    }

    internal enum NpcInteractionInputKind
    {
        None,
        Text,
        Number,
        MultiLineText
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
        public string RawText { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public IReadOnlyList<NpcInteractionChoice> Choices { get; init; } = Array.Empty<NpcInteractionChoice>();
        public NpcInteractionInputRequest InputRequest { get; init; }
    }

    internal sealed class NpcInteractionChoice
    {
        public string Label { get; init; } = string.Empty;
        public IReadOnlyList<NpcInteractionPage> Pages { get; init; } = Array.Empty<NpcInteractionPage>();
        public bool SubmitSelection { get; init; }
        public NpcInteractionInputKind SubmissionKind { get; init; }
        public string SubmissionValue { get; init; } = string.Empty;
        public int? SubmissionNumericValue { get; init; }
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
        public NpcInteractionPresentationStyle PresentationStyle { get; init; }
    }

    internal readonly struct NpcInteractionOverlayResult
    {
        public NpcInteractionOverlayResult(bool consumed, NpcInteractionEntry primaryActionEntry, NpcInteractionInputSubmission inputSubmission = null)
        {
            Consumed = consumed;
            PrimaryActionEntry = primaryActionEntry;
            InputSubmission = inputSubmission;
        }

        public bool Consumed { get; }
        public NpcInteractionEntry PrimaryActionEntry { get; }
        public NpcInteractionInputSubmission InputSubmission { get; }
    }

    internal sealed class NpcInteractionInputRequest
    {
        public NpcInteractionInputKind Kind { get; init; }
        public string DefaultValue { get; init; } = string.Empty;
        public int MinLength { get; init; }
        public int MaxLength { get; init; } = int.MaxValue;
        public int MinValue { get; init; } = int.MinValue;
        public int MaxValue { get; init; } = int.MaxValue;
        public int ColumnCount { get; init; }
        public int LineCount { get; init; } = 1;
    }

    internal sealed class NpcInteractionInputSubmission
    {
        public int EntryId { get; init; }
        public string EntryTitle { get; init; } = string.Empty;
        public string NpcName { get; init; } = "NPC";
        public NpcInteractionPresentationStyle PresentationStyle { get; init; }
        public NpcInteractionInputKind Kind { get; init; }
        public string Value { get; init; } = string.Empty;
        public int? NumericValue { get; init; }
    }

    internal sealed class QuestActionResult
    {
        public bool StateChanged { get; init; }
        public int? PreferredQuestId { get; init; }
        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> PublishedScriptNames { get; init; } = Array.Empty<string>();
        public QuestRewardChoicePrompt PendingRewardChoicePrompt { get; init; }
    }
}
