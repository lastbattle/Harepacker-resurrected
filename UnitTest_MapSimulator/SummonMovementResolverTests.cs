using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public class SummonMovementResolverTests
    {
        [Theory]
        [InlineData(3111002, 0, SummonMovementStyle.Stationary, 200f)]
        [InlineData(4341006, 0, SummonMovementStyle.Stationary, -50f)]
        [InlineData(33101008, 0, SummonMovementStyle.Stationary, -30f)]
        [InlineData(5211001, 0, SummonMovementStyle.Stationary, 45f)]
        public void Resolve_UsesClientPlacementOverrides(int skillId, int moveAbility, SummonMovementStyle style, float spawnDistanceX)
        {
            SummonMovementProfile profile = SummonMovementResolver.Resolve(skillId, new[] { "stand" });

            Assert.Equal(moveAbility, profile.MoveAbility);
            Assert.Equal(style, profile.Style);
            Assert.Equal(spawnDistanceX, profile.SpawnDistanceX);
        }

        [Fact]
        public void Resolve_MoveBranchMapsToDriftAroundOwner()
        {
            SummonMovementProfile profile = SummonMovementResolver.Resolve(2121005, new[] { "summoned", "move", "stand" });

            Assert.Equal(2, profile.MoveAbility);
            Assert.Equal(SummonMovementStyle.DriftAroundOwner, profile.Style);
        }

        [Fact]
        public void Resolve_FlyAndStandMapsToHoverFollow()
        {
            SummonMovementProfile profile = SummonMovementResolver.Resolve(3111005, new[] { "summoned", "fly", "stand" });

            Assert.Equal(4, profile.MoveAbility);
            Assert.Equal(SummonMovementStyle.HoverFollow, profile.Style);
        }

        [Fact]
        public void Resolve_FlyOnlyMapsToAnchorHover()
        {
            SummonMovementProfile profile = SummonMovementResolver.Resolve(35111001, new[] { "summoned", "fly" });

            Assert.Equal(5, profile.MoveAbility);
            Assert.Equal(SummonMovementStyle.HoverAroundAnchor, profile.Style);
        }

        [Fact]
        public void Resolve_StandOnlyMapsToGroundFollow()
        {
            SummonMovementProfile profile = SummonMovementResolver.Resolve(2311006, new[] { "summoned", "stand" });

            Assert.Equal(1, profile.MoveAbility);
            Assert.Equal(SummonMovementStyle.GroundFollow, profile.Style);
        }

        [Fact]
        public void ResolveSpawnPosition_UsesFacingAwareDistances()
        {
            Vector2 playerPosition = new(100f, 200f);

            Vector2 facingRight = SummonMovementResolver.ResolveSpawnPosition(
                SummonMovementStyle.Stationary,
                200f,
                playerPosition,
                facingRight: true);
            Vector2 facingLeft = SummonMovementResolver.ResolveSpawnPosition(
                SummonMovementStyle.Stationary,
                200f,
                playerPosition,
                facingRight: false);

            Assert.Equal(new Vector2(300f, 175f), facingRight);
            Assert.Equal(new Vector2(-100f, 175f), facingLeft);
        }
    }
}
