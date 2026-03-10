using System;
using System.Collections.Generic;
using HaCreator.MapEditor.Instance;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;

namespace HaCreator.MapSimulator.Fields
{
    public static class FieldObjectQuestVisibilityEvaluator
    {
        public static bool IsVisible(bool hiddenByMap, IReadOnlyList<ObjectInstanceQuest> questInfo, Func<int, QuestStateType> getQuestState)
        {
            if (hiddenByMap)
            {
                return false;
            }

            return IsVisible(questInfo, getQuestState);
        }

        public static bool IsVisible(IReadOnlyList<ObjectInstanceQuest> questInfo, Func<int, QuestStateType> getQuestState)
        {
            if (questInfo == null || questInfo.Count == 0)
            {
                return true;
            }

            if (getQuestState == null)
            {
                return false;
            }

            for (int i = 0; i < questInfo.Count; i++)
            {
                ObjectInstanceQuest requirement = questInfo[i];
                if (getQuestState(requirement.questId) == requirement.state)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
