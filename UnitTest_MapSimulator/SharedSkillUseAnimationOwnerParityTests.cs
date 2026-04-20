using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class SharedSkillUseAnimationOwnerParityTests
{
    [Fact]
    public void NonMeleeLocalShowSkillEffectStyleCast_UsesDirectPath_AndSkipsRequestSeam()
    {
        var cast = new SkillCastInfo
        {
            SkillId = 2301002,
            SkillData = new SkillData
            {
                SkillId = 2301002,
                IsAttack = false,
                IsPrepareSkill = false,
                IsKeydownSkill = false
            },
            DelayRateOverride = SkillManager.ResolveClientShowSkillEffectDelayRateFromActionSpeed(0)
        };

        Assert.True(MapSimulator.ShouldRegisterLocalSkillCastThroughClientShowSkillEffectDirectPathForTesting(cast));
        Assert.False(MapSimulator.ShouldRouteLocalSkillCastThroughClientSkillEffectRequestSeamForTesting(cast));
    }

    [Fact]
    public void LocalAttackCast_DoesNotUseClientShowSkillEffectDirectPath()
    {
        var cast = new SkillCastInfo
        {
            SkillId = 1121008,
            SkillData = new SkillData
            {
                SkillId = 1121008,
                IsAttack = true
            }
        };

        Assert.False(MapSimulator.ShouldRegisterLocalSkillCastThroughClientShowSkillEffectDirectPathForTesting(cast));
        Assert.False(MapSimulator.ShouldRouteLocalSkillCastThroughClientSkillEffectRequestSeamForTesting(cast));
    }

    [Fact]
    public void MissingSkillData_DoesNotRouteThroughEitherPath()
    {
        var cast = new SkillCastInfo
        {
            SkillId = 2301002,
            SkillData = null
        };

        Assert.False(MapSimulator.ShouldRegisterLocalSkillCastThroughClientShowSkillEffectDirectPathForTesting(cast));
        Assert.False(MapSimulator.ShouldRouteLocalSkillCastThroughClientSkillEffectRequestSeamForTesting(cast));
    }
}
