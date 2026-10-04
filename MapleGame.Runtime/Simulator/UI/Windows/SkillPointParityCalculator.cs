using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillPointParityCalculator
    {
        private const int SkillPointsPerLevel = 3;

        internal static Dictionary<int, int> CalculateRemainingPointsByTab(
            int characterLevel,
            IReadOnlyDictionary<int, List<SkillDisplayData>> skillsByTab,
            IReadOnlyDictionary<int, int> displayedSkillRootByTab)
        {
            Dictionary<int, int> remainingPoints = new();
            if (skillsByTab == null || skillsByTab.Count == 0)
                return remainingPoints;

            int resolvedCharacterLevel = Math.Max(1, characterLevel);
            List<BookOpenLevelInfo> tabOrder = BuildTabOrder(skillsByTab, displayedSkillRootByTab);

            foreach (KeyValuePair<int, List<SkillDisplayData>> entry in skillsByTab)
            {
                int tab = entry.Key;
                List<SkillDisplayData> skills = entry.Value ?? new List<SkillDisplayData>();
                int skillRootId = ResolveDisplayedSkillRootId(displayedSkillRootByTab, tab);
                if (tab == 0 || skillRootId == 0)
                {
                    remainingPoints[tab] = 0;
                    continue;
                }

                BookOpenLevelInfo currentInfo = tabOrder.FirstOrDefault(info => info.Tab == tab);
                if (currentInfo == null)
                {
                    remainingPoints[tab] = 0;
                    continue;
                }

                int nextOpenLevel = tabOrder
                    .Where(info => info.SortKey > currentInfo.SortKey)
                    .Select(info => info.OpenLevel)
                    .DefaultIfEmpty(int.MaxValue)
                    .Min();
                int awardedPoints = CalculateAwardedPointsForBook(resolvedCharacterLevel, currentInfo.OpenLevel, nextOpenLevel);
                int spentPoints = GetSpentPoints(skills);
                remainingPoints[tab] = Math.Max(0, awardedPoints - spentPoints);
            }

            return remainingPoints;
        }

        internal static int ResolveBookOpenLevelForTests(
            int tab,
            IReadOnlyList<SkillDisplayData> skills,
            int displayedSkillRootId)
        {
            return ResolveBookOpenLevel(tab, skills, displayedSkillRootId);
        }

        internal static int CalculateAwardedPointsForBookForTests(int characterLevel, int openLevel, int nextOpenLevel)
        {
            return CalculateAwardedPointsForBook(characterLevel, openLevel, nextOpenLevel);
        }

        private static List<BookOpenLevelInfo> BuildTabOrder(
            IReadOnlyDictionary<int, List<SkillDisplayData>> skillsByTab,
            IReadOnlyDictionary<int, int> displayedSkillRootByTab)
        {
            List<BookOpenLevelInfo> orderedTabs = new();
            foreach (int tab in skillsByTab.Keys.OrderBy(static key => key))
            {
                int skillRootId = ResolveDisplayedSkillRootId(displayedSkillRootByTab, tab);
                if (tab == 0 || skillRootId == 0)
                    continue;

                int openLevel = ResolveBookOpenLevel(
                    tab,
                    skillsByTab.TryGetValue(tab, out List<SkillDisplayData> skills) ? skills : null,
                    skillRootId);
                orderedTabs.Add(new BookOpenLevelInfo(tab, skillRootId, openLevel, ResolveSortKey(openLevel, tab)));
            }

            return orderedTabs
                .OrderBy(static info => info.SortKey)
                .ThenBy(static info => info.Tab)
                .ToList();
        }

        private static int ResolveDisplayedSkillRootId(IReadOnlyDictionary<int, int> displayedSkillRootByTab, int tab)
        {
            return displayedSkillRootByTab != null && displayedSkillRootByTab.TryGetValue(tab, out int displayedSkillRootId)
                ? displayedSkillRootId
                : 0;
        }

        private static int ResolveBookOpenLevel(int tab, IReadOnlyList<SkillDisplayData> skills, int displayedSkillRootId)
        {
            if (tab == 0 || displayedSkillRootId == 0)
                return 0;

            if (skills != null)
            {
                int minRequiredLevel = skills
                    .Where(static skill => skill != null && skill.RequiredCharacterLevel > 0)
                    .Select(static skill => skill.RequiredCharacterLevel)
                    .DefaultIfEmpty(0)
                    .Min();
                if (minRequiredLevel > 0)
                    return minRequiredLevel;
            }

            if (TryResolveFallbackOpenLevelFromSkillRoot(displayedSkillRootId, out int rootOpenLevel))
                return rootOpenLevel;

            return tab switch
            {
                1 => 10,
                2 => 30,
                3 => 70,
                4 => 120,
                5 => 160,
                6 => 200,
                _ => 10
            };
        }

        private static bool TryResolveFallbackOpenLevelFromSkillRoot(int displayedSkillRootId, out int openLevel)
        {
            openLevel = displayedSkillRootId switch
            {
                100 or 200 or 300 or 400 or 500 or 1100 or 1200 or 1300 or 1500 or 2000 => 10,
                110 or 120 or 130 or 210 or 220 or 230 or 310 or 320 or 410 or 420 or 510 or 520 or 1110 or 1210 or 1310 or 1510 or 2100 or 2200 or 2210 or 3200 or 3300 or 3500 => 30,
                111 or 121 or 131 or 211 or 221 or 231 or 311 or 321 or 411 or 421 or 511 or 521 or 1111 or 1211 or 1311 or 1511 or 2110 or 2211 or 3210 or 3310 or 3510 => 70,
                112 or 122 or 132 or 212 or 222 or 232 or 312 or 322 or 412 or 422 or 512 or 522 or 1112 or 1212 or 1312 or 1512 or 2111 or 2212 or 3211 or 3311 or 3511 => 120,
                430 => 20,
                431 => 55,
                432 => 70,
                433 => 120,
                434 => 160,
                _ => 0
            };

            return openLevel > 0;
        }

        private static int ResolveSortKey(int openLevel, int tab)
        {
            return (Math.Max(0, openLevel) * 10) + Math.Max(0, tab);
        }

        private static int CalculateAwardedPointsForBook(int characterLevel, int openLevel, int nextOpenLevel)
        {
            if (openLevel <= 0 || characterLevel < openLevel)
                return 0;

            int lastInclusiveLevel = nextOpenLevel > openLevel
                ? Math.Min(characterLevel, nextOpenLevel - 1)
                : characterLevel;
            if (lastInclusiveLevel < openLevel)
                return 0;

            return checked(((lastInclusiveLevel - openLevel) + 1) * SkillPointsPerLevel);
        }

        private static int GetSpentPoints(IEnumerable<SkillDisplayData> skills)
        {
            if (skills == null)
                return 0;

            int spentPoints = 0;
            foreach (SkillDisplayData skill in skills)
            {
                if (skill == null)
                    continue;

                spentPoints = checked(spentPoints + Math.Max(0, skill.CurrentLevel));
            }

            return spentPoints;
        }

        private sealed record BookOpenLevelInfo(int Tab, int SkillRootId, int OpenLevel, int SortKey);
    }
}
