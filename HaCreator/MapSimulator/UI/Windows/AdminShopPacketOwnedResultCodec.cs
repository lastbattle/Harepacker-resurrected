using System;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AdminShopPacketOwnedResultPayloadSnapshot
    {
        public byte Subtype { get; init; }
        public byte ResultCode { get; init; }
        public bool HasResultCode { get; init; }
        public int TrailingByteCount { get; init; }
    }

    internal static class AdminShopPacketOwnedResultCodec
    {
        internal static bool TryDecode(
            byte[] payload,
            out AdminShopPacketOwnedResultPayloadSnapshot snapshot)
        {
            snapshot = null;
            payload ??= Array.Empty<byte>();
            if (payload.Length < sizeof(byte))
            {
                return false;
            }

            byte subtype = payload[0];
            if (!AdminShopDialogClientParityText.HandlesResultSubtype(subtype))
            {
                snapshot = new AdminShopPacketOwnedResultPayloadSnapshot
                {
                    Subtype = subtype,
                    ResultCode = 0,
                    HasResultCode = false,
                    TrailingByteCount = Math.Max(0, payload.Length - sizeof(byte))
                };
                return true;
            }

            if (payload.Length < sizeof(byte) * 2)
            {
                return false;
            }

            snapshot = new AdminShopPacketOwnedResultPayloadSnapshot
            {
                Subtype = subtype,
                ResultCode = payload[1],
                HasResultCode = true,
                TrailingByteCount = Math.Max(0, payload.Length - (sizeof(byte) * 2))
            };
            return true;
        }
    }
}
