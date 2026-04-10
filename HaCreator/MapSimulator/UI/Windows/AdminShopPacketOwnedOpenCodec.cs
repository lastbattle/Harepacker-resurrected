using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AdminShopPacketOwnedOpenPayloadSnapshot
    {
        public int NpcTemplateId { get; init; }
        public int CommodityCount { get; init; }
        public bool AskItemWishlist { get; init; }
        public int TrailingByteCount { get; init; }
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

            if (payload.Length - offset < sizeof(byte))
            {
                return false;
            }

            bool askItemWishlist = payload[offset] != 0;
            offset += sizeof(byte);
            snapshot = new AdminShopPacketOwnedOpenPayloadSnapshot
            {
                NpcTemplateId = Math.Max(0, npcTemplateId),
                CommodityCount = Math.Max(0, itemCount),
                AskItemWishlist = askItemWishlist,
                TrailingByteCount = Math.Max(0, payload.Length - offset),
                Rows = rows
            };
            return true;
        }
    }
}
