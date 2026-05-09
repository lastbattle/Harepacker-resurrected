using MapleLib.PacketLib;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum SocialListGuildDialogRequestKind
    {
        CreateGuild,
        SetMark,
        CreateGuildAgreement
    }

    internal readonly record struct SocialListGuildDialogRequestPacket(
        SocialListGuildDialogRequestKind Kind,
        string GuildName,
        GuildMarkSelection? MarkSelection,
        int PartyId = 0,
        bool Accepted = true);

    internal static class SocialListGuildDialogRequestCodec
    {
        public const byte CreateGuildRequest = 2;
        public const byte SetGuildMarkRequest = 15;
        public const byte CreateGuildAgreementRequest = 32;

        public static bool TryParse(
            byte[] rawPacket,
            ushort createGuildOpcode,
            ushort setGuildMarkOpcode,
            out SocialListGuildDialogRequestPacket packet,
            out string error)
        {
            packet = default;
            error = null;

            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                error = "Guild dialog request packet is missing the 2-byte opcode.";
                return false;
            }

            try
            {
                PacketReader reader = new(rawPacket);
                ushort opcode = (ushort)reader.ReadShort();
                if (createGuildOpcode == 0 && setGuildMarkOpcode == 0)
                {
                    error = "No guild dialog request opcode is configured.";
                    return false;
                }

                if ((createGuildOpcode != 0 && opcode != createGuildOpcode)
                    && (setGuildMarkOpcode != 0 && opcode != setGuildMarkOpcode))
                {
                    error = $"Opcode {opcode} is not a configured guild create or guild mark request.";
                    return false;
                }

                byte subtype = reader.ReadByte();
                if (subtype == CreateGuildRequest)
                {
                    string guildName = NormalizeGuildName(reader.ReadMapleString());
                    packet = new SocialListGuildDialogRequestPacket(
                        SocialListGuildDialogRequestKind.CreateGuild,
                        guildName,
                        null);
                    return true;
                }

                if (subtype == SetGuildMarkRequest)
                {
                    GuildMarkSelection selection = new(
                        unchecked((ushort)reader.ReadShort()),
                        reader.ReadByte(),
                        unchecked((ushort)reader.ReadShort()),
                        reader.ReadByte(),
                        0);
                    packet = new SocialListGuildDialogRequestPacket(
                        SocialListGuildDialogRequestKind.SetMark,
                        string.Empty,
                        selection with { ComboIndex = ResolveGuildMarkComboIndex(selection.Mark) });
                    return true;
                }

                if (subtype == CreateGuildAgreementRequest)
                {
                    int partyId = reader.ReadInt();
                    bool accepted = reader.ReadByte() != 0;
                    packet = new SocialListGuildDialogRequestPacket(
                        SocialListGuildDialogRequestKind.CreateGuildAgreement,
                        string.Empty,
                        null,
                        partyId,
                        accepted);
                    return true;
                }

                error = $"Guild request subtype {subtype} is not a configured guild dialog request.";
                return false;
            }
            catch (Exception ex)
            {
                error = $"Guild dialog request packet could not be decoded: {ex.Message}";
                return false;
            }
        }

        public static byte[] BuildPacket(
            SocialListGuildDialogRequestPacket request,
            ushort createGuildOpcode,
            ushort setGuildMarkOpcode)
        {
            PacketWriter writer = new PacketWriter();
            switch (request.Kind)
            {
                case SocialListGuildDialogRequestKind.CreateGuild:
                    if (createGuildOpcode == 0)
                    {
                        throw new InvalidOperationException("Create-guild opcode is not configured.");
                    }

                    writer.WriteShort((short)createGuildOpcode);
                    writer.WriteByte(CreateGuildRequest);
                    writer.WriteMapleString(NormalizeGuildName(request.GuildName));
                    return writer.ToArray();

                case SocialListGuildDialogRequestKind.SetMark:
                    if (setGuildMarkOpcode == 0)
                    {
                        throw new InvalidOperationException("Set-mark opcode is not configured.");
                    }

                    if (!request.MarkSelection.HasValue)
                    {
                        throw new InvalidOperationException("Set-mark request is missing the guild emblem selection.");
                    }

                    GuildMarkSelection selection = request.MarkSelection.Value;
                    writer.WriteShort((short)setGuildMarkOpcode);
                    writer.WriteByte(SetGuildMarkRequest);
                    writer.WriteShort((short)selection.MarkBackground);
                    writer.WriteByte((byte)selection.MarkBackgroundColor);
                    writer.WriteShort((short)selection.Mark);
                    writer.WriteByte((byte)selection.MarkColor);
                    return writer.ToArray();

                case SocialListGuildDialogRequestKind.CreateGuildAgreement:
                    if (createGuildOpcode == 0)
                    {
                        throw new InvalidOperationException("Create-guild agreement opcode is not configured.");
                    }

                    writer.WriteShort((short)createGuildOpcode);
                    writer.WriteByte(CreateGuildAgreementRequest);
                    writer.WriteInt(Math.Max(0, request.PartyId));
                    writer.WriteByte(request.Accepted ? (byte)1 : (byte)0);
                    return writer.ToArray();

                default:
                    throw new InvalidOperationException($"Unsupported guild dialog request kind {request.Kind}.");
            }
        }

        private static string NormalizeGuildName(string guildName)
        {
            return string.IsNullOrWhiteSpace(guildName) ? "New Guild" : guildName.Trim();
        }

        private static int ResolveGuildMarkComboIndex(int mark)
        {
            if (mark < 1000)
            {
                return 0;
            }

            return mark / 1000 switch
            {
                2 => 0,
                3 => 1,
                4 => 2,
                5 => 3,
                9 => 4,
                _ => 0
            };
        }
    }
}
