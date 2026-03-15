using System.Collections.Generic;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class FieldObjectQuestVisibilityEvaluatorTests
    {
        [Fact]
        public void IsVisible_AllowsObjectsWithoutQuestMetadata()
        {
            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(null, _ => QuestStateType.Not_Started);

            Assert.True(visible);
        }

        [Fact]
        public void IsVisible_ReturnsTrueWhenAnyQuestRequirementMatches()
        {
            var questInfo = new List<ObjectInstanceQuest>
            {
                new ObjectInstanceQuest(1000, QuestStateType.Started),
                new ObjectInstanceQuest(1001, QuestStateType.Completed)
            };

            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                questInfo,
                questId => questId == 1000 ? QuestStateType.Started : QuestStateType.Not_Started);

            Assert.True(visible);
        }

        [Fact]
        public void IsVisible_ReturnsFalseWhenNoQuestRequirementMatches()
        {
            var questInfo = new List<ObjectInstanceQuest>
            {
                new ObjectInstanceQuest(1000, QuestStateType.Started)
            };

            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                questInfo,
                _ => QuestStateType.Completed);

            Assert.False(visible);
        }

        [Fact]
        public void IsVisible_ReturnsFalseWhenMapMarksObjectHidden()
        {
            var questInfo = new List<ObjectInstanceQuest>
            {
                new ObjectInstanceQuest(1000, QuestStateType.Started)
            };

            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                hiddenByMap: true,
                questInfo,
                questId => questId == 1000 ? QuestStateType.Started : QuestStateType.Not_Started);

            Assert.True(visible);
        }

        [Fact]
        public void IsVisible_ReturnsFalseForMapHiddenObjectsWithoutQuestMetadata()
        {
            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                hiddenByMap: true,
                questInfo: null,
                _ => QuestStateType.Not_Started);

            Assert.False(visible);
        }

        [Fact]
        public void IsVisible_IgnoresDynamicTagsWithoutPublishedRuntimeState()
        {
            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                hiddenByMap: false,
                questInfo: null,
                dynamicTags: new[] { "stageDoor" },
                _ => QuestStateType.Not_Started,
                _ => null);

            Assert.True(visible);
        }

        [Fact]
        public void IsVisible_RequiresPublishedTagStateToRevealMapHiddenObjects()
        {
            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                hiddenByMap: true,
                questInfo: null,
                dynamicTags: new[] { "stageDoor" },
                _ => QuestStateType.Not_Started,
                _ => null);

            Assert.False(visible);
        }

        [Fact]
        public void IsVisible_RevealsMapHiddenObjectsWhenDynamicTagStateEnablesThem()
        {
            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                hiddenByMap: true,
                questInfo: null,
                dynamicTags: new[] { "stageDoor" },
                _ => QuestStateType.Not_Started,
                tag => tag == "stageDoor" ? true : null);

            Assert.True(visible);
        }

        [Fact]
        public void IsVisible_ReturnsFalseWhenDynamicTagStateDisablesObject()
        {
            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                hiddenByMap: false,
                questInfo: null,
                dynamicTags: new[] { "stageDoor" },
                _ => QuestStateType.Not_Started,
                tag => tag == "stageDoor" ? false : null);

            Assert.False(visible);
        }

        [Fact]
        public void IsVisible_RequiresQuestMatchBeforeAllowingEnabledDynamicTags()
        {
            var questInfo = new List<ObjectInstanceQuest>
            {
                new ObjectInstanceQuest(1000, QuestStateType.Started)
            };

            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                hiddenByMap: false,
                questInfo,
                dynamicTags: new[] { "stageDoor" },
                _ => QuestStateType.Completed,
                tag => tag == "stageDoor" ? true : null);

            Assert.False(visible);
        }
    }
}
