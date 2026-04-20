using HaCreator.MapSimulator;

namespace UnitTest_MapSimulator;

public class ItemUpgradeOwnerParityTests
{
    [Fact]
    public void ItemUpgradeResultAckPayload_RoundTripsReturnResultAndOutcome()
    {
        byte[] payload = MapSimulator.BuildItemUpgradeResultAckPayloadForTests(returnResultCode: 61, resultValue: 0);

        bool decoded = MapSimulator.TryDecodeItemUpgradeResultAckPayloadForTests(
            payload,
            out int decodedReturnResultCode,
            out int decodedResultValue);

        Assert.True(decoded);
        Assert.Equal(61, decodedReturnResultCode);
        Assert.Equal(0, decodedResultValue);
    }

    [Fact]
    public void ItemUpgradeResultAckPayload_RejectsShortPayload()
    {
        bool decoded = MapSimulator.TryDecodeItemUpgradeResultAckPayloadForTests(
            new byte[7],
            out _,
            out _);

        Assert.False(decoded);
    }

    [Fact]
    public void ItemUpgradeResultAckDelay_UsesViciousHammerClientDelayFor61AndZeroResult()
    {
        int delay = MapSimulator.ResolveItemUpgradeResultAckDispatchDelayMsForTests(returnResultCode: 61, resultValue: 0);

        Assert.Equal(1000, delay);
    }

    [Fact]
    public void ItemUpgradeResultAckDelay_IsImmediateForOtherResultShapes()
    {
        Assert.Equal(0, MapSimulator.ResolveItemUpgradeResultAckDispatchDelayMsForTests(returnResultCode: 61, resultValue: 1));
        Assert.Equal(0, MapSimulator.ResolveItemUpgradeResultAckDispatchDelayMsForTests(returnResultCode: 0, resultValue: 0));
    }
}
