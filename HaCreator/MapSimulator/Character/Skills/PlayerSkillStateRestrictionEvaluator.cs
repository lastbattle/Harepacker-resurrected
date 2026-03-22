using HaCreator.MapSimulator.Character;

namespace HaCreator.MapSimulator.Character.Skills
{
    /// <summary>
    /// Applies local player-state restrictions to skill usage.
    /// </summary>
    public static class PlayerSkillStateRestrictionEvaluator
    {
        public static bool CanUseSkill(PlayerCharacter player, SkillData skill)
        {
            return GetRestrictionMessage(player, skill) == null;
        }

        public static string GetRestrictionMessage(PlayerCharacter player, SkillData skill)
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

            if (IsSwallowSkill(skill) && player.Physics?.IsOnLadderOrRope == true)
                return "Swallow skills cannot be used while on a ladder or rope.";

            return null;
        }

        private static bool IsSwallowSkill(SkillData skill)
        {
            return skill?.ActionName?.Contains("swallow", System.StringComparison.OrdinalIgnoreCase) == true
                   || skill?.Name?.Contains("swallow", System.StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
