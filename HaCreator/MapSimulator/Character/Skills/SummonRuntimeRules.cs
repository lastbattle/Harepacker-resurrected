using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class SummonRuntimeRules
    {
        private const int Sg88SkillId = 35121003;
        private const int BeholderSummonSkillId = 1321007;
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
        private const int ClientDefaultSummonAttackActionBase = 4;
        private const int ClientTeslaTriangleAttackAction = 6;
        private const int ClientSupportOwnedAttackAction = 13;
        private const int ClientSelfDestructAttackAction = 16;
        private static readonly string[] ClientSummonSpawnActionNames =
        {
            "summoned",
            "create",
            "summon"
        };
        private static readonly string[] ClientSummonIdleActionNames =
        {
            "stand",
            "fly",
            "move",
            "walk",
            "repeat"
        };
        private static readonly string[] ClientSummonPrepareActionNames =
        {
            "prepare"
        };
        private static readonly string[] ClientSummonRemovalActionNames =
        {
            "die",
            "die1"
        };
        private static readonly string[] ClientSummonHitActionNames =
        {
            "hit",
            "hit/0",
            "hit/0/1"
        };
        private static readonly string[] ClientSummonSupportActionNames =
        {
            "heal",
            "support"
        };
        private static readonly string[] ClientSummonedActionNamesByAction =
        {
            "stand",
            "move",
            "fly",
            "summoned",
            "attack1",
            "attack2",
            "attackTriangle",
            "skill1",
            "skill2",
            "skill3",
            "skill4",
            "skill5",
            "skill6",
            "heal",
            "subsummon",
            "hit",
            "die",
            "say",
            "prepare"
        };

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

            if (skill.SkillId == BeholderSummonSkillId)
            {
                return SummonAssistType.Support;
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

        public static int ResolveSummonImpactDelayMs(
            SkillData skill,
            int authoredDelayMs,
            string branchName)
        {
            return Math.Max(0, authoredDelayMs)
                   + ResolveSummonActionPrepareDurationMs(skill, branchName);
        }

        public static int ResolveSummonImpactExecutionDelayMs(
            SkillData skill,
            int authoredDelayMs,
            string branchName)
        {
            return ResolveSummonImpactDelayMs(skill, authoredDelayMs, branchName);
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
                return ResolvePacketSpecialBranch(
                    skill,
                    assistType,
                    "skill1",
                    "heal",
                    "support");
            }

            if (normalizedAction >= PacketSkillActionBeholderBuffBase
                && normalizedAction <= PacketSkillActionBeholderBuffMax)
            {
                string branchName = $"skill{normalizedAction - PacketSkillActionBeholderBuffBase + 2}";
                return ResolvePacketSpecialBranch(
                    skill,
                    assistType,
                    branchName,
                    "support",
                    "heal");
            }

            if (normalizedAction == PacketSkillActionHealingRobotHeal)
            {
                return ResolvePacketSpecialBranch(
                    skill,
                    assistType,
                    "heal",
                    "support");
            }

            if (normalizedAction == PacketSkillActionSubsummon)
            {
                return ResolvePacketSpecialBranch(
                    skill,
                    assistType,
                    "subsummon")
                       ?? ResolveAuthoredCustomSummonSkillBranch(skill, 0);
            }

            if (normalizedAction == ClientSelfDestructAttackAction)
            {
                return ResolveSelfDestructFinalBranch(
                    skill,
                    assistType ?? ResolveAssistType(skill));
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
                return ResolveNamedSummonBranch(skill, "subsummon", "skill1", "skill2")
                       ?? ResolveAuthoredCustomSummonSkillBranch(skill, 0);
            }

            return null;
        }

        public static string ResolvePacketAttackBranch(SkillData skill, byte packetAction)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return null;
            }

            int normalizedAction = packetAction & 0x7F;
            string specialBranch = ResolveClientSpecificPacketAttackBranch(skill, normalizedAction);
            if (!string.IsNullOrWhiteSpace(specialBranch))
            {
                return specialBranch;
            }

            if (normalizedAction < ClientDefaultSummonAttackActionBase)
            {
                return null;
            }

            int attackIndex = normalizedAction - ClientDefaultSummonAttackActionBase + 1;
            return ResolveClientSummonedActionBranch(skill, normalizedAction)
                   ?? ResolveNamedSummonBranch(skill, EnumeratePacketAttackBranchCandidates(attackIndex))
                   ?? ResolveAuthoredCustomSummonSkillBranch(skill, attackIndex - 1);
        }

        internal static string ResolveSpawnPlaybackBranch(SkillData skill)
        {
            return ResolveSummonPlaybackBranch(skill, skill?.SummonSpawnBranchName, ClientSummonSpawnActionNames);
        }

        internal static string ResolveIdlePlaybackBranch(SkillData skill)
        {
            return ResolveSummonPlaybackBranch(skill, skill?.SummonIdleBranchName, ClientSummonIdleActionNames);
        }

        internal static string ResolvePreparePlaybackBranch(SkillData skill)
        {
            return ResolveSummonPlaybackBranch(skill, skill?.SummonPrepareBranchName, ClientSummonPrepareActionNames);
        }

        internal static string ResolveRemovalPlaybackBranch(SkillData skill)
        {
            return ResolveSummonPlaybackBranch(skill, skill?.SummonRemovalBranchName, ClientSummonRemovalActionNames);
        }

        internal static string ResolveHitPlaybackBranch(SkillData skill)
        {
            return ResolveSummonPlaybackBranch(skill, skill?.SummonHitBranchName, ClientSummonHitActionNames);
        }

        private static string ResolvePacketSpecialBranch(
            SkillData skill,
            SummonAssistType? assistType,
            params string[] preferredBranchNames)
        {
            string branchName = ResolveNamedSummonBranch(skill, preferredBranchNames);
            if (!string.IsNullOrWhiteSpace(branchName))
            {
                return branchName;
            }

            return assistType.HasValue
                ? ResolveAssistOwnedPacketSkillBranch(skill, assistType.Value)
                : null;
        }

        internal static string ResolveBeholderHealBranch(SkillData skill)
        {
            return ResolveNamedSummonBranch(skill, "skill1", "heal", "support");
        }

        internal static string ResolveLocalAttackBranch(SkillData skill)
        {
            if (skill == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(skill.SummonAttackBranchName)
                && skill.SummonNamedAnimations?.ContainsKey(skill.SummonAttackBranchName) == true)
            {
                return skill.SummonAttackBranchName;
            }

            return ResolvePacketAttackBranch(skill, ClientDefaultSummonAttackActionBase)
                   ?? ResolveNamedSummonBranch(skill, "attack1", "attack", "attack0", "attackTriangle");
        }

        internal static int ResolveLocalAttackActionCode(SkillData skill, SummonAssistType assistType, string branchName, bool isSelfDestructAction = false)
        {
            if (skill == null || string.IsNullOrWhiteSpace(branchName))
            {
                return 0;
            }

            if (isSelfDestructAction
                && string.Equals(
                    branchName,
                    ResolveSelfDestructFinalBranch(skill, assistType),
                    StringComparison.OrdinalIgnoreCase))
            {
                return ClientSelfDestructAttackAction;
            }

            if (skill.SkillId == 35111002
                && string.Equals(branchName, "attackTriangle", StringComparison.OrdinalIgnoreCase))
            {
                return ClientTeslaTriangleAttackAction;
            }

            int beholderSupportAction = ResolveBeholderLocalSupportActionCode(skill, branchName);
            if (beholderSupportAction > 0)
            {
                return beholderSupportAction;
            }

            if (assistType == SummonAssistType.Support
                && IsSupportOwnedSuspendBranch(skill, assistType, branchName))
            {
                return ClientSupportOwnedAttackAction;
            }

            if (assistType == SummonAssistType.SummonAction
                && string.Equals(
                    branchName,
                    ResolveLocalSummonActionBranch(skill),
                    StringComparison.OrdinalIgnoreCase))
            {
                return PacketSkillActionSubsummon;
            }

            return 0;
        }

        internal static string ResolveLocalSupportBranch(SkillData skill, bool preferHealFirst)
        {
            return ResolveSupportOwnedBranch(skill, preferHealFirst);
        }

        internal static string ResolveLocalSummonActionBranch(SkillData skill)
        {
            return ResolveNamedSummonBranch(skill, "subsummon", "skill1", "skill2")
                   ?? ResolveAuthoredCustomSummonSkillBranch(skill, 0);
        }

        internal static string ResolveEmptyActionRetryBranch(SkillData skill)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return null;
            }

            return UsesFlyStyleEmptyActionRetry(skill)
                ? ResolveNamedSummonBranch(skill, "fly")
                : ResolveNamedSummonBranch(skill, "stand");
        }

        private static int ResolveBeholderLocalSupportActionCode(SkillData skill, string branchName)
        {
            if (skill?.SkillId != BeholderSummonSkillId || string.IsNullOrWhiteSpace(branchName))
            {
                return 0;
            }

            string healBranchName = ResolveBeholderHealBranch(skill);
            if (!string.IsNullOrWhiteSpace(healBranchName)
                && string.Equals(branchName, healBranchName, StringComparison.OrdinalIgnoreCase))
            {
                return PacketSkillActionBeholderHeal;
            }

            for (int branchIndex = BeholderPddBranchIndex; branchIndex <= BeholderPadBranchIndex; branchIndex++)
            {
                string buffBranchName = GetBeholderBranchName(branchIndex);
                if (!string.IsNullOrWhiteSpace(buffBranchName)
                    && string.Equals(branchName, buffBranchName, StringComparison.OrdinalIgnoreCase))
                {
                    return PacketSkillActionBeholderBuffBase + branchIndex;
                }
            }

            return 0;
        }

        private static string ResolveAssistOwnedPacketSkillBranch(SkillData skill, SummonAssistType assistType)
        {
            if (skill == null)
            {
                return null;
            }

            return assistType switch
            {
                SummonAssistType.Support when skill.SkillId == BeholderSummonSkillId
                    => ResolveNamedSummonBranch(skill, "skill1", "heal", "support", "stand"),
                SummonAssistType.Support when HasMinionAbilityToken(skill.MinionAbility, "heal")
                    => ResolveSupportOwnedBranch(skill, preferHealFirst: true),
                SummonAssistType.Support when HasMinionAbilityToken(skill.MinionAbility, "mes")
                                                  || HasMinionAbilityToken(skill.MinionAbility, "amplifyDamage")
                    => ResolveSupportOwnedBranch(skill, preferHealFirst: false),
                SummonAssistType.Support
                    => ResolveSupportOwnedBranch(skill, preferHealFirst: false),
                SummonAssistType.SummonAction
                    => ResolveNamedSummonBranch(skill, "subsummon", "skill1", "skill2")
                       ?? ResolveAuthoredCustomSummonSkillBranch(skill, 0),
                _ => null
            };
        }

        public static string ResolveSelfDestructFinalBranch(SkillData skill, SummonAssistType assistType)
        {
            if (skill == null)
            {
                return null;
            }

            if (skill.SelfDestructMinion)
            {
                string selfDestructBranch = ResolveNamedSummonBranch(
                    skill,
                    EnumeratePacketSelfDestructAttackBranchCandidates(skill));
                if (!string.IsNullOrWhiteSpace(selfDestructBranch))
                {
                    return selfDestructBranch;
                }
            }

            if (skill.SkillId == BeholderSummonSkillId)
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
                    => ResolveNamedSummonBranch(skill, "subsummon", "skill1", "skill2")
                       ?? ResolveAuthoredCustomSummonSkillBranch(skill, 0),
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

        internal static Rectangle ResolveSupportOwnedExpiryRange(
            SkillData skill,
            bool facingRight,
            string selfDestructBranchName)
        {
            Rectangle supportRange = ResolveSupportOwnedRange(skill, facingRight);
            if (skill == null
                || string.IsNullOrWhiteSpace(selfDestructBranchName)
                || !skill.TryGetSummonAttackRange(facingRight, selfDestructBranchName, out Rectangle selfDestructRange))
            {
                return supportRange;
            }

            if (supportRange.IsEmpty)
            {
                return selfDestructRange;
            }

            if (selfDestructRange.IsEmpty)
            {
                return supportRange;
            }

            return Rectangle.Union(supportRange, selfDestructRange);
        }

        private static string ResolvePacketIndexedSkillBranch(SkillData skill, byte normalizedAction)
        {
            if (normalizedAction < PacketSkillActionSkillBranchMin
                || normalizedAction > PacketSkillActionSkillBranchMax)
            {
                return null;
            }

            return ResolveNamedSummonBranch(
                       skill,
                       EnumeratePacketSkillBranchCandidates(skill, normalizedAction))
                   ?? ResolveAuthoredCustomSummonSkillBranch(
                       skill,
                       normalizedAction - PacketSkillActionSkillBranchMin);
        }

        private static string ResolveAuthoredCustomSummonSkillBranch(SkillData skill, int customActionIndex)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return null;
            }

            if (customActionIndex < 0)
            {
                customActionIndex = 0;
            }

            int currentIndex = 0;
            foreach (string branchName in EnumerateAuthoredCustomSummonSkillBranches(skill))
            {
                if (currentIndex == customActionIndex)
                {
                    return branchName;
                }

                currentIndex++;
            }

            return null;
        }

        private static IEnumerable<string> EnumerateAuthoredCustomSummonSkillBranches(SkillData skill)
        {
            if (skill?.SummonNamedAnimations == null)
            {
                yield break;
            }

            foreach (string branchName in skill.SummonNamedAnimations.Keys)
            {
                if (IsAuthoredCustomSummonSkillBranch(branchName))
                {
                    yield return branchName;
                }
            }
        }

        private static bool UsesFlyStyleEmptyActionRetry(SkillData skill)
        {
            int moveAbility = skill?.SummonMoveAbility ?? 0;
            return moveAbility == 4 || moveAbility == 5;
        }

        private static bool IsAuthoredCustomSummonSkillBranch(string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return false;
            }

            if (branchName.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                || branchName.StartsWith("skill", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(branchName, "prepare", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, "summon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, "create", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, "summoned", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, "die", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, "die1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, "effect", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, "effect0", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, "hit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, "hit/0", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, "hit/0/1", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsNamedClientSummonBranch(branchName, ClientSummonSpawnActionNames)
                || IsNamedClientSummonBranch(branchName, ClientSummonIdleActionNames)
                || IsNamedClientSummonBranch(branchName, ClientSummonSupportActionNames)
                || SkillLoader.IsRepeatStyleSummonBranchName(branchName))
            {
                return false;
            }

            return true;
        }

        private static bool IsNamedClientSummonBranch(string branchName, IEnumerable<string> candidates)
        {
            if (string.IsNullOrWhiteSpace(branchName) || candidates == null)
            {
                return false;
            }

            foreach (string candidate in candidates)
            {
                if (string.Equals(branchName, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

        internal static bool ShouldTrackSupportSuspendWindow(
            SkillData skill,
            SummonAssistType assistType,
            bool? preferHealFirstOverride = null,
            string explicitBranchName = null,
            int fallbackDurationMs = 0)
        {
            if (assistType != SummonAssistType.Support || skill == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(explicitBranchName)
                && !IsSupportOwnedSuspendBranch(skill, assistType, explicitBranchName))
            {
                return false;
            }

            bool preferHealFirst = preferHealFirstOverride
                ?? HasMinionAbilityToken(skill.MinionAbility, "heal");
            return ResolveSupportSuspendDurationMs(
                       skill,
                       preferHealFirst,
                       explicitBranchName) > 0
                   || fallbackDurationMs > 0;
        }

        internal static bool ShouldClearSupportSuspend(ActiveSummon summon, int currentTime)
        {
            return summon != null
                   && ShouldTrackSupportSuspendWindow(
                       summon.SkillData,
                       summon.AssistType,
                       explicitBranchName: summon.CurrentAnimationBranchName)
                   && summon.SupportSuspendUntilTime != int.MinValue
                   && currentTime >= summon.SupportSuspendUntilTime;
        }

        internal static bool ShouldClearHealingRobotSupportSuspend(ActiveSummon summon, int currentTime, int healingRobotSkillId)
        {
            return summon?.SkillId == healingRobotSkillId
                   && ShouldClearSupportSuspend(summon, currentTime);
        }

        internal static bool HasActiveOneTimeActionPlayback(ActiveSummon summon, int currentTime)
        {
            if (summon?.OneTimeActionFallbackAnimation?.Frames.Count <= 0
                || summon.OneTimeActionFallbackAnimationTime == int.MinValue)
            {
                return false;
            }

            int totalDuration = GetAnimationDuration(summon.OneTimeActionFallbackAnimation);
            if (totalDuration <= 0)
            {
                return summon.OneTimeActionFallbackEndTime > currentTime;
            }

            int baseAnimationTime = Math.Max(0, summon.OneTimeActionFallbackAnimationTime);
            int remainingDuration = Math.Max(0, totalDuration - Math.Min(baseAnimationTime, totalDuration));
            if (remainingDuration <= 0)
            {
                return false;
            }

            int fallbackStartTime = summon.OneTimeActionFallbackStartTime == int.MinValue
                ? currentTime
                : summon.OneTimeActionFallbackStartTime;
            int elapsed = Math.Max(0, currentTime - fallbackStartTime);
            return elapsed < remainingDuration
                   && (summon.OneTimeActionFallbackEndTime == int.MinValue
                       || currentTime < summon.OneTimeActionFallbackEndTime);
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
            return ResolveNamedSummonBranch(skill, (IEnumerable<string>)preferredBranchNames);
        }

        private static string ResolveNamedSummonBranch(SkillData skill, IEnumerable<string> preferredBranchNames)
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

        private static string ResolveClientSpecificPacketAttackBranch(SkillData skill, int normalizedAction)
        {
            return normalizedAction switch
            {
                ClientTeslaTriangleAttackAction => ResolveNamedSummonBranch(
                    skill,
                    "attackTriangle",
                    "attack1",
                    "attack",
                    "attack0"),
            ClientSupportOwnedAttackAction => ResolveNamedSummonBranch(
                skill,
                EnumeratePacketSupportOwnedAttackBranchCandidates(skill)),
                PacketSkillActionSubsummon => ResolveNamedSummonBranch(
                    skill,
                    "subsummon",
                    "skill1",
                    "skill2")
                   ?? ResolveAuthoredCustomSummonSkillBranch(skill, 0),
            ClientSelfDestructAttackAction => ResolveNamedSummonBranch(
                skill,
                EnumeratePacketSelfDestructAttackBranchCandidates(skill)),
            _ => null
        };
        }

        private static string ResolveClientSummonedActionBranch(SkillData skill, int normalizedAction)
        {
            if (skill?.SummonNamedAnimations == null
                || normalizedAction < 0
                || normalizedAction >= ClientSummonedActionNamesByAction.Length)
            {
                return null;
            }

            return ResolveNamedSummonBranch(skill, ClientSummonedActionNamesByAction[normalizedAction]);
        }

        private static string ResolveSummonPlaybackBranch(
            SkillData skill,
            string explicitBranchName,
            params string[] clientActionNames)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return explicitBranchName;
            }

            if (!string.IsNullOrWhiteSpace(explicitBranchName)
                && skill.SummonNamedAnimations.ContainsKey(explicitBranchName))
            {
                return explicitBranchName;
            }

            return ResolveNamedSummonBranch(skill, clientActionNames);
        }

        private static string[] EnumeratePacketAttackBranchCandidates(int attackIndex)
        {
            string indexedBranch = $"attack{Math.Max(1, attackIndex)}";
            return attackIndex switch
            {
                1 => new[] { indexedBranch, "attack", "attack0", "attackTriangle" },
                2 => new[] { indexedBranch, "attack", "attackTriangle", "attack1", "attack0" },
                _ => new[] { indexedBranch, "attack", "attackTriangle", "attack1", "attack0" }
            };
        }

        private static string[] EnumeratePacketSupportOwnedAttackBranchCandidates(SkillData skill)
        {
            string primarySupportBranch = ResolveSupportOwnedBranch(skill, preferHealFirst: true);
            string fallbackSupportBranch = ResolveSupportOwnedBranch(skill, preferHealFirst: false);
            return new[]
            {
                primarySupportBranch,
                fallbackSupportBranch,
                "heal",
                "support",
                "skill1",
                "skill2",
                "skill3",
                "skill4",
                "skill5",
                "skill6",
                "stand",
                "attack1",
                "attack",
                "attack0"
            };
        }

        private static bool IsSupportOwnedSuspendBranch(
            SkillData skill,
            SummonAssistType assistType,
            string branchName)
        {
            if (assistType != SummonAssistType.Support
                || skill?.SummonNamedAnimations == null
                || string.IsNullOrWhiteSpace(branchName))
            {
                return false;
            }

            foreach (string candidate in EnumerateSupportOwnedSuspendBranchCandidates(skill, assistType))
            {
                if (!string.IsNullOrWhiteSpace(candidate)
                    && string.Equals(candidate, branchName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateSupportOwnedSuspendBranchCandidates(
            SkillData skill,
            SummonAssistType assistType)
        {
            yield return ResolveSupportOwnedBranch(skill, preferHealFirst: true);
            yield return ResolveSupportOwnedBranch(skill, preferHealFirst: false);

            if (assistType == SummonAssistType.Support)
            {
                yield return ResolveSelfDestructFinalBranch(skill, assistType);

                if (skill?.SkillId == BeholderSummonSkillId)
                {
                    foreach (string branchName in EnumerateBeholderSupportBranchNames(skill))
                    {
                        yield return branchName;
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateBeholderSupportBranchNames(SkillData skill)
        {
            yield return ResolveBeholderHealBranch(skill);

            for (int branchIndex = BeholderPddBranchIndex; branchIndex <= BeholderPadBranchIndex; branchIndex++)
            {
                yield return GetBeholderBranchName(branchIndex);
            }
        }

        private static string[] EnumeratePacketSelfDestructAttackBranchCandidates(SkillData skill)
        {
            return new[]
            {
                skill?.SummonRemovalBranchName,
                "die1",
                "die",
                skill?.SummonAttackBranchName,
                "attack1",
                "attack",
                "attack0"
            };
        }

        private static string[] EnumeratePacketSkillBranchCandidates(
            SkillData skill,
            byte normalizedAction)
        {
            string indexedBranch = $"skill{normalizedAction}";
            if (normalizedAction == 1 && HasMinionAbilityToken(skill?.MinionAbility, "heal"))
            {
                return new[] { indexedBranch, "heal", "support", "skill2" };
            }

            if (normalizedAction == 1 && HasMinionAbilityToken(skill?.MinionAbility, "summon"))
            {
                return new[] { indexedBranch, "subsummon", "skill2" };
            }

            if (normalizedAction == 2 && HasMinionAbilityToken(skill?.MinionAbility, "summon"))
            {
                return new[] { indexedBranch, "subsummon", "skill1" };
            }

            return new[] { indexedBranch };
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
