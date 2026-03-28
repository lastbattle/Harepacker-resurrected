using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Combat;
using HaCreator.MapSimulator.Character.Skills;
using System.Reflection;

namespace UnitTest_MapSimulator;

public sealed class MobSkillStatusMapperTests
{
    [Theory]
    [InlineData(150, MobStatusEffect.PowerUp)]
    [InlineData(151, MobStatusEffect.PGuardUp)]
    [InlineData(152, MobStatusEffect.MagicUp)]
    [InlineData(153, MobStatusEffect.MGuardUp)]
    [InlineData(154, MobStatusEffect.ACC)]
    [InlineData(155, MobStatusEffect.EVA)]
    [InlineData(156, MobStatusEffect.Speed)]
    public void MonsterCarnivalGuardianSkillsMapToExpectedMobStatuses(int skillId, MobStatusEffect expectedEffect)
    {
        bool mapped = MobSkillStatusMapper.TryGetDefinition(skillId, out MobSkillStatusDefinition definition);

        Assert.True(mapped);
        Assert.Equal(MobSkillOperation.ApplyStatus, definition.Operation);
        Assert.Equal(expectedEffect, definition.Effect);
        Assert.Equal(MobSkillStatusTargetMode.Self, definition.TargetMode);
    }

    [Theory]
    [InlineData(MobStatusEffect.PowerUp, 40)]
    [InlineData(MobStatusEffect.PGuardUp, 50)]
    [InlineData(MobStatusEffect.MagicUp, 40)]
    [InlineData(MobStatusEffect.MGuardUp, 50)]
    [InlineData(MobStatusEffect.ACC, 30)]
    [InlineData(MobStatusEffect.EVA, 30)]
    [InlineData(MobStatusEffect.Speed, 30)]
    public void GuardianStatusValuesResolveFromRuntimeXField(MobStatusEffect effect, int expectedValue)
    {
        int resolved = MobSkillStatusMapper.ResolveStatusValue(effect, expectedValue, y: 0, hp: 0);

        Assert.Equal(expectedValue, resolved);
    }

    [Fact]
    public void SkillDebuffResolutionUsesLoadedEvaStatMagnitude()
    {
        MethodInfo resolver = typeof(SkillManager).GetMethod(
            "ResolveEvasionDebuffMagnitude",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(resolver);

        var levelData = new SkillLevelData
        {
            EVA = -12
        };

        object resolved = resolver.Invoke(null, new object[] { levelData, "reduce target eva" });

        Assert.Equal(12, Assert.IsType<int>(resolved));
    }
}
