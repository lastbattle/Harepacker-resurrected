using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum SocialListOutboundRequestKind
    {
        FriendAdd,
        FriendDelete,
        PartyCreate,
        PartyInvite,
        PartyKick,
        PartyWithdraw,
        PartyChangeBoss,
        GuildInvite,
        GuildKick,
        GuildWithdraw,
        GuildGradeChange,
        AllianceInvite,
        AllianceKick,
        AllianceWithdraw,
        AllianceGradeChange,
        BlacklistAdd,
        BlacklistDelete
    }

    internal readonly record struct SocialListOutboundRequestDraft(
        SocialListOutboundRequestKind Kind,
        string TargetName = null,
        int MemberId = 0,
        int Value = 0,
        int SecondaryValue = 0,
        string GroupName = null);

    internal readonly record struct SocialListPacketOwnedRequestDraft(
        SocialListTab Tab,
        string RequestKind,
        SocialListOutboundRequestDraft OutboundRequest);

    internal readonly record struct SocialListOutboundRequest(
        SocialListOutboundRequestKind Kind,
        ushort Opcode,
        byte Subtype,
        byte[] RawPacket,
        string Detail)
    {
        internal bool IsValid => RawPacket is { Length: > 0 };

        internal string Describe()
        {
            return string.IsNullOrWhiteSpace(Detail)
                ? $"{Kind} opcode={Opcode}, subtype={Subtype}, raw={Convert.ToHexString(RawPacket ?? Array.Empty<byte>())}"
                : Detail;
        }
    }
}
