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
            return IsVisible(hiddenByMap, questInfo, dynamicTags: null, getQuestState, getDynamicTagState: null);
        }

        public static bool IsVisible(
            bool hiddenByMap,
            IReadOnlyList<ObjectInstanceQuest> questInfo,
            IReadOnlyList<string> dynamicTags,
            Func<int, QuestStateType> getQuestState,
            Func<string, bool?> getDynamicTagState)
        {
            bool matchesQuestState = MatchesQuestState(questInfo, getQuestState);
            if (!matchesQuestState)
            {
                return false;
            }

            bool hasDynamicTags = dynamicTags != null && dynamicTags.Count > 0;
            if (hiddenByMap)
            {
                bool hasQuestInfo = questInfo != null && questInfo.Count > 0;
                if (!hasQuestInfo && !hasDynamicTags)
                {
                    return false;
                }

                return MatchesDynamicTagState(dynamicTags, getDynamicTagState, treatUnknownAsMatch: false);
            }

            return MatchesDynamicTagState(dynamicTags, getDynamicTagState, treatUnknownAsMatch: true);
        }

        public static bool IsVisible(IReadOnlyList<ObjectInstanceQuest> questInfo, Func<int, QuestStateType> getQuestState)
        {
            return IsVisible(questInfo, dynamicTags: null, getQuestState, getDynamicTagState: null);
        }

        public static bool IsVisible(
            IReadOnlyList<ObjectInstanceQuest> questInfo,
            IReadOnlyList<string> dynamicTags,
            Func<int, QuestStateType> getQuestState,
            Func<string, bool?> getDynamicTagState)
        {
            if (!MatchesQuestState(questInfo, getQuestState))
            {
                return false;
            }

            return MatchesDynamicTagState(dynamicTags, getDynamicTagState, treatUnknownAsMatch: true);
        }

        private static bool MatchesQuestState(IReadOnlyList<ObjectInstanceQuest> questInfo, Func<int, QuestStateType> getQuestState)
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

        private static bool MatchesDynamicTagState(
            IReadOnlyList<string> dynamicTags,
            Func<string, bool?> getDynamicTagState,
            bool treatUnknownAsMatch)
        {
            if (dynamicTags == null || dynamicTags.Count == 0 || getDynamicTagState == null)
            {
                return treatUnknownAsMatch || dynamicTags == null || dynamicTags.Count == 0;
            }

            for (int i = 0; i < dynamicTags.Count; i++)
            {
                string tag = dynamicTags[i];
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                bool? tagState = getDynamicTagState(tag);
                if (!tagState.HasValue)
                {
                    if (!treatUnknownAsMatch)
                    {
                        return false;
                    }

                    continue;
                }

                if (!tagState.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
