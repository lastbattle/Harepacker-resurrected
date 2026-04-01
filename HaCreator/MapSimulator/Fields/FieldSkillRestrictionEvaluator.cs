using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using System;
using MapleLib.WzLib;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Applies fieldLimit-based restrictions to local skill usage.
    /// </summary>
    public static class FieldSkillRestrictionEvaluator
    {
        private const int RocketBoosterSkillId = 35101004;

        public static bool CanUseSkill(MapInfo mapInfo, SkillData skill)
        {
            return CanUseSkill(mapInfo, skill, 0);
        }

        public static bool CanUseSkill(MapInfo mapInfo, SkillData skill, int currentJobId)
        {
            return CanUseSkill(mapInfo, skill, currentJobId, mobMassacreDisableSkill: false);
        }

        public static bool CanUseSkill(MapInfo mapInfo, SkillData skill, int currentJobId, bool mobMassacreDisableSkill)
        {
            return GetRestrictionMessage(mapInfo, skill, currentJobId, mobMassacreDisableSkill) == null;
        }

        public static string GetRestrictionMessage(MapInfo mapInfo, SkillData skill)
        {
            return GetRestrictionMessage(mapInfo, skill, 0);
        }

        public static string GetRestrictionMessage(MapInfo mapInfo, SkillData skill, int currentJobId)
        {
            return GetRestrictionMessage(mapInfo, skill, currentJobId, mobMassacreDisableSkill: false);
        }

        public static string GetRestrictionMessage(MapInfo mapInfo, SkillData skill, int currentJobId, bool mobMassacreDisableSkill)
        {
            if (skill == null)
                return "Skill data is unavailable.";

            string fieldLimitRestrictionMessage = GetRestrictionMessage(mapInfo?.fieldLimit ?? 0, skill);
            if (!string.IsNullOrWhiteSpace(fieldLimitRestrictionMessage))
            {
                return fieldLimitRestrictionMessage;
            }

            if (mobMassacreDisableSkill)
            {
                return "Skills cannot be used while the Mu Lung Dojo massacre field disables skill usage.";
            }

            return GetClientOwnedFieldRestrictionMessage(mapInfo, skill, currentJobId);
        }

        public static bool CanUseSkill(long fieldLimit, SkillData skill)
        {
            return GetRestrictionMessage(fieldLimit, skill) == null;
        }

        public static string GetRestrictionMessage(long fieldLimit, SkillData skill)
        {
            if (skill == null)
                return "Skill data is unavailable.";

            if (FieldLimitType.Unable_To_Use_Mystic_Door.Check(fieldLimit) && IsMysticDoorSkill(skill))
                return "Mystic Door cannot be used in this field.";

            if (FieldLimitType.Unable_To_Use_Rocket_Boost.Check(fieldLimit) && IsRocketBoosterSkill(skill))
                return "Rocket Booster cannot be used in this field.";

            if (FieldLimitType.Unable_To_Use_Taming_Mob.Check(fieldLimit) && UsesTamingMobRestrictedSkill(skill))
                return "Mount and mechanic vehicle skills cannot be used in this field.";

            if (FieldLimitType.Unable_To_Use_Skill.Check(fieldLimit))
                return "This field forbids skill usage.";

            if (FieldLimitType.Move_Skill_Only.Check(fieldLimit) && !skill.IsMovement)
                return "Only movement skills can be used in this field.";

            return null;
        }

        public static bool HasFieldEntryNotice(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Use_Skill.Check(fieldLimit)
                   || FieldLimitType.Move_Skill_Only.Check(fieldLimit)
                   || FieldLimitType.Unable_To_Use_Mystic_Door.Check(fieldLimit)
                   || FieldLimitType.Unable_To_Use_Rocket_Boost.Check(fieldLimit)
                   || FieldLimitType.Unable_To_Use_Taming_Mob.Check(fieldLimit);
        }

        public static string GetFieldEntryNotice(long fieldLimit)
        {
            if (FieldLimitType.Unable_To_Use_Skill.Check(fieldLimit))
                return "All skill usage is disabled in this map.";

            if (FieldLimitType.Move_Skill_Only.Check(fieldLimit))
                return "Only movement skills can be used in this map.";

            if (FieldLimitType.Unable_To_Use_Mystic_Door.Check(fieldLimit))
                return "Mystic Door is disabled in this map.";

            if (FieldLimitType.Unable_To_Use_Rocket_Boost.Check(fieldLimit))
                return "Rocket Booster is disabled in this map.";

            if (FieldLimitType.Unable_To_Use_Taming_Mob.Check(fieldLimit))
                return "Mount and mechanic vehicle skills are disabled in this map.";

            return null;
        }

        private static string GetClientOwnedFieldRestrictionMessage(MapInfo mapInfo, SkillData skill, int currentJobId)
        {
            if (mapInfo == null || skill == null)
            {
                return null;
            }

            string noSkillRestrictionMessage = GetNoSkillRestrictionMessage(mapInfo, skill, currentJobId);
            if (!string.IsNullOrWhiteSpace(noSkillRestrictionMessage))
            {
                return noSkillRestrictionMessage;
            }

            return mapInfo.fieldType switch
            {
                FieldType.FIELDTYPE_COCONUT => "Skills cannot be used while the Coconut minigame owns basic attacks.",
                FieldType.FIELDTYPE_SNOWBALL => "Skills cannot be used while the Snowball minigame owns basic attacks.",
                _ => null
            };
        }

        private static string GetNoSkillRestrictionMessage(MapInfo mapInfo, SkillData skill, int currentJobId)
        {
            WzImageProperty noSkillProperty = FindAdditionalFieldProperty(mapInfo, "noSkill");
            if (noSkillProperty == null)
            {
                return null;
            }

            if (MatchesListedSkill(noSkillProperty["skill"], skill.SkillId))
            {
                return "This skill is forbidden in this field.";
            }

            int resolvedJobClass = ResolveSkillRestrictionJobClass(currentJobId, skill);
            if (resolvedJobClass > 0 && MatchesListedSkillClass(noSkillProperty["class"], resolvedJobClass))
            {
                return "This field forbids skills for your job branch.";
            }

            return null;
        }

        private static WzImageProperty FindAdditionalFieldProperty(MapInfo mapInfo, string propertyName)
        {
            if (mapInfo?.additionalNonInfoProps != null)
            {
                for (int i = 0; i < mapInfo.additionalNonInfoProps.Count; i++)
                {
                    WzImageProperty property = mapInfo.additionalNonInfoProps[i];
                    if (string.Equals(property?.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return property;
                    }
                }
            }

            return mapInfo?.Image?[propertyName] as WzImageProperty;
        }

        private static bool MatchesListedSkill(WzImageProperty property, int skillId)
        {
            return skillId > 0 && ContainsIntValue(property, skillId);
        }

        private static bool MatchesListedSkillClass(WzImageProperty property, int jobClass)
        {
            return jobClass > 0 && ContainsIntValue(property, jobClass);
        }

        private static bool ContainsIntValue(WzImageProperty property, int expectedValue)
        {
            if (property?.WzProperties == null)
            {
                return false;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                if (TryReadInt(property.WzProperties[i], out int value) && value == expectedValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadInt(WzImageProperty property, out int value)
        {
            value = 0;
            if (property == null)
            {
                return false;
            }

            try
            {
                value = property.GetInt();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ResolveSkillRestrictionJobClass(int currentJobId, SkillData skill)
        {
            int resolvedJobId = Math.Abs(currentJobId);
            if (resolvedJobId <= 0)
            {
                resolvedJobId = Math.Abs(skill?.Job ?? 0);
            }

            if (resolvedJobId <= 0 && skill?.SkillId > 0)
            {
                resolvedJobId = Math.Abs(skill.SkillId / 10000);
            }

            if (resolvedJobId <= 0)
            {
                return 0;
            }

            int jobClass = resolvedJobId % 1000 / 100;
            if (jobClass == 0 && resolvedJobId < 1000)
            {
                jobClass = resolvedJobId / 100;
            }

            return jobClass;
        }

        private static bool IsMysticDoorSkill(SkillData skill)
        {
            return skill?.SkillId == 2311002
                   || string.Equals(skill?.Name, "Mystic Door", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRocketBoosterSkill(SkillData skill)
        {
            return skill?.SkillId == RocketBoosterSkillId
                   || string.Equals(skill?.Name, "Rocket Booster", StringComparison.OrdinalIgnoreCase);
        }

        private static bool UsesTamingMobRestrictedSkill(SkillData skill)
        {
            return ClientOwnedVehicleSkillClassifier.UsesVehicleOwnershipOrMountSkill(skill);
        }

        private static bool UsesVehicleOwnershipOrMountSkill(SkillData skill)
        {
            return ClientOwnedVehicleSkillClassifier.UsesVehicleOwnershipOrMountSkill(skill);
        }

        private static bool IsMechanicSkill(int skillId)
        {
            int skillBookId = skillId / 10000;
            return skillBookId >= 3500 && skillBookId <= 3512;
        }

        private static bool IsMechanicVehicleActionName(string actionName)
        {
            return ClientOwnedVehicleSkillClassifier.IsMechanicVehicleActionName(actionName, includeTransformStates: true)
                   || string.Equals(actionName, "ladder2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rope2", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsAny(string value, params string[] fragments)
        {
            if (string.IsNullOrWhiteSpace(value) || fragments == null)
            {
                return false;
            }

            for (int i = 0; i < fragments.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(fragments[i])
                    && value.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
