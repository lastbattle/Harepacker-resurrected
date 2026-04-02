using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HaCreator.MapSimulator.Managers
{
    internal sealed class CashShopStorageExpansionPacketResult
    {
        public int CommoditySerialNumber { get; init; }
        public int ResultSubtype { get; init; }
        public int FailureReason { get; init; }
        public long NxPrice { get; init; }
        public int SlotLimitAfterResult { get; init; }
        public bool ConsumeCash { get; init; } = true;
        public string Message { get; init; } = string.Empty;
    }

    internal static class CashShopStorageExpansionPacketCodec
    {
        private static readonly Regex MessagePattern = new(@"(?:^|\s)message=(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool TryDecodePayload(byte[] payload, out CashShopStorageExpansionPacketResult result)
        {
            result = null;
            if (payload == null || payload.Length < sizeof(byte))
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);

                int resultSubtype = reader.ReadByte();
                if (resultSubtype <= 0)
                {
                    return false;
                }

                int commoditySerialNumber = stream.Position <= stream.Length - sizeof(int)
                    ? reader.ReadInt32()
                    : 0;
                int failureReason = stream.Position <= stream.Length - sizeof(int)
                    ? reader.ReadInt32()
                    : 0;
                long nxPrice = stream.Position <= stream.Length - sizeof(int)
                    ? Math.Max(0, reader.ReadInt32())
                    : 0L;
                int slotLimitAfterResult = stream.Position <= stream.Length - sizeof(int)
                    ? Math.Max(0, reader.ReadInt32())
                    : 0;
                bool consumeCash = stream.Position < stream.Length
                    ? reader.ReadByte() != 0
                    : true;

                string message = string.Empty;
                if (stream.Position <= stream.Length - sizeof(ushort))
                {
                    ushort messageLength = reader.ReadUInt16();
                    if (messageLength > 0 && stream.Position <= stream.Length - messageLength)
                    {
                        message = Encoding.UTF8.GetString(reader.ReadBytes(messageLength)).Trim();
                    }
                }

                result = new CashShopStorageExpansionPacketResult
                {
                    CommoditySerialNumber = Math.Max(0, commoditySerialNumber),
                    ResultSubtype = resultSubtype,
                    FailureReason = Math.Max(0, failureReason),
                    NxPrice = nxPrice,
                    SlotLimitAfterResult = slotLimitAfterResult,
                    ConsumeCash = consumeCash,
                    Message = message ?? string.Empty
                };
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        public static byte[] EncodePayload(CashShopStorageExpansionPacketResult result)
        {
            if (result == null)
            {
                return Array.Empty<byte>();
            }

            string message = result.Message?.Trim() ?? string.Empty;
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            if (messageBytes.Length > ushort.MaxValue)
            {
                Array.Resize(ref messageBytes, ushort.MaxValue);
            }

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write((byte)Math.Clamp(result.ResultSubtype, 0, byte.MaxValue));
            writer.Write(Math.Max(0, result.CommoditySerialNumber));
            writer.Write(Math.Max(0, result.FailureReason));
            writer.Write((int)Math.Clamp(result.NxPrice, 0L, int.MaxValue));
            writer.Write(Math.Max(0, result.SlotLimitAfterResult));
            writer.Write(result.ConsumeCash ? (byte)1 : (byte)0);
            writer.Write((ushort)messageBytes.Length);
            if (messageBytes.Length > 0)
            {
                writer.Write(messageBytes);
            }

            writer.Flush();
            return stream.ToArray();
        }

        public static bool TryBuildPayloadFromText(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Structured storage-expansion payload is empty.";
                return false;
            }

            string trimmed = text.Trim();
            string message = string.Empty;
            Match messageMatch = MessagePattern.Match(trimmed);
            if (messageMatch.Success)
            {
                message = TrimQuotedValue(messageMatch.Groups[1].Value);
                trimmed = trimmed[..messageMatch.Index].TrimEnd();
            }

            string[] tokens = trimmed
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0)
            {
                error = "Structured storage-expansion payload is empty.";
                return false;
            }

            int resultSubtype = 0;
            int commoditySerialNumber = 0;
            int failureReason = 0;
            long nxPrice = 0L;
            int slotLimitAfterResult = 0;
            bool consumeCash = true;

            foreach (string token in tokens)
            {
                if (token.Equals("storageexpansion", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("storage", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("trunkexpansion", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("trunk", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryParseSubtypeToken(token, out int parsedSubtype))
                {
                    resultSubtype = parsedSubtype;
                    continue;
                }

                int separatorIndex = token.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
                {
                    error = $"Unsupported storage-expansion token '{token}'.";
                    return false;
                }

                string key = token[..separatorIndex].Trim();
                string value = TrimQuotedValue(token[(separatorIndex + 1)..]);
                if (key.Equals("subtype", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseSubtypeToken(value, out parsedSubtype))
                    {
                        error = $"Unsupported storage-expansion subtype '{value}'.";
                        return false;
                    }

                    resultSubtype = parsedSubtype;
                    continue;
                }

                if (key.Equals("sn", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("commodity", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("commoditysn", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out commoditySerialNumber))
                    {
                        error = $"Commodity serial '{value}' is not a valid integer.";
                        return false;
                    }

                    commoditySerialNumber = Math.Max(0, commoditySerialNumber);
                    continue;
                }

                if (key.Equals("reason", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("failure", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("failurereason", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out failureReason))
                    {
                        error = $"Failure reason '{value}' is not a valid integer.";
                        return false;
                    }

                    failureReason = Math.Max(0, failureReason);
                    continue;
                }

                if (key.Equals("price", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("nx", StringComparison.OrdinalIgnoreCase))
                {
                    if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out nxPrice))
                    {
                        error = $"NX price '{value}' is not a valid integer.";
                        return false;
                    }

                    nxPrice = Math.Max(0L, nxPrice);
                    continue;
                }

                if (key.Equals("slotlimit", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("slots", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("slotcap", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out slotLimitAfterResult))
                    {
                        error = $"Slot limit '{value}' is not a valid integer.";
                        return false;
                    }

                    slotLimitAfterResult = Math.Max(0, slotLimitAfterResult);
                    continue;
                }

                if (key.Equals("consumecash", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("charge", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("bill", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseBooleanToken(value, out consumeCash))
                    {
                        error = $"Cash-charge flag '{value}' is not a valid boolean token.";
                        return false;
                    }

                    continue;
                }

                error = $"Unsupported storage-expansion field '{key}'.";
                return false;
            }

            if (resultSubtype <= 0)
            {
                error = "Structured storage-expansion payload must specify success or rejected subtype.";
                return false;
            }

            payload = EncodePayload(new CashShopStorageExpansionPacketResult
            {
                CommoditySerialNumber = commoditySerialNumber,
                ResultSubtype = resultSubtype,
                FailureReason = failureReason,
                NxPrice = nxPrice,
                SlotLimitAfterResult = slotLimitAfterResult,
                ConsumeCash = consumeCash,
                Message = message
            });
            return true;
        }

        public static string BuildSummary(CashShopStorageExpansionPacketResult result)
        {
            if (result == null)
            {
                return "Storage-expansion packet data is unavailable.";
            }

            string commodityLabel = result.CommoditySerialNumber > 0
                ? $"SN {result.CommoditySerialNumber.ToString(CultureInfo.InvariantCulture)}"
                : "local seam";
            string outcome = result.ResultSubtype == 1 ? "accepted" : "rejected";
            string priceText = result.NxPrice > 0
                ? $"{result.NxPrice.ToString("N0", CultureInfo.InvariantCulture)} NX"
                : "0 NX";
            string slotText = result.SlotLimitAfterResult > 0
                ? $", slot limit {result.SlotLimitAfterResult.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
            string failureText = result.FailureReason > 0
                ? $", reason {result.FailureReason.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
            string billedText = result.ConsumeCash ? ", cash billed on success" : ", cash already settled";
            string messageText = string.IsNullOrWhiteSpace(result.Message) ? string.Empty : $" {result.Message}";
            return $"Storage-expansion result {outcome} via {commodityLabel} at {priceText}{slotText}{failureText}{billedText}.{messageText}".Trim();
        }

        private static bool TryParseSubtypeToken(string token, out int subtype)
        {
            subtype = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim().ToLowerInvariant();
            if (normalized is "success" or "ok" or "accepted" or "expand" or "expanded" or "1")
            {
                subtype = 1;
                return true;
            }

            if (normalized is "rejected" or "reject" or "failed" or "failure" or "deny" or "denied" or "2")
            {
                subtype = 2;
                return true;
            }

            return false;
        }

        private static bool TryParseBooleanToken(string token, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim().ToLowerInvariant();
            if (normalized is "1" or "true" or "yes" or "y" or "on")
            {
                value = true;
                return true;
            }

            if (normalized is "0" or "false" or "no" or "n" or "off")
            {
                value = false;
                return true;
            }

            return false;
        }

        private static string TrimQuotedValue(string value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length >= 2
                && ((trimmed[0] == '"' && trimmed[^1] == '"')
                    || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
            {
                return trimmed[1..^1];
            }

            return trimmed;
        }
    }
}
