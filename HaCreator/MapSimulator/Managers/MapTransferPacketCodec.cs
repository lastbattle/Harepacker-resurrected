using System;
using System.Collections.Generic;
using System.IO;

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
            byte[] rawPacket = new byte[sizeof(ushort) + payload.Length];
            BitConverter.GetBytes(OutboundRequestOpcode).CopyTo(rawPacket, 0);
            payload.CopyTo(rawPacket, sizeof(ushort));
            return rawPacket;
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

            request = new MapTransferRuntimeRequest
            {
                Type = resolvedType,
                Book = canTransferContinent ? MapTransferDestinationBook.Continent : MapTransferDestinationBook.Regular,
                MapId = mapId,
                SlotIndex = -1
            };
            return true;
        }
    }
}
