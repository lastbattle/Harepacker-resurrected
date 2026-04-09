using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class SummonRuntimeRules
    {
        private const int Sg88SkillId = 35121003;
        private const int BeholderPddBranchIndex = 0;
        private const int BeholderMddBranchIndex = 1;
        private const int BeholderAccBranchIndex = 2;
        private const int BeholderEvaBranchIndex = 3;
        private const int BeholderPadBranchIndex = 4;
        private const byte PacketSkillActionBeholderHeal = 7;
        private const byte PacketSkillActionBeholderBuffBase = 8;
        private const byte PacketSkillActionBeholderBuffMax = 12;
        private const byte PacketSkillActionHealingRobotHeal = 13;
        private const byte PacketSkillActionSubsummon = 14;
        private const byte PacketSkillActionSkillBranchMin = 1;
        private const byte PacketSkillActionSkillBranchMax = 6;

        public static int ResolveAuthoredDurationMs(SkillData skill, SkillLevelData levelData, int skillLevel)
        {
            if (levelData?.Time > 0)
            {
                return levelData.Time * 1000;
            }

            int authoredDurationSeconds = skill?.ResolveSummonDurationSeconds(Math.Max(1, skillLevel)) ?? 0;
            return authoredDurationSeconds > 0
                ? authoredDurationSeconds * 1000
                : 0;
        }

        public static int ResolveDurationMs(SkillData skill, SkillLevelData levelData, int skillLevel)
        {
            int authoredDurationMs = ResolveAuthoredDurationMs(skill, levelData, skillLevel);
            if (authoredDurationMs > 0)
            {
                return authoredDurationMs;
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

        public static bool IsSitdownHealingSupportSummon(SkillData skill)
        {
            return skill != null
                   && HasMinionAbilityToken(skill.MinionAbility, "heal")
                   && string.Equals(skill.SummonCondition, "whenUserLieDown", StringComparison.OrdinalIgnoreCase);
        }

        public static bool UsesReactiveDamageTriggerSummon(SkillData skill)
        {
            return skill != null
                   && string.Equals(skill.SummonCondition, "damaged", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(skill.MinionAbility)
                   && skill.MinionAbility.IndexOf("reflect", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static int ResolveSummonActionPrepareDurationMs(SkillData skill, string branchName)
        {
            if (skill == null)
            {
                return 0;
            }

            if (UsesNamedSummonAnimationBranch(skill, branchName))
            {
                return 0;
            }

            return GetAnimationDuration(skill.SummonAttackPrepareAnimation);
        }

        public static string ResolvePacketSkillBranch(SkillData skill, byte packetAction, SummonAssistType? assistType = null)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return null;
            }

            byte normalizedAction = (byte)(packetAction & 0x7F);
            string indexedBranch = ResolvePacketIndexedSkillBranch(skill, normalizedAction);
            if (!string.IsNullOrWhiteSpace(indexedBranch))
            {
                return indexedBranch;
            }

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
                return ResolveSupportOwnedBranch(skill, preferHealFirst: true);
            }

            if (HasMinionAbilityToken(skill.MinionAbility, "mes")
                || HasMinionAbilityToken(skill.MinionAbility, "amplifyDamage"))
            {
                return ResolveSupportOwnedBranch(skill, preferHealFirst: false);
            }

            if (HasMinionAbilityToken(skill.MinionAbility, "summon"))
            {
                return ResolveNamedSummonBranch(skill, "subsummon", "skill1", "skill2");
            }

            return null;
        }

        internal static string ResolveBeholderHealBranch(SkillData skill)
        {
            return ResolveNamedSummonBranch(skill, "skill1", "heal", "support");
        }

        internal static string ResolveLocalSupportBranch(SkillData skill, bool preferHealFirst)
        {
            return ResolveSupportOwnedBranch(skill, preferHealFirst);
        }

        internal static string ResolveLocalSummonActionBranch(SkillData skill)
        {
            return ResolveNamedSummonBranch(skill, "subsummon", "skill1", "skill2");
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
                    => ResolveNamedSummonBranch(skill, "skill1", "heal", "support", "stand"),
                SummonAssistType.Support when HasMinionAbilityToken(skill.MinionAbility, "heal")
                    => ResolveSupportOwnedBranch(skill, preferHealFirst: true),
                SummonAssistType.Support when HasMinionAbilityToken(skill.MinionAbility, "mes")
                                                  || HasMinionAbilityToken(skill.MinionAbility, "amplifyDamage")
                    => ResolveSupportOwnedBranch(skill, preferHealFirst: false),
                SummonAssistType.Support
                    => ResolveSupportOwnedBranch(skill, preferHealFirst: false),
                SummonAssistType.SummonAction
                    => ResolveNamedSummonBranch(skill, "subsummon", "skill1", "skill2"),
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
                return ResolveNamedSummonBranch(skill, "skill1", "heal", "support", "stand");
            }

            return assistType switch
                {
                SummonAssistType.Support when HasMinionAbilityToken(skill.MinionAbility, "heal")
                    => ResolveSupportOwnedBranch(skill, preferHealFirst: true),
                SummonAssistType.Support
                    => ResolveSupportOwnedBranch(skill, preferHealFirst: false),
                SummonAssistType.SummonAction
                    => ResolveNamedSummonBranch(skill, "subsummon", "skill1", "skill2"),
                _ => null
            };
        }

        internal static string ResolveSupportOwnedBranch(SkillData skill, bool preferHealFirst)
        {
            return preferHealFirst
                ? ResolveNamedSummonBranch(skill, "skill1", "heal", "support", "skill2", "stand")
                : ResolveNamedSummonBranch(skill, "skill2", "support", "heal", "skill1", "stand");
        }

        internal static Rectangle ResolveSupportOwnedRange(
            SkillData skill,
            bool facingRight,
            string explicitBranchName = null)
        {
            if (skill == null)
            {
                return Rectangle.Empty;
            }

            if (!string.IsNullOrWhiteSpace(explicitBranchName)
                && skill.TryGetSummonAttackRange(facingRight, explicitBranchName, out Rectangle explicitRange))
            {
                return explicitRange;
            }

            string supportBranchName = HasMinionAbilityToken(skill.MinionAbility, "heal")
                ? ResolveSupportOwnedBranch(skill, preferHealFirst: true)
                : ResolveSupportOwnedBranch(skill, preferHealFirst: false);
            if (!string.IsNullOrWhiteSpace(supportBranchName)
                && skill.TryGetSummonAttackRange(facingRight, supportBranchName, out Rectangle supportRange))
            {
                return supportRange;
            }

            return skill.GetSummonAttackRange(facingRight);
        }

        private static string ResolvePacketIndexedSkillBranch(SkillData skill, byte normalizedAction)
        {
            if (normalizedAction < PacketSkillActionSkillBranchMin
                || normalizedAction > PacketSkillActionSkillBranchMax)
            {
                return null;
            }

            return ResolveNamedSummonBranch(skill, $"skill{normalizedAction}");
        }

        internal static int ResolveSupportSuspendDurationMs(
            SkillData skill,
            bool preferHealFirst = true,
            string explicitBranchName = null)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return 0;
            }

            string branchName = explicitBranchName;
            if (string.IsNullOrWhiteSpace(branchName))
            {
                branchName = ResolveSupportOwnedBranch(skill, preferHealFirst);
            }

            if (string.IsNullOrWhiteSpace(branchName)
                || !skill.SummonNamedAnimations.TryGetValue(branchName, out SkillAnimation suspendAnimation))
            {
                return 0;
            }

            return GetAnimationDuration(suspendAnimation);
        }

        internal static int? ResolveBeholderBuffBranchIndex(
            SkillData skill,
            SkillLevelData buffLevelData,
            int currentPad,
            int currentPdd,
            int currentMdd,
            int currentAcc,
            int currentEva,
            int randomValue)
        {
            if (skill?.SummonNamedAnimations == null || buffLevelData == null)
            {
                return null;
            }

            int totalWeight = 0;
            for (int branchIndex = BeholderPddBranchIndex; branchIndex <= BeholderPadBranchIndex; branchIndex++)
            {
                if (HasBeholderBuffBranch(skill, buffLevelData, branchIndex))
                {
                    totalWeight += branchIndex + 1;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int weightedRoll = (Math.Abs(randomValue) % totalWeight) + 1;
            int cumulativeWeight = 0;
            int thresholdBranchIndex = BeholderPddBranchIndex;
            for (int branchIndex = BeholderPddBranchIndex; branchIndex <= BeholderPadBranchIndex; branchIndex++)
            {
                if (!HasBeholderBuffBranch(skill, buffLevelData, branchIndex))
                {
                    continue;
                }

                cumulativeWeight += branchIndex + 1;
                if (weightedRoll <= cumulativeWeight)
                {
                    thresholdBranchIndex = branchIndex;
                    break;
                }
            }

            if (thresholdBranchIndex >= BeholderPadBranchIndex
                && HasBeholderBuffBranch(skill, buffLevelData, BeholderPadBranchIndex)
                && currentPad < buffLevelData.PAD)
            {
                return BeholderPadBranchIndex;
            }

            if (thresholdBranchIndex >= BeholderEvaBranchIndex
                && HasBeholderBuffBranch(skill, buffLevelData, BeholderEvaBranchIndex)
                && currentEva < buffLevelData.EVA)
            {
                return BeholderEvaBranchIndex;
            }

            if (thresholdBranchIndex >= BeholderAccBranchIndex
                && HasBeholderBuffBranch(skill, buffLevelData, BeholderAccBranchIndex)
                && currentAcc < buffLevelData.ACC)
            {
                return BeholderAccBranchIndex;
            }

            if (thresholdBranchIndex >= BeholderMddBranchIndex
                && HasBeholderBuffBranch(skill, buffLevelData, BeholderMddBranchIndex)
                && currentMdd < buffLevelData.MDD)
            {
                return BeholderMddBranchIndex;
            }

            if (HasBeholderBuffBranch(skill, buffLevelData, BeholderPddBranchIndex)
                && currentPdd < buffLevelData.PDD)
            {
                return BeholderPddBranchIndex;
            }

            return null;
        }

        internal static string ResolveBeholderBuffBranchName(
            SkillData skill,
            SkillLevelData buffLevelData,
            int currentPad,
            int currentPdd,
            int currentMdd,
            int currentAcc,
            int currentEva,
            int randomValue)
        {
            int? branchIndex = ResolveBeholderBuffBranchIndex(
                skill,
                buffLevelData,
                currentPad,
                currentPdd,
                currentMdd,
                currentAcc,
                currentEva,
                randomValue);
            return branchIndex.HasValue
                ? GetBeholderBranchName(branchIndex.Value)
                : null;
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

        private static bool UsesNamedSummonAnimationBranch(SkillData skill, string branchName)
        {
            return skill?.SummonNamedAnimations != null
                && !string.IsNullOrWhiteSpace(branchName)
                && skill.SummonNamedAnimations.TryGetValue(branchName, out SkillAnimation branchAnimation)
                && branchAnimation?.Frames.Count > 0;
        }

        private static bool HasBeholderBuffBranch(SkillData skill, SkillLevelData buffLevelData, int branchIndex)
        {
            string branchName = GetBeholderBranchName(branchIndex);
            return branchName != null
                   && GetBeholderBranchValue(buffLevelData, branchIndex) > 0
                   && UsesNamedSummonAnimationBranch(skill, branchName);
        }

        private static string GetBeholderBranchName(int branchIndex)
        {
            return branchIndex switch
            {
                BeholderPddBranchIndex => "skill2",
                BeholderMddBranchIndex => "skill3",
                BeholderAccBranchIndex => "skill4",
                BeholderEvaBranchIndex => "skill5",
                BeholderPadBranchIndex => "skill6",
                _ => null
            };
        }

        private static int GetBeholderBranchValue(SkillLevelData buffLevelData, int branchIndex)
        {
            if (buffLevelData == null)
            {
                return 0;
            }

            return branchIndex switch
            {
                BeholderPddBranchIndex => buffLevelData.PDD,
                BeholderMddBranchIndex => buffLevelData.MDD,
                BeholderAccBranchIndex => buffLevelData.ACC,
                BeholderEvaBranchIndex => buffLevelData.EVA,
                BeholderPadBranchIndex => buffLevelData.PAD,
                _ => 0
            };
        }

        private static int GetAnimationDuration(SkillAnimation animation)
        {
            if (animation?.Frames == null || animation.Frames.Count <= 0)
            {
                return 0;
            }

            return animation.TotalDuration > 0
                ? animation.TotalDuration
                : animation.Frames.Sum(frame => frame.Delay);
        }
    }
}
