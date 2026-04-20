using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Xunit;

namespace UnitTest_MapSimulator;

public class InventoryItemMetadataResolverParityTests
{
    [Fact]
    public void BuildEffectLinesWithSpecExForTests_IncludesOtherPartyOwnershipLine()
    {
        WzSubProperty specEx = Node("specEx", Int("otherParty", 1));

        var lines = InventoryItemMetadataResolver.BuildEffectLinesWithSpecExForTests(
            infoProperty: null,
            specProperty: null,
            specExProperty: specEx);

        Assert.Contains("Applies to other party members", lines);
    }

    [Fact]
    public void BuildInfoMetadataLinesForTests_FormatsAndSortsPartyScaleRows()
    {
        WzSubProperty info = Node("info",
            Node("party",
                Int("6", 20),
                Int("2", 7),
                Int("4", 12),
                Int("5", 16),
                Int("1", 5),
                Int("3", 9),
                Int("7", 24)));

        var lines = InventoryItemMetadataResolver.BuildInfoMetadataLinesForTests(itemId: 0, infoProperty: info);

        Assert.Contains("Party scale rows: 1=5, 2=7, 3=9, 4=12, 5=16, 6=20 (+1 more)", lines);
    }

    private static WzSubProperty Node(string name, params WzImageProperty[] children)
    {
        WzSubProperty node = new(name);
        foreach (WzImageProperty child in children)
        {
            node.AddProperty(child);
        }

        return node;
    }

    private static WzIntProperty Int(string name, int value)
    {
        return new WzIntProperty(name, value);
    }
}
