using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class ClientSkillCancelResolver
    {
        private static readonly int[] SupportedAffectedSkillCancelTypes = { 1, 16, 33, 50, 51 };

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
                   || ResolveCancelRequestSkillIds(activeSkillId, getSkillData, skillCatalog).Contains(requestedSkillId);
        }

        public static int ResolveCancelRequestSkillId(
            int skillId,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            return ResolveCancelRequestSkillIds(skillId, getSkillData, skillCatalog).FirstOrDefault();
        }

        private static IReadOnlyList<int> ResolveCancelRequestSkillIds(
            int skillId,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            if (skillId <= 0)
            {
                return Array.Empty<int>();
            }

            SkillData skill = getSkillData?.Invoke(skillId);
            if (UsesAffectedSkillCancelFamily(skill))
            {
                int[] affectedSkillIds = skill.GetAffectedSkillIds();
                if (affectedSkillIds.Length > 0)
                {
                    return affectedSkillIds;
                }
            }

            SkillData dummyParent = FindDummyParentSkill(skillId, skillCatalog);
            return dummyParent != null
                ? new[] { dummyParent.SkillId }
                : new[] { skillId };
        }

        private static bool UsesAffectedSkillCancelFamily(SkillData skill)
        {
            return skill != null
                   && skill.GetAffectedSkillIds().Length > 0
                   && Array.IndexOf(SupportedAffectedSkillCancelTypes, skill.ClientInfoType) >= 0;
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
