using System;
using System.Net;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public class RockPaperScissorsParityTests
{
    [Fact]
    public void OfficialSessionBridge_ProxyArmedPassiveAttachWithAutoListenPort_QueuesDeferredOpcode160()
    {
        using RockPaperScissorsOfficialSessionBridgeManager bridge = new();
        var candidate = new RockPaperScissorsOfficialSessionBridgeManager.SessionDiscoveryCandidate(
            ProcessId: 1234,
            ProcessName: "MapleStory",
            LocalEndpoint: new IPEndPoint(IPAddress.Loopback, 55123),
            RemoteEndpoint: new IPEndPoint(IPAddress.Parse("203.0.113.45"), 8484));

        bool attached = bridge.TryAttachEstablishedSessionAndStartProxy(listenPort: 0, candidate, out string attachStatus);

        Assert.True(attached);
        Assert.True(bridge.IsRunning);
        Assert.True(bridge.HasPassiveEstablishedSocketPair);
        Assert.True(bridge.ListenPort > 0);
        Assert.Contains("Opcode 160 requests will queue", attachStatus, StringComparison.Ordinal);

        var packet = new RockPaperScissorsClientPacket(
            RockPaperScissorsField.ClientOpcode,
            RockPaperScissorsClientRequestType.Start,
            RockPaperScissorsChoice.None,
            Array.Empty<byte>(),
            "opcode=160 subtype=0");

        bool sentOrQueued = bridge.TrySendOrQueueClientPacket(packet, out bool queued, out string queueStatus);

        Assert.True(sentOrQueued);
        Assert.True(queued);
        Assert.Equal(1, bridge.QueuedCount);
        Assert.Contains($"127.0.0.1:{bridge.ListenPort}", queueStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void TryBuildOutboundTrace_ValidStartAndSelectPackets_ParseExpectedShape()
    {
        var startPacket = new RockPaperScissorsClientPacket(
            RockPaperScissorsField.ClientOpcode,
            RockPaperScissorsClientRequestType.Start,
            RockPaperScissorsChoice.None,
            Array.Empty<byte>(),
            "opcode=160 subtype=0");
        byte[] startRaw = RockPaperScissorsClientPacketTransportManager.BuildRawPacket(startPacket);

        bool startParsed = RockPaperScissorsOfficialSessionBridgeManager.TryBuildOutboundTrace(
            startRaw,
            "official-session:test-client",
            out RockPaperScissorsOfficialSessionBridgeManager.OutboundPacketTrace startTrace);

        Assert.True(startParsed);
        Assert.Equal(RockPaperScissorsField.ClientOpcode, startTrace.Opcode);
        Assert.Equal(RockPaperScissorsClientRequestType.Start, startTrace.RequestType);
        Assert.Equal(RockPaperScissorsChoice.None, startTrace.Choice);
        Assert.Equal(0, startTrace.PayloadLength);

        var selectPacket = new RockPaperScissorsClientPacket(
            RockPaperScissorsField.ClientOpcode,
            RockPaperScissorsClientRequestType.Select,
            RockPaperScissorsChoice.Scissor,
            new[] { (byte)RockPaperScissorsChoice.Scissor },
            "opcode=160 subtype=1 choice=scissor");
        byte[] selectRaw = RockPaperScissorsClientPacketTransportManager.BuildRawPacket(selectPacket);

        bool selectParsed = RockPaperScissorsOfficialSessionBridgeManager.TryBuildOutboundTrace(
            selectRaw,
            "official-session:test-client",
            out RockPaperScissorsOfficialSessionBridgeManager.OutboundPacketTrace selectTrace);

        Assert.True(selectParsed);
        Assert.Equal(RockPaperScissorsField.ClientOpcode, selectTrace.Opcode);
        Assert.Equal(RockPaperScissorsClientRequestType.Select, selectTrace.RequestType);
        Assert.Equal(RockPaperScissorsChoice.Scissor, selectTrace.Choice);
        Assert.Equal(1, selectTrace.PayloadLength);
    }

    [Fact]
    public void TryBuildOutboundTrace_RejectsMalformedSelectPayloadShapes()
    {
        byte[] missingSelectPayload =
        [
            0xA0, 0x00,
            (byte)RockPaperScissorsClientRequestType.Select
        ];
        byte[] oversizedSelectPayload =
        [
            0xA0, 0x00,
            (byte)RockPaperScissorsClientRequestType.Select,
            0x00, 0x01
        ];
        byte[] invalidSelectChoicePayload =
        [
            0xA0, 0x00,
            (byte)RockPaperScissorsClientRequestType.Select,
            0x03
        ];

        Assert.False(RockPaperScissorsOfficialSessionBridgeManager.TryBuildOutboundTrace(
            missingSelectPayload,
            "official-session:test-client",
            out _));
        Assert.False(RockPaperScissorsOfficialSessionBridgeManager.TryBuildOutboundTrace(
            oversizedSelectPayload,
            "official-session:test-client",
            out _));
        Assert.False(RockPaperScissorsOfficialSessionBridgeManager.TryBuildOutboundTrace(
            invalidSelectChoicePayload,
            "official-session:test-client",
            out _));
    }

    [Fact]
    public void TryBuildOutboundTrace_RejectsNonSelectPacketsWithPayload()
    {
        byte[] malformedStartPayload =
        [
            0xA0, 0x00,
            (byte)RockPaperScissorsClientRequestType.Start,
            0xFF
        ];

        Assert.False(RockPaperScissorsOfficialSessionBridgeManager.TryBuildOutboundTrace(
            malformedStartPayload,
            "official-session:test-client",
            out _));
    }
}
