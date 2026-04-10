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

    internal static class PacketOwnedMechanicRepeatSkillRuntime
    {
        public const int RepeatSkillModeEndAckPacketType = 1020;
        public const int Sg88ManualAttackConfirmPacketType = 1021;
        public const int SkillEffectRequestOpcode = 71;

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
