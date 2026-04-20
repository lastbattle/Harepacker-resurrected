using System;
using System.Linq;
using HaCreator.MapSimulator.Managers;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class RepeatSkillSustainPacketTransportParityTests
{
    [Fact]
    public void TryExtractSg88ReplayParityMismatchByteIndices_AcceptsQuotedJsonStyleLabels()
    {
        const string detail = "replayParity=mismatch; \"mismatchByteIndices\": [13,14]";

        bool parsed = PacketOwnedMechanicRepeatSkillRuntime.TryExtractSg88ReplayParityMismatchByteIndices(
            detail,
            out int[] byteIndices);

        Assert.True(parsed);
        Assert.Equal(new[] { 13, 14 }, byteIndices);
    }

    [Fact]
    public void TryExtractSg88ReplayParityMismatchByteIndices_AcceptsCompactColonSeparatedByteLabels()
    {
        const string detail = "replayParity=mismatch mismatchBytes:bytes13/14";

        bool parsed = PacketOwnedMechanicRepeatSkillRuntime.TryExtractSg88ReplayParityMismatchByteIndices(
            detail,
            out int[] byteIndices);

        Assert.True(parsed);
        Assert.Equal(new[] { 13, 14 }, byteIndices);
    }

    [Fact]
    public void TryExtractSg88ReplayParityMismatchByteIndices_FallsBackToFieldNames()
    {
        const string detail = "replayParity=mismatch mismatchFields=moveAction/vecCtrl";

        bool parsed = PacketOwnedMechanicRepeatSkillRuntime.TryExtractSg88ReplayParityMismatchByteIndices(
            detail,
            out int[] byteIndices);

        Assert.True(parsed);
        Assert.Equal(
            new[]
            {
                PacketOwnedMechanicRepeatSkillRuntime.Sg88FirstUseMoveActionByteIndex,
                PacketOwnedMechanicRepeatSkillRuntime.Sg88FirstUseVecCtrlByteIndex
            },
            byteIndices);
    }

    [Fact]
    public void TryExtractSg88ReplayParityMismatchByteIndices_FallsBackToExtendedFieldAliases()
    {
        const string detail = "replayParity=mismatch mismatchFieldNames: [requestTime, skillLevel]";

        bool parsed = PacketOwnedMechanicRepeatSkillRuntime.TryExtractSg88ReplayParityMismatchByteIndices(
            detail,
            out int[] byteIndices);

        Assert.True(parsed);
        Assert.Equal(new[] { 2, 3, 4, 5, 10 }, byteIndices);
    }

    [Fact]
    public void DescribeRecentSg88FirstUseReplayMatrix_ReportsOfficialCaptureParitySeparatelyFromSimulator()
    {
        Assert.True(
            PacketOwnedMechanicRepeatSkillRuntime.TryCreateSg88FirstUseRequest(
                requestTime: 5000,
                skillLevel: 15,
                x: 22,
                y: -14,
                moveActionLowBit: 1,
                vecCtrlState: 3,
                out PacketOwnedSg88FirstUseRequest request,
                out string error),
            error);

        byte[] officialMismatch = (byte[])request.RawPacket.Clone();
        officialMismatch[PacketOwnedMechanicRepeatSkillRuntime.Sg88FirstUseMoveActionByteIndex] = 0x03;
        byte[] simulatorMismatch = (byte[])request.RawPacket.Clone();
        simulatorMismatch[PacketOwnedMechanicRepeatSkillRuntime.Sg88FirstUseVecCtrlByteIndex] = 0x01;

        using SummonedOfficialSessionBridgeManager bridge = new();
        bridge.RecordObservedOutboundPacket(officialMismatch, "official-session:127.0.0.1:8484");
        bridge.RecordObservedOutboundPacket(simulatorMismatch, "simulator:sg88-template:unit");

        string matrix = bridge.DescribeRecentSg88FirstUseReplayMatrix(10);

        Assert.Contains("sourceRows:", matrix, StringComparison.Ordinal);
        Assert.Contains("officialSourceRows:", matrix, StringComparison.Ordinal);
        Assert.Contains("officialCaptureParity=observed=1", matrix, StringComparison.Ordinal);
        Assert.Contains("source=official-session count=1", matrix, StringComparison.Ordinal);
        Assert.Contains("source=simulator-sg88-template count=1", matrix, StringComparison.Ordinal);
    }
}
