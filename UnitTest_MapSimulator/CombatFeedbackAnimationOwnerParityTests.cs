using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Loaders;
using Microsoft.Xna.Framework;
using Xunit;

namespace UnitTest_MapSimulator;

public class CombatFeedbackAnimationOwnerParityTests
{
    [Fact]
    public void ResolveSpecialTextName_NormalizesToCanonicalOwnerNames()
    {
        Assert.Equal("Miss", DamageNumberRenderer.ResolveSpecialTextName(null));
        Assert.Equal("Miss", DamageNumberRenderer.ResolveSpecialTextName("   "));
        Assert.Equal("Miss", DamageNumberRenderer.ResolveSpecialTextName("miss"));
        Assert.Equal("guard", DamageNumberRenderer.ResolveSpecialTextName("GUARD"));
        Assert.Equal("shot", DamageNumberRenderer.ResolveSpecialTextName("Shot"));
        Assert.Equal("counter", DamageNumberRenderer.ResolveSpecialTextName("COUNTER"));
        Assert.Equal("resist", DamageNumberRenderer.ResolveSpecialTextName("resist"));
        Assert.Equal("Miss", DamageNumberRenderer.ResolveSpecialTextName("unknown"));
    }

    [Fact]
    public void TryResolveAnimationDisplayerCombatFeedbackColor_AcceptsOnlyOwnerTokens()
    {
        Assert.True(MapSimulator.TryResolveAnimationDisplayerCombatFeedbackColor("red", out DamageColorType red));
        Assert.Equal(DamageColorType.Red, red);

        Assert.True(MapSimulator.TryResolveAnimationDisplayerCombatFeedbackColor("blue", out DamageColorType blue));
        Assert.Equal(DamageColorType.Blue, blue);

        Assert.True(MapSimulator.TryResolveAnimationDisplayerCombatFeedbackColor("violet", out DamageColorType violet));
        Assert.Equal(DamageColorType.Violet, violet);

        Assert.True(MapSimulator.TryResolveAnimationDisplayerCombatFeedbackColor("0", out DamageColorType redNumeric));
        Assert.Equal(DamageColorType.Red, redNumeric);

        Assert.True(MapSimulator.TryResolveAnimationDisplayerCombatFeedbackColor("1", out DamageColorType blueNumeric));
        Assert.Equal(DamageColorType.Blue, blueNumeric);

        Assert.True(MapSimulator.TryResolveAnimationDisplayerCombatFeedbackColor("2", out DamageColorType violetNumeric));
        Assert.Equal(DamageColorType.Violet, violetNumeric);
    }

    [Theory]
    [InlineData("purple")]
    [InlineData("3")]
    [InlineData("-1")]
    public void TryResolveAnimationDisplayerCombatFeedbackColor_RejectsUnsupportedTokens(string token)
    {
        bool parsed = MapSimulator.TryResolveAnimationDisplayerCombatFeedbackColor(token, out _);
        Assert.False(parsed);
    }

    [Fact]
    public void ResolveAnimationDisplayerCombatFeedbackEffectUol_AlwaysRoutesSpecialTextToNoRed0Owner()
    {
        Assert.Equal(
            "Effect/BasicEff.img/NoRed0/shot",
            MapSimulator.ResolveAnimationDisplayerCombatFeedbackEffectUol("shot", DamageColorType.Red));
        Assert.Equal(
            "Effect/BasicEff.img/NoRed0/shot",
            MapSimulator.ResolveAnimationDisplayerCombatFeedbackEffectUol("shot", DamageColorType.Blue));
        Assert.Equal(
            "Effect/BasicEff.img/NoRed0/shot",
            MapSimulator.ResolveAnimationDisplayerCombatFeedbackEffectUol("shot", DamageColorType.Violet));
        Assert.Equal(
            "Effect/BasicEff.img/NoRed0/Miss",
            MapSimulator.ResolveAnimationDisplayerCombatFeedbackEffectUol("not_authored", DamageColorType.Red));
    }

    [Fact]
    public void ResolveAnimationDisplayerCombatFeedbackEffectUol_RejectsUnsupportedColor()
    {
        string effectUol = MapSimulator.ResolveAnimationDisplayerCombatFeedbackEffectUol(
            "Miss",
            (DamageColorType)3);
        Assert.Null(effectUol);
    }

