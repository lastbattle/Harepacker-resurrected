using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AdminShopPacketOwnedOpenPayloadSnapshot
    {
        public int NpcTemplateId { get; init; }
        public int CommodityCount { get; init; }
        public bool AskItemWishlist { get; init; }
        public bool HasAskItemWishlistByte { get; init; }
        public bool IsRejectedByEmptyCatalog { get; init; }
        public int TrailingByteCount { get; init; }
        public string TrailingPayloadSignature { get; init; } = string.Empty;
        public IReadOnlyList<AdminShopDialogUI.PacketOwnedAdminShopCommoditySnapshot> Rows { get; init; }
            = Array.Empty<AdminShopDialogUI.PacketOwnedAdminShopCommoditySnapshot>();
    }

    internal static class AdminShopPacketOwnedOpenCodec
    {
        private const int HeaderSize = sizeof(int) + sizeof(ushort);
        private const int RowSize = sizeof(int) + sizeof(int) + sizeof(int) + sizeof(byte) + sizeof(ushort);

        internal static bool TryDecode(
            byte[] payload,
            out AdminShopPacketOwnedOpenPayloadSnapshot snapshot)
        {
            snapshot = null;
            payload ??= Array.Empty<byte>();
            if (payload.Length < HeaderSize)
            {
                return false;
            }

            int npcTemplateId = BitConverter.ToInt32(payload, 0);
            int itemCount = BitConverter.ToUInt16(payload, sizeof(int));
            int offset = HeaderSize;
            List<AdminShopDialogUI.PacketOwnedAdminShopCommoditySnapshot> rows = new(itemCount);
            for (int i = 0; i < itemCount; i++)
            {
                if (payload.Length - offset < RowSize)
                {
                    return false;
                }

                rows.Add(new AdminShopDialogUI.PacketOwnedAdminShopCommoditySnapshot
                {
                    SerialNumber = BitConverter.ToInt32(payload, offset),
                    ItemId = BitConverter.ToInt32(payload, offset + sizeof(int)),
                    Price = BitConverter.ToInt32(payload, offset + (sizeof(int) * 2)),
                    SaleState = payload[offset + (sizeof(int) * 3)],
                    MaxPerSlot = BitConverter.ToUInt16(payload, offset + (sizeof(int) * 3) + sizeof(byte))
                });
                offset += RowSize;
            }

            if (itemCount == 0)
            {
                ReadOnlySpan<byte> rejectedTrailingPayload = payload.AsSpan(offset);
                snapshot = new AdminShopPacketOwnedOpenPayloadSnapshot
                {
                    NpcTemplateId = Math.Max(0, npcTemplateId),
                    CommodityCount = 0,
                    AskItemWishlist = false,
                    HasAskItemWishlistByte = false,
                    IsRejectedByEmptyCatalog = true,
                    TrailingByteCount = Math.Max(0, payload.Length - offset),
                    TrailingPayloadSignature = BuildPayloadSignature(rejectedTrailingPayload),
                    Rows = rows
                };
                return true;
            }

            if (payload.Length - offset < sizeof(byte))
            {
                return false;
            }

            bool askItemWishlist = payload[offset] != 0;
            offset += sizeof(byte);
            ReadOnlySpan<byte> trailingPayload = payload.AsSpan(offset);
            snapshot = new AdminShopPacketOwnedOpenPayloadSnapshot
            {
                NpcTemplateId = Math.Max(0, npcTemplateId),
                CommodityCount = Math.Max(0, itemCount),
                AskItemWishlist = askItemWishlist,
                HasAskItemWishlistByte = true,
                IsRejectedByEmptyCatalog = false,
                TrailingByteCount = Math.Max(0, payload.Length - offset),
                TrailingPayloadSignature = BuildPayloadSignature(trailingPayload),
                Rows = rows
            };
            return true;
        }

        private static string BuildPayloadSignature(ReadOnlySpan<byte> payload)
        {
            if (payload.Length <= 0)
            {
                return "none";
            }

            const ulong fnvOffsetBasis = 14695981039346656037UL;
            const ulong fnvPrime = 1099511628211UL;
            ulong hash = fnvOffsetBasis;
            for (int i = 0; i < payload.Length; i++)
            {
                hash ^= payload[i];
                hash *= fnvPrime;
            }

            int headByteCount = Math.Min(8, payload.Length);
            string head = Convert.ToHexString(payload[..headByteCount]);
            return string.Concat(
                payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ":",
                hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture),
                ":",
                head);
        }
    }
}
