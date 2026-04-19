using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;
using System.IO;

namespace UnitTest_MapSimulator
{
    public sealed class PacketFieldFeedbackParityTests
    {
        [Fact]
        public void SummonEffectCandidates_FallbackIdsIncludeNpcSummonFamily()
        {
            IReadOnlyList<string> candidates = MapSimulator.GetPacketOwnedSummonEffectCandidatesForTest(5);

            Assert.Contains("Effect:Summon.img:5", candidates);
            Assert.Contains("Effect:MapEff.img:NpcSummon", candidates);
            Assert.Equal(2, candidates.Count);
        }

        [Fact]
        public void SummonEffectCandidates_NonFallbackIdsStayOnSummonFamily()
        {
            IReadOnlyList<string> candidates = MapSimulator.GetPacketOwnedSummonEffectCandidatesForTest(33);

            Assert.Single(candidates);
            Assert.Equal("Effect:Summon.img:33", candidates[0]);
        }

        [Fact]
        public void WhisperChaseTransferPayload_MatchesRecoveredClientShape()
        {
            byte[] payload = MapSimulator.BuildPacketOwnedWhisperChaseTransferFieldPayloadForTest(910000000, 1234, -55);

            Assert.NotEmpty(payload);

            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            Assert.Equal(0, reader.ReadByte());
            Assert.Equal(910000000, reader.ReadInt32());
            Assert.Equal(0, reader.ReadUInt16());
            Assert.Equal(0, reader.ReadByte());
            Assert.Equal(0, reader.ReadByte());
            Assert.Equal(1, reader.ReadByte());
            Assert.Equal(1234, reader.ReadInt32());
            Assert.Equal(-55, reader.ReadInt32());
            Assert.Equal(stream.Length, stream.Position);
        }

        [Fact]
        public void SwindleEncoding_UsesClientAnsiCodePage()
        {
            int ansiCodePage = PacketFieldFeedbackRuntime.GetSwindleAnsiCodePageForTest();
            int encodingCodePage = PacketFieldFeedbackRuntime.GetSwindleEncodingCodePageForTest();

            if (ansiCodePage > 0)
            {
                Assert.Equal(ansiCodePage, encodingCodePage);
                return;
            }

            Assert.Equal(System.Text.Encoding.Default.CodePage, encodingCodePage);
        }

        [Fact]
        public void SwindleKeywordMatcher_RespectsDbcsCharacterBoundaries()
        {
            byte[] message =
            {
                0xB0, 0x61,
                0x61
            };
            byte[] keyword = { 0x61, 0x61 };

            bool matched = PacketFieldFeedbackRuntime.ContainsSwindleKeywordForTest(message, keyword, 0xB0);

            Assert.False(matched);
        }
    }
}
