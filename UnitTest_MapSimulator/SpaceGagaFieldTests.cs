using System;
using System.Buffers.Binary;
using HaCreator.MapSimulator.Effects;

namespace UnitTest_MapSimulator;

public class SpaceGagaFieldTests
{
    [Fact]
    public void TryApplyPacket_Type2Clock_ReportsBothClientStringPoolMappings()
    {
        SpaceGagaField field = new();
        field.Enable(922240000);

        byte[] payload = CreateClockPayload(clockType: 2, durationSeconds: 90);
        int currentTick = Environment.TickCount;

        bool applied = field.TryApplyPacket(SpaceGagaField.PacketTypeClock, payload, currentTick, out string errorMessage);

        Assert.True(applied);
        Assert.Null(errorMessage);
        Assert.Equal(2, field.LastDecodedClockType);
        Assert.Equal(90, field.LastDecodedClockDurationSec);
        Assert.Equal(90, field.DurationSeconds);

        string status = field.DescribeStatus();
        Assert.Contains("rawClock=2:90s", status);
        Assert.Contains("source=Map/Obj/etc.img/space/backgrnd", status);
        Assert.Contains("font=Map/Obj/etc.img/space/fontTime", status);
        Assert.Contains("StringPool 0x140D->Map/Obj/etc.img/space/backgrnd", status);
        Assert.Contains("0x140E->Map/Obj/etc.img/space/fontTime", status);
    }

    [Fact]
    public void TryApplyPacket_NonType2Clock_DoesNotStartTimerboard()
    {
        SpaceGagaField field = new();
        field.Enable(922240100);

        byte[] payload = CreateClockPayload(clockType: 1, durationSeconds: 45);

        bool applied = field.TryApplyPacket(SpaceGagaField.PacketTypeClock, payload, Environment.TickCount, out string errorMessage);

        Assert.True(applied);
        Assert.Null(errorMessage);
        Assert.Equal(1, field.LastDecodedClockType);
        Assert.Equal(45, field.LastDecodedClockDurationSec);
        Assert.Equal(0, field.DurationSeconds);
        Assert.Equal(0, field.RemainingSeconds);

        string status = field.DescribeStatus();
        Assert.Contains("timer=stopped", status);
        Assert.Contains("duration=0s", status);
        Assert.Contains("rawClock=1:45s", status);
    }

    [Fact]
    public void Reset_ClearsDecodedClockAndDisablesField()
    {
        SpaceGagaField field = new();
        field.Enable(922240200);
        field.TryApplyPacket(
            SpaceGagaField.PacketTypeClock,
            CreateClockPayload(clockType: 2, durationSeconds: 15),
            Environment.TickCount,
            out _);

        field.Reset();

        Assert.False(field.IsActive);
        Assert.Equal(0, field.MapId);
        Assert.Equal(0, field.DurationSeconds);
        Assert.Equal(-1, field.LastDecodedClockType);
        Assert.Equal(-1, field.LastDecodedClockDurationSec);
        Assert.Equal("SpaceGAGA timerboard inactive", field.DescribeStatus());
    }

    private static byte[] CreateClockPayload(byte clockType, int durationSeconds)
    {
        byte[] payload = new byte[5];
        payload[0] = clockType;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1), durationSeconds);
        return payload;
    }
}
