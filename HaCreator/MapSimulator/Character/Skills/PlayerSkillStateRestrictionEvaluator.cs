using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Character.Skills
{
    /// <summary>
    /// Applies local player-state restrictions to skill usage.
    /// </summary>
    public static class PlayerSkillStateRestrictionEvaluator
    {
        private const double HighestJumpVelocityWindow = 80d;
        private const int ArrowWeaponType = 45;
        private const int CrossbowWeaponType = 46;
        private const int ThrowingStarWeaponType = 47;
        private const int BulletWeaponType = 49;
        private const int WindWalkSkillId = 11101005;
        private const int WildHunterJaguarJumpSkillId = 33001002;
        private const int NightLordFlashJumpSkillId = 4111006;
        private const int ShadowerFlashJumpSkillId = 4211009;
        private const int DualBladeFlashJumpSkillId = 4321003;
        private const int RocketBoosterSkillId = 35101004;
        private const int JaguarRiderSkillId = 33001001;
        private const int MechanicPrototypeSkillId = 35001002;
        private const int FlameLauncherSkillId = 35001001;
        private const int EnhancedFlameLauncherSkillId = 35101009;
        private const int EvanIceBreathSkillId = 22121000;
        private const int EvanFireBreathSkillId = 22151001;
        private const int NightWalkerPoisonBombSkillId = 14111006;
        private const int HermitTauntSkillId = 4121003;
        private const int NightlordTauntSkillId = 4341003;
        private const int ShadowerTauntSkillId = 4221003;
        private const int GunslingerBlankShotSkillId = 5201002;
        private const int HermitNinjaAmbushSkillId = 4121004;
        private const int ShadowerNinjaAmbushSkillId = 4221004;
        private const int PirateDashSkillId = 5001005;
        private const int ThunderBreakerDashSkillId = 15001003;
        private const int DualBladeTornadoSpinSkillId = 4321000;
        private const int CorsairBattleshipSkillId = 5221006;
        private const int BattleMageTwisterSpinSkillId = 32121003;
        private const int MonsterRidingSkillId = 1004;
        private const int MonsterRidingBeginnerSkillId = 10001004;
        private const int MonsterRidingEvanSkillId = 20001004;
        private const int MonsterRidingMechanicSkillId = 20011004;
        private const int SoaringSkillId = 1047;
        private const int SoaringCygnusSkillId = 10001047;
        private const int SoaringEvanSkillId = 20001047;
        private const int SoaringMechanicSkillId = 20011047;
        private static readonly HashSet<int> WildHunterJaguarTamingMobItemIds = new()
        {
            1932030,
            1932031,
            1932032,
            1932033,
            1932036,
            1932015
        };

        public static bool CanUseSkill(PlayerCharacter player, SkillData skill)
        {
            return CanUseSkill(player, skill, System.Environment.TickCount);
        }

        public static bool CanUseSkill(PlayerCharacter player, SkillData skill, int currentTime)
        {
            return GetRestrictionMessage(player, skill, currentTime) == null;
        }

        public static string GetRestrictionMessage(PlayerCharacter player, SkillData skill)
        {
            return GetRestrictionMessage(player, skill, System.Environment.TickCount);
        }

        public static string GetRestrictionMessage(PlayerCharacter player, SkillData skill, int currentTime)
        {
            if (player == null)
                return "Player state is unavailable.";

            if (!player.IsAlive || player.State == PlayerState.Dead)
                return "Skills cannot be used while dead.";

            if (player.State == PlayerState.Hit)
                return "Skills cannot be used while recovering from a hit.";

            if (player.State == PlayerState.Sitting)
                return "Skills cannot be used while seated.";

            if (player.State == PlayerState.Prone)
                return "Skills cannot be used while lying down.";

            string statusRestrictionMessage = player.GetSkillBlockingRestrictionMessage(currentTime);
            if (!string.IsNullOrWhiteSpace(statusRestrictionMessage))
                return statusRestrictionMessage;

            if (IsSwallowSkill(skill) && player.Physics?.IsOnLadderOrRope == true)
                return "Swallow skills cannot be used while on a ladder or rope.";

            string prepareRestrictionMessage = GetPrepareSkillRestrictionMessage(player, skill);
            if (!string.IsNullOrWhiteSpace(prepareRestrictionMessage))
                return prepareRestrictionMessage;

            string clientSpecificRestrictionMessage = GetClientSpecificRestrictionMessage(player, skill);
            if (!string.IsNullOrWhiteSpace(clientSpecificRestrictionMessage))
                return clientSpecificRestrictionMessage;

            string movementRestrictionMessage = GetMovementRestrictionMessage(player, skill);
            if (!string.IsNullOrWhiteSpace(movementRestrictionMessage))
                return movementRestrictionMessage;

            return null;
        }

        private static string GetPrepareSkillRestrictionMessage(PlayerCharacter player, SkillData skill)
        {
            if (player?.Physics == null || skill == null || (!skill.IsPrepareSkill && !skill.IsKeydownSkill))
            {
                return null;
            }

            if (IsPrepareBombSkill(skill.SkillId))
            {
                return player.Physics.IsOnLadderOrRope
                    ? "This prepared skill cannot be used while on a ladder or rope."
                    : null;
            }

            if (AllowsAirbornePreparedStart(skill.SkillId))
            {
                return player.Physics.IsOnLadderOrRope
                    ? "This prepared skill cannot be used while on a ladder or rope."
                    : null;
            }

            if (RequiresGroundedPreparedStart(skill.SkillId) && !player.Physics.IsOnFoothold())
            {
                return "This prepared skill must start from the ground.";
            }

            return !player.Physics.IsOnFoothold()
                   && !player.Physics.IsSwimming()
                   && !player.Physics.IsUserFlying()
                ? "This prepared skill cannot be used while airborne."
                : null;
        }

        private static string GetClientSpecificRestrictionMessage(PlayerCharacter player, SkillData skill)
        {
            if (player?.Physics == null || skill == null)
            {
                return null;
            }

            if (ShouldBlockClientShootAttackOnLadderOrRope(
                    skill,
                    player.Build?.GetWeapon()?.ItemId ?? 0,
                    player.Physics.IsOnLadderOrRope,
                    player.ResolveMountedStateTamingMobPart()?.ItemId ?? 0))
            {
                return "This skill cannot be used while on a ladder or rope.";
            }

            if (UsesLadderOrRopeCastGate(skill) && player.Physics.IsOnLadderOrRope)
            {
                return "This skill cannot be used while on a ladder or rope.";
            }

            if (RequiresStableVehicleCastState(skill)
                && !player.Physics.IsOnFoothold()
                && !player.Physics.IsOnLadderOrRope
                && !player.Physics.IsSwimming()
                && !player.Physics.IsUserFlying())
            {
                return "This skill cannot be used while airborne.";
            }

            return null;
        }

        internal static bool ShouldBlockClientShootAttackOnLadderOrRope(
            SkillData skill,
            int equippedWeaponItemId,
            bool isOnLadderOrRope,
            int mountedTamingMobItemId)
        {
            if (!isOnLadderOrRope
                || skill?.IsAttack != true
                || skill.AttackType != SkillAttackType.Ranged
                || IsShootSkillNotUsingShootingWeapon(skill.SkillId))
            {
                return false;
            }

            return IsShootingWeaponCode(GetWeaponCode(equippedWeaponItemId))
                && !IsWildHunterJaguarMountItemId(mountedTamingMobItemId);
        }

        private static string GetMovementRestrictionMessage(PlayerCharacter player, SkillData skill)
        {
            if (player?.Physics == null || skill == null)
            {
                return null;
            }

            bool usesBoundJumpStateGate = UsesBoundJumpStateGate(skill);
            bool usesHighestJumpGate = UsesHighestJumpStateGate(skill);
            if (!usesBoundJumpStateGate && !usesHighestJumpGate)
            {
                return null;
            }

            if (player.Physics.IsOnLadderOrRope)
            {
                return "Bound-jump skills cannot be used while on a ladder or rope.";
            }

            if (usesBoundJumpStateGate && RequiresGroundedBoundJumpStart(skill))
            {
                if (!player.Physics.IsOnFoothold())
                {
                    return "This movement skill must start from the ground.";
                }

                if (skill.SkillId == WildHunterJaguarJumpSkillId
                    && (player.Physics.IsSwimming() || player.Physics.IsUserFlying()))
                {
                    return "This movement skill cannot be used while swimming or flying.";
                }

                return null;
            }

            if (!player.Physics.IsAirborne())
            {
                return usesHighestJumpGate
                    ? "This skill must be used while airborne."
                    : "Bound-jump skills must be chained while airborne.";
            }

            if (player.Physics.IsFreeFalling() && player.Physics.IsFalling())
            {
                return usesHighestJumpGate
                    ? "This skill cannot be used after the jump has already turned into a fall."
                    : "Bound-jump skills cannot be used after the jump has already turned into a fall.";
            }

            if (usesHighestJumpGate && Math.Abs(player.Physics.VelocityY) > HighestJumpVelocityWindow)
            {
                return "This skill must be used near the top of a jump.";
            }

            return null;
        }

        private static bool IsSwallowSkill(SkillData skill)
        {
            return skill?.IsSwallowFamilySkill == true;
        }

        private static bool IsPrepareBombSkill(int skillId)
        {
            return skillId == NightlordTauntSkillId
                   || skillId == GunslingerBlankShotSkillId
                   || skillId == NightWalkerPoisonBombSkillId;
        }

        private static bool RequiresGroundedPreparedStart(int skillId)
        {
            return skillId == FlameLauncherSkillId
                   || skillId == EnhancedFlameLauncherSkillId;
        }

        private static bool AllowsAirbornePreparedStart(int skillId)
        {
            return skillId == EvanIceBreathSkillId
                   || skillId == EvanFireBreathSkillId;
        }

        private static bool UsesLadderOrRopeCastGate(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            int skillId = skill.SkillId;
            return skillId == HermitTauntSkillId
                   || skillId == ShadowerTauntSkillId
                   || skillId == HermitNinjaAmbushSkillId
                   || skillId == ShadowerNinjaAmbushSkillId
                   || skillId == PirateDashSkillId
                   || skillId == ThunderBreakerDashSkillId
                   || skillId == DualBladeTornadoSpinSkillId
                   || skillId == CorsairBattleshipSkillId
                   || skillId == BattleMageTwisterSpinSkillId
                   || skillId == SoaringSkillId
                   || skillId == SoaringCygnusSkillId
                   || skillId == SoaringEvanSkillId
                   || skillId == SoaringMechanicSkillId
                   || UsesVehicleOwnershipOrMountSkill(skill);
        }

        private static bool IsShootSkillNotUsingShootingWeapon(int skillId)
        {
            return skillId is 11101004
                or 4121003
                or 4221003
                or 5121002
                or 15111006
                or 15111007
                or 21100004
                or 21110004
                or 21120006
                or 33101007;
        }

        private static bool RequiresStableVehicleCastState(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            int skillId = skill.SkillId;
            return skillId == JaguarRiderSkillId
                   || skillId == MechanicPrototypeSkillId
                   || skillId == MonsterRidingSkillId
                   || skillId == MonsterRidingBeginnerSkillId
                   || skillId == MonsterRidingEvanSkillId
                   || skillId == MonsterRidingMechanicSkillId
                   || UsesVehicleOwnershipOrMountSkill(skill);
        }

        private static bool UsesVehicleOwnershipOrMountSkill(SkillData skill)
        {
            return ClientOwnedVehicleSkillClassifier.UsesVehicleOwnershipOrMountSkill(skill);
        }

        private static bool LooksLikeRideDescriptionBuff(SkillData skill)
        {
            return ClientOwnedVehicleSkillClassifier.LooksLikeClientOwnedRideDescriptionBuff(skill);
        }

        private static bool UsesBoundJumpStateGate(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            if (IsExplicitBoundJumpSkill(skill.SkillId))
            {
                return true;
            }

            if (skill.CasterMove
                && skill.AvailableInJumpingState
                && UsesBoundJumpActionProfile(skill))
            {
                return true;
            }

            return skill.ClientInfoType == 40
                   && skill.CasterMove
                   && skill.AvailableInJumpingState;
        }

        private static bool UsesHighestJumpStateGate(SkillData skill)
        {
            return skill?.RequireHighestJump == true
                   && skill.AvailableInJumpingState;
        }

        private static bool RequiresGroundedBoundJumpStart(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            return skill.SkillId == WindWalkSkillId
                   || skill.SkillId == WildHunterJaguarJumpSkillId
                   || skill.SkillId == RocketBoosterSkillId;
        }

        private static bool UsesBoundJumpActionProfile(SkillData skill)
        {
            return EnumerateMovementActionCandidates(skill).Any(IsBoundJumpActionName)
                   || ActionTextContains(skill?.Name, "flash jump");
        }

        private static IEnumerable<string> EnumerateMovementActionCandidates(SkillData skill)
        {
            if (skill == null)
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(skill.PrepareActionName) && seen.Add(skill.PrepareActionName))
            {
                yield return skill.PrepareActionName;
            }

            if (skill.ActionNames != null)
            {
                foreach (string actionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(actionName) && seen.Add(actionName))
                    {
                        yield return actionName;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill.ActionName) && seen.Add(skill.ActionName))
            {
                yield return skill.ActionName;
            }
        }

        private static bool IsBoundJumpActionName(string actionName)
        {
            return ActionTextContains(actionName, "doublejump")
                   || ActionTextContains(actionName, "flash jump")
                   || ActionTextContains(actionName, "archerdoublejump")
                   || ActionTextContains(actionName, "backspin")
                   || ActionTextContains(actionName, "assaulter")
                   || ActionTextContains(actionName, "screw");
        }

        private static bool ActionTextContains(string actionName, string value)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && !string.IsNullOrWhiteSpace(value)
                   && actionName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsShootingWeaponCode(int weaponCode)
        {
            return weaponCode is ArrowWeaponType
                or CrossbowWeaponType
                or ThrowingStarWeaponType
                or BulletWeaponType;
        }

        private static bool IsWildHunterJaguarMountItemId(int itemId)
        {
            return WildHunterJaguarTamingMobItemIds.Contains(itemId);
        }

        private static int GetWeaponCode(int itemId)
        {
            return itemId > 0 ? Math.Abs(itemId / 10000) % 100 : 0;
        }

        private static bool IsExplicitBoundJumpSkill(int skillId)
        {
            return skillId == WindWalkSkillId
                   || skillId == WildHunterJaguarJumpSkillId
                   || skillId == NightLordFlashJumpSkillId
                   || skillId == ShadowerFlashJumpSkillId
                   || skillId == DualBladeFlashJumpSkillId
                   || skillId == RocketBoosterSkillId;
        }
    }
}
