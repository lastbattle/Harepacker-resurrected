using HaCreator.MapSimulator;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace UnitTest_MapSimulator;

public sealed class ItemUpgradeOwnerParityTests
{
    [Fact]
    public void ItemUpgradeOwnerRequestBlock_UsesClientCooldownBoundary()
    {
        Assert.True(MapSimulator.IsItemUpgradeOwnerRequestBlockedForTests(requestSent: true, lastRequestTick: 10, currentTick: 10));
        Assert.True(MapSimulator.IsItemUpgradeOwnerRequestBlockedForTests(requestSent: false, lastRequestTick: 1000, currentTick: 1499));
        Assert.False(MapSimulator.IsItemUpgradeOwnerRequestBlockedForTests(requestSent: false, lastRequestTick: 1000, currentTick: 1500));
    }

    [Fact]
    public void ItemUpgradeOwnerResultBusyArguments_MatchClientDefaults()
    {
        Assert.Equal(9, MapSimulator.ResolveItemUpgradeClientDuplicateRequestBusyResultValueForTests());
        Assert.Equal(-2, MapSimulator.ResolveItemUpgradeClientInitialResultValueForTests());
    }

    [Fact]
    public void DecodeItemUpgradeResultPayloadState_AcceptsReasonBranchFor65()
    {
        byte[] payload = { 65, 1, 0, 0, 0 };

        bool decoded = MapSimulator.TryDecodeItemUpgradeResultPayloadStateForTests(
            payload,
            out byte resultCode,
            out bool hasReasonCode,
            out int reasonCode,
            out bool hasOutcomeState,
            out int outcomeResultValue,
            out int outcomeUpgradeState,
            out string decodeError);

        Assert.True(decoded);
        Assert.Equal((byte)65, resultCode);
        Assert.True(hasReasonCode);
        Assert.Equal(1, reasonCode);
        Assert.False(hasOutcomeState);
        Assert.Equal(0, outcomeResultValue);
        Assert.Equal(0, outcomeUpgradeState);
        Assert.Null(decodeError);
    }

    [Fact]
    public void DecodeItemUpgradeResultPayloadState_AcceptsOutcomeBranchForNon65_66()
    {
        byte[] payload = { 1, 0, 0, 0, 0, 7, 0, 0, 0 };

        bool decoded = MapSimulator.TryDecodeItemUpgradeResultPayloadStateForTests(
            payload,
            out byte resultCode,
            out bool hasReasonCode,
            out int reasonCode,
            out bool hasOutcomeState,
            out int outcomeResultValue,
            out int outcomeUpgradeState,
            out string decodeError);

        Assert.True(decoded);
        Assert.Equal((byte)1, resultCode);
        Assert.False(hasReasonCode);
        Assert.Equal(0, reasonCode);
        Assert.True(hasOutcomeState);
        Assert.Equal(0, outcomeResultValue);
        Assert.Equal(7, outcomeUpgradeState);
        Assert.Null(decodeError);
    }

    [Fact]
    public void DecodeItemUpgradeResultPayloadState_RejectsMissingReasonFieldFor65()
    {
        byte[] payload = { 65 };

        bool decoded = MapSimulator.TryDecodeItemUpgradeResultPayloadStateForTests(
            payload,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out string decodeError);

        Assert.False(decoded);
        Assert.Contains("requires a reason payload field", decodeError);
    }

    [Fact]
    public void DecodeItemUpgradeResultPayloadState_RejectsTrailingBytesFor66()
    {
        byte[] payload = { 66, 1, 0, 0, 0, 0 };

        bool decoded = MapSimulator.TryDecodeItemUpgradeResultPayloadStateForTests(
            payload,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out string decodeError);

        Assert.False(decoded);
        Assert.Contains("unexpected trailing bytes", decodeError);
    }

    [Fact]
    public void DecodeItemUpgradeResultPayloadState_RejectsOutcomeShapeForNon65_66()
    {
        byte[] payload = { 0, 1, 0, 0, 0 };

        bool decoded = MapSimulator.TryDecodeItemUpgradeResultPayloadStateForTests(
            payload,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out string decodeError);

        Assert.False(decoded);
        Assert.Contains("requires outcome-state payload fields", decodeError);
    }

