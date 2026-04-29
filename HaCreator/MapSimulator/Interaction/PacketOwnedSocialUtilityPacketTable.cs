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
        internal const ushort MerchantInboundOpcode = 373;
        internal const ushort MerchantOutboundOpcode = 144;

        internal const ushort MapleTvInboundSetMessageOpcode = 405;
        internal const ushort MapleTvInboundClearMessageOpcode = 406;
        internal const ushort MapleTvInboundSendResultOpcode = 407;
        internal const ushort MapleTvOutboundConsumeCashItemOpcode = 85;
        internal const ushort FamilyLocalChartOpcode = 98;
        internal const ushort FamilyInfoOpcode = 99;
        internal const ushort FamilyResultOpcode = 100;
        internal const ushort FamilyPrivilegeListOpcode = 104;
        internal const ushort FamilySetPrivilegeOpcode = 107;
        internal const ushort FamilyUsePrivilegeRequestOpcode = 175;

        private static readonly ushort[] MessengerInboundOpcodeSet = { MessengerInboundOpcode };
        private static readonly ushort[] MessengerOutboundOpcodeSet =
        {
            MessengerOutboundOpcode,
            MessengerClaimRequestOpcode
        };
        private static readonly ushort[] MerchantInboundOpcodeSet = { MerchantInboundOpcode };
        private static readonly ushort[] MerchantOutboundOpcodeSet = { MerchantOutboundOpcode };
        private static readonly ushort[] MapleTvInboundOpcodeSet =
        {
            MapleTvInboundSetMessageOpcode,
            MapleTvInboundClearMessageOpcode,
            MapleTvInboundSendResultOpcode
        };
        private static readonly ushort[] MapleTvOutboundOpcodeSet = { MapleTvOutboundConsumeCashItemOpcode };
        private static readonly ushort[] FamilyInboundOpcodeSet =
        {
            FamilyLocalChartOpcode,
            FamilyInfoOpcode,
            FamilyResultOpcode,
            FamilyPrivilegeListOpcode,
            FamilySetPrivilegeOpcode
        };
        private static readonly ushort[] FamilyOutboundOpcodeSet = { FamilyUsePrivilegeRequestOpcode };

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

        private static readonly IReadOnlyDictionary<byte, string> MerchantInboundSubtypeHandlers =
            new Dictionary<byte, string>
            {
                [24] = "CPersonalShopDlg::OnPacket buy-result",
                [25] = "CPersonalShopDlg::OnPacket -> CMiniRoomBaseDlg::OnPacketBase",
                [26] = "CPersonalShopDlg::OnSoldItemResult",
                [27] = "CPersonalShopDlg::OnMoveItemToInventoryResult",
                [40] = "CEntrustedShopDlg::OnArrangeItemResult",
                [42] = "CEntrustedShopDlg::OnPacket withdraw-all result",
                [44] = "CEntrustedShopDlg::OnPacket withdraw-money result",
                [46] = "CEntrustedShopDlg::OnPacket visit-list result",
                [47] = "CEntrustedShopDlg::OnPacket blacklist result"
            };

        private static readonly IReadOnlyDictionary<byte, string> MerchantOutboundSubtypeHandlers =
            new Dictionary<byte, string>
            {
                [10] = "CPersonalShopDlg::SetRet(nRet=2) / close",
                [11] = "CPersonalShopDlg::SetRet(nRet=1) / open setup",
                [15] = "CPersonalShopDlg::PutItem(list item)",
                [23] = "CPersonalShopDlg::BuyItem(personal shop)",
                [34] = "CPersonalShopDlg::BuyItem(entrusted shop visitor path)",
                [39] = "CEntrustedShopDlg::OnGoOut",
                [40] = "CEntrustedShopDlg::OnArrange",
                [41] = "CEntrustedShopDlg::SetRet(nRet=8) / withdraw-all",
                [43] = "CEntrustedShopDlg::OnWithdrawMoney",
                [46] = "CEntrustedShopDlg::OnVisitList",
                [47] = "CEntrustedShopDlg::OnBlackList"
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

        internal static IReadOnlyDictionary<byte, string> GetRecoveredMerchantInboundSubtypeHandlers()
        {
            return MerchantInboundSubtypeHandlers;
        }

        internal static IReadOnlyDictionary<byte, string> GetRecoveredMerchantOutboundSubtypeHandlers()
        {
            return MerchantOutboundSubtypeHandlers;
        }

        internal static bool IsRecoveredMerchantInboundSubtype(byte subtype)
        {
            return MerchantInboundSubtypeHandlers.ContainsKey(subtype);
        }

        internal static IReadOnlyList<ushort> GetRecoveredInboundOpcodes(string ownerName)
        {
            if (string.Equals(ownerName, "Merchant", StringComparison.OrdinalIgnoreCase))
            {
                return MerchantInboundOpcodeSet;
            }

            if (string.Equals(ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
            {
                return MapleTvInboundOpcodeSet;
            }

            if (string.Equals(ownerName, "Family", StringComparison.OrdinalIgnoreCase))
            {
                return FamilyInboundOpcodeSet;
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
            if (string.Equals(ownerName, "Merchant", StringComparison.OrdinalIgnoreCase))
            {
                return MerchantOutboundOpcodeSet;
            }

            if (string.Equals(ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
            {
                return MapleTvOutboundOpcodeSet;
            }

            if (string.Equals(ownerName, "Family", StringComparison.OrdinalIgnoreCase))
            {
                return FamilyOutboundOpcodeSet;
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
            if (string.Equals(ownerName, "Merchant", StringComparison.OrdinalIgnoreCase))
            {
                return DescribeMerchantRecoveredPacketTable();
            }

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
            return $"Recovered MapleTV packet table: inbound opcodes {inboundSet} to CMapleTVMan::OnPacket (405: OnSetMessage, 406: OnClearMessage, 407: OnSendMessageResult); outbound opcode {MapleTvOutboundConsumeCashItemOpcode} via CUserLocal::ConsumeCashItem expects the authoritative set/result response family 405/407.";
        }

        internal static string DescribeMerchantRecoveredPacketTable()
        {
            string inboundSet = string.Join("/", MerchantInboundOpcodeSet.Select(opcode => opcode.ToString(CultureInfo.InvariantCulture)));
            string inboundSubtypes = string.Join(
                ", ",
                MerchantInboundSubtypeHandlers.Select(entry => $"{entry.Key}: {entry.Value}"));
            string outboundSubtypes = string.Join(
                ", ",
                MerchantOutboundSubtypeHandlers.Select(entry => $"{entry.Key}: {entry.Value}"));
            return $"Recovered merchant packet table: inbound opcode {inboundSet} to CPersonalShopDlg::OnPacket/CEntrustedShopDlg::OnPacket (subtypes {inboundSubtypes}); outbound opcode {MerchantOutboundOpcode} (subtypes {outboundSubtypes}).";
        }

        internal static bool TryBuildRecoveredResultExpectation(
            string ownerName,
            int requestOpcode,
            ReadOnlySpan<byte> payload,
            out int[] expectedInboundOpcodes,
            out byte[] expectedInboundSubtypes,
            out string expectationSummary)
        {
            expectedInboundOpcodes = Array.Empty<int>();
            expectedInboundSubtypes = Array.Empty<byte>();
            expectationSummary = string.Empty;

            if (string.Equals(ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
            {
                if (requestOpcode != MapleTvOutboundConsumeCashItemOpcode)
                {
                    return false;
                }

                expectedInboundOpcodes = new[] { (int)MapleTvInboundSetMessageOpcode, (int)MapleTvInboundSendResultOpcode };
                expectationSummary = "expect CMapleTVMan::OnSetMessage or OnSendMessageResult (opcodes 405/407)";
                return true;
            }

            if (string.Equals(ownerName, "Family", StringComparison.OrdinalIgnoreCase))
            {
                if (requestOpcode != FamilyUsePrivilegeRequestOpcode)
                {
                    return false;
                }

                expectedInboundOpcodes = new[] { (int)FamilyResultOpcode, (int)FamilySetPrivilegeOpcode };
                expectationSummary = "expect CWvsContext::OnFamilyResult or OnFamilySetPrivilege (opcodes 100/107)";
                return true;
            }

            if (requestOpcode == MerchantOutboundOpcode && payload.Length > 0)
            {
                byte merchantRequestSubtype = payload[0];
                switch (merchantRequestSubtype)
                {
                    case 10:
                        expectedInboundOpcodes = new[] { (int)MerchantInboundOpcode };
                        expectedInboundSubtypes = new byte[] { 25 };
                        expectationSummary = "expect CPersonalShopDlg::OnPacket -> CMiniRoomBaseDlg::OnPacketBase close/leave update (subtype 25)";
                        return true;
                    case 11:
                        expectedInboundOpcodes = new[] { (int)MerchantInboundOpcode };
                        expectedInboundSubtypes = new byte[] { 25 };
                        expectationSummary = "expect CPersonalShopDlg::SetRet(nRet=1) setup request to be followed by a MiniRoom base enter/result family (subtype 25)";
                        return true;
                    case 15:
                        expectedInboundOpcodes = new[] { (int)MerchantInboundOpcode };
                        expectedInboundSubtypes = new byte[] { 25 };
                        expectationSummary = "expect CPersonalShopDlg::PutItem list request to be followed by a MiniRoom base update family (subtype 25)";
                        return true;
                    case 23:
                    case 34:
                        expectedInboundOpcodes = new[] { (int)MerchantInboundOpcode };
                        expectedInboundSubtypes = new byte[] { 24, 26 };
                        expectationSummary = "expect CPersonalShopDlg::OnPacket buy/sold result (subtypes 24/26)";
                        return true;
                    case 39:
                        expectedInboundOpcodes = new[] { (int)MerchantInboundOpcode };
                        expectedInboundSubtypes = new byte[] { 25 };
                        expectationSummary = "expect CEntrustedShopDlg::OnGoOut to be followed by a MiniRoom base leave/update (subtype 25)";
                        return true;
                    case 40:
                        expectedInboundOpcodes = new[] { (int)MerchantInboundOpcode };
                        expectedInboundSubtypes = new byte[] { 40 };
                        expectationSummary = "expect CEntrustedShopDlg::OnArrangeItemResult (subtype 40)";
                        return true;
                    case 41:
                        expectedInboundOpcodes = new[] { (int)MerchantInboundOpcode };
                        expectedInboundSubtypes = new byte[] { 42 };
                        expectationSummary = "expect CEntrustedShopDlg::OnPacket withdraw-all result (subtype 42)";
                        return true;
                    case 43:
                        expectedInboundOpcodes = new[] { (int)MerchantInboundOpcode };
                        expectedInboundSubtypes = new byte[] { 44 };
                        expectationSummary = "expect CEntrustedShopDlg::OnPacket withdraw-money result (subtype 44)";
                        return true;
                    case 46:
                        expectedInboundOpcodes = new[] { (int)MerchantInboundOpcode };
                        expectedInboundSubtypes = new byte[] { 46 };
                        expectationSummary = "expect CEntrustedShopDlg::OnPacket visit-list result (subtype 46)";
                        return true;
                    case 47:
                        expectedInboundOpcodes = new[] { (int)MerchantInboundOpcode };
                        expectedInboundSubtypes = new byte[] { 47 };
                        expectationSummary = "expect CEntrustedShopDlg::OnPacket blacklist result (subtype 47)";
                        return true;
                    default:
                        return false;
                }
            }

            if (requestOpcode != MessengerOutboundOpcode || payload.Length == 0)
            {
                return false;
            }

            byte requestSubtype = payload[0];
            switch (requestSubtype)
            {
                case 0:
                    expectedInboundOpcodes = new[] { (int)MessengerInboundOpcode };
                    expectedInboundSubtypes = new byte[] { 1, 0, 7 };
                    expectationSummary = "expect CUIMessenger::OnSelfEnterResult/OnEnter/OnAvatar (subtypes 1/0/7)";
                    return true;
                case 2:
                    expectedInboundOpcodes = new[] { (int)MessengerInboundOpcode };
                    expectedInboundSubtypes = new byte[] { 2 };
                    expectationSummary = "expect CUIMessenger::OnLeave (subtype 2)";
                    return true;
                case 3:
                    expectedInboundOpcodes = new[] { (int)MessengerInboundOpcode };
                    expectedInboundSubtypes = new byte[] { 4 };
                    expectationSummary = "expect CUIMessenger::OnInviteResult (subtype 4)";
                    return true;
                case 5:
                    expectedInboundOpcodes = new[] { (int)MessengerInboundOpcode };
                    expectedInboundSubtypes = new byte[] { 5 };
                    expectationSummary = "expect CUIMessenger::OnBlocked (subtype 5)";
                    return true;
                case 6:
                    expectedInboundOpcodes = new[] { (int)MessengerInboundOpcode };
                    expectedInboundSubtypes = new byte[] { 6 };
                    expectationSummary = "expect CUIMessenger::OnChat (subtype 6)";
                    return true;
                default:
                    return false;
            }
        }

        internal static bool IsRecoveredTerminalResultOpcode(string ownerName, int requestOpcode, int inboundOpcode)
        {
            return string.Equals(ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase)
                && requestOpcode == MapleTvOutboundConsumeCashItemOpcode
                && inboundOpcode == MapleTvInboundSendResultOpcode;
        }

        internal static bool TryDecodeRecoveredInboundBranch(
            string ownerName,
            int inboundOpcode,
            ReadOnlySpan<byte> payload,
            out byte inboundSubtype,
            out byte resultCode,
            out string branchSummary)
        {
            inboundSubtype = byte.MaxValue;
            resultCode = byte.MaxValue;
            branchSummary = string.Empty;

            if (string.Equals(ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
            {
                switch (inboundOpcode)
                {
                    case MapleTvInboundSetMessageOpcode:
                        branchSummary = "CMapleTVMan::OnSetMessage";
                        return true;
                    case MapleTvInboundClearMessageOpcode:
                        branchSummary = "CMapleTVMan::OnClearMessage";
                        return true;
                    case MapleTvInboundSendResultOpcode:
                        if (payload.Length >= 2)
                        {
                            bool showFeedback = payload[0] != 0;
                            resultCode = payload[1];
                            branchSummary = showFeedback
                                ? $"CMapleTVMan::OnSendMessageResult code={resultCode}"
                                : "CMapleTVMan::OnSendMessageResult without feedback";
                        }
                        else
                        {
                            branchSummary = "CMapleTVMan::OnSendMessageResult";
                        }

                        return true;
                    default:
                        return false;
                }
            }

            if (inboundOpcode == MerchantInboundOpcode && payload.Length > 0)
            {
                inboundSubtype = payload[0];
                if (MerchantInboundSubtypeHandlers.TryGetValue(inboundSubtype, out string merchantHandlerName))
                {
                    branchSummary = $"{merchantHandlerName} (subtype {inboundSubtype})";
                    if (inboundSubtype == 24 && payload.Length > 1)
                    {
                        resultCode = payload[1];
                    }
                    else if (inboundSubtype == 42 && payload.Length > 1)
                    {
                        resultCode = payload[1];
                    }

                    return true;
                }

                branchSummary = $"CPersonalShopDlg::OnPacket/CEntrustedShopDlg::OnPacket subtype {inboundSubtype}";
                return true;
            }

            if (inboundOpcode != MessengerInboundOpcode || payload.Length == 0)
            {
                return false;
            }

            inboundSubtype = payload[0];
            if (MessengerInboundSubtypeHandlers.TryGetValue(inboundSubtype, out string handlerName))
            {
                branchSummary = $"{handlerName} (subtype {inboundSubtype})";
                return true;
            }

            branchSummary = $"CUIMessenger::OnPacket subtype {inboundSubtype}";
            return true;
        }
    }
}
