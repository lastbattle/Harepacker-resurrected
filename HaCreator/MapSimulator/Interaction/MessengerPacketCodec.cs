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
        Avatar = 8
    }

    internal readonly record struct MessengerInvitePacket(string ContactName);

    internal readonly record struct MessengerChatPacket(string ContactName, string Message);

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

    internal static class MessengerPacketCodec
    {
        public static bool TryParseInvite(ReadOnlySpan<byte> payload, out MessengerInvitePacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string contactName = reader.ReadString8();
                if (string.IsNullOrWhiteSpace(contactName))
                {
                    error = "Messenger invite packet contact name is empty.";
                    return false;
                }

                packet = new MessengerInvitePacket(contactName.Trim());
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

        private ref struct PacketReader
        {
            private readonly ReadOnlySpan<byte> _payload;
            private int _offset;

            public PacketReader(ReadOnlySpan<byte> payload)
            {
                _payload = payload;
                _offset = 0;
            }

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
