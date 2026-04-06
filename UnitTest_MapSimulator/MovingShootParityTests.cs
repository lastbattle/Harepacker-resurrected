using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public sealed class MovingShootParityTests
    {
        [Fact]
        public void ResolveQueuedMovingShootEntryAction_PrefersAuthoredClientActionOverLiveGenericAction()
        {
            var skill = new SkillData
            {
                SkillId = 33121004,
                ActionNames = new[] { "shoot2", "shoot1" }
            };

            (string actionName, int? rawActionCode) = SkillManager.ResolveQueuedMovingShootEntryAction(
                skill,
                currentActionName: "shoot1",
                currentRawActionCode: 22);

            Assert.Equal("shoot2", actionName);
            Assert.Equal(23, rawActionCode);
        }

        [Fact]
        public void ResolveQueuedMovingShootEntryAction_FallsBackToCurrentRawActionWhenSkillHasNoMappedQueueAction()
        {
            var skill = new SkillData
            {
                SkillId = 33121004,
                ActionNames = new[] { "customWildHunterAction" }
            };

            (string actionName, int? rawActionCode) = SkillManager.ResolveQueuedMovingShootEntryAction(
                skill,
                currentActionName: "customWildHunterAction",
                currentRawActionCode: 24);

            Assert.Equal("shootF", actionName);
            Assert.Equal(24, rawActionCode);
        }
    }
}
