using HaCreator.MapSimulator.Effects;

namespace UnitTest_MapSimulator;

public sealed class DojoFieldTests
{
    [Fact]
    public void TryApplyPacket_TypeOneClockPayloadDecodesWithoutStartingTimerboard()
    {
        DojoField field = CreateField();

        bool applied = field.TryApplyPacket(
            packetType: 1,
            payload: CreateClockPayload(clockType: 1, durationSec: 180),
            currentTimeMs: 1000,
            out string errorMessage);

        Assert.True(applied, errorMessage);
        Assert.Null(errorMessage);
        Assert.Equal(1, field.LastDecodedClockType);
        Assert.Equal(180, field.LastDecodedClockDurationSec);
        Assert.Equal(0, field.RemainingSeconds);
        Assert.Contains("rawClock=1:180s", field.DescribeStatus());
    }

    [Fact]
    public void ShowClearResult_StopsLiveClockBeforeTransfer()
    {
        DojoField field = CreateField();
        field.OnClock(clockType: 2, durationSec: 120, currentTimeMs: 1000);

        field.ShowClearResult(currentTimeMs: 2000, nextMapId: 925020200);

        Assert.Equal(0, field.RemainingSeconds);
        Assert.Equal(925020200, field.PendingTransferMapId);
        Assert.Contains("pendingReturn=925020200", field.DescribeStatus());
    }

    [Fact]
    public void SetStage_ClearsPendingResultTransfer()
    {
        DojoField field = CreateField();
        field.ShowClearResult(currentTimeMs: 1000, nextMapId: 925020200);

        field.SetStage(stage: 3, currentTimeMs: 2000);

        Assert.Equal(-1, field.PendingTransferMapId);
        Assert.DoesNotContain("pendingReturn=", field.DescribeStatus());
    }

    [Fact]
    public void TypeTwoClockPacket_ClearsPriorTimeOverResultAndPendingTransfer()
    {
        DojoField field = CreateField();
        field.ShowTimeOverResult(currentTimeMs: 1000, exitMapId: 925020100);

        bool applied = field.TryApplyPacket(
            packetType: 1,
            payload: CreateClockPayload(clockType: 2, durationSec: 90),
            currentTimeMs: 2000,
            out string errorMessage);

        Assert.True(applied, errorMessage);
        Assert.Null(errorMessage);
        Assert.Equal(-1, field.PendingTransferMapId);
        Assert.Equal(2, field.LastDecodedClockType);
        Assert.Equal(90, field.LastDecodedClockDurationSec);
        Assert.DoesNotContain("pendingReturn=", field.DescribeStatus());
    }

    private static DojoField CreateField()
    {
        DojoField field = new();
        field.Enable(925020100);
        return field;
    }

    private static byte[] CreateClockPayload(byte clockType, int durationSec)
    {
        byte[] payload = new byte[5];
        payload[0] = clockType;
        BitConverter.GetBytes(durationSec).CopyTo(payload, 1);
        return payload;
    }
}
