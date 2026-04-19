using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    public readonly record struct PacketOwnedRepeatSkillModeEndAck(
        int SkillId,
        int ReturnSkillId,
        int RequestedAt);

    public readonly record struct PacketOwnedSg88ManualAttackConfirm(
        int SummonObjectId,
        int RequestedAt);

    public readonly record struct PacketOwnedSkillEffectRequest(
        int Opcode,
        int SkillId,
        int SkillLevel,
        bool SendLocal,
        byte[] Payload);

    public readonly record struct PacketOwnedSg88FirstUseRequest(
        int Opcode,
        int SkillId,
        int SkillLevel,
        int RequestTime,
        short X,
        short Y,
        byte MoveActionLowBit,
        byte VecCtrlState,
        byte[] Payload,
        byte[] RawPacket);

    internal static class PacketOwnedMechanicRepeatSkillRuntime
    {
        public const int RepeatSkillModeEndAckPacketType = 1020;
        public const int Sg88ManualAttackConfirmPacketType = 1021;
        public const int SkillEffectRequestOpcode = 71;
        public const int Sg88FirstUseSummonOpcode = 103;
        public const int Sg88SkillId = 35121003;

        public static bool TryEncodeSkillEffectRequestPayload(
            int skillId,
            int skillLevel,
            bool sendLocal,
            out byte[] payload,
            out string error)
        {
            payload = Array.Empty<byte>();
            error = "Skill-effect request requires a positive skill id.";
            if (skillId <= 0)
            {
                return false;
            }

            if (skillLevel < byte.MinValue || skillLevel > byte.MaxValue)
            {
                error = "Skill-effect request skill level must fit in one byte.";
                return false;
            }

            payload = new byte[sizeof(int) + 2];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), skillId);
            payload[sizeof(int)] = (byte)skillLevel;
            payload[sizeof(int) + 1] = sendLocal ? (byte)1 : (byte)0;
            error = null;
            return true;
        }

        public static bool TryCreateSkillEffectRequest(
            int skillId,
            int skillLevel,
            bool sendLocal,
            out PacketOwnedSkillEffectRequest request,
            out string error)
        {
            request = default;
            if (!TryEncodeSkillEffectRequestPayload(
                    skillId,
                    skillLevel,
                    sendLocal,
                    out byte[] payload,
                    out error))
            {
                return false;
            }

            request = new PacketOwnedSkillEffectRequest(
                SkillEffectRequestOpcode,
                skillId,
                skillLevel,
                sendLocal,
                payload);
            return true;
        }

        public static bool TryEncodeSg88FirstUseRequestPayload(
            int requestTime,
            int skillLevel,
            short x,
            short y,
            byte moveActionLowBit,
            byte vecCtrlState,
            out byte[] payload,
            out string error)
        {
            payload = Array.Empty<byte>();
            error = "SG-88 first-use request skill level must fit in one byte.";
            if (skillLevel < byte.MinValue || skillLevel > byte.MaxValue)
            {
                return false;
            }

            payload = new byte[(sizeof(int) * 2) + 1 + (sizeof(short) * 2) + 2];
            int offset = 0;
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset, sizeof(int)), requestTime);
            offset += sizeof(int);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset, sizeof(int)), Sg88SkillId);
            offset += sizeof(int);
            payload[offset++] = (byte)skillLevel;
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(offset, sizeof(short)), x);
            offset += sizeof(short);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(offset, sizeof(short)), y);
            offset += sizeof(short);
            payload[offset++] = (byte)(moveActionLowBit & 1);
            payload[offset] = vecCtrlState;
            error = null;
            return true;
        }

        public static bool TryCreateSg88FirstUseRequest(
            int requestTime,
            int skillLevel,
            short x,
            short y,
            byte moveActionLowBit,
            byte vecCtrlState,
            out PacketOwnedSg88FirstUseRequest request,
            out string error)
        {
            request = default;
            if (!TryEncodeSg88FirstUseRequestPayload(
                    requestTime,
                    skillLevel,
                    x,
                    y,
                    moveActionLowBit,
                    vecCtrlState,
                    out byte[] payload,
                    out error))
            {
                return false;
            }

            byte[] rawPacket = new byte[sizeof(ushort) + payload.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)), Sg88FirstUseSummonOpcode);
            payload.CopyTo(rawPacket, sizeof(ushort));
            request = new PacketOwnedSg88FirstUseRequest(
                Sg88FirstUseSummonOpcode,
                Sg88SkillId,
                skillLevel,
                requestTime,
                x,
                y,
                (byte)(moveActionLowBit & 1),
                vecCtrlState,
                payload,
                rawPacket);
            return true;
        }

        public static bool TryDecodeSg88FirstUseRequestPayload(
            byte[] payload,
            out PacketOwnedSg88FirstUseRequest request,
            out string error)
        {
            return TryDecodeSg88FirstUseRequestPayload(
                payload,
                requireCanonicalMoveActionLowBit: true,
                out request,
                out error);
        }

        public static bool TryDecodeSg88FirstUseRawPacket(
            byte[] rawPacket,
            out PacketOwnedSg88FirstUseRequest request,
            out string error)
        {
            return TryDecodeSg88FirstUseRawPacketCore(
                rawPacket,
                requireCanonicalMoveActionLowBit: true,
                out request,
                out error);
        }

        public static bool TryDecodeSg88FirstUseRawPacketWithReplayParity(
            byte[] rawPacket,
            out PacketOwnedSg88FirstUseRequest request,
            out bool replayParityMatched,
            out string error)
        {
            request = default;
            replayParityMatched = false;
            if (!TryDecodeSg88FirstUseRawPacketCore(
                    rawPacket,
                    requireCanonicalMoveActionLowBit: false,
                    out PacketOwnedSg88FirstUseRequest decoded,
                    out error))
            {
                return false;
            }

            if (!TryCreateSg88FirstUseRequest(
                    decoded.RequestTime,
                    decoded.SkillLevel,
                    decoded.X,
                    decoded.Y,
                    decoded.MoveActionLowBit,
                    decoded.VecCtrlState,
                    out PacketOwnedSg88FirstUseRequest rebuilt,
                    out string rebuildError))
            {
                error = $"SG-88 first-use replay parity failed to rebuild from decoded fields: {rebuildError}";
                return false;
            }

            replayParityMatched = rawPacket.AsSpan().SequenceEqual(rebuilt.RawPacket);
            request = decoded;
            error = replayParityMatched
                ? null
                : BuildSg88FirstUseReplayParityMismatchDetail(rawPacket, rebuilt.RawPacket);
            return true;
        }

        private static bool TryDecodeSg88FirstUseRawPacketCore(
            byte[] rawPacket,
            bool requireCanonicalMoveActionLowBit,
            out PacketOwnedSg88FirstUseRequest request,
            out string error)
        {
            request = default;
            error = "SG-88 first-use raw packet is missing.";
            int minimumLength = sizeof(ushort) + ((sizeof(int) * 2) + 1 + (sizeof(short) * 2) + 2);
            if (rawPacket == null || rawPacket.Length != minimumLength)
            {
                return false;
            }

            ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)));
            if (opcode != Sg88FirstUseSummonOpcode)
            {
                error = $"SG-88 first-use raw packet opcode must be {Sg88FirstUseSummonOpcode}, got {opcode}.";
                return false;
            }

            byte[] payload = new byte[rawPacket.Length - sizeof(ushort)];
            Buffer.BlockCopy(rawPacket, sizeof(ushort), payload, 0, payload.Length);
            if (!TryDecodeSg88FirstUseRequestPayload(
                    payload,
                    requireCanonicalMoveActionLowBit,
                    out PacketOwnedSg88FirstUseRequest decoded,
                    out error))
            {
                return false;
            }

            request = decoded with { RawPacket = (byte[])rawPacket.Clone() };
            return true;
        }

        private static bool TryDecodeSg88FirstUseRequestPayload(
            byte[] payload,
            bool requireCanonicalMoveActionLowBit,
            out PacketOwnedSg88FirstUseRequest request,
            out string error)
        {
            request = default;
            error = "SG-88 first-use request payload is missing.";
            if (payload == null || payload.Length != ((sizeof(int) * 2) + 1 + (sizeof(short) * 2) + 2))
            {
                return false;
            }

            try
            {
                int offset = 0;
                int requestTime = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int)));
                offset += sizeof(int);
                int skillId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int)));
                offset += sizeof(int);
                byte skillLevelByte = payload[offset++];
                short x = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset, sizeof(short)));
                offset += sizeof(short);
                short y = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset, sizeof(short)));
                offset += sizeof(short);
                byte rawMoveActionByte = payload[offset++];
                byte moveActionLowBit = (byte)(rawMoveActionByte & 1);
                byte vecCtrlState = payload[offset];

                if (skillId != Sg88SkillId)
                {
                    error = $"SG-88 first-use payload skill id must be {Sg88SkillId}, got {skillId}.";
                    return false;
                }

                if (requireCanonicalMoveActionLowBit && (rawMoveActionByte & 0xFE) != 0)
                {
                    error = "SG-88 first-use payload move-action flag must keep only the low bit.";
                    return false;
                }

                byte[] rawPacket = new byte[sizeof(ushort) + payload.Length];
                BinaryPrimitives.WriteUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)), Sg88FirstUseSummonOpcode);
                payload.CopyTo(rawPacket, sizeof(ushort));
                request = new PacketOwnedSg88FirstUseRequest(
                    Sg88FirstUseSummonOpcode,
                    skillId,
                    skillLevelByte,
                    requestTime,
                    x,
                    y,
                    moveActionLowBit,
                    vecCtrlState,
                    (byte[])payload.Clone(),
                    rawPacket);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException)
            {
                error = $"SG-88 first-use payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static string BuildSg88FirstUseReplayParityMismatchDetail(byte[] observedRawPacket, byte[] rebuiltRawPacket)
        {
            if (observedRawPacket == null || rebuiltRawPacket == null)
            {
                return "SG-88 first-use replay parity mismatch between observed raw packet and rebuilt request packet.";
            }

            int comparedLength = Math.Min(observedRawPacket.Length, rebuiltRawPacket.Length);
            for (int i = 0; i < comparedLength; i++)
            {
                if (observedRawPacket[i] == rebuiltRawPacket[i])
                {
                    continue;
                }

                return $"SG-88 first-use replay parity mismatch at byteIndex={i} observed=0x{observedRawPacket[i]:X2} rebuilt=0x{rebuiltRawPacket[i]:X2}.";
            }

            return $"SG-88 first-use replay parity length mismatch observedLen={observedRawPacket.Length} rebuiltLen={rebuiltRawPacket.Length}.";
        }

        public static bool TryDecodeRepeatSkillModeEndAck(
            byte[] payload,
            out PacketOwnedRepeatSkillModeEndAck ack,
            out string error)
        {
            ack = default;
            error = "Repeat-skill mode-end ack payload is missing.";
            if (payload == null || payload.Length < (sizeof(int) * 3))
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                int skillId = reader.ReadInt32();
                int returnSkillId = reader.ReadInt32();
                int requestedAt = reader.ReadInt32();
                if (stream.Position != stream.Length)
                {
                    error = $"Repeat-skill mode-end ack payload has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                if (skillId <= 0)
                {
                    error = "Repeat-skill mode-end ack payload must include a positive skill id.";
                    return false;
                }

                if (returnSkillId <= 0)
                {
                    error = "Repeat-skill mode-end ack payload must include a positive return skill id.";
                    return false;
                }

                if (requestedAt == int.MinValue)
                {
                    error = "Repeat-skill mode-end ack payload must include the original request tick.";
                    return false;
                }

                ack = new PacketOwnedRepeatSkillModeEndAck(skillId, returnSkillId, requestedAt);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                error = $"Repeat-skill mode-end ack payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        public static bool TryDecodeSg88ManualAttackConfirm(
            byte[] payload,
            out PacketOwnedSg88ManualAttackConfirm confirm,
            out string error)
        {
            confirm = default;
            error = "SG-88 manual-attack confirm payload is missing.";
            if (payload == null || payload.Length < (sizeof(int) * 2))
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                int summonObjectId = reader.ReadInt32();
                int requestedAt = reader.ReadInt32();
                if (stream.Position != stream.Length)
                {
                    error = $"SG-88 manual-attack confirm payload has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                if (summonObjectId <= 0)
                {
                    error = "SG-88 manual-attack confirm payload must include a positive summon object id.";
                    return false;
                }

                if (requestedAt == int.MinValue)
                {
                    error = "SG-88 manual-attack confirm payload must include the original request tick.";
                    return false;
                }

                confirm = new PacketOwnedSg88ManualAttackConfirm(summonObjectId, requestedAt);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                error = $"SG-88 manual-attack confirm payload could not be decoded: {ex.Message}";
                return false;
            }
        }
    }
}
