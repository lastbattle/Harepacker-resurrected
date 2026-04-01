using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class SummonRuntimeRules
    {
        private const int Sg88SkillId = 35121003;
        private const byte PacketSkillActionBeholderHeal = 7;
        private const byte PacketSkillActionBeholderBuffBase = 8;
        private const byte PacketSkillActionBeholderBuffMax = 12;
        private const byte PacketSkillActionHealingRobotHeal = 13;
        private const byte PacketSkillActionSubsummon = 14;

        public static int ResolveDurationMs(SkillData skill, SkillLevelData levelData, int skillLevel)
        {
            if (levelData?.Time > 0)
            {
                return levelData.Time * 1000;
            }

            int authoredDurationSeconds = skill?.ResolveSummonDurationSeconds(Math.Max(1, skillLevel)) ?? 0;
            if (authoredDurationSeconds > 0)
            {
                return authoredDurationSeconds * 1000;
            }

            return IsSatelliteSummonSkill(skill?.SkillId ?? 0) ? 0 : 30000;
        }

        public static SummonAssistType ResolveAssistType(SkillData skill)
        {
            if (skill == null)
            {
                return SummonAssistType.PeriodicAttack;
            }

            if (UsesReactiveDamageTriggerSummon(skill))
            {
                return SummonAssistType.TargetedAttack;
            }

            if (skill.SkillId == Sg88SkillId)
            {
                return SummonAssistType.ManualAttack;
            }

            if (IsSatelliteSummonSkill(skill.SkillId))
            {
                return SummonAssistType.OwnerAttackTargeted;
            }

            if (HasMinionAbilityToken(skill.MinionAbility, "heal")
                || HasMinionAbilityToken(skill.MinionAbility, "mes")
                || HasMinionAbilityToken(skill.MinionAbility, "amplifyDamage"))
            {
                return SummonAssistType.Support;
            }

            if (HasMinionAbilityToken(skill.MinionAbility, "summon"))
            {
                return SummonAssistType.SummonAction;
            }

            return SummonAssistType.PeriodicAttack;
        }

        public static bool ShouldRegisterPuppet(SkillData skill)
        {
            return HasMinionAbilityToken(skill?.MinionAbility, "taunt");
        }

        public static bool HasMinionAbilityToken(string minionAbility, string token)
        {
            if (string.IsNullOrWhiteSpace(minionAbility) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            foreach (string part in minionAbility.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Trim().Equals(token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool UsesReactiveDamageTriggerSummon(SkillData skill)
        {
            return skill != null
                   && string.Equals(skill.SummonCondition, "damaged", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(skill.MinionAbility)
                   && skill.MinionAbility.IndexOf("reflect", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string ResolvePacketSkillBranch(SkillData skill, byte packetAction, SummonAssistType? assistType = null)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return null;
            }

            byte normalizedAction = (byte)(packetAction & 0x7F);
            if (normalizedAction == PacketSkillActionBeholderHeal)
            {
                return ResolveNamedSummonBranch(skill, "skill1", "heal", "support");
            }

            if (normalizedAction >= PacketSkillActionBeholderBuffBase
                && normalizedAction <= PacketSkillActionBeholderBuffMax)
            {
                string branchName = $"skill{normalizedAction - PacketSkillActionBeholderBuffBase + 2}";
                return ResolveNamedSummonBranch(skill, branchName, "support", "heal");
            }

            if (normalizedAction == PacketSkillActionHealingRobotHeal)
            {
                return ResolveNamedSummonBranch(skill, "heal", "support");
            }

            if (normalizedAction == PacketSkillActionSubsummon)
            {
                return ResolveNamedSummonBranch(skill, "subsummon");
            }

            if (assistType.HasValue)
            {
                string assistOwnedBranch = ResolveAssistOwnedPacketSkillBranch(skill, assistType.Value);
                if (!string.IsNullOrWhiteSpace(assistOwnedBranch))
                {
                    return assistOwnedBranch;
                }
            }

            if (HasMinionAbilityToken(skill.MinionAbility, "heal"))
            {
                return ResolveNamedSummonBranch(skill, "heal", "support");
            }

            if (HasMinionAbilityToken(skill.MinionAbility, "mes")
                || HasMinionAbilityToken(skill.MinionAbility, "amplifyDamage"))
            {
                return ResolveNamedSummonBranch(skill, "support", "heal");
            }

            if (HasMinionAbilityToken(skill.MinionAbility, "summon"))
            {
                return ResolveNamedSummonBranch(skill, "subsummon");
            }

            return null;
        }

        private static string ResolveAssistOwnedPacketSkillBranch(SkillData skill, SummonAssistType assistType)
        {
            if (skill == null)
            {
                return null;
            }

            return assistType switch
            {
                SummonAssistType.Support when skill.SkillId == 1321007
                    => ResolveNamedSummonBranch(skill, "skill1", "heal", "support"),
                SummonAssistType.Support when HasMinionAbilityToken(skill.MinionAbility, "heal")
                    => ResolveNamedSummonBranch(skill, "heal", "support"),
                SummonAssistType.Support when HasMinionAbilityToken(skill.MinionAbility, "mes")
                                                  || HasMinionAbilityToken(skill.MinionAbility, "amplifyDamage")
                    => ResolveNamedSummonBranch(skill, "support", "heal"),
                SummonAssistType.Support
                    => ResolveNamedSummonBranch(skill, "support", "heal"),
                SummonAssistType.SummonAction
                    => ResolveNamedSummonBranch(skill, "subsummon"),
                _ => null
            };
        }

        public static string ResolveSelfDestructFinalBranch(SkillData skill, SummonAssistType assistType)
        {
            if (skill == null)
            {
                return null;
            }

            if (skill.SkillId == 1321007)
            {
                return ResolveNamedSummonBranch(skill, "skill1", "heal", "support");
            }

            return assistType switch
            {
                SummonAssistType.Support when HasMinionAbilityToken(skill.MinionAbility, "heal")
                    => ResolveNamedSummonBranch(skill, "heal", "support"),
                SummonAssistType.Support
                    => ResolveNamedSummonBranch(skill, "support", "heal"),
                SummonAssistType.SummonAction
                    => ResolveNamedSummonBranch(skill, "subsummon"),
                _ => null
            };
        }

        public static bool IsSatelliteSummonSkill(int skillId)
        {
            return skillId == 35111001
                || skillId == 35111009
                || skillId == 35111010;
        }

        private static string ResolveNamedSummonBranch(SkillData skill, params string[] preferredBranchNames)
        {
            if (skill?.SummonNamedAnimations == null || preferredBranchNames == null)
            {
                return null;
            }

            foreach (string branchName in preferredBranchNames)
            {
                if (!string.IsNullOrWhiteSpace(branchName) && skill.SummonNamedAnimations.ContainsKey(branchName))
                {
                    return branchName;
                }
            }

            return null;
        }
    }
}
