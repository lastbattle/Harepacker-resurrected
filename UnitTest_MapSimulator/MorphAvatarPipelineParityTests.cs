using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class MorphAvatarPipelineParityTests
    {
        [Fact]
        public void EnumerateClientActionAliases_PreservesExactPublishedMorphActions()
        {
            CharacterPart morphPart = CreateMorphPart("stormbreak", "windspear", "shoot1", "swingT1", "archerDoubleJump");

            Assert.Equal("stormbreak", EnumerateAliases(morphPart, "stormbreak").First());
            Assert.Equal("windspear", EnumerateAliases(morphPart, "windspear").First());
            Assert.Equal("shoot1", EnumerateAliases(morphPart, "shoot1").First());
            Assert.Equal("swingT1", EnumerateAliases(morphPart, "swingT1").First());
            Assert.Equal("archerDoubleJump", EnumerateAliases(morphPart, "archerDoubleJump").First());
        }

        [Fact]
        public void EnumerateClientActionAliases_KeepsDoubleJumpPromotionExplicit()
        {
            CharacterPart morphPart = CreateMorphPart("jump", "archerDoubleJump");

            Assert.Equal("archerDoubleJump", EnumerateAliases(morphPart, "doubleJump").First());
            Assert.Equal("jump", EnumerateAliases(morphPart, "jump").First());
        }

        [Fact]
        public void EnumerateClientActionAliases_PrioritizesPublishedGenericAndArcherMorphRangedFallbacks()
        {
            CharacterPart morphPart = CreateMorphPart("shoot1", "shootF", "arrowRain", "windshot", "fist", "edrain");

            Assert.Equal("shoot1", EnumerateAliases(morphPart, "paralyze").First());
            Assert.Equal("shoot1", EnumerateAliases(morphPart, "shoot6").First());
            Assert.Equal("arrowRain", EnumerateAliases(morphPart, "rain").First());
            Assert.Equal("arrowRain", EnumerateAliases(morphPart, "arrowEruption").First());

            List<string> pirateOnlyRainAliases = EnumerateAliases(CreateMorphPart("fist", "edrain"), "rain");
            Assert.Equal("fist", pirateOnlyRainAliases.First());
            Assert.True(
                pirateOnlyRainAliases.IndexOf("edrain") > pirateOnlyRainAliases.IndexOf("fist"),
                "Pirate-only morphs should not over-promote edrain for the raw rain request.");
        }

        [Fact]
        public void EnumerateClientActionAliases_FallsBackToPublishedGenericMeleeBeforeCarryOverAliases()
        {
            CharacterPart genericMeleeMorph = CreateMorphPart("swingT1", "swingT3", "fist");
            Assert.Equal("swingT1", EnumerateAliases(genericMeleeMorph, "swingT2").First());
            Assert.Equal("swingT1", EnumerateAliases(genericMeleeMorph, "savage").First());

            CharacterPart stabFallbackMorph = CreateMorphPart("swingT1", "swingT3", "fist");
            Assert.Equal("swingT1", EnumerateAliases(stabFallbackMorph, "stabT2").First());
            Assert.Equal("swingT1", EnumerateAliases(stabFallbackMorph, "stabOF").First());
        }

        [Fact]
        public void EnumerateClientActionAliases_UsesNearestIndexedAlertFallbackBeforeBaseAlert()
        {
            CharacterPart morphPart = CreateMorphPart("alert4", "alert2", "alert");

            List<string> aliases = EnumerateAliases(morphPart, "alert6");

            Assert.Equal("alert4", aliases[1]);
            Assert.Equal("alert2", aliases[2]);
            Assert.Equal("alert", aliases[3]);
        }

        [Fact]
        public void EnumerateMorphTemplateCandidatesForTesting_KeepsPairedAndLinkedBackfillInsideLoaderOrder()
        {
            IReadOnlyList<int> candidates = CharacterLoader.EnumerateMorphTemplateCandidatesForTesting(
                1003,
                morphTemplateId => morphTemplateId switch
                {
                    1103 => 1100,
                    1000 => 1001,
                    _ => 0
                });

            Assert.Equal(new[] { 1003, 1103, 1100, 1000, 1001 }, candidates);
        }

        [Fact]
        public void EnumerateFlagOnlyMorphTemplateCandidatesForTesting_UsesSuffixReductionForCurrentOutliers()
        {
            Assert.Equal(new[] { 109 }, SkillLoader.EnumerateFlagOnlyMorphTemplateCandidatesForTesting(0010109));
            Assert.Equal(new[] { 111 }, SkillLoader.EnumerateFlagOnlyMorphTemplateCandidatesForTesting(20020111));
        }

        private static List<string> EnumerateAliases(CharacterPart morphPart, string actionName)
        {
            return MorphClientActionResolver.EnumerateClientActionAliases(morphPart, actionName).ToList();
        }

        private static CharacterPart CreateMorphPart(params string[] actionNames)
        {
            var part = new CharacterPart
            {
                Type = CharacterPartType.Morph
            };

            foreach (string actionName in actionNames)
            {
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    continue;
                }

                part.Animations[actionName] = CreateAnimation(actionName);
            }

            return part;
        }

        private static CharacterAnimation CreateAnimation(string actionName)
        {
            return new CharacterAnimation
            {
                ActionName = actionName,
                Frames = new List<CharacterFrame>
                {
                    new()
                }
            };
        }
    }
}
