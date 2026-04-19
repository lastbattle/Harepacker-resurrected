using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketOwnedSocialUtilityPacketTable
    {
        internal const ushort MessengerInboundOpcode = 372;
        internal const ushort MessengerOutboundOpcode = 143;
        internal const ushort MessengerClaimRequestOpcode = 118;

        internal const ushort MapleTvInboundSetMessageOpcode = 405;
        internal const ushort MapleTvInboundClearMessageOpcode = 406;
        internal const ushort MapleTvInboundSendResultOpcode = 407;
        internal const ushort MapleTvOutboundConsumeCashItemOpcode = 85;

        private static readonly ushort[] MessengerInboundOpcodeSet = { MessengerInboundOpcode };
        private static readonly ushort[] MessengerOutboundOpcodeSet =
        {
            MessengerOutboundOpcode,
            MessengerClaimRequestOpcode
        };
        private static readonly ushort[] MapleTvInboundOpcodeSet =
        {
            MapleTvInboundSetMessageOpcode,
            MapleTvInboundClearMessageOpcode,
            MapleTvInboundSendResultOpcode
        };
        private static readonly ushort[] MapleTvOutboundOpcodeSet = { MapleTvOutboundConsumeCashItemOpcode };

        private static readonly IReadOnlyDictionary<byte, string> MessengerInboundSubtypeHandlers =
            new Dictionary<byte, string>
            {
                [0] = "CUIMessenger::OnEnter",
                [1] = "CUIMessenger::OnSelfEnterResult",
                [2] = "CUIMessenger::OnLeave",
                [3] = "CUIMessenger::OnInvite",
                [4] = "CUIMessenger::OnInviteResult",
                [5] = "CUIMessenger::OnBlocked",
                [6] = "CUIMessenger::OnChat",
                [7] = "CUIMessenger::OnAvatar",
                [8] = "CUIMessenger::OnMigrated"
            };

        private static readonly IReadOnlyDictionary<byte, string> MessengerOutboundSubtypeHandlers =
            new Dictionary<byte, string>
            {
                [0] = "CUIMessenger::TryNew (accept)",
                [2] = "CUIMessenger::OnDestroy (leave)",
                [3] = "CUIMessenger::SendInviteMsg",
                [5] = "CUIMessenger::OnInvite blacklist auto-reject",
                [6] = "CUIMessenger::ProcessChat"
            };

        internal static IReadOnlyDictionary<byte, string> GetRecoveredMessengerInboundSubtypeHandlers()
        {
            return MessengerInboundSubtypeHandlers;
        }

        internal static IReadOnlyDictionary<byte, string> GetRecoveredMessengerOutboundSubtypeHandlers()
        {
            return MessengerOutboundSubtypeHandlers;
        }

        internal static IReadOnlyList<byte> GetRecoveredMessengerInboundSubtypes()
        {
            return MessengerInboundSubtypeHandlers.Keys.OrderBy(key => key).ToArray();
        }

        internal static IReadOnlyList<byte> GetRecoveredMessengerOutboundSubtypes()
        {
            return MessengerOutboundSubtypeHandlers.Keys.OrderBy(key => key).ToArray();
        }

        internal static IReadOnlyList<ushort> GetRecoveredInboundOpcodes(string ownerName)
        {
            if (string.Equals(ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
            {
                return MapleTvInboundOpcodeSet;
            }

            return MessengerInboundOpcodeSet;
        }

        internal static ushort ResolveRecoveredInboundOpcode(string ownerName, ushort requestedOpcode)
        {
            IReadOnlyList<ushort> recovered = GetRecoveredInboundOpcodes(ownerName);
            if (requestedOpcode != 0 && recovered.Contains(requestedOpcode))
            {
                return requestedOpcode;
            }

            return recovered.Count > 0 ? recovered[0] : requestedOpcode;
        }

        internal static IReadOnlyList<ushort> GetRecoveredOutboundOpcodes(string ownerName)
        {
            if (string.Equals(ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
            {
                return MapleTvOutboundOpcodeSet;
            }

            return MessengerOutboundOpcodeSet;
        }

        internal static bool IsRecoveredInboundOpcode(string ownerName, ushort opcode)
        {
            return opcode != 0 && GetRecoveredInboundOpcodes(ownerName).Contains(opcode);
        }

        internal static bool TryResolveRecoveredInboundOpcodeToken(string ownerName, string token, out ushort inboundOpcode)
        {
            inboundOpcode = ResolveRecoveredInboundOpcode(ownerName, 0);
            if (string.IsNullOrWhiteSpace(token))
            {
                return true;
            }

            string normalizedToken = token.Trim();
            if (string.Equals(normalizedToken, "table", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedToken, "default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedToken, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!ushort.TryParse(normalizedToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort parsedOpcode))
            {
                return false;
            }

            if (!IsRecoveredInboundOpcode(ownerName, parsedOpcode))
            {
                return false;
            }

            inboundOpcode = parsedOpcode;
            return true;
        }

        internal static string DescribeRecoveredInboundOpcodeSet(string ownerName)
        {
            IReadOnlyList<ushort> recovered = GetRecoveredInboundOpcodes(ownerName);
            if (recovered.Count == 0)
            {
                return $"No recovered {ownerName} inbound opcodes are registered.";
            }

            return $"Recovered {ownerName} inbound opcodes are {string.Join("/", recovered.Select(opcode => opcode.ToString(CultureInfo.InvariantCulture)))}.";
        }

        internal static string DescribeRecoveredPacketTable(string ownerName)
        {
            if (string.Equals(ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
            {
                return DescribeMapleTvRecoveredPacketTable();
            }

            return DescribeMessengerRecoveredPacketTable();
        }

        internal static string DescribeMessengerRecoveredPacketTable()
        {
            string inboundSet = string.Join("/", MessengerInboundOpcodeSet.Select(opcode => opcode.ToString(CultureInfo.InvariantCulture)));
            string inboundSubtypes = string.Join(
                ", ",
                MessengerInboundSubtypeHandlers.Select(entry => $"{entry.Key}: {entry.Value}"));
            string outboundSubtypes = string.Join(
                ", ",
                MessengerOutboundSubtypeHandlers.Select(entry => $"{entry.Key}: {entry.Value}"));
            return $"Recovered Messenger packet table: inbound opcode {inboundSet} to CUIMessenger::OnPacket (subtypes {inboundSubtypes}); outbound opcode {MessengerOutboundOpcode} (subtypes {outboundSubtypes}); claim-request opcode {MessengerClaimRequestOpcode} via CWvsContext::SendClaimRequest.";
        }

        internal static string DescribeMapleTvRecoveredPacketTable()
        {
            string inboundSet = string.Join("/", MapleTvInboundOpcodeSet.Select(opcode => opcode.ToString(CultureInfo.InvariantCulture)));
            return $"Recovered MapleTV packet table: inbound opcodes {inboundSet} to CMapleTVMan::OnPacket (405: OnSetMessage, 406: OnClearMessage, 407: OnSendMessageResult); outbound opcode {MapleTvOutboundConsumeCashItemOpcode} via CUserLocal::ConsumeCashItem.";
        }
    }
}
