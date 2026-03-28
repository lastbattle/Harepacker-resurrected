using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.Text;

namespace HaCreator.MapSimulator.Pools
{
    public enum RemoteUserPacketType
    {
        UserEnterField = 179,
        UserLeaveField = 180,
        UserMove = 181,
        UserMoveAction = 182,
        UserHelper = 183,
        UserBattlefieldTeam = 184,
        UserPortableChair = 210,
        UserMount = 211,
        UserPreparedSkill = 212,
        UserPreparedSkillClear = 213,
        UserMeleeAttack = 214
    }

    public readonly record struct RemoteUserEnterFieldPacket(
        int CharacterId,
        string Name,
        LoginAvatarLook AvatarLook,
        short X,
        short Y,
        bool FacingRight,
        string ActionName,
        bool IsVisibleInWorld,
        int? PortableChairItemId);

    public readonly record struct RemoteUserLeaveFieldPacket(int CharacterId);

    public readonly record struct RemoteUserMovePacket(int CharacterId, PlayerMovementSyncSnapshot Snapshot, byte MoveAction);

    public readonly record struct RemoteUserMoveActionPacket(int CharacterId, byte MoveAction);

    public readonly record struct RemoteUserPortableChairPacket(int CharacterId, int? ChairItemId);

    public readonly record struct RemoteUserMountPacket(int CharacterId, int? TamingMobItemId);

    public readonly record struct RemoteUserPreparedSkillPacket(
        int CharacterId,
        int SkillId,
        int DurationMs,
        int GaugeDurationMs,
        int MaxHoldDurationMs,
        bool IsKeydownSkill,
        bool IsHolding,
        bool ShowText,
        string SkinKey,
        string SkillName);

    public readonly record struct RemoteUserPreparedSkillClearPacket(int CharacterId);

    public readonly record struct RemoteUserMeleeAttackPacket(
        int CharacterId,
        int SkillId,
        int MasteryPercent,
        int ChargeSkillId,
        bool? FacingRight,
        string ActionName);

    public readonly record struct RemoteUserHelperPacket(int CharacterId, MinimapUI.HelperMarkerType? MarkerType, bool ShowDirectionOverlay);

    public readonly record struct RemoteUserBattlefieldTeamPacket(int CharacterId, int? TeamId);

    public static class RemoteUserPacketCodec
    {
        public static bool TryParseEnterField(ReadOnlySpan<byte> payload, out RemoteUserEnterFieldPacket packet, out string error)
        {
            packet = default;
            error = null;

            if (TryParseCompactEnterField(payload, out packet, out error))
            {
                return true;
            }

            string compactError = error;
            if (TryParseOfficialEnterField(payload, out packet, out error))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(compactError) && !string.IsNullOrWhiteSpace(error))
            {
                error = $"{error} Compact parse also failed: {compactError}";
            }

            return false;
        }

