using System;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillCooldownTooltipText
    {
        public static string FormatCooldownState(int remainingMs)
        {
            if (remainingMs <= 0)
            {
                return "Ready";
            }

            int seconds = Math.Max(1, (int)Math.Ceiling(remainingMs / 1000f));
            return $"{seconds}s remaining";
        }
    }
}
