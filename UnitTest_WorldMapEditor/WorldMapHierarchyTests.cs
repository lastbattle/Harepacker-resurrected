using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;

namespace UnitTest_WorldMapEditor;

public sealed class WorldMapHierarchyTests
{
    [Fact]
    public void Index_ResolvesParentChildrenLinksAndMapPlacements()
    {
        WorldMapDocument root = WorldMapCodec.Read(WorldMapFixtureFactory.CreateSurface());
        WorldMapDocument child = WorldMapCodec.Read(WorldMapFixtureFactory.CreateChildSurface());
        var hierarchy = new WorldMapHierarchyIndex(new[] { root, child });

        Assert.Single(hierarchy.Roots);
        Assert.Same(root, hierarchy.Find(WorldMapFixtureFactory.RootName));
        Assert.Same(child, hierarchy.Find("WorldMapFixtureChild.img"));
        Assert.Single(hierarchy.GetChildren(WorldMapFixtureFactory.RootName));
        Assert.Same(child, hierarchy.GetChildren(WorldMapFixtureFactory.RootName)[0]);
        Assert.Contains(root, hierarchy.FindByMapId(WorldMapFixtureFactory.FirstMapId));

        IReadOnlyList<WorldMapHierarchyReference> inbound =
            hierarchy.GetInboundReferences(WorldMapFixtureFactory.ChildName);
        Assert.Single(inbound);
        Assert.Contains(inbound, reference => reference.Kind == "MapLink" && reference.SourceName == WorldMapFixtureFactory.RootName);
        IReadOnlyList<WorldMapHierarchyReference> rootInbound =
            hierarchy.GetInboundReferences(WorldMapFixtureFactory.RootName);
        Assert.Single(rootInbound);
        Assert.Contains(rootInbound, reference => reference.Kind == "parentMap" && reference.SourceName == WorldMapFixtureFactory.ChildName);
        Assert.False(hierarchy.HasCycles);
    }

    [Fact]
    public void Index_DetectsParentCyclesWithoutDroppingEitherDocument()
    {
        WorldMapDocument first = WorldMapCodec.Read(
            WorldMapFixtureFactory.CreateSurface("WorldMapA.img", "A", "B", includeLink: false, includeFog: false));
        WorldMapDocument second = WorldMapCodec.Read(
            WorldMapFixtureFactory.CreateSurface("WorldMapB.img", "B", "A", includeLink: false, includeFog: false));
        var hierarchy = new WorldMapHierarchyIndex(new[] { first, second });

        Assert.True(hierarchy.HasCycles);
        Assert.Contains("A", hierarchy.GetCycleMembers());
        Assert.Contains("B", hierarchy.GetCycleMembers());
        Assert.Equal(2, hierarchy.Documents.Count);
    }

    [Fact]
    public void Index_DuplicateLogicalNamesHaveDeterministicFirstLookup()
    {
        WorldMapDocument first = WorldMapCodec.Read(
            WorldMapFixtureFactory.CreateSurface("WorldMapDuplicateA.img", "Duplicate", includeLink: false, includeFog: false));
        WorldMapDocument second = WorldMapCodec.Read(
            WorldMapFixtureFactory.CreateSurface("WorldMapDuplicateB.img", "Duplicate", includeLink: false, includeFog: false));
        var hierarchy = new WorldMapHierarchyIndex(new[] { first, second });

        Assert.Equal(2, hierarchy.Documents.Count);
        Assert.True(hierarchy.TryGetByLogicalName("Duplicate", out WorldMapDocument resolved));
        Assert.Same(first, resolved);
        Assert.Single(hierarchy.ByLogicalName);
    }
}
