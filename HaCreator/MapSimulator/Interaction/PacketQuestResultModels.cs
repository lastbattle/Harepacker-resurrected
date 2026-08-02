using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum PacketQuestResultTextKind : byte
    {
        Auto = 0,
        StartDescription = 1,
        ProgressDescription = 2,
        CompletionDescription = 3,
        Summary = 4,
        DemandSummary = 5,
        RewardSummary = 6
    }

    internal sealed class PacketQuestResultPresentation
    {
        public int QuestId { get; init; }
        public string QuestName { get; init; } = string.Empty;
        public string NoticeText { get; init; } = string.Empty;
        public IReadOnlyList<NpcInteractionPage> ModalPages { get; init; } = Array.Empty<NpcInteractionPage>();
    }

    internal static class PacketQuestResultCloseBehavior
    {
        internal static bool ShouldStartFollowUpQuest(NpcInteractionOverlayCloseKind closeKind)
        {
            return closeKind != NpcInteractionOverlayCloseKind.None;
        }
    }
}
