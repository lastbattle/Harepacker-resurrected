using System;
using System.Collections.Generic;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AdminShopPacketOwnedWishlistSearchSnapshot
    {
        public int ServiceSessionId { get; init; } = -1;
        public int SearchSessionId { get; init; } = -1;
        public string Query { get; init; } = string.Empty;
        public string CategoryKey { get; init; } = string.Empty;
        public int PriceRangeIndex { get; init; } = -1;
        public IReadOnlyList<int> ItemIds { get; init; } = Array.Empty<int>();
        public int TrailingByteCount { get; init; }
    }

    internal static class AdminShopPacketOwnedWishlistSearchResultCodec
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("WLSR");
        private const byte Version = 1;
        private const int HeaderSize = 4 + 1 + 4 + 4 + 1;
        private const byte FlagQuery = 1 << 0;
        private const byte FlagCategory = 1 << 1;
        private const byte FlagPriceRange = 1 << 2;

        internal static bool TryDecode(
            byte[] payload,
            out AdminShopPacketOwnedWishlistSearchSnapshot snapshot)
        {
            snapshot = null;
            payload ??= Array.Empty<byte>();
            if (payload.Length < HeaderSize)
            {
                return false;
            }

            ReadOnlySpan<byte> span = payload;
            if (!span[..Magic.Length].SequenceEqual(Magic))
            {
                return false;
            }

            int offset = Magic.Length;
            byte version = span[offset++];
            if (version != Version)
            {
                return false;
            }

            int serviceSessionId = BitConverter.ToInt32(payload, offset);
            offset += sizeof(int);
            int searchSessionId = BitConverter.ToInt32(payload, offset);
            offset += sizeof(int);
            byte flags = span[offset++];

            if (!TryReadUtf8String(span, ref offset, (flags & FlagQuery) != 0, out string query))
            {
                return false;
            }

            if (!TryReadUtf8String(span, ref offset, (flags & FlagCategory) != 0, out string categoryKey))
            {
                return false;
            }

            int priceRangeIndex = -1;
            if ((flags & FlagPriceRange) != 0)
            {
                if (span.Length - offset < sizeof(short))
                {
                    return false;
                }

                priceRangeIndex = BitConverter.ToInt16(payload, offset);
                offset += sizeof(short);
            }

            if (span.Length - offset < sizeof(ushort))
            {
                return false;
            }

            int itemCount = BitConverter.ToUInt16(payload, offset);
            offset += sizeof(ushort);
            if (itemCount < 0 || span.Length - offset < itemCount * sizeof(int))
            {
                return false;
            }

            List<int> itemIds = new(itemCount);
            for (int i = 0; i < itemCount; i++)
            {
                itemIds.Add(BitConverter.ToInt32(payload, offset));
                offset += sizeof(int);
            }

            snapshot = new AdminShopPacketOwnedWishlistSearchSnapshot
            {
                ServiceSessionId = serviceSessionId,
                SearchSessionId = searchSessionId,
                Query = query ?? string.Empty,
                CategoryKey = categoryKey ?? string.Empty,
                PriceRangeIndex = priceRangeIndex,
                ItemIds = itemIds,
                TrailingByteCount = Math.Max(0, payload.Length - offset)
            };
            return true;
        }

        private static bool TryReadUtf8String(
            ReadOnlySpan<byte> span,
            ref int offset,
            bool present,
            out string value)
        {
            value = string.Empty;
            if (!present)
            {
                return true;
            }

            if (span.Length - offset < sizeof(byte))
            {
                return false;
            }

            int byteLength = span[offset++];
            if (span.Length - offset < byteLength)
            {
                return false;
            }

            if (byteLength <= 0)
            {
                value = string.Empty;
                return true;
            }

            value = Encoding.UTF8.GetString(span.Slice(offset, byteLength));
            offset += byteLength;
            return true;
        }
    }
}
