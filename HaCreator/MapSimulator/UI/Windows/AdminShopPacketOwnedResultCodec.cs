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
        public string TrailingPayloadSignature { get; init; } = "none";
        public byte[] TrailingPayload { get; init; } = Array.Empty<byte>();
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
                int trailingByteCount = Math.Max(0, payload.Length - sizeof(byte));
                byte[] trailingPayload = trailingByteCount > 0 ? payload[1..] : Array.Empty<byte>();
                snapshot = new AdminShopPacketOwnedResultPayloadSnapshot
                {
                    Subtype = subtype,
                    ResultCode = 0,
                    HasResultCode = false,
                    TrailingByteCount = trailingByteCount,
                    TrailingPayloadSignature = BuildPayloadSignature(trailingPayload),
                    TrailingPayload = trailingPayload
                };
                return true;
            }

            if (payload.Length < sizeof(byte) * 2)
            {
                snapshot = new AdminShopPacketOwnedResultPayloadSnapshot
                {
                    Subtype = subtype,
                    ResultCode = 0,
                    HasResultCode = false,
                    TrailingByteCount = 0,
                    TrailingPayloadSignature = "none",
                    TrailingPayload = Array.Empty<byte>()
                };
                return true;
            }

            int handledTrailingByteCount = Math.Max(0, payload.Length - (sizeof(byte) * 2));
            byte[] handledTrailingPayload = handledTrailingByteCount > 0 ? payload[2..] : Array.Empty<byte>();
            snapshot = new AdminShopPacketOwnedResultPayloadSnapshot
            {
                Subtype = subtype,
                ResultCode = payload[1],
                HasResultCode = true,
                TrailingByteCount = handledTrailingByteCount,
                TrailingPayloadSignature = BuildPayloadSignature(handledTrailingPayload),
                TrailingPayload = handledTrailingPayload
            };
            return true;
        }

        private static string BuildPayloadSignature(byte[] payload)
        {
            payload ??= Array.Empty<byte>();
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
            string head = Convert.ToHexString(payload.AsSpan(0, headByteCount));
            return string.Concat(
                payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ":",
                hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture),
                ":",
                head);
        }
    }
}
