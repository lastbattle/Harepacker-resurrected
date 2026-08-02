using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Loaders;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillRootRecommendationResolver
    {
        internal const int DualBladeRogueSkillRootId = 400;
        internal const int DualBladeFirstSkillRootId = 430;

        public static int ResolveRecommendationSourceSkillRootId(int currentSkillRootId)
        {
            return currentSkillRootId == DualBladeRogueSkillRootId || currentSkillRootId == DualBladeFirstSkillRootId
                ? DualBladeFirstSkillRootId
                : currentSkillRootId;
        }

        public static bool UsesCombinedDualBladeSpentPoints(int currentSkillRootId)
        {
            return currentSkillRootId == DualBladeRogueSkillRootId || currentSkillRootId == DualBladeFirstSkillRootId;
        }

        public static int ResolveRecommendedSkillId(
            IReadOnlyList<SkillDisplayData> visibleSkills,
            IReadOnlyList<SkillDataLoader.RecommendedSkillEntry> entries,
            int spentSkillLevels)
        {
            if (visibleSkills == null || visibleSkills.Count == 0 || entries == null || entries.Count == 0)
                return 0;

            if (!visibleSkills.Any(skill => skill != null))
                return 0;

            int previousSkillId = 0;
            foreach (SkillDataLoader.RecommendedSkillEntry entry in entries.OrderBy(entry => entry.SpentSpThreshold).ThenBy(entry => entry.SkillId))
            {
                if (entry.SpentSpThreshold > spentSkillLevels)
                    return previousSkillId;

                if (entry.SpentSpThreshold == spentSkillLevels)
                    return entry.SkillId;

                previousSkillId = entry.SkillId;
            }

            return 0;
        }
    }
}
