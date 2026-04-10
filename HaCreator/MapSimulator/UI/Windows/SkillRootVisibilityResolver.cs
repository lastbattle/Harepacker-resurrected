using System;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillRootVisibilityResolver
    {
        private const int InvisibleRecoverySkillId = 1014;
        private const int DualBladeBornHiddenSkillIdA = 4000001;
        private const int DualBladeBornHiddenSkillIdB = 4001344;

        public static bool IsSkillVisible(SkillDisplayData skill, int currentJobId, int currentSubJob)
        {
            if (skill == null)
                return false;

            if (skill.SkillId == InvisibleRecoverySkillId)
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

        private static bool IsDualBladeBorn(int currentJobId, int currentSubJob)
        {
            int normalizedJobId = Math.Max(0, currentJobId);
            return normalizedJobId / 1000 == 0 && currentSubJob == 1;
        }
    }
}
