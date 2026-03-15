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

            return null;
        }
    }
}
