using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class SummonAttackBranchParityTests
{
    [Theory]
    [InlineData(4, "attack1")]
    [InlineData(5, "attack2")]
    [InlineData(7, "attack4")]
    public void ResolvePacketAttackBranch_UsesDefaultAttackWindowForOrdinaryAttackActions(byte rawAction, string expectedBranch)
    {
        SkillData skill = CreateSummonSkill("attack1", "attack2", "attack4");

        string branch = SummonRuntimeRules.ResolvePacketAttackBranch(skill, rawAction);

        Assert.Equal(expectedBranch, branch);
    }

    [Fact]
    public void ResolvePacketAttackBranch_PrefersTeslaTriangleBranchForClientAction6()
    {
        SkillData skill = CreateSummonSkill("attack1", "attack3", "attackTriangle");

        string branch = SummonRuntimeRules.ResolvePacketAttackBranch(skill, 6);

        Assert.Equal("attackTriangle", branch);
    }

    [Fact]
    public void ResolvePacketAttackBranch_PrefersSupportOwnedBranchForClientAction13()
    {
        SkillData skill = CreateSummonSkill("heal", "support", "attack1");
        skill.MinionAbility = "heal";

        string branch = SummonRuntimeRules.ResolvePacketAttackBranch(skill, 13);

        Assert.Equal("heal", branch);
    }

    [Fact]
    public void ResolvePacketAttackBranch_PrefersRemovalBranchForClientAction16()
    {
        SkillData skill = CreateSummonSkill("attack1", "die1");
        skill.SummonAttackBranchName = "attack1";
        skill.SummonRemovalBranchName = "die1";

        string branch = SummonRuntimeRules.ResolvePacketAttackBranch(skill, 16);

        Assert.Equal("die1", branch);
    }

    [Fact]
    public void ResolvePacketAttackBranch_FallsBackToFirstAuthoredAttackBranchWhenSpecialClientBranchIsMissing()
    {
        SkillData skill = CreateSummonSkill("attack1", "attack3");

        string branch = SummonRuntimeRules.ResolvePacketAttackBranch(skill, 6);

        Assert.Equal("attack1", branch);
    }

    private static SkillData CreateSummonSkill(params string[] branchNames)
    {
        SkillData skill = new();
        foreach (string branchName in branchNames)
        {
            skill.SummonNamedAnimations[branchName] = new SkillAnimation { Name = branchName };
        }

        return skill;
    }
}
