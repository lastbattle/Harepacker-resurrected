using HaCreator.MapSimulator.Managers;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum MessengerPacketType
    {
        Invite = 0,
        InviteAccept = 1,
        InviteReject = 2,
        Leave = 3,
        RoomChat = 4,
        Whisper = 5,
        MemberInfo = 6,
        Blocked = 7,
        Avatar = 8,
        Enter = 9,
        InviteResult = 10,
        Migrated = 11,
        SelfEnterResult = 12
    }

    internal readonly record struct MessengerInvitePacket(
        string ContactName,
        byte InviteType,
        int InviteSequence,
        bool SkipBlacklistAutoReject);

    internal readonly record struct MessengerChatPacket(string ContactName, string Message);

    internal readonly record struct MessengerClientChatPacket(
        string ContactName,
        string Message,
        bool IsWhisper,
        bool IsActivityPulse,
        bool ActivityEnabled);

    internal readonly record struct MessengerMemberInfoPacket(
        string ContactName,
        bool IsOnline,
        int Channel,
        int Level,
        string JobName,
        string LocationSummary,
        string StatusText,
        LoginAvatarLook AvatarLook);

    internal readonly record struct MessengerBlockedPacket(string ContactName, bool Blocked);

    internal readonly record struct MessengerAvatarPacket(int SlotIndex, LoginAvatarLook AvatarLook);

    internal readonly record struct MessengerEnterPacket(
        int SlotIndex,
        string ContactName,
        int Channel,
        bool IsNew,
        LoginAvatarLook AvatarLook)
    {
        public bool IsOnline => true;
    }

    internal readonly record struct MessengerInviteResultPacket(string ContactName, bool InviteSent);

    internal readonly record struct MessengerLeaveSlotPacket(int SlotIndex);

    internal readonly record struct MessengerSelfEnterResultPacket(int SlotIndex)
    {
        public bool Succeeded => SlotIndex >= 0;
    }

    internal readonly record struct MessengerMigratedParticipantPacket(
        int SlotIndex,
        byte State,
        string ContactName,
        int Channel,
        LoginAvatarLook AvatarLook)
    {
        public bool ClearSlot => State == 0;
        public bool PreserveSlot => State == 1;
        public bool Present => State >= 2;
        public bool IsOnline => Present;
    }

    internal readonly record struct MessengerMigratedPacket(
        MessengerMigratedParticipantPacket[] Participants);

    internal static class MessengerPacketCodec
    {
        private const string ChatSeparator = " : ";

        public static bool TryParseClientDispatch(ReadOnlySpan<byte> payload, out byte packetType, out byte[] body, out string error)
        {
            packetType = 0;
            body = Array.Empty<byte>();
            error = null;

            if (payload.Length == 0)
            {
                error = "Messenger client packet payload is empty.";
                return false;
            }

            packetType = payload[0];
            body = payload[1..].ToArray();
            return true;
        }

        public static bool TryParseInvite(ReadOnlySpan<byte> payload, out MessengerInvitePacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string contactName = reader.ReadString16();
                byte inviteType = reader.HasRemaining ? reader.ReadByte() : (byte)0;
                int inviteSequence = reader.HasRemaining ? reader.ReadInt32() : 0;
                bool skipBlacklistAutoReject = reader.HasRemaining && reader.ReadByte() != 0;

                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger invite packet contact name is empty.";
                    return false;
                }

                packet = new MessengerInvitePacket(
                    contactName.Trim(),
                    inviteType,
                    inviteSequence,
                    skipBlacklistAutoReject);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseChat(ReadOnlySpan<byte> payload, out MessengerChatPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string contactName = reader.ReadString8();
                string message = reader.ReadString16();
                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger chat packet contact name is empty.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    error = "Messenger chat packet message is empty.";
                    return false;
                }

                packet = new MessengerChatPacket(contactName.Trim(), message.Trim());
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseMemberInfo(ReadOnlySpan<byte> payload, out MessengerMemberInfoPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string contactName = reader.ReadString8();
                bool isOnline = reader.ReadByte() != 0;
                int channel = Math.Max(1, (int)reader.ReadByte());
                int level = Math.Max(1, (int)reader.ReadInt16());
                string jobName = reader.ReadString8();
                string locationSummary = reader.ReadString8();
                string statusText = reader.ReadString8();
                LoginAvatarLook avatarLook = null;
                if (reader.TryReadAvatarLook(out LoginAvatarLook decodedAvatarLook, out string avatarError))
                {
                    avatarLook = decodedAvatarLook;
                }
                else
                {
                    error = avatarError;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger member-info packet contact name is empty.";
                    return false;
                }

                packet = new MessengerMemberInfoPacket(
                    contactName.Trim(),
                    isOnline,
                    channel,
                    level,
                    string.IsNullOrWhiteSpace(jobName) ? "Adventurer" : jobName.Trim(),
                    string.IsNullOrWhiteSpace(locationSummary) ? "Field" : locationSummary.Trim(),
                    string.IsNullOrWhiteSpace(statusText) ? (isOnline ? "Online" : "Offline") : statusText.Trim(),
                    avatarLook);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseBlocked(ReadOnlySpan<byte> payload, out MessengerBlockedPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string contactName = reader.ReadString16();
                bool blocked = reader.ReadByte() != 0;
                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger blocked packet contact name is empty.";
                    return false;
                }

                packet = new MessengerBlockedPacket(contactName.Trim(), blocked);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseAvatar(ReadOnlySpan<byte> payload, out MessengerAvatarPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                int slotIndex = reader.ReadByte();
                byte[] avatarPayload = reader.ReadRemainingBytes();
                if (avatarPayload.Length == 0)
                {
                    error = "Messenger avatar packet is missing AvatarLook bytes.";
                    return false;
                }

                if (!LoginAvatarLookCodec.TryDecode(avatarPayload, out LoginAvatarLook avatarLook, out error))
                {
                    error ??= "Messenger avatar packet AvatarLook payload could not be decoded.";
                    return false;
                }

                packet = new MessengerAvatarPacket(slotIndex, avatarLook);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseEnter(ReadOnlySpan<byte> payload, out MessengerEnterPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                int slotIndex = reader.ReadByte();
                if (!reader.TryReadAvatarLook(out LoginAvatarLook avatarLook, out error))
                {
                    error ??= "Messenger enter packet AvatarLook payload could not be decoded.";
                    return false;
                }

                string contactName = reader.ReadString8();
                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger enter packet contact name is empty.";
                    return false;
                }

                int channel = Math.Max(1, (int)reader.ReadByte());
                bool isNew = reader.ReadByte() != 0;

                packet = new MessengerEnterPacket(slotIndex, contactName.Trim(), channel, isNew, avatarLook);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseInviteResult(ReadOnlySpan<byte> payload, out MessengerInviteResultPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string contactName = reader.ReadString16();
                bool inviteSent = reader.ReadByte() != 0;
                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger invite-result packet contact name is empty.";
                    return false;
                }

                packet = new MessengerInviteResultPacket(contactName.Trim(), inviteSent);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseLeaveSlot(ReadOnlySpan<byte> payload, out MessengerLeaveSlotPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                packet = new MessengerLeaveSlotPacket(reader.ReadByte());
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseClientChat(ReadOnlySpan<byte> payload, out MessengerClientChatPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string chatText = reader.ReadString16();
                if (string.IsNullOrWhiteSpace(chatText))
                {
                    error = "Messenger client chat packet text is empty.";
                    return false;
                }

                int separatorIndex = chatText.IndexOf(ChatSeparator, StringComparison.Ordinal);
                if (separatorIndex > 0 && separatorIndex < chatText.Length - ChatSeparator.Length)
                {
                    string contactName = chatText[..separatorIndex].Trim();
                    string message = chatText[(separatorIndex + ChatSeparator.Length)..].Trim();
                    if (string.IsNullOrWhiteSpace(contactName))
                    {
                        error = "Messenger client chat packet speaker name is empty.";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(message))
                    {
                        error = "Messenger client chat packet message is empty.";
                        return false;
                    }

                    packet = new MessengerClientChatPacket(contactName, message, IsWhisper: false, IsActivityPulse: false, ActivityEnabled: false);
                    return true;
                }

                if (chatText.Length < 2)
                {
                    error = "Messenger client chat packet is missing both the MapleStory 'name : message' separator and the activity marker.";
                    return false;
                }

                string activityName = chatText[..^1].Trim();
                char activityMarker = chatText[^1];
                if (string.IsNullOrWhiteSpace(activityName))
                {
                    error = "Messenger client chat packet speaker name is empty.";
                    return false;
                }

                if (activityMarker is not '0' and not '1')
                {
                    error = "Messenger client chat packet activity marker must be '0' or '1' when no room-chat text is present.";
                    return false;
                }

                packet = new MessengerClientChatPacket(
                    activityName,
                    string.Empty,
                    IsWhisper: false,
                    IsActivityPulse: true,
                    ActivityEnabled: activityMarker != '0');
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseSelfEnterResult(ReadOnlySpan<byte> payload, out MessengerSelfEnterResultPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                packet = new MessengerSelfEnterResultPacket((sbyte)reader.ReadByte());
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseMigrated(ReadOnlySpan<byte> payload, out MessengerMigratedPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                MessengerMigratedParticipantPacket[] participants = new MessengerMigratedParticipantPacket[3];
                for (int i = 0; i < participants.Length; i++)
                {
                    byte state = reader.ReadByte();
                    if (state == 0)
                    {
                        participants[i] = new MessengerMigratedParticipantPacket(i, state, string.Empty, 1, null);
                        continue;
                    }

                    if (state == 1)
                    {
                        participants[i] = new MessengerMigratedParticipantPacket(i, state, string.Empty, 1, null);
                        continue;
                    }

                    if (!reader.TryReadAvatarLook(out LoginAvatarLook avatarLook, out error))
                    {
                        error ??= "Messenger migrated packet AvatarLook payload could not be decoded.";
                        return false;
                    }

                    string contactName = reader.ReadString8();
                    if (string.IsNullOrWhiteSpace(contactName))
                    {
                        error = "Messenger migrated packet contact name is empty.";
                        return false;
                    }

                    int channel = Math.Max(1, (int)reader.ReadByte());

                    participants[i] = new MessengerMigratedParticipantPacket(
                        i,
                        state,
                        contactName.Trim(),
                        channel,
                        avatarLook);
                }

                packet = new MessengerMigratedPacket(participants);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private ref struct PacketReader
        {
            private readonly ReadOnlySpan<byte> _payload;
            private int _offset;

            public PacketReader(ReadOnlySpan<byte> payload)
            {
                _payload = payload;
                _offset = 0;
            }

            public bool HasRemaining => _offset < _payload.Length;

            public byte ReadByte()
            {
                EnsureAvailable(sizeof(byte));
                return _payload[_offset++];
            }

            public short ReadInt16()
            {
                EnsureAvailable(sizeof(short));
                short value = (short)(_payload[_offset] | (_payload[_offset + 1] << 8));
                _offset += sizeof(short);
                return value;
            }

            public int ReadInt32()
            {
                EnsureAvailable(sizeof(int));
                int value = _payload[_offset]
                    | (_payload[_offset + 1] << 8)
                    | (_payload[_offset + 2] << 16)
                    | (_payload[_offset + 3] << 24);
                _offset += sizeof(int);
                return value;
            }

            public string ReadString8()
            {
                int length = ReadByte();
                return ReadString(length);
            }

            public string ReadString16()
            {
                int length = ReadInt16();
                return ReadString(length);
            }

            public bool TryReadAvatarLook(out LoginAvatarLook avatarLook, out string error)
            {
                avatarLook = null;
                error = null;
                if (_offset >= _payload.Length)
                {
                    return true;
                }

                if (_payload.Length - _offset < sizeof(int))
                {
                    error = "Messenger member-info AvatarLook length is truncated.";
                    return false;
                }

                int avatarLookLength = ReadInt32();
                if (avatarLookLength <= 0)
                {
                    return true;
                }

                EnsureAvailable(avatarLookLength);
                byte[] avatarLookPayload = _payload.Slice(_offset, avatarLookLength).ToArray();
                _offset += avatarLookLength;
                if (!LoginAvatarLookCodec.TryDecode(avatarLookPayload, out avatarLook, out error))
                {
                    error ??= "Messenger member-info AvatarLook payload could not be decoded.";
                    return false;
                }

                return true;
            }

            public byte[] ReadRemainingBytes()
            {
                byte[] remaining = _payload.Slice(_offset).ToArray();
                _offset = _payload.Length;
                return remaining;
            }

            private string ReadString(int length)
            {
                if (length < 0)
                {
                    throw new InvalidOperationException($"Messenger packet string length {length} is invalid.");
                }

                EnsureAvailable(length);
                string value = System.Text.Encoding.ASCII.GetString(_payload.Slice(_offset, length));
                _offset += length;
                return value;
            }

            private void EnsureAvailable(int count)
            {
                if (count < 0 || _offset + count > _payload.Length)
                {
                    throw new InvalidOperationException("Messenger packet payload ended before all fields could be read.");
                }
            }
        }
    }
}
