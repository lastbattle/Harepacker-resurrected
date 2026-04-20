using System;
using System.Buffers.Binary;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public class DojoOfficialSessionBridgeNestedRelayParityTests
{
    [Fact]
    public void TryMapInboundPacket_SharedWrapperOpcodesDecodeWithoutMemoizedMapping()
    {
        using var bridge = new DojoOfficialSessionBridgeManager();
        byte[] dojoClockPayload = BuildDojoClockPayload(durationSec: 90);
        byte[] fieldSpecificPayload = BuildRelayPayload(DojoField.PacketTypeClock, dojoClockPayload);

        byte[] fieldSpecificRawPacket = BuildRawPacket(149, fieldSpecificPayload);
        bool fieldSpecificMapped = bridge.TryMapInboundPacket(
            fieldSpecificRawPacket,
            "test:opcode149",
            out DojoPacketInboxMessage fieldSpecificMessage);

        Assert.True(fieldSpecificMapped);
        Assert.NotNull(fieldSpecificMessage);
        Assert.Equal(DojoField.PacketTypeClock, fieldSpecificMessage.PacketType);
        Assert.Equal(dojoClockPayload, fieldSpecificMessage.Payload);
        Assert.Equal("none", bridge.DescribePacketMappings());
        Assert.Equal("none", bridge.DescribeLearnedPacketTable());

        byte[] currentWrapperRelayPayload = BuildRelayPayload(149, fieldSpecificPayload);
        byte[] currentWrapperRawPacket = BuildRawPacket(SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode, currentWrapperRelayPayload);
        bool currentWrapperMapped = bridge.TryMapInboundPacket(
            currentWrapperRawPacket,
            "test:opcode163",
            out DojoPacketInboxMessage currentWrapperMessage);

        Assert.True(currentWrapperMapped);
        Assert.NotNull(currentWrapperMessage);
        Assert.Equal(DojoField.PacketTypeClock, currentWrapperMessage.PacketType);
        Assert.Equal(dojoClockPayload, currentWrapperMessage.Payload);
        Assert.Equal("none", bridge.DescribePacketMappings());
        Assert.Equal("none", bridge.DescribeLearnedPacketTable());
    }

    [Fact]
    public void TryMapInboundPacket_NonWrapperNestedRelayMemoizesRecoveredOpcode()
    {
        using var bridge = new DojoOfficialSessionBridgeManager();
        byte[] dojoClockPayload = BuildDojoClockPayload(durationSec: 120);
        byte[] fieldSpecificPayload = BuildRelayPayload(DojoField.PacketTypeClock, dojoClockPayload);
        byte[] doubleRelayPayload = BuildRelayPayload(
            SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode,
            BuildRelayPayload(149, fieldSpecificPayload));
        byte[] rawPacket = BuildRawPacket(500, doubleRelayPayload);

        bool mapped = bridge.TryMapInboundPacket(rawPacket, "test:opcode500", out DojoPacketInboxMessage message);

        Assert.True(mapped);
        Assert.NotNull(message);
        Assert.Equal(DojoField.PacketTypeClock, message.PacketType);
        Assert.Equal(dojoClockPayload, message.Payload);
        Assert.Contains("500->clock", bridge.DescribePacketMappings(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("500->clock", bridge.DescribeLearnedPacketTable(), StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] BuildDojoClockPayload(int durationSec)
    {
        byte[] payload = new byte[5];
        payload[0] = 2;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1, sizeof(int)), durationSec);
        return payload;
    }

    private static byte[] BuildRelayPayload(int packetType, byte[] payload)
    {
        payload ??= Array.Empty<byte>();
        byte[] relayPayload = new byte[sizeof(ushort) + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(relayPayload.AsSpan(0, sizeof(ushort)), (ushort)packetType);
        if (payload.Length > 0)
        {
            Buffer.BlockCopy(payload, 0, relayPayload, sizeof(ushort), payload.Length);
        }

        return relayPayload;
    }

    private static byte[] BuildRawPacket(int opcode, byte[] payload)
    {
        payload ??= Array.Empty<byte>();
        byte[] rawPacket = new byte[sizeof(ushort) + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)), (ushort)opcode);
        if (payload.Length > 0)
        {
            Buffer.BlockCopy(payload, 0, rawPacket, sizeof(ushort), payload.Length);
        }

        return rawPacket;
    }
}
