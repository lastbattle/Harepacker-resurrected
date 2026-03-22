using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Combat;

namespace UnitTest_MapSimulator;

public sealed class MobSkillStatusMapperTests
{
    [Theory]
    [InlineData(140, MobStatusEffect.PImmune)]
    [InlineData(141, MobStatusEffect.MImmune)]
    [InlineData(142, MobStatusEffect.HardSkin)]
    public void TryGetDefinition_MapsMobDefenseSkillFamily(int skillId, MobStatusEffect effect)
    {
        bool mapped = MobSkillStatusMapper.TryGetDefinition(skillId, out MobSkillStatusDefinition definition);

        Assert.True(mapped);
        Assert.Equal(skillId, definition.SkillId);
        Assert.Equal(MobSkillOperation.ApplyStatus, definition.Operation);
        Assert.Equal(effect, definition.Effect);
        Assert.Equal(MobSkillStatusTargetMode.Self, definition.TargetMode);
    }

    [Fact]
    public void ResolveStatusValue_UsesHpForHardSkinReduction()
    {
        int value = MobSkillStatusMapper.ResolveStatusValue(MobStatusEffect.HardSkin, x: 1, y: 0, hp: 70);

        Assert.Equal(70, value);
    }

    [Fact]
    public void HardSkinStatus_ReducesIncomingPhysicalDamage()
    {
        MobAI ai = new MobAI();
        ai.Initialize(1000);
        ai.ApplyStatusEffect(MobStatusEffect.HardSkin, durationMs: 5000, currentTick: 0, value: 70);

        int damage = ai.CalculateIncomingDamage(100, MobDamageType.Physical);

        Assert.Equal(30, damage);
    }

    [Fact]
    public void PhysicalImmunityStatus_ReducesIncomingPhysicalDamageToOne()
    {
        MobAI ai = new MobAI();
        ai.Initialize(1000);
        ai.ApplyStatusEffect(MobStatusEffect.PImmune, durationMs: 5000, currentTick: 0, value: 1);

        int damage = ai.CalculateIncomingDamage(100, MobDamageType.Physical);

        Assert.Equal(1, damage);
    }
}
