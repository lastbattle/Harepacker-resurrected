using HaCreator.MapSimulator.Effects;

namespace UnitTest_MapSimulator;

public class SpaceGagaFieldTests
{
    [Fact]
    public void TryApplyPacket_Type2Clock_UpdatesTimerAndCapturesRawClock()
    {
        SpaceGagaField field = new();
        field.Enable(922240000);

        byte[] payload = BuildClockPayload(clockType: 2, durationSeconds: 91);

        bool applied = field.TryApplyPacket(SpaceGagaField.PacketTypeClock, payload, currentTimeMs: 1_000, out string? errorMessage);

        Assert.True(applied);
        Assert.Null(errorMessage);
        Assert.Equal(91, field.DurationSeconds);
        Assert.Equal(2, field.LastDecodedClockType);
        Assert.Equal(91, field.LastDecodedClockDurationSec);
        Assert.InRange(field.RemainingSeconds, 90, 91);
    }

    [Fact]
    public void TryApplyPacket_NonType2Clock_DoesNotStartTimer()
    {
        SpaceGagaField field = new();
        field.Enable(922240100);

        bool applied = field.TryApplyPacket(SpaceGagaField.PacketTypeClock, BuildClockPayload(clockType: 1, durationSeconds: 45), currentTimeMs: 2_000, out string? errorMessage);

        Assert.True(applied);
        Assert.Null(errorMessage);
        Assert.Equal(0, field.DurationSeconds);
        Assert.Equal(1, field.LastDecodedClockType);
        Assert.Equal(45, field.LastDecodedClockDurationSec);
        Assert.Equal(0, field.RemainingSeconds);
    }

    [Fact]
    public void Reset_ClearsRuntimeAndRawClockState()
    {
        SpaceGagaField field = new();
        field.Enable(922240200);
        field.OnClock(clockType: 2, durationSec: 30, currentTimeMs: 500);

        field.Reset();

        Assert.False(field.IsActive);
        Assert.Equal(0, field.MapId);
        Assert.Equal(0, field.DurationSeconds);
        Assert.Equal(-1, field.LastDecodedClockType);
        Assert.Equal(-1, field.LastDecodedClockDurationSec);
        Assert.Equal("SpaceGAGA timerboard inactive", field.DescribeStatus());
    }

    [Fact]
    public void DescribeStatus_ReportsRecoveredStringPoolEvidence()
    {
        SpaceGagaField field = new();
        field.Enable(922240000);
        field.TryApplyPacket(SpaceGagaField.PacketTypeClock, BuildClockPayload(clockType: 2, durationSeconds: 90), currentTimeMs: 0, out _);

        string status = field.DescribeStatus();

        Assert.Contains("rawClock=2:90s", status);
        Assert.Contains("StringPool 0x140D seed=0x44", status);
        Assert.Contains("raw=44 C3 06 CD 1C 7C 6C 18 C2 08 93 3B 4A 0F 27 50 31 FD 17 DC 50 56 21 10 8C 0E 8C 3F 16 08 2E", status);
        Assert.Contains("decoded=Map/Obj/etc.img/space/backgrnd", status);
        Assert.Contains("StringPool::ms_aString[0x140D] / CTimerboard_SpaceGAGA::OnCreate", status);
        Assert.Contains("StringPool 0x140E seed=0x45", status);
        Assert.Contains("raw=45 51 AE 0A 49 29 7E 8F F5 BE BA D3 E6 A5 F9 09 12 6F BF 1B 05 03 33 83 B5 B5 BA E4 A1 A1 F1", status);
        Assert.Contains("decoded=Map/Obj/etc.img/space/fontTime", status);
        Assert.Contains("StringPool::ms_aString[0x140E] / CTimerboard_SpaceGAGA::GetFontTime", status);
    }

    private static byte[] BuildClockPayload(byte clockType, int durationSeconds)
    {
        byte[] payload = new byte[5];
        payload[0] = clockType;
        BitConverter.GetBytes(durationSeconds).CopyTo(payload, 1);
        return payload;
    }
}
