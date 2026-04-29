using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class TradingRoomPacketTable
    {
        internal const ushort TradingRoomInboundOpcode = 373;
        internal const ushort TradingRoomOutboundOpcode = 144;

        private static readonly ushort[] TradingRoomInboundOpcodeSet = { TradingRoomInboundOpcode };
        private static readonly ushort[] TradingRoomOutboundOpcodeSet = { TradingRoomOutboundOpcode };

        private static readonly IReadOnlyDictionary<byte, string> TradingRoomInboundSubtypeHandlers =
            new Dictionary<byte, string>
            {
                [15] = "CTradingRoomDlg::OnPutItem",
                [16] = "CTradingRoomDlg::OnPutMoney",
                [17] = "CTradingRoomDlg::OnTrade (handoff)",
                [20] = "CTradingRoomDlg::OnTrade CRC follow-up",
                [21] = "CTradingRoomDlg::OnExceedLimit"
            };

        private static readonly IReadOnlyDictionary<byte, string> TradingRoomOutboundSubtypeHandlers =
            new Dictionary<byte, string>
            {
                [15] = "CTradingRoomDlg::OnPutItem request",
                [16] = "CTradingRoomDlg::OnPutMoney request",
                [17] = "CTradingRoomDlg::OnTrade request",
                [20] = "CTradingRoomDlg::OnTrade CRC follow-up"
            };

        internal static IReadOnlyList<ushort> GetRecoveredInboundOpcodes()
        {
            return TradingRoomInboundOpcodeSet;
        }

        internal static IReadOnlyList<ushort> GetRecoveredOutboundOpcodes()
        {
            return TradingRoomOutboundOpcodeSet;
        }

        internal static ushort ResolveRecoveredInboundOpcode(ushort requestedOpcode)
        {
            if (requestedOpcode != 0 && TradingRoomInboundOpcodeSet.Contains(requestedOpcode))
            {
                return requestedOpcode;
            }

            return TradingRoomInboundOpcodeSet.Length > 0 ? TradingRoomInboundOpcodeSet[0] : requestedOpcode;
        }

        internal static bool IsRecoveredInboundOpcode(ushort opcode)
        {
            return opcode != 0 && TradingRoomInboundOpcodeSet.Contains(opcode);
        }

        internal static bool IsRecoveredInboundSubtype(byte subtype)
        {
            return TradingRoomInboundSubtypeHandlers.ContainsKey(subtype);
        }

        internal static bool IsRecoveredOutboundSubtype(byte subtype)
        {
            return TradingRoomOutboundSubtypeHandlers.ContainsKey(subtype);
        }

        internal static bool TryResolveRecoveredInboundOpcodeToken(string token, out ushort inboundOpcode)
        {
            inboundOpcode = ResolveRecoveredInboundOpcode(0);
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

            if (!IsRecoveredInboundOpcode(parsedOpcode))
            {
                return false;
            }

            inboundOpcode = parsedOpcode;
            return true;
        }

        internal static string DescribeRecoveredInboundOpcodeSet()
        {
            return $"Recovered TradingRoom inbound opcodes are {string.Join("/", TradingRoomInboundOpcodeSet.Select(opcode => opcode.ToString(CultureInfo.InvariantCulture)))}.";
        }

        internal static string DescribeRecoveredPacketTable()
        {
            string inboundSet = string.Join("/", TradingRoomInboundOpcodeSet.Select(opcode => opcode.ToString(CultureInfo.InvariantCulture)));
            string outboundSet = string.Join("/", TradingRoomOutboundOpcodeSet.Select(opcode => opcode.ToString(CultureInfo.InvariantCulture)));
            string inboundSubtypes = string.Join(
                ", ",
                TradingRoomInboundSubtypeHandlers.Select(entry => $"{entry.Key}: {entry.Value}"));
            string outboundSubtypes = string.Join(
                ", ",
                TradingRoomOutboundSubtypeHandlers.Select(entry => $"{entry.Key}: {entry.Value}"));
            return $"Recovered TradingRoom packet table: inbound opcode {inboundSet} to CTradingRoomDlg::OnPacket ({inboundSubtypes}); outbound opcode {outboundSet} ({outboundSubtypes}).";
        }

        internal static bool TryBuildRecoveredResultExpectation(
            int requestOpcode,
            ReadOnlySpan<byte> payload,
            out int[] expectedInboundOpcodes,
            out byte[] expectedInboundSubtypes,
            out string expectationSummary)
        {
            expectedInboundOpcodes = Array.Empty<int>();
            expectedInboundSubtypes = Array.Empty<byte>();
            expectationSummary = string.Empty;

            if (requestOpcode != TradingRoomOutboundOpcode || payload.Length == 0)
            {
                return false;
            }

            byte requestSubtype = payload[0];
            switch (requestSubtype)
            {
                case 15:
                    expectedInboundOpcodes = new[] { (int)TradingRoomInboundOpcode };
                    expectedInboundSubtypes = new byte[] { 15, 21 };
                    expectationSummary = "expect CTradingRoomDlg::OnPutItem echo or OnExceedLimit failure (subtypes 15/21)";
                    return true;
                case 16:
                    expectedInboundOpcodes = new[] { (int)TradingRoomInboundOpcode };
                    expectedInboundSubtypes = new byte[] { 16, 21 };
                    expectationSummary = "expect CTradingRoomDlg::OnPutMoney echo or OnExceedLimit failure (subtypes 16/21)";
                    return true;
                case 17:
                    expectedInboundOpcodes = new[] { (int)TradingRoomInboundOpcode };
                    expectedInboundSubtypes = new byte[] { 17, 20, 21 };
                    expectationSummary = "expect CTradingRoomDlg::OnTrade handoff/checksum branch or OnExceedLimit failure (subtypes 17/20/21)";
                    return true;
                case 20:
                    expectedInboundOpcodes = new[] { (int)TradingRoomInboundOpcode };
                    expectedInboundSubtypes = new byte[] { 20, 21 };
                    expectationSummary = "expect CTradingRoomDlg::OnTrade checksum follow-up or OnExceedLimit failure (subtypes 20/21)";
                    return true;
                default:
                    return false;
            }
        }

        internal static bool TryDecodeRecoveredInboundBranch(
            int inboundOpcode,
            ReadOnlySpan<byte> payload,
            out byte inboundSubtype,
            out string branchSummary)
        {
            inboundSubtype = byte.MaxValue;
            branchSummary = string.Empty;
            if (inboundOpcode != TradingRoomInboundOpcode || payload.Length == 0)
            {
                return false;
            }

            inboundSubtype = payload[0];
            if (TradingRoomInboundSubtypeHandlers.TryGetValue(inboundSubtype, out string handlerName))
            {
                branchSummary = $"{handlerName} (subtype {inboundSubtype.ToString(CultureInfo.InvariantCulture)})";
                return true;
            }

            branchSummary = $"CTradingRoomDlg::OnPacket subtype {inboundSubtype.ToString(CultureInfo.InvariantCulture)}";
            return true;
        }
    }
}
