using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

namespace HaCreator.MapSimulator.Pools
{
    public enum RemotePlayerAffectedAreaDisposition
    {
        NeutralUnknown = 0,
        FriendlySupport = 1,
        Hostile = 2
    }

    public static class RemoteAffectedAreaSupportResolver
    {
        private static readonly string[] FriendlyAreaDescriptionTokens =
        {
            "ally",
            "allies",
            "nearby ally",
            "nearby allies",
            "all allies",
            "party member",
            "party members",
            "team member",
            "team members",
            "nearby party",
            "nearby team",
            "all party",
            "all team"
        };

        private static readonly string[] FriendlyPartyDescriptionTokens =
        {
            "party member",
            "party members",
            "nearby party",
            "all party"
        };

        private static readonly string[] FriendlyTeamDescriptionTokens =
        {
            "team member",
            "team members",
            "nearby team",
            "all team"
        };

        private static readonly string[] FriendlyAreaSupportTokens =
        {
            "heal",
            "recovery",
            "regeneration",
            "protect",
            "protective",
            "protection",
            "shield",
            "barrier",
            "wall",
            "absorb",
            "resistance",
            "abnormal condition",
            "abnormal conditions",
            "magic effect",
            "magic effects",
            "dispel",
            "remove",
            "damage",
            "attack",
            "defense",
            "haste",
            "speed",
            "aura",
            "avoidability",
            "dodge chance",
            "movement speed",
            "attack speed"
        };

        private static readonly string[] FriendlyAreaCleanseTokens =
        {
            "remove all abnormal conditions",
            "removing all abnormal conditions",
            "remove abnormal conditions",
            "removing abnormal conditions",
            "cure abnormal conditions",
            "nullifies all enemy magic effects"
        };

        private static readonly PlayerMobStatusEffect[] SupportedAreaCleanseEffects =
        {
            PlayerMobStatusEffect.Seal,
            PlayerMobStatusEffect.Darkness,
            PlayerMobStatusEffect.Weakness,
            PlayerMobStatusEffect.Stun,
            PlayerMobStatusEffect.Poison,
            PlayerMobStatusEffect.Slow,
            PlayerMobStatusEffect.Freeze,
            PlayerMobStatusEffect.Curse,
            PlayerMobStatusEffect.PainMark,
            PlayerMobStatusEffect.Attract,
            PlayerMobStatusEffect.ReverseInput,
            PlayerMobStatusEffect.Undead
        };

        private static readonly string[] HostileAreaTokens =
        {
            "poison",
            "venom",
            "burn",
            "flame",
            "mist",
            "cloud",
            "freeze",
            "ice",
            "blizzard",
            "frost",
            "stun",
            "paraly",
            "shock",
            "seal",
            "blind",
            "dark",
            "darkness",
            "slow",
            "web",
            "weak",
            "curse"
        };

        private static readonly string[] HostileSummonSupportTokens =
        {
            "enemy",
            "enemies",
            "monster",
            "monsters"
        };

        public static bool IsAreaBuffItemType(int areaType)
        {
            return areaType == 3;
        }

        public static bool IsRecoveryZone(SkillData skill, SkillLevelData levelData = null)
        {
            if (skill == null)
            {
                return false;
            }

            if (skill.IsHeal)
            {
                return true;
            }

            if (levelData != null && (levelData.HP > 0 || levelData.MP > 0))
            {
                return true;
            }

            return ContainsToken(skill.ZoneType, "heal", "recovery", "regen")
                   || ContainsToken(skill.Name, "heal", "recovery", "regeneration")
                   || ContainsToken(skill.Description, "heal", "recovery", "regeneration");
        }

        public static bool IsSupportZone(SkillData skill, SkillLevelData levelData = null)
        {
            return IsInvincibleZone(skill) || IsRecoveryZone(skill, levelData);
        }

