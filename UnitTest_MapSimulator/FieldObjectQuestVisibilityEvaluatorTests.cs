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

            Assert.False(visible);
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
    }
}
