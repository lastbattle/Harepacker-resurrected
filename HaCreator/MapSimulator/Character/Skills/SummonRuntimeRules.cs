using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class SummonRuntimeRules
    {
        private const int Sg88SkillId = 35121003;
        private const int BeholderSummonSkillId = 1321007;
        private const int HealingRobotSkillId = 35111011;
        private const int HealingRobotInfoType = 33;
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

            SummonAssistType authoredFallbackAssistType =
                ResolveAuthoredAssistTypeForRuntimeOwnershipWithoutActionFamily(
                    skill,
                    SummonAssistType.PeriodicAttack);
            if (authoredFallbackAssistType != SummonAssistType.PeriodicAttack)
            {
                return authoredFallbackAssistType;
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
                   && skill.SkillId == HealingRobotSkillId
                   && skill.ClientInfoType == HealingRobotInfoType
                   && HasMinionAbilityToken(skill.MinionAbility, "heal")
                   && string.Equals(skill.SummonCondition, "whenUserLieDown", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool CanAffectLocalPlayerFromFriendlySupport(SkillData skill, PlayerState localPlayerState)
        {
            return !IsSitdownHealingSupportSummon(skill)
                   || localPlayerState == PlayerState.Sitting;
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
            if (normalizedAction == 0)
            {
                string noActionBranch = ResolveNoActionFamilyPacketSkillBranch(skill, assistType);
                if (!string.IsNullOrWhiteSpace(noActionBranch))
                {
                    return noActionBranch;
                }

                return ResolveClientSummonedActionBranch(skill, normalizedAction);
            }

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

            if (normalizedAction >= PacketSkillActionSkillBranchMin
                && normalizedAction <= PacketSkillActionSkillBranchMax
                && HasSupportOwnedMinionAbilityCue(skill))
            {
                return ResolveSupportOwnedIndexedPacketSkillBranch(skill, normalizedAction);
            }

            if (normalizedAction == PacketSkillActionHealingRobotHeal)
            {
                if (!IsSitdownHealingSupportSummon(skill))
                {
                    return null;
                }

                return ResolveSitdownHealingRequestBranch(skill);
            }

            if (normalizedAction == PacketSkillActionSubsummon)
            {
                // Client action `14` is authored on the `subsummon` family only.
                // Do not alias this action to support/summon fallbacks when missing.
                return ResolveNamedSummonBranch(skill, "subsummon");
            }

            if (normalizedAction == ClientSelfDestructAttackAction)
            {
                return ResolveSelfDestructFinalBranch(
                    skill,
                    assistType ?? ResolveAssistType(skill));
            }

            string clientActionBranch = ResolveClientSummonedActionBranch(skill, normalizedAction);
            if (!string.IsNullOrWhiteSpace(clientActionBranch))
            {
                return clientActionBranch;
            }

            if (IsStrictClientSummonedPacketSkillAction(normalizedAction))
            {
                return null;
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

        internal static bool IsStrictPacketSkillBranchAction(byte packetAction)
        {
            byte normalizedAction = (byte)(packetAction & 0x7F);
            return normalizedAction == PacketSkillActionSubsummon
                   || IsStrictClientSummonedPacketSkillAction(normalizedAction);
        }

        private static bool IsStrictClientSummonedPacketSkillAction(byte normalizedAction)
        {
            // CSummoned::SetAttackAction consumes the decoded client action family.
            // Hit/say/prepare are direct action slots, not assist-family fallbacks.
            return normalizedAction == 15
                   || normalizedAction == 17
                   || normalizedAction == 18;
        }

        internal static SummonAssistType ResolvePacketSkillAssistTypeForRuntimeOwnership(
            SkillData skill,
            byte packetAction,
            SummonAssistType currentAssistType)
        {
            if (skill == null)
            {
                return currentAssistType;
            }

            byte normalizedAction = (byte)(packetAction & 0x7F);
            if (normalizedAction == 0)
            {
                return ResolveAuthoredAssistTypeForRuntimeOwnershipWithoutActionFamily(
                    skill,
                    currentAssistType);
            }

            if (normalizedAction == PacketSkillActionSubsummon)
            {
                return HasAuthoredSummonOwnedPacketSkillBranch(
                        skill,
                        normalizedAction,
                        allowMissingSummonMinionCue: true)
                    ? SummonAssistType.SummonAction
                    : currentAssistType;
            }

            if (normalizedAction == PacketSkillActionHealingRobotHeal
                && HasAuthoredHealingRobotPacketSkillBranch(skill))
            {
                return SummonAssistType.Support;
            }

            if (IsBeholderSupportPacketSkillAction(normalizedAction))
            {
                return skill.SkillId == BeholderSummonSkillId
                       && HasAuthoredSupportOwnedPacketSkillBranch(skill, normalizedAction)
                    ? SummonAssistType.Support
                    : currentAssistType;
            }

            if (normalizedAction >= PacketSkillActionSkillBranchMin
                && normalizedAction <= PacketSkillActionSkillBranchMax)
            {
                bool hasSummonMinionCue = HasMinionAbilityToken(skill.MinionAbility, "summon");
                bool hasSupportMinionCue = HasSupportOwnedMinionAbilityCue(skill);
                bool hasExplicitSummonCue = HasExplicitSummonOwnedPacketSkillBranch(skill, normalizedAction);
                bool hasExplicitSupportCue = HasExplicitSupportOwnedPacketSkillBranch(skill, normalizedAction);

                if (hasSummonMinionCue)
                {
                    return HasAuthoredSummonOwnedPacketSkillBranch(
                            skill,
                            normalizedAction,
                            allowMissingSummonMinionCue: false)
                        ? SummonAssistType.SummonAction
                        : currentAssistType;
                }

                if (hasSupportMinionCue)
                {
                    return HasAuthoredSupportOwnedPacketSkillBranch(skill, normalizedAction)
                        ? SummonAssistType.Support
                        : currentAssistType;
                }

                // When minionAbility does not disambiguate indexed skill actions, keep
                // ownership deterministic only when authored branches expose a single family.
                if (hasExplicitSummonCue && !hasExplicitSupportCue)
                {
                    return HasAuthoredSummonOwnedPacketSkillBranch(
                            skill,
                            normalizedAction,
                            allowMissingSummonMinionCue: true)
                        ? SummonAssistType.SummonAction
                        : currentAssistType;
                }

                if (hasExplicitSupportCue && !hasExplicitSummonCue)
                {
                    return HasAuthoredSupportOwnedPacketSkillBranch(skill, normalizedAction)
                        ? SummonAssistType.Support
                        : currentAssistType;
                }
            }

            return currentAssistType;
        }

        internal static SummonAssistType ResolveAuthoredAssistTypeForRuntimeOwnershipWithoutActionFamily(
            SkillData skill,
            SummonAssistType currentAssistType)
        {
            if (skill == null)
            {
                return currentAssistType;
            }

            bool hasExplicitSummonCue =
                HasExplicitSummonOwnedPacketSkillBranch(skill, PacketSkillActionSubsummon);
            bool hasExplicitSupportCue =
                HasExplicitSupportOwnedPacketSkillBranch(skill, PacketSkillActionHealingRobotHeal);
            if (hasExplicitSummonCue != hasExplicitSupportCue)
            {
                return hasExplicitSummonCue
                    ? SummonAssistType.SummonAction
                    : SummonAssistType.Support;
            }

            // Keep authored conflicting explicit-family cues deterministic: when both
            // summon and support explicit families exist, action-family-missing packets
            // retain current ownership.
            if (hasExplicitSummonCue)
            {
                return currentAssistType;
            }

            // With no explicit family cue (`subsummon` / `heal` / `support`), allow
            // minionAbility to disambiguate ownership only when authored branches
            // expose exactly one family.
            bool hasSummonMinionCue = HasSummonOwnedMinionAbilityCue(skill);
            bool hasSupportMinionCue = HasSupportOwnedMinionAbilityCue(skill);
            if (hasSummonMinionCue == hasSupportMinionCue)
            {
                return currentAssistType;
            }

            if (hasSupportMinionCue)
            {
                return HasAuthoredSupportOwnedNoActionPacketSkillBranch(skill)
                    ? SummonAssistType.Support
                    : currentAssistType;
            }

            return HasAuthoredSummonOwnedNoActionPacketSkillBranch(skill)
                ? SummonAssistType.SummonAction
                : currentAssistType;
        }

        internal static bool HasAuthoredPacketSkillAssistOwnershipBranch(
            SkillData skill,
            byte packetAction,
            SummonAssistType assistType)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return false;
            }

            byte normalizedAction = (byte)(packetAction & 0x7F);
            if (assistType == SummonAssistType.SummonAction)
            {
                if (normalizedAction == 0)
                {
                    return HasExplicitSummonOwnedPacketSkillBranch(skill, PacketSkillActionSubsummon)
                           || HasAuthoredSummonOwnedNoActionPacketSkillBranch(skill);
                }

                if (normalizedAction == PacketSkillActionSubsummon)
                {
                    // Client action `14` is the subsummon family. Do not flip ownership
                    // unless the authored subsummon branch exists.
                    return HasAuthoredSummonOwnedPacketSkillBranch(
                        skill,
                        normalizedAction,
                        allowMissingSummonMinionCue: true);
                }

                if (normalizedAction >= PacketSkillActionSkillBranchMin
                    && normalizedAction <= PacketSkillActionSkillBranchMax)
                {
                    bool hasSummonOwnershipCue = HasMinionAbilityToken(skill.MinionAbility, "summon")
                        || HasExplicitSummonOwnedPacketSkillBranch(skill, normalizedAction);
                    return hasSummonOwnershipCue
                           && HasAuthoredSummonOwnedPacketSkillBranch(
                               skill,
                               normalizedAction,
                               allowMissingSummonMinionCue: true);
                }
            }
            else if (assistType == SummonAssistType.Support)
            {
                if (normalizedAction == 0)
                {
                    return (HasSupportOwnedMinionAbilityCue(skill)
                            || HasExplicitSupportOwnedPacketSkillBranch(
                                skill,
                                PacketSkillActionHealingRobotHeal))
                           && HasAuthoredSupportOwnedNoActionPacketSkillBranch(skill);
                }

                if (normalizedAction == PacketSkillActionHealingRobotHeal)
                {
                    return HasAuthoredHealingRobotPacketSkillBranch(skill);
                }

                if (IsBeholderSupportPacketSkillAction(normalizedAction))
                {
                    if (skill.SkillId != BeholderSummonSkillId)
                    {
                        return false;
                    }

                    return HasAuthoredSupportOwnedPacketSkillBranch(skill, normalizedAction);
                }

                if (normalizedAction >= PacketSkillActionSkillBranchMin
                    && normalizedAction <= PacketSkillActionSkillBranchMax)
                {
                    bool hasSupportMinionCue = HasSupportOwnedMinionAbilityCue(skill);
                    bool hasExplicitSupportCue = HasExplicitSupportOwnedPacketSkillBranch(skill, normalizedAction);
                    if (!hasSupportMinionCue
                        && !hasExplicitSupportCue
                        && skill.SkillId != BeholderSummonSkillId)
                    {
                        return false;
                    }

                    return HasAuthoredSupportOwnedPacketSkillBranch(skill, normalizedAction);
                }
            }

            return !string.IsNullOrWhiteSpace(ResolvePacketSkillBranch(skill, normalizedAction, assistType));
        }

        private static bool HasAuthoredSummonOwnedPacketSkillBranch(
            SkillData skill,
            byte normalizedAction,
            bool allowMissingSummonMinionCue)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return false;
            }

            if (normalizedAction == PacketSkillActionSubsummon)
            {
                return !string.IsNullOrWhiteSpace(ResolveNamedSummonBranch(skill, "subsummon"));
            }

            if (normalizedAction < PacketSkillActionSkillBranchMin
                || normalizedAction > PacketSkillActionSkillBranchMax
                || (!allowMissingSummonMinionCue
                    && !HasMinionAbilityToken(skill.MinionAbility, "summon")))
            {
                return false;
            }

            string indexedBranch = $"skill{normalizedAction}";
            if (normalizedAction == 1)
            {
                return !string.IsNullOrWhiteSpace(
                    ResolveNamedSummonBranch(skill, indexedBranch, "subsummon", "skill2"));
            }

            if (normalizedAction == 2)
            {
                return !string.IsNullOrWhiteSpace(
                    ResolveNamedSummonBranch(skill, indexedBranch, "subsummon", "skill1"));
            }

            return !string.IsNullOrWhiteSpace(ResolveNamedSummonBranch(skill, indexedBranch));
        }

        private static bool HasAuthoredSummonOwnedNoActionPacketSkillBranch(SkillData skill)
        {
            return HasSummonOwnedMinionAbilityCue(skill)
                   && !string.IsNullOrWhiteSpace(ResolveNamedSummonBranch(skill, "subsummon"));
        }

        private static bool HasAuthoredSupportOwnedNoActionPacketSkillBranch(SkillData skill)
        {
            return !string.IsNullOrWhiteSpace(ResolveNoActionSupportOwnedBranch(skill));
        }

        private static bool HasAuthoredHealingRobotPacketSkillBranch(SkillData skill)
        {
            return IsSitdownHealingSupportSummon(skill)
                   && !string.IsNullOrWhiteSpace(ResolveSitdownHealingRequestBranch(skill));
        }

        private static bool HasAuthoredSupportOwnedPacketSkillBranch(SkillData skill, byte normalizedAction)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return false;
            }

            if (normalizedAction == PacketSkillActionHealingRobotHeal)
            {
                return HasAuthoredHealingRobotPacketSkillBranch(skill);
            }

            if (skill.SkillId == BeholderSummonSkillId && IsBeholderSupportPacketSkillAction(normalizedAction))
            {
                if (normalizedAction == PacketSkillActionBeholderHeal)
                {
                    return !string.IsNullOrWhiteSpace(ResolveBeholderHealBranch(skill));
                }

                int branchIndex = normalizedAction - PacketSkillActionBeholderBuffBase;
                return !string.IsNullOrWhiteSpace(GetBeholderBranchName(branchIndex))
                       && !string.IsNullOrWhiteSpace(ResolveNamedSummonBranch(skill, GetBeholderBranchName(branchIndex)));
            }

            if (normalizedAction < PacketSkillActionSkillBranchMin
                || normalizedAction > PacketSkillActionSkillBranchMax)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(
                ResolveSupportOwnedIndexedPacketSkillBranch(skill, normalizedAction));
        }

        private static string ResolveSupportOwnedIndexedPacketSkillBranch(SkillData skill, byte normalizedAction)
        {
            if (normalizedAction < PacketSkillActionSkillBranchMin
                || normalizedAction > PacketSkillActionSkillBranchMax)
            {
                return null;
            }

            string indexedBranch = $"skill{normalizedAction}";
            return ResolveNamedSummonBranch(
                skill,
                indexedBranch,
                "heal",
                "support",
                "skill1",
                "skill2",
                "skill3",
                "skill4",
                "skill5",
                "skill6");
        }

        private static string ResolveNoActionFamilyPacketSkillBranch(
            SkillData skill,
            SummonAssistType? assistType)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return null;
            }

            bool hasExplicitSummonCue =
                HasExplicitSummonOwnedPacketSkillBranch(skill, PacketSkillActionSubsummon);
            bool hasExplicitSupportCue =
                HasExplicitSupportOwnedPacketSkillBranch(skill, PacketSkillActionHealingRobotHeal);
            if (hasExplicitSummonCue != hasExplicitSupportCue)
            {
                return hasExplicitSummonCue
                    ? ResolveNoActionSummonOwnedBranch(skill)
                    : ResolveNoActionSupportOwnedBranch(skill);
            }

            if (hasExplicitSummonCue)
            {
                return assistType switch
                {
                    SummonAssistType.SummonAction => ResolveNoActionSummonOwnedBranch(skill),
                    SummonAssistType.Support => ResolveNoActionSupportOwnedBranch(skill),
                    _ => null
                };
            }

            bool hasSummonMinionCue = HasSummonOwnedMinionAbilityCue(skill);
            bool hasSupportMinionCue = HasSupportOwnedMinionAbilityCue(skill);
            if (hasSummonMinionCue == hasSupportMinionCue)
            {
                return assistType.HasValue
                    ? ResolveAssistOwnedPacketSkillBranch(skill, assistType.Value)
                    : null;
            }

            if (hasSupportMinionCue)
            {
                return HasAuthoredSupportOwnedNoActionPacketSkillBranch(skill)
                    ? ResolveNoActionSupportOwnedBranch(skill)
                    : null;
            }

            return HasAuthoredSummonOwnedNoActionPacketSkillBranch(skill)
                ? ResolveNoActionSummonOwnedBranch(skill)
                : null;
        }

        private static bool HasExplicitSupportOwnedPacketSkillBranch(SkillData skill, byte normalizedAction)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return false;
            }

            if (normalizedAction == PacketSkillActionHealingRobotHeal)
            {
                return !string.IsNullOrWhiteSpace(ResolveNamedSummonBranch(skill, "heal", "support"));
            }

            if (normalizedAction >= PacketSkillActionSkillBranchMin
                && normalizedAction <= PacketSkillActionSkillBranchMax)
            {
                return !string.IsNullOrWhiteSpace(ResolveNamedSummonBranch(skill, "heal", "support"));
            }

            return false;
        }

        private static bool HasExplicitSummonOwnedPacketSkillBranch(SkillData skill, byte normalizedAction)
        {
            if (skill?.SummonNamedAnimations == null || skill.SummonNamedAnimations.Count == 0)
            {
                return false;
            }

            if (normalizedAction == PacketSkillActionSubsummon)
            {
                return !string.IsNullOrWhiteSpace(ResolveNamedSummonBranch(skill, "subsummon"));
            }

            if (normalizedAction < PacketSkillActionSkillBranchMin
                || normalizedAction > PacketSkillActionSkillBranchMax)
            {
                return false;
            }

            // Indexed actions expose an explicit summon-family cue only when `subsummon`
            // exists as an authored branch candidate.
            return (normalizedAction == 1 || normalizedAction == 2)
                && !string.IsNullOrWhiteSpace(ResolveNamedSummonBranch(skill, "subsummon"));
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

            int healingRobotSupportAction = ResolveHealingRobotLocalSupportActionCode(skill, assistType, branchName);
            if (healingRobotSupportAction > 0)
            {
                return healingRobotSupportAction;
            }

            if (IsSitdownHealingSupportSummon(skill))
            {
                return 0;
            }

            if (assistType == SummonAssistType.Support
                && IsSupportOwnedSuspendBranch(skill, assistType, branchName))
            {
                return ClientSupportOwnedAttackAction;
            }

            if (assistType == SummonAssistType.SummonAction
                && string.Equals(
                    branchName,
                    ResolveStrictSubsummonActionBranch(skill),
                    StringComparison.OrdinalIgnoreCase))
            {
                return PacketSkillActionSubsummon;
            }

            return 0;
        }

        internal static int ResolveLocalAttackPacketActionCode(
            SkillData skill,
            SummonAssistType assistType,
            string branchName,
            bool facingRight,
            bool isSelfDestructAction = false)
        {
            int actionCode = ResolveLocalAttackActionCode(
                skill,
                assistType,
                branchName,
                isSelfDestructAction);

            if (actionCode == PacketSkillActionHealingRobotHeal
                && IsSitdownHealingSupportSummon(skill)
                && assistType == SummonAssistType.Support)
            {
                return EncodeHealingRobotSkillAction(actionCode, facingRight);
            }

            return EncodeSummonedActionFacingBit(actionCode, facingRight);
        }

        internal static int EncodeSummonedActionFacingBit(int actionCode, bool facingRight)
        {
            if (actionCode <= 0)
            {
                return 0;
            }

            int normalizedActionCode = actionCode & 0x7F;
            return facingRight
                ? normalizedActionCode
                : normalizedActionCode | 0x80;
        }

        private static int EncodeHealingRobotSkillAction(int actionCode, bool facingRight)
        {
            int normalizedActionCode = actionCode & 0x7F;
            byte moveAction = (byte)(facingRight ? 0 : 1);
            // Client `CSummoned::TryDoingHealingRobot` sends `(moveAction << 7) | 13`.
            return (moveAction << 7) | normalizedActionCode;
        }

        private static int ResolveHealingRobotLocalSupportActionCode(
            SkillData skill,
            SummonAssistType assistType,
            string branchName)
        {
            if (assistType != SummonAssistType.Support
                || !IsSitdownHealingSupportSummon(skill)
                || string.IsNullOrWhiteSpace(branchName))
            {
                return 0;
            }

            string healBranchName = ResolveSupportOwnedBranch(skill, preferHealFirst: true);
            return !string.IsNullOrWhiteSpace(healBranchName)
                   && string.Equals(branchName, healBranchName, StringComparison.OrdinalIgnoreCase)
                ? PacketSkillActionHealingRobotHeal
                : 0;
        }

        internal static bool TryResolveExplicitSelfDestructPlayback(
            SkillData skill,
            SummonAssistType assistType,
            string currentBranchName,
            out string branchName,
            out int actionCode)
        {
            branchName = currentBranchName;
            if (string.IsNullOrWhiteSpace(branchName))
            {
                branchName = ResolveSelfDestructFinalBranch(skill, assistType);
            }

            actionCode = ResolveLocalAttackActionCode(
                skill,
                assistType,
                branchName,
                isSelfDestructAction: true);
            return !string.IsNullOrWhiteSpace(branchName) && actionCode > 0;
        }

        internal static string ResolveLocalSupportBranch(SkillData skill, bool preferHealFirst)
        {
            return ResolveSupportOwnedBranch(skill, preferHealFirst);
        }

        internal static string ResolveSitdownHealingRequestBranch(SkillData skill)
        {
            return IsSitdownHealingSupportSummon(skill)
                ? ResolveNamedSummonBranch(skill, "heal", "support")
                : null;
        }

        internal static string ResolveLocalSummonActionBranch(SkillData skill)
        {
            return ResolveNamedSummonBranch(skill, "subsummon", "skill1", "skill2")
                   ?? ResolveAuthoredCustomSummonSkillBranch(skill, 0);
        }

        private static string ResolveNoActionSummonOwnedBranch(SkillData skill)
        {
            return ResolveNamedSummonBranch(skill, "subsummon");
        }

        private static string ResolveNoActionSupportOwnedBranch(SkillData skill)
        {
            if (skill == null)
            {
                return null;
            }

            return HasMinionAbilityToken(skill.MinionAbility, "heal")
                ? ResolveNamedSummonBranch(skill, "heal", "support", "stand")
                : ResolveNamedSummonBranch(skill, "support", "heal", "stand");
        }

        internal static string ResolveStrictSubsummonActionBranch(SkillData skill)
        {
            return ResolveNamedSummonBranch(skill, "subsummon");
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
            if (preferHealFirst && IsSitdownHealingSupportSummon(skill))
            {
                return ResolveNamedSummonBranch(skill, "heal", "support", "skill1", "skill2", "stand");
            }

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

        internal static Rectangle ResolveSitdownHealingAttemptRange(SkillData skill, bool facingRight)
        {
            return TryResolveSitdownHealingAttemptRange(skill, facingRight, out Rectangle range)
                ? range
                : Rectangle.Empty;
        }

        internal static bool TryResolveSitdownHealingAttemptRange(
            SkillData skill,
            bool facingRight,
            out Rectangle range)
        {
            range = Rectangle.Empty;
            if (!IsSitdownHealingSupportSummon(skill))
            {
                return false;
            }

            string healBranchName = ResolveSupportOwnedBranch(skill, preferHealFirst: true);
            if (string.IsNullOrWhiteSpace(healBranchName))
            {
                return skill.TryGetSummonAttackRange(facingRight, branchName: null, out range)
                       && !range.IsEmpty;
            }

            return skill.TryGetSummonAttackRange(facingRight, healBranchName, out range)
                   && !range.IsEmpty;
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
            return ResolveTrackedSuspendDurationMs(
                skill,
                SummonAssistType.Support,
                preferHealFirst,
                explicitBranchName);
        }

        internal static int ResolveTrackedSuspendDurationMs(
            SkillData skill,
            SummonAssistType assistType,
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
                branchName = assistType == SummonAssistType.SummonAction
                    ? ResolveLocalSummonActionBranch(skill)
                    : ResolveSupportOwnedBranch(skill, preferHealFirst);
            }

            if (string.IsNullOrWhiteSpace(branchName)
                || !IsTrackedSuspendBranch(skill, assistType, branchName)
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
            if ((assistType != SummonAssistType.Support
                 && assistType != SummonAssistType.SummonAction)
                || skill == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(explicitBranchName)
                && !IsTrackedSuspendBranch(skill, assistType, explicitBranchName))
            {
                return false;
            }

            bool preferHealFirst = preferHealFirstOverride
                ?? HasMinionAbilityToken(skill.MinionAbility, "heal");
            return ResolveTrackedSuspendDurationMs(
                       skill,
                       assistType,
                       preferHealFirst,
                       explicitBranchName) > 0
                   || fallbackDurationMs > 0;
        }

        private static bool IsTrackedSuspendBranch(
            SkillData skill,
            SummonAssistType assistType,
            string branchName)
        {
            if (assistType == SummonAssistType.SummonAction)
            {
                return IsSummonActionSuspendBranch(skill, branchName);
            }

            return IsSupportOwnedSuspendBranch(skill, assistType, branchName);
        }

        private static bool IsSummonActionSuspendBranch(
            SkillData skill,
            string branchName)
        {
            if (skill?.SummonNamedAnimations == null
                || string.IsNullOrWhiteSpace(branchName))
            {
                return false;
            }

            foreach (string candidate in EnumerateSummonActionSuspendBranchCandidates(skill))
            {
                if (!string.IsNullOrWhiteSpace(candidate)
                    && string.Equals(candidate, branchName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateSummonActionSuspendBranchCandidates(SkillData skill)
        {
            yield return ResolveLocalSummonActionBranch(skill);
            yield return ResolveSelfDestructFinalBranch(skill, SummonAssistType.SummonAction);
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
                   && IsSitdownHealingSupportSummon(summon.SkillData)
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

        internal static bool CanInitiateTeslaCoilAttack(ActiveSummon summon, int teslaCoilSkillId)
        {
            return summon != null
                   && !summon.IsPendingRemoval
                   && summon.SkillId == teslaCoilSkillId
                   && summon.TeslaCoilState == 1;
        }

        internal static bool CanInitiateTeslaCoilAttackGroup(
            IEnumerable<ActiveSummon> summons,
            int teslaCoilSkillId,
            int requiredCount)
        {
            if (summons == null || requiredCount <= 0)
            {
                return false;
            }

            int armedCount = 0;
            foreach (ActiveSummon summon in summons)
            {
                if (!CanInitiateTeslaCoilAttack(summon, teslaCoilSkillId))
                {
                    return false;
                }

                armedCount++;
            }

            return armedCount >= requiredCount;
        }

        internal static byte ResolveTeslaCoilIdleRuntimeState(byte currentState, bool hasActiveOneTimeActionPlayback)
        {
            return currentState == 2 && !hasActiveOneTimeActionPlayback
                ? (byte)1
                : currentState;
        }

        internal static bool TryRearmTeslaCoilAfterActionPlayback(
            ActiveSummon summon,
            int teslaCoilSkillId,
            bool hasActiveOneTimeActionPlayback)
        {
            if (summon?.SkillId != teslaCoilSkillId)
            {
                return false;
            }

            byte resolvedState = ResolveTeslaCoilIdleRuntimeState(
                summon.TeslaCoilState,
                hasActiveOneTimeActionPlayback);
            if (resolvedState == summon.TeslaCoilState)
            {
                return false;
            }

            summon.TeslaCoilState = resolvedState;
            if (resolvedState == 1)
            {
                ClearTeslaCoilAttackPlaybackOwnership(summon);
            }

            return true;
        }

        internal static void RearmTeslaCoilForRefresh(ActiveSummon summon, int teslaCoilSkillId)
        {
            if (summon?.SkillId != teslaCoilSkillId)
            {
                return;
            }

            summon.TeslaCoilState = 1;
            ClearTeslaCoilAttackPlaybackOwnership(summon);
        }

        private static void ClearTeslaCoilAttackPlaybackOwnership(ActiveSummon summon)
        {
            summon.TeslaTrianglePoints = Array.Empty<Point>();
            summon.CurrentAnimationBranchName = null;
            summon.LastAttackAnimationStartTime = int.MinValue;
            summon.OneTimeActionFallbackAnimation = null;
            summon.OneTimeActionFallbackStartTime = int.MinValue;
            summon.OneTimeActionFallbackAnimationTime = int.MinValue;
            summon.OneTimeActionFallbackEndTime = int.MinValue;
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
                    "subsummon"),
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

        private static bool IsBeholderSupportPacketSkillAction(byte normalizedAction)
        {
            return normalizedAction == PacketSkillActionBeholderHeal
                   || (normalizedAction >= PacketSkillActionBeholderBuffBase
                       && normalizedAction <= PacketSkillActionBeholderBuffMax);
        }

        private static bool HasSupportOwnedMinionAbilityCue(SkillData skill)
        {
            return HasMinionAbilityToken(skill?.MinionAbility, "heal")
                   || HasMinionAbilityToken(skill?.MinionAbility, "mes")
                   || HasMinionAbilityToken(skill?.MinionAbility, "amplifyDamage");
        }

        private static bool HasSummonOwnedMinionAbilityCue(SkillData skill)
        {
            return HasMinionAbilityToken(skill?.MinionAbility, "summon");
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
