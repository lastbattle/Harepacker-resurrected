using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class RemoteGrenadeParityTests
{
    [Fact]
    public void BuildRemoteThrowGrenadePresentationForParity_MonsterBomb_PublishesClientLimitTime()
    {
        RemoteUserActorPool.RemoteGrenadePresentation presentation =
            RemoteUserActorPool.BuildRemoteThrowGrenadePresentationForParity(
                characterId: 1001,
                skillId: 4341003,
                skillLevel: 1,
                ownerLevel: 120,
                target: new Point(10, 20),
                keyDownTime: 777,
                facingRight: true,
                currentTime: 5000,
                maxGaugeTimeMs: 3000);

        Assert.Equal(450, presentation.InitDelayMs);
        Assert.Equal(950, presentation.ExplosionDelayMs);
        Assert.Equal(950, presentation.LimitTimeMs);
    }

    [Fact]
    public void ResolveRemoteGrenadeExpireTimeForParity_MonsterBomb_UsesClientLimitTime()
    {
        var ballAnimation = BuildAnimation(240);
        var explosionAnimation = BuildAnimation(600);
        var presentation = new RemoteUserActorPool.RemoteGrenadePresentation(
            CharacterId: 1001,
            SkillId: 4341003,
            SkillLevel: 1,
            OwnerLevel: 120,
            Target: new Point(10, 20),
            KeyDownTime: 1200,
            Impact: new Vector2(300, 200),
            FacingRight: true,
            CurrentTime: 9000,
            GravityFree: true,
            UsesMonsterBombGauge: true,
            InitDelayMs: 450,
            ExplosionDelayMs: 950,
            DragX: 40000,
            DragY: 60000,
            LimitTimeMs: 950);

        int expireTime = RemoteUserActorPool.ResolveRemoteGrenadeExpireTimeForParity(
            presentation,
            ballAnimation,
            explosionAnimation);

        Assert.Equal(9950, expireTime);
    }

    [Fact]
    public void ResolveRemoteGrenadeExpireTimeForParity_GenericGrenade_PreservesExplosionTailLifetime()
    {
        var ballAnimation = BuildAnimation(300);
        var explosionAnimation = BuildAnimation(450);
        var presentation = new RemoteUserActorPool.RemoteGrenadePresentation(
            CharacterId: 1001,
            SkillId: 5201002,
            SkillLevel: 1,
            OwnerLevel: 120,
            Target: new Point(10, 20),
            KeyDownTime: 400,
            Impact: new Vector2(240, -240),
            FacingRight: true,
            CurrentTime: 2000,
            GravityFree: false,
            UsesMonsterBombGauge: false,
            InitDelayMs: 0,
            ExplosionDelayMs: 0,
            DragX: 0,
            DragY: 0,
            LimitTimeMs: 0);

        int expireTime = RemoteUserActorPool.ResolveRemoteGrenadeExpireTimeForParity(
            presentation,
            ballAnimation,
            explosionAnimation);

        Assert.Equal(2750, expireTime);
    }

    private static SkillAnimation BuildAnimation(params int[] frameDelays)
    {
        var animation = new SkillAnimation();
        animation.Frames = new List<SkillFrame>();
        foreach (int delay in frameDelays)
        {
            animation.Frames.Add(new SkillFrame { Delay = delay });
        }

        return animation;
    }
}
