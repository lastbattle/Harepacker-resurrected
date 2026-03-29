using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class ClientOwnedVehicleSkillClassifier
    {
        private const int BattleshipSkillId = 5221006;

        private static readonly string[] RideDescriptionMarkers =
        {
            "mount/unmount",
            "summon and mount",
            "monster rider",
            "jaguar rider",
            "allows you to ride",
            "allows one to ride",
            "enables you to ride",
            "enables one to ride",
            "method of transportation"
        };

        private static readonly string[] BattleshipBoardingMarkers =
        {
            "only available when aboard the battleship",
            "available when aboard the battleship",
            "aboard the battleship"
        };

        internal static bool LooksLikeClientOwnedRideDescriptionBuff(SkillData skill)
        {
            if (skill?.IsBuff != true)
            {
                return false;
            }

            // The currently confirmed non-type-13 ride buffs are invisible timed ownership
            // grants that only advertise the mount through their string surface.
            if (!skill.Invisible && skill.ClientInfoType != 13)
            {
                return false;
            }

            return HasRideDescriptionText(skill);
        }

        internal static bool IsWzAuthoredClientOwnedVehicleBuff(SkillData skill)
        {
            return skill?.IsBuff == true
                   && (skill.ClientInfoType == 13
                       || LooksLikeClientOwnedRideDescriptionBuff(skill));
        }

        internal static bool IsClientOwnedVehicleActionSkill(SkillData skill, SkillLevelData levelData = null)
        {
            if (skill == null || skill.IsBuff)
            {
                return false;
            }

            if (!string.Equals(skill.ActionName, "cannon", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(skill.ActionName, "torpedo", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return skill.SkillId == 5221007
                   || skill.SkillId == 5221008
                   || HasRequiredSkill(skill, BattleshipSkillId)
                   || levelData?.RequiredSkill == BattleshipSkillId
                   || HasBattleshipBoardingText(skill);
        }

        private static bool HasRequiredSkill(SkillData skill, int requiredSkillId)
        {
            if (skill?.Levels == null || requiredSkillId <= 0)
            {
                return false;
            }

            foreach (SkillLevelData candidateLevel in skill.Levels.Values)
            {
                if (candidateLevel?.RequiredSkill == requiredSkillId)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasRideDescriptionText(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            string combinedText = $"{skill.Name} {skill.Description}";
            foreach (string marker in RideDescriptionMarkers)
            {
                if (combinedText.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasBattleshipBoardingText(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            string combinedText = $"{skill.Name} {skill.Description}";
            foreach (string marker in BattleshipBoardingMarkers)
            {
                if (combinedText.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
