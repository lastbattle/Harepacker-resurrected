using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class TransportationParityTests
{
    [Fact]
    public void BuildFieldInitPayload_EncodesFieldIdAndShipKind()
    {
        byte[] payload = TransportationFieldInitRequestCodec.BuildFieldInitPayload(101000300, 0);

        Assert.Equal(5, payload.Length);
        Assert.Equal("7C80040500", Convert.ToHexString(payload));
    }

    [Fact]
    public void BuildRawFieldInitPacket_PrefixesOpcode264()
    {
        byte[] rawPacket = TransportationFieldInitRequestCodec.BuildRawFieldInitPacket(200090000, 1);

        Assert.Equal("0801D084EC0B01", Convert.ToHexString(rawPacket));
    }

    [Fact]
    public void QueueFieldInitRequest_PersistsDeferredOutboundPacket()
    {
        TransportationOfficialSessionBridgeManager bridge = new();

        bool queued = bridge.TryQueueFieldInitRequest(200110000, 1, out string status);

        Assert.True(queued, status);
        Assert.Equal(1, bridge.PendingPacketCount);
        Assert.Equal(1, bridge.QueuedCount);
        Assert.Equal(TransportationFieldInitRequestCodec.OutboundFieldInitOpcode, bridge.LastQueuedOpcode);
        Assert.Equal("080150E24A0C01", Convert.ToHexString(bridge.LastQueuedRawPacket));
    }

    [Fact]
    public void QueueFieldInitRequest_RejectsUnsupportedShipKind()
    {
        TransportationOfficialSessionBridgeManager bridge = new();

        bool queued = bridge.TryQueueFieldInitRequest(101000300, 2, out string status);

        Assert.False(queued);
        Assert.Contains("ship kinds 0 and 1", status);
        Assert.Equal(0, bridge.PendingPacketCount);
    }
}
