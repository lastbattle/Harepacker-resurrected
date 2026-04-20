using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator;

public sealed class PacketFieldUtilityParityTests
{
    [Fact]
    public void BuildMissingQuizPresentation_ClearsDisplayAndTimer()
    {
        (string summary, string displayText, bool startEventTimer) =
            MapSimulator.BuildMissingPacketOwnedQuizPresentationForPacketParity(
                isQuestion: true,
                category: 3,
                problemId: 7);

        Assert.Contains("3-7", summary);
        Assert.Equal(string.Empty, displayText);
        Assert.False(startEventTimer);
    }

    [Fact]
    public void BuildResolvedQuizPresentation_QuestionWithoutQText_KeepsTimerPath()
    {
        (string summary, string displayText, bool startEventTimer) =
            MapSimulator.BuildResolvedPacketOwnedQuizPresentationForPacketParity(
                isQuestion: true,
                category: 1,
                problemId: 2,
                questionText: null,
                detailText: "unused",
                answerValue: 1);

        Assert.Contains("no `q` text", summary);
        Assert.Equal(string.Empty, displayText);
        Assert.True(startEventTimer);
    }

    [Fact]
    public void BuildResolvedQuizPresentation_AnswerWithoutAValue_DefaultsToFalseMarker()
    {
        (string summary, string displayText, bool startEventTimer) =
            MapSimulator.BuildResolvedPacketOwnedQuizPresentationForPacketParity(
                isQuestion: false,
                category: 4,
                problemId: 5,
                questionText: "unused",
                detailText: "Detail",
                answerValue: null);

        Assert.Contains("4-5", summary);
        Assert.Contains("X", displayText);
        Assert.False(startEventTimer);
    }

    [Fact]
    public void BuildOfficialSessionFootHoldInfoResponsePayload_EncodesClientTupleOrder()
    {
        var entries = new[]
        {
            new PacketFieldUtilityFootholdEntry(
                "a",
                2,
                new[] { 10 },
                new PacketFieldUtilityMovingFootholdState(
                    Speed: 50,
                    X1: 100,
                    X2: 200,
                    Y1: 300,
                    Y2: 400,
                    CurrentX: 1234,
                    CurrentY: -567,
                    ReverseVertical: true,
                    ReverseHorizontal: false)),
        };

        byte[] payload = PacketFieldUtilityRuntime.BuildOfficialSessionFootHoldInfoResponsePayload(entries);

        Assert.Equal(sizeof(int) + sizeof(int) + sizeof(int) + sizeof(byte) + sizeof(byte), payload.Length);
        Assert.Equal(2, BitConverter.ToInt32(payload, 0));
        Assert.Equal(1234, BitConverter.ToInt32(payload, 4));
        Assert.Equal(-567, BitConverter.ToInt32(payload, 8));
        Assert.Equal(1, payload[12]);
        Assert.Equal(0, payload[13]);
    }

    [Fact]
    public void DescribeFootholdDispatch_FallsBackToDeferredOutboxWhenEarlierTransportsFail()
    {
        static (bool Success, string Status) SendBridge(int _, IReadOnlyList<byte> __) => (false, "bridge-failed");
        static (bool Success, string Status) SendOutbox(int _, IReadOnlyList<byte> __) => (false, "outbox-failed");
        static (bool Success, string Status) QueueBridge(int _, IReadOnlyList<byte> __) => (false, "queue-bridge-failed");
        static (bool Success, string Status) QueueOutbox(int _, IReadOnlyList<byte> __) => (true, "queue-outbox-ok");

        string summary = MapSimulator.DescribePacketOwnedFootholdOfficialResponseDispatch(
            payloadSummary: "payload ready.",
            opcode: 270,
            payload: new byte[] { 1, 2, 3 },
            trySendBridge: SendBridge,
            trySendOutbox: SendOutbox,
            tryQueueBridge: QueueBridge,
            tryQueueOutbox: QueueOutbox,
            allowDeferredBridge: true);

        Assert.Contains("Queued opcode 270 for deferred generic local-utility outbox delivery", summary);
        Assert.Contains("Bridge: bridge-failed", summary);
        Assert.Contains("Outbox: outbox-failed", summary);
        Assert.Contains("Deferred outbox: queue-outbox-ok", summary);
    }

    [Fact]
    public void ResolveSnapshotName_UsesRuntimeThenAuthoredFallbackWithoutPacketCacheWhenRuntimeExists()
    {
        string withRuntime = MapSimulator.SelectPacketOwnedDynamicPlatformSnapshotName(
            authoredPacketOwnedSnapshotName: "runtime-name",
            packetCacheName: "cached-name",
            authoredFallbackName: "authored-fallback",
            platformId: 9,
            allowPacketCacheNameFallback: true);
        string authoredFallback = MapSimulator.SelectPacketOwnedDynamicPlatformSnapshotName(
            authoredPacketOwnedSnapshotName: null,
            packetCacheName: "cached-name",
            authoredFallbackName: "authored-fallback",
            platformId: 9,
            allowPacketCacheNameFallback: false);

        Assert.Equal("runtime-name", withRuntime);
        Assert.Equal("authored-fallback", authoredFallback);
    }

    [Fact]
    public void ResolveSnapshotState_DefaultsFromRuntimeActiveFlagWhenPacketStateMissing()
    {
        Assert.Equal(2, MapSimulator.ResolvePacketOwnedSnapshotStateForPacketParity(packetOwnedState: null, isRuntimeActive: true));
        Assert.Equal(0, MapSimulator.ResolvePacketOwnedSnapshotStateForPacketParity(packetOwnedState: null, isRuntimeActive: false));
        Assert.Equal(1, MapSimulator.ResolvePacketOwnedSnapshotStateForPacketParity(packetOwnedState: 1, isRuntimeActive: false));
    }

    [Fact]
    public void BuildDynamicObjectTagAliases_SplitsCombinedTagsAndAddsPieceCoordinateForms()
    {
        IReadOnlyList<string> aliases = DynamicFootholdField.BuildDynamicObjectTagAliasCandidatesForPacketParity(
            "james1;james2;james3;",
            piece: -2,
            x: 10,
            y: 20);

        Assert.Contains("james1", aliases);
        Assert.Contains("james2", aliases);
        Assert.Contains("james3", aliases);
        Assert.Contains("james1/piece/-2", aliases);
        Assert.Contains("james2/piece/-2/10,20", aliases);
    }

    [Fact]
    public void BuildPacketOwnedSnapshotObjectKeyName_OmitsZeroPieceAndKeepsNegativeNonZero()
    {
        string zeroPiece = DynamicFootholdField.BuildPacketOwnedSnapshotObjectKeyName(
            objectKeyName: "oS/l0/l1/l2",
            piece: 0,
            x: 100,
            y: 200);
        string negativePiece = DynamicFootholdField.BuildPacketOwnedSnapshotObjectKeyName(
            objectKeyName: "oS/l0/l1/l2",
            piece: -3,
            x: 100,
            y: 200);

        Assert.Equal("oS/l0/l1/l2/100,200", zeroPiece);
        Assert.Equal("oS/l0/l1/l2/-3/100,200", negativePiece);
    }
}
