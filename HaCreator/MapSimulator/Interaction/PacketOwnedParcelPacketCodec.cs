using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedParcelDecodedEntry
    {
        public int ParcelSerial { get; init; }
        public string Sender { get; init; } = string.Empty;
        public string MemoText { get; init; } = string.Empty;
        public bool IsQuickDelivery { get; init; }
        public byte QuickDeliveryRawFlag { get; init; }
        public byte[] QuickDeliveryReservedBytes { get; init; } = Array.Empty<byte>();
        public bool HasQuickDeliveryReservedState { get; init; }
        public DateTimeOffset? ExpirationTimestampUtc { get; init; }
        public bool IsRead { get; init; }
        public bool IsKept { get; init; }
        public bool IsAttachmentClaimed { get; init; }
        public byte StateFlags { get; init; }
        public byte PostBodyItemPresenceFlag { get; init; }
        public bool HasItemAttachment { get; init; }
        public bool HasMesoAttachment { get; init; }
        public int AttachmentItemId { get; init; }
        public int AttachmentQuantity { get; init; }
        public int AttachmentMeso { get; init; }
        public bool HasUndecodedItemAttachment { get; init; }
        public byte[] TrailingPayloadBytes { get; init; } = Array.Empty<byte>();
        public bool HasTrailingPayloadBytes { get; init; }
    }

    internal sealed class PacketOwnedParcelSessionDecodeResult
    {
        public IReadOnlyList<PacketOwnedParcelDecodedEntry> ReceiveEntries { get; init; } = Array.Empty<PacketOwnedParcelDecodedEntry>();
        public IReadOnlyList<PacketOwnedParcelDecodedEntry> ArrivalNoticeEntries { get; init; } = Array.Empty<PacketOwnedParcelDecodedEntry>();
    }

    internal static class PacketOwnedParcelPacketCodec
    {
        private const int ParcelFixedBodyLength = 0xEA;
        private const int ParcelSenderOffset = 4;
        private const int ParcelSenderLength = 13;
        private const int ParcelMesoOffset = 0x11;
        // IDA: CTabReceive::Draw reads PARCEL::dateExpire_Parcel at +0x15.
        private const int ParcelExpiryTimestampOffset = 0x15;
        // IDA: CTabReceive::Draw reads PARCEL::bQuickDelivery at +0x1D.
        private const int ParcelQuickDeliveryOffset = 0x1D;
        // IDA: CTabReceive::Draw and CParcelDlg::SetMemo read PARCEL::sMemo at +0x21.
        // The +0x1E..+0x20 gap remains an internal parcel byte seam.
        private const int ParcelQuickDeliveryReservedOffset = ParcelQuickDeliveryOffset + 1;
        private const int ParcelQuickDeliveryReservedLength = 3;
        private const int ParcelMemoOffset = 0x21;
        private const int ParcelMemoLength = ParcelFixedBodyLength - ParcelMemoOffset;
        private const int MinimumMemoCandidateLength = 4;
        private const int MinimumAttachmentHeaderLength = sizeof(byte) + sizeof(int) + sizeof(byte) + sizeof(long);
        private const byte ParcelStateReadFlag = 1 << 0;
        private const byte ParcelStateKeepFlag = 1 << 1;
        private const byte ParcelStateClaimedFlag = 1 << 2;
        private const byte ParcelStateHasItemFlag = 1 << 3;
        private const byte ParcelStateHasMesoFlag = 1 << 4;
        private static readonly Encoding ParcelTextEncoding = ResolveParcelTextEncoding();

        internal static bool TryDecodeSessionPayload(ReadOnlySpan<byte> payload, out PacketOwnedParcelSessionDecodeResult result, out string error)
        {
            result = null;
            error = null;

            using PacketReader reader = new(payload.ToArray());
            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < sizeof(byte))
            {
                error = "Parcel receive payload is missing the receive-entry count.";
                return false;
            }

            int receiveCount = reader.ReadByte();
            var receiveEntries = new List<PacketOwnedParcelDecodedEntry>(Math.Max(0, receiveCount));
            for (int i = 0; i < receiveCount; i++)
            {
                if (!TryDecodeParcelEntry(reader, out PacketOwnedParcelDecodedEntry entry, out error))
                {
                    error = $"Parcel receive payload row {i.ToString(CultureInfo.InvariantCulture)} could not be decoded. {error}";
                    return false;
                }

                receiveEntries.Add(entry);
            }

            if (stream.Length - stream.Position < sizeof(byte))
            {
                error = "Parcel receive payload is missing the arrival-notice count.";
                return false;
            }

            int arrivalCount = reader.ReadByte();
            var arrivalEntries = new List<PacketOwnedParcelDecodedEntry>(Math.Max(0, arrivalCount));
            for (int i = 0; i < arrivalCount; i++)
            {
                if (!TryDecodeParcelEntry(reader, out PacketOwnedParcelDecodedEntry entry, out error))
                {
                    error = $"Parcel arrival payload row {i.ToString(CultureInfo.InvariantCulture)} could not be decoded. {error}";
                    return false;
                }

                arrivalEntries.Add(entry);
            }

            result = new PacketOwnedParcelSessionDecodeResult
            {
                ReceiveEntries = receiveEntries,
                ArrivalNoticeEntries = arrivalEntries
            };
            return true;
        }

        internal static bool TryDecodeSingleEntryPayload(ReadOnlySpan<byte> payload, out PacketOwnedParcelDecodedEntry entry, out string error)
        {
            using PacketReader reader = new(payload.ToArray());
            Stream stream = reader.BaseStream;
            bool decoded = TryDecodeParcelEntry(reader, out entry, out error);
            if (!decoded)
            {
                return false;
            }

            long remainingLength = stream.Length - stream.Position;
            if (remainingLength > 0)
            {
                if (remainingLength > int.MaxValue)
                {
                    error = "Parcel payload trailing byte segment was larger than supported decoder limits.";
                    return false;
                }

                byte[] trailingBytes = reader.ReadBytes((int)remainingLength);
                entry = CloneWithTrailingPayloadBytes(entry, trailingBytes);
            }

            return true;
        }

        private static PacketOwnedParcelDecodedEntry CloneWithTrailingPayloadBytes(PacketOwnedParcelDecodedEntry source, byte[] trailingBytes)
        {
            if (source == null)
            {
                return null;
            }

            byte[] resolvedTrailingBytes = trailingBytes ?? Array.Empty<byte>();
            return new PacketOwnedParcelDecodedEntry
            {
                ParcelSerial = source.ParcelSerial,
                Sender = source.Sender,
                MemoText = source.MemoText,
                IsQuickDelivery = source.IsQuickDelivery,
                QuickDeliveryRawFlag = source.QuickDeliveryRawFlag,
                QuickDeliveryReservedBytes = source.QuickDeliveryReservedBytes ?? Array.Empty<byte>(),
                HasQuickDeliveryReservedState = source.HasQuickDeliveryReservedState,
                ExpirationTimestampUtc = source.ExpirationTimestampUtc,
                IsRead = source.IsRead,
                IsKept = source.IsKept,
                IsAttachmentClaimed = source.IsAttachmentClaimed,
                StateFlags = source.StateFlags,
                PostBodyItemPresenceFlag = source.PostBodyItemPresenceFlag,
                HasItemAttachment = source.HasItemAttachment,
                HasMesoAttachment = source.HasMesoAttachment,
                AttachmentItemId = source.AttachmentItemId,
                AttachmentQuantity = source.AttachmentQuantity,
                AttachmentMeso = source.AttachmentMeso,
                HasUndecodedItemAttachment = source.HasUndecodedItemAttachment,
                TrailingPayloadBytes = resolvedTrailingBytes,
                HasTrailingPayloadBytes = resolvedTrailingBytes.Length > 0
            };
        }

        private static bool TryDecodeParcelEntry(PacketReader reader, out PacketOwnedParcelDecodedEntry entry, out string error)
        {
            entry = null;
            error = null;

            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < ParcelFixedBodyLength + sizeof(byte))
            {
                error = "Packet did not contain a full PARCEL::Decode body.";
                return false;
            }

            byte[] parcelBytes = reader.ReadBytes(ParcelFixedBodyLength);
            byte postBodyByte = reader.ReadByte();
            long attachmentStartPosition = stream.Position;
            ParcelPostBodyState postBodyState = ResolvePostBodyState(postBodyByte);
            bool ambiguousReadFlag = postBodyByte == ParcelStateReadFlag;

            int itemId = 0;
            int quantity = 0;
            bool hasUndecodedItemAttachment = false;
            if (postBodyState.HasItemAttachment && !ambiguousReadFlag)
            {
                if (!TryReadItemAttachment(reader, out itemId, out quantity, out error))
                {
                    return false;
                }

                hasUndecodedItemAttachment = itemId <= 0;
            }
            else if (ambiguousReadFlag)
            {
                stream.Position = attachmentStartPosition;
                if (ShouldTreatAmbiguousPostBodyByteAsLegacyItemPresence(postBodyByte, stream, attachmentStartPosition)
                    && TryReadItemAttachment(reader, out itemId, out quantity, out _))
                {
                    postBodyState = new ParcelPostBodyState(
                        IsRead: false,
                        IsKept: false,
                        IsAttachmentClaimed: false,
                        HasItemAttachment: true,
                        HasMesoAttachment: false);
                    hasUndecodedItemAttachment = itemId <= 0;
                }
                else
                {
                    stream.Position = attachmentStartPosition;
                    postBodyState = new ParcelPostBodyState(
                        IsRead: true,
                        IsKept: false,
                        IsAttachmentClaimed: false,
                        HasItemAttachment: false,
                        HasMesoAttachment: false);
                }
            }

            string sender = ReadFixedAscii(parcelBytes, ParcelSenderOffset, ParcelSenderLength);
            string memoText = ExtractMemoText(parcelBytes, sender);
            int serial = BinaryPrimitives.ReadInt32LittleEndian(parcelBytes.AsSpan(0, sizeof(int)));
            int attachmentMeso = parcelBytes.Length >= ParcelMesoOffset + sizeof(int)
                ? Math.Max(0, BinaryPrimitives.ReadInt32LittleEndian(parcelBytes.AsSpan(ParcelMesoOffset, sizeof(int))))
                : 0;
            bool hasMesoAttachment = postBodyState.HasMesoAttachment;
            if (!hasMesoAttachment)
            {
                attachmentMeso = 0;
            }

            DateTimeOffset? expirationTimestampUtc = TryDecodeExpirationTimestamp(parcelBytes);
            byte quickDeliveryRawFlag = parcelBytes.Length > ParcelQuickDeliveryOffset
                ? parcelBytes[ParcelQuickDeliveryOffset]
                : (byte)0;
            byte[] quickDeliveryReservedBytes = ReadFixedBytes(parcelBytes, ParcelQuickDeliveryReservedOffset, ParcelQuickDeliveryReservedLength);
            bool isQuickDelivery = quickDeliveryRawFlag != 0;
            bool hasQuickDeliveryReservedState = quickDeliveryReservedBytes.Any(value => value != 0);

            entry = new PacketOwnedParcelDecodedEntry
            {
                ParcelSerial = Math.Max(0, serial),
                Sender = string.IsNullOrWhiteSpace(sender) ? "Maple Delivery Service" : sender,
                MemoText = memoText,
                IsQuickDelivery = isQuickDelivery,
                QuickDeliveryRawFlag = quickDeliveryRawFlag,
                QuickDeliveryReservedBytes = quickDeliveryReservedBytes,
                HasQuickDeliveryReservedState = hasQuickDeliveryReservedState,
                ExpirationTimestampUtc = expirationTimestampUtc,
                IsRead = postBodyState.IsRead,
                IsKept = postBodyState.IsKept,
                IsAttachmentClaimed = postBodyState.IsAttachmentClaimed,
                StateFlags = postBodyByte,
                PostBodyItemPresenceFlag = postBodyByte,
                HasItemAttachment = postBodyState.HasItemAttachment,
                HasMesoAttachment = hasMesoAttachment,
                AttachmentItemId = Math.Max(0, itemId),
                AttachmentQuantity = Math.Max(0, quantity),
                AttachmentMeso = attachmentMeso,
                HasUndecodedItemAttachment = hasUndecodedItemAttachment
            };
            return true;
        }

        private static ParcelPostBodyState ResolvePostBodyState(byte postBodyByte)
        {
            bool usesSimulatorBitfield = (postBodyByte & (ParcelStateKeepFlag | ParcelStateClaimedFlag | ParcelStateHasItemFlag | ParcelStateHasMesoFlag)) != 0;
            if (usesSimulatorBitfield)
            {
                return new ParcelPostBodyState(
                    IsRead: HasStateFlag(postBodyByte, ParcelStateReadFlag),
                    IsKept: HasStateFlag(postBodyByte, ParcelStateKeepFlag),
                    IsAttachmentClaimed: HasStateFlag(postBodyByte, ParcelStateClaimedFlag),
                    HasItemAttachment: HasStateFlag(postBodyByte, ParcelStateHasItemFlag),
                    HasMesoAttachment: HasStateFlag(postBodyByte, ParcelStateHasMesoFlag));
            }

            // MapleStory v95 PARCEL::Decode reads this byte as the GW_ItemSlotBase presence flag.
            return new ParcelPostBodyState(
                IsRead: false,
                IsKept: false,
                IsAttachmentClaimed: false,
                HasItemAttachment: postBodyByte != 0,
                HasMesoAttachment: false);
        }

        private static bool ShouldTreatAmbiguousPostBodyByteAsLegacyItemPresence(byte postBodyByte, Stream stream, long attachmentStartPosition)
        {
            if (postBodyByte != ParcelStateReadFlag
                || stream == null
                || !stream.CanSeek)
            {
                return false;
            }

            long remaining = stream.Length - attachmentStartPosition;
            if (remaining < MinimumAttachmentHeaderLength)
            {
                return false;
            }

            long cursor = stream.Position;
            try
            {
                stream.Position = attachmentStartPosition;
                int slotType = stream.ReadByte();
                if (slotType is not 1 and not 2 and not 3)
                {
                    return false;
                }

                Span<byte> itemIdBytes = stackalloc byte[sizeof(int)];
                if (stream.Read(itemIdBytes) != sizeof(int))
                {
                    return false;
                }

                int candidateItemId = BinaryPrimitives.ReadInt32LittleEndian(itemIdBytes);
                return candidateItemId > 0;
            }
            finally
            {
                stream.Position = cursor;
            }
        }

        private static bool HasStateFlag(byte stateFlags, byte flag)
        {
            return (stateFlags & flag) != 0;
        }

        private static bool TryReadItemAttachment(PacketReader reader, out int itemId, out int quantity, out string error)
        {
            itemId = 0;
            quantity = 0;
            error = null;

            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < MinimumAttachmentHeaderLength)
            {
                error = "Parcel attachment payload is too short to contain a GW_ItemSlotBase body.";
                return false;
            }

            byte slotType = reader.ReadByte();
            if (slotType is not 1 and not 2 and not 3)
            {
                error = $"Parcel attachment used unsupported GW_ItemSlotBase type {slotType.ToString(CultureInfo.InvariantCulture)}.";
                return false;
            }

            itemId = reader.ReadInt32();
            bool hasCashSerialNumber = reader.ReadByte() != 0;
            if (hasCashSerialNumber)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    error = "Parcel attachment payload ended before the cash serial number.";
                    return false;
                }

                _ = reader.ReadInt64();
            }

            if (stream.Length - stream.Position < sizeof(long))
            {
                error = "Parcel attachment payload ended before the item serial number.";
                return false;
            }

            _ = reader.ReadInt64();

            quantity = 1;
            return slotType switch
            {
                1 => TryReadEquipBody(reader, hasCashSerialNumber, out error),
                2 => TryReadBundleBody(reader, itemId, out quantity, out error),
                3 => TryReadPetBody(reader, out error),
                _ => false
            };
        }

        private static bool TryReadEquipBody(PacketReader reader, bool hasCashSerialNumber, out string error)
        {
            error = null;
            Stream stream = reader.BaseStream;
            const int equipStatsByteLength = (sizeof(byte) * 2) + (sizeof(short) * 15);
            if (stream.Length - stream.Position < equipStatsByteLength)
            {
                error = "Parcel equip attachment payload ended before the stat block.";
                return false;
            }

            stream.Position += equipStatsByteLength;
            if (!TryReadMapleString(reader, out _))
            {
                error = "Parcel equip attachment payload ended before the title string.";
                return false;
            }

            const int tailLength = sizeof(short) + (sizeof(byte) * 2) + (sizeof(int) * 3) + (sizeof(byte) * 2) + (sizeof(short) * 5);
            if (stream.Length - stream.Position < tailLength + sizeof(long) + sizeof(int))
            {
                error = "Parcel equip attachment payload ended before the tail block.";
                return false;
            }

            stream.Position += tailLength;
            if (!hasCashSerialNumber)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    error = "Parcel equip attachment payload ended before the non-cash serial number.";
                    return false;
                }

                stream.Position += sizeof(long);
            }

            stream.Position += sizeof(long) + sizeof(int);
            return true;
        }

        private static bool TryReadBundleBody(PacketReader reader, int itemId, out int quantity, out string error)
        {
            quantity = 1;
            error = null;
            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < sizeof(ushort))
            {
                error = "Parcel bundle attachment payload ended before the quantity field.";
                return false;
            }

            quantity = Math.Max(1, (int)reader.ReadUInt16());
            if (!TryReadMapleString(reader, out _))
            {
                error = "Parcel bundle attachment payload ended before the title string.";
                return false;
            }

            if (stream.Length - stream.Position < sizeof(short))
            {
                error = "Parcel bundle attachment payload ended before the attribute field.";
                return false;
            }

            _ = reader.ReadInt16();
            if (itemId / 10000 is 207 or 233)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    error = "Parcel bundle attachment payload ended before the recharge serial number.";
                    return false;
                }

                _ = reader.ReadInt64();
            }

            return true;
        }

        private static bool TryReadPetBody(PacketReader reader, out string error)
        {
            error = null;
            Stream stream = reader.BaseStream;
            const int petNameLength = 13;
            const int petTailLength = sizeof(byte) + sizeof(short) + sizeof(byte) + sizeof(long) + sizeof(short) + sizeof(ushort) + sizeof(int) + sizeof(short);
            if (stream.Length - stream.Position < petNameLength + petTailLength)
            {
                error = "Parcel pet attachment payload ended before the pet body finished.";
                return false;
            }

            stream.Position += petNameLength + petTailLength;
            return true;
        }

        private static bool TryReadMapleString(PacketReader reader, out string value)
        {
            value = string.Empty;
            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < sizeof(short))
            {
                return false;
            }

            short length = reader.ReadInt16();
            if (length < 0 || stream.Length - stream.Position < length)
            {
                return false;
            }

            value = DecodePacketText(reader.ReadBytes(length));
            return true;
        }

        private static string ReadFixedAscii(ReadOnlySpan<byte> bytes, int offset, int length)
        {
            if (offset < 0 || length <= 0 || offset >= bytes.Length)
            {
                return string.Empty;
            }

            int safeLength = Math.Min(length, bytes.Length - offset);
            ReadOnlySpan<byte> slice = bytes.Slice(offset, safeLength);
            int terminator = slice.IndexOf((byte)0);
            if (terminator >= 0)
            {
                slice = slice[..terminator];
            }

            return DecodePacketText(slice);
        }

        private static byte[] ReadFixedBytes(ReadOnlySpan<byte> bytes, int offset, int length)
        {
            if (offset < 0 || length <= 0 || offset >= bytes.Length)
            {
                return Array.Empty<byte>();
            }

            int safeLength = Math.Min(length, bytes.Length - offset);
            return bytes.Slice(offset, safeLength).ToArray();
        }

        private static DateTimeOffset? TryDecodeExpirationTimestamp(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < ParcelExpiryTimestampOffset + sizeof(long))
            {
                return null;
            }

            long rawFileTime = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(ParcelExpiryTimestampOffset, sizeof(long)));
            if (rawFileTime <= 0)
            {
                return null;
            }

            try
            {
                DateTime timestampUtc = DateTime.FromFileTimeUtc(rawFileTime);
                if (timestampUtc.Year < 2000 || timestampUtc.Year > 2100)
                {
                    return null;
                }

                return new DateTimeOffset(timestampUtc, TimeSpan.Zero);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private static string ExtractMemoText(ReadOnlySpan<byte> bytes, string sender)
        {
            string fixedMemo = ReadFixedAscii(bytes, ParcelMemoOffset, ParcelMemoLength);
            if (!string.IsNullOrWhiteSpace(fixedMemo)
                && !string.Equals(fixedMemo, sender, StringComparison.OrdinalIgnoreCase))
            {
                return fixedMemo;
            }

            string mapleStringCandidate = ExtractLongestMapleStringCandidate(bytes, sender);
            if (!string.IsNullOrWhiteSpace(mapleStringCandidate))
            {
                return mapleStringCandidate;
            }

            string best = string.Empty;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!IsPrintableAscii(bytes[i]))
                {
                    continue;
                }

                int start = i;
                while (i < bytes.Length && IsPrintableAscii(bytes[i]))
                {
                    i++;
                }

                int length = i - start;
                if (length < MinimumMemoCandidateLength)
                {
                    continue;
                }

                string candidate = Encoding.ASCII.GetString(bytes.Slice(start, length)).Trim();
                if (string.IsNullOrWhiteSpace(candidate)
                    || string.Equals(candidate, sender, StringComparison.OrdinalIgnoreCase)
                    || candidate.All(character => char.IsDigit(character)))
                {
                    continue;
                }

                if (candidate.Length > best.Length)
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static string ExtractLongestMapleStringCandidate(ReadOnlySpan<byte> bytes, string sender)
        {
            string best = string.Empty;
            for (int i = 0; i <= bytes.Length - sizeof(short); i++)
            {
                short length = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(i, sizeof(short)));
                if (length < MinimumMemoCandidateLength || length > 120)
                {
                    continue;
                }

                int start = i + sizeof(short);
                if (start + length > bytes.Length)
                {
                    continue;
                }

                ReadOnlySpan<byte> slice = bytes.Slice(start, length);
                if (!IsLikelyReadablePacketText(slice))
                {
                    continue;
                }

                string candidate = DecodePacketText(slice);
                if (string.IsNullOrWhiteSpace(candidate)
                    || string.Equals(candidate, sender, StringComparison.OrdinalIgnoreCase)
                    || candidate.All(character => char.IsDigit(character)))
                {
                    continue;
                }

                if (candidate.Length > best.Length)
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static bool IsLikelyReadablePacketText(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0)
            {
                return false;
            }

            string decoded = DecodePacketText(bytes);
            if (string.IsNullOrWhiteSpace(decoded))
            {
                return false;
            }

            int visibleCharacters = 0;
            int readableCharacters = 0;
            foreach (char character in decoded)
            {
                if (char.IsControl(character))
                {
                    continue;
                }

                visibleCharacters++;
                if (char.IsLetterOrDigit(character)
                    || char.IsWhiteSpace(character)
                    || char.IsPunctuation(character))
                {
                    readableCharacters++;
                }
            }

            if (visibleCharacters < MinimumMemoCandidateLength)
            {
                return false;
            }

            return readableCharacters >= (visibleCharacters * 3) / 4;
        }

        private static string DecodePacketText(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            int nullTerminator = bytes.IndexOf((byte)0);
            if (nullTerminator >= 0)
            {
                bytes = bytes[..nullTerminator];
            }

            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            string decoded;
            try
            {
                decoded = ParcelTextEncoding.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                decoded = Encoding.ASCII.GetString(bytes);
            }

            if (string.IsNullOrEmpty(decoded))
            {
                return string.Empty;
            }

            StringBuilder sanitized = new(decoded.Length);
            foreach (char character in decoded)
            {
                if (character == '\0')
                {
                    continue;
                }

                if (char.IsControl(character)
                    && character != '\r'
                    && character != '\n'
                    && character != '\t')
                {
                    continue;
                }

                sanitized.Append(character);
            }

            return sanitized.ToString().Trim();
        }

        private static Encoding ResolveParcelTextEncoding()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(949);
            }
            catch (ArgumentException)
            {
                return Encoding.ASCII;
            }
            catch (NotSupportedException)
            {
                return Encoding.ASCII;
            }
        }

        private static bool IsPrintableAscii(byte value)
        {
            return value is >= 32 and <= 126;
        }

        private readonly record struct ParcelPostBodyState(
            bool IsRead,
            bool IsKept,
            bool IsAttachmentClaimed,
            bool HasItemAttachment,
            bool HasMesoAttachment);
    }
}
