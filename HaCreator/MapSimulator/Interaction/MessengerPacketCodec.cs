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
        MemberInfo = 6
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
        string StatusText);

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
                    string.IsNullOrWhiteSpace(statusText) ? (isOnline ? "Online" : "Offline") : statusText.Trim());
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
