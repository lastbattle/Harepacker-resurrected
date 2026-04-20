using System.Net;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public class GuildBossParityTests
{
    [Fact]
    public void ComputePulleyHitAnimationDurationMs_UsesFallbackWhenNoPositiveDelayExists()
    {
        int duration = GuildBossField.ComputePulleyHitAnimationDurationMs(
            new[] { 0, -90, 0 },
            fallbackDurationMs: 270);

        Assert.Equal(270, duration);
    }

    [Fact]
    public void ComputePulleyHitAnimationDurationMs_SumsPositiveDelaysOnly()
    {
        int duration = GuildBossField.ComputePulleyHitAnimationDurationMs(
            new[] { 90, -1, 90, 0, 90 },
            fallbackDurationMs: 270);

        Assert.Equal(270, duration);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("65535")]
    public void TryParseProxyListenPort_AcceptsZeroAndValidPortRange(string value)
    {
        bool parsed = GuildBossSessionCommandParsing.TryParseProxyListenPort(value, out int listenPort);

        Assert.True(parsed);
        Assert.InRange(listenPort, 0, ushort.MaxValue);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void TryParseProxyListenPort_RejectsNegativeOverflowAndNonNumeric(string value)
    {
        bool parsed = GuildBossSessionCommandParsing.TryParseProxyListenPort(value, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void PassiveEstablishedAttach_RejectsDeferredPulleyQueue()
    {
        using var manager = new GuildBossOfficialSessionBridgeManager();
        var candidate = BuildCandidate(localPort: 42000, remotePort: 8484);

        bool attached = manager.TryAttachEstablishedSession(candidate, out string attachStatus);
        bool queued = manager.TryQueuePulleyRequest(new GuildBossField.PulleyPacketRequest(100, 1), out string queueStatus);

        Assert.True(attached);
        Assert.Contains("cannot decrypt inbound 344/345 traffic or inject opcode 259", attachStatus);
        Assert.True(manager.HasPassiveEstablishedSocketPair);
        Assert.False(queued);
        Assert.Contains("cannot queue opcode 259", queueStatus);
    }

    [Fact]
    public void ReconnectProxyAttach_WithAutoListenPort_AllowsDeferredPulleyQueue()
    {
        using var manager = new GuildBossOfficialSessionBridgeManager();
        var candidate = BuildCandidate(localPort: 43000, remotePort: 8585);

        bool attached = manager.TryAttachEstablishedSessionAndStartProxy(listenPort: 0, candidate, out string attachStatus);
        bool queued = manager.TryQueuePulleyRequest(new GuildBossField.PulleyPacketRequest(200, 2), out string queueStatus);

        Assert.True(attached);
        Assert.Contains("Armed localhost proxy on 127.0.0.1:", attachStatus);
        Assert.True(manager.IsRunning);
        Assert.True(manager.HasPassiveEstablishedSocketPair);
        Assert.True(queued);
        Assert.Contains("proxied reconnect handshake", queueStatus);
    }

    [Fact]
    public void OwnershipAndPreviewGates_MatchGuildBossParityRules()
    {
        Assert.False(MapSimulator.HasGuildBossOfficialSessionBridgeOwnership(false, false, false));
        Assert.True(MapSimulator.HasGuildBossOfficialSessionBridgeOwnership(true, false, false));
        Assert.True(MapSimulator.HasGuildBossOfficialSessionBridgeOwnership(false, true, false));
        Assert.True(MapSimulator.HasGuildBossOfficialSessionBridgeOwnership(false, false, true));

        Assert.True(MapSimulator.ShouldAllowLocalGuildBossPulleyPreview(false, false));
        Assert.False(MapSimulator.ShouldAllowLocalGuildBossPulleyPreview(true, false));
        Assert.False(MapSimulator.ShouldAllowLocalGuildBossPulleyPreview(false, true));

        Assert.True(MapSimulator.ShouldFallbackGuildBossPulleyTransport(false));
        Assert.False(MapSimulator.ShouldFallbackGuildBossPulleyTransport(true));
    }

    [Fact]
    public void MatchesRequestedTargetConfiguration_IgnoresListenPortWhenAutoSelectIsEnabled()
    {
        bool matches = GuildBossOfficialSessionBridgeManager.MatchesRequestedTargetConfiguration(
            currentListenPort: 18500,
            currentRemoteHost: "127.0.0.1",
            currentRemotePort: 8484,
            expectedListenPort: 18488,
            expectedRemoteHost: "localhost",
            expectedRemotePort: 8484,
            ignoreListenPort: true);

        Assert.True(matches);
    }

    private static GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate BuildCandidate(int localPort, int remotePort)
    {
        return new GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate(
            ProcessId: 1234,
            ProcessName: "MapleStory",
            LocalEndpoint: new IPEndPoint(IPAddress.Loopback, localPort),
            RemoteEndpoint: new IPEndPoint(IPAddress.Parse("203.0.113.10"), remotePort));
    }
}
