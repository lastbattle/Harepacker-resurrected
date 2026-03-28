using HaCreator.MapSimulator.Character;
using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    /// <summary>
    /// Applies local player-state restrictions to skill usage.
    /// </summary>
    public static class PlayerSkillStateRestrictionEvaluator
    {
        private const double HighestJumpVelocityWindow = 80d;
        private const int WindWalkSkillId = 11101005;
        private const int WildHunterJaguarJumpSkillId = 33001002;
        private const int NightLordFlashJumpSkillId = 4111006;
        private const int ShadowerFlashJumpSkillId = 4211009;
        private const int DualBladeFlashJumpSkillId = 4321003;
        private const int RocketBoosterSkillId = 35101004;

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

            string movementRestrictionMessage = GetMovementRestrictionMessage(player, skill);
            if (!string.IsNullOrWhiteSpace(movementRestrictionMessage))
                return movementRestrictionMessage;

            return null;
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
