using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class ClientSkillCancelResolver
    {
        public static bool DoesClientCancelMatchSkillId(
            int activeSkillId,
            int requestedSkillId,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            if (activeSkillId <= 0)
            {
                return false;
            }

            if (requestedSkillId <= 0)
            {
                return true;
            }

            return activeSkillId == requestedSkillId
                   || ResolveCancelRequestSkillId(activeSkillId, getSkillData, skillCatalog) == requestedSkillId;
        }

        public static int ResolveCancelRequestSkillId(
            int skillId,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            if (skillId <= 0)
            {
                return skillId;
            }

            SkillData skill = getSkillData?.Invoke(skillId);
            if (skill?.AffectedSkillId > 0
                && (skill.ClientInfoType == 50 || skill.ClientInfoType == 51))
            {
                return skill.AffectedSkillId;
            }

            SkillData dummyParent = FindDummyParentSkill(skillId, skillCatalog);
            return dummyParent?.SkillId ?? skillId;
        }

        private static SkillData FindDummyParentSkill(int skillId, IReadOnlyCollection<SkillData> skillCatalog)
        {
            if (skillId <= 0 || skillCatalog == null || skillCatalog.Count == 0)
            {
                return null;
            }

            return skillCatalog.FirstOrDefault(candidate =>
                candidate?.SkillId > 0
                && candidate.SkillId != skillId
                && candidate.LinksDummySkill(skillId));
        }
    }
}