        internal static bool IsStatusCleansingZone(SkillData skill, IEnumerable<SkillData> supportSkills = null)
        {
            if (HasStatusCleansingMetadata(skill))
            {
                return true;
            }

            if (supportSkills == null)
            {
                return false;
            }

            foreach (SkillData supportSkill in supportSkills)
            {
                if (supportSkill == null || ReferenceEquals(supportSkill, skill))
                {
                    continue;
                }

                if (HasStatusCleansingMetadata(supportSkill))
                {
                    return true;
                }
            }

            return false;
        }

        internal static IReadOnlyList<PlayerMobStatusEffect> GetSupportedAreaCleanseEffects()
        {
            return SupportedAreaCleanseEffects;
        }

        public static bool HasProjectableSupportBuffMetadata(SkillLevelData levelData)
        {
            if (levelData == null)
            {
                return false;
            }

            return levelData.PAD > 0
                   || levelData.AttackPercent > 0
                   || levelData.MAD > 0
                   || levelData.MagicAttackPercent > 0
                   || levelData.PDD > 0
                   || levelData.MDD > 0
                   || levelData.DefensePercent > 0
                   || levelData.MagicDefensePercent > 0
                   || levelData.ACC > 0
                   || levelData.EVA > 0
                   || levelData.AccuracyPercent > 0
                   || levelData.AvoidabilityPercent > 0
                   || levelData.Speed > 0
                   || levelData.SpeedPercent > 0
                   || levelData.Jump > 0
                   || levelData.CriticalRate > 0
                   || levelData.CriticalDamageMin > 0
                   || levelData.CriticalDamageMax > 0
                   || levelData.Mastery > 0
                   || levelData.EnhancedPAD > 0
                   || levelData.EnhancedMAD > 0
                   || levelData.EnhancedPDD > 0
                   || levelData.EnhancedMDD > 0
                   || levelData.EnhancedMaxHP > 0
                   || levelData.EnhancedMaxMP > 0
                   || levelData.IndieMaxHP > 0
                   || levelData.IndieMaxMP > 0
                   || levelData.MaxHPPercent > 0
                   || levelData.MaxMPPercent > 0
                   || levelData.STR > 0
                   || levelData.DEX > 0
                   || levelData.INT > 0
                   || levelData.LUK > 0
                   || levelData.AllStat > 0
                   || levelData.DamageReductionRate > 0
                   || levelData.AbnormalStatusResistance > 0
                   || levelData.ElementalResistance > 0
                   || levelData.ExperienceRate > 0
                   || levelData.DropRate > 0
                   || levelData.MesoRate > 0
                   || levelData.BossDamageRate > 0
                   || levelData.IgnoreDefenseRate > 0
                   || levelData.X > 0
                   || levelData.Y > 0
                   || levelData.Z > 0;
        }

        public static SkillLevelData CreateProjectedSupportBuffLevelData(SkillLevelData levelData)
        {
            return CreateProjectedSupportBuffLevelData(levelData == null ? Array.Empty<SkillLevelData>() : new[] { levelData });
        }

        public static SkillLevelData CreateProjectedSupportBuffLevelData(
            SkillData skill,
            SkillLevelData levelData,
            params SkillData[] supportSkills)
        {
            SkillLevelData projected = CreateProjectedSupportBuffLevelData(levelData);
            if (projected == null)
            {
                return null;
            }

            return ApplyDerivedSupportBuffMappings(projected, skill, levelData, supportSkills);
        }

        public static SkillLevelData CreateProjectedSupportBuffLevelData(params SkillLevelData[] levelDataEntries)
        {
            if (levelDataEntries == null || levelDataEntries.Length == 0)
            {
                return null;
            }

            SkillLevelData projected = null;
            for (int i = 0; i < levelDataEntries.Length; i++)
            {
                SkillLevelData entry = levelDataEntries[i];
                if (!HasProjectableSupportBuffMetadata(entry))
                {
                    continue;
                }

                projected ??= new SkillLevelData();
                MergeProjectableSupportStats(projected, entry);
            }

            return projected;
        }

