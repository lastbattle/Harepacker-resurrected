using System;
using HaCreator.MapSimulator.Character.Skills;

namespace HaCreator.MapSimulator.Pools
{
    public static class RemoteAffectedAreaSupportResolver
    {
        private static readonly string[] FriendlyAreaDescriptionTokens =
        {
            "party member",
            "party members",
            "team member",
            "team members",
            "nearby party",
            "nearby team",
            "all party",
            "all team"
        };

        private static readonly string[] FriendlyAreaSupportTokens =
        {
            "heal",
            "recovery",
            "regeneration",
            "damage",
            "attack",
            "defense",
            "haste",
            "speed",
            "aura"
        };

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

        public static bool IsFriendlyPlayerAreaSkill(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            if (skill.IsMassSpell || IsSupportZone(skill))
            {
                return true;
            }

            return IsFriendlySupportSummonArea(skill);
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

            return ownerIsPartyMember && IsFriendlyPlayerAreaSkill(skill);
        }

        private static bool IsFriendlySupportSummonArea(SkillData skill)
        {
            if (skill?.ClientInfoType != 33)
            {
                return false;
            }

            if (ContainsToken(skill.MinionAbility, "heal", "amplifyDamage", "mes"))
            {
                return true;
            }

            return ContainsToken(skill.Description, FriendlyAreaDescriptionTokens)
                   && ContainsToken(skill.Description, FriendlyAreaSupportTokens);
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
