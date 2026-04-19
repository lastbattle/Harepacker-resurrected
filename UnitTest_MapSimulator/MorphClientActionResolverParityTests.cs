using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class MorphClientActionResolverParityTests
    {
        [Fact]
        public void Rbooster_PrefersBodyRedirectedAlertBeforeSit()
        {
            CharacterPart morphPart = CreateMorphPart("alert", "sit");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "rbooster").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("alert", aliases[0]);
            Assert.Contains("sit", aliases);
        }

        [Fact]
        public void Rbooster_FallsBackToSit_WhenAlertIsMissing()
        {
            CharacterPart morphPart = CreateMorphPart("sit");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "rbooster").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("sit", aliases[0]);
        }

        [Theory]
        [InlineData("siege_pre")]
        [InlineData("siege")]
        [InlineData("siege_stand")]
        [InlineData("siege_after")]
        [InlineData("tank_pre")]
        [InlineData("tank")]
        [InlineData("tank_walk")]
        [InlineData("tank_stand")]
        [InlineData("tank_prone")]
        [InlineData("tank_after")]
        [InlineData("tank_laser")]
        [InlineData("tank_siegepre")]
        [InlineData("tank_siegeattack")]
        [InlineData("tank_siegestand")]
        [InlineData("tank_siegeafter")]
        [InlineData("tank_msummon")]
        [InlineData("tank_rbooster_pre")]
        [InlineData("tank_rbooster_after")]
        [InlineData("tank_msummon2")]
        [InlineData("tank_mRush")]
        [InlineData("rbooster_pre")]
        [InlineData("rbooster_after")]
        [InlineData("gatlingshot")]
        [InlineData("gatlingshot2")]
        public void MechanicPostureAliases_FallbackToSit(string rawActionName)
        {
            CharacterPart morphPart = CreateMorphPart("sit", "alert");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, rawActionName).ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("sit", aliases[0]);
        }

        [Fact]
        public void Lasergun_PrefersShootFamilyBeforeStabFallback()
        {
            CharacterPart morphPart = CreateMorphPart("shoot2", "shoot1", "stabO1");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "lasergun").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("shoot2", aliases[0]);
            Assert.Contains("shoot1", aliases);
            Assert.Contains("stabO1", aliases);
        }

        [Fact]
        public void CannonJump_PrefersBodyRedirectedSwingBeforeJump()
        {
            CharacterPart morphPart = CreateMorphPart("jump", "swingOF");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "cannonJump").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("swingOF", aliases[0]);
            Assert.Contains("jump", aliases);
        }

        [Fact]
        public void CannonJump_FallsBackToJump_WhenSwingRedirectIsMissing()
        {
            CharacterPart morphPart = CreateMorphPart("jump", "stand");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "cannonJump").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("jump", aliases[0]);
        }

        [Fact]
        public void Fastest_PrefersRopeAndSwingRedirectsBeforeMovementFallback()
        {
            CharacterPart morphPart = CreateMorphPart("rope", "swingPF", "fly", "jump", "stand");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "fastest").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("rope", aliases[0]);
            Assert.True(aliases.IndexOf("swingPF") < aliases.IndexOf("fly"));
        }

        [Fact]
        public void Fastest_FallsBackToFly_WhenBodyRedirectsAreMissing()
        {
            CharacterPart morphPart = CreateMorphPart("fly", "jump", "stand");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "fastest").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("fly", aliases[0]);
        }

        [Fact]
        public void CombatStep_PrefersBodyRedirectedWalk2BeforeGenericMovementFallback()
        {
            CharacterPart morphPart = CreateMorphPart("walk2", "walk", "move", "stand");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "combatStep").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("walk2", aliases[0]);
            Assert.True(aliases.IndexOf("walk") > 0);
            Assert.True(aliases.IndexOf("move") > aliases.IndexOf("walk"));
        }

        [Fact]
        public void CombatStep_FallsBackToWalk_WhenWalk2IsMissing()
        {
            CharacterPart morphPart = CreateMorphPart("walk", "move", "stand");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "combatStep").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("walk", aliases[0]);
        }

        [Fact]
        public void CyclonePre_PrefersCheckedFallbackFamilyOrder()
        {
            CharacterPart morphPart = CreateMorphPart("alert", "stabO1", "swingO2", "swingTF");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "cyclone_pre").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("alert", aliases[0]);
            Assert.True(aliases.IndexOf("stabO1") > aliases.IndexOf("alert"));
            Assert.True(aliases.IndexOf("swingO2") > aliases.IndexOf("stabO1"));
            Assert.True(aliases.IndexOf("swingTF") > aliases.IndexOf("swingO2"));
        }

        [Fact]
        public void Cyclone_PrefersSwingO2BeforeSwingTF()
        {
            CharacterPart morphPart = CreateMorphPart("swingTF", "swingO2");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "cyclone").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("swingO2", aliases[0]);
            Assert.True(aliases.IndexOf("swingTF") > 0);
        }

        [Fact]
        public void CycloneAfter_KeepsAlertAsLateFallback()
        {
            CharacterPart morphPart = CreateMorphPart("alert", "swingTF", "swingO2");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "cyclone_after").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("swingO2", aliases[0]);
            Assert.True(aliases.IndexOf("swingTF") > aliases.IndexOf("swingO2"));
            Assert.True(aliases.IndexOf("alert") > aliases.IndexOf("swingTF"));
        }

        [Fact]
        public void TornadoDash_PrefersBodyRedirectedSwingBeforeMovementFallback()
        {
            CharacterPart morphPart = CreateMorphPart("fly", "jump", "stand", "swingO3");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "tornadoDash").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("swingO3", aliases[0]);
            Assert.Contains("fly", aliases);
            Assert.Contains("jump", aliases);
            Assert.Contains("stand", aliases);
        }

        [Fact]
        public void TornadoDashStop_PrefersBodyRedirectedSwingBeforeMovementFallback()
        {
            CharacterPart morphPart = CreateMorphPart("fly", "jump", "stand", "swingOF");

            List<string> aliases = MorphClientActionResolver.EnumerateClientActionAliases(morphPart, "tornadoDashStop").ToList();

            Assert.NotEmpty(aliases);
            Assert.Equal("swingOF", aliases[0]);
            Assert.Contains("fly", aliases);
            Assert.Contains("jump", aliases);
            Assert.Contains("stand", aliases);
        }

        private static CharacterPart CreateMorphPart(params string[] actionNames)
        {
            CharacterPart part = new()
            {
                Animations = new Dictionary<string, CharacterAnimation>(System.StringComparer.OrdinalIgnoreCase),
                AvailableAnimations = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            };

            foreach (string actionName in actionNames)
            {
                CharacterAnimation animation = new()
                {
                    ActionName = actionName
                };
                animation.Frames.Add(new CharacterFrame { Delay = 100 });
                part.Animations[actionName] = animation;
                part.AvailableAnimations.Add(actionName);
            }

            return part;
        }
    }
}
