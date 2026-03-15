using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System.Collections.Generic;

namespace UnitTest_MapSimulator
{
    public class FieldObjectVisibilityTests
    {
        [Fact]
        public void IsVisible_RevealsHiddenObject_WhenAuthoredTagStateIsPublished()
        {
            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                hiddenByMap: true,
                questInfo: null,
                dynamicTags: new[] { "cannonTuto" },
                getQuestState: _ => QuestStateType.Not_Started,
                getDynamicTagState: tag => tag == "cannonTuto" ? true : null);

            Assert.True(visible);
        }

        [Fact]
        public void IsVisible_HidesVisibleObject_WhenDynamicTagStateTurnsOff()
        {
            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                hiddenByMap: false,
                questInfo: null,
                dynamicTags: new[] { "eventDoor" },
                getQuestState: _ => QuestStateType.Not_Started,
                getDynamicTagState: tag => tag == "eventDoor" ? false : null);

            Assert.False(visible);
        }

        [Fact]
        public void Load_ParsesNamedVisibilityEntries_FromPublicTaggedObjectVisible()
        {
            MapInfo mapInfo = new MapInfo();
            WzSubProperty defaults = new WzSubProperty("publicTaggedObjectVisible");
            defaults["cannonTuto"] = new WzIntProperty("cannonTuto", 1);
            defaults["eventDoor"] = new WzIntProperty("eventDoor", 0);
            mapInfo.additionalNonInfoProps.Add(defaults);

            IReadOnlyDictionary<string, bool> states = FieldObjectTagStateDefaultsLoader.Load(mapInfo);

            Assert.True(states["cannonTuto"]);
            Assert.False(states["eventDoor"]);
        }

        [Fact]
        public void Load_ParsesIndexedVisibilityEntries_FromTypoedInfoVariant()
        {
            MapInfo mapInfo = new MapInfo();
            WzSubProperty defaults = new WzSubProperty("pulbicTaggedObjectVisible");

            WzSubProperty firstEntry = new WzSubProperty("0");
            firstEntry["tag"] = new WzStringProperty("tag", "questDoor");
            firstEntry["visible"] = new WzIntProperty("visible", 1);
            defaults["0"] = firstEntry;

            WzSubProperty secondEntry = new WzSubProperty("1");
            secondEntry["name"] = new WzStringProperty("name", "hiddenSwitch");
            secondEntry["state"] = new WzIntProperty("state", 0);
            defaults["1"] = secondEntry;

            mapInfo.unsupportedInfoProperties.Add(defaults);

            IReadOnlyDictionary<string, bool> states = FieldObjectTagStateDefaultsLoader.Load(mapInfo);

            Assert.True(states["questDoor"]);
            Assert.False(states["hiddenSwitch"]);
        }

        [Fact]
        public void IsVisible_StillRequiresQuestState_WhenQuestMetadataIsPresent()
        {
            var questInfo = new List<ObjectInstanceQuest>
            {
                new ObjectInstanceQuest(22000, QuestStateType.Completed)
            };

            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(
                hiddenByMap: true,
                questInfo: questInfo,
                dynamicTags: new[] { "questDoor" },
                getQuestState: _ => QuestStateType.Started,
                getDynamicTagState: _ => true);

            Assert.False(visible);
        }
    }
}
