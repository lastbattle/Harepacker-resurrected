using MapleLib.PacketLib;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class SocialListOutboundPacketCodec
    {
        public const byte FriendAddRequest = 1;
        public const byte FriendDeleteRequest = 3;
        public const byte PartyCreateRequest = 1;
        public const byte PartyWithdrawRequest = 2;
        public const byte PartyInviteRequest = 4;
        public const byte PartyKickRequest = 5;
        public const byte PartyChangeBossRequest = 6;
        public const byte GuildInviteRequest = 5;
        public const byte GuildWithdrawRequest = 7;
        public const byte GuildKickRequest = 8;
        public const byte GuildGradeChangeRequest = 14;
        public const byte AllianceInviteRequest = 3;
        public const byte AllianceKickRequest = 6;
        public const byte AllianceWithdrawRequest = 2;
        public const byte AllianceGradeChangeRequest = 9;
        public const byte BlacklistAddRequest = 31;
        public const byte BlacklistDeleteRequest = 32;

        public static bool TryBuildOutboundRequest(
            SocialListOutboundRequestDraft draft,
            ushort opcode,
            out SocialListOutboundRequest request,
            out string error)
        {
            request = default;
            error = null;
            if (opcode == 0)
            {
                error = $"No outbound opcode is configured for {draft.Kind}.";
                return false;
            }

            if (!TryResolveSubtype(draft.Kind, out byte subtype))
            {
                error = $"No social-list outbound request subtype is mapped for {draft.Kind}.";
                return false;
            }

            if (draft.Kind == SocialListOutboundRequestKind.AllianceKick
                && (draft.MemberId <= 0 || draft.SecondaryValue <= 0))
            {
                error = "Alliance kick requires the client-owned target guild id and alliance id payload.";
                return false;
            }

            if (draft.Kind == SocialListOutboundRequestKind.GuildWithdraw
                && (draft.MemberId <= 0 || string.IsNullOrWhiteSpace(draft.TargetName)))
            {
                error = "Guild withdraw requires the client-owned local character id and local character name payload.";
                return false;
            }

            PacketWriter writer = new();
            writer.WriteShort(opcode);
            writer.Write(subtype);
            WritePayload(writer, draft);

            byte[] rawPacket = writer.ToArray();
            request = new SocialListOutboundRequest(
                draft.Kind,
                opcode,
                subtype,
                rawPacket,
                $"Social-list outbound request draft {draft.Kind}: opcode={opcode}; subtype={subtype}; target={NormalizeTarget(draft.TargetName)}; memberId={Math.Max(0, draft.MemberId)}; value={draft.Value}; raw={Convert.ToHexString(rawPacket)}");
            return true;
        }

        internal static string DescribeOpcodeFramedOutboundRequest(
            ReadOnlySpan<byte> rawPacket,
            ushort friendRequestOpcode,
            ushort partyRequestOpcode,
            ushort guildRequestOpcode,
            ushort allianceRequestOpcode,
            ushort blacklistRequestOpcode)
        {
            if (rawPacket.Length < sizeof(ushort) + 1)
            {
                return "client-request:unknown";
            }

            ushort opcode = (ushort)(rawPacket[0] | (rawPacket[1] << 8));
            byte subtype = rawPacket[sizeof(ushort)];
            if (friendRequestOpcode > 0 && opcode == friendRequestOpcode)
            {
                return subtype switch
                {
                    FriendAddRequest => "friend-request:add",
                    FriendDeleteRequest => "friend-request:delete",
                    _ => $"friend-request:subtype-{subtype}"
                };
            }

            if (partyRequestOpcode > 0 && opcode == partyRequestOpcode)
            {
                return subtype switch
                {
                    PartyCreateRequest => "party-request:create",
                    PartyWithdrawRequest => "party-request:withdraw",
                    PartyInviteRequest => "party-request:invite",
                    PartyKickRequest => "party-request:kick",
                    PartyChangeBossRequest => "party-request:change-boss",
                    _ => $"party-request:subtype-{subtype}"
                };
            }

            if (guildRequestOpcode > 0 && opcode == guildRequestOpcode)
            {
                return subtype switch
                {
                    GuildInviteRequest => "guild-request:invite",
                    GuildWithdrawRequest => "guild-request:withdraw",
                    GuildKickRequest => "guild-request:kick",
                    GuildGradeChangeRequest => "guild-request:grade-change",
                    _ => $"guild-request:subtype-{subtype}"
                };
            }

            if (allianceRequestOpcode > 0 && opcode == allianceRequestOpcode)
            {
                return subtype switch
                {
                    AllianceWithdrawRequest => "alliance-request:withdraw",
                    AllianceInviteRequest => "alliance-request:invite",
                    AllianceKickRequest => "alliance-request:kick",
                    AllianceGradeChangeRequest => "alliance-request:grade-change",
                    _ => $"alliance-request:subtype-{subtype}"
                };
            }

            if (blacklistRequestOpcode > 0 && opcode == blacklistRequestOpcode)
            {
                return subtype switch
                {
                    BlacklistAddRequest => "blacklist-request:add",
                    BlacklistDeleteRequest => "blacklist-request:delete",
                    _ => $"blacklist-request:subtype-{subtype}"
                };
            }

            return "client-request";
        }

        private static bool TryResolveSubtype(SocialListOutboundRequestKind kind, out byte subtype)
        {
            subtype = kind switch
            {
                SocialListOutboundRequestKind.FriendAdd => FriendAddRequest,
                SocialListOutboundRequestKind.FriendDelete => FriendDeleteRequest,
                SocialListOutboundRequestKind.PartyCreate => PartyCreateRequest,
                SocialListOutboundRequestKind.PartyInvite => PartyInviteRequest,
                SocialListOutboundRequestKind.PartyKick => PartyKickRequest,
                SocialListOutboundRequestKind.PartyWithdraw => PartyWithdrawRequest,
                SocialListOutboundRequestKind.PartyChangeBoss => PartyChangeBossRequest,
                SocialListOutboundRequestKind.GuildInvite => GuildInviteRequest,
                SocialListOutboundRequestKind.GuildKick => GuildKickRequest,
                SocialListOutboundRequestKind.GuildWithdraw => GuildWithdrawRequest,
                SocialListOutboundRequestKind.GuildGradeChange => GuildGradeChangeRequest,
                SocialListOutboundRequestKind.AllianceInvite => AllianceInviteRequest,
                SocialListOutboundRequestKind.AllianceKick => AllianceKickRequest,
                SocialListOutboundRequestKind.AllianceWithdraw => AllianceWithdrawRequest,
                SocialListOutboundRequestKind.AllianceGradeChange => AllianceGradeChangeRequest,
                SocialListOutboundRequestKind.BlacklistAdd => BlacklistAddRequest,
                SocialListOutboundRequestKind.BlacklistDelete => BlacklistDeleteRequest,
                _ => 0
            };
            return subtype != 0;
        }

        private static void WritePayload(PacketWriter writer, SocialListOutboundRequestDraft draft)
        {
            switch (draft.Kind)
            {
                case SocialListOutboundRequestKind.PartyCreate:
                case SocialListOutboundRequestKind.AllianceWithdraw:
                    return;

                case SocialListOutboundRequestKind.FriendAdd:
                    writer.WriteMapleString(NormalizeTarget(draft.TargetName));
                    writer.WriteMapleString(NormalizeTarget(draft.GroupName));
                    return;

                case SocialListOutboundRequestKind.PartyWithdraw:
                    writer.Write((byte)0);
                    return;

                case SocialListOutboundRequestKind.FriendDelete:
                    writer.WriteInt(Math.Max(0, draft.MemberId));
                    return;

                case SocialListOutboundRequestKind.GuildWithdraw:
                case SocialListOutboundRequestKind.GuildKick:
                    writer.WriteInt(Math.Max(0, draft.MemberId));
                    writer.WriteMapleString(NormalizeTarget(draft.TargetName));
                    return;

                case SocialListOutboundRequestKind.AllianceKick:
                    writer.WriteInt(draft.MemberId);
                    writer.WriteInt(draft.SecondaryValue);
                    return;

                case SocialListOutboundRequestKind.PartyKick:
                case SocialListOutboundRequestKind.PartyChangeBoss:
                    writer.WriteInt(Math.Max(0, draft.MemberId));
                    return;

                case SocialListOutboundRequestKind.GuildGradeChange:
                    writer.WriteInt(Math.Max(0, draft.MemberId));
                    writer.Write((byte)Math.Clamp(draft.Value, 1, 5));
                    return;

                case SocialListOutboundRequestKind.AllianceGradeChange:
                    writer.WriteInt(Math.Max(0, draft.MemberId));
                    writer.Write(draft.Value >= 0 ? (byte)1 : (byte)0);
                    return;

                default:
                    writer.WriteMapleString(NormalizeTarget(draft.TargetName));
                    if (draft.MemberId > 0)
                    {
                        writer.WriteInt(draft.MemberId);
                    }

                    return;
            }
        }

        private static string NormalizeTarget(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
