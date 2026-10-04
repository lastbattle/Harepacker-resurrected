using System;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum UserInfoCollectionClaimResultCode : byte
    {
        Success = 0,
        NoPendingSummary = 1,
        Unavailable = 2,
        Rejected = 3
    }

    internal readonly record struct UserInfoCollectionClaimResult(
        UserInfoCollectionClaimResultCode ResultCode,
        int CharacterId,
        int SnapshotFingerprint,
        string NoticeText);

    internal static class UserInfoCollectionClaimResultCodec
    {
        internal static bool TryDecode(
            ReadOnlySpan<byte> payload,
            out UserInfoCollectionClaimResult result,
            out string error)
        {
            result = default;
            error = null;

            if (payload.Length < sizeof(byte) + sizeof(int) + sizeof(int))
            {
                error = "CUIUserInfo BtArrayGet result payload is missing result code, character id, or collection fingerprint.";
                return false;
            }

            int offset = 0;
            var resultCode = (UserInfoCollectionClaimResultCode)payload[offset++];
            int characterId = ReadInt32LittleEndian(payload, offset);
            offset += sizeof(int);
            int snapshotFingerprint = ReadInt32LittleEndian(payload, offset);
            offset += sizeof(int);

            if (characterId <= 0)
            {
                error = $"CUIUserInfo BtArrayGet result character id {characterId} is invalid.";
                return false;
            }

            string noticeText = null;
            if (offset < payload.Length)
            {
                if (!TryReadString16(payload, ref offset, out noticeText, out error))
                {
                    return false;
                }
            }

            if (offset != payload.Length)
            {
                error = $"CUIUserInfo BtArrayGet result payload has {payload.Length - offset} unread byte(s).";
                return false;
            }

            result = new UserInfoCollectionClaimResult(resultCode, characterId, snapshotFingerprint, noticeText);
            return true;
        }

        private static bool TryReadString16(ReadOnlySpan<byte> payload, ref int offset, out string value, out string error)
        {
            value = null;
            error = null;

            if (offset + sizeof(ushort) > payload.Length)
            {
                error = "CUIUserInfo BtArrayGet result notice is missing a client string length.";
                return false;
            }

            int length = payload[offset] | (payload[offset + 1] << 8);
            offset += sizeof(ushort);
            if (offset + length > payload.Length)
            {
                error = "CUIUserInfo BtArrayGet result notice has an invalid client string length.";
                return false;
            }

            value = Encoding.Default.GetString(payload[offset..(offset + length)]);
            offset += length;
            return true;
        }

        private static int ReadInt32LittleEndian(ReadOnlySpan<byte> payload, int offset)
        {
            return payload[offset]
                | (payload[offset + 1] << 8)
                | (payload[offset + 2] << 16)
                | (payload[offset + 3] << 24);
        }
    }
}
