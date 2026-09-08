using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;

namespace UnitTest_MapSimulator;

public class FieldObjectTagStateDefaultsLoaderTests
{
    [Fact]
    public void Load_ParsesContainerBasedTaggedObjectVisibility()
    {
        var visibility = new WzSubProperty("pulbicTaggedObjectVisible");
        visibility.AddProperty(new WzIntProperty("normal", 1));

        var mapInfo = new MapInfo();
        mapInfo.unsupportedInfoProperties.Add(visibility);

        IReadOnlyDictionary<string, bool> states = FieldObjectTagStateDefaultsLoader.Load(mapInfo);

        Assert.Single(states);
        Assert.True(states["normal"]);
    }
}
