using System;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum QuestRewardRaiseInboundPacketKind
    {
        OwnerSync,
        PutItemAddResult,
        PutItemReleaseResult,
        PutItemConfirmResult,
        OwnerDestroyResult
    }

    internal sealed record QuestRewardRaiseInboundPacket(
        QuestRewardRaiseInboundPacketKind Kind,
        bool Success,
        QuestRewardRaisePacketPayload Payload,
        byte[] RawPayload);

    internal static class QuestRewardRaiseInboundPacketCodec
    {
        internal static bool TryDecode(
            QuestRewardRaiseInboundPacketKind kind,
            byte[] payload,
            out QuestRewardRaiseInboundPacket packet,
            out string error)
        {
            packet = null;
            error = null;

            if (payload == null)
            {
                error = "Raise inbound payload is missing.";
                return false;
            }

            bool hasResultFlag = kind is QuestRewardRaiseInboundPacketKind.PutItemAddResult
                or QuestRewardRaiseInboundPacketKind.PutItemReleaseResult
                or QuestRewardRaiseInboundPacketKind.PutItemConfirmResult;
            bool success = true;
            byte[] statePayload = payload;

            if (hasResultFlag)
            {
                if (payload.Length < 1)
                {
                    error = "Raise inbound result payload must include a success flag.";
                    return false;
                }

                success = payload[0] != 0;
                statePayload = payload.Length == 1 ? Array.Empty<byte>() : payload[1..];
            }

            if (!QuestRewardRaiseOutboundRequest.TryDecodePayload(statePayload, out QuestRewardRaisePacketPayload decodedPayload, out error))
            {
                return false;
            }

            packet = new QuestRewardRaiseInboundPacket(kind, success, decodedPayload, payload.ToArray());
            return true;
        }

        internal static byte[] Encode(
            QuestRewardRaiseInboundPacketKind kind,
            bool success,
            byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            if (kind is not QuestRewardRaiseInboundPacketKind.PutItemAddResult
                and not QuestRewardRaiseInboundPacketKind.PutItemReleaseResult
                and not QuestRewardRaiseInboundPacketKind.PutItemConfirmResult)
            {
                return payload;
            }

            byte[] framedPayload = new byte[payload.Length + 1];
            framedPayload[0] = success ? (byte)1 : (byte)0;
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, framedPayload, 1, payload.Length);
            }

            return framedPayload;
        }
    }
}