        private static bool TryParseCompactEnterField(ReadOnlySpan<byte> payload, out RemoteUserEnterFieldPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                bool facingRight = reader.ReadByte() == 0;
                bool isVisibleInWorld = reader.ReadByte() != 0;
                string name = reader.ReadString8();
                string actionName = reader.ReadString8();
                int avatarLookLength = reader.ReadInt32();
                if (avatarLookLength < 0 || !reader.CanRead(avatarLookLength))
                {
                    error = $"Remote user enter packet AvatarLook length {avatarLookLength} is invalid.";
                    return false;
                }

                byte[] avatarLookPayload = reader.ReadBytes(avatarLookLength);
                if (!LoginAvatarLookCodec.TryDecode(avatarLookPayload, out LoginAvatarLook avatarLook, out string avatarError))
                {
                    error = avatarError ?? "Remote user enter packet AvatarLook payload could not be decoded.";
                    return false;
                }

                packet = new RemoteUserEnterFieldPacket(characterId, name, avatarLook, x, y, facingRight, actionName, isVisibleInWorld, null);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryParseOfficialEnterField(ReadOnlySpan<byte> payload, out RemoteUserEnterFieldPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                reader.ReadByte();
                string name = reader.ReadString16();
                reader.ReadString16();
                reader.ReadBytes(6);

                int avatarLookOffset = FindOfficialAvatarLookOffset(payload, reader.Offset, out error);
                if (avatarLookOffset < 0)
                {
                    return false;
                }

                var avatarReader = new PacketReader(payload.Slice(avatarLookOffset));
                if (!LoginAvatarLookCodec.TryDecode(avatarReader.ReadRemainingBytes(), out LoginAvatarLook avatarLook, out string avatarError))
                {
                    error = avatarError ?? "Remote user official enter packet AvatarLook payload could not be decoded.";
                    return false;
                }

                avatarReader = new PacketReader(payload.Slice(avatarLookOffset));
                byte[] avatarPayload = avatarReader.ReadRemainingBytes();
                if (!LoginAvatarLookCodec.TryDecode(avatarPayload, out avatarLook, out avatarError))
                {
                    error = avatarError ?? "Remote user official enter packet AvatarLook payload could not be decoded.";
                    return false;
                }

                int consumedAvatarBytes = LoginAvatarLookCodec.Encode(avatarLook).Length;
                int remainingBytes = payload.Length - avatarLookOffset - consumedAvatarBytes;
                if (remainingBytes != OfficialEnterFieldSuffixLength)
                {
                    error = $"Remote user official enter packet AvatarLook left {remainingBytes} trailing bytes; expected {OfficialEnterFieldSuffixLength}.";
                    return false;
                }

                avatarReader = new PacketReader(payload.Slice(avatarLookOffset + consumedAvatarBytes));
                avatarReader.ReadInt32();
                avatarReader.ReadInt32();
                avatarReader.ReadInt32();
                avatarReader.ReadInt32();
                avatarReader.ReadInt32();
                int portableChairItemId = avatarReader.ReadInt32();
                short x = avatarReader.ReadInt16();
                short y = avatarReader.ReadInt16();
                byte moveAction = avatarReader.ReadByte();

                packet = new RemoteUserEnterFieldPacket(
                    characterId,
                    string.IsNullOrWhiteSpace(name) ? $"Remote{characterId}" : name.Trim(),
                    avatarLook,
                    x,
                    y,
                    DecodeFacingRight(moveAction),
                    ResolveActionNameFromMoveAction(moveAction, portableChairItemId),
                    true,
                    portableChairItemId > 0 ? portableChairItemId : null);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseLeaveField(ReadOnlySpan<byte> payload, out RemoteUserLeaveFieldPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (payload.Length != sizeof(int))
            {
                error = $"Remote user leave packet expects 4 bytes but received {payload.Length}.";
                return false;
            }

            int characterId = payload[0]
                | (payload[1] << 8)
                | (payload[2] << 16)
                | (payload[3] << 24);
            packet = new RemoteUserLeaveFieldPacket(characterId);
            return true;
        }

        public static bool TryParseMove(ReadOnlySpan<byte> payload, int currentTime, out RemoteUserMovePacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                if (!TryDecodeMoveSnapshot(ref reader, currentTime, out PlayerMovementSyncSnapshot snapshot, out byte moveAction))
                {
                    error = $"Remote user move packet for {characterId} could not be decoded.";
                    return false;
                }

                packet = new RemoteUserMovePacket(characterId, snapshot, moveAction);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseMoveAction(ReadOnlySpan<byte> payload, out RemoteUserMoveActionPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (payload.Length != sizeof(int) + sizeof(byte))
            {
                error = $"Remote user move-action packet expects 5 bytes but received {payload.Length}.";
                return false;
            }

            int characterId = payload[0]
                | (payload[1] << 8)
                | (payload[2] << 16)
                | (payload[3] << 24);
            packet = new RemoteUserMoveActionPacket(characterId, payload[4]);
            return true;
        }

        public static bool TryParsePortableChair(ReadOnlySpan<byte> payload, out RemoteUserPortableChairPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (!TryParseOptionalItemPacket(payload, "portable-chair", out int characterId, out int? itemId, out error))
            {
                return false;
            }

            packet = new RemoteUserPortableChairPacket(characterId, itemId);
            return true;
        }

        public static bool TryParseMount(ReadOnlySpan<byte> payload, out RemoteUserMountPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (!TryParseOptionalItemPacket(payload, "mount", out int characterId, out int? itemId, out error))
            {
                return false;
            }

            packet = new RemoteUserMountPacket(characterId, itemId);
            return true;
        }

        public static bool TryParsePreparedSkill(ReadOnlySpan<byte> payload, out RemoteUserPreparedSkillPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int skillId = reader.ReadInt32();
                int durationMs = reader.ReadInt32();
                int gaugeDurationMs = reader.ReadInt32();
                int maxHoldDurationMs = reader.ReadInt32();
                byte flags = reader.ReadByte();
                string skinKey = reader.ReadString8();
                string skillName = reader.ReadString8();
                packet = new RemoteUserPreparedSkillPacket(
                    characterId,
                    skillId,
                    durationMs,
                    gaugeDurationMs,
                    maxHoldDurationMs,
                    (flags & 0x01) != 0,
                    (flags & 0x02) != 0,
                    (flags & 0x04) != 0,
                    skinKey,
                    skillName);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParsePreparedSkillClear(ReadOnlySpan<byte> payload, out RemoteUserPreparedSkillClearPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (!TryParseLeaveField(payload, out RemoteUserLeaveFieldPacket clearPacket, out error))
            {
                return false;
            }

            packet = new RemoteUserPreparedSkillClearPacket(clearPacket.CharacterId);
            return true;
        }

        public static bool TryParseMeleeAttack(ReadOnlySpan<byte> payload, out RemoteUserMeleeAttackPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload);
                int characterId = reader.ReadInt32();
                int skillId = reader.ReadInt32();
                int masteryPercent = reader.ReadInt32();
                int chargeSkillId = reader.ReadInt32();
                byte facingRaw = reader.ReadByte();
                bool? facingRight = facingRaw switch
                {
                    0 => false,
                    1 => true,
                    byte.MaxValue => null,
                    _ => null
                };
                if (facingRaw != 0 && facingRaw != 1 && facingRaw != byte.MaxValue)
                {
                    error = $"Remote user melee packet facing value {facingRaw} is not recognized.";
                    return false;
                }

                string actionName = reader.ReadString8();
                packet = new RemoteUserMeleeAttackPacket(characterId, skillId, masteryPercent, chargeSkillId, facingRight, actionName);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseHelper(ReadOnlySpan<byte> payload, out RemoteUserHelperPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (payload.Length != sizeof(int) + sizeof(byte) + sizeof(byte))
            {
                error = $"Remote user helper packet expects 6 bytes but received {payload.Length}.";
                return false;
            }

            int characterId = payload[0]
                | (payload[1] << 8)
                | (payload[2] << 16)
                | (payload[3] << 24);
            byte markerRaw = payload[4];
            MinimapUI.HelperMarkerType? markerType = markerRaw == byte.MaxValue
                ? null
                : Enum.IsDefined(typeof(MinimapUI.HelperMarkerType), (int)markerRaw)
                    ? (MinimapUI.HelperMarkerType)markerRaw
                    : null;
            if (markerRaw != byte.MaxValue && !markerType.HasValue)
            {
                error = $"Remote user helper marker value {markerRaw} is not recognized.";
                return false;
            }

            packet = new RemoteUserHelperPacket(characterId, markerType, payload[5] != 0);
            return true;
        }

        public static bool TryParseBattlefieldTeam(ReadOnlySpan<byte> payload, out RemoteUserBattlefieldTeamPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (payload.Length != sizeof(int) * 2)
            {
                error = $"Remote user Battlefield team packet expects 8 bytes but received {payload.Length}.";
                return false;
            }

            int characterId = payload[0]
                | (payload[1] << 8)
                | (payload[2] << 16)
                | (payload[3] << 24);
            int teamId = payload[4]
                | (payload[5] << 8)
                | (payload[6] << 16)
                | (payload[7] << 24);
            packet = new RemoteUserBattlefieldTeamPacket(characterId, teamId < 0 ? null : teamId);
            return true;
        }

        private static bool TryParseOptionalItemPacket(ReadOnlySpan<byte> payload, string packetName, out int characterId, out int? itemId, out string error)
        {
            error = null;
            characterId = 0;
            itemId = null;
            if (payload.Length != sizeof(int) * 2)
            {
                error = $"Remote user {packetName} packet expects 8 bytes but received {payload.Length}.";
                return false;
            }

            characterId = payload[0]
                | (payload[1] << 8)
                | (payload[2] << 16)
                | (payload[3] << 24);
            int rawItemId = payload[4]
                | (payload[5] << 8)
                | (payload[6] << 16)
                | (payload[7] << 24);
            itemId = rawItemId <= 0 ? null : rawItemId;
            return true;
        }

        private const int OfficialEnterFieldSuffixLength = (sizeof(int) * 6) + (sizeof(short) * 2) + sizeof(byte);

        private static int FindOfficialAvatarLookOffset(ReadOnlySpan<byte> payload, int searchStartOffset, out string error)
        {
            error = null;
            int firstCandidate = searchStartOffset + 16 + sizeof(short);
            int lastCandidate = payload.Length - OfficialEnterFieldSuffixLength;
            if (firstCandidate > lastCandidate)
            {
                error = "Remote user official enter packet is too short to contain remote secondary stats, AvatarLook, and spawn suffix.";
                return -1;
            }

            int resolvedOffset = -1;
            for (int avatarLookOffset = firstCandidate; avatarLookOffset <= lastCandidate; avatarLookOffset++)
            {
                byte[] avatarPayload = payload.Slice(avatarLookOffset).ToArray();
                if (!LoginAvatarLookCodec.TryDecode(avatarPayload, out LoginAvatarLook avatarLook, out _))
                {
                    continue;
                }

                int consumedAvatarBytes = LoginAvatarLookCodec.Encode(avatarLook).Length;
                if (payload.Length - avatarLookOffset - consumedAvatarBytes != OfficialEnterFieldSuffixLength)
                {
                    continue;
                }

                if (resolvedOffset >= 0)
                {
                    error = "Remote user official enter packet AvatarLook boundary is ambiguous.";
                    return -1;
                }

                resolvedOffset = avatarLookOffset;
            }

            if (resolvedOffset < 0)
            {
                error = "Remote user official enter packet AvatarLook boundary could not be resolved after the remote secondary-stat payload.";
            }

            return resolvedOffset;
        }

        private static bool DecodeFacingRight(byte moveAction)
        {
            return (moveAction & 1) == 0;
        }

        private static string ResolveActionNameFromMoveAction(byte moveAction, int portableChairItemId)
        {
            if (portableChairItemId > 0)
            {
                return CharacterPart.GetActionString(CharacterAction.Sit);
            }

            return (moveAction >> 1) switch
            {
                1 => CharacterPart.GetActionString(CharacterAction.Walk1),
                4 => CharacterPart.GetActionString(CharacterAction.Alert),
                5 => CharacterPart.GetActionString(CharacterAction.Jump),
                6 => CharacterPart.GetActionString(CharacterAction.Sit),
                17 => CharacterPart.GetActionString(CharacterAction.Ladder),
                18 => CharacterPart.GetActionString(CharacterAction.Rope),
                _ => CharacterPart.GetActionString(CharacterAction.Stand1)
            };
        }

        private static bool TryDecodeMoveSnapshot(ref PacketReader reader, int currentTime, out PlayerMovementSyncSnapshot snapshot, out byte moveAction)
        {
            snapshot = null;
            moveAction = 0;

            if (!reader.CanRead(sizeof(short) * 4 + sizeof(byte)))
            {
                return false;
            }

            int startX = reader.ReadInt16();
            int startY = reader.ReadInt16();
            short startVelocityX = reader.ReadInt16();
            short startVelocityY = reader.ReadInt16();
            byte elementCount = reader.ReadByte();

            List<MovePathElement> elements = new(elementCount);
            int currentX = startX;
            int currentY = startY;
            short currentVelocityX = startVelocityX;
            short currentVelocityY = startVelocityY;
            short currentFoothold = 0;
            int cursorTime = currentTime;

            for (int i = 0; i < elementCount; i++)
            {
                if (!reader.CanRead(1))
                {
                    return false;
                }

                byte attr = reader.ReadByte();
                int elementX = currentX;
                int elementY = currentY;
                short elementVelocityX = currentVelocityX;
                short elementVelocityY = currentVelocityY;
                short elementFoothold = currentFoothold;

                switch (attr)
                {
                    case 0:
                    case 5:
                    case 12:
                    case 14:
                    case 35:
                    case 36:
                        elementX = reader.ReadInt16();
                        elementY = reader.ReadInt16();
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        elementFoothold = reader.ReadInt16();
                        if (attr == 12)
                        {
                            reader.ReadInt16();
                        }

                        reader.ReadInt16();
                        reader.ReadInt16();
                        break;
                    case 1:
                    case 2:
                    case 13:
                    case 16:
                    case 18:
                    case 31:
                    case 32:
                    case 33:
                    case 34:
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        elementFoothold = 0;
                        break;
                    case 3:
                    case 4:
                    case 6:
                    case 7:
                    case 8:
                    case 10:
                        elementX = reader.ReadInt16();
                        elementY = reader.ReadInt16();
                        elementFoothold = reader.ReadInt16();
                        elementVelocityX = 0;
                        elementVelocityY = 0;
                        break;
                    case 9:
                        reader.ReadByte();
                        elementVelocityX = 0;
                        elementVelocityY = 0;
                        elementFoothold = 0;
                        break;
                    case 11:
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        reader.ReadInt16();
                        elementFoothold = 0;
                        break;
                    case 17:
                        elementX = reader.ReadInt16();
                        elementY = reader.ReadInt16();
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        break;
                    case 20:
                    case 21:
                    case 22:
                    case 23:
                    case 24:
                    case 25:
                    case 26:
                    case 27:
                    case 28:
                    case 29:
                    case 30:
                        break;
                }

                moveAction = reader.ReadByte();
                short elapsed = reader.ReadInt16();
                MoveAction decodedAction = DecodeMoveAction(moveAction);
                bool facingRight = (moveAction & 1) == 0;

                elements.Add(new MovePathElement
                {
                    X = elementX,
                    Y = elementY,
                    VelocityX = elementVelocityX,
                    VelocityY = elementVelocityY,
                    Action = decodedAction,
                    FootholdId = elementFoothold,
                    TimeStamp = cursorTime,
                    Duration = elapsed,
                    FacingRight = facingRight,
                    StatChanged = false
                });

                currentX = elementX;
                currentY = elementY;
                currentVelocityX = elementVelocityX;
                currentVelocityY = elementVelocityY;
                currentFoothold = elementFoothold;
                cursorTime += Math.Max(1, (int)elapsed);
            }

            snapshot = new PlayerMovementSyncSnapshot(
                new PassivePositionSnapshot
                {
                    X = currentX,
                    Y = currentY,
                    VelocityX = currentVelocityX,
                    VelocityY = currentVelocityY,
                    Action = DecodeMoveAction(moveAction),
                    FootholdId = currentFoothold,
                    TimeStamp = cursorTime,
                    FacingRight = (moveAction & 1) == 0
                },
                elements);
            return true;
        }

        private static MoveAction DecodeMoveAction(byte moveAction)
        {
            int normalized = (moveAction >> 1) & 0x0F;
            return Enum.IsDefined(typeof(MoveAction), normalized)
                ? (MoveAction)normalized
                : MoveAction.Stand;
        }

        private ref struct PacketReader
        {
            private readonly ReadOnlySpan<byte> _buffer;
            private int _offset;

            public PacketReader(ReadOnlySpan<byte> buffer)
            {
                _buffer = buffer;
                _offset = 0;
            }

            public bool CanRead(int byteCount) => _offset + byteCount <= _buffer.Length;

            public byte ReadByte()
            {
                EnsureReadable(sizeof(byte));
                return _buffer[_offset++];
            }

            public short ReadInt16()
            {
                EnsureReadable(sizeof(short));
                short value = (short)(_buffer[_offset] | (_buffer[_offset + 1] << 8));
                _offset += sizeof(short);
                return value;
            }

            public int ReadInt32()
            {
                EnsureReadable(sizeof(int));
                int value = _buffer[_offset]
                    | (_buffer[_offset + 1] << 8)
                    | (_buffer[_offset + 2] << 16)
                    | (_buffer[_offset + 3] << 24);
                _offset += sizeof(int);
                return value;
            }

            public byte[] ReadBytes(int length)
            {
                EnsureReadable(length);
                byte[] value = _buffer.Slice(_offset, length).ToArray();
                _offset += length;
                return value;
            }

            public byte[] ReadRemainingBytes()
            {
                return ReadBytes(_buffer.Length - _offset);
            }

            public string ReadString8()
            {
                int length = ReadByte();
                EnsureReadable(length);
                string value = Encoding.UTF8.GetString(_buffer.Slice(_offset, length));
                _offset += length;
                return value;
            }

            public string ReadString16()
            {
                int length = ReadInt16();
                EnsureReadable(length);
                string value = Encoding.Default.GetString(_buffer.Slice(_offset, length));
                _offset += length;
                return value;
            }

            public int Offset => _offset;

            private void EnsureReadable(int byteCount)
            {
                if (!CanRead(byteCount))
                {
                    throw new InvalidOperationException("Remote user packet payload ended unexpectedly.");
                }
            }
        }
    }
}
