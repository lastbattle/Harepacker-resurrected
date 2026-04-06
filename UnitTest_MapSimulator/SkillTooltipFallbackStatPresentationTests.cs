using HaCreator.MapSimulator.Loaders;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator
{
    public sealed class SkillTooltipFallbackStatPresentationTests
    {
        [Fact]
        public void BuildFallbackLevelDescriptionForTests_IncludesDamageOverTimeFieldsInWzOrder()
        {
            WzSubProperty skillEntry = new("11110000");
            WzSubProperty common = new("common");
            common.AddProperty(new WzStringProperty("mpCon", "30+x"));
            common.AddProperty(new WzStringProperty("damage", "205+15*x"));
            common.AddProperty(new WzStringProperty("time", "5+7*x"));
            common.AddProperty(new WzStringProperty("dot", "25+15*x"));
            common.AddProperty(new WzStringProperty("dotInterval", "1"));
            common.AddProperty(new WzStringProperty("dotTime", "3+x"));
            common.AddProperty(new WzStringProperty("mobCount", "8"));
            skillEntry.AddProperty(common);

            WzSubProperty info = new("info");
            info.AddProperty(new WzIntProperty("dot", 1));
            info.AddProperty(new WzStringProperty("dotType", "burn"));
            skillEntry.AddProperty(info);

            string description = SkillDataLoader.BuildFallbackLevelDescriptionForTests(skillEntry, 3);

            Assert.Equal(
                "MP Cost: 33\n" +
                "Damage: 250%\n" +
                "Duration: 26 sec\n" +
                "Damage Over Time: 70\n" +
                "Damage Over Time Interval: 1 sec\n" +
                "Damage Over Time Duration: 6 sec\n" +
                "Mob Count: 8\n" +
                "Damage Over Time Type: Burn",
                description);
        }
    }
}
