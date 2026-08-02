using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class SocialListOutboundRequestCodec
    {
        private const byte FriendAddSubtype = 1;
        private const byte FriendDeleteSubtype = 3;
        private const byte PartyCreateSubtype = 1;
        private const byte PartyInviteSubtype = 4;
        private const byte PartyWithdrawSubtype = 2;
        private const byte PartyKickSubtype = 5;
        private const byte PartyChangeBossSubtype = 6;
        private const byte GuildInviteSubtype = 5;
        private const byte GuildWithdrawSubtype = 7;
        private const byte GuildKickSubtype = 8;
        private const byte GuildGradeChangeSubtype = 14;
        private const byte AllianceInviteSubtype = 3;
        private const byte AllianceWithdrawSubtype = 2;
        private const byte AllianceKickSubtype = 6;
        private const byte AllianceGradeChangeSubtype = 9;
        private const byte BlacklistAddSubtype = 31;
        private const byte BlacklistDeleteSubtype = 32;

        internal static bool TryBuild(
            SocialListOutboundRequestDraft draft,
            IReadOnlyDictionary<SocialListTab, ushort> opcodes,
            out SocialListOutboundRequest request,
            out string error)
        {
            request = default;
            error = null;

            SocialListTab tab = ResolveRequestTab(draft.Kind);
            if (opcodes == null || !opcodes.TryGetValue(tab, out ushort opcode) || opcode == 0)
            {
                error = $"No outbound opcode is configured for the {tab} social-list request family.";
                return false;
            }

            byte subtype = ResolveSubtype(draft.Kind);
            byte[] payload = BuildPayload(draft, subtype);
            byte[] rawPacket = new byte[payload.Length + sizeof(ushort)];
            rawPacket[0] = (byte)(opcode & 0xFF);
            rawPacket[1] = (byte)(opcode >> 8);
            Buffer.BlockCopy(payload, 0, rawPacket, sizeof(ushort), payload.Length);

            request = new SocialListOutboundRequest(
                draft.Kind,
                opcode,
                subtype,
                rawPacket,
                $"Built {draft.Kind} social-list outbound request opcode={opcode}, subtype={subtype}, payload={Convert.ToHexString(payload)}.");
            return true;
        }

        private static SocialListTab ResolveRequestTab(SocialListOutboundRequestKind kind)
        {
            return kind switch
            {
                SocialListOutboundRequestKind.PartyCreate
                    or SocialListOutboundRequestKind.PartyInvite
                    or SocialListOutboundRequestKind.PartyKick
                    or SocialListOutboundRequestKind.PartyWithdraw
                    or SocialListOutboundRequestKind.PartyChangeBoss => SocialListTab.Party,
                SocialListOutboundRequestKind.GuildInvite
                    or SocialListOutboundRequestKind.GuildKick
                    or SocialListOutboundRequestKind.GuildWithdraw
                    or SocialListOutboundRequestKind.GuildGradeChange => SocialListTab.Guild,
                SocialListOutboundRequestKind.AllianceInvite
                    or SocialListOutboundRequestKind.AllianceKick
                    or SocialListOutboundRequestKind.AllianceWithdraw
                    or SocialListOutboundRequestKind.AllianceGradeChange => SocialListTab.Alliance,
                SocialListOutboundRequestKind.BlacklistAdd
                    or SocialListOutboundRequestKind.BlacklistDelete => SocialListTab.Blacklist,
                _ => SocialListTab.Friend
            };
        }

        private static byte ResolveSubtype(SocialListOutboundRequestKind kind)
        {
            return kind switch
            {
                SocialListOutboundRequestKind.FriendDelete => FriendDeleteSubtype,
                SocialListOutboundRequestKind.PartyCreate => PartyCreateSubtype,
                SocialListOutboundRequestKind.PartyInvite => PartyInviteSubtype,
                SocialListOutboundRequestKind.PartyKick => PartyKickSubtype,
                SocialListOutboundRequestKind.PartyWithdraw => PartyWithdrawSubtype,
                SocialListOutboundRequestKind.PartyChangeBoss => PartyChangeBossSubtype,
                SocialListOutboundRequestKind.GuildInvite => GuildInviteSubtype,
                SocialListOutboundRequestKind.GuildKick => GuildKickSubtype,
                SocialListOutboundRequestKind.GuildWithdraw => GuildWithdrawSubtype,
                SocialListOutboundRequestKind.GuildGradeChange => GuildGradeChangeSubtype,
                SocialListOutboundRequestKind.AllianceInvite => AllianceInviteSubtype,
                SocialListOutboundRequestKind.AllianceKick => AllianceKickSubtype,
                SocialListOutboundRequestKind.AllianceWithdraw => AllianceWithdrawSubtype,
                SocialListOutboundRequestKind.AllianceGradeChange => AllianceGradeChangeSubtype,
                SocialListOutboundRequestKind.BlacklistAdd => BlacklistAddSubtype,
                SocialListOutboundRequestKind.BlacklistDelete => BlacklistDeleteSubtype,
                _ => FriendAddSubtype
            };
        }

        private static byte[] BuildPayload(SocialListOutboundRequestDraft draft, byte subtype)
        {
            using MemoryStream stream = new();
            stream.WriteByte(subtype);

            switch (draft.Kind)
            {
                case SocialListOutboundRequestKind.FriendAdd:
                    WriteMapleString(stream, draft.TargetName);
                    WriteMapleString(stream, draft.GroupName);
                    break;
                case SocialListOutboundRequestKind.FriendDelete:
                    WriteInt32(stream, Math.Max(0, draft.MemberId));
                    break;
                case SocialListOutboundRequestKind.PartyInvite:
                case SocialListOutboundRequestKind.GuildInvite:
                case SocialListOutboundRequestKind.AllianceInvite:
                case SocialListOutboundRequestKind.BlacklistAdd:
                case SocialListOutboundRequestKind.BlacklistDelete:
                    WriteTarget(stream, draft);
                    break;
                case SocialListOutboundRequestKind.PartyKick:
                case SocialListOutboundRequestKind.PartyChangeBoss:
                    WriteInt32(stream, Math.Max(0, draft.MemberId));
                    break;
                case SocialListOutboundRequestKind.GuildKick:
                    WriteInt32(stream, Math.Max(0, draft.MemberId));
                    WriteMapleString(stream, draft.TargetName);
                    break;
                case SocialListOutboundRequestKind.GuildWithdraw:
                    WriteInt32(stream, Math.Max(0, draft.MemberId));
                    WriteMapleString(stream, draft.TargetName);
                    break;
                case SocialListOutboundRequestKind.AllianceKick:
                    WriteInt32(stream, Math.Max(0, draft.MemberId));
                    WriteInt32(stream, Math.Max(0, draft.SecondaryValue));
                    break;
                case SocialListOutboundRequestKind.AllianceGradeChange:
                    WriteInt32(stream, Math.Max(0, draft.MemberId));
                    stream.WriteByte(unchecked((byte)Math.Clamp(draft.Value, -1, 1)));
                    break;
                case SocialListOutboundRequestKind.GuildGradeChange:
                    WriteInt32(stream, Math.Max(0, draft.MemberId));
                    stream.WriteByte((byte)Math.Clamp(draft.Value, 1, 5));
                    break;
                case SocialListOutboundRequestKind.PartyWithdraw:
                    stream.WriteByte(0);
                    break;
                case SocialListOutboundRequestKind.PartyCreate:
                case SocialListOutboundRequestKind.AllianceWithdraw:
                    break;
            }

            return stream.ToArray();
        }

        private static void WriteTarget(Stream stream, SocialListOutboundRequestDraft draft)
        {
            if (draft.MemberId > 0)
            {
                WriteInt32(stream, draft.MemberId);
            }

            WriteMapleString(stream, draft.TargetName);
        }

        private static void WriteInt32(Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 24) & 0xFF));
        }

        private static void WriteMapleString(Stream stream, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim());
            ushort length = (ushort)Math.Min(bytes.Length, ushort.MaxValue);
            stream.WriteByte((byte)(length & 0xFF));
            stream.WriteByte((byte)(length >> 8));
            stream.Write(bytes, 0, length);
        }
    }
}
