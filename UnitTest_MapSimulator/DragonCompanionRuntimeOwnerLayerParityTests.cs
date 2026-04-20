using HaCreator.MapSimulator.Companions;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class DragonCompanionRuntimeOwnerLayerParityTests
{
    [Fact]
    public void ResolveClientOwnerUpdateVisibility_IgnoresMapSuppression_WhenOwnerUpdated()
    {
        Assert.True(DragonCompanionRuntime.ResolveClientOwnerUpdateVisibility(ownerUpdated: true, suppressedForMap: true));
        Assert.False(DragonCompanionRuntime.ResolveClientOwnerUpdateVisibility(ownerUpdated: false, suppressedForMap: false));
    }

    [Fact]
    public void ResolveClientActionLayerColorAfterOwnerUpdate_CapsAlpha_WhenOwnerPhaseMismatches()
    {
        Color current = new(255, 255, 255, 255);

        Color result = DragonCompanionRuntime.ResolveClientActionLayerColorAfterOwnerUpdate(
            current,
            ownerUpdateVisible: true,
            hasLocalUser: true,
            ownerMatchesLocalPhase: false,
            ownerPhaseAlpha: 96,
            hasSpecialDragonRidingMount: false);

        Assert.Equal(96, result.A);
    }

    [Fact]
    public void ResolveClientActionLayerColorAfterOwnerUpdate_RestoresOpaque_WhenOwnerPhaseMatches()
    {
        Color current = new(255, 255, 255, 80);

        Color result = DragonCompanionRuntime.ResolveClientActionLayerColorAfterOwnerUpdate(
            current,
            ownerUpdateVisible: true,
            hasLocalUser: true,
            ownerMatchesLocalPhase: true,
            ownerPhaseAlpha: 60,
            hasSpecialDragonRidingMount: false);

        Assert.Equal(255, result.A);
    }

    [Fact]
    public void ResolveClientActionLayerColorAfterOwnerUpdate_PreservesCurrentAlpha_ForSpecialDragonRidingMount()
    {
        Color current = new(255, 255, 255, 90);

        Color result = DragonCompanionRuntime.ResolveClientActionLayerColorAfterOwnerUpdate(
            current,
            ownerUpdateVisible: true,
            hasLocalUser: true,
            ownerMatchesLocalPhase: true,
            ownerPhaseAlpha: 30,
            hasSpecialDragonRidingMount: true);

        Assert.Equal(90, result.A);
    }

    [Fact]
    public void ResolveNoDragonSuppression_UsesWrapperOwnedDecisionOnly()
    {
        Assert.False(DragonCompanionRuntime.ResolveNoDragonSuppression(null));
        Assert.False(DragonCompanionRuntime.ResolveNoDragonSuppression(false));
        Assert.True(DragonCompanionRuntime.ResolveNoDragonSuppression(true));
    }
}
