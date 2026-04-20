using HaCreator.MapSimulator.Companions;
using System;
using System.Linq;

namespace UnitTest_MapSimulator
{
    public class PetActionLoaderParityTests
    {
        [Fact]
        public void ClientBaseActionOrder_MatchesRecoveredClientSlots()
        {
            string[] expected =
            {
                "move",
                "stand0",
                "stand1",
                "jump",
                "fly",
                "hungry",
                "rest0",
                "rest1",
                "hang"
            };

            Assert.Equal(expected, PetActionAliases.EnumerateClientBaseActions().ToArray());
        }

        [Fact]
        public void StartAlias_ResolvesStarBeforeFallback()
        {
            string[] candidates = PetActionAliases.EnumerateCandidates("start").ToArray();

            Assert.True(candidates.Length >= 2);
            Assert.Equal("start", candidates[0], ignoreCase: true);
            Assert.Equal("star", candidates[1], ignoreCase: true);
        }

        [Fact]
        public void SneerArroganceAliasFamily_IsBidirectionalAndKnown()
        {
            string[] sneerCandidates = PetActionAliases.EnumerateCandidates("sneer").ToArray();
            string[] arroganceCandidates = PetActionAliases.EnumerateCandidates("arrogance").ToArray();
            string[] knownActions = PetActionAliases.EnumerateKnownActions().ToArray();

            Assert.Contains("arrogance", sneerCandidates, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("sneer", arroganceCandidates, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("arrogance", knownActions, StringComparer.OrdinalIgnoreCase);
        }
    }
}
