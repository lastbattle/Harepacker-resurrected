using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Moq;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class MobAngerGaugeBurstParityTests
    {
        [Fact]
        public void StringPoolOwnerIds_ResolveAuthoredMobBurstPath()
        {
            Assert.Equal("Mob/%07d.img", MapleStoryStringPool.GetOrNull(0x03CE));
            Assert.Equal("AngerGaugeEffect", MapleStoryStringPool.GetOrNull(0x0C2F));
            Assert.Equal("Mob/9400633.img/AngerGaugeEffect", MobAngerGaugeBurstStringPoolText.ResolvePath(9400633));
        }

        [Fact]
        public void ResolveRepeatIntervalMs_PrefersSpecialAttackAfterWhenPublished()
        {
            var attack = new MobAttackEntry
            {
                IsSpecialAttack = true,
                AttackAfter = 3300
            };

            int resolved = MobAngerGaugeBurstParity.ResolveRepeatIntervalMs(
                BuildFrames(150, 150, 150, 150, 150, 150, 150, 150),
                attack,
                configuredSpecialAttackAfterMs: 1200);

            Assert.Equal(3300, resolved);
        }

        [Fact]
        public void ResolveRepeatIntervalMs_FallsBackToVisualDurationWhenNoOwnerTiming()
        {
            int resolved = MobAngerGaugeBurstParity.ResolveRepeatIntervalMs(
                BuildFrames(150, 150, 150, 150, 150, 150, 150, 150),
                currentAttack: null,
                configuredSpecialAttackAfterMs: 0);

            Assert.Equal(1200, resolved);
        }

        [Fact]
        public void ResolveRepeatIntervalMs_UsesMinimumDelayForMissingFrameDelay()
        {
            int resolved = MobAngerGaugeBurstParity.ResolveRepeatIntervalMs(
                BuildFrames(0, -1, 5),
                currentAttack: null,
                configuredSpecialAttackAfterMs: 0);

            Assert.Equal(25, resolved);
        }

        [Fact]
        public void ShouldRegisterBurst_GatesFallbackCadenceByChargeAndNextAllowedTick()
        {
            Assert.False(MobAngerGaugeBurstParity.ShouldRegisterBurst(3, 3, -1, int.MinValue, 1000));
            Assert.True(MobAngerGaugeBurstParity.ShouldRegisterBurst(3, 3, 2, int.MinValue, 1000));
            Assert.False(MobAngerGaugeBurstParity.ShouldRegisterBurst(3, 3, 3, 2000, 1999));
            Assert.True(MobAngerGaugeBurstParity.ShouldRegisterBurst(3, 3, 3, 2000, 2000));
        }

        [Fact]
        public void ShouldTriggerAngerGaugeFullChargeEffect_RequiresPublishedOwnerTiming()
        {
            var ai = CreateAngerGaugeAi();
            ai.AddAttack(new MobAttackEntry
            {
                IsSpecialAttack = true,
                AttackAfter = 0,
                Delay = 100,
                Range = 100
            });

            ai.StartAttack(0, 1000);

            Assert.False(ai.ShouldTriggerAngerGaugeFullChargeEffect(1100));
            Assert.True(ai.ShouldUseFallbackAngerGaugeFullChargeCadence());
            Assert.Equal(0, ai.AngerGaugeFullChargeEffectIntervalMs);
        }

        [Fact]
        public void ShouldTriggerAngerGaugeFullChargeEffect_UsesOwnerCooldownGate()
        {
            var ai = CreateAngerGaugeAi();
            ai.AddAttack(new MobAttackEntry
            {
                IsSpecialAttack = true,
                AttackAfter = 3300,
                Range = 100
            });

            ai.StartAttack(0, 1000);

            Assert.False(ai.ShouldTriggerAngerGaugeFullChargeEffect(4299));
            Assert.True(ai.ShouldTriggerAngerGaugeFullChargeEffect(4300));
            Assert.False(ai.ShouldTriggerAngerGaugeFullChargeEffect(7599));
            Assert.True(ai.ShouldTriggerAngerGaugeFullChargeEffect(7600));
        }

        [Fact]
        public void MixedOwnerMobs_ClearStaleIntervalAndAllowFallbackOnlyDuringUntimedSpecial()
        {
            var ai = CreateAngerGaugeAi();
            ai.AddAttack(new MobAttackEntry
            {
                IsSpecialAttack = true,
                AttackAfter = 3300,
                Range = 100
            });
            ai.AddAttack(new MobAttackEntry
            {
                IsSpecialAttack = true,
                AttackAfter = 0,
                Delay = 100,
                Range = 100
            });
            ai.AddAttack(new MobAttackEntry
            {
                IsSpecialAttack = false,
                Range = 100
            });

            ai.StartAttack(0, 1000);
            Assert.Equal(3300, ai.AngerGaugeFullChargeEffectIntervalMs);

            ai.StartAttack(2, 2000);
            Assert.False(ai.ShouldUseFallbackAngerGaugeFullChargeCadence());

            ai.StartAttack(1, 3000);
            Assert.Equal(0, ai.AngerGaugeFullChargeEffectIntervalMs);
            Assert.True(ai.ShouldUseFallbackAngerGaugeFullChargeCadence());
        }

        [Fact]
        public void OwnerTimingFlag_TracksLiveSpecialAttackMetadata()
        {
            var ai = CreateAngerGaugeAi();
            var timedSpecialAttack = new MobAttackEntry
            {
                IsSpecialAttack = true,
                AttackAfter = 3300,
                Delay = 100,
                Range = 100
            };

            ai.AddAttack(timedSpecialAttack);
            Assert.True(ai.HasSpecialAttackFullChargeEffectOwnerTiming);

            timedSpecialAttack.AttackAfter = 0;
            ai.StartAttack(0, 1000);

            Assert.False(ai.HasSpecialAttackFullChargeEffectOwnerTiming);
            Assert.False(ai.ShouldTriggerAngerGaugeFullChargeEffect(1100));
            Assert.True(ai.ShouldUseFallbackAngerGaugeFullChargeCadence());
        }

        [Fact]
        public void AddFullChargedAngerGauge_RegistersOwnerMetadataAndNoFlipLoadLayerLane()
        {
            var effects = new AnimationEffects();
            List<IDXObject> frames = BuildFrames(150, 150);
            const string sourceUol = "Mob/9400633.img/AngerGaugeEffect";

            effects.AddFullChargedAngerGauge(
                frames,
                sourceUol,
                getOrigin: () => new Vector2(10f, 20f),
                fallbackX: 10f,
                fallbackY: 20f,
                currentTimeMs: 1000,
                zOrder: 1);

            OneTimeAnimation animation = Assert.Single(effects.OneTimeAnimations);
            Assert.Equal(AnimationOneTimeOwner.FullChargedAngerGauge, animation.Owner);
            Assert.Equal(AnimationOneTimePlaybackMode.GA_STOP, animation.PlaybackMode);
            Assert.True(animation.UsesOverlayParent);
            Assert.Equal(AnimationOneTimeOverlayParentKind.MobActionLayer, animation.OverlayParentKind);
            Assert.Equal(sourceUol, animation.SourceUol);
            Assert.False(animation.ResolveDrawFlipForTesting());

            Assert.True(animation.RecoveredRegistrationTrace.HasValue);
            OneTimeAnimationRecoveredRegistrationTrace trace = animation.RecoveredRegistrationTrace.Value;
            Assert.False(trace.LoadLayerFlip);
            Assert.Equal(0, trace.LoadLayerCanvasValue);
            Assert.Equal(unchecked((int)0xC00614A4), trace.LoadLayerOptionValue);
            Assert.Equal(255, trace.LoadLayerAlphaValue);
            Assert.Equal(0, trace.LoadLayerReservedValue);
            Assert.Equal(0, trace.RegisterOneTimeAnimationDelayMs);
            Assert.False(trace.RegisterOneTimeAnimationUsesFlipOrigin);

            OneTimeAnimationRecoveredNativeOperationKind[] operationKinds = animation
                .RecoveredNativeExecutionTrace
                .Select(operation => operation.Kind)
                .ToArray();

            Assert.Equal(
                new[]
                {
                    OneTimeAnimationRecoveredNativeOperationKind.RetainOverlayParent,
                    OneTimeAnimationRecoveredNativeOperationKind.RetainOriginVector,
                    OneTimeAnimationRecoveredNativeOperationKind.LoadLayer,
                    OneTimeAnimationRecoveredNativeOperationKind.Animate,
                    OneTimeAnimationRecoveredNativeOperationKind.RetainLoadedLayerForRegistration,
                    OneTimeAnimationRecoveredNativeOperationKind.RegisterOneTimeAnimation,
                    OneTimeAnimationRecoveredNativeOperationKind.ReleaseLoadedLayer,
                    OneTimeAnimationRecoveredNativeOperationKind.ReleaseSourceUol,
                    OneTimeAnimationRecoveredNativeOperationKind.ReleaseOriginVector,
                    OneTimeAnimationRecoveredNativeOperationKind.ReleaseOverlayParent
                },
                operationKinds);
        }

        private static MobAI CreateAngerGaugeAi()
        {
            var ai = new MobAI();
            ai.Initialize(maxHp: 1000, level: 1, exp: 0, isBoss: true);
            ai.ConfigureAngerGauge(hasAngerGauge: true, chargeTarget: 3);
            return ai;
        }

        private static List<IDXObject> BuildFrames(params int[] delays)
        {
            var frames = new List<IDXObject>(delays.Length);
            foreach (int delay in delays)
            {
                var frame = new Mock<IDXObject>();
                frame.SetupGet(entry => entry.Delay).Returns(delay);
                frame.SetupGet(entry => entry.X).Returns(0);
                frame.SetupGet(entry => entry.Y).Returns(0);
                frame.SetupGet(entry => entry.Width).Returns(1);
                frame.SetupGet(entry => entry.Height).Returns(1);
                frame.SetupProperty(entry => entry.Tag);
                frame.SetupGet(entry => entry.Texture).Returns((Microsoft.Xna.Framework.Graphics.Texture2D)null);
                frames.Add(frame.Object);
            }

            return frames;
        }
    }
}