        public static int ResolveDerivedProjectedDamageReductionRate(SkillData skill, SkillLevelData levelData)
        {
            if (skill == null || levelData == null)
            {
                return 0;
            }

            if (skill.HasMagicStealMetadata)
            {
                int authoredShieldRate = levelData.X > 0 ? levelData.X : levelData.Y;
                return Math.Clamp(authoredShieldRate, 0, 100);
            }

            string combinedText = BuildCombinedSupportText(skill, supportSkills: null);

            // Blue Aura-style affected areas author the absorbed share on `y`
            // while `x` remains the distributed-share value.
            bool usesPartyDamageSharing = ContainsToken(skill.AffectedSkillEffect, "partyDamageSharing");
            if (usesPartyDamageSharing)
            {
                int absorbedShareRate = levelData.Y > 0 ? levelData.Y : levelData.X;
                return Math.Clamp(absorbedShareRate, 0, 100);
            }

            bool usesPartyAbsorbShield =
                ContainsToken(combinedText, "absorbs")
                && ContainsToken(combinedText, "damage received", "damage taken", "damage suffered")
                && ContainsToken(combinedText, FriendlyAreaDescriptionTokens);
            if (!usesPartyAbsorbShield)
            {
                return 0;
            }

            int absorbShieldRate = levelData.X > 0 ? levelData.X : levelData.Y;
            return Math.Clamp(absorbShieldRate, 0, 100);
        }

        public static int ResolveDerivedProjectedOutgoingDamageRate(
            SkillData skill,
            SkillLevelData levelData,
            IEnumerable<SkillData> supportSkills = null)
        {
            if (skill == null || levelData == null || levelData.X <= 0)
            {
                return 0;
            }

            string combinedText = BuildCombinedSupportText(skill, supportSkills);
            if (string.IsNullOrWhiteSpace(combinedText))
            {
                return 0;
            }

            bool hasOutgoingDamageText =
                ContainsToken(
                    combinedText,
                    "damage done",
                    "damage dealt",
                    "increase damage",
                    "increases damage",
                    "damage +",
                    "final damage");
            if (!hasOutgoingDamageText)
            {
                return 0;
            }

            return Math.Clamp(levelData.X, 0, 100);
        }

        public static RemotePlayerAffectedAreaDisposition ResolveDisposition(SkillData skill, SkillLevelData levelData = null)
        {
            return ResolveDisposition(skill, supportSkills: null, levelData);
        }

        public static RemotePlayerAffectedAreaDisposition ResolveDisposition(
            SkillData skill,
            IEnumerable<SkillData> supportSkills,
            SkillLevelData levelData = null)
        {
            if (IsFriendlyPlayerAreaSkill(skill, supportSkills, levelData))
            {
                return RemotePlayerAffectedAreaDisposition.FriendlySupport;
            }

            return IsHostilePlayerAreaSkill(skill, supportSkills, levelData)
                ? RemotePlayerAffectedAreaDisposition.Hostile
                : RemotePlayerAffectedAreaDisposition.NeutralUnknown;
        }

        public static bool IsFriendlyPlayerAreaSkill(SkillData skill, SkillLevelData levelData = null)
        {
            return IsFriendlyPlayerAreaSkill(skill, supportSkills: null, levelData);
        }

