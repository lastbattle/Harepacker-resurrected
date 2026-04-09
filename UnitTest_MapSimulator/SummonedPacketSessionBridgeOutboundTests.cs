using HaCreator.MapSimulator.Managers;
using System;
using System.Buffers.Binary;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class SummonedPacketSessionBridgeOutboundTests
    {
        [Fact]
        public void TryCreateAndBuildSg88Template_RewritesSummonPrimaryAndTargets()
        {
            byte[] rawPacket = CreateRawPacket(
                0x4A3,
                4001,
                77,
                9001,
                9001,
                9002,
                1234);

            Assert.True(
                SummonedOfficialSessionBridgeManager.TryDecodeObservedOutboundPacket(
                    rawPacket,
                    "test",
                    out SummonedOfficialSessionBridgeManager.OutboundPacketTrace requestPacket));

            Assert.True(
                SummonedOfficialSessionBridgeManager.TryCreateSg88ManualAttackRequestTemplate(
                    requestPacket,
                    4001,
                    9001,
                    new[] { 9001, 9002 },
                    out SummonedOfficialSessionBridgeManager.Sg88ManualAttackRequestPacketTemplate template,
                    out string templateError),
                templateError);

            Assert.True(
                SummonedOfficialSessionBridgeManager.TryBuildSg88ManualAttackRequestRawPacket(
                    template,
                    5001,
                    9101,
                    new[] { 9101, 9102 },
                    out byte[] rewrittenPacket,
                    out string buildError),
                buildError);

            Assert.Equal((ushort)0x4A3, ReadUInt16LittleEndian(rewrittenPacket, 0));
            Assert.Equal(5001, ReadInt32LittleEndian(rewrittenPacket, 2));
            Assert.Equal(77, ReadInt32LittleEndian(rewrittenPacket, 6));
            Assert.Equal(9101, ReadInt32LittleEndian(rewrittenPacket, 10));
            Assert.Equal(9101, ReadInt32LittleEndian(rewrittenPacket, 14));
            Assert.Equal(9102, ReadInt32LittleEndian(rewrittenPacket, 18));
            Assert.Equal(1234, ReadInt32LittleEndian(rewrittenPacket, 22));
        }

        [Fact]
        public void TryCreateSg88Template_AllowsPrimaryTargetToShareTargetSlot()
        {
            byte[] rawPacket = CreateRawPacket(
                0x4A3,
                4001,
                9001,
                9002);

            Assert.True(
                SummonedOfficialSessionBridgeManager.TryDecodeObservedOutboundPacket(
                    rawPacket,
                    "test",
                    out SummonedOfficialSessionBridgeManager.OutboundPacketTrace requestPacket));

            Assert.True(
                SummonedOfficialSessionBridgeManager.TryCreateSg88ManualAttackRequestTemplate(
                    requestPacket,
                    4001,
                    9001,
                    new[] { 9001, 9002 },
                    out SummonedOfficialSessionBridgeManager.Sg88ManualAttackRequestPacketTemplate template,
                    out string templateError),
                templateError);

            Assert.True(
                SummonedOfficialSessionBridgeManager.TryBuildSg88ManualAttackRequestRawPacket(
                    template,
                    5001,
                    9101,
                    new[] { 9101, 9102 },
                    out byte[] rewrittenPacket,
                    out string buildError),
                buildError);

            Assert.Equal(5001, ReadInt32LittleEndian(rewrittenPacket, 2));
            Assert.Equal(9101, ReadInt32LittleEndian(rewrittenPacket, 6));
            Assert.Equal(9102, ReadInt32LittleEndian(rewrittenPacket, 10));
        }

        [Fact]
        public void TryBuildSg88Template_FailsWhenTargetCountDoesNotMatchTemplate()
        {
            byte[] rawPacket = CreateRawPacket(
                0x4A3,
                4001,
                77,
                9001,
                9001,
                9002,
                1234);

            Assert.True(
                SummonedOfficialSessionBridgeManager.TryDecodeObservedOutboundPacket(
                    rawPacket,
                    "test",
                    out SummonedOfficialSessionBridgeManager.OutboundPacketTrace requestPacket));

            Assert.True(
                SummonedOfficialSessionBridgeManager.TryCreateSg88ManualAttackRequestTemplate(
                    requestPacket,
                    4001,
                    9001,
                    new[] { 9001, 9002 },
                    out SummonedOfficialSessionBridgeManager.Sg88ManualAttackRequestPacketTemplate template,
                    out _));

            Assert.False(
                SummonedOfficialSessionBridgeManager.TryBuildSg88ManualAttackRequestRawPacket(
                    template,
                    5001,
                    9101,
                    new[] { 9101 },
                    out _,
                    out string buildError));
            Assert.Contains("requires 2 target id slot(s)", buildError, StringComparison.Ordinal);
        }

        private static byte[] CreateRawPacket(ushort opcode, params int[] values)
        {
            byte[] rawPacket = new byte[sizeof(ushort) + (values.Length * sizeof(int))];
            BinaryPrimitives.WriteUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)), opcode);
            for (int i = 0; i < values.Length; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(
                    rawPacket.AsSpan(sizeof(ushort) + (i * sizeof(int)), sizeof(int)),
                    values[i]);
            }

            return rawPacket;
        }

        private static ushort ReadUInt16LittleEndian(byte[] buffer, int offset)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset, sizeof(ushort)));
        }

        private static int ReadInt32LittleEndian(byte[] buffer, int offset)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, sizeof(int)));
        }
    }
}
