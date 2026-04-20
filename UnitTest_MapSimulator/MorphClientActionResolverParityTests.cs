using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class MorphClientActionResolverParityTests
    {
        public static IEnumerable<object[]> CannonFamilyRedirectCases()
        {
            yield return new object[] { "flamesplash", new[] { "shootF", "stabO1", "alert", "doublefire" } };
            yield return new object[] { "swiftShot", new[] { "swingO3", "stabO1", "alert", "doublefire" } };
            yield return new object[] { "cannonSmash", new[] { "stabO1", "alert", "doublefire" } };
            yield return new object[] { "giganticBackstep", new[] { "stabO1", "doublefire" } };
            yield return new object[] { "rushBoom", new[] { "alert", "swingOF", "swingTF", "swingT3", "doublefire" } };
            yield return new object[] { "cannonSlam", new[] { "alert", "swingPF", "stabO2", "swingP2", "swingT2", "swingP1", "doublefire" } };
            yield return new object[] { "counterCannon", new[] { "swingT3", "doublefire" } };
            yield return new object[] { "cannonSpike", new[] { "alert", "swingO3", "doublefire" } };
            yield return new object[] { "superCannon", new[] { "alert", "swingOF", "swingO3", "doublefire" } };
            yield return new object[] { "magneticCannon", new[] { "swingO2", "swingP2", "swingPF", "doublefire" } };
            yield return new object[] { "bombExplosion", new[] { "alert", "stabO1", "swingP1", "doublefire" } };
            yield return new object[] { "monkeyBoomboom", new[] { "alert", "swingP1", "doublefire" } };
            yield return new object[] { "immolation", new[] { "shootF", "stabO1", "doublefire" } };
            yield return new object[] { "piratebless", new[] { "alert", "swingP1", "doublefire" } };
            yield return new object[] { "pirateSpirit", new[] { "alert", "swingP2", "swingO2", "swingPF", "doublefire" } };
            yield return new object[] { "cannonBooster", new[] { "swingT2", "swingP1", "doublefire" } };
            yield return new object[] { "noiseWave", new[] { "swingOF", "swingTF", "swingT3", "alert", "doublefire" } };
            yield return new object[] { "noiseWave_pre", new[] { "alert", "doublefire" } };
            yield return new object[] { "noiseWave_ing", new[] { "alert", "doublefire" } };
        }

        [Theory]
        [MemberData(nameof(CannonFamilyRedirectCases))]
        public void CannonFamilyRequests_FollowCheckedBodyRedirectOrderBeforeAuthoredFallback(
            string requestedActionName,
            string[] expectedPrefix)
        {
            CharacterPart morphPart = CreateMorphPart(
                "shootF",
                "stabO1",
                "stabO2",
                "alert",
                "swingO2",
                "swingO3",
                "swingOF",
                "swingP1",
                "swingP2",
                "swingPF",
                "swingT2",
                "swingT3",
                "swingTF",
                "doublefire");

            string[] resolvedAliases = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, requestedActionName)
                .Take(expectedPrefix.Length)
                .ToArray();

            Assert.Equal(expectedPrefix, resolvedAliases);
        }

        [Theory]
        [MemberData(nameof(CannonFamilyRedirectCases))]
        public void CannonFamilyRequests_KeepDoublefireAsFinalBackstopWhenRedirectAliasesAreMissing(
            string requestedActionName,
            string[] _)
        {
            CharacterPart morphPart = CreateMorphPart("doublefire");

            string firstAlias = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, requestedActionName)
                .FirstOrDefault();

            Assert.Equal("doublefire", firstAlias);
        }

        [Fact]
        public void Recovery_PrefersExactPublishedBranch_BeforeBodyRedirectAliases()
        {
            CharacterPart morphPart = CreateMorphPart("recovery", "alert", "stand");

            string[] aliases = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, "recovery")
                .Take(3)
                .ToArray();

            Assert.Equal(new[] { "recovery", "alert", "stand" }, aliases);
        }

        [Fact]
        public void Recovery_FallsBackToAlertThenStand_WhenExactBranchMissing()
        {
            CharacterPart morphPart = CreateMorphPart("alert", "stand");

            string[] aliases = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, "recovery")
                .Take(2)
                .ToArray();

            Assert.Equal(new[] { "alert", "stand" }, aliases);
        }

        [Fact]
        public void DoubleJump_UsesClientBodyRedirectSurface_SitThenJump()
        {
            CharacterPart morphPart = CreateMorphPart("sit", "jump");

            string[] aliases = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, "doubleJump")
                .Take(2)
                .ToArray();

            Assert.Equal(new[] { "sit", "jump" }, aliases);
        }

        [Fact]
        public void Fastest_UsesCheckedMovementRedirectOrder()
        {
            CharacterPart morphPart = CreateMorphPart("rope", "swingPF", "swingOF", "fly", "jump", "stand");

            string[] aliases = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, "fastest")
                .Take(3)
                .ToArray();

            Assert.Equal(new[] { "rope", "swingPF", "swingOF" }, aliases);
        }

        [Theory]
        [InlineData("ride3", new[] { "stand1", "alert", "fly" })]
        [InlineData("demonSlasher", new[] { "stand1", "swingO2" })]
        [InlineData("braveslash1", new[] { "alert", "swingO2", "swingOF", "stabO1" })]
        [InlineData("braveslash3", new[] { "alert", "swingT1", "swingT3", "stabO1" })]
        [InlineData("darkChain", new[] { "swingO3", "swingO2", "stabO1" })]
        [InlineData("darkLightning", new[] { "swingO2", "stabO1", "stabO2" })]
        [InlineData("maxForce2", new[] { "stabOF", "swingOF", "swingO2", "swingO1" })]
        [InlineData("maxForce3", new[] { "swingO2", "swingO1", "swingO3", "stabOF" })]
        [InlineData("chargeBlow", new[] { "stabO1", "alert" })]
        [InlineData("chainPull", new[] { "stabO1", "swingO3" })]
        public void ClientPublishedAliasFamilies_PreserveMappedAliasOrder(
            string actionName,
            string[] expectedPrefix)
        {
            CharacterPart morphPart = CreateMorphPart(expectedPrefix);

            string[] aliases = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, actionName)
                .Take(expectedPrefix.Length)
                .ToArray();

            Assert.Equal(expectedPrefix, aliases);
        }

        [Fact]
        public void Stand1_FallsBackToStandWhenSuffixBranchMissing()
        {
            CharacterPart morphPart = CreateMorphPart("stand", "walk");

            string[] aliases = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, "stand1")
                .Take(2)
                .ToArray();

            Assert.Equal(new[] { "stand", "walk" }, aliases);
        }

        private static CharacterPart CreateMorphPart(params string[] actionNames)
        {
            var uniqueNames = new HashSet<string>(actionNames, StringComparer.OrdinalIgnoreCase);
            var animations = uniqueNames.ToDictionary(
                actionName => actionName,
                actionName => new CharacterAnimation { ActionName = actionName },
                StringComparer.OrdinalIgnoreCase);

            return new CharacterPart
            {
                Animations = animations,
                AvailableAnimations = new HashSet<string>(uniqueNames, StringComparer.OrdinalIgnoreCase)
            };
        }
    }
}
