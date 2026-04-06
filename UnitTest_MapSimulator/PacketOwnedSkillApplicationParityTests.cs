using HaCreator.MapSimulator;
using System.Collections.Generic;

namespace UnitTest_MapSimulator
{
    public sealed class PacketOwnedSkillApplicationParityTests
    {
        [Fact]
        public void BuildSkillIdCatalog_MergesPreferredIdsExactNamesAndDescriptionMarkers()
        {
            KeyValuePair<int, string>[] skillNames =
            {
                new(31101003, " Vengeance "),
                new(4120010, "Expert Throwing Star Handling"),
                new(9001000, "Unrelated Skill")
            };
            KeyValuePair<int, string>[] skillDescriptions =
            {
                new(4120010, "When Expert Throwing Star Handling is activated, the next attack will always be a Critical Attack."),
                new(9001000, "Does not match the packet-owned crit marker.")
            };

            HashSet<int> ids = PacketOwnedSkillAliasCatalog.BuildSkillIdCatalog(
                skillNames,
                skillDescriptions,
                preferredCurrentSkillId: 31101003,
                preferredLegacySkillId: 3120010,
                canonicalSkillName: "Vengeance",
                canonicalDescriptionFragment: "the next attack will always be a Critical Attack");

            Assert.Contains(31101003, ids);
            Assert.Contains(3120010, ids);
            Assert.Contains(4120010, ids);
            Assert.DoesNotContain(9001000, ids);
        }

        [Fact]
        public void BuildSkillIdCatalog_MatchesDescriptionCaseInsensitively()
        {
            KeyValuePair<int, string>[] skillDescriptions =
            {
                new(4120010, "THE NEXT ATTACK WILL ALWAYS BE A critical attack."),
                new(4120011, "The next throw will not consume a star.")
            };

            HashSet<int> ids = PacketOwnedSkillAliasCatalog.BuildSkillIdCatalog(
                skillNames: null,
                skillDescriptions: skillDescriptions,
                preferredCurrentSkillId: 0,
                preferredLegacySkillId: 0,
                canonicalDescriptionFragment: "the next attack will always be a Critical Attack");

            Assert.Contains(4120010, ids);
            Assert.DoesNotContain(4120011, ids);
        }

        [Fact]
        public void BuildPreferredAliasCandidates_PrefersCurrentThenLegacyThenRemainingIds()
        {
            int[] candidates = PacketOwnedSkillAliasCatalog.BuildPreferredAliasCandidates(
                new[] { 31101003, 4120010, 3120010, 31101003, 4000000 },
                preferredCurrentSkillId: 31101003,
                preferredLegacySkillId: 3120010);

            Assert.Equal(new[] { 31101003, 3120010, 4000000, 4120010 }, candidates);
        }
    }
}
