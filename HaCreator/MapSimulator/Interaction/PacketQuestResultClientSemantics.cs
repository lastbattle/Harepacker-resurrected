namespace HaCreator.MapSimulator.Interaction
{
    using System.Collections.Generic;

    internal static class PacketQuestResultClientSemantics
    {
        internal const int FirstHandledSubtype = 6;
        internal const int LastHandledSubtype = 18;

        internal static bool IsHandledSubtype(int resultType)
        {
            return resultType >= FirstHandledSubtype && resultType <= LastHandledSubtype;
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
