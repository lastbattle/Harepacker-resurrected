using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AdminShopPacketOwnedWishlistSearchResultRow
    {
        public int ItemId { get; init; }
        public int ResultItemId { get; init; }
        public long Price { get; init; } = long.MinValue;
        public bool? AlreadyWishlisted { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Seller { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public string CategoryKey { get; init; } = string.Empty;
        public string PriceLabel { get; init; } = string.Empty;

        public bool HasMetadata =>
            !string.IsNullOrWhiteSpace(Title)
            || !string.IsNullOrWhiteSpace(Seller)
            || !string.IsNullOrWhiteSpace(Detail)
            || !string.IsNullOrWhiteSpace(CategoryKey)
            || !string.IsNullOrWhiteSpace(PriceLabel)
            || Price != long.MinValue
            || AlreadyWishlisted.HasValue
            || ResultItemId > 0;
    }

    internal sealed class AdminShopPacketOwnedWishlistSearchSnapshot
    {
        public int ServiceSessionId { get; init; } = -1;
        public int SearchSessionId { get; init; } = -1;
        public int LocalSearchRequestId { get; init; } = -1;
        public string Query { get; init; } = string.Empty;
        public string CategoryKey { get; init; } = string.Empty;
        public int PriceRangeIndex { get; init; } = -1;
        public bool UsedFallbackRequestContext { get; init; }
        public IReadOnlyList<int> ItemIds { get; init; } = Array.Empty<int>();
        public IReadOnlyList<AdminShopPacketOwnedWishlistSearchResultRow> ResultRows { get; init; }
            = Array.Empty<AdminShopPacketOwnedWishlistSearchResultRow>();
        public bool IsStateOnlySessionSnapshot { get; init; }
        public bool IgnoredDueToSessionMismatch { get; init; }
        public int TrailingByteCount { get; init; }
    }

    internal static class AdminShopPacketOwnedWishlistSearchResultCodec
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("WLSR");
        private const byte Version1 = 1;
        private const byte Version2 = 2;
        private const int HeaderSize = 4 + 1 + 4 + 4 + 1;
        private const int LegacyHeaderSize = sizeof(int) + sizeof(int) + sizeof(ushort);
        private const byte FlagQuery = 1 << 0;
        private const byte FlagCategory = 1 << 1;
        private const byte FlagPriceRange = 1 << 2;
        private const byte RowFlagResultItemId = 1 << 0;
        private const byte RowFlagPrice = 1 << 1;
        private const byte RowFlagAlreadyWishlisted = 1 << 2;
        private const byte RowFlagTitle = 1 << 3;
        private const byte RowFlagSeller = 1 << 4;
        private const byte RowFlagDetail = 1 << 5;
        private const byte RowFlagCategoryKey = 1 << 6;
        private const byte RowFlagPriceLabel = 1 << 7;

        internal static bool TryDecode(
            byte[] payload,
            out AdminShopPacketOwnedWishlistSearchSnapshot snapshot)
        {
            snapshot = null;
            payload ??= Array.Empty<byte>();
            if (payload.Length < LegacyHeaderSize)
            {
                return false;
            }

            ReadOnlySpan<byte> span = payload;
            if (!span[..Magic.Length].SequenceEqual(Magic))
            {
                return TryDecodeLegacy(payload, out snapshot);
            }

            int offset = Magic.Length;
            byte version = span[offset++];
            if (version != Version1 && version != Version2)
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
            if (itemCount < 0)
            {
                return false;
            }

            List<AdminShopPacketOwnedWishlistSearchResultRow> rows = new(itemCount);
            if (version == Version2)
            {
                for (int i = 0; i < itemCount; i++)
                {
                    if (span.Length - offset < sizeof(int) + sizeof(byte))
                    {
                        return false;
                    }

                    int itemId = BitConverter.ToInt32(payload, offset);
                    offset += sizeof(int);
                    byte rowFlags = span[offset++];
                    int resultItemId = 0;
                    long price = long.MinValue;
                    bool? alreadyWishlisted = null;
                    string title = string.Empty;
                    string seller = string.Empty;
                    string detail = string.Empty;
                    string rowCategoryKey = string.Empty;
                    string rowPriceLabel = string.Empty;

                    if ((rowFlags & RowFlagResultItemId) != 0)
                    {
                        if (span.Length - offset < sizeof(int))
                        {
                            return false;
                        }

                        resultItemId = BitConverter.ToInt32(payload, offset);
                        offset += sizeof(int);
                    }

                    if ((rowFlags & RowFlagPrice) != 0)
                    {
                        if (span.Length - offset < sizeof(int))
                        {
                            return false;
                        }

                        price = BitConverter.ToInt32(payload, offset);
                        offset += sizeof(int);
                    }

                    if ((rowFlags & RowFlagAlreadyWishlisted) != 0)
                    {
                        if (span.Length - offset < sizeof(byte))
                        {
                            return false;
                        }

                        alreadyWishlisted = span[offset++] != 0;
                    }

                    if (!TryReadUtf8String(span, ref offset, (rowFlags & RowFlagTitle) != 0, out title)
                        || !TryReadUtf8String(span, ref offset, (rowFlags & RowFlagSeller) != 0, out seller)
                        || !TryReadUtf8String(span, ref offset, (rowFlags & RowFlagDetail) != 0, out detail)
                        || !TryReadUtf8String(span, ref offset, (rowFlags & RowFlagCategoryKey) != 0, out rowCategoryKey)
                        || !TryReadUtf8String(span, ref offset, (rowFlags & RowFlagPriceLabel) != 0, out rowPriceLabel))
                    {
                        return false;
                    }

                    rows.Add(new AdminShopPacketOwnedWishlistSearchResultRow
                    {
                        ItemId = itemId,
                        ResultItemId = resultItemId,
                        Price = price,
                        AlreadyWishlisted = alreadyWishlisted,
                        Title = title ?? string.Empty,
                        Seller = seller ?? string.Empty,
                        Detail = detail ?? string.Empty,
                        CategoryKey = rowCategoryKey ?? string.Empty,
                        PriceLabel = rowPriceLabel ?? string.Empty
                    });
                }
            }
            else
            {
                if (span.Length - offset < itemCount * sizeof(int))
                {
                    return false;
                }

                for (int i = 0; i < itemCount; i++)
                {
                    rows.Add(new AdminShopPacketOwnedWishlistSearchResultRow
                    {
                        ItemId = BitConverter.ToInt32(payload, offset)
                    });
                    offset += sizeof(int);
                }
            }

            List<int> itemIds = rows
                .Select(row => row.ResultItemId > 0 ? row.ResultItemId : row.ItemId)
                .ToList();

            snapshot = new AdminShopPacketOwnedWishlistSearchSnapshot
            {
                ServiceSessionId = serviceSessionId,
                SearchSessionId = searchSessionId,
                Query = query ?? string.Empty,
                CategoryKey = categoryKey ?? string.Empty,
                PriceRangeIndex = priceRangeIndex,
                ItemIds = itemIds,
                ResultRows = rows,
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

        // Some official-session captures only expose a compact wishlist row payload:
        // [serviceSessionId:int32][searchSessionId:int32][itemCount:uint16][itemId:int32 * itemCount]
        // Some captures also end at the 8-byte session header with no decoded row list.
        // Keep this path strict so unrelated subtype-4 tails are not decoded accidentally,
        // but preserve payload-authored session ids when row decoding is unavailable.
        private static bool TryDecodeLegacy(
            byte[] payload,
            out AdminShopPacketOwnedWishlistSearchSnapshot snapshot)
        {
            snapshot = null;
            payload ??= Array.Empty<byte>();
            if (payload.Length < sizeof(int) * 2)
            {
                return false;
            }

            int serviceSessionId = BitConverter.ToInt32(payload, 0);
            int searchSessionId = BitConverter.ToInt32(payload, sizeof(int));
            if (payload.Length < LegacyHeaderSize)
            {
                snapshot = BuildStateOnlyLegacySnapshot(
                    serviceSessionId,
                    searchSessionId,
                    payload.Length - (sizeof(int) * 2));
                return true;
            }

            int itemCount = BitConverter.ToUInt16(payload, sizeof(int) * 2);
            if (itemCount < 0 || itemCount > 1024)
            {
                snapshot = BuildStateOnlyLegacySnapshot(
                    serviceSessionId,
                    searchSessionId,
                    payload.Length - (sizeof(int) * 2));
                return true;
            }

            int expectedLength = LegacyHeaderSize + (itemCount * sizeof(int));
            if (payload.Length < expectedLength)
            {
                snapshot = BuildStateOnlyLegacySnapshot(
                    serviceSessionId,
                    searchSessionId,
                    payload.Length - (sizeof(int) * 2));
                return true;
            }

            List<AdminShopPacketOwnedWishlistSearchResultRow> rows = new(itemCount);
            int offset = LegacyHeaderSize;
            for (int i = 0; i < itemCount; i++)
            {
                int itemId = BitConverter.ToInt32(payload, offset);
                offset += sizeof(int);
                if (itemId <= 0)
                {
                    return false;
                }

                rows.Add(new AdminShopPacketOwnedWishlistSearchResultRow
                {
                    ItemId = itemId
                });
            }

            snapshot = new AdminShopPacketOwnedWishlistSearchSnapshot
            {
                ServiceSessionId = serviceSessionId,
                SearchSessionId = searchSessionId,
                Query = string.Empty,
                CategoryKey = string.Empty,
                PriceRangeIndex = -1,
                ItemIds = rows.Select(row => row.ItemId).ToList(),
                ResultRows = rows,
                IsStateOnlySessionSnapshot = false,
                TrailingByteCount = payload.Length - expectedLength
            };
            return true;
        }

        private static AdminShopPacketOwnedWishlistSearchSnapshot BuildStateOnlyLegacySnapshot(
            int serviceSessionId,
            int searchSessionId,
            int trailingByteCount)
        {
            return new AdminShopPacketOwnedWishlistSearchSnapshot
            {
                ServiceSessionId = serviceSessionId,
                SearchSessionId = searchSessionId,
                Query = string.Empty,
                CategoryKey = string.Empty,
                PriceRangeIndex = -1,
                ItemIds = Array.Empty<int>(),
                ResultRows = Array.Empty<AdminShopPacketOwnedWishlistSearchResultRow>(),
                IsStateOnlySessionSnapshot = true,
                TrailingByteCount = Math.Max(0, trailingByteCount)
            };
        }
    }
}
