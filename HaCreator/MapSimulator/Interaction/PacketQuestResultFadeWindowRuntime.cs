using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal readonly record struct PacketQuestResultFadeWindowDeleteRequest(
        int Type,
        int FriendId,
        int QuestId,
        string Sender,
        uint NewYearCardSerialNumber);

    internal sealed class PacketQuestResultFadeWindowRuntime
    {
        internal const int QuestResultFadeWindowType = 7;
        private readonly HashSet<int> _activeQuestIds = new();

        internal PacketQuestResultFadeWindowDeleteRequest LastDeleteRequest { get; private set; }

        internal void RegisterQuestFadeWindow(int questId)
        {
            if (questId <= 0)
            {
                return;
            }

            _activeQuestIds.Add(questId);
        }

        internal bool ApplyQuestResultDeleteFadeWindow(int questId)
        {
            LastDeleteRequest = new PacketQuestResultFadeWindowDeleteRequest(
                QuestResultFadeWindowType,
                FriendId: 0,
                QuestId: questId,
                Sender: string.Empty,
                NewYearCardSerialNumber: 0);

            if (questId <= 0)
            {
                return false;
            }

            return _activeQuestIds.Remove(questId);
        }
    }
}
