using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class ClientOwnedVehicleSkillClassifier
    {
        private const int BattleshipSkillId = 5221006;
        private const int BattleshipTamingMobItemId = 1932000;
        private const int MechanicTamingMobItemId = 1932016;
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

        private static readonly int[] MechanicVehicleTransformSkillIds =
        {
            35001001,
            35101004,
            35101009,
            35111004,
            35121003,
            35121005,
            35121009,
            35121010,
            35121013
        };
        private static readonly IReadOnlySet<int> EventVehicleType1ItemIds =
            new HashSet<int>
            {
                1932001,
                1932002
            };
        private static readonly IReadOnlySet<int> EventVehicleType2ItemIds =
            new HashSet<int>
            {
                1932004,
                1932006,
                1932007,
                1932008,
                1932009,
                1932010,
                1932011,
                1932012,
                1932013,
                1932014,
                1932017,
                1932018,
                1932019,
                1932020,
                1932021,
                1932022,
                1932023,
                1932025,
                1932026,
                1932027,
                1932028,
                1932029,
                1932034,
                1932035,
                1932037,
                1932038,
                1932039,
                1932040
            };
        private static readonly IReadOnlySet<int> WildHunterJaguarTamingMobItemIds =
            new HashSet<int>
            {
                1932015,
                1932030,
                1932031,
                1932032,
                1932033,
                1932036
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

        private static readonly string[] SharedClientOwnedVehicleVehicleIdOnlyActionNames =
            ResolveClientRawActionNames(
                4,  // alert
                // IDA `CActionMan::LoadTamingMobAction` admits raw 55 and 210 in the
                // same pre-gate mounted-preservation bucket when a mount owner already
                // exists, without introducing ownerless inference.
                55, // iceStrike
                210, // quadBlow
                // IDA `IsAbleTamingMobAction` admits raw actions 4 and 42-46 for mounted action
                // coverage before vehicle-specific branches.
                // Keep these as owner-preservation only for mounts that already own the seam.
                42, // paralyze
                43, // ladder2
                44, // rope2
                // IDA `CActionMan::LoadTamingMobAction` remaps these raw actions to mount-safe
                // fallback actions before vehicle-family gating:
                // 270 -> 43 (ladder2), 271 -> 2 (stand1), 272 -> 2 (stand1).
                270, // braveslash3
                271, // braveslash4
                272  // chargeBlow
            );

        private static readonly string[] ClientConfirmedVehicle193FamilyOnlyActionNames =
            ResolveClientRawActionNames(
                // IDA `CActionMan::LoadTamingMobAction` admits these raw actions in the
                // nVehicleID/10000 == 193 branch before IsAbleTamingMobAction checks.
                58,  // avenger
                59,  // assaulter
                118, // firestrike
                119, // flamegear
                132, // tripleSwing
                133  // finalCharge
            );

        private static readonly string[] ClientConfirmedMechanicAndEventVehicleType2OnlyActionNames =
            ResolveClientRawActionNames(
                // IDA `CActionMan::LoadTamingMobAction` routes raw 45/46 through the
                // 193-family event/jaguar/1932016 gate and does not admit 1932000 here.
                45, // shoot6
                46  // arrowRain
            );
        private static readonly string[] ClientConfirmedEventVehicleType1OnlyActionNames =
            ResolveClientRawActionNames(
                // IDA `CActionMan::LoadTamingMobAction` keeps raw 144 gated behind
                // is_event_vehicle_type1(v6) before generic IsAbleTamingMob* checks.
                144 // comboJudgement
            );
        private static readonly string[] ClientConfirmedWildHunterJaguarOneTimeActionNames =
            ResolveClientRawActionNames(
                // IDA `IsAbleTamingMobOneTimeAction` has a dedicated wild-hunter jaguar
                // gate for these raw actions before the 1932016-only one-time branch.
                57,  // burster2
                142, // rollingSpin
                207, // finishAttack_link2
                208, // swingRes
                212, // finishBlow
                214, // cyclone_pre
                229, // tank_siegepre
                247, // swallow_attack
                248, // drillrush
                249, // giant
                250, // mbooster
                251, // crossRoad
                253, // wildbeast
                255, // earthslug
                256  // rpunch
            );
        private static readonly string[] WzOnlyWildHunterJaguarVehicleOneTimeActionNames =
            ResolveClientRawActionNames(
                // WZ publishes these roots on checked jaguar vehicles such as
                // Character/TamingMob/01932015, but v95 `IsAbleTamingMobOneTimeAction`
                // does not admit them for the wild-hunter jaguar vehicle family.
                242, // doubleJump
                243, // knockback
                244, // swallow_pre
                245, // swallow_loop
                246, // swallow
                254, // sonicBoom
                260, // clawCut
                261, // mine
                262, // ride
                263, // getoff
                265, // proneStab_jaguar
                266, // herbalism_jaguar
                267  // mining_jaguar
            );

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

        private static readonly string[] ExplicitMechanicVehiclePresentationActionNames =
        {
            "swingT1",
            "swingT2",
            "alert2",
            "shot",
            "ride2",
            "getoff2",
            "ladder2",
            "rope2"
        };

        private static readonly string[] ClientConfirmedMechanicVehicleVehicleIdOnlyActionNames =
            ResolveClientRawActionNames(
                9,   // swingT1
                10,  // swingT2
                45,  // shoot6
                46,  // arrowRain
                56,  // burster1
                64,  // rush2
                65,  // sanctuary
                116, // blade
                209, // tripleBlow
                210, // quadBlow
                211, // deathBlow
                212, // finishBlow
                215, // cyclone
                216, // cyclone_after
                217, // lasergun
                242, // doubleJump
                243, // knockback
                244, // swallow_pre
                245, // swallow_loop
                246, // swallow
                259, // flashRain
                260, // clawCut
                261, // mine
                262, // ride
                263, // getoff
                264  // capture
            );

        private static readonly string[] WzOnlyMechanicVehicleOneTimeActionNames =
        {
            // WZ publishes these roots on Character/TamingMob/01932016, but v95
            // LoadTamingMobAction falls back before load_tamingmob_action because
            // neither IsAbleTamingMobAction nor IsAbleTamingMobOneTimeAction admits
            // the corresponding raw action codes for vehicle 1932016.
            "tank_siegeattack",
            "tank_siegestand",
            "tank_siegeafter",
            "ride2",
            "getoff2",
            "flamethrower_pre",
            "flamethrower",
            "flamethrower_after",
            "flamethrower_pre2",
            "flamethrower2",
            "flamethrower_after2",
            "gatlingshot",
            "herbalism_mechanic",
            "mining_mechanic"
        };

        private static readonly string[] ClientConfirmedMechanicVehicleRenderableOneTimeActionNames =
            ResolveClientRawActionNames(
                241, // gatlingshot2
                235, // tank_rbooster_after
                248, // drillrush
                250, // mbooster
                255, // earthslug
                256  // rpunch
            );

        private static readonly string[] ClientConfirmedMechanicVehicleOwnerOnlyOneTimeActionNames =
            ResolveClientRawActionNames(
                // IDA admits raw actions 249 and 251 for vehicle id 1932016, but
                // Character/TamingMob/01932016 does not publish these roots. Preserve
                // the known owner without treating the names as renderable mount frames.
                249, // giant
                251  // crossRoad
            );

        private static readonly string[] ClientConfirmedMechanicVehicleCurrentActionNames =
        {
            // WZ publishes these exact roots on Character/TamingMob/01932016, and the client
            // taming-mob action gates key the extra coverage by vehicle id rather than prefix.
            // The active transform-owner seam still comes from MoveAction2RawAction and the
            // active 35121005 tank path in PrepareActionLayer, so the one-time surface above
            // is admitted only after a 1932016 taming-mob owner is already known.
            "tank_pre",
            "tank",
            "tank_walk",
            "tank_prone",
            "tank_stand",
            "tank_after",
            "tank_laser",
            "tank_siegepre",
            "tank_siegeattack",
            "tank_siegestand",
            "tank_siegeafter",
            "tank_msummon",
            "tank_msummon2",
            "tank_rbooster_pre",
            "tank_rbooster_after",
            "tank_mRush",
            "siege_pre",
            "siege",
            "siege_stand",
            "siege_after",
            "rbooster_pre",
            "rbooster",
            "rbooster_after",
            "msummon",
            "msummon2",
            "ride2",
            "getoff2",
            "mRush",
            "flamethrower_pre",
            "flamethrower",
            "flamethrower_after",
            "flamethrower_pre2",
            "flamethrower2",
            "flamethrower_after2",
            "herbalism_mechanic",
            "mining_mechanic"
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

        internal static bool IsMechanicVehicleTransformSkillId(int skillId)
        {
            return Array.IndexOf(MechanicVehicleTransformSkillIds, skillId) >= 0;
        }

        internal static bool IsExplicitMechanicVehiclePresentationSkillId(int? skillId)
        {
            return skillId.HasValue
                   && Array.IndexOf(MechanicVehicleStateSkillIds, skillId.Value) >= 0;
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

            if (IsWzOnlyMechanicVehicleOneTimeActionName(actionName))
            {
                return false;
            }

            if (includeTransformStates
                && (string.Equals(actionName, "tank", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "siege", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!includeTransformStates
                && (string.Equals(actionName, "tank", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "siege", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return ContainsActionName(ClientConfirmedMechanicVehicleCurrentActionNames, actionName);
        }

        internal static bool IsExplicitMechanicVehiclePresentationActionName(string actionName)
        {
            return ContainsActionName(ExplicitMechanicVehiclePresentationActionNames, actionName);
        }

        internal static bool IsWzOnlyMechanicVehicleOneTimeActionName(string actionName)
        {
            return ContainsActionName(WzOnlyMechanicVehicleOneTimeActionNames, actionName);
        }

        internal static bool IsClientAdmittedMechanicVehicleOneTimeActionName(string actionName)
        {
            return IsClientAdmittedMechanicVehicleRenderableOneTimeActionName(actionName)
                   || IsClientAdmittedMechanicVehicleOwnerOnlyOneTimeActionName(actionName);
        }

        internal static bool IsClientAdmittedMechanicVehicleRenderableOneTimeActionName(string actionName)
        {
            return ContainsActionName(ClientConfirmedMechanicVehicleRenderableOneTimeActionNames, actionName);
        }

        internal static bool IsClientAdmittedMechanicVehicleOwnerOnlyOneTimeActionName(string actionName)
        {
            return ContainsActionName(ClientConfirmedMechanicVehicleOwnerOnlyOneTimeActionNames, actionName);
        }

        internal static bool IsWzOnlyWildHunterJaguarVehicleOneTimeActionName(string actionName)
        {
            return ContainsActionName(WzOnlyWildHunterJaguarVehicleOneTimeActionNames, actionName);
        }

        internal static bool IsWzOnlyClientOwnedVehicleOneTimeActionName(int mountItemId, string actionName)
        {
            if (mountItemId == MechanicTamingMobItemId)
            {
                return IsWzOnlyMechanicVehicleOneTimeActionName(actionName);
            }

            return IsWildHunterJaguarTamingMobItemId(mountItemId)
                   && IsWzOnlyWildHunterJaguarVehicleOneTimeActionName(actionName);
        }

        internal static bool IsOverlappingMechanicVehicleOneTimeActionName(string actionName)
        {
            return IsWzOnlyMechanicVehicleOneTimeActionName(actionName)
                   || IsClientAdmittedMechanicVehicleRenderableOneTimeActionName(actionName);
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
                   || ContainsActionName(ClientConfirmedVehicle193FamilyOnlyActionNames, actionName)
                   || ContainsActionName(SharedClientOwnedVehicleVehicleIdOnlyActionNames, actionName)
                   || IsMountedMoveActionName(actionName);
        }

        internal static bool IsMechanicVehicleOwnedCurrentActionName(string actionName, bool includeTransformStates = false)
        {
            if (IsMechanicVehicleActionName(actionName, includeTransformStates))
            {
                return true;
            }

            if (ContainsActionName(ClientConfirmedMechanicVehicleVehicleIdOnlyActionNames, actionName))
            {
                return true;
            }

            if (IsClientAdmittedMechanicVehicleOneTimeActionName(actionName))
            {
                return true;
            }

            return IsMountedMoveActionName(actionName)
                   || ContainsActionName(ClientConfirmedVehicle193FamilyOnlyActionNames, actionName)
                   || ContainsActionName(ClientConfirmedMechanicAndEventVehicleType2OnlyActionNames, actionName)
                   || ContainsActionName(MechanicClientOwnedVehicleMountedMoveActions, actionName);
        }

        internal static bool SupportsExplicitMechanicVehiclePresentationCurrentAction(string actionName)
        {
            return string.IsNullOrWhiteSpace(actionName)
                   || IsKnownMechanicVehicleCurrentActionName(actionName);
        }

        internal static bool IsKnownClientOwnedVehicleCurrentActionName(int mountItemId, string actionName)
        {
            if (mountItemId == 1932000)
            {
                return IsBattleshipVehicleOwnedCurrentActionName(
                    actionName,
                    includeSupportActions: true);
            }

            if (mountItemId == 1932016)
            {
                return IsKnownMechanicVehicleCurrentActionName(actionName);
            }

            if (IsEventVehicleType1TamingMobItemId(mountItemId))
            {
                return IsKnownEventVehicleType1CurrentActionName(actionName);
            }

            if (IsEventVehicleType2TamingMobItemId(mountItemId))
            {
                return IsKnownEventVehicleType2CurrentActionName(actionName);
            }

            if (IsWildHunterJaguarTamingMobItemId(mountItemId))
            {
                return IsKnownWildHunterJaguarCurrentActionName(actionName);
            }

            return false;
        }

        internal static bool IsClientOwnedVehicleMountOwnerItemId(int mountItemId)
        {
            return mountItemId == BattleshipTamingMobItemId
                   || mountItemId == 1932016
                   || IsEventVehicleType1TamingMobItemId(mountItemId)
                   || IsEventVehicleType2TamingMobItemId(mountItemId)
                   || IsWildHunterJaguarTamingMobItemId(mountItemId);
        }

        internal static bool IsEventVehicleType1TamingMobItemId(int mountItemId)
        {
            return EventVehicleType1ItemIds.Contains(mountItemId);
        }

        internal static bool IsEventVehicleType2TamingMobItemId(int mountItemId)
        {
            return EventVehicleType2ItemIds.Contains(mountItemId);
        }

        internal static bool IsWildHunterJaguarTamingMobItemId(int mountItemId)
        {
            return WildHunterJaguarTamingMobItemIds.Contains(mountItemId);
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
            // WZ publishes the full tank/siege families on Character/00002000.img as well as on
            // Character/TamingMob/01932016, so action names alone are no longer a trustworthy
            // local-owner seam for Mechanic vehicle ownership.
            return false;
        }

        private static bool IsMountedMoveActionName(string actionName)
        {
            return ContainsActionName(SharedClientOwnedVehicleMountedMoveActions, actionName);
        }

        private static bool IsKnownMechanicVehicleCurrentActionName(string actionName)
        {
            if (IsWzOnlyMechanicVehicleOneTimeActionName(actionName))
            {
                return false;
            }

            return IsMountedMoveActionName(actionName)
                   || ContainsActionName(MechanicClientOwnedVehicleMountedMoveActions, actionName)
                   || ContainsActionName(SharedClientOwnedVehicleVehicleIdOnlyActionNames, actionName)
                   || ContainsActionName(ClientConfirmedVehicle193FamilyOnlyActionNames, actionName)
                   || ContainsActionName(ClientConfirmedMechanicAndEventVehicleType2OnlyActionNames, actionName)
                   || ContainsActionName(ClientConfirmedMechanicVehicleCurrentActionNames, actionName)
                   || ContainsActionName(ClientConfirmedMechanicVehicleVehicleIdOnlyActionNames, actionName)
                   || IsClientAdmittedMechanicVehicleOneTimeActionName(actionName);
        }

        private static bool IsKnownEventVehicleType1CurrentActionName(string actionName)
        {
            return IsMountedMoveActionName(actionName)
                   || ContainsActionName(SharedClientOwnedVehicleVehicleIdOnlyActionNames, actionName)
                   || ContainsActionName(ClientConfirmedVehicle193FamilyOnlyActionNames, actionName)
                   || ContainsActionName(ClientConfirmedEventVehicleType1OnlyActionNames, actionName);
        }

        private static bool IsKnownEventVehicleType2CurrentActionName(string actionName)
        {
            return IsMountedMoveActionName(actionName)
                   || ContainsActionName(SharedClientOwnedVehicleVehicleIdOnlyActionNames, actionName)
                   || ContainsActionName(ClientConfirmedVehicle193FamilyOnlyActionNames, actionName)
                   || ContainsActionName(ClientConfirmedMechanicAndEventVehicleType2OnlyActionNames, actionName);
        }

        private static bool IsKnownWildHunterJaguarCurrentActionName(string actionName)
        {
            return IsKnownEventVehicleType2CurrentActionName(actionName)
                   || ContainsActionName(ClientConfirmedWildHunterJaguarOneTimeActionNames, actionName);
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

        private static string[] ResolveClientRawActionNames(params int[] rawActionCodes)
        {
            if (rawActionCodes == null || rawActionCodes.Length == 0)
            {
                return Array.Empty<string>();
            }

            var names = new List<string>(rawActionCodes.Length);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (int rawActionCode in rawActionCodes)
            {
                if (!CharacterPart.TryGetActionStringFromCode(rawActionCode, out string actionName)
                    || string.IsNullOrWhiteSpace(actionName)
                    || !seen.Add(actionName))
                {
                    continue;
                }

                names.Add(actionName);
            }

            return names.ToArray();
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