        public static bool IsFriendlyPlayerAreaSkill(
            SkillData skill,
            IEnumerable<SkillData> supportSkills,
            SkillLevelData levelData = null)
        {
            if (IsFriendlyPlayerAreaSkillCore(skill, levelData))
            {
                return true;
            }

            if (supportSkills == null)
            {
                return false;
            }

            foreach (SkillData supportSkill in supportSkills)
            {
                if (supportSkill == null || ReferenceEquals(supportSkill, skill))
                {
                    continue;
                }

                if (IsFriendlyPlayerAreaSkillCore(supportSkill, levelData: null))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsInvincibleZone(SkillData skill)
        {
            return string.Equals(skill?.ZoneType, "invincible", StringComparison.OrdinalIgnoreCase)
                   || skill?.HasInvincibleMetadata == true;
        }

        public static bool CanAffectLocalPlayer(
            SkillData skill,
            IEnumerable<SkillData> supportSkills,
            int localPlayerId,
            int ownerId,
            bool ownerIsPartyMember,
            bool ownerIsSameTeamMember,
            SkillLevelData levelData = null)
        {
            if (skill == null || localPlayerId <= 0 || ownerId <= 0)
            {
                return false;
            }

            if (!IsFriendlyPlayerAreaSkill(skill, supportSkills, levelData))
            {
                return false;
            }

            if (ownerId == localPlayerId)
            {
                return true;
            }

            if (ownerIsPartyMember && SupportsPartyMembers(skill, supportSkills))
            {
                return true;
            }

            return ownerIsSameTeamMember && SupportsTeamMembers(skill, supportSkills);
        }

        public static bool CanAffectLocalPlayer(
            SkillData skill,
            int localPlayerId,
            int ownerId,
            bool ownerIsPartyMember,
            bool ownerIsSameTeamMember,
            SkillLevelData levelData = null)
        {
            return CanAffectLocalPlayer(
                skill,
                supportSkills: null,
                localPlayerId,
                ownerId,
                ownerIsPartyMember,
                ownerIsSameTeamMember,
                levelData);
        }

        public static bool IsHostilePlayerAreaSkill(SkillData skill, SkillLevelData levelData = null)
        {
            return IsHostilePlayerAreaSkill(skill, supportSkills: null, levelData);
        }

        public static bool IsHostilePlayerAreaSkill(
            SkillData skill,
            IEnumerable<SkillData> supportSkills,
            SkillLevelData levelData = null)
        {
            if (skill == null)
            {
                return false;
            }

            if (!HasHostileMobGameplay(skill, supportSkills, levelData))
            {
                return false;
            }

            if (levelData == null)
            {
                return !HasFriendlySupportMetadata(skill, null);
            }

            return !HasPositiveSupportMetadata(levelData);
        }

        public static bool HasHostileMobGameplay(SkillData skill, SkillLevelData levelData = null)
        {
            return HasHostileMobGameplay(skill, supportSkills: null, levelData);
        }

        public static bool HasHostileMobGameplay(
            SkillData skill,
            IEnumerable<SkillData> supportSkills,
            SkillLevelData levelData = null)
        {
            if (HasHostileMobGameplayCore(skill, levelData))
            {
                return true;
            }

            if (supportSkills == null)
            {
                return false;
            }

            foreach (SkillData supportSkill in supportSkills)
            {
                if (supportSkill == null || ReferenceEquals(supportSkill, skill))
                {
                    continue;
                }

                if (HasHostileMobGameplayCore(supportSkill, levelData))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasHostileMobGameplay(IEnumerable<(SkillData Skill, SkillLevelData LevelData)> skillEntries)
        {
            if (skillEntries == null)
            {
                return false;
            }

            foreach ((SkillData skill, SkillLevelData levelData) in skillEntries)
            {
                if (HasHostileMobGameplayCore(skill, levelData))
                {
                    return true;
                }
            }

            return false;
        }

        internal static IReadOnlyList<(SkillData Skill, SkillLevelData LevelData)> FilterHostileSkillEntries(
            IEnumerable<(SkillData Skill, SkillLevelData LevelData)> skillEntries)
        {
            List<(SkillData Skill, SkillLevelData LevelData)> hostileEntries = new();
            if (skillEntries == null)
            {
                return hostileEntries;
            }

            HashSet<int> visitedSkillIds = new();
            foreach ((SkillData skill, SkillLevelData levelData) in skillEntries)
            {
                if (skill == null || levelData == null)
                {
                    continue;
                }

                int skillId = skill.SkillId;
                if (skillId > 0 && !visitedSkillIds.Add(skillId))
                {
                    continue;
                }

                if (HasHostileMobGameplayCore(skill, levelData))
                {
                    hostileEntries.Add((skill, levelData));
                }
            }

            return hostileEntries;
        }

        private static bool HasHostileMobGameplayCore(SkillData skill, SkillLevelData levelData)
        {
            if (skill == null)
            {
                return false;
            }

            if (UsesHostileDotOrBodyAttackMetadata(skill))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(skill.DebuffMessageToken))
            {
                return true;
            }

            string hostileSearchText = BuildHostileGameplaySearchText(skill);
            if (ContainsToken(hostileSearchText, HostileAreaTokens))
            {
                return true;
            }

            if (skill.Type == SkillType.Debuff)
            {
                return true;
            }

            if (levelData == null)
            {
                return false;
            }

            bool hasHostileDamageMetadata =
                levelData.Damage > 0
                || levelData.DotDamage > 0
                || levelData.DotTime > 0
                || levelData.AttackCount > 1
                || skill.Type == SkillType.Attack
                || skill.Type == SkillType.Magic;
            return hasHostileDamageMetadata
                   || levelData.Speed < 0
                   || levelData.Jump < 0
                   || levelData.PAD < 0
                   || levelData.MAD < 0
                   || levelData.PDD < 0
                   || levelData.MDD < 0
                   || levelData.ACC < 0
                   || levelData.EVA < 0
                   || levelData.STR < 0
                   || levelData.DEX < 0
                   || levelData.INT < 0
                   || levelData.LUK < 0;
        }

        private static bool UsesHostileDotOrBodyAttackMetadata(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            if (skill.UsesAffectedSkillBodyAttack)
            {
                return true;
            }

            bool usesAffectedAreaDot =
                string.Equals(skill.AffectedSkillEffect, "dot", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(skill.DotType);
            if (usesAffectedAreaDot)
            {
                return true;
            }

            return string.Equals(skill.MinionAttack, "dot", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(skill.DotType);
        }

        private static bool IsFriendlySupportSummonArea(SkillData skill)
        {
            if (skill?.ClientInfoType != 33)
            {
                return false;
            }

            if (ContainsToken(skill.MinionAbility, "heal", "amplifyDamage"))
            {
                return true;
            }

            if (ContainsToken(skill.MinionAbility, "mes")
                && ContainsToken(skill.Description, HostileSummonSupportTokens))
            {
                return false;
            }

            return ContainsToken(skill.Description, FriendlyAreaDescriptionTokens)
                   && ContainsToken(skill.Description, FriendlyAreaSupportTokens);
        }

        public static bool SupportsPartyMembers(SkillData skill, IEnumerable<SkillData> supportSkills = null)
        {
            if (SupportsPartyMembers(skill))
            {
                return true;
            }

            if (supportSkills == null)
            {
                return false;
            }

            foreach (SkillData supportSkill in supportSkills)
            {
                if (supportSkill == null || ReferenceEquals(supportSkill, skill))
                {
                    continue;
                }

                if (SupportsPartyMembers(supportSkill))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool SupportsTeamMembers(SkillData skill, IEnumerable<SkillData> supportSkills = null)
        {
            if (SupportsTeamMembers(skill))
            {
                return true;
            }

            if (supportSkills == null)
            {
                return false;
            }

            foreach (SkillData supportSkill in supportSkills)
            {
                if (supportSkill == null || ReferenceEquals(supportSkill, skill))
                {
                    continue;
                }

                if (SupportsTeamMembers(supportSkill))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFriendlyPlayerAreaSkillCore(SkillData skill, SkillLevelData levelData)
        {
            if (skill == null)
            {
                return false;
            }

            if (IsSupportZone(skill, levelData)
                || skill.Type == SkillType.PartyBuff
                || skill.HasInvincibleMetadata)
            {
                return true;
            }

            if (IsFriendlySupportSummonArea(skill))
            {
                return true;
            }

            return HasFriendlySupportMetadata(skill, levelData);
        }

        private static bool HasFriendlySupportMetadata(SkillData skill, SkillLevelData levelData)
        {
            if (skill == null)
            {
                return false;
            }

            if (SupportsAlliedMembers(skill)
                && HasProtectiveSupportMetadata(skill))
            {
                return true;
            }

            if (SupportsAlliedMembers(skill)
                && (ContainsToken(skill.Name, FriendlyAreaSupportTokens)
                    || ContainsToken(skill.Description, FriendlyAreaSupportTokens)
                    || ContainsToken(skill.DescriptionHints, FriendlyAreaSupportTokens)))
            {
                return true;
            }

            if (HasPositiveSupportMetadata(levelData))
            {
                return !HasHostileMobGameplay(skill, levelData) || SupportsAlliedMembers(skill);
            }

            bool hasDerivedSupportAliasMetadata =
                levelData != null
                && ((levelData.X > 0 && ContainsToken(skill.DescriptionHints ?? skill.Description, "movement speed", "speed", "attack", "damage"))
                    || (levelData.Y != 0 && ContainsToken(skill.DescriptionHints ?? skill.Description, "attack speed", "weapon speed"))
                    || (levelData.Z > 0 && ContainsToken(skill.DescriptionHints ?? skill.Description, "dodge chance", "avoidability")));
            return hasDerivedSupportAliasMetadata && SupportsAlliedMembers(skill);
        }

        private static bool HasPositiveSupportMetadata(SkillLevelData levelData)
        {
            if (levelData == null)
            {
                return false;
            }

            return levelData.HP > 0
                   || levelData.MP > 0
                   || levelData.PAD > 0
                   || levelData.AttackPercent > 0
                   || levelData.MAD > 0
                   || levelData.MagicAttackPercent > 0
                   || levelData.PDD > 0
                   || levelData.MDD > 0
                   || levelData.DefensePercent > 0
                   || levelData.MagicDefensePercent > 0
                   || levelData.STR > 0
                   || levelData.DEX > 0
                   || levelData.INT > 0
                   || levelData.LUK > 0
                   || levelData.ACC > 0
                   || levelData.EVA > 0
                   || levelData.AccuracyPercent > 0
                   || levelData.AvoidabilityPercent > 0
                   || levelData.Speed > 0
                   || levelData.SpeedPercent > 0
                   || levelData.Jump > 0
                   || levelData.MaxHPPercent > 0
                   || levelData.MaxMPPercent > 0
                   || levelData.DamageReductionRate > 0
                   || levelData.CriticalRate > 0
                   || levelData.CriticalDamageMin > 0
                   || levelData.CriticalDamageMax > 0
                   || levelData.Mastery > 0
                   || levelData.AllStat > 0
                   || levelData.ExperienceRate > 0
                   || levelData.DropRate > 0
                   || levelData.MesoRate > 0
                   || levelData.AbnormalStatusResistance > 0
                   || levelData.ElementalResistance > 0
                   || levelData.BossDamageRate > 0
                   || levelData.IgnoreDefenseRate > 0;
        }

        private static bool HasStatusCleansingMetadata(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            if (skill.HasDispelMetadata)
            {
                return true;
            }

            return ContainsToken(skill.Name, "dispel")
                   || ContainsToken(skill.Description, FriendlyAreaCleanseTokens)
                   || (ContainsToken(skill.Description, "abnormal condition", "abnormal conditions")
                       && ContainsToken(skill.Description, "remove", "removing", "dispel", "nullifies"));
        }

        private static bool HasProtectiveSupportMetadata(SkillData skill)
        {
            return skill != null
                   && (skill.HasMagicStealMetadata
                       || skill.RedirectsDamageToMp
                       || skill.ReflectsIncomingDamage
                       || skill.HasInvincibleMetadata
                       || skill.HasDispelMetadata);
        }

        private static bool SupportsPartyMembers(SkillData skill)
        {
            return skill != null
                   && (skill.IsMassSpell
                       || SupportsPartyMembersViaSupportSummonMetadata(skill)
                       || skill.Type == SkillType.PartyBuff
                       || skill.Target == SkillTarget.Party
                       || ContainsToken(skill.Description, FriendlyPartyDescriptionTokens)
                       || ContainsToken(skill.DescriptionHints, FriendlyPartyDescriptionTokens));
        }

        private static bool SupportsTeamMembers(SkillData skill)
        {
            return skill != null
                   && (ContainsToken(skill.Description, FriendlyTeamDescriptionTokens)
                       || ContainsToken(skill.DescriptionHints, FriendlyTeamDescriptionTokens));
        }

        private static bool SupportsPartyMembersViaSupportSummonMetadata(SkillData skill)
        {
            return skill?.ClientInfoType == 33
                   && ContainsToken(skill.MinionAbility, "heal", "amplifyDamage");
        }

        private static bool SupportsAlliedMembers(SkillData skill)
        {
            return SupportsPartyMembers(skill) || SupportsTeamMembers(skill);
        }

        private static void MergeProjectableSupportStats(SkillLevelData target, SkillLevelData source)
        {
            if (target == null || source == null)
            {
                return;
            }

            target.Level = Math.Max(target.Level, source.Level);
            target.PAD = Math.Max(target.PAD, source.PAD);
            target.AttackPercent = Math.Max(target.AttackPercent, source.AttackPercent);
            target.MAD = Math.Max(target.MAD, source.MAD);
            target.MagicAttackPercent = Math.Max(target.MagicAttackPercent, source.MagicAttackPercent);
            target.PDD = Math.Max(target.PDD, source.PDD);
            target.MDD = Math.Max(target.MDD, source.MDD);
            target.DefensePercent = Math.Max(target.DefensePercent, source.DefensePercent);
            target.MagicDefensePercent = Math.Max(target.MagicDefensePercent, source.MagicDefensePercent);
            target.ACC = Math.Max(target.ACC, source.ACC);
            target.EVA = Math.Max(target.EVA, source.EVA);
            target.AccuracyPercent = Math.Max(target.AccuracyPercent, source.AccuracyPercent);
            target.AvoidabilityPercent = Math.Max(target.AvoidabilityPercent, source.AvoidabilityPercent);
            target.Speed = Math.Max(target.Speed, source.Speed);
            target.SpeedPercent = Math.Max(target.SpeedPercent, source.SpeedPercent);
            target.Jump = Math.Max(target.Jump, source.Jump);
            target.STR = Math.Max(target.STR, source.STR);
            target.DEX = Math.Max(target.DEX, source.DEX);
            target.INT = Math.Max(target.INT, source.INT);
            target.LUK = Math.Max(target.LUK, source.LUK);
            target.Mastery = Math.Max(target.Mastery, source.Mastery);
            target.CriticalRate = Math.Max(target.CriticalRate, source.CriticalRate);
            target.CriticalDamageMin = Math.Max(target.CriticalDamageMin, source.CriticalDamageMin);
            target.CriticalDamageMax = Math.Max(target.CriticalDamageMax, source.CriticalDamageMax);
            target.EnhancedPAD = Math.Max(target.EnhancedPAD, source.EnhancedPAD);
            target.EnhancedMAD = Math.Max(target.EnhancedMAD, source.EnhancedMAD);
            target.EnhancedPDD = Math.Max(target.EnhancedPDD, source.EnhancedPDD);
            target.EnhancedMDD = Math.Max(target.EnhancedMDD, source.EnhancedMDD);
            target.EnhancedMaxHP = Math.Max(target.EnhancedMaxHP, source.EnhancedMaxHP);
            target.EnhancedMaxMP = Math.Max(target.EnhancedMaxMP, source.EnhancedMaxMP);
            target.IndieMaxHP = Math.Max(target.IndieMaxHP, source.IndieMaxHP);
            target.IndieMaxMP = Math.Max(target.IndieMaxMP, source.IndieMaxMP);
            target.MaxHPPercent = Math.Max(target.MaxHPPercent, source.MaxHPPercent);
            target.MaxMPPercent = Math.Max(target.MaxMPPercent, source.MaxMPPercent);
            target.AllStat = Math.Max(target.AllStat, source.AllStat);
            target.DamageReductionRate = Math.Max(target.DamageReductionRate, source.DamageReductionRate);
            target.AbnormalStatusResistance = Math.Max(target.AbnormalStatusResistance, source.AbnormalStatusResistance);
            target.ElementalResistance = Math.Max(target.ElementalResistance, source.ElementalResistance);
            target.ExperienceRate = Math.Max(target.ExperienceRate, source.ExperienceRate);
            target.DropRate = Math.Max(target.DropRate, source.DropRate);
            target.MesoRate = Math.Max(target.MesoRate, source.MesoRate);
            target.BossDamageRate = Math.Max(target.BossDamageRate, source.BossDamageRate);
            target.IgnoreDefenseRate = Math.Max(target.IgnoreDefenseRate, source.IgnoreDefenseRate);
            target.X = Math.Max(target.X, source.X);
            target.Y = Math.Max(target.Y, source.Y);
            target.Z = Math.Max(target.Z, source.Z);
        }

        private static SkillLevelData ApplyDerivedSupportBuffMappings(
            SkillLevelData projected,
            SkillData skill,
            SkillLevelData authoredLevelData,
            IEnumerable<SkillData> supportSkills)
        {
            if (projected == null)
            {
                return null;
            }

            string combinedText = BuildCombinedSupportText(skill, supportSkills);
            if (string.IsNullOrWhiteSpace(combinedText))
            {
                return projected;
            }

            bool derivedMovementSpeedFromAliasX =
                projected.Speed <= 0
                && authoredLevelData?.X > 0
                && ContainsToken(combinedText, "movement speed", "speed");
            if (derivedMovementSpeedFromAliasX)
            {
                projected.Speed = Math.Max(projected.Speed, authoredLevelData.X);
            }

            if (ContainsToken(combinedText, "attack speed", "weapon speed"))
            {
                int boosterValue = 0;
                if (authoredLevelData != null)
                {
                    boosterValue = authoredLevelData.Y < 0
                        ? Math.Abs(authoredLevelData.Y)
                        : Math.Max(authoredLevelData.Y, 0);
                }

                if (boosterValue > 0
                    && (projected.X <= 0 || derivedMovementSpeedFromAliasX))
                {
                    projected.X = boosterValue;
                }
            }

            if (projected.AvoidabilityPercent <= 0
                && authoredLevelData?.Z > 0
                && ContainsToken(combinedText, "dodge chance", "avoidability"))
            {
                projected.AvoidabilityPercent = Math.Max(projected.AvoidabilityPercent, authoredLevelData.Z);
            }

            return projected;
        }

        private static string BuildCombinedSupportText(SkillData skill, IEnumerable<SkillData> supportSkills)
        {
            var values = new List<string>(6);
            AddSupportText(values, skill);

            if (supportSkills != null)
            {
                foreach (SkillData supportSkill in supportSkills)
                {
                    if (supportSkill == null || ReferenceEquals(supportSkill, skill))
                    {
                        continue;
                    }

                    AddSupportText(values, supportSkill);
                }
            }

            return string.Join(" ", values);
        }

        private static string BuildHostileGameplaySearchText(SkillData skill)
        {
            var values = new List<string>(8);
            AddSupportText(values, skill);

            if (!string.IsNullOrWhiteSpace(skill?.ZoneType))
            {
                values.Add(skill.ZoneType);
            }

            if (!string.IsNullOrWhiteSpace(skill?.DebuffMessageToken))
            {
                values.Add(skill.DebuffMessageToken);
            }

            if (!string.IsNullOrWhiteSpace(skill?.MinionAttack))
            {
                values.Add(skill.MinionAttack);
            }

            if (!string.IsNullOrWhiteSpace(skill?.MinionAbility))
            {
                values.Add(skill.MinionAbility);
            }

            if (!string.IsNullOrWhiteSpace(skill?.AffectedSkillEffect))
            {
                values.Add(skill.AffectedSkillEffect);
            }

            if (!string.IsNullOrWhiteSpace(skill?.DotType))
            {
                values.Add(skill.DotType);
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName))
            {
                values.Add(skill.ActionName);
            }

            return string.Join(" ", values);
        }

        private static void AddSupportText(ICollection<string> values, SkillData skill)
        {
            if (skill == null || values == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(skill.Name))
            {
                values.Add(skill.Name);
            }

            if (!string.IsNullOrWhiteSpace(skill.Description))
            {
                values.Add(skill.Description);
            }

            if (!string.IsNullOrWhiteSpace(skill.DescriptionHints))
            {
                values.Add(skill.DescriptionHints);
            }
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
