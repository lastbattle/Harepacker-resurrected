using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum ExpeditionIntermediaryPacketKind
    {
        Get = 0,
        Modified = 1,
        Invite = 2,
        ResponseInvite = 3,
        Notice = 4,
        MasterChanged = 5,
        Removed = 6,
        Ignored = 7
    }

    internal readonly record struct ExpeditionIntermediaryPacket(
        ExpeditionIntermediaryPacketKind Kind,
        int RetCode,
        string ExpeditionTitle,
        int MasterPartyIndex,
        int PartyIndex,
        IReadOnlyList<ExpeditionPartySeed> Parties,
        IReadOnlyList<ExpeditionMemberSeed> Members,
        string CharacterName,
        int ResponseCode,
        ExpeditionNoticeKind NoticeKind,
        ExpeditionRemovalKind RemovalKind,
        int Level,
        int JobCode,
        int PartyQuestId,
        string Detail);

    internal static class ExpeditionIntermediaryPacketCodec
    {
        private const int ExpeditionStructSize = 0x384;
        private const int PartyMemberStructSize = 0xB2;
        private const int ExpeditionPartyCount = 5;
        private const int PartyMemberCount = 6;

        private const int ExpeditionPartyQuestIdOffset = 0x00;
        private const int ExpeditionMasterPartyIndexOffset = 0x04;
        private const int ExpeditionPartiesOffset = 0x08;

        private const int PartyCharacterIdsOffset = 0x00;
        private const int PartyNamesOffset = 0x18;
        private const int PartyNameWidth = 13;
        private const int PartyJobsOffset = 0x66;
        private const int PartyLevelsOffset = 0x7E;
        private const int PartyChannelsOffset = 0x96;
        private const int PartyBossCharacterIdOffset = 0xAE;

        public static bool TryDecodeResultPayload(
            ReadOnlySpan<byte> payload,
            string localPlayerName,
            string defaultLocation,
            int defaultChannel,
            out ExpeditionIntermediaryPacket packet,
            out string error)
        {
            packet = default;
            error = null;

            if (payload.Length <= 0)
            {
                error = "Expedition payload is empty.";
                return false;
            }

            int offset = 0;
            int retCode = payload[offset++];
            try
            {
                switch (retCode)
                {
                    case 57:
                    case 59:
                    case 61:
                    {
                        if (!TryReadBytes(payload, ref offset, ExpeditionStructSize, out ReadOnlySpan<byte> expeditionBytes))
                        {
                            error = "Expedition get packet payload ended before the expedition structure was fully decoded.";
                            return false;
                        }

                        int partyQuestId = ReadInt32(expeditionBytes, ExpeditionPartyQuestIdOffset);
                        int masterPartyIndex = Math.Max(0, ReadInt32(expeditionBytes, ExpeditionMasterPartyIndexOffset));
                        IReadOnlyList<ExpeditionPartySeed> parties = ParseParties(
                            expeditionBytes,
                            localPlayerName,
                            defaultLocation,
                            defaultChannel);
                        string expeditionTitle = BuildExpeditionTitle(parties, partyQuestId);

                        packet = new ExpeditionIntermediaryPacket(
                            ExpeditionIntermediaryPacketKind.Get,
                            retCode,
                            expeditionTitle,
                            masterPartyIndex,
                            PartyIndex: 0,
                            Parties: parties,
                            Members: Array.Empty<ExpeditionMemberSeed>(),
                            CharacterName: null,
                            ResponseCode: 0,
                            NoticeKind: ExpeditionNoticeKind.Joined,
                            RemovalKind: ExpeditionRemovalKind.Disband,
                            Level: 0,
                            JobCode: 0,
                            PartyQuestId: partyQuestId,
                            Detail: $"Decoded expedition get retCode={retCode} with {parties.Count} non-empty party slot(s).");
                        return true;
                    }

                    case 70:
                    {
                        int masterPartyIndex = ReadInt32(payload, ref offset);
                        int partyIndex = ReadInt32(payload, ref offset);
                        if (!TryReadBytes(payload, ref offset, PartyMemberStructSize, out ReadOnlySpan<byte> partyMemberBytes))
                        {
                            error = "Expedition modified payload ended before the PARTYMEMBER structure was fully decoded.";
                            return false;
                        }

                        IReadOnlyList<ExpeditionMemberSeed> members = ParsePartyMembers(
                            partyMemberBytes,
                            localPlayerName,
                            defaultLocation,
                            defaultChannel);
                        packet = new ExpeditionIntermediaryPacket(
                            ExpeditionIntermediaryPacketKind.Modified,
                            retCode,
                            ExpeditionTitle: null,
                            MasterPartyIndex: masterPartyIndex,
                            PartyIndex: partyIndex,
                            Parties: Array.Empty<ExpeditionPartySeed>(),
                            Members: members,
                            CharacterName: null,
                            ResponseCode: 0,
                            NoticeKind: ExpeditionNoticeKind.Joined,
                            RemovalKind: ExpeditionRemovalKind.Disband,
                            Level: 0,
                            JobCode: 0,
                            PartyQuestId: 0,
                            Detail: $"Decoded expedition modified retCode={retCode} for party {partyIndex + 1} with {members.Count} member(s).");
                        return true;
                    }

                    case 72:
                    {
                        int level = ReadInt32(payload, ref offset);
                        int jobCode = ReadInt32(payload, ref offset);
                        string inviterName = ReadMapleString(payload, ref offset);
                        int partyQuestId = ReadInt32(payload, ref offset);
                        packet = new ExpeditionIntermediaryPacket(
                            ExpeditionIntermediaryPacketKind.Invite,
                            retCode,
                            ExpeditionTitle: null,
                            MasterPartyIndex: 0,
                            PartyIndex: 0,
                            Parties: Array.Empty<ExpeditionPartySeed>(),
                            Members: Array.Empty<ExpeditionMemberSeed>(),
                            CharacterName: NormalizeName(inviterName, "Expedition Leader"),
                            ResponseCode: 0,
                            NoticeKind: ExpeditionNoticeKind.Joined,
                            RemovalKind: ExpeditionRemovalKind.Disband,
                            Level: Math.Max(1, level),
                            JobCode: Math.Max(0, jobCode),
                            PartyQuestId: Math.Max(0, partyQuestId),
                            Detail: $"Decoded expedition invite retCode={retCode} from {NormalizeName(inviterName, "Expedition Leader")}.");
                        return true;
                    }

                    case 73:
                    {
                        int responseCode = ReadInt32(payload, ref offset);
                        string inviterName = ReadMapleString(payload, ref offset);
                        packet = new ExpeditionIntermediaryPacket(
                            ExpeditionIntermediaryPacketKind.ResponseInvite,
                            retCode,
                            ExpeditionTitle: null,
                            MasterPartyIndex: 0,
                            PartyIndex: 0,
                            Parties: Array.Empty<ExpeditionPartySeed>(),
                            Members: Array.Empty<ExpeditionMemberSeed>(),
                            CharacterName: NormalizeName(inviterName, "Expedition Leader"),
                            ResponseCode: responseCode,
                            NoticeKind: ExpeditionNoticeKind.Joined,
                            RemovalKind: ExpeditionRemovalKind.Disband,
                            Level: 0,
                            JobCode: 0,
                            PartyQuestId: 0,
                            Detail: $"Decoded expedition response retCode={retCode} with response={responseCode}.");
                        return true;
                    }

                    case 60:
                    case 64:
                    case 66:
                    {
                        string characterName = ReadMapleString(payload, ref offset);
                        ExpeditionNoticeKind noticeKind = retCode switch
                        {
                            64 => ExpeditionNoticeKind.Left,
                            66 => ExpeditionNoticeKind.Removed,
                            _ => ExpeditionNoticeKind.Joined
                        };

                        packet = new ExpeditionIntermediaryPacket(
                            ExpeditionIntermediaryPacketKind.Notice,
                            retCode,
                            ExpeditionTitle: null,
                            MasterPartyIndex: 0,
                            PartyIndex: 0,
                            Parties: Array.Empty<ExpeditionPartySeed>(),
                            Members: Array.Empty<ExpeditionMemberSeed>(),
                            CharacterName: NormalizeName(characterName, "Unknown member"),
                            ResponseCode: 0,
                            NoticeKind: noticeKind,
                            RemovalKind: ExpeditionRemovalKind.Disband,
                            Level: 0,
                            JobCode: 0,
                            PartyQuestId: 0,
                            Detail: $"Decoded expedition notice retCode={retCode} for {NormalizeName(characterName, "Unknown member")}.");
                        return true;
                    }

                    case 69:
                    {
                        int masterPartyIndex = ReadInt32(payload, ref offset);
                        packet = new ExpeditionIntermediaryPacket(
                            ExpeditionIntermediaryPacketKind.MasterChanged,
                            retCode,
                            ExpeditionTitle: null,
                            MasterPartyIndex: masterPartyIndex,
                            PartyIndex: 0,
                            Parties: Array.Empty<ExpeditionPartySeed>(),
                            Members: Array.Empty<ExpeditionMemberSeed>(),
                            CharacterName: null,
                            ResponseCode: 0,
                            NoticeKind: ExpeditionNoticeKind.Joined,
                            RemovalKind: ExpeditionRemovalKind.Disband,
                            Level: 0,
                            JobCode: 0,
                            PartyQuestId: 0,
                            Detail: $"Decoded expedition master-changed retCode={retCode} to party {masterPartyIndex + 1}.");
                        return true;
                    }

                    case 58:
                    case 65:
                    case 67:
                    case 68:
                    {
                        ExpeditionRemovalKind removalKind = retCode switch
                        {
                            68 => ExpeditionRemovalKind.Removed,
                            67 => ExpeditionRemovalKind.Disband,
                            _ => ExpeditionRemovalKind.Leave
                        };

                        packet = new ExpeditionIntermediaryPacket(
                            ExpeditionIntermediaryPacketKind.Removed,
                            retCode,
                            ExpeditionTitle: null,
                            MasterPartyIndex: 0,
                            PartyIndex: 0,
                            Parties: Array.Empty<ExpeditionPartySeed>(),
                            Members: Array.Empty<ExpeditionMemberSeed>(),
                            CharacterName: null,
                            ResponseCode: 0,
                            NoticeKind: ExpeditionNoticeKind.Joined,
                            RemovalKind: removalKind,
                            Level: 0,
                            JobCode: 0,
                            PartyQuestId: 0,
                            Detail: $"Decoded expedition removed retCode={retCode} ({removalKind}).");
                        return true;
                    }

                    case 62:
                    case 63:
                    case 71:
                        packet = new ExpeditionIntermediaryPacket(
                            ExpeditionIntermediaryPacketKind.Ignored,
                            retCode,
                            ExpeditionTitle: null,
                            MasterPartyIndex: 0,
                            PartyIndex: 0,
                            Parties: Array.Empty<ExpeditionPartySeed>(),
                            Members: Array.Empty<ExpeditionMemberSeed>(),
                            CharacterName: null,
                            ResponseCode: 0,
                            NoticeKind: ExpeditionNoticeKind.Joined,
                            RemovalKind: ExpeditionRemovalKind.Disband,
                            Level: 0,
                            JobCode: 0,
                            PartyQuestId: 0,
                            Detail: $"Expedition retCode={retCode} does not mutate intermediary roster state.");
                        return true;

                    default:
                        error = $"Unsupported expedition retCode {retCode}.";
                        return false;
                }
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static IReadOnlyList<ExpeditionPartySeed> ParseParties(
            ReadOnlySpan<byte> expeditionBytes,
            string localPlayerName,
            string defaultLocation,
            int defaultChannel)
        {
            List<ExpeditionPartySeed> parties = new();
            for (int partyIndex = 0; partyIndex < ExpeditionPartyCount; partyIndex++)
            {
                int partyOffset = ExpeditionPartiesOffset + (partyIndex * PartyMemberStructSize);
                ReadOnlySpan<byte> partyBytes = expeditionBytes.Slice(partyOffset, PartyMemberStructSize);
                IReadOnlyList<ExpeditionMemberSeed> members = ParsePartyMembers(
                    partyBytes,
                    localPlayerName,
                    defaultLocation,
                    defaultChannel);
                if (members.Count > 0)
                {
                    parties.Add(new ExpeditionPartySeed(partyIndex, members));
                }
            }

            return parties;
        }

        private static IReadOnlyList<ExpeditionMemberSeed> ParsePartyMembers(
            ReadOnlySpan<byte> partyBytes,
            string localPlayerName,
            string defaultLocation,
            int defaultChannel)
        {
            List<ExpeditionMemberSeed> members = new();
            int bossCharacterId = ReadInt32(partyBytes, PartyBossCharacterIdOffset);
            for (int slot = 0; slot < PartyMemberCount; slot++)
            {
                int characterId = ReadInt32(partyBytes, PartyCharacterIdsOffset + (slot * sizeof(int)));
                string name = ReadFixedString(partyBytes.Slice(PartyNamesOffset + (slot * PartyNameWidth), PartyNameWidth));
                if (characterId <= 0 || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                int job = ReadInt32(partyBytes, PartyJobsOffset + (slot * sizeof(int)));
                int level = ReadInt32(partyBytes, PartyLevelsOffset + (slot * sizeof(int)));
                int channel = ReadInt32(partyBytes, PartyChannelsOffset + (slot * sizeof(int)));
                bool isLocal = !string.IsNullOrWhiteSpace(localPlayerName)
                    && string.Equals(name, localPlayerName.Trim(), StringComparison.OrdinalIgnoreCase);
                bool isBoss = characterId == bossCharacterId;
                members.Add(new ExpeditionMemberSeed(
                    name,
                    isBoss ? "Master" : "Member",
                    Math.Max(1, level),
                    string.IsNullOrWhiteSpace(defaultLocation) ? "Unknown map" : defaultLocation.Trim(),
                    channel > 0 ? channel : Math.Max(1, defaultChannel),
                    channel > 0,
                    isLocal));
            }

            return members;
        }

        private static string BuildExpeditionTitle(IReadOnlyList<ExpeditionPartySeed> parties, int partyQuestId)
        {
            for (int i = 0; i < parties.Count; i++)
            {
                IReadOnlyList<ExpeditionMemberSeed> members = parties[i].Members;
                for (int j = 0; j < members.Count; j++)
                {
                    ExpeditionMemberSeed member = members[j];
                    if (string.Equals(member.RoleLabel, "Master", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(member.Name))
                    {
                        return $"{member.Name}'s Expedition";
                    }
                }
            }

            return partyQuestId > 0
                ? $"Expedition PQ {partyQuestId.ToString(CultureInfo.InvariantCulture)}"
                : "Expedition";
        }

        private static bool TryReadBytes(ReadOnlySpan<byte> source, ref int offset, int length, out ReadOnlySpan<byte> bytes)
        {
            bytes = default;
            if (length < 0 || offset < 0 || offset + length > source.Length)
            {
                return false;
            }

            bytes = source.Slice(offset, length);
            offset += length;
            return true;
        }

        private static int ReadInt32(ReadOnlySpan<byte> source, ref int offset)
        {
            if (offset < 0 || offset + sizeof(int) > source.Length)
            {
                throw new InvalidOperationException("Expedition payload ended before all Int32 fields were decoded.");
            }

            int value = source[offset]
                | (source[offset + 1] << 8)
                | (source[offset + 2] << 16)
                | (source[offset + 3] << 24);
            offset += sizeof(int);
            return value;
        }

        private static int ReadInt32(ReadOnlySpan<byte> source, int offset)
        {
            if (offset < 0 || offset + sizeof(int) > source.Length)
            {
                throw new InvalidOperationException("Expedition packet structure field read exceeded payload length.");
            }

            return source[offset]
                | (source[offset + 1] << 8)
                | (source[offset + 2] << 16)
                | (source[offset + 3] << 24);
        }

        private static string ReadMapleString(ReadOnlySpan<byte> source, ref int offset)
        {
            if (offset < 0 || offset + sizeof(ushort) > source.Length)
            {
                throw new InvalidOperationException("Expedition payload ended before maple-string length was decoded.");
            }

            int length = source[offset] | (source[offset + 1] << 8);
            offset += sizeof(ushort);
            if (length <= 0)
            {
                return string.Empty;
            }

            if (offset + length > source.Length)
            {
                throw new InvalidOperationException("Expedition payload ended before maple-string bytes were decoded.");
            }

            string value = Encoding.Default.GetString(source.Slice(offset, length));
            offset += length;
            return value;
        }

        private static string ReadFixedString(ReadOnlySpan<byte> fixedBytes)
        {
            int length = 0;
            while (length < fixedBytes.Length && fixedBytes[length] != 0)
            {
                length++;
            }

            return length <= 0 ? string.Empty : Encoding.Default.GetString(fixedBytes.Slice(0, length)).Trim();
        }

        private static string NormalizeName(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
