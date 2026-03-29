using System.Net;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class MemoryGameOfficialSessionBridgeManagerTests
{
    [Fact]
    public void TryResolveDiscoveryCandidate_RejectsMultipleMatchesWithoutLocalPortFilter()
    {
        IReadOnlyList<MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
        [
            CreateCandidate(4000, 8484),
            CreateCandidate(4001, 8484)
        ];

        bool resolved = MemoryGameOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(
            candidates,
            remotePort: 8484,
            owningProcessId: null,
            owningProcessName: "MapleStory",
            localPort: null,
            out _,
            out string status);

        Assert.False(resolved);
        Assert.Contains("multiple candidates", status);
        Assert.Contains("localPort filter", status);
    }

    [Fact]
    public void TryResolveDiscoveryCandidate_SelectsSingleMatchWhenLocalPortFilterIsProvided()
    {
        IReadOnlyList<MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
        [
            CreateCandidate(4000, 8484),
            CreateCandidate(4001, 8484)
        ];

        bool resolved = MemoryGameOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(
            candidates,
            remotePort: 8484,
            owningProcessId: null,
            owningProcessName: "MapleStory",
            localPort: 4001,
            out MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
            out string status);

        Assert.True(resolved);
        Assert.Equal(4001, candidate.LocalEndpoint.Port);
        Assert.Null(status);
    }

    [Fact]
    public void DescribeDiscoveryCandidates_FiltersByLocalPort()
    {
        IReadOnlyList<MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
        [
            CreateCandidate(4000, 8484, processId: 120, processName: "MapleStory"),
            CreateCandidate(4001, 8484, processId: 120, processName: "MapleStory")
        ];

        string description = MemoryGameOfficialSessionBridgeManager.DescribeDiscoveryCandidates(
            candidates,
            remotePort: 8484,
            owningProcessId: null,
            owningProcessName: "MapleStory",
            localPort: 4001);

        Assert.Contains("local 127.0.0.1:4001 -> remote 192.168.0.2:8484", description);
        Assert.DoesNotContain("local 127.0.0.1:4000", description);
    }

    private static MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate CreateCandidate(
        int localPort,
        int remotePort,
        int processId = 100,
        string processName = "MapleStory")
    {
        return new MemoryGameOfficialSessionBridgeManager.SessionDiscoveryCandidate(
            processId,
            processName,
            new IPEndPoint(IPAddress.Loopback, localPort),
            new IPEndPoint(IPAddress.Parse("192.168.0.2"), remotePort));
    }
}
