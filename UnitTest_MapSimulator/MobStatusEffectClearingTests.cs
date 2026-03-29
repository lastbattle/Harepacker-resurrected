using HaCreator.MapSimulator.AI;

namespace UnitTest_MapSimulator;

public sealed class MobStatusEffectClearingTests
{
    [Fact]
    public void ClearNegativeStatusEffects_RemovesSignedStatDebuffs()
    {
        var ai = new MobAI();
        ai.Initialize(1000);
        ai.ApplyStatusEffect(MobStatusEffect.ACC, 5000, 0, -15);
        ai.ApplyStatusEffect(MobStatusEffect.EVA, 5000, 0, -20);
        ai.ApplyStatusEffect(MobStatusEffect.PDamage, 5000, 0, -25);
        ai.ApplyStatusEffect(MobStatusEffect.Showdown, 5000, 0, 10);

        int cleared = ai.ClearNegativeStatusEffects();

        Assert.Equal(4, cleared);
        Assert.False(ai.HasStatusEffect(MobStatusEffect.ACC));
        Assert.False(ai.HasStatusEffect(MobStatusEffect.EVA));
        Assert.False(ai.HasStatusEffect(MobStatusEffect.PDamage));
        Assert.False(ai.HasStatusEffect(MobStatusEffect.Showdown));
    }

    [Fact]
    public void ClearPositiveStatusEffects_RemovesSignedStatBuffs()
    {
        var ai = new MobAI();
        ai.Initialize(1000);
        ai.ApplyStatusEffect(MobStatusEffect.ACC, 5000, 0, 12);
        ai.ApplyStatusEffect(MobStatusEffect.EVA, 5000, 0, 18);
        ai.ApplyStatusEffect(MobStatusEffect.Speed, 5000, 0, 25);
        ai.ApplyStatusEffect(MobStatusEffect.Rich, 5000, 0, 1);

        int cleared = ai.ClearPositiveStatusEffects();

        Assert.Equal(4, cleared);
        Assert.False(ai.HasStatusEffect(MobStatusEffect.ACC));
        Assert.False(ai.HasStatusEffect(MobStatusEffect.EVA));
        Assert.False(ai.HasStatusEffect(MobStatusEffect.Speed));
        Assert.False(ai.HasStatusEffect(MobStatusEffect.Rich));
    }
}
