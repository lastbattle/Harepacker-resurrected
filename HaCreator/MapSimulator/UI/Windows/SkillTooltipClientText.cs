using System;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillTooltipClientText
    {
        internal const int CurrentLevelHeaderStringPoolId = 0x2B3;
        internal const int NextLevelHeaderStringPoolId = 0x2B4;
        internal const int RequiredSkillHeaderStringPoolId = 0x801;
        internal const int RequiredSkillLevelStringPoolId = 0x800;

        public static string FormatCurrentLevelHeader(int currentLevel)
        {
            return FormatLevelHeader(
                CurrentLevelHeaderStringPoolId,
                "[Current Level {0}]",
                currentLevel);
        }

        public static string FormatNextLevelHeader(int nextLevel)
        {
            return FormatLevelHeader(
                NextLevelHeaderStringPoolId,
                "[Next Level {0}]",
                nextLevel);
        }

        public static string ResolveRequiredSkillHeaderText()
        {
            return MapleStoryStringPool.GetOrFallback(RequiredSkillHeaderStringPoolId, "Required Skill");
        }

        public static string FormatRequiredSkillLevelText(int requiredLevel)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                RequiredSkillLevelStringPoolId,
                "Lv. {0}+",
                1,
                out _);
            return string.Format(format, Math.Max(1, requiredLevel));
        }

        private static string FormatLevelHeader(int stringPoolId, string fallbackFormat, int level)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                1,
                out _);
            return string.Format(format, Math.Max(0, level));
        }
    }
}
