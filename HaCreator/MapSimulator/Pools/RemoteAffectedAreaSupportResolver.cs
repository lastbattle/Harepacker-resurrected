using System;
using HaCreator.MapSimulator.Character.Skills;

namespace HaCreator.MapSimulator.Pools
{
    public static class RemoteAffectedAreaSupportResolver
    {
        public static bool IsAreaBuffItemType(int areaType)
        {
            return areaType == 3;
        }

        public static bool IsRecoveryZone(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            if (skill.IsHeal)
            {
                return true;
            }

            return ContainsToken(skill.ZoneType, "heal", "recovery", "regen")
                   || ContainsToken(skill.Name, "heal", "recovery", "regeneration")
                   || ContainsToken(skill.Description, "heal", "recovery", "regeneration");
        }

        public static bool IsSupportZone(SkillData skill)
        {
            return IsInvincibleZone(skill) || IsRecoveryZone(skill);
        }

        public static bool IsInvincibleZone(SkillData skill)
        {
            return string.Equals(skill?.ZoneType, "invincible", StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanAffectLocalPlayer(SkillData skill, int localPlayerId, int ownerId, bool ownerIsPartyMember)
        {
            if (skill == null || localPlayerId <= 0 || ownerId <= 0)
            {
                return false;
            }

            if (ownerId == localPlayerId)
            {
                return true;
            }

            return skill.IsMassSpell && ownerIsPartyMember;
        }

        private static bool ContainsToken(string value, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
