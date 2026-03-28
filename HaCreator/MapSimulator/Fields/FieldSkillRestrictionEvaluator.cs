using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib.WzStructure.Data;
using System;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Applies fieldLimit-based restrictions to local skill usage.
    /// </summary>
    public static class FieldSkillRestrictionEvaluator
    {
        private const int RocketBoosterSkillId = 35101004;

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
            if (skill == null)
            {
                return false;
            }

            if (UsesVehicleOwnershipOrMountSkill(skill))
            {
                return true;
            }

            if (!IsMechanicSkill(skill.SkillId))
            {
                return false;
            }

            if (skill.ClientInfoType == 13)
            {
                return true;
            }

            string combinedText = $"{skill.Name} {skill.Description}";
            if (ContainsAny(combinedText, "mount/unmount", "summon and mount", "prototype mech"))
            {
                return true;
            }

            return IsMechanicVehicleActionName(skill.ActionName)
                   || IsMechanicVehicleActionName(skill.PrepareActionName)
                   || IsMechanicVehicleActionName(skill.KeydownActionName)
                   || IsMechanicVehicleActionName(skill.KeydownEndActionName);
        }

        private static bool UsesVehicleOwnershipOrMountSkill(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            if (skill.UsesTamingMobMount)
            {
                return true;
            }

            if (skill.ClientInfoType != 13)
            {
                return false;
            }

            if (skill.SkillId == 5221006 || skill.SkillId == 33001001)
            {
                return true;
            }

            string combinedText = $"{skill.Name} {skill.Description}";
            return ContainsAny(combinedText, "mount/unmount", "summon and mount", "monster rider", "jaguar rider");
        }

        private static bool IsMechanicSkill(int skillId)
        {
            int skillBookId = skillId / 10000;
            return skillBookId >= 3500 && skillBookId <= 3512;
        }

        private static bool IsMechanicVehicleActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.StartsWith("tank_", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("siege_", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "ride2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "getoff2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "ladder2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rope2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "herbalism_mechanic", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "mining_mechanic", StringComparison.OrdinalIgnoreCase);
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
