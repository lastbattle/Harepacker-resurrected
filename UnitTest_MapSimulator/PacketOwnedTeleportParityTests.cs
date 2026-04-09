using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedTeleportParityTests
{
    [Fact]
    public void TryResolvePacketOwnedSyntheticSourcePortalName_UsesExplicitTargetPortalNameWhenUnique()
    {
        PortalInstance[] currentFieldPortals =
        {
            CreatePortal("sp", "targetA", 200000000),
            CreatePortal("other", "targetB", 200000000)
        };

        bool resolved = MapSimulator.TryResolvePacketOwnedSyntheticSourcePortalName(
            currentFieldPortals,
            200000000,
            "targetA",
            Array.Empty<string>(),
            out string sourcePortalName);

        Assert.True(resolved);
        Assert.Equal("sp", sourcePortalName);
    }

    [Fact]
    public void TryResolvePacketOwnedSyntheticSourcePortalName_CollapsesCandidateNamesBackToOneSourcePortal()
    {
        PortalInstance[] currentFieldPortals =
        {
            CreatePortal("sp", "targetA", 200000000),
            CreatePortal("sp", "targetB", 200000000)
        };

        bool resolved = MapSimulator.TryResolvePacketOwnedSyntheticSourcePortalName(
            currentFieldPortals,
            200000000,
            null,
            new[] { "targetA", "targetB" },
            out string sourcePortalName);

        Assert.True(resolved);
        Assert.Equal("sp", sourcePortalName);
    }

    [Fact]
    public void TryResolvePacketOwnedSyntheticSourcePortalName_RejectsAmbiguousCandidateCollapse()
    {
        PortalInstance[] currentFieldPortals =
        {
            CreatePortal("spA", "targetA", 200000000),
            CreatePortal("spB", "targetB", 200000000)
        };

        bool resolved = MapSimulator.TryResolvePacketOwnedSyntheticSourcePortalName(
            currentFieldPortals,
            200000000,
            null,
            new[] { "targetA", "targetB" },
            out string sourcePortalName);

        Assert.False(resolved);
        Assert.Null(sourcePortalName);
    }

    [Fact]
    public void TryResolvePacketOwnedSyntheticSourcePortalName_FallsBackToUniqueMapOnlyReturn()
    {
        PortalInstance[] currentFieldPortals =
        {
            CreatePortal("sp", null, 200000000)
        };

        bool resolved = MapSimulator.TryResolvePacketOwnedSyntheticSourcePortalName(
            currentFieldPortals,
            200000000,
            null,
            Array.Empty<string>(),
            out string sourcePortalName);

        Assert.True(resolved);
        Assert.Equal("sp", sourcePortalName);
    }

    private static PortalInstance CreatePortal(string portalName, string targetPortalName, int targetMapId)
    {
        return new PortalInstance(
            baseInfo: null!,
            board: null!,
            x: 0,
            y: 0,
            pn: portalName,
            pt: default,
            tn: targetPortalName,
            tm: targetMapId,
            script: null,
            delay: null,
            hideTooltip: default,
            onlyOnce: default,
            horizontalImpact: null,
            verticalImpact: null,
            image: null,
            hRange: null,
            vRange: null);
    }
}