    [Fact]
    public void BuildOneTimeLayerRegistration_UsesRecoveredEffectHpTimelineAndLayerConstants()
    {
        DamageNumberDigitSet large = CreateDigitSet("NoRed1", originX: 15, width: 31, height: 35);
        large.CriticalEffectOrigin = new Point(41, 70);
        DamageNumberDigitSet small = CreateDigitSet("NoRed0", originX: 15, width: 31, height: 35);

        var visual = DamageNumberRenderer.PrepareVisual(
            12345,
            DamageColorType.Red,
            isCritical: true,
            isMiss: false,
            specialTextName: null,
            large,
            small);
        var layer = DamageNumberRenderer.PrepareLayer(visual);
        var registration = DamageNumberRenderer.BuildOneTimeLayerRegistration(visual, layer, centerX: 500, centerTop: 300);

        Assert.Equal(400, registration.Timeline.HoldDurationMs);
        Assert.Equal(600, registration.Timeline.FadeDurationMs);
        Assert.Equal(1000, registration.Timeline.TotalLifetimeMs);
        Assert.Equal(250, registration.Timeline.CriticalDelayMs);
        Assert.Equal(30, registration.Timeline.RiseDistancePx);

        Assert.Equal(unchecked((int)0xC0050004), registration.RecoveredLayerSettings.InitialLayerOptionValue);
        Assert.Equal(-1, registration.RecoveredLayerSettings.LayerPriorityValue);
        Assert.Equal(0, registration.RecoveredLayerSettings.CreateLayerCanvasValue);
        Assert.Equal(0, registration.RecoveredLayerSettings.FinalizeLayerOptionValue);

        Assert.Equal(500 - registration.Placement.Width / 2, registration.Placement.Left);
        Assert.Equal(300 - 47, registration.Placement.Top);

        Assert.True(registration.HasCriticalBanner);
        Assert.Equal(3, registration.InsertDescriptors.Length);
        Assert.Equal(400, registration.InsertDescriptors[0].RecoveredInsertCanvasSettings.DurationMs);
        Assert.Equal(255, registration.InsertDescriptors[0].RecoveredInsertCanvasSettings.StartAlphaValue);
        Assert.Equal(255, registration.InsertDescriptors[0].RecoveredInsertCanvasSettings.EndAlphaValue);
        Assert.Equal(600, registration.InsertDescriptors[1].RecoveredInsertCanvasSettings.DurationMs);
        Assert.Equal(255, registration.InsertDescriptors[1].RecoveredInsertCanvasSettings.StartAlphaValue);
        Assert.Equal(0, registration.InsertDescriptors[1].RecoveredInsertCanvasSettings.EndAlphaValue);
        Assert.Equal(250, registration.InsertDescriptors[2].StartDelayMs);
    }

