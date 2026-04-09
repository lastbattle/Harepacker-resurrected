using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class SummonedActionLoaderParityTests
{
    [Fact]
    public void ResolveSpawnPlaybackBranch_FallsBackToClientSpawnActionNames()
    {
        SkillData skill = CreateSkill("summoned");
        skill.SummonSpawnBranchName = "create";

        string branchName = SummonRuntimeRules.ResolveSpawnPlaybackBranch(skill);

        Assert.Equal("summoned", branchName);
    }

    [Fact]
    public void ResolvePacketAttackBranch_PrefersExactIndexedAttackBranch()
    {
        SkillData skill = CreateSkill("attack2", "attack", "attackTriangle");

        string branchName = SummonRuntimeRules.ResolvePacketAttackBranch(skill, packetAction: 5);

        Assert.Equal("attack2", branchName);
    }

    [Fact]
    public void ResolvePacketAttackBranch_FallsBackToGenericAttackBranch()
    {
        SkillData skill = CreateSkill("attack", "attackTriangle");

        string branchName = SummonRuntimeRules.ResolvePacketAttackBranch(skill, packetAction: 6);

        Assert.Equal("attack", branchName);
    }

    [Fact]
    public void ResolvePacketSkillBranch_UsesHealFallbackForFirstSkillSlot()
    {
        SkillData skill = CreateSkill("heal", "support");
        skill.MinionAbility = "heal";

        string branchName = SummonRuntimeRules.ResolvePacketSkillBranch(
            skill,
            packetAction: 1,
            assistType: SummonAssistType.Support);

        Assert.Equal("heal", branchName);
    }

    [Fact]
    public void ResolveHitPlaybackBranch_FallsBackToNestedHitBranch()
    {
        SkillData skill = CreateSkill("hit/0");

        string branchName = SummonRuntimeRules.ResolveHitPlaybackBranch(skill);

        Assert.Equal("hit/0", branchName);
    }

    private static SkillData CreateSkill(params string[] branchNames)
    {
        SkillData skill = new();
        foreach (string branchName in branchNames)
        {
            skill.SummonNamedAnimations[branchName] = new SkillAnimation
            {
                Name = branchName,
                Frames = { new SkillFrame { Delay = 120 } }
            };
        }

        return skill;
    }
}
