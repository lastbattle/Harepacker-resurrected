using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class BulletAnimationOwnerParityTests
{
    [Fact]
    public void ResolveBulletAnimationPresentationDurationMs_AuthoredDuration_ClampsNormalAndQueued()
    {
        var authoredAnimation = new SkillAnimation
        {
            Frames = new List<SkillFrame>
            {
                new() { Delay = 80 },
                new() { Delay = 80 }
            }
        };

        var normalProjectile = new ActiveProjectile
        {
            Data = new ProjectileData { LifeTime = 1000f }
        };
        var queuedProjectile = new ActiveProjectile
        {
            Data = new ProjectileData { LifeTime = 1000f },
            IsQueuedFinalAttack = true
        };

        int normalDuration = SkillManager.ResolveBulletAnimationPresentationDurationMs(
            normalProjectile,
            new Vector2(0f, 0f),
            new Vector2(300f, 0f),
            authoredAnimation,
            effectAnimation: null);
        int queuedDuration = SkillManager.ResolveBulletAnimationPresentationDurationMs(
            queuedProjectile,
            new Vector2(0f, 0f),
            new Vector2(300f, 0f),
            authoredAnimation,
            effectAnimation: null);

        Assert.Equal(160, normalDuration);
        Assert.Equal(160, queuedDuration);
    }

    [Fact]
    public void ResolveBulletAnimationPresentationDurationMs_NoAuthoredDuration_UsesDistanceLifetimeFallbacks()
    {
        var normalProjectile = new ActiveProjectile
        {
            Data = new ProjectileData { LifeTime = 100f }
        };
        var queuedProjectile = new ActiveProjectile
        {
            Data = new ProjectileData { LifeTime = 100f },
            IsQueuedSparkAttack = true
        };

        int normalDuration = SkillManager.ResolveBulletAnimationPresentationDurationMs(
            normalProjectile,
            new Vector2(0f, 0f),
            new Vector2(300f, 0f));
        int queuedDuration = SkillManager.ResolveBulletAnimationPresentationDurationMs(
            queuedProjectile,
            new Vector2(0f, 0f),
            new Vector2(300f, 0f));

        Assert.Equal(100, normalDuration);
        Assert.Equal(450, queuedDuration);
    }

    [Fact]
    public void ResolveBulletAnimationPresentationDurationMs_InvalidInput_FallsBackToOneTick()
    {
        var projectile = new ActiveProjectile
        {
            Data = null
        };

        int duration = SkillManager.ResolveBulletAnimationPresentationDurationMs(
            projectile,
            new Vector2(float.NaN, 0f),
            new Vector2(10f, 0f));

        Assert.Equal(1, duration);
    }

    [Fact]
    public void ShouldSyncBulletAnimationOwnerFromProjectile_DetachedOwner_DoesNotResync()
    {
        var owner = new ActiveBulletAnimationOwner
        {
            IsDetachedFromProjectile = true,
            Presentation = new BulletAnimationPresentation
            {
                StartTime = 0,
                EndTime = 100
            }
        };
        var projectile = new ActiveProjectile
        {
            X = 10f,
            Y = 10f
        };

        bool shouldSync = SkillManager.ShouldSyncBulletAnimationOwnerFromProjectile(owner, projectile);

        Assert.False(shouldSync);
    }

    [Fact]
    public void ResolveBulletAfterimageParentRepeatLayerObjectId_UsesMainLayerOwnerIdentity()
    {
        var owner = new ActiveBulletAnimationOwner
        {
            MainLayerObjectId = 77
        };
        var previousLayer = new ProjectileAfterimageLayer
        {
            RepeatLayerObjectId = 999
        };

        int parentId = SkillManager.ResolveBulletAfterimageParentRepeatLayerObjectId(owner, previousLayer);

        Assert.Equal(77, parentId);
    }

    [Fact]
    public void ResolveBulletAfterimageRepeatLayerObjectId_ComposesOwnerScopedIdentity()
    {
        var owner = new ActiveBulletAnimationOwner
        {
            MainLayerObjectId = 7
        };

        int firstId = SkillManager.ResolveBulletAfterimageRepeatLayerObjectId(owner);
        int secondId = SkillManager.ResolveBulletAfterimageRepeatLayerObjectId(owner);

        Assert.Equal(70001, firstId);
        Assert.Equal(70002, secondId);
    }
}
