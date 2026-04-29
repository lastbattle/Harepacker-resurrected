using MapleLib.PacketLib;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class SocialListOutboundPacketCodec
    {
        public const byte FriendAddRequest = 1;
        public const byte FriendDeleteRequest = 2;
        public const byte PartyCreateRequest = 1;
        public const byte PartyInviteRequest = 4;
        public const byte PartyKickRequest = 5;
        public const byte PartyWithdrawRequest = 6;
        public const byte PartyChangeBossRequest = 7;
        public const byte GuildInviteRequest = 5;
        public const byte GuildKickRequest = 7;
        public const byte GuildWithdrawRequest = 9;
        public const byte GuildGradeChangeRequest = 15;
        public const byte AllianceInviteRequest = 4;
        public const byte AllianceKickRequest = 6;
        public const byte AllianceWithdrawRequest = 8;
        public const byte AllianceGradeChangeRequest = 11;
        public const byte BlacklistAddRequest = 18;
        public const byte BlacklistDeleteRequest = 19;

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
                case SocialListOutboundRequestKind.PartyWithdraw:
                case SocialListOutboundRequestKind.GuildWithdraw:
                case SocialListOutboundRequestKind.AllianceWithdraw:
                    return;

                case SocialListOutboundRequestKind.FriendDelete:
                case SocialListOutboundRequestKind.PartyKick:
                case SocialListOutboundRequestKind.PartyChangeBoss:
                case SocialListOutboundRequestKind.GuildKick:
                case SocialListOutboundRequestKind.AllianceKick:
                case SocialListOutboundRequestKind.BlacklistDelete:
                    writer.WriteInt(Math.Max(0, draft.MemberId));
                    writer.WriteMapleString(NormalizeTarget(draft.TargetName));
                    return;

                case SocialListOutboundRequestKind.GuildGradeChange:
                case SocialListOutboundRequestKind.AllianceGradeChange:
                    writer.WriteInt(Math.Max(0, draft.MemberId));
                    writer.Write(draft.Value >= 0 ? (byte)1 : (byte)0);
                    writer.WriteMapleString(NormalizeTarget(draft.TargetName));
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
