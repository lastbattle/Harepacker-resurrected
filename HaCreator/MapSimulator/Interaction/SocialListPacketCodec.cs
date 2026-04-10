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
        Request = 7,
        ReplaceWithIds = 8,
        UpsertWithId = 9,
        RemoveById = 10,
        SelectById = 11
    }

    internal readonly record struct SocialListPacketEntry(
        int? MemberId,
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
        int? MemberId,
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

    internal enum GuildSkillResultPacketKind : byte
    {
        LevelUp = 0,
        Renew = 1
    }

    internal readonly record struct GuildSkillResultPacket(
        GuildSkillResultPacketKind Kind,
        int SkillId,
        bool Approved,
        int? SkillLevel,
        int? RemainingDurationMinutes,
        int? GuildFundMeso,
        string Summary);

    internal enum SocialListClientGuildResultKind : byte
    {
        Ranking = 76,
        RankTitles = 68,
        Notice = 71,
        Mark = 69,
        PointsAndLevel = 75
    }

    internal readonly record struct SocialListClientGuildResultPacket(
        SocialListClientGuildResultKind Kind,
        int GuildId,
        IReadOnlyList<GuildRankingSeedEntry> RankingEntries,
        IReadOnlyList<string> RankTitles,
        string Notice,
        GuildMarkSelection? MarkSelection,
        int GuildPoints,
        int GuildLevel);

    internal enum SocialListClientAllianceResultKind : byte
    {
        RankTitles = 26,
        Notice = 28
    }

    internal readonly record struct SocialListClientAllianceResultPacket(
        SocialListClientAllianceResultKind Kind,
        int AllianceId,
        IReadOnlyList<string> RankTitles,
        string Notice);

    internal readonly record struct SocialListGradeChangePacket(
        int MemberId,
        int Delta,
        string Summary);

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
                    case SocialListRosterPacketKind.ReplaceWithIds:
                    {
                        int count = reader.ReadUInt16();
                        List<SocialListPacketEntry> entries = new(count);
                        for (int i = 0; i < count; i++)
                        {
                            entries.Add(ReadEntry(ref reader, kind == SocialListRosterPacketKind.ReplaceWithIds));
                        }

                        string summary = reader.HasRemaining ? reader.ReadString16() : null;
                        packet = new SocialListRosterPacket(kind, entries, null, null, summary, null, Approved: false);
                        return true;
                    }

                    case SocialListRosterPacketKind.Upsert:
                    case SocialListRosterPacketKind.UpsertWithId:
                        packet = new SocialListRosterPacket(
                            kind,
                            new[] { ReadEntry(ref reader, kind == SocialListRosterPacketKind.UpsertWithId) },
                            null,
                            null,
                            null,
                            null,
                            Approved: false);
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

                        packet = new SocialListRosterPacket(kind, Array.Empty<SocialListPacketEntry>(), null, name.Trim(), null, null, Approved: false);
                        return true;
                    }

                    case SocialListRosterPacketKind.RemoveById:
                    case SocialListRosterPacketKind.SelectById:
                    {
                        int memberId = reader.ReadInt32();
                        if (memberId <= 0)
                        {
                            error = $"Social-list {kind} packet member id must be positive.";
                            return false;
                        }

                        packet = new SocialListRosterPacket(kind, Array.Empty<SocialListPacketEntry>(), memberId, null, null, null, Approved: false);
                        return true;
                    }

                    case SocialListRosterPacketKind.Clear:
                    {
                        string summary = reader.HasRemaining ? reader.ReadString16() : null;
                        packet = new SocialListRosterPacket(kind, Array.Empty<SocialListPacketEntry>(), null, null, summary, null, Approved: false);
                        return true;
                    }

                    case SocialListRosterPacketKind.Summary:
                    {
                        string summary = reader.ReadString16();
                        packet = new SocialListRosterPacket(kind, Array.Empty<SocialListPacketEntry>(), null, null, summary, null, Approved: false);
                        return true;
                    }

                    case SocialListRosterPacketKind.Resolve:
                    {
                        bool approved = reader.ReadBoolean();
                        string requestKind = reader.ReadString8();
                        string summary = reader.HasRemaining ? reader.ReadString16() : null;
                        packet = new SocialListRosterPacket(kind, Array.Empty<SocialListPacketEntry>(), null, null, summary, requestKind, Approved: approved);
                        return true;
                    }

                    case SocialListRosterPacketKind.Request:
                    {
                        string requestKind = reader.ReadString8();
                        string summary = reader.HasRemaining ? reader.ReadString16() : null;
                        packet = new SocialListRosterPacket(kind, Array.Empty<SocialListPacketEntry>(), null, null, summary, requestKind, Approved: false);
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

        public static bool TryParseClientGuildResult(ReadOnlySpan<byte> payload, out SocialListClientGuildResultPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                SocialListClientGuildResultKind kind = (SocialListClientGuildResultKind)reader.ReadByte();
                switch (kind)
                {
                    case SocialListClientGuildResultKind.Ranking:
                    {
                        int guildId = reader.ReadInt32();
                        int count = reader.ReadInt32();
                        List<GuildRankingSeedEntry> rankingEntries = new(Math.Max(0, count));
                        for (int i = 0; i < count; i++)
                        {
                            string guildName = NormalizeRoleLabel(reader.ReadString16(), $"Guild {i + 1}");
                            int points = reader.ReadInt32();
                            int markBackground = reader.ReadUInt16();
                            int markBackgroundColor = reader.ReadByte();
                            int mark = reader.ReadUInt16();
                            int markColor = reader.ReadByte();
                            rankingEntries.Add(new GuildRankingSeedEntry(
                                guildName,
                                "Guild Master",
                                points,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                markBackground,
                                markBackgroundColor,
                                mark,
                                markColor,
                                IsPacketOwned: true));
                        }

                        packet = new SocialListClientGuildResultPacket(kind, guildId, rankingEntries, Array.Empty<string>(), null, null, 0, 0);
                        return true;
                    }

                    case SocialListClientGuildResultKind.RankTitles:
                    {
                        int guildId = reader.ReadInt32();
                        string[] titles = new string[5];
                        for (int i = 0; i < titles.Length; i++)
                        {
                            titles[i] = NormalizeRoleLabel(reader.ReadString16(), $"Rank {i + 1}");
                        }

                        packet = new SocialListClientGuildResultPacket(kind, guildId, Array.Empty<GuildRankingSeedEntry>(), titles, null, null, 0, 0);
                        return true;
                    }

                    case SocialListClientGuildResultKind.Notice:
                    {
                        int guildId = reader.ReadInt32();
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            guildId,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            reader.ReadString16().Trim(),
                            null,
                            0,
                            0);
                        return true;
                    }

                    case SocialListClientGuildResultKind.Mark:
                    {
                        int guildId = reader.ReadInt32();
                        GuildMarkSelection selection = new(
                            reader.ReadUInt16(),
                            reader.ReadByte(),
                            reader.ReadUInt16(),
                            reader.ReadByte(),
                            0);
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            guildId,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            selection,
                            0,
                            0);
                        return true;
                    }

                    case SocialListClientGuildResultKind.PointsAndLevel:
                    {
                        int guildId = reader.ReadInt32();
                        int guildPoints = reader.ReadInt32();
                        int guildLevel = reader.ReadInt32();
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            guildId,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            guildPoints,
                            guildLevel);
                        return true;
                    }

                    default:
                        error = $"Unsupported client guild-result subtype {(byte)kind}.";
                        return false;
                }
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseGuildSkillResult(ReadOnlySpan<byte> payload, out GuildSkillResultPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                GuildSkillResultPacketKind kind = (GuildSkillResultPacketKind)reader.ReadByte();
                if (kind != GuildSkillResultPacketKind.LevelUp && kind != GuildSkillResultPacketKind.Renew)
                {
                    error = $"Unsupported guild-skill result kind {(byte)kind}.";
                    return false;
                }

                int skillId = reader.ReadInt32();
                if (skillId <= 0)
                {
                    error = "Guild-skill result packet skill id must be positive.";
                    return false;
                }

                bool approved = reader.ReadBoolean();
                byte flags = reader.ReadByte();
                int? skillLevel = (flags & 0x01) != 0 ? reader.ReadInt32() : null;
                int? remainingDurationMinutes = (flags & 0x02) != 0 ? reader.ReadInt32() : null;
                int? guildFundMeso = (flags & 0x04) != 0 ? reader.ReadInt32() : null;
                string summary = reader.HasRemaining ? reader.ReadString16().Trim() : null;

                packet = new GuildSkillResultPacket(
                    kind,
                    skillId,
                    approved,
                    skillLevel,
                    remainingDurationMinutes,
                    guildFundMeso,
                    summary);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseClientAllianceResult(ReadOnlySpan<byte> payload, out SocialListClientAllianceResultPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                SocialListClientAllianceResultKind kind = (SocialListClientAllianceResultKind)reader.ReadByte();
                switch (kind)
                {
                    case SocialListClientAllianceResultKind.RankTitles:
                    {
                        int allianceId = reader.ReadInt32();
                        string[] titles = new string[5];
                        for (int i = 0; i < titles.Length; i++)
                        {
                            titles[i] = NormalizeRoleLabel(reader.ReadString16(), $"Rank {i + 1}");
                        }

                        packet = new SocialListClientAllianceResultPacket(kind, allianceId, titles, null);
                        return true;
                    }

                    case SocialListClientAllianceResultKind.Notice:
                    {
                        int allianceId = reader.ReadInt32();
                        packet = new SocialListClientAllianceResultPacket(kind, allianceId, Array.Empty<string>(), reader.ReadString16().Trim());
                        return true;
                    }

                    default:
                        error = $"Unsupported client alliance-result subtype {(byte)kind}.";
                        return false;
                }
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseGradeChange(ReadOnlySpan<byte> payload, out SocialListGradeChangePacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                int memberId = reader.ReadInt32();
                sbyte delta = unchecked((sbyte)reader.ReadByte());
                if (memberId <= 0)
                {
                    error = "Social-list grade-change member id must be positive.";
                    return false;
                }

                if (delta == 0)
                {
                    error = "Social-list grade-change delta must not be zero.";
                    return false;
                }

                packet = new SocialListGradeChangePacket(memberId, delta > 0 ? 1 : -1, reader.HasRemaining ? reader.ReadString16().Trim() : null);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static SocialListPacketEntry ReadEntry(ref PacketReader reader, bool includeMemberId)
        {
            int? memberId = includeMemberId ? reader.ReadInt32() : null;
            string name = reader.ReadString8();
            string primary = reader.ReadString8();
            string secondary = reader.ReadString8();
            string location = reader.ReadString8();
            int channel = Math.Max(1, (int)reader.ReadByte());
            byte flags = reader.ReadByte();
            return new SocialListPacketEntry(
                memberId > 0 ? memberId : null,
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

            public int ReadInt32()
            {
                EnsureRemaining(4);
                int value = _payload[_offset]
                    | (_payload[_offset + 1] << 8)
                    | (_payload[_offset + 2] << 16)
                    | (_payload[_offset + 3] << 24);
                _offset += 4;
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
