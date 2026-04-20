using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator;

public sealed class RemoteUserTemporaryStatAvatarEffectParityTests
{
    [Fact]
    public void AuraReplacementCadenceReset_WhenCurrentAuraStartsWithAnimation_ReturnsTrue()
    {
        RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState current = CreateAuraState(skillId: 32001003);

        bool shouldReset =
            RemoteUserActorPool.ShouldResetRemoteTemporaryStatAffectedLayerShiftCadenceForAuraReplacementForTesting(
                previousState: null,
                current);

        Assert.True(shouldReset);
    }

    [Fact]
    public void AuraReplacementCadenceReset_WhenSkillIdChanges_ReturnsTrue()
    {
        RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState previous = CreateAuraState(skillId: 32001003);
        RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState current = CreateAuraState(skillId: 32101002);

        bool shouldReset =
            RemoteUserActorPool.ShouldResetRemoteTemporaryStatAffectedLayerShiftCadenceForAuraReplacementForTesting(
                previous,
                current);

        Assert.True(shouldReset);
    }

    [Fact]
    public void AuraReplacementCadenceReset_WhenSkillIdMatches_ReturnsFalse()
    {
        RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState previous = CreateAuraState(skillId: 32110000);
        RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState current = CreateAuraState(skillId: 32110000);

        bool shouldReset =
            RemoteUserActorPool.ShouldResetRemoteTemporaryStatAffectedLayerShiftCadenceForAuraReplacementForTesting(
                previous,
                current);

        Assert.False(shouldReset);
    }

    [Fact]
    public void AuraReplacementCadenceReset_WhenCurrentStateHasNoEffectPlane_ReturnsFalse()
    {
        RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState previous = CreateAuraState(skillId: 32001003);
        RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState current = new()
        {
            SkillId = 32101002
        };

        bool shouldReset =
            RemoteUserActorPool.ShouldResetRemoteTemporaryStatAffectedLayerShiftCadenceForAuraReplacementForTesting(
                previous,
                current);

        Assert.False(shouldReset);
    }

    [Fact]
    public void ForcedShiftCadenceReset_AlwaysReturnsPhaseOne()
    {
        int boundaryReset =
            RemoteUserActorPool.ResolveRemoteTemporaryStatAffectedLayerShiftCadenceUpdateCountAfterForcedShiftForTesting(33);
        int arbitraryReset =
            RemoteUserActorPool.ResolveRemoteTemporaryStatAffectedLayerShiftCadenceUpdateCountAfterForcedShiftForTesting(7);

        Assert.Equal(1, boundaryReset);
        Assert.Equal(1, arbitraryReset);
    }

    [Fact]
    public void RemainingUpdates_AfterForcedShiftReset_UsesPostResetCadencePhase()
    {
        int postForcedShiftUpdateCount =
            RemoteUserActorPool.ResolveRemoteTemporaryStatAffectedLayerShiftCadenceUpdateCountAfterForcedShiftForTesting(66);

        int remainingUpdates =
            RemoteUserActorPool.ResolveRemoteTemporaryStatAffectedLayerShiftCadenceRemainingUpdatesForTesting(
                postForcedShiftUpdateCount);

        Assert.Equal(32, remainingUpdates);
    }

    private static RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState CreateAuraState(int skillId)
    {
        return new RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState
        {
            SkillId = skillId,
            UnderFaceAnimation = new SkillAnimation
            {
                Name = "affected",
                Frames =
                {
                    new SkillFrame
                    {
                        Delay = 180
                    }
                }
            }
        };
    }
}
