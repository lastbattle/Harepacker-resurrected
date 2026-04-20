using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class SharedSkillUseAnimationOwnerParityTests
{
    [Fact]
    public void ResolveClientLocalShowSkillEffectBLeftOverride_UsesFixedZeroForDoActiveSkillSummonFamily()
    {
        SkillData skill = new()
        {
            SkillId = 3111002
        };

        int? resolvedBLeft = SkillManager.ResolveClientLocalShowSkillEffectBLeftOverride(skill, moveActionRawCode: 1);

        Assert.True(resolvedBLeft.HasValue);
        Assert.Equal(0, resolvedBLeft.Value);
    }

    [Fact]
    public void ResolveClientLocalShowSkillEffectBLeftOverride_UsesMoveActionForMonsterMagnetFamily()
    {
        SkillData skill = new()
        {
            SkillId = 1121001
        };

        Assert.Equal(1, SkillManager.ResolveClientLocalShowSkillEffectBLeftOverride(skill, moveActionRawCode: 1));
        Assert.Equal(0, SkillManager.ResolveClientLocalShowSkillEffectBLeftOverride(skill, moveActionRawCode: 0));
    }

    [Fact]
    public void ResolveClientLocalShowSkillEffectBLeftOverride_ReturnsNullForUnownedSkillIds()
    {
        SkillData skill = new()
        {
            SkillId = 2301002
        };

        Assert.Null(SkillManager.ResolveClientLocalShowSkillEffectBLeftOverride(skill, moveActionRawCode: 1));
    }
}
