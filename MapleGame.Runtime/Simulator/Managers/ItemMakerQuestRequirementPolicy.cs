using MapleLib.WzLib.WzStructure.Data.QuestStructure;

namespace HaCreator.MapSimulator.Managers
{
    internal static class ItemMakerQuestRequirementPolicy
    {
        public static bool MatchesClientQuestRequirement(QuestStateType currentState, int requiredStateValue)
        {
            return requiredStateValue switch
            {
                (int)QuestStateType.Not_Started => currentState == QuestStateType.Not_Started,
                (int)QuestStateType.Started => currentState == QuestStateType.Started,
                (int)QuestStateType.Completed => currentState == QuestStateType.Completed,
                // `CUIItemMaker::DoesSatisfyPreCondition` treats reqQuest=3 as a special
                // "started or completed" gate instead of broadening every value >= 3.
                (int)QuestStateType.PartyQuest => currentState == QuestStateType.Started || currentState == QuestStateType.Completed,
                _ => false
            };
        }

        public static string DescribeRequirementState(int requiredStateValue)
        {
            return requiredStateValue switch
            {
                (int)QuestStateType.Not_Started => "not started",
                (int)QuestStateType.Started => "started",
                (int)QuestStateType.Completed => "completed",
                (int)QuestStateType.PartyQuest => "started or completed",
                _ => $"state {requiredStateValue}"
            };
        }
    }
}
