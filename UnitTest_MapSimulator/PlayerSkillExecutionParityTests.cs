using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public class PlayerSkillExecutionParityTests
    {
        [Fact]
        public void SwallowAbsorbOutcomeBuffer_PreservesArrivalOrder_ForSameSkillAndTarget()
        {
            WildHunterSwallowAbsorbOutcomeBuffer buffer = new();

            buffer.Store(skillId: 33101005, targetMobId: 42, success: false, currentTime: 1000, lifetimeMs: 5000);
            buffer.Store(skillId: 33101005, targetMobId: 42, success: true, currentTime: 1100, lifetimeMs: 5000);

            bool consumedFirst = buffer.TryConsume(skillId => skillId == 33101005, targetMobId: 42, currentTime: 1200, out bool firstSuccess);
            bool consumedSecond = buffer.TryConsume(skillId => skillId == 33101005, targetMobId: 42, currentTime: 1300, out bool secondSuccess);

            Assert.True(consumedFirst);
            Assert.False(firstSuccess);
            Assert.True(consumedSecond);
            Assert.True(secondSuccess);
        }

        [Fact]
        public void SwallowAbsorbOutcomeBuffer_ConsumesOnlyMatchingTarget_AndRetainsOtherEntries()
        {
            WildHunterSwallowAbsorbOutcomeBuffer buffer = new();

            buffer.Store(skillId: 33101005, targetMobId: 100, success: true, currentTime: 1000, lifetimeMs: 5000);
            buffer.Store(skillId: 33101005, targetMobId: 200, success: false, currentTime: 1100, lifetimeMs: 5000);

            bool consumedSecondTargetFirst = buffer.TryConsume(skillId => skillId == 33101005, targetMobId: 200, currentTime: 1200, out bool secondTargetSuccess);
            bool consumedFirstTargetAfter = buffer.TryConsume(skillId => skillId == 33101005, targetMobId: 100, currentTime: 1300, out bool firstTargetSuccess);

            Assert.True(consumedSecondTargetFirst);
            Assert.False(secondTargetSuccess);
            Assert.True(consumedFirstTargetAfter);
            Assert.True(firstTargetSuccess);
        }

        [Fact]
        public void SwallowAbsorbOutcomeBuffer_DropsOldest_WhenCapacityExceeded()
        {
            WildHunterSwallowAbsorbOutcomeBuffer buffer = new();

            for (int i = 0; i < 9; i++)
            {
                buffer.Store(skillId: 33101005, targetMobId: 1000 + i, success: true, currentTime: 1000 + (i * 10), lifetimeMs: 5000);
            }

            bool consumedEvictedOldest = buffer.TryConsume(skillId => skillId == 33101005, targetMobId: 1000, currentTime: 1200, out _);
            bool consumedNextOldest = buffer.TryConsume(skillId => skillId == 33101005, targetMobId: 1001, currentTime: 1200, out bool nextOldestSuccess);

            Assert.False(consumedEvictedOldest);
            Assert.True(consumedNextOldest);
            Assert.True(nextOldestSuccess);
        }

        [Fact]
        public void SwallowFollowUpQueueCapacityGate_MatchesClientBoundedOwnership()
        {
            Assert.True(SkillManager.ShouldQueueWildHunterSwallowFollowUp(
                pendingFollowUpCount: 0,
                SkillManager.SwallowFollowUpRequestKind.Digest));
            Assert.True(SkillManager.ShouldQueueWildHunterSwallowFollowUp(
                pendingFollowUpCount: 7,
                SkillManager.SwallowFollowUpRequestKind.Attack));
            Assert.False(SkillManager.ShouldQueueWildHunterSwallowFollowUp(
                pendingFollowUpCount: 8,
                SkillManager.SwallowFollowUpRequestKind.Attack));
            Assert.False(SkillManager.ShouldQueueWildHunterSwallowFollowUp(
                pendingFollowUpCount: 1,
                SkillManager.SwallowFollowUpRequestKind.None));
        }
    }
}
