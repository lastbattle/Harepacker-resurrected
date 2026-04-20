using System.Net;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class GuildBossParityTests
{
    [Fact]
    public void ComputePulleyHitAnimationDurationMs_UsesFallbackWhenFramesMissing()
    {
        Assert.Equal(270, GuildBossField.ComputePulleyHitAnimationDurationMs(null, 270));
        Assert.Equal(270, GuildBossField.ComputePulleyHitAnimationDurationMs(new[] { 0, -1, 0 }, 270));
    }

    [Fact]
    public void TryQueuePulleyRequest_PassiveAttachRejectsDeferredQueueing()
    {
        using var bridge = new GuildBossOfficialSessionBridgeManager();
        var passiveCandidate = new GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate(
            ProcessId: 1234,
            ProcessName: "MapleStory",
            LocalEndpoint: new IPEndPoint(IPAddress.Loopback, 51000),
            RemoteEndpoint: new IPEndPoint(IPAddress.Loopback, 8484));

        Assert.True(bridge.TryAttachEstablishedSession(passiveCandidate, out _));
        bool queued = bridge.TryQueuePulleyRequest(new GuildBossField.PulleyPacketRequest(10, 1), out string queueStatus);

        Assert.False(queued);
        Assert.Contains("cannot queue opcode 259", queueStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryQueuePulleyRequest_AttachProxyAllowsQueueingBeforeReconnect()
    {
        using var bridge = new GuildBossOfficialSessionBridgeManager();
        var passiveCandidate = new GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate(
            ProcessId: 1234,
            ProcessName: "MapleStory",
            LocalEndpoint: new IPEndPoint(IPAddress.Loopback, 51001),
            RemoteEndpoint: new IPEndPoint(IPAddress.Loopback, 8484));

        Assert.True(bridge.TryAttachEstablishedSessionAndStartProxy(0, passiveCandidate, out _));
        bool queued = bridge.TryQueuePulleyRequest(new GuildBossField.PulleyPacketRequest(20, 2), out string queueStatus);

        Assert.True(queued);
        Assert.Contains("proxied reconnect handshake", queueStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseProxyListenPort_AcceptsAutoPortZero()
    {
        Assert.True(GuildBossSessionCommandParsing.TryParseProxyListenPort("0", out int listenPort));
        Assert.Equal(0, listenPort);
    }
}
