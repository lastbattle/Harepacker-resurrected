using System;
using System.Reflection;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character;
using Xunit;

namespace UnitTest_MapSimulator;

public class MobStatusParitySeamTests
{
    [Theory]
    [InlineData(138, 100, 0, 0, 0, true)]
    [InlineData(138, 0, 0, 0, 0, false)]
    [InlineData(170, 100, 100, 1, 0, false)]
    public void ShouldResistMobSkillStatus_UsesExpectedMobSkillAdmission(
        int skillId,
        int abnormalResistancePercent,
        int elementalResistancePercent,
        int elementAttribute,
        int rollPercent,
        bool expected)
    {
        bool resisted = PlayerMobStatusController.ShouldResistMobSkillStatus(
            skillId,
            abnormalResistancePercent,
            elementalResistancePercent,
            elementAttribute,
            rollPercent);

        Assert.Equal(expected, resisted);
    }

    [Fact]
    public void IsPlayerTargetedMobSkill_Admits138ButNot170()
    {
        Assert.True(InvokeIsPlayerTargetedMobSkill(138));
        Assert.False(InvokeIsPlayerTargetedMobSkill(170));
    }

    private static bool InvokeIsPlayerTargetedMobSkill(int skillId)
    {
        MethodInfo method = typeof(MapSimulator).GetMethod(
            "IsPlayerTargetedMobSkill",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        object result = method.Invoke(null, new object[] { skillId });
        Assert.IsType<bool>(result);
        return (bool)result;
    }
}
