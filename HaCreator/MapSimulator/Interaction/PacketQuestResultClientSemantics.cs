namespace HaCreator.MapSimulator.Interaction
{
    using System;
    using System.Collections.Generic;

    internal enum PacketQuestResultSubtype10ContinuationDisposition
    {
        Abandon = 0,
        Continue = 1
    }

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
        PacketQuestResultNoticeDispatchStage Stage,
        bool AutoSeparated);

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
                    ResolveSubtype10NoticeDispatchStage(openedModal),
                    AutoSeparated: false),
                12 => new PacketQuestResultNoticeRouting(
                    PacketQuestResultNoticeSurface.UtilDialogNotice,
                    PacketQuestResultNoticeDispatchStage.Immediate,
                    AutoSeparated: false),
                _ => new PacketQuestResultNoticeRouting(
                    PacketQuestResultNoticeSurface.Chat,
                    PacketQuestResultNoticeDispatchStage.Immediate,
                    AutoSeparated: true)
            };
        }

        internal static bool ResolveUtilDialogNoticeAutoSeparated(int resultType)
        {
            return resultType switch
            {
                10 or 11 or 12 or 13 or 15 or 16 => false,
                _ => true
            };
        }

        internal static PacketQuestResultSubtype10ContinuationDisposition ResolveSubtype10ContinuationDisposition(
            NpcInteractionOverlayCloseKind closeKind)
        {
            return closeKind == NpcInteractionOverlayCloseKind.Completed
                ? PacketQuestResultSubtype10ContinuationDisposition.Continue
                : PacketQuestResultSubtype10ContinuationDisposition.Abandon;
        }

        internal static bool TryDecodeSubtype10TrailingFollowUpQuestId(
            ReadOnlySpan<byte> payload,
            out int followUpQuestId,
            out string error)
        {
            followUpQuestId = 0;
            if (payload.Length != sizeof(ushort))
            {
                error = "Quest-result subtype 10 trailing follow-up quest id must be exactly 2 bytes.";
                return false;
            }

            followUpQuestId = BitConverter.ToUInt16(payload);
            error = string.Empty;
            return true;
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
