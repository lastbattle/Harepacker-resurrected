using System;
using System.Collections.Generic;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum SocialListRosterPacketKind : byte
    {
        Replace = 0,
        Upsert = 1,
        Remove = 2,
        Clear = 3,
        Select = 4,
        Summary = 5,
        Resolve = 6,
        Request = 7
    }

    internal readonly record struct SocialListPacketEntry(
        string Name,
        string PrimaryText,
        string SecondaryText,
        string LocationSummary,
        int Channel,
        bool IsOnline,
        bool IsLeader,
        bool IsBlocked,
        bool IsLocalPlayer);

    internal readonly record struct SocialListRosterPacket(
        SocialListRosterPacketKind Kind,
        IReadOnlyList<SocialListPacketEntry> Entries,
        string Name,
        string Summary,
        string RequestKind,
        bool Approved);

    internal readonly record struct SocialListGuildAuthorityPacket(
        string RoleLabel,
        bool CanManageRanks,
        bool CanToggleAdmission,
        bool CanEditNotice);

    internal readonly record struct SocialListAllianceAuthorityPacket(
        string RoleLabel,
        bool CanEditRanks,
        bool CanEditNotice);

    internal readonly record struct SocialListGuildUiPacket(
        bool HasGuildMembership,
        string GuildName,
        int GuildLevel);

    internal static class SocialListPacketCodec
    {
        public static bool TryParseRoster(ReadOnlySpan<byte> payload, out SocialListRosterPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                SocialListRosterPacketKind kind = (SocialListRosterPacketKind)reader.ReadByte();
                switch (kind)
                {
                    case SocialListRosterPacketKind.Replace:
                    {
                        int count = reader.ReadUInt16();
                        List<SocialListPacketEntry> entries = new(count);
                        for (int i = 0; i < count; i++)
                        {
                            entries.Add(ReadEntry(ref reader));
                        }

                        string summary = reader.HasRemaining ? reader.ReadString16() : null;
                        packet = new SocialListRosterPacket(kind, entries, null, summary, null, Approved: false);
                        return true;
                    }

                    case SocialListRosterPacketKind.Upsert:
                        packet = new SocialListRosterPacket(kind, new[] { ReadEntry(ref reader) }, null, null, null, Approved: false);
                        return true;

                    case SocialListRosterPacketKind.Remove:
                    case SocialListRosterPacketKind.Select:
                    {
                        string name = reader.ReadString8();
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            error = $"Social-list {kind} packet entry name is empty.";
                            return false;
                        }

                        packet = new SocialListRosterPacket(kind, Array.Empty<SocialListPacketEntry>(), name.Trim(), null, null, Approved: false);
                        return true;
                    }

                    case SocialListRosterPacketKind.Clear:
                    {
                        string summary = reader.HasRemaining ? reader.ReadString16() : null;
                        packet = new SocialListRosterPacket(kind, Array.Empty<SocialListPacketEntry>(), null, summary, null, Approved: false);
                        return true;
                    }

                    case SocialListRosterPacketKind.Summary:
                    {
                        string summary = reader.ReadString16();
                        packet = new SocialListRosterPacket(kind, Array.Empty<SocialListPacketEntry>(), null, summary, null, Approved: false);
                        return true;
                    }

                    case SocialListRosterPacketKind.Resolve:
                    {
                        bool approved = reader.ReadBoolean();
                        string requestKind = reader.ReadString8();
                        string summary = reader.HasRemaining ? reader.ReadString16() : null;
                        packet = new SocialListRosterPacket(kind, Array.Empty<SocialListPacketEntry>(), null, summary, requestKind, Approved: approved);
                        return true;
                    }

                    case SocialListRosterPacketKind.Request:
                    {
                        string requestKind = reader.ReadString8();
                        string summary = reader.HasRemaining ? reader.ReadString16() : null;
                        packet = new SocialListRosterPacket(kind, Array.Empty<SocialListPacketEntry>(), null, summary, requestKind, Approved: false);
                        return true;
                    }

                    default:
                        error = $"Unsupported social-list roster packet kind {(byte)kind}.";
                        return false;
                }
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseGuildAuthority(ReadOnlySpan<byte> payload, out SocialListGuildAuthorityPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string roleLabel = reader.ReadString8();
                packet = new SocialListGuildAuthorityPacket(
                    NormalizeRoleLabel(roleLabel, "Member"),
                    reader.ReadBoolean(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean());
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseAllianceAuthority(ReadOnlySpan<byte> payload, out SocialListAllianceAuthorityPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                string roleLabel = reader.ReadString8();
                packet = new SocialListAllianceAuthorityPacket(
                    NormalizeRoleLabel(roleLabel, "Member"),
                    reader.ReadBoolean(),
                    reader.ReadBoolean());
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseGuildUi(ReadOnlySpan<byte> payload, out SocialListGuildUiPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                bool hasMembership = reader.ReadBoolean();
                string guildName = reader.ReadString8();
                int guildLevel = reader.ReadUInt16();
                packet = new SocialListGuildUiPacket(
                    hasMembership,
                    hasMembership ? NormalizeRoleLabel(guildName, "No Guild") : "No Guild",
                    Math.Max(0, guildLevel));
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static SocialListPacketEntry ReadEntry(ref PacketReader reader)
        {
            string name = reader.ReadString8();
            string primary = reader.ReadString8();
            string secondary = reader.ReadString8();
            string location = reader.ReadString8();
            int channel = Math.Max(1, (int)reader.ReadByte());
            byte flags = reader.ReadByte();
            return new SocialListPacketEntry(
                string.IsNullOrWhiteSpace(name) ? "Packet Entry" : name.Trim(),
                string.IsNullOrWhiteSpace(primary) ? "-" : primary.Trim(),
                string.IsNullOrWhiteSpace(secondary) ? "-" : secondary.Trim(),
                string.IsNullOrWhiteSpace(location) ? "Unknown" : location.Trim(),
                channel,
                (flags & 0x01) != 0,
                (flags & 0x02) != 0,
                (flags & 0x04) != 0,
                (flags & 0x08) != 0);
        }

        private static string NormalizeRoleLabel(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
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
                EnsureRemaining(1);
                return _payload[_offset++];
            }

            public bool ReadBoolean()
            {
                return ReadByte() != 0;
            }

            public ushort ReadUInt16()
            {
                EnsureRemaining(2);
                ushort value = (ushort)(_payload[_offset] | (_payload[_offset + 1] << 8));
                _offset += 2;
                return value;
            }

            public string ReadString8()
            {
                int length = ReadByte();
                if (length == 0)
                {
                    return string.Empty;
                }

                EnsureRemaining(length);
                string value = Encoding.UTF8.GetString(_payload.Slice(_offset, length));
                _offset += length;
                return value;
            }

            public string ReadString16()
            {
                int length = ReadUInt16();
                if (length == 0)
                {
                    return string.Empty;
                }

                EnsureRemaining(length);
                string value = Encoding.UTF8.GetString(_payload.Slice(_offset, length));
                _offset += length;
                return value;
            }

            private void EnsureRemaining(int count)
            {
                if (_offset + count > _payload.Length)
                {
                    throw new InvalidOperationException("Social-list packet payload ended before all fields were decoded.");
                }
            }
        }
    }
}
