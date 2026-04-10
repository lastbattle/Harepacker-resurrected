using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillRootVisibilityResolver
    {
        private const int BeginnerSkillRootId = 0;
        private const int SkillRootDivisor = 10000;
        private const int MechanicSiegeHiddenSkillId = 4321001;
        private const int InvisibleRecoverySkillId = 1014;
        private const int CygnusMobilityHiddenSkillId = 10001015;
        private const int DualBladeBornHiddenSkillIdA = 4000001;
        private const int DualBladeBornHiddenSkillIdB = 4001344;
        private const int DualBladeRogueSkillRootId = 400;

        public static bool IsSkillVisible(SkillDisplayData skill, int currentJobId, int currentSubJob)
        {
            if (skill == null)
                return false;

            if (IsAlwaysHiddenSkill(skill.SkillId))
                return false;

            if (IsDualBladeBorn(currentJobId, currentSubJob) &&
                (skill.SkillId == DualBladeBornHiddenSkillIdA || skill.SkillId == DualBladeBornHiddenSkillIdB))
            {
                return false;
            }

            bool hasLearnedRecord = skill.CurrentLevel > 0;
            if (skill.IsTimeLimited && !hasLearnedRecord)
                return false;

            if (skill.IsInvisible && !hasLearnedRecord)
                return false;

            return true;
        }

        public static IReadOnlyList<int> ResolveVisibleSkillRootIds(
            int currentJobId,
            IEnumerable<int> availableSkillRootIds,
            IEnumerable<int> learnedSkillIds,
            Func<int, int, bool> rootContainsSkill)
        {
            if (availableSkillRootIds == null)
                return Array.Empty<int>();

            HashSet<int> availableRoots = availableSkillRootIds
                .Where(skillRootId => skillRootId >= 0)
                .ToHashSet();
            if (availableRoots.Count == 0)
                return Array.Empty<int>();

            var visibleRoots = ResolveCurrentLineageVisibleRoots(currentJobId, availableRoots);

            if (learnedSkillIds != null)
            {
                foreach (int skillId in learnedSkillIds)
                {
                    int skillRootId = ResolveSkillRootId(skillId);
                    if (!availableRoots.Contains(skillRootId))
                        continue;

                    if (rootContainsSkill == null || rootContainsSkill(skillRootId, skillId))
                        visibleRoots.Add(skillRootId);
                }
            }

            if (visibleRoots.Count == 0 && availableRoots.Contains(BeginnerSkillRootId))
                visibleRoots.Add(BeginnerSkillRootId);

            return visibleRoots
                .OrderBy(GetSkillRootSortKey)
                .ThenBy(skillRootId => skillRootId)
                .ToList();
        }

        public static int ResolveSkillRootId(int skillId)
        {
            if (skillId <= 0)
                return BeginnerSkillRootId;

            return skillId < SkillRootDivisor
                ? BeginnerSkillRootId
                : skillId / SkillRootDivisor;
        }

        private static HashSet<int> ResolveCurrentLineageVisibleRoots(int currentJobId, HashSet<int> availableRoots)
        {
            var visibleRoots = new HashSet<int>();
            if (availableRoots.Contains(BeginnerSkillRootId))
                visibleRoots.Add(BeginnerSkillRootId);

            int currentSkillRootId = Math.Max(0, currentJobId);
            var visitedRoots = new HashSet<int>();
            while (currentSkillRootId > BeginnerSkillRootId && visitedRoots.Add(currentSkillRootId))
            {
                if (availableRoots.Contains(currentSkillRootId))
                    visibleRoots.Add(currentSkillRootId);

                if (!TryGetParentSkillRootId(currentSkillRootId, out int parentSkillRootId))
                    break;

                currentSkillRootId = parentSkillRootId;
            }

            return visibleRoots;
        }

        private static bool IsAlwaysHiddenSkill(int skillId)
        {
            return skillId == MechanicSiegeHiddenSkillId ||
                   skillId == InvisibleRecoverySkillId ||
                   skillId == CygnusMobilityHiddenSkillId;
        }

        private static bool TryGetParentSkillRootId(int skillRootId, out int parentSkillRootId)
        {
            parentSkillRootId = BeginnerSkillRootId;
            if (skillRootId <= BeginnerSkillRootId)
                return false;

            if (TryGetSpecialParentSkillRootId(skillRootId, out parentSkillRootId))
                return true;

            if (skillRootId >= 800 && skillRootId < 1000)
                return false;

            int jobTier = skillRootId % 10;
            if (jobTier == 1 || jobTier == 2)
            {
                parentSkillRootId = skillRootId - 1;
                return true;
            }

            if (skillRootId % 100 != 0)
            {
                parentSkillRootId = (skillRootId / 100) * 100;
                return true;
            }

            return false;
        }

        private static bool TryGetSpecialParentSkillRootId(int skillRootId, out int parentSkillRootId)
        {
            switch (skillRootId)
            {
                case DualBladeRogueSkillRootId:
                    parentSkillRootId = BeginnerSkillRootId;
                    return true;
                case 430:
                    parentSkillRootId = DualBladeRogueSkillRootId;
                    return true;
                case 431:
                case 432:
                case 433:
                case 434:
                    parentSkillRootId = skillRootId - 1;
                    return true;
                case 1000:
                case 2000:
                case 2001:
                case 2002:
                case 2003:
                case 2004:
                case 2005:
                case 3000:
                case 3001:
                case 4001:
                case 4002:
                case 5000:
                case 6000:
                case 6001:
                    parentSkillRootId = BeginnerSkillRootId;
                    return true;
                case >= 1100 and <= 1500 when skillRootId % 100 == 0:
                    parentSkillRootId = 1000;
                    return true;
                case 2100:
                    parentSkillRootId = 2000;
                    return true;
                case 2200:
                    parentSkillRootId = 2001;
                    return true;
                case 2300:
                    parentSkillRootId = 2002;
                    return true;
                case 2400:
                    parentSkillRootId = 2003;
                    return true;
                case 2500:
                    parentSkillRootId = 2005;
                    return true;
                case 2700:
                    parentSkillRootId = 2004;
                    return true;
                case >= 2211 and <= 2218:
                    parentSkillRootId = skillRootId - 1;
                    return true;
                case 2210:
                    parentSkillRootId = 2200;
                    return true;
                case >= 3200 and <= 3500 when skillRootId % 100 == 0:
                    parentSkillRootId = 3000;
                    return true;
                case 3100:
                    parentSkillRootId = 3001;
                    return true;
                case 3002:
                    parentSkillRootId = 3000;
                    return true;
                case 3600:
                    parentSkillRootId = 3002;
                    return true;
                case 4100:
                    parentSkillRootId = 4001;
                    return true;
                case 4200:
                    parentSkillRootId = 4002;
                    return true;
                case 5100:
                    parentSkillRootId = 5000;
                    return true;
                case 6100:
                    parentSkillRootId = 6000;
                    return true;
                case 6500:
                    parentSkillRootId = 6001;
                    return true;
                default:
                    parentSkillRootId = BeginnerSkillRootId;
                    return false;
            }
        }

        private static int GetSkillRootSortKey(int skillRootId)
        {
            if (skillRootId <= 0)
                return 0;

            if (IsDualBladeRoot(skillRootId))
            {
                return skillRootId switch
                {
                    400 => 100,
                    430 => 200,
                    431 => 300,
                    432 => 400,
                    433 => 500,
                    434 => 600,
                    _ => 100
                };
            }

            if (skillRootId % 100 == 0)
                return 100;

            return (skillRootId % 10) switch
            {
                0 => 200,
                1 => 300,
                2 => 400,
                _ => 100
            };
        }

        private static bool IsDualBladeRoot(int skillRootId)
        {
            return skillRootId == DualBladeRogueSkillRootId || (skillRootId >= 430 && skillRootId <= 434);
        }

        private static bool IsDualBladeBorn(int currentJobId, int currentSubJob)
        {
            int normalizedJobId = Math.Max(0, currentJobId);
            return normalizedJobId / 1000 == 0 && currentSubJob == 1;
        }
    }
}
