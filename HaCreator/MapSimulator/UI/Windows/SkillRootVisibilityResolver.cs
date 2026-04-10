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

            var visibleRoots = new HashSet<int>();
            foreach (int skillRootId in availableRoots)
            {
                if (IsCurrentJobVisibleRoot(currentJobId, skillRootId))
                    visibleRoots.Add(skillRootId);
            }

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

        private static bool IsAlwaysHiddenSkill(int skillId)
        {
            return skillId == MechanicSiegeHiddenSkillId ||
                   skillId == InvisibleRecoverySkillId ||
                   skillId == CygnusMobilityHiddenSkillId;
        }

        private static bool IsCurrentJobVisibleRoot(int currentJobId, int skillRootId)
        {
            int normalizedJobId = Math.Max(0, currentJobId);
            int normalizedSkillRootId = Math.Max(0, skillRootId);
            if (normalizedSkillRootId == BeginnerSkillRootId)
                return true;

            if (normalizedJobId <= 0)
                return false;

            if (normalizedSkillRootId == normalizedJobId)
                return true;

            if (IsDualBladeRoot(normalizedJobId) || IsDualBladeRoot(normalizedSkillRootId))
            {
                return normalizedJobId >= 430 &&
                       normalizedJobId <= 434 &&
                       normalizedSkillRootId >= 400 &&
                       normalizedSkillRootId <= normalizedJobId;
            }

            if (normalizedJobId >= 800 && normalizedJobId < 1000)
                return normalizedSkillRootId == normalizedJobId;

            if (TryResolveSpecialLineage(normalizedJobId, out IReadOnlyList<int> lineage))
                return lineage.Contains(normalizedSkillRootId);

            return IsStandardLineageRoot(normalizedJobId, normalizedSkillRootId);
        }

        private static bool IsStandardLineageRoot(int currentJobId, int skillRootId)
        {
            int firstJob = (currentJobId / 100) * 100;
            if (skillRootId == firstJob)
                return true;

            int secondJob = (currentJobId / 10) * 10;
            if (secondJob > firstJob && skillRootId == secondJob)
                return true;

            int thirdJob = secondJob + (currentJobId % 10 > 0 ? 1 : 0);
            return thirdJob > secondJob && thirdJob < currentJobId && skillRootId == thirdJob;
        }

        private static bool TryResolveSpecialLineage(int jobId, out IReadOnlyList<int> lineage)
        {
            lineage = jobId switch
            {
                2000 or >= 2100 and <= 2112 => BuildLineage(jobId, 2000, 2100, 2110, 2111, 2112),
                2001 or >= 2200 and <= 2218 => BuildLineage(jobId, 2001, 2200, 2210, 2211, 2212, 2213, 2214, 2215, 2216, 2217, 2218),
                2002 or >= 2300 and <= 2312 => BuildLineage(jobId, 2002, 2300, 2310, 2311, 2312),
                2003 or >= 2400 and <= 2412 => BuildLineage(jobId, 2003, 2400, 2410, 2411, 2412),
                2004 or >= 2700 and <= 2712 => BuildLineage(jobId, 2004, 2700, 2710, 2711, 2712),
                2005 or >= 2500 and <= 2512 => BuildLineage(jobId, 2005, 2500, 2510, 2511, 2512),
                1000 or >= 1100 and <= 1112 => BuildLineage(jobId, 1000, 1100, 1110, 1111, 1112),
                >= 1200 and <= 1212 => BuildLineage(jobId, 1000, 1200, 1210, 1211, 1212),
                >= 1300 and <= 1312 => BuildLineage(jobId, 1000, 1300, 1310, 1311, 1312),
                >= 1400 and <= 1412 => BuildLineage(jobId, 1000, 1400, 1410, 1411, 1412),
                >= 1500 and <= 1512 => BuildLineage(jobId, 1000, 1500, 1510, 1511, 1512),
                3000 or >= 3200 and <= 3212 => BuildLineage(jobId, 3000, 3200, 3210, 3211, 3212),
                >= 3300 and <= 3312 => BuildLineage(jobId, 3000, 3300, 3310, 3311, 3312),
                >= 3500 and <= 3512 => BuildLineage(jobId, 3000, 3500, 3510, 3511, 3512),
                3001 or >= 3100 and <= 3112 => BuildLineage(jobId, 3001, 3100, 3110, 3111, 3112),
                3002 or >= 3600 and <= 3612 => BuildLineage(jobId, 3000, 3002, 3600, 3610, 3611, 3612),
                4001 or >= 4100 and <= 4112 => BuildLineage(jobId, 4001, 4100, 4110, 4111, 4112),
                4002 or >= 4200 and <= 4212 => BuildLineage(jobId, 4002, 4200, 4210, 4211, 4212),
                5000 or >= 5100 and <= 5112 => BuildLineage(jobId, 5000, 5100, 5110, 5111, 5112),
                6000 or >= 6100 and <= 6112 => BuildLineage(jobId, 6000, 6100, 6110, 6111, 6112),
                6001 or >= 6500 and <= 6512 => BuildLineage(jobId, 6001, 6500, 6510, 6511, 6512),
                _ => null
            };

            return lineage != null;
        }

        private static IReadOnlyList<int> BuildLineage(int currentJobId, params int[] lineage)
        {
            var visibleLineage = new List<int>(lineage.Length);
            foreach (int skillRootId in lineage)
            {
                visibleLineage.Add(skillRootId);
                if (skillRootId == currentJobId)
                    break;
            }

            if (!visibleLineage.Contains(currentJobId))
                visibleLineage.Add(currentJobId);

            return visibleLineage;
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
            return skillRootId >= 430 && skillRootId <= 434;
        }

        private static bool IsDualBladeBorn(int currentJobId, int currentSubJob)
        {
            int normalizedJobId = Math.Max(0, currentJobId);
            return normalizedJobId / 1000 == 0 && currentSubJob == 1;
        }
    }
}
