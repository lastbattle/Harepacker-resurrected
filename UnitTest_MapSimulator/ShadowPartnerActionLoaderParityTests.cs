using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public sealed class ShadowPartnerActionLoaderParityTests
    {
        [Fact]
        public void TryBuildPiecedShadowPartnerActionAnimation_BuiltInPositiveDelayPlanCarriesClientEventDelay()
        {
            var actionAnimations = new Dictionary<string, SkillAnimation>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["stabOF"] = new()
                {
                    Name = "stabOF",
                    Frames = new List<SkillFrame>
                    {
                        new() { Delay = 90 },
                        new() { Delay = 120 },
                        new() { Delay = 180 }
                    }
                }
            };

            SkillAnimation pieced = ShadowPartnerClientActionResolver.TryBuildPiecedShadowPartnerActionAnimation(
                actionAnimations,
                actionName: "assassinationS",
                supportedRawActionNames: null,
                piecePlanOverride: null,
                requireSupportedRawActionName: false);

            Assert.NotNull(pieced);
            Assert.Equal(240, pieced.ClientEventDelayMs);
        }

        [Fact]
        public void TryBuildPiecedShadowPartnerActionAnimation_BuiltInZigZagPlanKeepsZeroClientEventDelay()
        {
            var actionAnimations = new Dictionary<string, SkillAnimation>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["alert"] = new()
                {
                    Name = "alert",
                    Frames = new List<SkillFrame>
                    {
                        new() { Delay = 90 },
                        new() { Delay = 120 },
                        new() { Delay = 180 }
                    }
                }
            };

            IReadOnlyList<ShadowPartnerClientActionResolver.ShadowPartnerActionPiece> zigzagPlan = new[]
            {
                new ShadowPartnerClientActionResolver.ShadowPartnerActionPiece(0, "alert", 0, 120, IsClientActionManInitPiece: true),
                new ShadowPartnerClientActionResolver.ShadowPartnerActionPiece(1, "alert", 1, 180, IsClientActionManInitPiece: true),
                new ShadowPartnerClientActionResolver.ShadowPartnerActionPiece(2, "alert", 1, 180, IsSyntheticMirroredTailPiece: true, IsClientActionManInitPiece: true)
            };

            SkillAnimation pieced = ShadowPartnerClientActionResolver.TryBuildPiecedShadowPartnerActionAnimation(
                actionAnimations,
                actionName: "alert3",
                supportedRawActionNames: null,
                piecePlanOverride: zigzagPlan,
                requireSupportedRawActionName: false);

            Assert.NotNull(pieced);
            Assert.Equal(0, pieced.ClientEventDelayMs);
        }

        [Fact]
        public void EnumerateCreateActionCandidates_PreservesCreate2FamilyBeforeLegacyFallback()
        {
            List<string> candidates = ShadowPartnerClientActionResolver
                .EnumerateCreateActionCandidates(PlayerState.Standing)
                .Take(15)
                .ToList();

            int create2Index = candidates.FindIndex(candidate => string.Equals(candidate, "create2", StringComparison.OrdinalIgnoreCase));
            int create3Index = candidates.FindIndex(candidate => string.Equals(candidate, "create3", StringComparison.OrdinalIgnoreCase));
            int create4Index = candidates.FindIndex(candidate => string.Equals(candidate, "create4", StringComparison.OrdinalIgnoreCase));
            int create1Index = candidates.FindIndex(candidate => string.Equals(candidate, "create1", StringComparison.OrdinalIgnoreCase));
            int create0Index = candidates.FindIndex(candidate => string.Equals(candidate, "create0", StringComparison.OrdinalIgnoreCase));

            Assert.True(create2Index >= 0);
            Assert.True(create3Index > create2Index);
            Assert.True(create4Index > create3Index);
            Assert.True(create1Index > create4Index);
            Assert.True(create0Index > create1Index);
        }

        [Fact]
        public void ResolveCreateActionName_FallsBackToLegacyCreateFamilyWhenCreate2FamilyIsMissing()
        {
            var actionAnimations = new Dictionary<string, SkillAnimation>(StringComparer.OrdinalIgnoreCase)
            {
                ["create1"] = new() { Name = "create1", Frames = new List<SkillFrame> { new() { Delay = 120 } } },
                ["create0"] = new() { Name = "create0", Frames = new List<SkillFrame> { new() { Delay = 90 } } }
            };

            string resolved = ShadowPartnerClientActionResolver.ResolveCreateActionName(actionAnimations, PlayerState.Standing);
            Assert.Equal("create1", resolved);
        }

        [Fact]
        public void EnumerateClientMappedCandidates_SuppressesRawBackedGenericAttackIdentityCandidate()
        {
            int avengerRawActionCode = FindRawActionCode("avenger");
            Assert.True(avengerRawActionCode >= 0, "Expected to resolve raw action code for 'avenger'.");

            List<string> candidates = ShadowPartnerClientActionResolver
                .EnumerateClientMappedCandidates(
                    playerActionName: "attack1",
                    state: PlayerState.Attacking,
                    fallbackActionName: "stand1",
                    rawActionCode: avengerRawActionCode)
                .ToList();

            Assert.DoesNotContain("attack1", candidates, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("avenger", candidates, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void EnumerateClientMappedCandidates_UsesSlashAndBlowRawActionHeuristicAliases()
        {
            int deathBlowRawActionCode = FindRawActionCode("deathBlow");
            Assert.True(deathBlowRawActionCode >= 0, "Expected to resolve raw action code for 'deathBlow'.");

            List<string> candidates = ShadowPartnerClientActionResolver
                .EnumerateClientMappedCandidates(
                    playerActionName: "stand1",
                    state: PlayerState.Attacking,
                    fallbackActionName: "stand1",
                    rawActionCode: deathBlowRawActionCode)
                .ToList();

            Assert.Contains("swingO1", candidates, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldHoldBlockingAction_CompletesForAssassinationAndCreateFamilies()
        {
            var playback = new SkillAnimation
            {
                Name = "playback",
                Frames = new List<SkillFrame>
                {
                    new() { Delay = 100 },
                    new() { Delay = 100 }
                }
            };
            playback.CalculateDuration();

            Assert.True(ShadowPartnerClientActionResolver.ShouldHoldBlockingAction("assassinationS", playback, elapsedTimeMs: 100));
            Assert.False(ShadowPartnerClientActionResolver.ShouldHoldBlockingAction("assassinationS", playback, elapsedTimeMs: 250));

            Assert.True(ShadowPartnerClientActionResolver.ShouldHoldBlockingAction("create2", playback, elapsedTimeMs: 100));
            Assert.False(ShadowPartnerClientActionResolver.ShouldHoldBlockingAction("create2", playback, elapsedTimeMs: 250));
        }

        private static int FindRawActionCode(string actionName)
        {
            for (int code = 0; code < 0x400; code++)
            {
                if (CharacterPart.TryGetActionStringFromCode(code, out string resolved)
                    && string.Equals(resolved, actionName, StringComparison.OrdinalIgnoreCase))
                {
                    return code;
                }
            }

            return -1;
        }
    }
}
