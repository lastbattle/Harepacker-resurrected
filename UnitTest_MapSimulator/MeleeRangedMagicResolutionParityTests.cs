using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public class MeleeRangedMagicResolutionParityTests
{
    [Theory]
    [InlineData(3001005, 300, 0, -28)]
    [InlineData(33101007, 3310, 0, -40)]
    [InlineData(33121005, 3312, 0, -17)]
    [InlineData(35111015, 3511, 0, -18)]
    [InlineData(35111004, 3511, 1932016, -45)]
    [InlineData(0, 3511, 1932016, -45)]
    public void FallbackShootAttackPointYOffsetMatchesClientSkillAndVehicleNudges(
        int skillId,
        int jobId,
        int mountedTamingMobItemId,
        int expectedOffsetY)
    {
        int offsetY = ClientShootAttackFamilyResolver.ResolveFallbackShootAttackPointYOffset(
            skillId,
            jobId,
            mountedTamingMobItemId);

        Assert.Equal(expectedOffsetY, offsetY);
    }

    [Fact]
    public void ProjectileSpawnYKeepsAuthoredShootPointUnchanged()
    {
        float spawnY = SkillManager.ResolveProjectileSpawnY(
            attackOriginY: 200f,
            usesAuthoredShootPoint: true,
            fallbackShootPointYOffset: -28f);

        Assert.Equal(200f, spawnY);
    }

    [Fact]
    public void QueuedProjectileFireTimeRefreshRebasesLegacyFallbackOffsetToClientFallbackOffset()
    {
        var projectile = new ActiveProjectile
        {
            OwnerX = 100f,
            OwnerY = 100f,
            X = 105f,
            Y = 83f,
            PreviousX = 105f,
            PreviousY = 83f,
            UsesAuthoredShootPoint = false,
            FallbackShootPointYOffset = -20f
        };

        SkillManager.ApplyQueuedProjectileFireTimeOrigin(
            projectile,
            new Vector2(200f, 200f),
            usesAuthoredShootPoint: false,
            fallbackShootPointYOffset: -28f);

        Assert.Equal(200f, projectile.OwnerX);
        Assert.Equal(200f, projectile.OwnerY);
        Assert.Equal(205f, projectile.X);
        Assert.Equal(175f, projectile.Y);
        Assert.Equal(projectile.X, projectile.PreviousX);
        Assert.Equal(projectile.Y, projectile.PreviousY);
        Assert.Equal(-28f, projectile.FallbackShootPointYOffset);
    }
}
