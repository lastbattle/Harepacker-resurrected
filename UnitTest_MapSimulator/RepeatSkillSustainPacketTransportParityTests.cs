using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator
{
    public class RepeatSkillSustainPacketTransportParityTests
    {
        [Fact]
        public void TryExtractMismatchByteIndices_AcceptsSpacedBytesWrappedSlashList()
        {
            string decodeDetail = "SG-88 first-use replay parity mismatch mismatchBytes = bytes[0x0D/0x0E/15];";

            bool parsed = PacketOwnedMechanicRepeatSkillRuntime.TryExtractSg88ReplayParityMismatchByteIndices(
                decodeDetail,
                out int[] byteIndices);

            Assert.True(parsed);
            Assert.Equal(new[] { 13, 14, 15 }, byteIndices);
        }

        [Fact]
        public void TryExtractMismatchByteIndices_AcceptsSpacedSingleByteMarkers()
        {
            string decodeDetail = "SG-88 first-use replay parity mismatch mismatchByteIndex = byte(0x0F)";

            bool parsed = PacketOwnedMechanicRepeatSkillRuntime.TryExtractSg88ReplayParityMismatchByteIndices(
                decodeDetail,
                out int[] byteIndices);

            Assert.True(parsed);
            Assert.Equal(new[] { 15 }, byteIndices);
        }

        [Fact]
        public void Sg88ReplayMatrix_IncludesOfficialSourceRowsAndOfficialParitySummary()
        {
            SummonedOfficialSessionBridgeManager manager = new();
            Queue<SummonedOfficialSessionBridgeManager.OutboundPacketTrace> recentQueue = GetRecentOutboundQueue(manager);
            recentQueue.Enqueue(CreateSg88Trace(
                source: "official-session:capture-1",
                replayMatched: false,
                vecCtrlState: 2,
                mismatchByteIndices: new[] { PacketOwnedMechanicRepeatSkillRuntime.Sg88FirstUseVecCtrlByteIndex },
                mismatchPairs: new[] { "byte14:0x02->0x00" }));
            recentQueue.Enqueue(CreateSg88Trace(
                source: "simulator:sg88-template:replay-1",
                replayMatched: true,
                vecCtrlState: 0,
                mismatchByteIndices: System.Array.Empty<int>(),
                mismatchPairs: System.Array.Empty<string>()));

            string matrix = manager.DescribeRecentSg88FirstUseReplayMatrix(10);

            Assert.Contains("sourceRows:", matrix);
            Assert.Contains("officialSourceRows:", matrix);
            Assert.Contains("source=official-session", matrix);
            Assert.Contains("officialCaptureParity=observed=1", matrix);
        }

        private static Queue<SummonedOfficialSessionBridgeManager.OutboundPacketTrace> GetRecentOutboundQueue(SummonedOfficialSessionBridgeManager manager)
        {
            FieldInfo field = typeof(SummonedOfficialSessionBridgeManager).GetField(
                "_recentOutboundPackets",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return Assert.IsType<Queue<SummonedOfficialSessionBridgeManager.OutboundPacketTrace>>(field.GetValue(manager));
        }

        private static SummonedOfficialSessionBridgeManager.OutboundPacketTrace CreateSg88Trace(
            string source,
            bool replayMatched,
            byte vecCtrlState,
            int[] mismatchByteIndices,
            string[] mismatchPairs)
        {
            PacketOwnedSg88FirstUseRequest decoded = new(
                PacketOwnedMechanicRepeatSkillRuntime.Sg88FirstUseSummonOpcode,
                PacketOwnedMechanicRepeatSkillRuntime.Sg88SkillId,
                skillLevel: 1,
                requestTime: 123,
                x: 10,
                y: 20,
                moveActionLowBit: 1,
                rawMoveActionByte: 1,
                vecCtrlState: vecCtrlState,
                payload: new byte[15],
                rawPacket: new byte[17]);

            return new SummonedOfficialSessionBridgeManager.OutboundPacketTrace(
                Opcode: PacketOwnedMechanicRepeatSkillRuntime.Sg88FirstUseSummonOpcode,
                PayloadLength: 15,
                PayloadHex: "00",
                RawPacketHex: "00",
                Source: source,
                ObservedAt: 1000,
                BoundSg88SummonObjectId: null,
                BoundSg88RequestedAt: null,
                DecodedSg88FirstUseRequest: decoded,
                DecodedSg88FirstUseReplayParityMatched: replayMatched,
                DecodedSg88FirstUseDecodeDetail: replayMatched
                    ? null
                    : $"mismatchBytes=[{string.Join(",", mismatchByteIndices.Select(i => i.ToString()))}]",
                DecodedSg88FirstUseReplayTemplateHex: "67007B0000004BFA1700010A0014000100",
                DecodedSg88FirstUseReplayMismatchByteIndex: mismatchByteIndices.FirstOrDefault(-1),
                DecodedSg88FirstUseReplayMismatchByteIndices: mismatchByteIndices,
                DecodedSg88FirstUseReplayMismatchPairs: mismatchPairs);
        }
    }
}
