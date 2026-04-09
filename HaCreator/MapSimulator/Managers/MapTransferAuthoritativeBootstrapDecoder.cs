using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    internal static class MapTransferAuthoritativeBootstrapDecoder
    {
        internal const ulong CharacterDataMapTransferFlag = 0x1000UL;
        private const int LogoutGiftConfigByteLength = 3 * sizeof(int);
        internal const int BootstrapBookByteLength =
            (MapTransferRuntimeManager.RegularCapacity + MapTransferRuntimeManager.ContinentCapacity) * sizeof(int);

        internal static bool TryFindBootstrapBooks(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool ignoredTrailingLogoutGiftConfig,
            out bool matchedExactTailBoundary,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            ignoredTrailingLogoutGiftConfig = false;
            matchedExactTailBoundary = false;
            matchedKnownCharacterDataTail = false;

            if (payload.Length < BootstrapBookByteLength || isPlausibleMapId == null)
            {
                return false;
            }

            if (payload.Length >= BootstrapBookByteLength + LogoutGiftConfigByteLength)
            {
                ReadOnlySpan<byte> leadingPayload = payload[..^LogoutGiftConfigByteLength];
                if (TryFindBootstrapBooksAtExactTail(
                        leadingPayload,
                        characterDataFlags,
                        isPlausibleMapId,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out matchedKnownCharacterDataTail))
                {
                    ignoredTrailingLogoutGiftConfig = true;
                    matchedExactTailBoundary = true;
                    return true;
                }

                if (TryFindBootstrapBooksCore(
                        leadingPayload,
                        characterDataFlags,
                        isPlausibleMapId,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out matchedKnownCharacterDataTail))
                {
                    ignoredTrailingLogoutGiftConfig = true;
                    return true;
                }
            }

            if (TryFindBootstrapBooksAtExactTail(
                    payload,
                    characterDataFlags,
                    isPlausibleMapId,
                    out regularFields,
                    out continentFields,
                    out matchedOffset,
                    out matchedKnownCharacterDataTail))
            {
                matchedExactTailBoundary = true;
                return true;
            }

            return TryFindBootstrapBooksCore(
                payload,
                characterDataFlags,
                isPlausibleMapId,
                out regularFields,
                out continentFields,
                out matchedOffset,
                out matchedKnownCharacterDataTail);
        }

        private static bool TryFindBootstrapBooksAtExactTail(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownCharacterDataTail = false;

            if (payload.Length < BootstrapBookByteLength)
            {
                return false;
            }

            int tailOffset = payload.Length - BootstrapBookByteLength;
            return TryReadBootstrapBooksAtOffset(
                payload,
                tailOffset,
                characterDataFlags,
                isPlausibleMapId,
                out regularFields,
                out continentFields,
                out matchedOffset,
                out matchedKnownCharacterDataTail);
        }

        private static bool TryFindBootstrapBooksCore(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownCharacterDataTail = false;

            if (payload.Length < BootstrapBookByteLength || isPlausibleMapId == null)
            {
                return false;
            }

            for (int offset = payload.Length - BootstrapBookByteLength; offset >= 0; offset--)
            {
                if (TryReadBootstrapBooksAtOffset(
                        payload,
                        offset,
                        characterDataFlags,
                        isPlausibleMapId,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out matchedKnownCharacterDataTail))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadBootstrapBooksAtOffset(
            ReadOnlySpan<byte> payload,
            int offset,
            ulong characterDataFlags,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownCharacterDataTail = false;

            int slotCount = MapTransferRuntimeManager.RegularCapacity + MapTransferRuntimeManager.ContinentCapacity;
            if (offset < 0 || payload.Length - offset < BootstrapBookByteLength || isPlausibleMapId == null)
            {
                return false;
            }

            int[] candidateRegular = new int[MapTransferRuntimeManager.RegularCapacity];
            int[] candidateContinent = new int[MapTransferRuntimeManager.ContinentCapacity];
            HashSet<int> seenMaps = new();

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int mapId = BitConverter.ToInt32(payload.Slice(offset + (slotIndex * sizeof(int)), sizeof(int)));
                if (!IsAcceptedBootstrapValue(mapId, isPlausibleMapId))
                {
                    return false;
                }

                if (slotIndex < MapTransferRuntimeManager.RegularCapacity)
                {
                    candidateRegular[slotIndex] = mapId;
                }
                else
                {
                    candidateContinent[slotIndex - MapTransferRuntimeManager.RegularCapacity] = mapId;
                }

                if (mapId > 0 && mapId != MapTransferRuntimeManager.EmptyDestinationMapId)
                {
                    seenMaps.Add(mapId);
                }
            }

            if (seenMaps.Count == 0)
            {
                return false;
            }

            if (!TryValidatePostMapTransferTail(
                    payload[(offset + BootstrapBookByteLength)..],
                    characterDataFlags,
                    out bool matchedKnownTail))
            {
                return false;
            }

            regularFields = candidateRegular;
            continentFields = candidateContinent;
            matchedOffset = offset;
            matchedKnownCharacterDataTail = matchedKnownTail;
            return true;
        }

        private static bool IsAcceptedBootstrapValue(int mapId, Func<int, bool> isPlausibleMapId)
        {
            return mapId == 0 ||
                   mapId == MapTransferRuntimeManager.EmptyDestinationMapId ||
                   isPlausibleMapId(mapId);
        }

        private static bool TryValidatePostMapTransferTail(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            out bool matchedKnownTail)
        {
            matchedKnownTail = false;
            if (payload.Length == 0)
            {
                matchedKnownTail = true;
                return true;
            }

            const ulong unsupportedTailFlags =
                0x40000UL | // New Year card records
                0x200000UL; // Wild Hunter info
            bool hasUnsupportedTailFlags = (characterDataFlags & unsupportedTailFlags) != 0;
            if (hasUnsupportedTailFlags)
            {
                return true;
            }

            try
            {
                using MemoryStream stream = new(payload.ToArray(), writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                if ((characterDataFlags & 0x80000UL) != 0)
                {
                    int questExCount = reader.ReadUInt16();
                    for (int i = 0; i < questExCount; i++)
                    {
                        _ = reader.ReadUInt16();
                        _ = ReadMapleString(reader);
                    }
                }

                if ((characterDataFlags & 0x400000UL) != 0)
                {
                    int questCompleteCount = reader.ReadUInt16();
                    for (int i = 0; i < questCompleteCount; i++)
                    {
                        _ = reader.ReadUInt16();
                        _ = reader.ReadInt64();
                    }
                }

                if ((characterDataFlags & 0x800000UL) != 0)
                {
                    int visitorQuestCount = reader.ReadUInt16();
                    for (int i = 0; i < visitorQuestCount; i++)
                    {
                        _ = reader.ReadUInt16();
                        _ = reader.ReadUInt16();
                    }
                }

                matchedKnownTail = stream.Position == stream.Length;
                return matchedKnownTail;
            }
            catch (Exception) when (payload.Length > 0)
            {
                return false;
            }
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            int length = reader.ReadUInt16();
            if (length <= 0)
            {
                return string.Empty;
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Maple string exceeded the remaining post-map-transfer tail payload.");
            }

            return Encoding.Default.GetString(bytes);
        }
    }
}
