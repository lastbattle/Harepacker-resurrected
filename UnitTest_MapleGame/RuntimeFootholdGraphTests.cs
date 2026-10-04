using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Physics;

namespace UnitTest_MapleGame;

public sealed class RuntimeFootholdGraphTests
{
    [Fact]
    public void EndpointIdsPreserveSharedAndDisconnectedCoincidentAnchors()
    {
        RuntimeFootholdDefinition[] definitions =
        {
            Definition(7, 0, 8, 0, 100, 100, 100, 10, 11),
            Definition(8, 7, 0, 100, 100, 200, 100, 11, 12),
            Definition(9, 0, 0, 0, 100, 100, 100, 20, 21)
        };

        RuntimeFootholdGraph graph = new(definitions);
        RuntimeFoothold first = graph.Footholds[0];
        RuntimeFoothold connected = graph.Footholds[1];
        RuntimeFoothold disconnected = graph.Footholds[2];

        Assert.Same(first.SecondAnchor, connected.FirstAnchor);
        Assert.Contains(connected, first.SecondAnchor.ConnectedLines);
        Assert.NotSame(first.FirstAnchor, disconnected.FirstAnchor);
        Assert.Equal(5, graph.Anchors.Count);
    }

    [Fact]
    public void DuplicateSourceNumbersRemainDistinctRuntimeFootholds()
    {
        RuntimeFootholdGraph graph = new(new[]
        {
            Definition(0, 0, 0, 0, 100, 50, 100, 1, 2),
            Definition(0, 0, 0, 100, 100, 150, 100, 3, 4)
        });

        Assert.Equal(2, graph.Footholds.Count);
        Assert.NotEqual(graph.Footholds[0].Number, graph.Footholds[1].Number);
        Assert.Equal(0, graph.Footholds[0].Number);
    }

    private static RuntimeFootholdDefinition Definition(
        int number,
        int previous,
        int next,
        int x1,
        int y1,
        int x2,
        int y2,
        int firstEndpoint,
        int secondEndpoint) => new(
            number,
            previous,
            next,
            x1,
            y1,
            x2,
            y2,
            0,
            0,
            null,
            null,
            default,
            default,
            firstEndpoint,
            secondEndpoint);
}
