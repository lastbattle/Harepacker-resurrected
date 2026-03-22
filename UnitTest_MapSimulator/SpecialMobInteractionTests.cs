using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure.Data.MobStructure;

namespace UnitTest_MapSimulator;

public sealed class SpecialMobInteractionTests
{
    [Fact]
    public void RewardSuppression_CoversFriendlyEscortAndSpecialDeathLanes()
    {
        Assert.True(SpecialMobInteractionRules.ShouldSuppressRewardDrops(
            new MobData { Friendly = true },
            MobDeathType.Killed));
        Assert.True(SpecialMobInteractionRules.ShouldSuppressRewardDrops(
            new MobData { Escort = 1 },
            MobDeathType.Killed));
        Assert.True(SpecialMobInteractionRules.ShouldSuppressRewardDrops(
            new MobData(),
            MobDeathType.Timeout));
        Assert.False(SpecialMobInteractionRules.ShouldSuppressRewardDrops(
            new MobData(),
            MobDeathType.Killed));
    }

    [Fact]
    public void ResolveState_UsesLowestActiveEscortIndex()
    {
        EscortProgressionState state = EscortProgressionController.ResolveState(new int?[] { 3, null, 2, 5 });

        Assert.True(state.HasIndexedEscorts);
        Assert.Equal(2, state.ActiveIndex);
        Assert.True(EscortProgressionController.CanFollowIndex(2, state));
        Assert.False(EscortProgressionController.CanFollowIndex(3, state));
    }

    [Fact]
    public void Update_ExpiresRemoveAfterMobsThroughTimeoutLane()
    {
        MobAI ai = new MobAI();
        ai.Initialize(100);
        ai.ConfigureSpecialBehavior(canTargetPlayer: false, isEscortMob: false, removeAfterMs: 1000);

        ai.Update(0, 0f, 0f, null, null);
        ai.Update(1001, 0f, 0f, null, null);

        Assert.True(ai.IsDead);
        Assert.Equal(MobDeathType.Timeout, ai.DeathType);
    }

    [Fact]
    public void Update_TriggersSelfDestructionBombWhenThresholdIsReached()
    {
        MobAI ai = new MobAI();
        ai.Initialize(100);
        ai.ConfigureSpecialBehavior(canTargetPlayer: false, isEscortMob: false, selfDestructHpThreshold: 50);

        Assert.False(ai.TakeDamage(50, 0, false, null, null));

        ai.Update(1, 0f, 0f, null, null);

        Assert.True(ai.IsDead);
        Assert.Equal(MobDeathType.Bomb, ai.DeathType);
    }
}
