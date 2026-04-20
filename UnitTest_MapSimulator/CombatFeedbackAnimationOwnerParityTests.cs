using System.Linq;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Loaders;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class CombatFeedbackAnimationOwnerParityTests
{
    [Theory]
    [InlineData(null, DamageColorType.Red)]
    [InlineData("", DamageColorType.Red)]
    [InlineData("red", DamageColorType.Red)]
    [InlineData("blue", DamageColorType.Blue)]
    [InlineData("violet", DamageColorType.Violet)]
    [InlineData("0", DamageColorType.Red)]
    [InlineData("1", DamageColorType.Blue)]
    [InlineData("2", DamageColorType.Violet)]
    public void TryResolveAnimationDisplayerCombatFeedbackColor_AcceptsOnlyOwnerTokens(
        string token,
        DamageColorType expectedColor)
    {
        bool resolved = MapSimulator.TryResolveAnimationDisplayerCombatFeedbackColor(
            token,
            out DamageColorType colorType);

        Assert.True(resolved);
        Assert.Equal(expectedColor, colorType);
    }

    [Theory]
    [InlineData("purple")]
    [InlineData("3")]
    [InlineData("-1")]
    public void TryResolveAnimationDisplayerCombatFeedbackColor_RejectsNonOwnerTokens(string token)
    {
        bool resolved = MapSimulator.TryResolveAnimationDisplayerCombatFeedbackColor(
            token,
            out _);

        Assert.False(resolved);
    }

    [Theory]
    [InlineData("miss", "Miss")]
    [InlineData("MISS", "Miss")]
    [InlineData("guard", "guard")]
    [InlineData("shot", "shot")]
    [InlineData("counter", "counter")]
    [InlineData("resist", "resist")]
    [InlineData("unknown", "Miss")]
    [InlineData("", "Miss")]
    public void ResolveSpecialTextName_CanonicalizesOwnerSetNames(string specialTextName, string expected)
    {
        string resolved = DamageNumberRenderer.ResolveSpecialTextName(specialTextName);
        Assert.Equal(expected, resolved);
    }

    [Theory]
    [InlineData(DamageColorType.Red)]
    [InlineData(DamageColorType.Blue)]
    [InlineData(DamageColorType.Violet)]
    public void ResolveAnimationDisplayerCombatFeedbackEffectUol_SupportedColorsRouteToNoRed0Owner(
        DamageColorType colorType)
    {
        string resolved = MapSimulator.ResolveAnimationDisplayerCombatFeedbackEffectUol("shot", colorType);
        Assert.Equal("Effect/BasicEff.img/NoRed0/shot", resolved);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void ResolveAnimationDisplayerCombatFeedbackEffectUol_UnsupportedColorReturnsNull(int encodedColor)
    {
        string resolved = MapSimulator.ResolveAnimationDisplayerCombatFeedbackEffectUol(
            "shot",
            (DamageColorType)encodedColor);
        Assert.Null(resolved);
    }

    [Fact]
    public void BuildOneTimeLayerRegistration_CriticalPathKeepsRecoveredTimingAndLayerSettings()
    {
        DamageNumberRenderer.PreparedDamageNumberVisual visual = DamageNumberRenderer.PrepareVisual(
            damage: 12345,
            colorType: DamageColorType.Red,
            isCritical: true,
            isMiss: false,
            BuildCriticalLargeDigitSet(),
            BuildSmallDigitSet());
        DamageNumberRenderer.PreparedDamageNumberLayer layer = DamageNumberRenderer.PrepareLayer(visual);
        DamageNumberRenderer.PreparedDamageNumberLayerRegistration registration =
            DamageNumberRenderer.BuildOneTimeLayerRegistration(
                visual,
                layer,
                centerX: 220,
                centerTop: 500);

        Assert.Equal(220 - visual.CanvasWidth / 2, registration.Placement.Left);
        Assert.Equal(500 - DamageNumberConstants.COMPOSITE_PLACEMENT_OFFSET_Y, registration.Placement.Top);
        Assert.Equal(3, registration.InsertDescriptors.Length);

        CanvasLayerInsertDescriptor holdDescriptor = registration.InsertDescriptors[0];
        CanvasLayerInsertDescriptor fadeDescriptor = registration.InsertDescriptors[1];
        CanvasLayerInsertDescriptor overlayDescriptor = registration.InsertDescriptors[2];

        Assert.Equal(400, holdDescriptor.RecoveredInsertCanvasSettings.DurationMs);
        Assert.Equal(255, holdDescriptor.RecoveredInsertCanvasSettings.StartAlphaValue);
        Assert.Equal(255, holdDescriptor.RecoveredInsertCanvasSettings.EndAlphaValue);

        Assert.Equal(600, fadeDescriptor.RecoveredInsertCanvasSettings.DurationMs);
        Assert.Equal(255, fadeDescriptor.RecoveredInsertCanvasSettings.StartAlphaValue);
        Assert.Equal(0, fadeDescriptor.RecoveredInsertCanvasSettings.EndAlphaValue);
        Assert.Equal(new Point(0, -30), fadeDescriptor.RecoveredMoveSettings.EndOffset);

        Assert.Equal(250, overlayDescriptor.StartDelayMs);
        Assert.Equal(750, overlayDescriptor.RecoveredInsertCanvasSettings.DurationMs);
        Assert.Equal(255, overlayDescriptor.RecoveredInsertCanvasSettings.StartAlphaValue);
        Assert.Equal(0, overlayDescriptor.RecoveredInsertCanvasSettings.EndAlphaValue);

        Assert.Equal(0, registration.RecoveredLayerSettings.CreateLayerCanvasValue);
        Assert.Equal(unchecked((int)0xC0050004), registration.RecoveredLayerSettings.InitialLayerOptionValue);
        Assert.Equal(-1, registration.RecoveredLayerSettings.LayerPriorityValue);
        Assert.Equal(0, registration.RecoveredLayerSettings.FinalizeLayerOptionValue);
    }

    [Fact]
    public void BuildRecoveredNativeExecutionTrace_PrependsTemporaryCanvasOperations()
    {
        DamageNumberRenderer.PreparedDamageNumberVisual visual = DamageNumberRenderer.PrepareVisual(
            damage: 54321,
            colorType: DamageColorType.Red,
            isCritical: true,
            isMiss: false,
            BuildCriticalLargeDigitSet(),
            BuildSmallDigitSet());
        DamageNumberRenderer.PreparedDamageNumberLayer layer = DamageNumberRenderer.PrepareLayer(visual);
        DamageNumberRenderer.PreparedDamageNumberLayerRegistration registration =
            DamageNumberRenderer.BuildOneTimeLayerRegistration(
                visual,
                layer,
                centerX: 400,
                centerTop: 360);
        CanvasLayerRecoveredOwnerTrace ownerTrace = Assert.IsType<CanvasLayerRecoveredOwnerTrace>(
            registration.PreparedRegistration.RecoveredOwnerTrace);

        CanvasLayerRecoveredNativeOperation[] operations =
            OneTimeCanvasLayerAnimation.BuildRecoveredNativeExecutionTrace(
                registration.RecoveredRegistrationTrace,
                ownerTrace);

        Assert.NotEmpty(operations);
        Assert.Equal(CanvasLayerRecoveredNativeOperationKind.CreateTemporaryCanvas, operations[0].Kind);

        CanvasLayerRecoveredNativeOperation firstInsertTemporary = operations.First(
            operation => operation.Kind == CanvasLayerRecoveredNativeOperationKind.InsertTemporaryCanvas);
        Assert.Equal(0, firstInsertTemporary.InsertCanvasSettings.DurationMs);
        Assert.Equal(255, firstInsertTemporary.InsertCanvasSettings.StartAlphaValue);
        Assert.Equal(255, firstInsertTemporary.InsertCanvasSettings.EndAlphaValue);
        Assert.Equal(firstInsertTemporary.MoveSettings.StartOffset, firstInsertTemporary.MoveSettings.EndOffset);
    }

    [Fact]
    public void TriggerTremble_RespectsGateAndRecoveredDurations()
    {
        var effects = new ScreenEffects
        {
            TrembleEnabled = false
        };

        effects.TriggerTremble(
            trembleForce: 5.0,
            heavyAndShort: true,
            delayMs: 0,
            additionalTimeMs: 0,
            enforce: false,
            currentTimeMs: 1000);
        Assert.False(effects.IsTrembleActive);

        effects.TriggerTremble(
            trembleForce: 5.0,
            heavyAndShort: true,
            delayMs: 0,
            additionalTimeMs: 0,
            enforce: true,
            currentTimeMs: 1000);
        Assert.True(effects.IsTrembleActive);
        Assert.Equal(1000, effects.TrembleStartTime);
        Assert.Equal(2500, effects.TrembleEndTime);
        Assert.Equal(0.85, effects.TrembleReduction, 3);
    }

    [Fact]
    public void TriggerTremble_AdditionalTimeUsesNoReductionAndExtendsDuration()
    {
        var effects = new ScreenEffects();

        effects.TriggerTremble(
            trembleForce: 5.0,
            heavyAndShort: false,
            delayMs: 80,
            additionalTimeMs: 120,
            enforce: true,
            currentTimeMs: 900);

        Assert.True(effects.IsTrembleActive);
        Assert.Equal(980, effects.TrembleStartTime);
        Assert.Equal(3100, effects.TrembleEndTime);
        Assert.Equal(1.0, effects.TrembleReduction, 3);
    }

    private static DamageNumberDigitSet BuildCriticalLargeDigitSet()
    {
        var set = new DamageNumberDigitSet
        {
            Name = "NoCri1",
            IsLoaded = true,
            CriticalEffectOrigin = new Point(41, 70)
        };

        for (int i = 0; i <= 9; i++)
        {
            set.Widths[i] = 43;
            set.Heights[i] = 48;
            set.Origins[i] = new Point(22, 35);
        }

        return set;
    }

    private static DamageNumberDigitSet BuildSmallDigitSet()
    {
        var set = new DamageNumberDigitSet
        {
            Name = "NoRed0",
            IsLoaded = true
        };

        for (int i = 0; i <= 9; i++)
        {
            set.Widths[i] = 31;
            set.Heights[i] = 33;
            set.Origins[i] = new Point(15, 24);
        }

        return set;
    }
}
