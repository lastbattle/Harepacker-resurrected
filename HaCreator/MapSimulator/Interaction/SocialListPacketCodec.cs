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

    internal readonly record struct SocialListClientFriendEntry(
        int FriendId,
        string Name,
        string GroupName,
        int ChannelId,
        byte Flag,
        bool InShop,
        bool IsListenBlocked = false,
        bool IsTalkBlocked = false,
        bool HasBlockState = false);

    internal enum SocialListClientFriendResultKind : byte
    {
        Reset = 7,
        Update = 8,
        Insert = 9,
        Refresh = 10,
        Channel = 20,
        Capacity = 21,
        NoticeInviteBlocked = 11,
        NoticeTargetFull = 12,
        NoticeTargetUnknown = 13,
        NoticeSelf = 14,
        NoticeDuplicate = 15,
        NoticeFailure = 16,
        NoticeFailureWithMessage = 17,
        ResetBlocked = 18,
        NoticeBlocked = 19,
        NoticeRequestDenied = 22,
        NoticeCapacityExpanded = 23
    }

    internal readonly record struct SocialListClientFriendResultPacket(
        SocialListClientFriendResultKind Kind,
        IReadOnlyList<SocialListClientFriendEntry> Entries,
        SocialListClientFriendEntry? Entry,
        int FriendId,
        int ChannelId,
        int FriendMax,
        string Summary);

    internal readonly record struct SocialListClientPartyEntry(
        int MemberId,
        string Name,
        int JobId,
        int Level,
        int ChannelId,
        int FieldId,
        bool IsLeader);

    internal enum SocialListClientPartyResultKind : byte
    {
        Invite = 4,
        Load = 7,
        Create = 8,
        Notice9 = 9,
        Notice10 = 10,
        Join = 12,
        Notice16 = 16,
        Notice17 = 17,
        Notice18 = 18,
        NoOp19 = 19,
        NoticeNamed = 22,
        Notice29 = 29,
        Notice37 = 37,
        Refresh = 38,
        LeaderChange = 31,
        Notice32 = 32,
        Notice33 = 33,
        Notice34 = 34,
        Notice36 = 36,
        MemberJobLevel = 39,
        PqRewardSelectSuccess = 40,
        PqRewardSelectFail = 41,
        PqRewardReceive = 42,
        PqRewardRequestFail = 43,
        Notice44 = 44,
        NoticeOptionalMessage = 45,
        TownPortal = 46,
        SearchPacket = 75,
        SearchPacket2 = 76,
        SearchPacket3 = 77,
        SearchApply = 78,
        SearchPacket4 = 79,
        SearchPacket5 = 80
    }

    internal readonly record struct SocialListClientPartyResultPacket(
        SocialListClientPartyResultKind Kind,
        int PartyId,
        IReadOnlyList<SocialListClientPartyEntry> Entries,
        int MemberId,
        int Level,
        int JobId,
        string ActorName,
        string Summary);

    internal enum GuildSkillResultPacketKind : byte
    {
        LevelUp = 0,
        Renew = 1,
        FundSync = 2
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
        GuildNameInput = 1,
        CreateGuildAgreement = 3,
        GuildInvite = 5,
        GuildMarkInput = 17,
        GuildDataSnapshot = 28,
        Notice35 = 35,
        Notice37 = 37,
        Notice42 = 42,
        Notice43 = 43,
        Notice44 = 44,
        Notice47 = 47,
        Notice50 = 50,
        Notice55 = 55,
        Notice56 = 56,
        Notice57 = 57,
        Notice58 = 58,
        GradeChange = 66,
        Ranking = 76,
        GuildQuestNotEnoughMembers = 77,
        GuildQuestRegistrantDisconnected = 78,
        RankTitles = 68,
        Notice = 71,
        Mark = 69,
        PointsAndLevel = 75,
        GuildQuestQueueNotice = 79,
        GuildBoardAuthKey = 80,
        SkillRecord = 81,
        ResultNotice = 82
    }

    internal readonly record struct SocialListClientGuildResultPacket(
        SocialListClientGuildResultKind Kind,
        int GuildId,
        IReadOnlyList<GuildRankingSeedEntry> RankingEntries,
        IReadOnlyList<string> RankTitles,
        string Notice,
        GuildMarkSelection? MarkSelection,
        int GuildPoints,
        int GuildLevel,
        bool HasExplicitNotice,
        string ResultNotice,
        SocialListGradeChangePacket GradeChange,
        SocialListGuildSkillRecordPacket? GuildSkillRecord,
        int GuildQuestChannel = 0,
        int GuildQuestWaitStatus = 0,
        string GuildBoardAuthKey = null,
        string DirectNotice = null,
        byte RawSubtype = 0,
        bool UsesSharedResultNoticeFallback = false,
        string ExplicitBranchSummary = null,
        string GuildName = null,
        IReadOnlyList<SocialListGuildSkillRecordPacket> GuildSkillRecords = null);

    internal readonly record struct SocialListGuildSkillRecordPacket(
        int SkillId,
        int SkillLevel,
        DateTimeOffset? Expiration,
        string BuyCharacterName);

    internal enum SocialListClientAllianceResultKind : byte
    {
        GradeChange = 27,
        RankTitles = 26,
        Notice = 28
    }

    internal enum SocialListClientResultOpcodeKind
    {
        FriendResult,
        PartyResult,
        GuildResult,
        AllianceResult
    }

    internal readonly record struct SocialListClientAllianceResultPacket(
        SocialListClientAllianceResultKind Kind,
        int AllianceId,
        IReadOnlyList<string> RankTitles,
        string Notice,
        SocialListGradeChangePacket GradeChange);

    internal readonly record struct SocialListGradeChangePacket(
        int MemberId,
        int Delta,
        int? AbsoluteGrade,
        string Summary);

    internal static class SocialListPacketCodec
    {
        public static bool TryParseOpcodeFramedClientResult(
            ReadOnlySpan<byte> rawPacket,
            ushort friendResultOpcode,
            ushort partyResultOpcode,
            ushort guildResultOpcode,
            ushort allianceResultOpcode,
            out SocialListClientResultOpcodeKind kind,
            out byte[] payload,
            out string error)
        {
            kind = default;
            payload = null;
            error = null;

            if (rawPacket.Length < sizeof(ushort))
            {
                error = "Opcode-framed social-list packet must include a 2-byte opcode.";
                return false;
            }

            ushort opcode = (ushort)(rawPacket[0] | (rawPacket[1] << 8));
            if (friendResultOpcode > 0 && opcode == friendResultOpcode)
            {
                kind = SocialListClientResultOpcodeKind.FriendResult;
            }
            else if (partyResultOpcode > 0 && opcode == partyResultOpcode)
            {
                kind = SocialListClientResultOpcodeKind.PartyResult;
            }
            else if (guildResultOpcode > 0 && opcode == guildResultOpcode)
            {
                kind = SocialListClientResultOpcodeKind.GuildResult;
            }
            else if (allianceResultOpcode > 0 && opcode == allianceResultOpcode)
            {
                kind = SocialListClientResultOpcodeKind.AllianceResult;
            }
            else
            {
                error = $"Unsupported opcode-framed social-list result opcode {opcode}.";
                return false;
            }

            payload = rawPacket[sizeof(ushort)..].ToArray();
            return true;
        }

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

        public static bool TryParseClientFriendResult(ReadOnlySpan<byte> payload, out SocialListClientFriendResultPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                SocialListClientFriendResultKind kind = (SocialListClientFriendResultKind)reader.ReadByte();
                switch (kind)
                {
                    case SocialListClientFriendResultKind.Reset:
                    case SocialListClientFriendResultKind.Refresh:
                    case SocialListClientFriendResultKind.ResetBlocked:
                    {
                        int count = reader.ReadByte();
                        List<SocialListClientFriendEntry> entries = new(Math.Max(0, count));
                        for (int i = 0; i < count; i++)
                        {
                            entries.Add(ReadClientFriendEntry(ref reader, inShop: false));
                        }

                        for (int i = 0; i < entries.Count; i++)
                        {
                            bool inShop = reader.ReadByte() != 0;
                            entries[i] = entries[i] with { InShop = inShop };
                        }

                        for (int i = 0; i < entries.Count; i++)
                        {
                            bool isListenBlocked = reader.ReadInt32() != 0;
                            entries[i] = entries[i] with
                            {
                                IsListenBlocked = isListenBlocked,
                                HasBlockState = true
                            };
                        }

                        for (int i = 0; i < entries.Count; i++)
                        {
                            bool isTalkBlocked = reader.ReadInt32() != 0;
                            entries[i] = entries[i] with
                            {
                                IsTalkBlocked = isTalkBlocked,
                                HasBlockState = true
                            };
                        }

                        packet = new SocialListClientFriendResultPacket(kind, entries, null, 0, 0, 0, null);
                        return true;
                    }

                    case SocialListClientFriendResultKind.Update:
                    {
                        int lookupFriendId = reader.ReadInt32();
                        SocialListClientFriendEntry entry = ReadClientFriendEntry(ref reader, reader.ReadByte() != 0);
                        packet = new SocialListClientFriendResultPacket(
                            kind,
                            Array.Empty<SocialListClientFriendEntry>(),
                            entry,
                            lookupFriendId > 0 ? lookupFriendId : entry.FriendId,
                            0,
                            0,
                            null);
                        return true;
                    }

                    case SocialListClientFriendResultKind.Insert:
                    {
                        int friendId = reader.ReadInt32();
                        string inviteName = reader.ReadMapleString16();
                        _ = reader.ReadInt32();
                        _ = reader.ReadInt32();
                        SocialListClientFriendEntry entry = ReadClientFriendEntry(ref reader, reader.ReadByte() != 0);
                        packet = new SocialListClientFriendResultPacket(
                            kind,
                            Array.Empty<SocialListClientFriendEntry>(),
                            entry,
                            friendId > 0 ? friendId : entry.FriendId,
                            0,
                            0,
                            string.IsNullOrWhiteSpace(inviteName) ? null : inviteName.Trim());
                        return true;
                    }

                    case SocialListClientFriendResultKind.Channel:
                    {
                        int friendId = reader.ReadInt32();
                        bool inShop = reader.ReadByte() != 0;
                        int channelId = reader.ReadInt32();
                        packet = new SocialListClientFriendResultPacket(kind, Array.Empty<SocialListClientFriendEntry>(), null, friendId, channelId, 0, inShop ? "Cash Shop" : null);
                        return true;
                    }

                    case SocialListClientFriendResultKind.Capacity:
                        packet = new SocialListClientFriendResultPacket(kind, Array.Empty<SocialListClientFriendEntry>(), null, 0, 0, reader.ReadByte(), null);
                        return true;

                    case SocialListClientFriendResultKind.NoticeFailure:
                    case SocialListClientFriendResultKind.NoticeBlocked:
                    case SocialListClientFriendResultKind.NoticeFailureWithMessage:
                    case SocialListClientFriendResultKind.NoticeRequestDenied:
                    {
                        string summary = reader.ReadByte() != 0 && reader.HasRemaining
                            ? reader.ReadMapleString16().Trim()
                            : ResolveFriendResultNoticeText(0x02DF, kind);
                        packet = new SocialListClientFriendResultPacket(kind, Array.Empty<SocialListClientFriendEntry>(), null, 0, 0, 0, summary);
                        return true;
                    }

                    case SocialListClientFriendResultKind.NoticeInviteBlocked:
                        packet = new SocialListClientFriendResultPacket(kind, Array.Empty<SocialListClientFriendEntry>(), null, 0, 0, 0, ResolveFriendResultNoticeText(0x02E0, kind));
                        return true;

                    case SocialListClientFriendResultKind.NoticeTargetFull:
                        packet = new SocialListClientFriendResultPacket(kind, Array.Empty<SocialListClientFriendEntry>(), null, 0, 0, 0, ResolveFriendResultNoticeText(0x02E1, kind));
                        return true;

                    case SocialListClientFriendResultKind.NoticeTargetUnknown:
                        packet = new SocialListClientFriendResultPacket(kind, Array.Empty<SocialListClientFriendEntry>(), null, 0, 0, 0, ResolveFriendResultNoticeText(0x02E2, kind));
                        return true;

                    case SocialListClientFriendResultKind.NoticeSelf:
                        packet = new SocialListClientFriendResultPacket(kind, Array.Empty<SocialListClientFriendEntry>(), null, 0, 0, 0, ResolveFriendResultNoticeText(0x02E4, kind));
                        return true;

                    case SocialListClientFriendResultKind.NoticeDuplicate:
                        packet = new SocialListClientFriendResultPacket(kind, Array.Empty<SocialListClientFriendEntry>(), null, 0, 0, 0, ResolveFriendResultNoticeText(0x02E3, kind));
                        return true;

                    case SocialListClientFriendResultKind.NoticeCapacityExpanded:
                        packet = new SocialListClientFriendResultPacket(kind, Array.Empty<SocialListClientFriendEntry>(), null, 0, 0, 0, ResolveFriendResultNoticeText(0x0180, kind));
                        return true;

                    default:
                        error = $"Unsupported client friend-result subtype {(byte)kind}.";
                        return false;
                }
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryParseClientPartyResult(ReadOnlySpan<byte> payload, out SocialListClientPartyResultPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                SocialListClientPartyResultKind kind = (SocialListClientPartyResultKind)reader.ReadByte();
                switch (kind)
                {
                    case SocialListClientPartyResultKind.Invite:
                    {
                        int partyId = reader.ReadInt32();
                        string inviterName = reader.ReadMapleString16().Trim();
                        _ = reader.ReadInt32();
                        _ = reader.ReadInt32();
                        bool hasAcceptedFlag = reader.ReadByte() != 0;
                        string summary = string.IsNullOrWhiteSpace(inviterName)
                            ? $"Client OnPartyResult({(byte)kind}) opened a party invite."
                            : $"Client OnPartyResult({(byte)kind}) opened a party invite from {inviterName}.";
                        if (hasAcceptedFlag)
                        {
                            summary += " The client packet also carried an immediate invite-response flag.";
                        }

                        packet = new SocialListClientPartyResultPacket(kind, partyId, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, inviterName, summary);
                        return true;
                    }

                    case SocialListClientPartyResultKind.Load:
                    case SocialListClientPartyResultKind.Refresh:
                    {
                        int partyId = reader.ReadInt32();
                        IReadOnlyList<SocialListClientPartyEntry> entries = ReadClientPartyEntries(ref reader);
                        packet = new SocialListClientPartyResultPacket(kind, partyId, entries, 0, 0, 0, null, null);
                        return true;
                    }

                    case SocialListClientPartyResultKind.Join:
                    {
                        int partyId = reader.ReadInt32();
                        int actorId = reader.ReadInt32();
                        if (reader.ReadByte() != 0)
                        {
                            _ = reader.ReadByte();
                            string actorName = reader.ReadMapleString16();
                            IReadOnlyList<SocialListClientPartyEntry> entries = ReadClientPartyEntries(ref reader);
                            packet = new SocialListClientPartyResultPacket(kind, partyId, entries, actorId, 0, 0, actorName, null);
                            return true;
                        }

                        packet = new SocialListClientPartyResultPacket(kind, partyId, Array.Empty<SocialListClientPartyEntry>(), actorId, 0, 0, null, null);
                        return true;
                    }

                    case SocialListClientPartyResultKind.Create:
                    {
                        int partyId = reader.ReadInt32();
                        packet = new SocialListClientPartyResultPacket(kind, partyId, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x0143, kind));
                        return true;
                    }

                    case SocialListClientPartyResultKind.Notice9:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x014C, kind));
                        return true;

                    case SocialListClientPartyResultKind.Notice10:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x014D, kind));
                        return true;

                    case SocialListClientPartyResultKind.Notice16:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x014E, kind));
                        return true;

                    case SocialListClientPartyResultKind.Notice17:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x014C, kind));
                        return true;

                    case SocialListClientPartyResultKind.Notice18:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x014F, kind));
                        return true;

                    case SocialListClientPartyResultKind.NoOp19:
                        packet = new SocialListClientPartyResultPacket(
                            kind,
                            0,
                            Array.Empty<SocialListClientPartyEntry>(),
                            0,
                            0,
                            0,
                            null,
                            $"Client OnPartyResult({(byte)kind}) carried a non-roster branch and did not mutate the party list.");
                        return true;

                    case SocialListClientPartyResultKind.NoticeNamed:
                    {
                        string characterName = reader.ReadMapleString16().Trim();
                        string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                            0x158A,
                            "Party notice for %s.",
                            1,
                            out _);
                        packet = new SocialListClientPartyResultPacket(
                            kind,
                            0,
                            Array.Empty<SocialListClientPartyEntry>(),
                            0,
                            0,
                            0,
                            characterName,
                            FormatSingleStringArgument(format, characterName));
                        return true;
                    }

                    case SocialListClientPartyResultKind.Notice29:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x13D5, kind));
                        return true;

                    case SocialListClientPartyResultKind.LeaderChange:
                    {
                        int memberId = reader.ReadInt32();
                        bool isLeader = reader.ReadByte() != 0;
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), memberId, isLeader ? 1 : 0, 0, null, null);
                        return true;
                    }

                    case SocialListClientPartyResultKind.MemberJobLevel:
                    {
                        int memberId = reader.ReadInt32();
                        int level = reader.ReadInt32();
                        int jobId = reader.ReadInt32();
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), memberId, level, jobId, null, null);
                        return true;
                    }

                    case SocialListClientPartyResultKind.PqRewardSelectSuccess:
                    {
                        int actorId = reader.ReadInt32();
                        string actorName = reader.ReadMapleString16().Trim();
                        int selectedRewardSlot = reader.ReadByte();
                        string summary = string.IsNullOrWhiteSpace(actorName)
                            ? $"Client OnPartyResult({(byte)kind}) applied PQ reward slot {selectedRewardSlot} for member #{actorId}."
                            : $"Client OnPartyResult({(byte)kind}) applied PQ reward slot {selectedRewardSlot} for {actorName} (#{actorId}).";
                        packet = new SocialListClientPartyResultPacket(
                            kind,
                            0,
                            Array.Empty<SocialListClientPartyEntry>(),
                            actorId,
                            selectedRewardSlot,
                            0,
                            actorName,
                            summary);
                        return true;
                    }

                    case SocialListClientPartyResultKind.PqRewardSelectFail:
                    {
                        int failureCode = reader.ReadByte();
                        packet = new SocialListClientPartyResultPacket(
                            kind,
                            0,
                            Array.Empty<SocialListClientPartyEntry>(),
                            0,
                            failureCode,
                            0,
                            null,
                            $"Client OnPartyResult({(byte)kind}) reported a PQ reward selection failure code {failureCode}.");
                        return true;
                    }

                    case SocialListClientPartyResultKind.PqRewardReceive:
                        packet = new SocialListClientPartyResultPacket(
                            kind,
                            0,
                            Array.Empty<SocialListClientPartyEntry>(),
                            0,
                            reader.RemainingLength,
                            0,
                            null,
                            $"Client OnPartyResult({(byte)kind}) delivered a PQ reward payload ({reader.RemainingLength} byte(s)).");
                        return true;

                    case SocialListClientPartyResultKind.PqRewardRequestFail:
                        packet = new SocialListClientPartyResultPacket(
                            kind,
                            0,
                            Array.Empty<SocialListClientPartyEntry>(),
                            0,
                            reader.RemainingLength,
                            0,
                            null,
                            $"Client OnPartyResult({(byte)kind}) reported a PQ reward request failure payload ({reader.RemainingLength} byte(s)).");
                        return true;

                    case SocialListClientPartyResultKind.Notice32:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x0FF9, kind));
                        return true;

                    case SocialListClientPartyResultKind.Notice33:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x0FFB, kind));
                        return true;

                    case SocialListClientPartyResultKind.Notice34:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x0FFA, kind));
                        return true;

                    case SocialListClientPartyResultKind.Notice36:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x0153, kind));
                        return true;

                    case SocialListClientPartyResultKind.Notice37:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, ResolvePartyResultNoticeText(0x017B, kind));
                        return true;

                    case SocialListClientPartyResultKind.Notice44:
                        packet = new SocialListClientPartyResultPacket(
                            kind,
                            0,
                            Array.Empty<SocialListClientPartyEntry>(),
                            0,
                            0,
                            0,
                            null,
                            $"{ResolvePartyResultNoticeText(0x13D6, kind)} {ResolvePartyResultNoticeText(0x017B, SocialListClientPartyResultKind.Notice37)}".Trim());
                        return true;

                    case SocialListClientPartyResultKind.NoticeOptionalMessage:
                    {
                        string summary = reader.ReadByte() != 0 && reader.HasRemaining
                            ? reader.ReadMapleString16().Trim()
                            : ResolvePartyResultNoticeText(0x0156, kind);
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, summary);
                        return true;
                    }

                    case SocialListClientPartyResultKind.TownPortal:
                    {
                        int portalSlot = reader.ReadByte();
                        int fieldId = reader.ReadInt32();
                        int skillId = reader.ReadInt32();
                        int targetFieldId = reader.ReadInt32();
                        short portalX = reader.ReadInt16();
                        short portalY = reader.ReadInt16();
                        string summary =
                            $"Client OnPartyResult({(byte)kind}) updated party town portal slot {portalSlot} to field {fieldId} -> {targetFieldId} at ({portalX}, {portalY}) with skill {skillId}.";
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), portalSlot, targetFieldId, skillId, null, summary);
                        return true;
                    }

                    case SocialListClientPartyResultKind.SearchPacket:
                    case SocialListClientPartyResultKind.SearchPacket2:
                    case SocialListClientPartyResultKind.SearchPacket3:
                    case SocialListClientPartyResultKind.SearchPacket4:
                    case SocialListClientPartyResultKind.SearchPacket5:
                        packet = new SocialListClientPartyResultPacket(kind, 0, Array.Empty<SocialListClientPartyEntry>(), 0, 0, 0, null, null);
                        return true;

                    case SocialListClientPartyResultKind.SearchApply:
                    {
                        int partyId = reader.ReadInt32();
                        string actorName = reader.ReadMapleString16().Trim();
                        int fieldId = reader.ReadInt32();
                        int channelId = reader.ReadInt32();
                        string summary = string.IsNullOrWhiteSpace(actorName)
                            ? $"Client OnPartyResult({(byte)kind}) opened a party-search apply prompt for party {partyId}."
                            : $"Client OnPartyResult({(byte)kind}) opened a party-search apply prompt from {actorName} for party {partyId} at field {fieldId}, channel {channelId}.";
                        packet = new SocialListClientPartyResultPacket(kind, partyId, Array.Empty<SocialListClientPartyEntry>(), 0, fieldId, channelId, actorName, summary);
                        return true;
                    }

                    default:
                        error = $"Unsupported client party-result subtype {(byte)kind}.";
                        return false;
                }
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
                byte rawSubtype = reader.ReadByte();
                SocialListClientGuildResultKind kind = (SocialListClientGuildResultKind)rawSubtype;
                switch (kind)
                {
                    case SocialListClientGuildResultKind.GuildNameInput:
                    {
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            0,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            0,
                            0,
                            null,
                            null,
                            RawSubtype: rawSubtype,
                            UsesSharedResultNoticeFallback: false,
                            ExplicitBranchSummary: "Client OnGuildResult(1) opened the guild-name input dialog.");
                        return true;
                    }

                    case SocialListClientGuildResultKind.CreateGuildAgreement:
                    {
                        if (!reader.HasRemaining)
                        {
                            packet = new SocialListClientGuildResultPacket(
                                kind,
                                0,
                                Array.Empty<GuildRankingSeedEntry>(),
                                Array.Empty<string>(),
                                null,
                                null,
                                0,
                                0,
                                HasExplicitNotice: false,
                                null,
                                default,
                                null,
                                0,
                                0,
                                null,
                                null,
                                RawSubtype: rawSubtype,
                                UsesSharedResultNoticeFallback: false,
                                ExplicitBranchSummary: "Client OnGuildResult(3) followed the party-boss create-guild branch.");
                            return true;
                        }

                        int partyId = reader.ReadInt32();
                        string masterName = reader.HasRemaining ? reader.ReadString16().Trim() : string.Empty;
                        string guildName = reader.HasRemaining ? reader.ReadString16().Trim() : string.Empty;
                        string summary = string.IsNullOrWhiteSpace(masterName) && string.IsNullOrWhiteSpace(guildName)
                            ? $"Client OnGuildResult(3) opened the create-guild agreement flow for party {partyId}."
                            : $"Client OnGuildResult(3) opened the create-guild agreement for party {partyId}: master={masterName}, guild={guildName}.";

                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            0,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            0,
                            0,
                            null,
                            null,
                            RawSubtype: rawSubtype,
                            UsesSharedResultNoticeFallback: false,
                            ExplicitBranchSummary: summary);
                        return true;
                    }

                    case SocialListClientGuildResultKind.GuildInvite:
                    {
                        int inviterId = reader.ReadInt32();
                        string inviterName = reader.ReadString16().Trim();
                        int inviterLevel = reader.ReadInt32();
                        int inviterJobId = reader.ReadInt32();
                        string summary = string.IsNullOrWhiteSpace(inviterName)
                            ? $"Client OnGuildResult(5) opened a guild invite from #{inviterId} (Lv. {inviterLevel}, job {inviterJobId})."
                            : $"Client OnGuildResult(5) opened a guild invite from {inviterName} (#{inviterId}, Lv. {inviterLevel}, job {inviterJobId}).";

                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            0,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            0,
                            0,
                            null,
                            null,
                            RawSubtype: rawSubtype,
                            UsesSharedResultNoticeFallback: false,
                            ExplicitBranchSummary: summary);
                        return true;
                    }

                    case SocialListClientGuildResultKind.GuildMarkInput:
                    {
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            0,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            0,
                            0,
                            null,
                            null,
                            RawSubtype: rawSubtype,
                            UsesSharedResultNoticeFallback: false,
                            ExplicitBranchSummary: "Client OnGuildResult(17) opened the guild-mark input dialog.");
                        return true;
                    }

                    case SocialListClientGuildResultKind.GuildDataSnapshot:
                    {
                        int guildId = reader.ReadInt32();
                        string guildName = reader.ReadString16().Trim();
                        for (int i = 0; i < 5; i++)
                        {
                            _ = reader.ReadString16();
                        }

                        int memberCount = reader.ReadByte();
                        if (memberCount > 0)
                        {
                            _ = reader.ReadBytes(memberCount * sizeof(int));
                            _ = reader.ReadBytes(memberCount * 37);
                        }

                        _ = reader.ReadInt32(); // nMaxMemberNum
                        _ = reader.ReadUInt16(); // nMarkBg
                        _ = reader.ReadByte(); // nMarkBgColor
                        _ = reader.ReadUInt16(); // nMark
                        _ = reader.ReadByte(); // nMarkColor
                        string notice = reader.ReadString16().Trim();
                        int guildPoints = reader.ReadInt32();
                        _ = reader.ReadInt32(); // nAllianceID
                        int guildLevel = reader.ReadByte();
                        int skillCount = reader.ReadUInt16();
                        List<SocialListGuildSkillRecordPacket> skillRecords = new(Math.Max(0, skillCount));
                        for (int i = 0; i < skillCount; i++)
                        {
                            if (!TryReadGuildSkillRecord(ref reader, out SocialListGuildSkillRecordPacket skillRecord, out string skillError))
                            {
                                error = skillError;
                                return false;
                            }

                            skillRecords.Add(skillRecord);
                        }

                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            guildId,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            notice,
                            null,
                            guildPoints,
                            guildLevel,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            RawSubtype: rawSubtype,
                            GuildName: guildName,
                            GuildSkillRecords: skillRecords);
                        return true;
                    }

                    case SocialListClientGuildResultKind.Notice35:
                    case SocialListClientGuildResultKind.Notice37:
                    case SocialListClientGuildResultKind.Notice42:
                    case SocialListClientGuildResultKind.Notice43:
                    case SocialListClientGuildResultKind.Notice44:
                    case SocialListClientGuildResultKind.Notice47:
                    case SocialListClientGuildResultKind.Notice50:
                    case SocialListClientGuildResultKind.Notice58:
                    {
                        int stringPoolId = ResolveClientGuildResultNoticeStringPoolId(rawSubtype);
                        string directNotice = ResolveClientGuildResultDirectNoticeText(stringPoolId, rawSubtype);
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            0,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            0,
                            0,
                            null,
                            directNotice,
                            RawSubtype: rawSubtype);
                        return true;
                    }

                    case SocialListClientGuildResultKind.Notice55:
                    case SocialListClientGuildResultKind.Notice56:
                    case SocialListClientGuildResultKind.Notice57:
                    {
                        string decodedName = reader.ReadString16().Trim();
                        int stringPoolId = ResolveClientGuildResultNoticeStringPoolId(rawSubtype);
                        string directNotice = ResolveClientGuildResultNamedNoticeText(stringPoolId, decodedName, rawSubtype);
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            0,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            0,
                            0,
                            null,
                            directNotice,
                            RawSubtype: rawSubtype);
                        return true;
                    }

                    case SocialListClientGuildResultKind.GradeChange:
                    {
                        int guildId = reader.ReadInt32();
                        int memberId = reader.ReadInt32();
                        int absoluteGrade = reader.ReadByte();
                        if (memberId <= 0)
                        {
                            error = "Client guild-result grade-change member id must be positive.";
                            return false;
                        }

                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            guildId,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            new SocialListGradeChangePacket(memberId, 0, absoluteGrade, null),
                            null,
                            RawSubtype: rawSubtype);
                        return true;
                    }

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

                        packet = new SocialListClientGuildResultPacket(kind, guildId, rankingEntries, Array.Empty<string>(), null, null, 0, 0, HasExplicitNotice: false, null, default, null, RawSubtype: rawSubtype);
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

                        packet = new SocialListClientGuildResultPacket(kind, guildId, Array.Empty<GuildRankingSeedEntry>(), titles, null, null, 0, 0, HasExplicitNotice: false, null, default, null, RawSubtype: rawSubtype);
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
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            RawSubtype: rawSubtype);
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
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            RawSubtype: rawSubtype);
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
                            guildLevel,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            RawSubtype: rawSubtype);
                        return true;
                    }

                    case SocialListClientGuildResultKind.SkillRecord:
                    {
                        int guildId = reader.ReadInt32();
                        int skillId = reader.ReadInt32();
                        int skillLevel = reader.ReadUInt16();
                        long expirationFileTime = reader.ReadInt64();
                        string buyCharacterName = reader.ReadString16().Trim();
                        if (skillId <= 0)
                        {
                            error = "Client guild-result skill-record skill id must be positive.";
                            return false;
                        }

                        DateTimeOffset? expiration = null;
                        if (expirationFileTime > 0)
                        {
                            try
                            {
                                expiration = DateTimeOffset.FromFileTime(expirationFileTime);
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                expiration = null;
                            }
                        }

                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            guildId,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            new SocialListGuildSkillRecordPacket(skillId, skillLevel, expiration, buyCharacterName),
                            RawSubtype: rawSubtype);
                        return true;
                    }

                    case SocialListClientGuildResultKind.GuildQuestQueueNotice:
                    {
                        int channel = reader.ReadByte();
                        int waitStatus = reader.ReadInt32();
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            0,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            channel,
                            waitStatus,
                            RawSubtype: rawSubtype);
                        return true;
                    }

                    case SocialListClientGuildResultKind.GuildBoardAuthKey:
                    {
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            0,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            GuildBoardAuthKey: reader.ReadString16().Trim(),
                            RawSubtype: rawSubtype);
                        return true;
                    }

                    case SocialListClientGuildResultKind.GuildQuestNotEnoughMembers:
                    case SocialListClientGuildResultKind.GuildQuestRegistrantDisconnected:
                    {
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            0,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            RawSubtype: rawSubtype);
                        return true;
                    }

                    case SocialListClientGuildResultKind.ResultNotice:
                    {
                        bool hasExplicitNotice = reader.ReadBoolean();
                        string resultNotice = hasExplicitNotice
                            ? reader.ReadString16().Trim()
                            : null;
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            0,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            hasExplicitNotice,
                            resultNotice,
                            default,
                            null,
                            RawSubtype: rawSubtype);
                        return true;
                    }

                    default:
                    {
                        bool usesSharedFallback = IsClientGuildResultSharedFallbackNoticeSubtype(rawSubtype);
                        packet = new SocialListClientGuildResultPacket(
                            kind,
                            0,
                            Array.Empty<GuildRankingSeedEntry>(),
                            Array.Empty<string>(),
                            null,
                            null,
                            0,
                            0,
                            HasExplicitNotice: false,
                            null,
                            default,
                            null,
                            RawSubtype: rawSubtype,
                            UsesSharedResultNoticeFallback: usesSharedFallback);
                        return true;
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool IsClientGuildResultSharedFallbackNoticeSubtype(byte subtype)
        {
            // CWvsContext::OnGuildResult uses shared StringPool 0x176 in the default switch branch.
            // Verified jump-table defaults in MapleStory v95 at 0xA0D41B.
            return subtype > 82 || subtype switch
            {
                2 or 4 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16
                    or 18 or 19 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27
                    or 29 or 31 or 32 or 36 or 39 or 45 or 48 or 51 or 53 or 59
                    or 65 or 67 or 68 or 70 or 72 or 73 or 74 => true,
                _ => false
            };
        }

        private static int ResolveClientGuildResultNoticeStringPoolId(byte subtype)
        {
            return subtype switch
            {
                35 => 0x0169,
                37 => 0x016A,
                42 => 0x0169,
                43 => 0x016D,
                44 => 0x0177,
                47 => 0x016B,
                50 => 0x016B,
                55 => 0x015B,
                56 => 0x0ACF,
                57 => 0x015C,
                58 => 0x0174,
                _ => 0
            };
        }

        private static string ResolveClientGuildResultDirectNoticeText(int stringPoolId, byte subtype)
        {
            if (stringPoolId <= 0)
            {
                return $"Client OnGuildResult({subtype}) notice.";
            }

            return MapleStoryStringPool.GetOrFallback(
                stringPoolId,
                $"Client OnGuildResult({subtype}) notice.",
                appendFallbackSuffix: true,
                minimumHexWidth: 3);
        }

        private static string ResolveClientGuildResultNamedNoticeText(int stringPoolId, string name, byte subtype)
        {
            string fallbackFormat = "User: %s";
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                1,
                out _);
            return FormatSingleStringArgument(format, name);
        }

        private static bool TryReadGuildSkillRecord(
            ref PacketReader reader,
            out SocialListGuildSkillRecordPacket skillRecord,
            out string error)
        {
            skillRecord = default;
            error = null;

            int skillId = reader.ReadInt32();
            int skillLevel = reader.ReadUInt16();
            long expirationFileTime = reader.ReadInt64();
            string buyCharacterName = reader.ReadString16().Trim();
            if (skillId <= 0)
            {
                error = "Client guild-result skill-record skill id must be positive.";
                return false;
            }

            DateTimeOffset? expiration = null;
            if (expirationFileTime > 0)
            {
                try
                {
                    expiration = DateTimeOffset.FromFileTime(expirationFileTime);
                }
                catch (ArgumentOutOfRangeException)
                {
                    expiration = null;
                }
            }

            skillRecord = new SocialListGuildSkillRecordPacket(skillId, skillLevel, expiration, buyCharacterName);
            return true;
        }

        public static bool TryParseGuildSkillResult(ReadOnlySpan<byte> payload, out GuildSkillResultPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                PacketReader reader = new(payload);
                GuildSkillResultPacketKind kind = (GuildSkillResultPacketKind)reader.ReadByte();
                if (kind == GuildSkillResultPacketKind.FundSync)
                {
                    int syncedGuildFundMeso = reader.ReadInt32();
                    string fundSummary = reader.HasRemaining ? reader.ReadString16().Trim() : null;
                    packet = new GuildSkillResultPacket(
                        kind,
                        0,
                        Approved: true,
                        null,
                        null,
                        syncedGuildFundMeso,
                        fundSummary);
                    return true;
                }

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
                    case SocialListClientAllianceResultKind.GradeChange:
                    {
                        int memberId = reader.ReadInt32();
                        int absoluteGrade = reader.ReadByte();
                        if (memberId <= 0)
                        {
                            error = "Client alliance-result grade-change member id must be positive.";
                            return false;
                        }

                        packet = new SocialListClientAllianceResultPacket(
                            kind,
                            0,
                            Array.Empty<string>(),
                            null,
                            new SocialListGradeChangePacket(memberId, 0, absoluteGrade, null));
                        return true;
                    }

                    case SocialListClientAllianceResultKind.RankTitles:
                    {
                        int allianceId = reader.ReadInt32();
                        string[] titles = new string[5];
                        for (int i = 0; i < titles.Length; i++)
                        {
                            titles[i] = NormalizeRoleLabel(reader.ReadString16(), $"Rank {i + 1}");
                        }

                        packet = new SocialListClientAllianceResultPacket(kind, allianceId, titles, null, default);
                        return true;
                    }

                    case SocialListClientAllianceResultKind.Notice:
                    {
                        int allianceId = reader.ReadInt32();
                        packet = new SocialListClientAllianceResultPacket(kind, allianceId, Array.Empty<string>(), reader.ReadString16().Trim(), default);
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

                packet = new SocialListGradeChangePacket(memberId, delta > 0 ? 1 : -1, null, reader.HasRemaining ? reader.ReadString16().Trim() : null);
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

        private static SocialListClientFriendEntry ReadClientFriendEntry(ref PacketReader reader, bool inShop)
        {
            int friendId = reader.ReadInt32();
            string name = reader.ReadFixedString(13);
            byte flag = reader.ReadByte();
            int channelId = reader.ReadInt32();
            string groupName = reader.ReadFixedString(17);
            return new SocialListClientFriendEntry(
                friendId,
                string.IsNullOrWhiteSpace(name) ? $"Friend {friendId}" : name.Trim(),
                string.IsNullOrWhiteSpace(groupName) ? string.Empty : groupName.Trim(),
                channelId,
                flag,
                inShop);
        }

        private static string ResolveFriendResultNoticeText(int stringPoolId, SocialListClientFriendResultKind kind)
        {
            return MapleStoryStringPool.GetOrFallback(
                stringPoolId,
                $"Client OnFriendResult({(byte)kind}) notice.",
                appendFallbackSuffix: true,
                minimumHexWidth: 3);
        }

        private static string ResolvePartyResultNoticeText(int stringPoolId, SocialListClientPartyResultKind kind)
        {
            return MapleStoryStringPool.GetOrFallback(
                stringPoolId,
                $"Client OnPartyResult({(byte)kind}) notice.",
                appendFallbackSuffix: true,
                minimumHexWidth: 3);
        }

        private static string FormatSingleStringArgument(string format, string value)
        {
            string normalizedValue = string.IsNullOrWhiteSpace(value) ? "the character" : value.Trim();
            string normalizedFormat = string.IsNullOrWhiteSpace(format) ? "%s" : format;
            try
            {
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    normalizedFormat.Replace("%s", "{0}", StringComparison.Ordinal),
                    normalizedValue);
            }
            catch (FormatException)
            {
                return $"{normalizedFormat} {normalizedValue}".Trim();
            }
        }

        private static IReadOnlyList<SocialListClientPartyEntry> ReadClientPartyEntries(ref PacketReader reader)
        {
            ReadOnlySpan<byte> partyData = reader.ReadBytes(0x17A);

            const int memberCount = 6;
            const int nameLength = 13;
            const int idsOffset = 0;
            const int namesOffset = idsOffset + (memberCount * sizeof(int));
            const int jobsOffset = namesOffset + (memberCount * nameLength);
            const int levelsOffset = jobsOffset + (memberCount * sizeof(int));
            const int channelsOffset = levelsOffset + (memberCount * sizeof(int));
            const int leaderOffset = channelsOffset + (memberCount * sizeof(int));
            const int fieldIdsOffset = leaderOffset + sizeof(int);

            int leaderId = ReadInt32(partyData, leaderOffset);
            List<SocialListClientPartyEntry> entries = new(memberCount);
            for (int i = 0; i < memberCount; i++)
            {
                int memberId = ReadInt32(partyData, idsOffset + (i * sizeof(int)));
                if (memberId <= 0)
                {
                    continue;
                }

                string name = ReadFixedString(partyData.Slice(namesOffset + (i * nameLength), nameLength));
                int jobId = ReadInt32(partyData, jobsOffset + (i * sizeof(int)));
                int level = ReadInt32(partyData, levelsOffset + (i * sizeof(int)));
                int channelId = ReadInt32(partyData, channelsOffset + (i * sizeof(int)));
                int fieldId = ReadInt32(partyData, fieldIdsOffset + (i * sizeof(int)));
                entries.Add(new SocialListClientPartyEntry(
                    memberId,
                    string.IsNullOrWhiteSpace(name) ? $"Member {memberId}" : name.Trim(),
                    jobId,
                    Math.Max(1, level),
                    channelId,
                    fieldId,
                    memberId == leaderId));
            }

            return entries;
        }

        private static int ReadInt32(ReadOnlySpan<byte> buffer, int offset)
        {
            return buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24);
        }

        private static string ReadFixedString(ReadOnlySpan<byte> buffer)
        {
            int length = buffer.IndexOf((byte)0);
            if (length < 0)
            {
                length = buffer.Length;
            }

            return length == 0 ? string.Empty : Encoding.UTF8.GetString(buffer[..length]);
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
            public int RemainingLength => _payload.Length - _offset;

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

            public short ReadInt16()
            {
                return unchecked((short)ReadUInt16());
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

            public long ReadInt64()
            {
                EnsureRemaining(8);
                uint low = (uint)(_payload[_offset]
                    | (_payload[_offset + 1] << 8)
                    | (_payload[_offset + 2] << 16)
                    | (_payload[_offset + 3] << 24));
                uint high = (uint)(_payload[_offset + 4]
                    | (_payload[_offset + 5] << 8)
                    | (_payload[_offset + 6] << 16)
                    | (_payload[_offset + 7] << 24));
                _offset += 8;
                return (long)((ulong)low | ((ulong)high << 32));
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

            public string ReadMapleString16()
            {
                return ReadString16();
            }

            public string ReadFixedString(int length)
            {
                EnsureRemaining(length);
                string value = SocialListPacketCodec.ReadFixedString(_payload.Slice(_offset, length));
                _offset += length;
                return value;
            }

            public ReadOnlySpan<byte> ReadBytes(int count)
            {
                EnsureRemaining(count);
                ReadOnlySpan<byte> value = _payload.Slice(_offset, count);
                _offset += count;
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
