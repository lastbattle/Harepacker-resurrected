using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;
using System.Buffers.Binary;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class PacketOwnedLogoutGiftParityTests
    {
        [Fact]
        public void TryDecodeTrailingLogoutGiftConfigPayload_SplitsLeadingAndTrailingOpaqueTail()
        {
            byte[] leadingOpaque = BuildInt32Bytes(111, 222, 333, 444);
            byte[] trailingOpaque = new byte[] { 0xFF, 0xEE, 0xDD, 0xCC };
            byte[] config = BuildInt32Bytes(1, 900001, 900002, 900003);
            byte[] payload = Concat(leadingOpaque, config, trailingOpaque);

            bool decoded = PacketStageTransitionRuntime.TryDecodeTrailingLogoutGiftConfigPayload(
                payload,
                out int predictQuitRawValue,
                out int[] commoditySerialNumbers,
                out byte[] decodedLeadingOpaqueBytes,
                out int[] decodedLeadingOpaqueInt32Values,
                out byte[] decodedTrailingOpaqueBytes,
                out int[] decodedTrailingOpaqueInt32Values,
                out int logoutGiftConfigOffset,
                out string error);

            Assert.True(decoded, error);
            Assert.Equal(1, predictQuitRawValue);
            Assert.Equal(new[] { 900001, 900002, 900003 }, commoditySerialNumbers);
            Assert.Equal(leadingOpaque, decodedLeadingOpaqueBytes);
            Assert.Equal(new[] { 111, 222, 333, 444 }, decodedLeadingOpaqueInt32Values);
            Assert.Equal(trailingOpaque, decodedTrailingOpaqueBytes);
            Assert.Equal(new[] { unchecked((int)0xCCDDEEFF) }, decodedTrailingOpaqueInt32Values);
            Assert.Equal(leadingOpaque.Length, logoutGiftConfigOffset);
        }

        [Fact]
        public void TryDecodeTrailingLogoutGiftConfigPayload_RejectsTooShortTail()
        {
            bool decoded = PacketStageTransitionRuntime.TryDecodeTrailingLogoutGiftConfigPayload(
                new byte[15],
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out string error);

            Assert.False(decoded);
            Assert.Contains("too short", error);
        }

        [Fact]
        public void DecodePacketOwnedLogoutGiftLeadingContextFields_MapsLastThreeInt32ToPrecursorSlots()
        {
            PacketOwnedLogoutGiftContextField[] fields = MapSimulator.DecodePacketOwnedLogoutGiftLeadingContextFields(
                new[] { 10, 20, 30, 40 });

            Assert.Equal(3, fields.Length);
            Assert.Equal(4134, fields[0].DwordIndex);
            Assert.Equal(4135, fields[1].DwordIndex);
            Assert.Equal(4136, fields[2].DwordIndex);
            Assert.Equal(20, fields[0].Value);
            Assert.Equal(30, fields[1].Value);
            Assert.Equal(40, fields[2].Value);
            Assert.Contains("dword_4098", fields[0].SemanticName);
            Assert.Contains("dword_409C", fields[1].SemanticName);
            Assert.Contains("dword_40A0", fields[2].SemanticName);
        }

        [Theory]
        [InlineData(false, true, 1, PacketOwnedLogoutGiftOwnerAvailability.StageNotField)]
        [InlineData(true, true, 0, PacketOwnedLogoutGiftOwnerAvailability.PredictQuitFalse)]
        [InlineData(true, false, 0, PacketOwnedLogoutGiftOwnerAvailability.Available)]
        [InlineData(true, true, 1, PacketOwnedLogoutGiftOwnerAvailability.Available)]
        public void ResolvePacketOwnedLogoutGiftOwnerAvailability_MatchesClientGate(
            bool isFieldStageActive,
            bool hasPredictQuitFlag,
            int predictQuitRawValue,
            PacketOwnedLogoutGiftOwnerAvailability expected)
        {
            PacketOwnedLogoutGiftOwnerAvailability availability = MapSimulator.ResolvePacketOwnedLogoutGiftOwnerAvailability(
                isFieldStageActive,
                hasPredictQuitFlag,
                predictQuitRawValue);

            Assert.Equal(expected, availability);
        }

        [Theory]
        [InlineData(true, false, false, true, PacketOwnedLogoutGiftRefreshDisposition.NoInstantiatedOwner)]
        [InlineData(true, false, false, false, PacketOwnedLogoutGiftRefreshDisposition.NoOwnerAllowed)]
        [InlineData(true, true, true, true, PacketOwnedLogoutGiftRefreshDisposition.RefreshVisibleOwner)]
        [InlineData(true, true, false, true, PacketOwnedLogoutGiftRefreshDisposition.RefreshHiddenInstantiatedOwner)]
        [InlineData(false, false, false, true, PacketOwnedLogoutGiftRefreshDisposition.MissingConfig)]
        public void ResolvePacketOwnedLogoutGiftRefreshDisposition_MatchesPacket432Branching(
            bool hasConfig,
            bool ownerSingletonPresent,
            bool ownerVisible,
            bool shouldShowOwner,
            PacketOwnedLogoutGiftRefreshDisposition expected)
        {
            PacketOwnedLogoutGiftRefreshDisposition disposition = MapSimulator.ResolvePacketOwnedLogoutGiftRefreshDisposition(
                hasConfig,
                ownerSingletonPresent,
                ownerVisible,
                shouldShowOwner);

            Assert.Equal(expected, disposition);
        }

        [Theory]
        [InlineData(true, false, false, false, false)]
        [InlineData(false, true, false, false, false)]
        [InlineData(false, false, true, false, false)]
        [InlineData(false, false, false, true, false)]
        [InlineData(false, false, false, false, true)]
        public void ResolvePacketOwnedLogoutGiftFieldStageActive_RejectsNonFieldStages(
            bool isLoginMap,
            bool isCashShopMap,
            bool isCashShopStageVisible,
            bool isMtsStageVisible,
            bool expected)
        {
            bool isFieldStageActive = MapSimulator.ResolvePacketOwnedLogoutGiftFieldStageActive(
                isLoginMap,
                isCashShopMap,
                isCashShopStageVisible,
                isMtsStageVisible);

            Assert.Equal(expected, isFieldStageActive);
        }

        [Fact]
        public void TryBuildPacketOwnedLogoutGiftSelectionPayload_UsesClientButtonIdsAndOpcode313PayloadShape()
        {
            bool accepted = MapSimulator.TryBuildPacketOwnedLogoutGiftSelectionPayload(1002, out byte[] payload, out int slotIndex);

            Assert.True(accepted);
            Assert.Equal(2, slotIndex);
            Assert.Equal(4, payload.Length);
            Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(payload));
        }

        [Fact]
        public void TryBuildPacketOwnedLogoutGiftSelectionPayload_RejectsNonClientButtonRange()
        {
            bool accepted = MapSimulator.TryBuildPacketOwnedLogoutGiftSelectionPayload(999, out byte[] payload, out int slotIndex);

            Assert.False(accepted);
            Assert.Empty(payload);
            Assert.Equal(0, slotIndex);
        }

        private static byte[] BuildInt32Bytes(params int[] values)
        {
            byte[] bytes = new byte[values.Length * sizeof(int)];
            for (int i = 0; i < values.Length; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(
                    bytes.AsSpan(i * sizeof(int), sizeof(int)),
                    values[i]);
            }

            return bytes;
        }

        private static byte[] Concat(params byte[][] chunks)
        {
            int totalLength = 0;
            foreach (byte[] chunk in chunks)
            {
                totalLength += chunk?.Length ?? 0;
            }

            byte[] combined = new byte[totalLength];
            int offset = 0;
            foreach (byte[] chunk in chunks)
            {
                if (chunk == null || chunk.Length == 0)
                {
                    continue;
                }

                chunk.CopyTo(combined, offset);
                offset += chunk.Length;
            }

            return combined;
        }
    }
}
