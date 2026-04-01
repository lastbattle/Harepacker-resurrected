using System;
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
                   || levelData.ACC > 0
                   || levelData.EVA > 0
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
                   || levelData.MesoRate > 0;
        }

        public static SkillLevelData CreateProjectedSupportBuffLevelData(SkillLevelData levelData)
        {
            if (!HasProjectableSupportBuffMetadata(levelData))
            {
                return null;
            }

            return new SkillLevelData
            {
                Level = levelData.Level,
                PAD = levelData.PAD,
                MAD = levelData.MAD,
                PDD = levelData.PDD,
                MDD = levelData.MDD,
                ACC = levelData.ACC,
                EVA = levelData.EVA,
                Speed = levelData.Speed,
                Jump = levelData.Jump,
                STR = levelData.STR,
                DEX = levelData.DEX,
                INT = levelData.INT,
                LUK = levelData.LUK,
                CriticalRate = levelData.CriticalRate,
                EnhancedPAD = levelData.EnhancedPAD,
                EnhancedMAD = levelData.EnhancedMAD,
                EnhancedPDD = levelData.EnhancedPDD,
                EnhancedMDD = levelData.EnhancedMDD,
                EnhancedMaxHP = levelData.EnhancedMaxHP,
                EnhancedMaxMP = levelData.EnhancedMaxMP,
                IndieMaxHP = levelData.IndieMaxHP,
                IndieMaxMP = levelData.IndieMaxMP,
                MaxHPPercent = levelData.MaxHPPercent,
                MaxMPPercent = levelData.MaxMPPercent,
                AllStat = levelData.AllStat,
                DamageReductionRate = levelData.DamageReductionRate,
                AbnormalStatusResistance = levelData.AbnormalStatusResistance,
                ElementalResistance = levelData.ElementalResistance,
                ExperienceRate = levelData.ExperienceRate,
                DropRate = levelData.DropRate,
                MesoRate = levelData.MesoRate
            };
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
                   || levelData.STR > 0
                   || levelData.DEX > 0
                   || levelData.INT > 0
                   || levelData.LUK > 0
                   || levelData.ACC > 0
                   || levelData.EVA > 0
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
