using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public class BulletAnimationOwnerParityTests
{
    [Fact]
    public void ResolveBulletAnimationOwnerCurrentPosition_UsesRegisteredTimelineForNormalOwner()
    {
        var presentation = new BulletAnimationPresentation
        {
            Kind = BulletAnimationOwnerKind.Normal,
            StartTime = 1000,
            EndTime = 1100,
            SourcePoint = new Vector2(10f, 20f),
            DestinationPoint = new Vector2(110f, 20f)
        };
        var projectile = new ActiveProjectile
        {
            X = 999f,
            Y = 999f,
            Data = new ProjectileData()
        };

        Vector2 resolved = SkillManager.ResolveBulletAnimationOwnerCurrentPosition(
            projectile,
            presentation,
            projectilePosition: new Vector2(projectile.X, projectile.Y),
            currentTime: 1050);

        Assert.Equal(new Vector2(60f, 20f), resolved);
    }

    [Fact]
    public void ShouldSyncBulletAnimationOwnerFromProjectile_StaysAttachedWhenTimelineAnchorsAreValid()
    {
        var owner = new ActiveBulletAnimationOwner
        {
            Presentation = new BulletAnimationPresentation
            {
                Kind = BulletAnimationOwnerKind.Normal,
                StartTime = 0,
                EndTime = 100,
                SourcePoint = new Vector2(0f, 0f),
                DestinationPoint = new Vector2(100f, 0f)
            }
        };
        var projectile = new ActiveProjectile
        {
            X = float.NaN,
            Y = float.NaN,
            PreviousX = float.NaN,
            PreviousY = float.NaN
        };

        bool shouldSync = SkillManager.ShouldSyncBulletAnimationOwnerFromProjectile(owner, projectile);

        Assert.True(shouldSync);
    }

    [Fact]
    public void ResolveBulletAnimationOwnerStopPosition_UsesRegisteredTimelineWhenProjectileIsMissing()
    {
        var owner = new ActiveBulletAnimationOwner
        {
            PreviousPosition = new Vector2(4f, 4f),
            Presentation = new BulletAnimationPresentation
            {
                Kind = BulletAnimationOwnerKind.Normal,
                StartTime = 0,
                EndTime = 100,
                SourcePoint = new Vector2(10f, 0f),
                DestinationPoint = new Vector2(110f, 0f)
            }
        };

        Vector2 stop = SkillManager.ResolveBulletAnimationOwnerStopPosition(owner, projectile: null, currentTime: 50);

        Assert.Equal(new Vector2(60f, 0f), stop);
    }

    [Fact]
    public void ResolveBulletAnimationOwnerCurrentPosition_FallsBackToLiveProjectileWhenTimelineAnchorsAreInvalid()
    {
        var presentation = new BulletAnimationPresentation
        {
            Kind = BulletAnimationOwnerKind.Normal,
            StartTime = 0,
            EndTime = 100,
            SourcePoint = new Vector2(float.NaN, 0f),
            DestinationPoint = new Vector2(10f, 0f)
        };
        var projectile = new ActiveProjectile
        {
            X = 33f,
            Y = 44f
        };

        Vector2 resolved = SkillManager.ResolveBulletAnimationOwnerCurrentPosition(
            projectile,
            presentation,
            projectilePosition: Vector2.Zero,
            currentTime: 50);

        Assert.Equal(new Vector2(33f, 44f), resolved);
    }

    [Fact]
    public void ResolveBulletAnimationOwnerStopPosition_FallsBackToPreviousPositionWhenTimelineAnchorsAreInvalid()
    {
        var owner = new ActiveBulletAnimationOwner
        {
            PreviousPosition = new Vector2(7f, 8f),
            Presentation = new BulletAnimationPresentation
            {
                Kind = BulletAnimationOwnerKind.Normal,
                StartTime = 0,
                EndTime = 100,
                SourcePoint = new Vector2(float.NaN, 0f),
                DestinationPoint = new Vector2(10f, 0f)
            }
        };

        Vector2 stop = SkillManager.ResolveBulletAnimationOwnerStopPosition(owner, projectile: null, currentTime: 50);

        Assert.Equal(new Vector2(7f, 8f), stop);
    }
}
