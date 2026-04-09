using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class SummonSupportSuspendParityTests
{
    [Fact]
    public void ShouldClearHealingRobotSupportSuspend_DoesNotClearBeforeSuspendWindowExpires()
    {
        ActiveSummon summon = CreateHealingRobotSummon(supportSuspendUntilTime: 1500);

        bool shouldClear = SummonRuntimeRules.ShouldClearHealingRobotSupportSuspend(
            summon,
            currentTime: 1499,
            healingRobotSkillId: 35111011);

        Assert.False(shouldClear);
    }

    [Fact]
    public void ShouldClearHealingRobotSupportSuspend_ClearsWhenSuspendWindowExpires()
    {
        ActiveSummon summon = CreateHealingRobotSummon(supportSuspendUntilTime: 1500);

        bool shouldClear = SummonRuntimeRules.ShouldClearHealingRobotSupportSuspend(
            summon,
            currentTime: 1500,
            healingRobotSkillId: 35111011);

        Assert.True(shouldClear);
    }

    [Fact]
    public void ShouldClearHealingRobotSupportSuspend_IgnoresOtherSummonsAndUnsuspendedRobots()
    {
        ActiveSummon nonHealingRobot = new()
        {
            SkillId = 35111002,
            AssistType = SummonAssistType.Support,
            SupportSuspendUntilTime = 1500
        };
        ActiveSummon unsuspendedHealingRobot = CreateHealingRobotSummon(supportSuspendUntilTime: int.MinValue);

        Assert.False(SummonRuntimeRules.ShouldClearHealingRobotSupportSuspend(
            nonHealingRobot,
            currentTime: 2000,
            healingRobotSkillId: 35111011));
        Assert.False(SummonRuntimeRules.ShouldClearHealingRobotSupportSuspend(
            unsuspendedHealingRobot,
            currentTime: 2000,
            healingRobotSkillId: 35111011));
    }

    private static ActiveSummon CreateHealingRobotSummon(int supportSuspendUntilTime)
    {
        return new ActiveSummon
        {
            SkillId = 35111011,
            AssistType = SummonAssistType.Support,
            SupportSuspendUntilTime = supportSuspendUntilTime
        };
    }
}
