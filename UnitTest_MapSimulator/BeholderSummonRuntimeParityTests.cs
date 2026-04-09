using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class BeholderSummonRuntimeParityTests
{
    [Fact]
    public void ResolveSupportSuspendDurationMs_UsesStandFallback_ForStandOnlyPacketOwnedSupportSummons()
    {
        SkillData skill = CreateSupportSummonSkill(
            skillId: 35111005,
            minionAbility: "mes",
            ("stand", 90));

        int suspendDurationMs = SummonRuntimeRules.ResolveSupportSuspendDurationMs(
            skill,
            preferHealFirst: false);

        Assert.Equal(90, suspendDurationMs);
        Assert.True(SummonRuntimeRules.ShouldTrackSupportSuspendWindow(skill, SummonAssistType.Support));
    }

    [Fact]
    public void ResolveSupportSuspendDurationMs_PrefersAuthoredSupportBranch_ForNonHealPacketOwnedSupportSummons()
    {
        SkillData skill = CreateSupportSummonSkill(
            skillId: 35121010,
            minionAbility: "amplifyDamage",
            ("skill2", 180),
            ("stand", 120));

        int suspendDurationMs = SummonRuntimeRules.ResolveSupportSuspendDurationMs(
            skill,
            preferHealFirst: false);

        Assert.Equal(180, suspendDurationMs);
    }

    [Fact]
    public void ShouldClearHealingRobotSupportSuspend_AllowsNonHealingRobotSupportSummonsWithTrackedSupportWindow()
    {
        SkillData skill = CreateSupportSummonSkill(
            skillId: 35121010,
            minionAbility: "amplifyDamage",
            ("stand", 120));
        ActiveSummon summon = new()
        {
            SkillId = 35121010,
            SkillData = skill,
            AssistType = SummonAssistType.Support,
            SupportSuspendUntilTime = 500
        };

        Assert.True(SummonRuntimeRules.ShouldClearHealingRobotSupportSuspend(
            summon,
            currentTime: 500,
            healingRobotSkillId: 35111011));
    }

    private static SkillData CreateSupportSummonSkill(
        int skillId,
        string minionAbility,
        params (string BranchName, int DelayMs)[] branches)
    {
        SkillData skill = new()
        {
            SkillId = skillId,
            MinionAbility = minionAbility
        };

        foreach ((string branchName, int delayMs) in branches)
        {
            skill.SummonNamedAnimations[branchName] = new SkillAnimation
            {
                Name = branchName,
                Frames =
                {
                    new SkillFrame
                    {
                        Delay = delayMs
                    }
                }
            };
        }

        return skill;
    }
}
