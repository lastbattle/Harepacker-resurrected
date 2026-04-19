using System;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Loaders;
using Microsoft.Xna.Framework;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class CombatFeedbackAnimationOwnerParityTests
    {
        [Theory]
        [InlineData(DamageColorType.Red, "Miss", "Effect/BasicEff.img/NoRed0/Miss")]
        [InlineData(DamageColorType.Blue, "guard", "Effect/BasicEff.img/NoRed0/guard")]
        [InlineData(DamageColorType.Violet, "shot", "Effect/BasicEff.img/NoRed0/shot")]
        public void ResolveCombatFeedbackEffectUol_UsesNoRedOwnerLaneForSupportedFamilies(
            DamageColorType colorType,
            string specialTextName,
            string expectedUol)
        {
            string resolved = MapSimulator.ResolveAnimationDisplayerCombatFeedbackEffectUol(specialTextName, colorType);
            Assert.Equal(expectedUol, resolved);
        }

        [Fact]
        public void ResolveCombatFeedbackEffectUol_RejectsUnsupportedColorFamily()
        {
            string resolved = MapSimulator.ResolveAnimationDisplayerCombatFeedbackEffectUol("Miss", (DamageColorType)7);
            Assert.Null(resolved);
        }

        [Theory]
        [InlineData("Miss", "Miss")]
        [InlineData("guard", "guard")]
        [InlineData("shot", "shot")]
        [InlineData("counter", "counter")]
        [InlineData("resist", "resist")]
        [InlineData("unknown", "Miss")]
        public void ResolveSpecialTextName_NormalizesCanonicalOwnerNames(string input, string expected)
        {
            Assert.Equal(expected, DamageNumberRenderer.ResolveSpecialTextName(input));
        }

        [Fact]
        public void BuildOneTimeLayerRegistration_DoesNotEmitOverlayWhenCompositionTraceDoesNotOwnOverlayLayer()
        {
            DamageNumberRenderer.PreparedSpriteDrawInfo overlaySprite = new("effect", 11, -19);
            DamageNumberRenderer.PreparedDamageNumberCompositionTrace compositionTrace = new(
                new CanvasLayerRecoveredCanvasSettings(80, DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX),
                Array.Empty<DamageNumberRenderer.PreparedDamageNumberCompositionInsertCommand>(),
                Array.Empty<DamageNumberRenderer.PreparedDamageNumberCompositionNativeOperation>(),
                KeepsCriticalBannerOnSeparateLayer: false,
                CriticalBannerLayerCanvasPath: "Effect/BasicEff.img/NoCri1/effect",
                CriticalBannerLayerSprite: overlaySprite);
            DamageNumberRenderer.PreparedDamageNumberVisual visual = new(
                "12345",
                80,
                DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX,
                DamageNumberRenderer.DamageNumberFormatStringPoolId,
                Array.Empty<DamageNumberRenderer.PreparedDigitDrawInfo>(),
                MissSprite: null,
                CriticalBannerSprite: overlaySprite,
                compositionTrace);
            DamageNumberRenderer.PreparedDamageNumberLayer layer = DamageNumberRenderer.PrepareLayer(visual);

            DamageNumberRenderer.PreparedDamageNumberLayerRegistration registration =
                DamageNumberRenderer.BuildOneTimeLayerRegistration(visual, layer, centerX: 500, centerTop: 250);

            Assert.False(registration.HasCriticalBanner);
            Assert.DoesNotContain(
                registration.InsertDescriptors,
                descriptor => descriptor.Content == AnimationCanvasLayerContent.OverlayCanvas);
            Assert.NotNull(registration.PreparedRegistration.RecoveredOwnerTrace);
            Assert.False(registration.PreparedRegistration.RecoveredOwnerTrace.Value.KeepsOverlayOnSeparateLayer);
            Assert.Equal(Point.Zero, registration.PreparedRegistration.RecoveredOwnerTrace.Value.OverlayOffset);
        }

        [Fact]
        public void BuildOneTimeLayerRegistration_EmitsOverlayWhenCompositionTraceOwnsOverlayLayer()
        {
            DamageNumberRenderer.PreparedSpriteDrawInfo overlaySprite = new("effect", 9, -17);
            DamageNumberRenderer.PreparedDamageNumberCompositionTrace compositionTrace = new(
                new CanvasLayerRecoveredCanvasSettings(70, DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX),
                Array.Empty<DamageNumberRenderer.PreparedDamageNumberCompositionInsertCommand>(),
                Array.Empty<DamageNumberRenderer.PreparedDamageNumberCompositionNativeOperation>(),
                KeepsCriticalBannerOnSeparateLayer: true,
                CriticalBannerLayerCanvasPath: "Effect/BasicEff.img/NoCri1/effect",
                CriticalBannerLayerSprite: overlaySprite);
            DamageNumberRenderer.PreparedDamageNumberVisual visual = new(
                "6789",
                70,
                DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX,
                DamageNumberRenderer.DamageNumberFormatStringPoolId,
                Array.Empty<DamageNumberRenderer.PreparedDigitDrawInfo>(),
                MissSprite: null,
                CriticalBannerSprite: overlaySprite,
                compositionTrace);
            DamageNumberRenderer.PreparedDamageNumberLayer layer = DamageNumberRenderer.PrepareLayer(visual);

            DamageNumberRenderer.PreparedDamageNumberLayerRegistration registration =
                DamageNumberRenderer.BuildOneTimeLayerRegistration(visual, layer, centerX: 300, centerTop: 200);

            Assert.True(registration.HasCriticalBanner);
            Assert.Contains(
                registration.InsertDescriptors,
                descriptor => descriptor.Content == AnimationCanvasLayerContent.OverlayCanvas);
            Assert.NotNull(registration.PreparedRegistration.RecoveredOwnerTrace);
            Assert.True(registration.PreparedRegistration.RecoveredOwnerTrace.Value.KeepsOverlayOnSeparateLayer);
            Assert.Equal(
                DamageNumberConstants.CRITICAL_EFFECT_OFFSET_Y,
                registration.PreparedRegistration.RecoveredOwnerTrace.Value.OverlayLayerPositionOffsetY);
        }

        [Fact]
        public void RecoveredInsertState_UsesRecoveredDurationAndMoveForOverlayCriticalLayer()
        {
            CanvasLayerInsertDescriptor[] descriptors = OneTimeCanvasLayerAnimation.BuildInsertDescriptors(
                holdDurationMs: 400,
                fadeDurationMs: 600,
                riseDistancePx: 30,
                hasOverlay: true,
                overlayOffset: new Point(12, -18),
                overlayDelayMs: 250);
            CanvasLayerInsertDescriptor overlayDescriptor = descriptors[2];

            bool active = OneTimeCanvasLayerAnimation.TryResolveRecoveredInsertState(
                overlayDescriptor,
                elapsedMs: 400,
                out float alpha,
                out Point animatedOffset);

            Assert.True(active);
            Assert.InRange(alpha, 0.79f, 0.81f);
            Assert.Equal(12, animatedOffset.X);
            Assert.Equal(-24, animatedOffset.Y);
        }

        [Fact]
        public void RecoveredInsertState_EndsAtRecoveredDurationBoundary()
        {
            CanvasLayerInsertDescriptor[] descriptors = OneTimeCanvasLayerAnimation.BuildInsertDescriptors(
                holdDurationMs: 400,
                fadeDurationMs: 600,
                riseDistancePx: 30,
                hasOverlay: true,
                overlayOffset: new Point(0, 0),
                overlayDelayMs: 250);
            CanvasLayerInsertDescriptor overlayDescriptor = descriptors[2];

            bool stillActive = OneTimeCanvasLayerAnimation.TryResolveRecoveredInsertState(
                overlayDescriptor,
                elapsedMs: 999,
                out float alphaBeforeEnd,
                out _);
            bool ended = OneTimeCanvasLayerAnimation.TryResolveRecoveredInsertState(
                overlayDescriptor,
                elapsedMs: 1000,
                out _,
                out _);

            Assert.True(stillActive);
            Assert.InRange(alphaBeforeEnd, 0.0f, 0.01f);
            Assert.False(ended);
        }

        [Fact]
        public void RecoveredNativeExecutionTrace_PrependsOwnerTemporaryCanvasOperations()
        {
            CanvasLayerRecoveredLayerSettings layerSettings = DamageNumberRenderer.ResolveRecoveredLayerSettings();
            CanvasLayerInsertDescriptor[] descriptors = OneTimeCanvasLayerAnimation.BuildInsertDescriptors(
                holdDurationMs: 400,
                fadeDurationMs: 600,
                riseDistancePx: 30,
                hasOverlay: false,
                overlayOffset: Point.Zero,
                overlayDelayMs: 0);
            CanvasLayerRecoveredRegistrationTrace registrationTrace = OneTimeCanvasLayerAnimation.BuildRecoveredRegistrationTrace(
                left: 120,
                top: 240,
                canvasWidth: 80,
                canvasHeight: DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX,
                insertDescriptors: descriptors,
                recoveredLayerSettings: layerSettings,
                registersOneTimeAnimation: true);

            CanvasLayerRecoveredPreparedSourceTrace insertSource = new(
                "NoRed0",
                "3",
                "effect/BasicEff.img/NoRed0/3",
                UseLargeDigitSet: false,
                SourceOrigin: new Point(15, 33),
                SourceWidth: 31,
                SourceHeight: 33,
                CanvasOffset: new Point(7, -3));
            CanvasLayerRecoveredOwnerTrace ownerTrace = new(
                DamageNumberRenderer.DamageNumberFormatStringPoolId,
                "123",
                new CanvasLayerRecoveredCanvasSettings(80, DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX),
                new[] { insertSource },
                new[]
                {
                    new CanvasLayerRecoveredTemporaryCanvasOperation(
                        CanvasLayerRecoveredTemporaryCanvasOperationKind.CreateCanvas,
                        new CanvasLayerRecoveredCanvasSettings(80, DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX),
                        default),
                    new CanvasLayerRecoveredTemporaryCanvasOperation(
                        CanvasLayerRecoveredTemporaryCanvasOperationKind.InsertCanvas,
                        new CanvasLayerRecoveredCanvasSettings(80, DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX),
                        insertSource)
                },
                KeepsOverlayOnSeparateLayer: false,
                OverlayCanvasPath: null,
                OverlaySpriteName: null,
                OverlayOffset: Point.Zero,
                OverlayLayerPositionOffsetY: 0);

            CanvasLayerRecoveredNativeOperation[] operations = OneTimeCanvasLayerAnimation.BuildRecoveredNativeExecutionTrace(
                registrationTrace,
                ownerTrace);

            Assert.True(operations.Length > 4);
            Assert.Equal(CanvasLayerRecoveredNativeOperationKind.CreateTemporaryCanvas, operations[0].Kind);
            Assert.Equal(CanvasLayerRecoveredNativeOperationKind.InsertTemporaryCanvas, operations[1].Kind);
            Assert.Equal(new Point(7, -3), operations[1].Offset);
            Assert.Equal(CanvasLayerRecoveredNativeOperationKind.CreateLayer, operations[2].Kind);
        }

        [Fact]
        public void RecoveredNativeExecutionTrace_RegistersOneTimeWithRetainReleasePair()
        {
            CanvasLayerRecoveredRegistrationTrace registrationTrace = OneTimeCanvasLayerAnimation.BuildRecoveredRegistrationTrace(
                left: 0,
                top: 0,
                canvasWidth: 64,
                canvasHeight: DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX,
                insertDescriptors: OneTimeCanvasLayerAnimation.BuildInsertDescriptors(
                    holdDurationMs: 400,
                    fadeDurationMs: 600,
                    riseDistancePx: 30,
                    hasOverlay: false,
                    overlayOffset: Point.Zero,
                    overlayDelayMs: 0),
                recoveredLayerSettings: DamageNumberRenderer.ResolveRecoveredLayerSettings(),
                registersOneTimeAnimation: true);

            CanvasLayerRecoveredNativeOperation[] operations = OneTimeCanvasLayerAnimation.BuildRecoveredNativeExecutionTrace(
                registrationTrace,
                ownerTrace: null);

            Assert.True(operations.Length >= 3);
            Assert.Equal(CanvasLayerRecoveredNativeOperationKind.RetainLayerForOneTimeRegistration, operations[^3].Kind);
            Assert.Equal(1, operations[^3].Value);
            Assert.Equal(CanvasLayerRecoveredNativeOperationKind.RegisterOneTimeAnimation, operations[^2].Kind);
            Assert.Equal(CanvasLayerRecoveredNativeOperationKind.ReleaseLayerAfterOneTimeRegistration, operations[^1].Kind);
            Assert.Equal(-1, operations[^1].Value);
        }

        [Fact]
        public void RecoveredNativeExecutionTrace_SkipsRetainReleaseWhenOneTimeRegistrationDisabled()
        {
            CanvasLayerRecoveredRegistrationTrace registrationTrace = OneTimeCanvasLayerAnimation.BuildRecoveredRegistrationTrace(
                left: 0,
                top: 0,
                canvasWidth: 64,
                canvasHeight: DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX,
                insertDescriptors: OneTimeCanvasLayerAnimation.BuildInsertDescriptors(
                    holdDurationMs: 400,
                    fadeDurationMs: 600,
                    riseDistancePx: 30,
                    hasOverlay: false,
                    overlayOffset: Point.Zero,
                    overlayDelayMs: 0),
                recoveredLayerSettings: DamageNumberRenderer.ResolveRecoveredLayerSettings(),
                registersOneTimeAnimation: false);

            CanvasLayerRecoveredNativeOperation[] operations = OneTimeCanvasLayerAnimation.BuildRecoveredNativeExecutionTrace(
                registrationTrace,
                ownerTrace: null);

            Assert.DoesNotContain(
                operations,
                operation => operation.Kind == CanvasLayerRecoveredNativeOperationKind.RetainLayerForOneTimeRegistration
                             || operation.Kind == CanvasLayerRecoveredNativeOperationKind.RegisterOneTimeAnimation
                             || operation.Kind == CanvasLayerRecoveredNativeOperationKind.ReleaseLayerAfterOneTimeRegistration);
        }

        [Fact]
        public void TrembleConfigGate_RejectsNonEnforcedRequestsWhenDisabled()
        {
            ScreenEffects effects = new()
            {
                TrembleEnabled = false
            };

            effects.TriggerTremble(18.0, heavyAndShort: true, delayMs: 0, additionalTimeMs: 0, enforce: false, currentTimeMs: 1000);

            Assert.False(effects.IsTrembleActive);
        }

        [Fact]
        public void TrembleDurationAndReduction_MatchHeavyAndNormalOwnerValues()
        {
            ScreenEffects heavy = new();
            heavy.TriggerTremble(10.0, heavyAndShort: true, delayMs: 10, additionalTimeMs: 0, enforce: true, currentTimeMs: 1000);

            Assert.True(heavy.IsTrembleActive);
            Assert.Equal(1010, heavy.TrembleStartTime);
            Assert.Equal(2510, heavy.TrembleEndTime);
            Assert.Equal(0.85, heavy.TrembleReduction, 5);

            ScreenEffects normal = new();
            normal.TriggerTremble(10.0, heavyAndShort: false, delayMs: 10, additionalTimeMs: 0, enforce: true, currentTimeMs: 1000);

            Assert.True(normal.IsTrembleActive);
            Assert.Equal(1010, normal.TrembleStartTime);
            Assert.Equal(3010, normal.TrembleEndTime);
            Assert.Equal(0.92, normal.TrembleReduction, 5);
        }

        [Fact]
        public void TrembleAdditionalTime_UsesNoReductionDuringExtensionWindow()
        {
            ScreenEffects effects = new();

            effects.TriggerTremble(12.0, heavyAndShort: true, delayMs: 5, additionalTimeMs: 120, enforce: true, currentTimeMs: 2000);

            Assert.True(effects.IsTrembleActive);
            Assert.Equal(2005, effects.TrembleStartTime);
            Assert.Equal(3625, effects.TrembleEndTime);
            Assert.Equal(1.0, effects.TrembleReduction, 5);
        }
    }
}
