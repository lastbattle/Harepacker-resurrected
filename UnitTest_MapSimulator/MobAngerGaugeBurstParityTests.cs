using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render.DX;
using Moq;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class MobAngerGaugeBurstParityTests
{
    [Fact]
    public void ShouldTriggerAngerGaugeFullChargeEffect_UsesOwnerReplayGateFromLastRegistration()
    {
        var ai = CreateAngerGaugeAi();
        ai.AddAttack(new MobAttackEntry
        {
            AttackId = 4,
            AnimationName = "attack4",
            IsSpecialAttack = true,
            AttackAfter = 3300,
            Delay = 1,
            Range = 500
        });

        ai.StartAttack(0, 1_000);

        Assert.True(ai.ShouldTriggerAngerGaugeFullChargeEffect(1_001));

        ai.RecordAngerGaugeFullChargeEffectRegistration(1_001);

        Assert.False(ai.ShouldTriggerAngerGaugeFullChargeEffect(4_300));
        Assert.True(ai.ShouldTriggerAngerGaugeFullChargeEffect(4_301));
    }

    [Fact]
    public void StartAttack_ClearsStaleTimedOwnerInterval_WhenLaterSpecialAttackOmitsAttackAfter()
    {
        var ai = CreateAngerGaugeAi();
        ai.AddAttack(new MobAttackEntry
        {
            AttackId = 4,
            AnimationName = "attack4",
            IsSpecialAttack = true,
            AttackAfter = 3300,
            Delay = 1,
            Range = 500
        });
        ai.AddAttack(new MobAttackEntry
        {
            AttackId = 5,
            AnimationName = "attack5",
            IsSpecialAttack = true,
            AttackAfter = 0,
            Delay = 1,
            Range = 500
        });

        ai.StartAttack(0, 1_000);
        Assert.Equal(3300, ai.AngerGaugeFullChargeEffectIntervalMs);
        Assert.False(ai.ShouldUseFallbackAngerGaugeFullChargeCadence());

        ai.StartAttack(1, 2_000);

        Assert.Equal(0, ai.AngerGaugeFullChargeEffectIntervalMs);
        Assert.True(ai.ShouldUseFallbackAngerGaugeFullChargeCadence());
        Assert.False(ai.ShouldTriggerAngerGaugeFullChargeEffect(2_001));
    }

    [Fact]
    public void ShouldUseFallbackCadence_OutsideSpecialAttack_KeysOffLiveRuntimeOwnerTiming()
    {
        var ai = CreateAngerGaugeAi();
        ai.AddAttack(new MobAttackEntry
        {
            AttackId = 4,
            AnimationName = "attack4",
            IsSpecialAttack = true,
            AttackAfter = 3300,
            Delay = 1,
            Range = 500
        });
        ai.AddAttack(new MobAttackEntry
        {
            AttackId = 1,
            AnimationName = "attack1",
            IsSpecialAttack = false,
            AttackAfter = 0,
            Delay = 1,
            Range = 500
        });
        ai.AddAttack(new MobAttackEntry
        {
            AttackId = 5,
            AnimationName = "attack5",
            IsSpecialAttack = true,
            AttackAfter = 0,
            Delay = 1,
            Range = 500
        });

        ai.StartAttack(0, 1_000);
        ai.StartAttack(1, 1_500);

        Assert.False(ai.ShouldUseFallbackAngerGaugeFullChargeCadence());

        ai.StartAttack(2, 2_000);
        ai.StartAttack(1, 2_500);

        Assert.True(ai.ShouldUseFallbackAngerGaugeFullChargeCadence());
    }

    [Fact]
    public void RecordRegistration_RefreshesOwnerStartTimeLane_ForReplayGate()
    {
        var ai = CreateAngerGaugeAi();
        ai.AddAttack(new MobAttackEntry
        {
            AttackId = 4,
            AnimationName = "attack4",
            IsSpecialAttack = true,
            AttackAfter = 3300,
            Delay = 1,
            Range = 500
        });

        ai.StartAttack(0, 1_000);
        Assert.True(ai.ShouldTriggerAngerGaugeFullChargeEffect(1_001));
        ai.RecordAngerGaugeFullChargeEffectRegistration(1_001);

        ai.RecordAngerGaugeFullChargeEffectRegistration(2_500);
        Assert.False(ai.ShouldTriggerAngerGaugeFullChargeEffect(5_799));
        Assert.True(ai.ShouldTriggerAngerGaugeFullChargeEffect(5_800));
    }

    [Fact]
    public void ResolveRepeatIntervalMs_UsesOwnerVsFallbackCadenceSplit()
    {
        List<IDXObject> frames = CreateFrames(150, 150, 150, 150, 150, 150, 150, 150);

        int ownerTimedInterval = MobAngerGaugeBurstParity.ResolveRepeatIntervalMs(
            frames,
            new MobAttackEntry { IsSpecialAttack = true, AttackAfter = 3300 },
            configuredSpecialAttackAfterMs: 9999);

        int ownerUntimedInterval = MobAngerGaugeBurstParity.ResolveRepeatIntervalMs(
            frames,
            new MobAttackEntry { IsSpecialAttack = true, AttackAfter = 0 },
            configuredSpecialAttackAfterMs: 3300);

        int fallbackInterval = MobAngerGaugeBurstParity.ResolveRepeatIntervalMs(
            frames,
            new MobAttackEntry { IsSpecialAttack = false, AttackAfter = 3300 },
            configuredSpecialAttackAfterMs: 3300);

        Assert.Equal(3300, ownerTimedInterval);
        Assert.Equal(3300, ownerUntimedInterval);
        Assert.Equal(1200, fallbackInterval);
    }

    [Fact]
    public void FullChargedAngerGauge_RegistrationTrace_KeepsRecoveredConstants()
    {
        const string sourceUol = "Mob/9400633.img/AngerGaugeEffect";
        OneTimeAnimationRecoveredRegistrationTrace trace =
            OneTimeAnimationRecoveredRegistrationTrace.CreateFullChargedAngerGauge(sourceUol);

        Assert.Equal(MapleStoryStringPool.MobAngerGaugeBurstTemplatePathStringPoolId, trace.MobTemplatePathStringPoolId);
        Assert.Equal(MapleStoryStringPool.MobAngerGaugeBurstEffectNameStringPoolId, trace.EffectNameStringPoolId);
        Assert.Equal(0, trace.LoadLayerCanvasValue);
        Assert.Equal(unchecked((int)0xC00614A4), trace.LoadLayerOptionValue);
        Assert.Equal(255, trace.LoadLayerAlphaValue);
        Assert.Equal(0, trace.LoadLayerReservedValue);
        Assert.False(trace.LoadLayerFlip);
        Assert.Equal(AnimationOneTimePlaybackMode.GA_STOP, trace.AnimatePlaybackMode);
        Assert.True(trace.RegistersOneTimeAnimation);
        Assert.Equal(0, trace.RegisterOneTimeAnimationDelayMs);

        var animation = new OneTimeAnimation();
        animation.Initialize(
            CreateFrames(150),
            x: 0,
            y: 0,
            flip: trace.LoadLayerFlip,
            currentTimeMs: 0,
            zOrder: 1,
            getPosition: null,
            getFlip: null,
            owner: AnimationOneTimeOwner.FullChargedAngerGauge,
            playbackMode: trace.AnimatePlaybackMode,
            sourceUol: sourceUol,
            usesOverlayParent: true,
            recoveredRegistrationTrace: trace);

        OneTimeAnimationRecoveredNativeOperation loadLayer = animation.RecoveredNativeExecutionTrace
            .Single(op => op.Kind == OneTimeAnimationRecoveredNativeOperationKind.LoadLayer);
        OneTimeAnimationRecoveredNativeOperation animate = animation.RecoveredNativeExecutionTrace
            .Single(op => op.Kind == OneTimeAnimationRecoveredNativeOperationKind.Animate);
        OneTimeAnimationRecoveredNativeOperation register = animation.RecoveredNativeExecutionTrace
            .Single(op => op.Kind == OneTimeAnimationRecoveredNativeOperationKind.RegisterOneTimeAnimation);

        Assert.Equal(unchecked((int)0xC00614A4), loadLayer.Value);
        Assert.Equal(0, loadLayer.LoadLayerCanvasValue);
        Assert.Equal(255, loadLayer.LoadLayerAlphaValue);
        Assert.False(loadLayer.LoadLayerFlip);
        Assert.Equal(0, loadLayer.LoadLayerReservedValue);

        Assert.Equal(AnimationOneTimePlaybackMode.GA_STOP, animate.PlaybackMode);
        Assert.Equal((int)AnimationOneTimePlaybackMode.GA_STOP, animate.Value);
        Assert.True(animate.AnimateUsesMissingStartTime);
        Assert.True(animate.AnimateUsesMissingRepeatCount);

        Assert.Equal(0, register.Value);
        Assert.False(register.RegisterOneTimeAnimationHasCallback);
    }

    [Fact]
    public void MapleStoryStringPool_MobAngerGaugeBurstIdsAndFormattedPath_RemainPinned()
    {
        Assert.Equal(0x03CE, MapleStoryStringPool.MobAngerGaugeBurstTemplatePathStringPoolId);
        Assert.Equal(0x0C2F, MapleStoryStringPool.MobAngerGaugeBurstEffectNameStringPoolId);

        Assert.True(MapleStoryStringPool.TryGet(MapleStoryStringPool.MobAngerGaugeBurstTemplatePathStringPoolId, out string templatePath));
        Assert.True(MapleStoryStringPool.TryGet(MapleStoryStringPool.MobAngerGaugeBurstEffectNameStringPoolId, out string effectName));
        Assert.Equal("Mob/%07d.img", templatePath);
        Assert.Equal("AngerGaugeEffect", effectName);

        Assert.Equal(
            "Mob/9400633.img/AngerGaugeEffect",
            MapleStoryStringPool.ResolveMobAngerGaugeBurstPath(9400633));
        Assert.Equal(
            "Mob/9400633.img/AngerGaugeEffect",
            MapleStoryStringPool.ResolveMobAngerGaugeBurstPath("9400633"));
    }

    private static MobAI CreateAngerGaugeAi()
    {
        var ai = new MobAI();
        ai.Initialize(maxHp: 100, isBoss: true);
        ai.ConfigureAngerGauge(hasAngerGauge: true, chargeTarget: 3);
        return ai;
    }

    private static List<IDXObject> CreateFrames(params int[] delays)
    {
        var frames = new List<IDXObject>(delays.Length);
        foreach (int delay in delays)
        {
            var frame = new Mock<IDXObject>(MockBehavior.Strict);
            frame.SetupGet(f => f.Delay).Returns(delay);
            frames.Add(frame.Object);
        }

        return frames;
    }
}
