using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Combat;
using Xunit;

namespace UnitTest_MapSimulator;

public class AnimationDisplayerReservedOwnerParityTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void ReservedTypeConsumptionGate_MatchesRecoveredClientRange(int reservedType, bool expected)
    {
        Assert.Equal(
            expected,
            MapSimulator.ShouldConsumeAnimationDisplayerReservedTypeWithoutVisualFallback(reservedType));
    }

    [Theory]
    [InlineData(100000000, 100000000, false)]
    [InlineData(100000001, 100000000, true)]
    [InlineData(-1, 100000000, false)]
    [InlineData(999999999, 100000000, false)] // MapConstants.MaxMap
    public void ReservedTransferFieldGate_RespectsTargetAndCurrentField(int targetFieldId, int currentMapId, bool expected)
    {
        Assert.Equal(
            expected,
            MapSimulator.ShouldApplyAnimationDisplayerReservedTransferFieldRequest(targetFieldId, currentMapId));
    }

    [Theory]
    [InlineData(0, 100000000, true)]
    [InlineData(100000000, 100000000, true)]
    [InlineData(100000001, 100000000, false)]
    [InlineData(100000001, -1, true)]
    public void ReservedFieldScopedVisualGate_MatchesClientStyleFieldCheck(int reservedFieldId, int currentMapId, bool expected)
    {
        Assert.Equal(
            expected,
            MapSimulator.ShouldApplyAnimationDisplayerReservedFieldScopedVisual(reservedFieldId, currentMapId));
    }

    [Theory]
    [InlineData("Bgm01.img/FloralLife", "Bgm01/FloralLife")]
    [InlineData("BgmEvent.img/Summer", "BgmEvent/Summer")]
    [InlineData("UI.img/Click", "")]
    [InlineData("Sound.img/Bgm01", "")]
    public void ReservedBgmOverride_OnlyAcceptsBgmImageFamilies(string descriptor, string expected)
    {
        Assert.Equal(expected, MapSimulator.ResolveAnimationDisplayerReservedBgmOverrideName(descriptor));
    }

    [Fact]
    public void ReservedType4RestoreTarget_PreservesOriginalPreReservedActionAcrossChainedTriggers()
    {
        (string previousActionName, bool? previousFacingRight) =
            MapSimulator.ResolveAnimationDisplayerReservedRemoteUtilityActionRestoreTarget(
                actorActionName: "attack2",
                actorFacingRight: false,
                activeReservedActionName: "attack2",
                activePreviousActionName: "stand1",
                activePreviousFacingRight: true);

        Assert.Equal("stand1", previousActionName);
        Assert.True(previousFacingRight);
    }

    [Theory]
    [InlineData("Effect/BasicEff.img/Catch/Success", true, true)]
    [InlineData("Effect/BasicEff.img/Catch/Success/3", true, true)]
    [InlineData("Effect/BasicEff.img/Catch/Fail", true, false)]
    [InlineData("BasicEff/Catch/5", true, true)]
    [InlineData("Effect/BasicEff.img/Catch", true, true)]
    [InlineData("Effect/BasicEff.img/Catch/Unknown", false, false)]
    public void CatchEffectPathResolver_CoversSuccessFailAndNumericCatchBranches(
        string effectUol,
        bool expectedResolved,
        bool expectedSuccess)
    {
        bool resolved = MapSimulator.TryResolveAnimationDisplayerCatchSuccessFromEffectUol(effectUol, out bool success);
        Assert.Equal(expectedResolved, resolved);
        Assert.Equal(expectedSuccess, success);
    }

    [Theory]
    [InlineData(false, false, false, 2, true, false)]
    [InlineData(false, true, false, 2, true, true)]
    [InlineData(true, false, false, 2, true, true)]
    [InlineData(true, true, false, 1, false, true)]
    [InlineData(true, true, true, 2, true, false)]
    public void MobSwallowProjectileOwnerGate_AcceptsResolvedFallbackFrames(
        bool hasCanvasFrames,
        bool hasResolvedAnimationDisplayerFrames,
        bool hasClientMobActionFrames,
        int attackType,
        bool isRangedAttack,
        bool expected)
    {
        bool canRegister = MobAttackSystem.ShouldRegisterAnimationDisplayerMobSwallowBullet(
            hasCanvasFrames,
            hasResolvedAnimationDisplayerFrames,
            hasClientMobActionFrames,
            attackType,
            isRangedAttack);
        Assert.Equal(expected, canRegister);
    }
}
