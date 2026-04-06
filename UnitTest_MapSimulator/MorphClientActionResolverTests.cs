using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator
{
    public sealed class MorphClientActionResolverTests
    {
        [Fact]
        public void EnumerateClientActionAliases_ShotPrefersWindshotWhenPublished()
        {
            CharacterPart morphPart = CreateMorphPart(
                "shoot1",
                "shootF",
                "windshot",
                "windspear",
                "stormbreak",
                "arrowRain");

            string[] aliases = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, "shot")
                .Take(4)
                .ToArray();

            Assert.Equal("windshot", aliases[0]);
        }

        [Fact]
        public void EnumerateClientActionAliases_SpearPrefersWindspearWhenPublished()
        {
            CharacterPart morphPart = CreateMorphPart(
                "shoot1",
                "shootF",
                "windshot",
                "windspear",
                "stormbreak",
                "arrowRain");

            string[] aliases = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, "spear")
                .Take(4)
                .ToArray();

            Assert.Equal("windspear", aliases[0]);
        }

        [Fact]
        public void EnumerateClientActionAliases_BreakPrefersStormbreakWhenPublished()
        {
            CharacterPart morphPart = CreateMorphPart(
                "shoot1",
                "shootF",
                "windshot",
                "windspear",
                "stormbreak",
                "arrowRain");

            string[] aliases = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, "break")
                .Take(4)
                .ToArray();

            Assert.Equal("stormbreak", aliases[0]);
        }

        [Fact]
        public void EnumerateClientActionAliases_SpearUsesGenericShootBeforePirateFallbackWhenNoArcherAliasPublishes()
        {
            CharacterPart morphPart = CreateMorphPart(
                "shoot1",
                "shootF",
                "doublefire",
                "backspin");

            string[] aliases = MorphClientActionResolver
                .EnumerateClientActionAliases(morphPart, "spear")
                .Take(4)
                .ToArray();

            Assert.Equal("shoot1", aliases[0]);
            Assert.Equal("shootF", aliases[1]);
            Assert.Contains("doublefire", aliases);
        }

        private static CharacterPart CreateMorphPart(params string[] actionNames)
        {
            var part = new CharacterPart
            {
                Type = CharacterPartType.Morph,
                Animations = new Dictionary<string, CharacterAnimation>(System.StringComparer.OrdinalIgnoreCase)
            };

            foreach (string actionName in actionNames)
            {
                part.Animations[actionName] = new CharacterAnimation
                {
                    ActionName = actionName
                };
            }

            return part;
        }
    }
}
