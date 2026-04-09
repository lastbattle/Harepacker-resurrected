namespace HaCreator.MapSimulator.Interaction
{
    using System.Collections.Generic;

    internal enum PacketQuestResultNoticeDispatchStage
    {
        Immediate = 0,
        AfterDialog = 1
    }

    internal enum PacketQuestResultNoticeSurface
    {
        Chat = 0,
        UtilDialogNotice = 1
    }

    internal readonly record struct PacketQuestResultNoticeRouting(
        PacketQuestResultNoticeSurface Surface,
        PacketQuestResultNoticeDispatchStage Stage);

    internal static class PacketQuestResultClientSemantics
    {
        internal const int FirstHandledSubtype = 6;
        internal const int LastHandledSubtype = 18;

        internal static bool IsHandledSubtype(int resultType)
        {
            return resultType >= FirstHandledSubtype && resultType <= LastHandledSubtype;
        }

        internal static PacketQuestResultNoticeDispatchStage ResolveSubtype10NoticeDispatchStage(bool openedModal)
        {
            return openedModal
                ? PacketQuestResultNoticeDispatchStage.AfterDialog
                : PacketQuestResultNoticeDispatchStage.Immediate;
        }

        internal static PacketQuestResultNoticeRouting ResolveNoticeRouting(int resultType, bool openedModal)
        {
            return resultType switch
            {
                10 => new PacketQuestResultNoticeRouting(
                    PacketQuestResultNoticeSurface.UtilDialogNotice,
                    ResolveSubtype10NoticeDispatchStage(openedModal)),
                12 => new PacketQuestResultNoticeRouting(
                    PacketQuestResultNoticeSurface.UtilDialogNotice,
                    PacketQuestResultNoticeDispatchStage.Immediate),
                _ => new PacketQuestResultNoticeRouting(
                    PacketQuestResultNoticeSurface.Chat,
                    PacketQuestResultNoticeDispatchStage.Immediate)
            };
        }

        internal static IReadOnlyList<int> GetNewlyAvailableQuestIds(
            IEnumerable<int> previousAvailableQuestIds,
            IEnumerable<int> currentAvailableQuestIds)
        {
            var previous = previousAvailableQuestIds != null
                ? new HashSet<int>(previousAvailableQuestIds)
                : new HashSet<int>();
            var current = new List<int>();
            var seenCurrent = new HashSet<int>();

            if (currentAvailableQuestIds == null)
            {
                return current;
            }

            foreach (int questId in currentAvailableQuestIds)
            {
                if (questId <= 0 || !seenCurrent.Add(questId) || previous.Contains(questId))
                {
                    continue;
                }

                current.Add(questId);
            }

            return current;
        }
    }
}
