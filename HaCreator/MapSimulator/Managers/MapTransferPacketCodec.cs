using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using BinaryReader = MapleLib.PacketLib.PacketReader;
using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator.Managers
{
    internal static class MapTransferPacketCodec
    {
        public const ushort OutboundRequestOpcode = 114;
        public const int InboundResultOpcode = 69;

        public static byte[] BuildRequestPayload(MapTransferRuntimeRequest request)
        {
            if (request == null)
            {
                return Array.Empty<byte>();
            }

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)(request.Type == MapTransferRuntimeRequestType.Delete ? 0 : 1));
            writer.Write(request.Book == MapTransferDestinationBook.Continent);
            if (request.Type == MapTransferRuntimeRequestType.Delete)
            {
                writer.Write(request.MapId);
            }

            return stream.ToArray();
        }

        public static byte[] BuildRawRequestPacket(MapTransferRuntimeRequest request)
        {
            byte[] payload = BuildRequestPayload(request);
            using BinaryWriter writer = new();
            writer.Write(OutboundRequestOpcode);
            writer.Write(payload);
            return writer.ToArray();
        }

        public static byte[] BuildSyntheticResultPayload(
            MapTransferRuntimePacketResultCode resultCode,
            bool canTransferContinent,
            IReadOnlyList<int> fieldList)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)resultCode);
            writer.Write(canTransferContinent);

            if (resultCode is MapTransferRuntimePacketResultCode.RegisterApplied or MapTransferRuntimePacketResultCode.DeleteApplied)
            {
                int expectedCount = canTransferContinent
                    ? MapTransferRuntimeManager.ContinentCapacity
                    : MapTransferRuntimeManager.RegularCapacity;
                for (int index = 0; index < expectedCount; index++)
                {
                    int mapId = index < (fieldList?.Count ?? 0)
                        ? fieldList[index]
                        : MapTransferRuntimeManager.EmptyDestinationMapId;
                    writer.Write(mapId);
                }
            }

            return stream.ToArray();
        }

        public static bool TryDecodeInboundResultPacket(byte[] rawPacket, out byte[] payload, out string errorMessage)
        {
            payload = Array.Empty<byte>();
            errorMessage = null;

            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                errorMessage = "Map transfer result packet must include a 2-byte opcode.";
                return false;
            }

            int packetType = BitConverter.ToUInt16(rawPacket, 0);
            if (packetType != InboundResultOpcode)
            {
                payload = Array.Empty<byte>();
                errorMessage = $"Expected map transfer result opcode {InboundResultOpcode}, but received {packetType}.";
                return false;
            }

            if (!TryResolveInboundResultPacketLength(rawPacket, 0, out int packetLength, out errorMessage))
            {
                payload = Array.Empty<byte>();
                return false;
            }

            if (packetLength != rawPacket.Length)
            {
                payload = Array.Empty<byte>();
                errorMessage = $"Map transfer result packet contains {rawPacket.Length - packetLength} trailing byte(s); use clientrawseq for opcode-framed packet streams.";
                return false;
            }

            payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];
            return true;
        }

        internal static bool TryDecodeOutboundRequestPacket(
            byte[] rawPacket,
            out MapTransferRuntimeRequest request,
            out string errorMessage)
        {
            request = null;
            errorMessage = null;

            if (rawPacket == null || rawPacket.Length < sizeof(ushort) + sizeof(byte) + sizeof(byte))
            {
                errorMessage = "Map transfer request packet must include a 2-byte opcode, request type, and continent flag.";
                return false;
            }

            int packetType = BitConverter.ToUInt16(rawPacket, 0);
            if (packetType != OutboundRequestOpcode)
            {
                errorMessage = $"Expected map transfer request opcode {OutboundRequestOpcode}, but received {packetType}.";
                return false;
            }

            using MemoryStream stream = new(rawPacket, sizeof(ushort), rawPacket.Length - sizeof(ushort), writable: false);
            using BinaryReader reader = new(stream);
            byte requestType = reader.ReadByte();
            bool canTransferContinent = reader.ReadBoolean();
            MapTransferRuntimeRequestType resolvedType = requestType switch
            {
                0 => MapTransferRuntimeRequestType.Delete,
                1 => MapTransferRuntimeRequestType.Register,
                _ => (MapTransferRuntimeRequestType)(-1)
            };
            if (resolvedType != MapTransferRuntimeRequestType.Delete &&
                resolvedType != MapTransferRuntimeRequestType.Register)
            {
                errorMessage = $"Unsupported map transfer request type {requestType}.";
                return false;
            }

            int mapId = 0;
            if (resolvedType == MapTransferRuntimeRequestType.Delete)
            {
                if (stream.Position + sizeof(int) > stream.Length)
                {
                    errorMessage = "Map transfer delete request packet is missing the target map id.";
                    return false;
                }

                mapId = reader.ReadInt32();
            }

            if (stream.Position != stream.Length)
            {
                errorMessage = "Map transfer request packet contains trailing bytes beyond the client opcode 114 payload.";
                return false;
            }

            request = new MapTransferRuntimeRequest
            {
                Type = resolvedType,
                Book = canTransferContinent ? MapTransferDestinationBook.Continent : MapTransferDestinationBook.Regular,
                MapId = mapId,
                SlotIndex = -1
            };
            return true;
        }

        internal static bool TryParseRawPacketHex(string hexText, out byte[] rawPacket, out string errorMessage)
        {
            rawPacket = Array.Empty<byte>();
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(hexText))
            {
                errorMessage = "A raw packet hex string is required.";
                return false;
            }

            StringBuilder normalized = new(hexText.Length);
            for (int i = 0; i < hexText.Length; i++)
            {
                char value = hexText[i];
                if (char.IsWhiteSpace(value) || value == '-' || value == ':')
                {
                    continue;
                }

                if (value == '0' &&
                    i + 1 < hexText.Length &&
                    (hexText[i + 1] == 'x' || hexText[i + 1] == 'X'))
                {
                    i++;
                    continue;
                }

                if (!Uri.IsHexDigit(value))
                {
                    errorMessage = $"Raw packet hex contains invalid character '{value}'.";
                    return false;
                }

                normalized.Append(value);
            }

            if (normalized.Length == 0)
            {
                errorMessage = "A raw packet hex string is required.";
                return false;
            }

            if ((normalized.Length % 2) != 0)
            {
                errorMessage = "Raw packet hex must contain an even number of hex digits.";
                return false;
            }

            rawPacket = Convert.FromHexString(normalized.ToString());
            return true;
        }

        internal static bool TryParseRawPacketHexSequence(
            string hexSequenceText,
            out IReadOnlyList<byte[]> rawPackets,
            out string errorMessage)
        {
            rawPackets = Array.Empty<byte[]>();
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(hexSequenceText))
            {
                errorMessage = "A raw packet hex string is required.";
                return false;
            }

            char[] delimiters = { ',', ';', '\r', '\n' };
            string[] segments = hexSequenceText
                .Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Trim())
                .Where(segment => segment.Length > 0)
                .ToArray();
            if (segments.Length == 0)
            {
                errorMessage = "A raw packet hex string is required.";
                return false;
            }

            List<byte[]> parsedPackets = new(segments.Length);
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (!TryParseRawPacketHex(segment, out byte[] rawPacket, out string parseError))
                {
                    errorMessage = $"Invalid packet #{i + 1}: {parseError}";
                    return false;
                }

                if (!TrySplitKnownOpcodeFramedPackets(rawPacket, out IReadOnlyList<byte[]> segmentPackets, out string splitError))
                {
                    errorMessage = $"Invalid packet #{i + 1}: {splitError}";
                    return false;
                }

                parsedPackets.AddRange(segmentPackets);
            }

            rawPackets = parsedPackets;
            return true;
        }

        internal static bool TrySplitKnownOpcodeFramedPackets(
            byte[] rawStream,
            out IReadOnlyList<byte[]> rawPackets,
            out string errorMessage)
        {
            rawPackets = Array.Empty<byte[]>();
            errorMessage = null;
            if (rawStream == null || rawStream.Length == 0)
            {
                errorMessage = "A raw packet byte stream is required.";
                return false;
            }

            List<byte[]> packets = new();
            int offset = 0;
            while (offset < rawStream.Length)
            {
                if (!TryResolveKnownOpcodePacketLength(rawStream, offset, out int packetLength, out errorMessage))
                {
                    rawPackets = Array.Empty<byte[]>();
                    return false;
                }

                byte[] packet = new byte[packetLength];
                Buffer.BlockCopy(rawStream, offset, packet, 0, packetLength);
                packets.Add(packet);
                offset += packetLength;
            }

            rawPackets = packets;
            return true;
        }

        private static bool TryResolveKnownOpcodePacketLength(
            byte[] rawStream,
            int offset,
            out int packetLength,
            out string errorMessage)
        {
            packetLength = 0;
            errorMessage = null;
            if (rawStream == null || offset < 0 || rawStream.Length - offset < sizeof(ushort))
            {
                errorMessage = "Map transfer packet stream ended before a 2-byte opcode.";
                return false;
            }

            ushort opcode = BitConverter.ToUInt16(rawStream, offset);
            return opcode switch
            {
                OutboundRequestOpcode => TryResolveOutboundRequestPacketLength(rawStream, offset, out packetLength, out errorMessage),
                InboundResultOpcode => TryResolveInboundResultPacketLength(rawStream, offset, out packetLength, out errorMessage),
                _ => FailUnknownOpcode(opcode, out packetLength, out errorMessage)
            };
        }

        private static bool TryResolveOutboundRequestPacketLength(
            byte[] rawStream,
            int offset,
            out int packetLength,
            out string errorMessage)
        {
            packetLength = 0;
            errorMessage = null;
            const int minimumLength = sizeof(ushort) + sizeof(byte) + sizeof(byte);
            if (rawStream.Length - offset < minimumLength)
            {
                errorMessage = "Map transfer request packet stream ended before request type and continent flag.";
                return false;
            }

            byte requestType = rawStream[offset + sizeof(ushort)];
            packetLength = requestType switch
            {
                0 => minimumLength + sizeof(int),
                1 => minimumLength,
                _ => 0
            };
            if (packetLength == 0)
            {
                errorMessage = $"Unsupported map transfer request type {requestType}.";
                return false;
            }

            if (rawStream.Length - offset < packetLength)
            {
                errorMessage = "Map transfer request packet stream ended before the complete opcode 114 payload.";
                return false;
            }

            return true;
        }

        private static bool TryResolveInboundResultPacketLength(
            byte[] rawStream,
            int offset,
            out int packetLength,
            out string errorMessage)
        {
            packetLength = 0;
            errorMessage = null;
            const int minimumLength = sizeof(ushort) + sizeof(byte) + sizeof(byte);
            if (rawStream.Length - offset < minimumLength)
            {
                errorMessage = "Map transfer result packet stream ended before result code and continent flag.";
                return false;
            }

            MapTransferRuntimePacketResultCode resultCode =
                (MapTransferRuntimePacketResultCode)rawStream[offset + sizeof(ushort)];
            bool canTransferContinent = rawStream[offset + sizeof(ushort) + sizeof(byte)] != 0;
            packetLength = minimumLength;
            if (resultCode is MapTransferRuntimePacketResultCode.RegisterApplied or MapTransferRuntimePacketResultCode.DeleteApplied)
            {
                int fieldCount = canTransferContinent
                    ? MapTransferRuntimeManager.ContinentCapacity
                    : MapTransferRuntimeManager.RegularCapacity;
                packetLength += fieldCount * sizeof(int);
            }
            else if (resultCode is MapTransferRuntimePacketResultCode.OfficialFailure6 or MapTransferRuntimePacketResultCode.OfficialFailure7 &&
                     rawStream.Length - offset > packetLength &&
                     TryResolveOptionalTargetUserStringByteLength(rawStream, offset, packetLength, out int stringByteLength))
            {
                packetLength += stringByteLength;
            }

            if (rawStream.Length - offset < packetLength)
            {
                errorMessage = "Map transfer result packet stream ended before the complete opcode 69 payload.";
                return false;
            }

            return true;
        }

        private static bool TryResolveOptionalTargetUserStringByteLength(
            byte[] rawStream,
            int packetOffset,
            int packetLength,
            out int byteLength)
        {
            byteLength = 0;
            int offset = packetOffset + packetLength;
            if (rawStream == null || offset < 0 || rawStream.Length - offset < sizeof(short))
            {
                return false;
            }

            short encodedLength = BitConverter.ToInt16(rawStream, offset);
            if (encodedLength < -32 || encodedLength > 32)
            {
                return false;
            }

            int payloadByteLength = encodedLength < 0
                ? checked(-encodedLength * sizeof(char))
                : encodedLength;
            byteLength = sizeof(short) + payloadByteLength;
            if (payloadByteLength < 0 || rawStream.Length - offset < byteLength)
            {
                return false;
            }

            int nextPacketOffset = offset + byteLength;
            return nextPacketOffset == rawStream.Length ||
                   (rawStream.Length - nextPacketOffset >= sizeof(ushort) &&
                    IsKnownMapTransferOpcode(BitConverter.ToUInt16(rawStream, nextPacketOffset)));
        }

        private static bool IsKnownMapTransferOpcode(ushort opcode)
        {
            return opcode == OutboundRequestOpcode || opcode == InboundResultOpcode;
        }

        private static bool FailUnknownOpcode(ushort opcode, out int packetLength, out string errorMessage)
        {
            packetLength = 0;
            errorMessage = $"Expected map transfer request opcode {OutboundRequestOpcode} or result opcode {InboundResultOpcode}, but received {opcode}.";
            return false;
        }
    }
}
