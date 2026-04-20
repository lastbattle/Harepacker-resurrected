using HaCreator.MapSimulator.Managers;
using System;
using System.IO;

namespace UnitTest_MapSimulator
{
    public class MapTransferAuthoritativeBootstrapDecoderTailParityTests
    {
        [Fact]
        public void TryFindBootstrapBooks_AcceptsBoundedOpaqueTailBeforeServerFileTimeSuffix()
        {
            byte[] payload = BuildBootstrapPayload(opaqueByteCount: 80, appendServerFileTime: true);

            bool parsed = MapTransferAuthoritativeBootstrapDecoder.TryFindBootstrapBooks(
                payload,
                characterDataFlags: 0UL,
                characterJobId: 0,
                isPlausibleMapId: mapId => mapId == 100_000_000,
                out int[] regularFields,
                out int[] continentFields,
                out int matchedOffset,
                out _,
                out _,
                out _,
                out _,
                out _,
                out bool matchedKnownCharacterDataTail);

            Assert.True(parsed);
            Assert.Equal(0, matchedOffset);
            Assert.True(matchedKnownCharacterDataTail);
            Assert.Equal(100_000_000, regularFields[0]);
            Assert.Equal(MapTransferRuntimeManager.EmptyDestinationMapId, continentFields[0]);
        }

        [Fact]
        public void TryFindBootstrapBooks_RejectsOversizedOpaqueTailBeforeServerFileTimeSuffix()
        {
            byte[] payload = BuildBootstrapPayload(opaqueByteCount: 257, appendServerFileTime: true);

            bool parsed = MapTransferAuthoritativeBootstrapDecoder.TryFindBootstrapBooks(
                payload,
                characterDataFlags: 0UL,
                characterJobId: 0,
                isPlausibleMapId: mapId => mapId == 100_000_000,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);

            Assert.False(parsed);
        }

        private static byte[] BuildBootstrapPayload(int opaqueByteCount, bool appendServerFileTime)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);

            writer.Write(100_000_000);
            for (int i = 1; i < MapTransferRuntimeManager.RegularCapacity; i++)
            {
                writer.Write(MapTransferRuntimeManager.EmptyDestinationMapId);
            }

            for (int i = 0; i < MapTransferRuntimeManager.ContinentCapacity; i++)
            {
                writer.Write(MapTransferRuntimeManager.EmptyDestinationMapId);
            }

            for (int i = 0; i < opaqueByteCount; i++)
            {
                writer.Write((byte)0xA5);
            }

            if (appendServerFileTime)
            {
                long serverFileTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToFileTimeUtc();
                writer.Write(serverFileTime);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
