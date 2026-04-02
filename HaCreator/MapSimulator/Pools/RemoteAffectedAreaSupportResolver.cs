using System;
using System.Collections.Generic;
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

        public static bool HasProjectableSupportBuffMetadata(SkillLevelData levelData)
        {
            if (levelData == null)
            {
                return false;
            }

            return levelData.PAD > 0
                   || levelData.MAD > 0
                   || levelData.PDD > 0
                   || levelData.MDD > 0
                   || levelData.DefensePercent > 0
                   || levelData.MagicDefensePercent > 0
                   || levelData.ACC > 0
                   || levelData.EVA > 0
                   || levelData.AccuracyPercent > 0
                   || levelData.AvoidabilityPercent > 0
                   || levelData.Speed > 0
                   || levelData.Jump > 0
                   || levelData.CriticalRate > 0
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

        public static RemotePlayerAffectedAreaDisposition ResolveDisposition(SkillData skill, SkillLevelData levelData = null)
        {
            if (IsFriendlyPlayerAreaSkill(skill, levelData))
            {
                return RemotePlayerAffectedAreaDisposition.FriendlySupport;
            }

            return IsHostilePlayerAreaSkill(skill, levelData)
                ? RemotePlayerAffectedAreaDisposition.Hostile
                : RemotePlayerAffectedAreaDisposition.NeutralUnknown;
        }

        public static bool IsFriendlyPlayerAreaSkill(SkillData skill, SkillLevelData levelData = null)
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

            return HasPositiveSupportMetadata(levelData) && !IsHostilePlayerAreaSkill(skill, levelData);
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
            SkillLevelData levelData = null)
        {
            if (skill == null || localPlayerId <= 0 || ownerId <= 0)
            {
                return false;
            }

            if (!IsFriendlyPlayerAreaSkill(skill, levelData))
            {
                return false;
            }

            if (ownerId == localPlayerId)
            {
                return true;
            }

            return ownerIsPartyMember && SupportsPartyMembers(skill, supportSkills);
        }

        public static bool CanAffectLocalPlayer(
            SkillData skill,
            int localPlayerId,
            int ownerId,
            bool ownerIsPartyMember,
            SkillLevelData levelData = null)
        {
            return CanAffectLocalPlayer(
                skill,
                supportSkills: null,
                localPlayerId,
                ownerId,
                ownerIsPartyMember,
                levelData);
        }

        public static bool IsHostilePlayerAreaSkill(SkillData skill, SkillLevelData levelData = null)
        {
            if (skill == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(skill.DebuffMessageToken))
            {
                return true;
            }

            if (ContainsToken(skill.ZoneType, HostileAreaTokens)
                || ContainsToken(skill.Name, HostileAreaTokens)
                || ContainsToken(skill.Description, HostileAreaTokens))
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
                || levelData.AttackCount > 1
                || skill.Type == SkillType.Attack
                || skill.Type == SkillType.Magic;
            return hasHostileDamageMetadata && !HasPositiveSupportMetadata(levelData);
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

        private static bool HasPositiveSupportMetadata(SkillLevelData levelData)
        {
            if (levelData == null)
            {
                return false;
            }

            return levelData.HP > 0
                   || levelData.MP > 0
                   || levelData.PAD > 0
                   || levelData.MAD > 0
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
                   || levelData.Jump > 0
                   || levelData.MaxHPPercent > 0
                   || levelData.MaxMPPercent > 0
                   || levelData.DamageReductionRate > 0
                   || levelData.CriticalRate > 0
                   || levelData.AllStat > 0
                   || levelData.ExperienceRate > 0
                   || levelData.DropRate > 0
                   || levelData.MesoRate > 0
                   || levelData.AbnormalStatusResistance > 0
                   || levelData.ElementalResistance > 0;
        }

        private static bool SupportsPartyMembers(SkillData skill)
        {
            return skill != null
                   && (skill.IsMassSpell
                       || SupportsPartyMembersViaSupportSummonMetadata(skill)
                       || skill.Type == SkillType.PartyBuff
                       || skill.Target == SkillTarget.Party
                       || ContainsToken(skill.Description, FriendlyAreaDescriptionTokens));
        }

        private static bool SupportsPartyMembersViaSupportSummonMetadata(SkillData skill)
        {
            return skill?.ClientInfoType == 33
                   && ContainsToken(skill.MinionAbility, "heal", "amplifyDamage");
        }

        private static void MergeProjectableSupportStats(SkillLevelData target, SkillLevelData source)
        {
            if (target == null || source == null)
            {
                return;
            }

            target.Level = Math.Max(target.Level, source.Level);
            target.PAD = Math.Max(target.PAD, source.PAD);
            target.MAD = Math.Max(target.MAD, source.MAD);
            target.PDD = Math.Max(target.PDD, source.PDD);
            target.MDD = Math.Max(target.MDD, source.MDD);
            target.DefensePercent = Math.Max(target.DefensePercent, source.DefensePercent);
            target.MagicDefensePercent = Math.Max(target.MagicDefensePercent, source.MagicDefensePercent);
            target.ACC = Math.Max(target.ACC, source.ACC);
            target.EVA = Math.Max(target.EVA, source.EVA);
            target.AccuracyPercent = Math.Max(target.AccuracyPercent, source.AccuracyPercent);
            target.AvoidabilityPercent = Math.Max(target.AvoidabilityPercent, source.AvoidabilityPercent);
            target.Speed = Math.Max(target.Speed, source.Speed);
            target.Jump = Math.Max(target.Jump, source.Jump);
            target.STR = Math.Max(target.STR, source.STR);
            target.DEX = Math.Max(target.DEX, source.DEX);
            target.INT = Math.Max(target.INT, source.INT);
            target.LUK = Math.Max(target.LUK, source.LUK);
            target.CriticalRate = Math.Max(target.CriticalRate, source.CriticalRate);
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
