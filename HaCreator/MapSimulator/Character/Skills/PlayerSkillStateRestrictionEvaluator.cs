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

            return null;
        }

        private static bool IsSwallowSkill(SkillData skill)
        {
            return skill?.IsSwallowSkill == true
                   || (skill?.DummySkillParents?.Length > 0);
        }
    }
}