    [Fact]
    public void TryMapItemUpgradeResultCode_MatchesClientSuccessFailCodes()
    {
        Assert.True(MapSimulator.TryMapItemUpgradeResultCodeForTests(0, out bool failSuccess));
        Assert.False(failSuccess);

        Assert.True(MapSimulator.TryMapItemUpgradeResultCodeForTests(1, out bool okSuccess));
        Assert.True(okSuccess);

        Assert.False(MapSimulator.TryMapItemUpgradeResultCodeForTests(2, out _));
    }

    [Fact]
    public void TryMapItemUpgradeOutcomeStateResult_MatchesClientOutcomeStates()
    {
        Assert.True(MapSimulator.TryMapItemUpgradeOutcomeStateResultForTests(0, out bool failSuccess));
        Assert.False(failSuccess);

        Assert.True(MapSimulator.TryMapItemUpgradeOutcomeStateResultForTests(1, out bool okSuccess));
        Assert.True(okSuccess);

        Assert.False(MapSimulator.TryMapItemUpgradeOutcomeStateResultForTests(2, out _));
    }

    [Fact]
    public void TryResolveItemUpgradePacketOwnedNoticeOnlyResult_MapsClient66ReasonCodes()
    {
        Assert.True(MapSimulator.TryResolveItemUpgradePacketOwnedNoticeOnlyResultForTests(66, 1, null, out string reason1));
        Assert.True(MapSimulator.TryResolveItemUpgradePacketOwnedNoticeOnlyResultForTests(66, 2, null, out string reason2));
        Assert.True(MapSimulator.TryResolveItemUpgradePacketOwnedNoticeOnlyResultForTests(66, 3, null, out string reason3));

        Assert.NotEqual(reason1, reason2);
        Assert.NotEqual(reason2, reason3);
    }

    [Fact]
    public void TryResolveItemUpgradePacketOwnedNoticeOnlyResult_UsesBusyPathForUnknown66Reason()
    {
        Assert.True(MapSimulator.TryResolveItemUpgradePacketOwnedNoticeOnlyResultForTests(66, 99, 123, out string message));
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public void TryResolveItemUpgradePacketOwnedNoticeOnlyResult_Maps65ReasonZeroToApplyBranch()
    {
        Assert.False(MapSimulator.TryResolveItemUpgradePacketOwnedNoticeOnlyResultForTests(65, 0, null, out _));
        Assert.True(MapSimulator.TryResolveItemUpgradePacketOwnedNoticeOnlyResultForTests(65, 1, null, out string message));
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public void ItemUpgradeRequestPayload_RoundTripsThreeDecode4Fields()
    {
        byte[] payload = MapSimulator.BuildItemUpgradeRequestPayloadForTests(123456, 4, 987654321);

        Assert.True(MapSimulator.TryDecodeItemUpgradeRequestPayloadForTests(payload, out int itemToken, out int slotPosition, out int updateTick));
        Assert.Equal(123456, itemToken);
        Assert.Equal(4, slotPosition);
        Assert.Equal(987654321, updateTick);
    }

    [Fact]
    public void ItemUpgradeConsumeCashRequestPayload_RoundTripsPrefixedAndCoreFields()
    {
        byte[] payload = MapSimulator.BuildItemUpgradeConsumeCashRequestPayloadForTests(
            useRequestTick: 1111,
            consumableSlotPosition: 8,
            consumableItemId: 5062100,
            itemToken: 2222,
            updateTick: 3333);

        Assert.True(MapSimulator.TryDecodeItemUpgradeConsumeCashRequestPayloadForTests(
            payload,
            out int useRequestTick,
            out short consumeSlotPosition,
            out int consumeItemId,
            out int itemToken,
            out int slotPosition,
            out int updateTick));

        Assert.Equal(1111, useRequestTick);
        Assert.Equal((short)8, consumeSlotPosition);
        Assert.Equal(5062100, consumeItemId);
        Assert.Equal(2222, itemToken);
        Assert.Equal(8, slotPosition);
        Assert.Equal(3333, updateTick);
    }

    [Fact]
    public void ShouldUseConsumeCashItemUseRequestPayload_OnlyForMatchedCashSeeds()
    {
        Assert.True(MapSimulator.ShouldUseConsumeCashItemUseRequestPayloadForTests(InventoryType.CASH, hasMatchedConsumeCashSeed: true));
        Assert.False(MapSimulator.ShouldUseConsumeCashItemUseRequestPayloadForTests(InventoryType.CASH, hasMatchedConsumeCashSeed: false));
        Assert.False(MapSimulator.ShouldUseConsumeCashItemUseRequestPayloadForTests(InventoryType.USE, hasMatchedConsumeCashSeed: true));
    }
}
