using HaCreator.MapSimulator;
using MapleLib.WzLib.WzStructure;

namespace UnitTest_MapSimulator;

public sealed class TransportationFieldDefinitionResolverTests
{
    [Fact]
    public void OrdinaryMapWithoutShip_DoesNotCreateTransportationField()
    {
        var mapInfo = new MapInfo();

        bool resolved = MapSimulator.TryResolveTransportationFieldDefinition(
            mapInfo,
            shipObject: null,
            out var definition);

        Assert.False(resolved);
        Assert.Null(definition);
    }
}
