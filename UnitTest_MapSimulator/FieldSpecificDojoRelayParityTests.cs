using System;
using System.Buffers.Binary;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator;

public class FieldSpecificDojoRelayParityTests
{
    [Fact]
    public void TryDecodeDojoFieldSpecificRelayPayload_DecodesDirectFieldSpecificPrefix()
    {
        byte[] dojoClockPayload = BuildDojoClockPayload(durationSec: 45);
        byte[] fieldSpecificPayload = BuildRelayPayload(DojoField.PacketTypeClock, dojoClockPayload);

        bool decoded = MapSimulator.TryDecodeDojoFieldSpecificRelayPayload(
            fieldSpecificPayload,
            out int packetType,
            out byte[] packetPayload,
            out string summary);

        Assert.True(decoded);
        Assert.Equal(DojoField.PacketTypeClock, packetType);
        Assert.Equal(dojoClockPayload, packetPayload);
        Assert.Contains("decoded packet 1", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDecodeDojoFieldSpecificRelayPayload_DecodesNestedFieldSpecificRelayPrefix()
    {
        byte[] dojoClockPayload = BuildDojoClockPayload(durationSec: 60);
        byte[] fieldSpecificPayload = BuildRelayPayload(DojoField.PacketTypeClock, dojoClockPayload);
        byte[] nestedRelayPayload = BuildRelayPayload(149, fieldSpecificPayload);

        bool decoded = MapSimulator.TryDecodeDojoFieldSpecificRelayPayload(
            nestedRelayPayload,
            out int packetType,
            out byte[] packetPayload,
            out string summary);

        Assert.True(decoded);
        Assert.Equal(DojoField.PacketTypeClock, packetType);
        Assert.Equal(dojoClockPayload, packetPayload);
        Assert.Contains("nested relay packet-id prefixes", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDecodeDojoFieldSpecificRelayPayload_DecodesNestedWrapperAndFieldSpecificPrefixes()
    {
        byte[] dojoClockPayload = BuildDojoClockPayload(durationSec: 75);
        byte[] fieldSpecificPayload = BuildRelayPayload(DojoField.PacketTypeClock, dojoClockPayload);
        byte[] fieldSpecificRelayPayload = BuildRelayPayload(149, fieldSpecificPayload);
        byte[] nestedRelayPayload = BuildRelayPayload(SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode, fieldSpecificRelayPayload);

        bool decoded = MapSimulator.TryDecodeDojoFieldSpecificRelayPayload(
            nestedRelayPayload,
            out int packetType,
            out byte[] packetPayload,
            out string summary);

        Assert.True(decoded);
        Assert.Equal(DojoField.PacketTypeClock, packetType);
        Assert.Equal(dojoClockPayload, packetPayload);
        Assert.Contains("nested relay packet-id prefixes", summary, StringComparison.OrdinalIgnoreCase);
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
}
