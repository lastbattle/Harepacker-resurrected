using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzProperties;
using System.Linq;

namespace UnitTest_MapSimulator
{
    public sealed class InventoryItemMetadataResolverTests
    {
        [Fact]
        public void BuildConsumeItemRequirementLinesForTests_SupportsSimpleNumericEntries()
        {
            WzSubProperty consumeItem = new("consumeItem");
            consumeItem.AddProperty(new WzIntProperty("0", 4031838));
            consumeItem.AddProperty(new WzStringProperty("1", "4031839"));

            IReadOnlyList<string> lines = InventoryItemMetadataResolver.BuildConsumeItemRequirementLinesForTests(consumeItem);

            Assert.Equal(2, lines.Count);
            Assert.Equal("Consumes: Item #4031838", lines[0]);
            Assert.Equal("Consumes: Item #4031839", lines[1]);
        }

        [Fact]
        public void BuildInfoMetadataLinesForTests_IncludesCreateAndCreatePeriod()
        {
            WzSubProperty info = new("info");
            info.AddProperty(new WzIntProperty("create", 4032606));
            info.AddProperty(new WzIntProperty("createPeriod", 30));

            IReadOnlyList<string> lines = InventoryItemMetadataResolver.BuildInfoMetadataLinesForTests(0, info);

            Assert.Contains("Creates: Item #4032606", lines);
            Assert.Contains("Created item expires after 30 days", lines);
        }

        [Fact]
        public void BuildInfoMetadataLinesForTests_IncludesReplaceItemAndDuration()
        {
            WzSubProperty info = new("info");
            WzSubProperty replace = new("replace");
            replace.AddProperty(new WzIntProperty("itemid", 2022658));
            replace.AddProperty(new WzIntProperty("period", 1440));
            info.AddProperty(replace);

            IReadOnlyList<string> lines = InventoryItemMetadataResolver.BuildInfoMetadataLinesForTests(0, info);

            Assert.Contains("Replaces with: Item #2022658", lines);
            Assert.Contains("Replacement lasts 1 day", lines);
        }

        [Fact]
        public void BuildConsumeItemRequirementLinesForTests_PreservesStructuredCountAndRate()
        {
            WzSubProperty consumeItem = new("consumeItem");
            WzSubProperty entry = new("0");
            entry.AddProperty(new WzIntProperty("itemcode", 4000000));
            entry.AddProperty(new WzIntProperty("count", 3));
            entry.AddProperty(new WzIntProperty("rate", 75));
            consumeItem.AddProperty(entry);

            IReadOnlyList<string> lines = InventoryItemMetadataResolver.BuildConsumeItemRequirementLinesForTests(consumeItem);

            Assert.Single(lines);
            Assert.Equal("Consumes: Item #4000000 x3 (75%)", lines.Single());
        }
    }
}
