using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator;

public sealed class SpecialMobParityFlowTests
{
    [Fact]
    public void EscortProgression_UsesLowestActiveEscortIndex()
    {
        EscortProgressionState state = EscortProgressionController.ResolveState(new int?[] { null, 3, 1, 2 });

        Assert.True(state.HasIndexedEscorts);
        Assert.Equal(1, state.ActiveIndex);
        Assert.True(EscortProgressionController.CanFollowIndex(1, state));
        Assert.False(EscortProgressionController.CanFollowIndex(2, state));
        Assert.False(EscortProgressionController.CanFollowIndex(3, state));
    }

    [Fact]
    public void EscortProgression_AllowsFollowWhenNoIndexedEscortIsLive()
    {
        EscortProgressionState state = EscortProgressionController.ResolveState(new int?[] { null, 0, -1 });

        Assert.False(state.HasIndexedEscorts);
        Assert.Null(state.ActiveIndex);
        Assert.True(EscortProgressionController.CanFollowIndex(null, state));
        Assert.True(EscortProgressionController.CanFollowIndex(2, state));
    }

    [Fact]
    public void MobAi_RemoveAfterTimeout_KillsThroughTimeoutLane()
    {
        MobAI ai = new MobAI();
        ai.Initialize(maxHp: 100);
        ai.ConfigureSpecialBehavior(
            canTargetPlayer: true,
            isEscortMob: false,
            removeAfterMs: 1000);

        ai.Update(0, 0f, 0f, null, null);
        ai.Update(1000, 0f, 0f, null, null);

        Assert.True(ai.IsDead);
        Assert.Equal(MobDeathType.Timeout, ai.DeathType);
        Assert.Equal(MobAIState.Death, ai.State);
    }

    [Fact]
    public void MobAi_SelfDestructionRemoveAfter_UsesReservedActionBeforeBombDeath()
    {
        MobAI ai = new MobAI();
        ai.Initialize(maxHp: 100);
        ai.ConfigureSpecialBehavior(
            canTargetPlayer: true,
            isEscortMob: false,
            selfDestructAction: 4,
            selfDestructRemoveAfterMs: 1000);
        ai.AddAttack(new MobAttackEntry
        {
            AttackId = 4,
            AnimationName = "attack4",
            Range = 100,
            Cooldown = 0
        });

        ai.Update(0, 0f, 0f, null, null);
        ai.Update(1000, 0f, 0f, null, null);

        Assert.False(ai.IsDead);
        Assert.Equal(MobAIState.Attack, ai.State);

        ai.NotifyAttackAnimationComplete(1000);

        Assert.True(ai.IsDead);
        Assert.Equal(MobDeathType.Bomb, ai.DeathType);
        Assert.Equal(MobAIState.Death, ai.State);
    }
}
