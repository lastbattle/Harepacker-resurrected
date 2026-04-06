using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class ClientOwnedVehicleSkillClassifier
    {
        private const int BattleshipSkillId = 5221006;
        private static readonly int[] BattleshipMountedActionSkillIds =
        {
            5211004,
            5211005,
            5221007,
            5221008
        };

        private static readonly int[] BattleshipVehicleValidSupportSkillIds =
        {
            5201002,
            5201003,
            5211001,
            5211002,
            5211004,
            5211005,
            5221000,
            5221010
        };

        private static readonly int[] MechanicVehicleStateSkillIds =
        {
            35111004,
            35121005,
            35121013
        };

        private static readonly string[] RideDescriptionMarkers =
        {
            "mount/unmount",
            "summon and mount",
            "monster rider",
            "jaguar rider",
            "travel by riding a mount",
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

        private static readonly string[] SharedClientOwnedVehicleMountedMoveActions =
        {
            "walk1",
            "walk2",
            "stand1",
            "stand2",
            "jump",
            "prone",
            "fly",
            "swim",
            "tired"
        };

        private static readonly string[] MechanicClientOwnedVehicleMountedMoveActions =
        {
            "alert",
            "alert2",
            "alert3",
            "proneStab",
            "shot",
            "ladder",
            "rope",
            "ladder2",
            "rope2"
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

            bool hasBattleshipMountedAction = false;
            foreach (string actionName in EnumerateActionNames(skill))
            {
                if (IsBattleshipMountedActionName(actionName))
                {
                    hasBattleshipMountedAction = true;
                    break;
                }
            }

            if (!hasBattleshipMountedAction)
            {
                return false;
            }

            return Array.IndexOf(BattleshipMountedActionSkillIds, skill.SkillId) >= 0
                   || HasRequiredSkill(skill, BattleshipSkillId)
                   || levelData?.RequiredSkill == BattleshipSkillId
                   || HasBattleshipBoardingText(skill);
        }

        internal static bool IsClientOwnedVehicleStateSkill(SkillData skill)
        {
            if (skill == null || skill.IsBuff)
            {
                return false;
            }

            if (Array.IndexOf(MechanicVehicleStateSkillIds, skill.SkillId) < 0)
            {
                return false;
            }

            return skill.ClientInfoType == 10
                   || string.Equals(skill.ActionName, "tank_pre", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(skill.ActionName, "siege_pre", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(skill.ActionName, "tank_siegepre", StringComparison.OrdinalIgnoreCase)
                   || HasMechanicVehicleStateText(skill);
        }

        internal static bool IsClientOwnedVehicleValidSupportSkill(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            return Array.IndexOf(BattleshipVehicleValidSupportSkillIds, skill.SkillId) >= 0
                   && HasBattleshipSupportText(skill);
        }

        internal static bool UsesVehicleOwnershipOrMountSkill(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            if (skill.UsesTamingMobMount)
            {
                return true;
            }

            return IsWzAuthoredClientOwnedVehicleBuff(skill)
                   || LooksLikeClientOwnedRideDescriptionBuff(skill)
                   || IsClientOwnedVehicleActionSkill(skill)
                   || IsClientOwnedVehicleStateSkill(skill)
                   || UsesMechanicVehicleMountSkill(skill);
        }

        internal static bool UsesMechanicVehicleMountSkill(SkillData skill)
        {
            if (skill == null || !IsMechanicSkill(skill.SkillId))
            {
                return false;
            }

            foreach (string actionName in EnumerateActionNames(skill))
            {
                if (IsMechanicVehicleActionName(actionName, includeTransformStates: true))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsMechanicVehicleActionName(string actionName, bool includeTransformStates = false)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (includeTransformStates
                && (string.Equals(actionName, "tank", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "siege", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return actionName.StartsWith("tank_", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("siege_", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("flamethrower", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("rbooster", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("gatlingshot", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("drillrush", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("earthslug", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("rpunch", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("mbooster", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("msummon", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("mRush", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "alert3", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "ride2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "getoff2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "herbalism_mechanic", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "mining_mechanic", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMechanicSkill(int skillId)
        {
            int skillBookId = skillId / 10000;
            return skillBookId >= 3500 && skillBookId <= 3512;
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

        internal static bool IsBattleshipMountedActionName(string actionName)
        {
            return string.Equals(actionName, "cannon", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "torpedo", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "fireburner", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "coolingeffect", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBattleshipVehicleActionName(string actionName, bool includeSupportActions = false)
        {
            if (IsBattleshipMountedActionName(actionName))
            {
                return true;
            }

            return includeSupportActions
                   && (string.Equals(actionName, "alert2", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(actionName, "alert3", StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsBattleshipVehicleOwnedCurrentActionName(string actionName, bool includeSupportActions = false)
        {
            return IsBattleshipVehicleActionName(actionName, includeSupportActions)
                   || IsMountedMoveActionName(actionName);
        }

        internal static bool IsMechanicVehicleOwnedCurrentActionName(string actionName, bool includeTransformStates = false)
        {
            if (IsMechanicVehicleActionName(actionName, includeTransformStates))
            {
                return true;
            }

            return IsMountedMoveActionName(actionName)
                   || ContainsActionName(MechanicClientOwnedVehicleMountedMoveActions, actionName);
        }

        internal static bool IsDistinctMechanicVehicleActionName(string actionName, bool includeTransformStates = false)
        {
            if (!IsMechanicVehicleActionName(actionName, includeTransformStates))
            {
                return false;
            }

            return !string.Equals(actionName, "alert3", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "ride2", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "getoff2", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsOwnerlessMechanicVehicleInferenceActionName(string actionName, bool includeTransformStates = false)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (includeTransformStates
                && (string.Equals(actionName, "tank", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "siege", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return actionName.StartsWith("tank_", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("siege_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMountedMoveActionName(string actionName)
        {
            return ContainsActionName(SharedClientOwnedVehicleMountedMoveActions, actionName);
        }

        private static bool ContainsActionName(string[] candidates, string actionName)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            foreach (string candidate in candidates)
            {
                if (string.Equals(candidate, actionName, StringComparison.OrdinalIgnoreCase))
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

        private static bool HasBattleshipSupportText(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            string combinedText = $"{skill.Name} {skill.Description}";
            return combinedText.IndexOf("grenade", StringComparison.OrdinalIgnoreCase) >= 0
                   || combinedText.IndexOf("gun booster", StringComparison.OrdinalIgnoreCase) >= 0
                   || combinedText.IndexOf("octopus", StringComparison.OrdinalIgnoreCase) >= 0
                   || combinedText.IndexOf("gaviota", StringComparison.OrdinalIgnoreCase) >= 0
                   || combinedText.IndexOf("flamethrower", StringComparison.OrdinalIgnoreCase) >= 0
                   || combinedText.IndexOf("ice splitter", StringComparison.OrdinalIgnoreCase) >= 0
                   || combinedText.IndexOf("maple warrior", StringComparison.OrdinalIgnoreCase) >= 0
                   || combinedText.IndexOf("hero's will", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasMechanicVehicleStateText(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            string combinedText = $"{skill.Name} {skill.Description}";
            return combinedText.IndexOf("siege mode", StringComparison.OrdinalIgnoreCase) >= 0
                   || combinedText.IndexOf("tank mode", StringComparison.OrdinalIgnoreCase) >= 0
                   || combinedText.IndexOf("missile tank", StringComparison.OrdinalIgnoreCase) >= 0
                   || combinedText.IndexOf("get on/off your mount", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<string> EnumerateActionNames(SkillData skill)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (skill?.ActionNames != null)
            {
                foreach (string actionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(actionName) && seen.Add(actionName))
                    {
                        yield return actionName;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName))
            {
                string actionName = skill.ActionName;
                if (seen.Add(actionName))
                {
                    yield return actionName;
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.PrepareActionName))
            {
                string actionName = skill.PrepareActionName;
                if (seen.Add(actionName))
                {
                    yield return actionName;
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.KeydownActionName))
            {
                string actionName = skill.KeydownActionName;
                if (seen.Add(actionName))
                {
                    yield return actionName;
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.KeydownEndActionName))
            {
                string actionName = skill.KeydownEndActionName;
                if (seen.Add(actionName))
                {
                    yield return actionName;
                }
            }
        }
    }
}