    [Fact]
    public void BuildRecoveredNativeExecutionTrace_PrependsTemporaryCanvasOperationsWith255To255InsertInvariants()
    {
        CanvasLayerRecoveredLayerSettings layerSettings = DamageNumberRenderer.ResolveRecoveredLayerSettings();
        CanvasLayerInsertDescriptor[] insertDescriptors = OneTimeCanvasLayerAnimation.BuildInsertDescriptors(
            holdDurationMs: 400,
            fadeDurationMs: 600,
            riseDistancePx: 30,
            hasOverlay: true,
            overlayOffset: new Point(3, 4),
            overlayDelayMs: 250);
        CanvasLayerRecoveredRegistrationTrace registrationTrace = OneTimeCanvasLayerAnimation.BuildRecoveredRegistrationTrace(
            left: 100,
            top: 200,
            canvasWidth: 62,
            canvasHeight: 57,
            insertDescriptors,
            layerSettings,
            registersOneTimeAnimation: true);
        CanvasLayerRecoveredOwnerTrace ownerTrace = new(
            FormatStringPoolId: 0x1A15,
            FormattedText: "1234",
            CanvasSettings: new CanvasLayerRecoveredCanvasSettings(62, 57),
            PreparedSources: new[]
            {
                new CanvasLayerRecoveredPreparedSourceTrace(
                    "NoRed1",
                    "1",
                    "effect/BasicEff.img/NoRed1/1",
                    UseLargeDigitSet: true,
                    SourceOrigin: new Point(15, 20),
                    SourceWidth: 31,
                    SourceHeight: 35,
                    CanvasOffset: new Point(0, 0))
            },
            TemporaryCanvasOperations: new[]
            {
                new CanvasLayerRecoveredTemporaryCanvasOperation(
                    CanvasLayerRecoveredTemporaryCanvasOperationKind.CreateCanvas,
                    new CanvasLayerRecoveredCanvasSettings(62, 57),
                    default),
                new CanvasLayerRecoveredTemporaryCanvasOperation(
                    CanvasLayerRecoveredTemporaryCanvasOperationKind.InsertCanvas,
                    new CanvasLayerRecoveredCanvasSettings(62, 57),
                    new CanvasLayerRecoveredPreparedSourceTrace(
                        "NoRed1",
                        "1",
                        "effect/BasicEff.img/NoRed1/1",
                        UseLargeDigitSet: true,
                        SourceOrigin: new Point(15, 20),
                        SourceWidth: 31,
                        SourceHeight: 35,
                        CanvasOffset: new Point(7, 8)))
            },
            KeepsOverlayOnSeparateLayer: true,
            OverlayCanvasPath: "effect/BasicEff.img/NoCri1/effect",
            OverlaySpriteName: "effect",
            OverlayOffset: new Point(10, 11),
            OverlayLayerPositionOffsetY: -30);

        CanvasLayerRecoveredNativeOperation[] trace =
            OneTimeCanvasLayerAnimation.BuildRecoveredNativeExecutionTrace(registrationTrace, ownerTrace);

        Assert.True(trace.Length > 6);
        Assert.Equal(CanvasLayerRecoveredNativeOperationKind.CreateTemporaryCanvas, trace[0].Kind);
        Assert.Equal(CanvasLayerRecoveredNativeOperationKind.InsertTemporaryCanvas, trace[1].Kind);
        Assert.Equal(CanvasLayerRecoveredNativeOperationKind.CreateLayer, trace[2].Kind);
        Assert.Equal(255, trace[1].InsertCanvasSettings.StartAlphaValue);
        Assert.Equal(255, trace[1].InsertCanvasSettings.EndAlphaValue);
        Assert.Equal(0, trace[1].InsertCanvasSettings.DurationMs);
        Assert.Equal(new Point(7, 8), trace[1].MoveSettings.StartOffset);
        Assert.Equal(new Point(7, 8), trace[1].MoveSettings.EndOffset);
        Assert.Contains(trace, op => op.Kind == CanvasLayerRecoveredNativeOperationKind.RegisterOneTimeAnimation);
    }

    [Fact]
    public void TriggerTremble_MirrorsClientGateDurationsAndReductionFactors()
    {
        var effects = new ScreenEffects
        {
            TrembleEnabled = false
        };

        effects.TriggerTremble(
            trembleForce: 12.5,
            heavyAndShort: true,
            delayMs: 50,
            additionalTimeMs: 0,
            enforce: false,
            currentTimeMs: 1000);

        Assert.False(effects.IsTrembleActive);

        effects.TriggerTremble(
            trembleForce: 12.5,
            heavyAndShort: true,
            delayMs: 50,
            additionalTimeMs: 0,
            enforce: true,
            currentTimeMs: 1000);

        Assert.True(effects.IsTrembleActive);
        Assert.Equal(1050, effects.TrembleStartTime);
        Assert.Equal(2550, effects.TrembleEndTime);
        Assert.Equal(0.85, effects.TrembleReduction, 3);

        effects.TriggerTremble(
            trembleForce: 12.5,
            heavyAndShort: false,
            delayMs: 10,
            additionalTimeMs: 0,
            enforce: true,
            currentTimeMs: 2000);

        Assert.Equal(2010, effects.TrembleStartTime);
        Assert.Equal(4010, effects.TrembleEndTime);
        Assert.Equal(0.92, effects.TrembleReduction, 3);

        effects.TriggerTremble(
            trembleForce: 12.5,
            heavyAndShort: false,
            delayMs: 0,
            additionalTimeMs: 300,
            enforce: true,
            currentTimeMs: 3000);

        Assert.Equal(3000, effects.TrembleStartTime);
        Assert.Equal(5300, effects.TrembleEndTime);
        Assert.Equal(1.0, effects.TrembleReduction, 3);
    }

    private static DamageNumberDigitSet CreateDigitSet(string name, int originX, int width, int height)
    {
        var set = new DamageNumberDigitSet
        {
            IsLoaded = true,
            Name = name
        };

        for (int digit = 0; digit <= 9; digit++)
        {
            set.Origins[digit] = new Point(originX, 20);
            set.Widths[digit] = width;
            set.Heights[digit] = height;
        }

        return set;
    }
}
