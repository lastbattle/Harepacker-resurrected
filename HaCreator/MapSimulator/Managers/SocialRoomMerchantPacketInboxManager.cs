using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class SocialRoomMerchantPacketInboxMessage
    {
        public SocialRoomMerchantPacketInboxMessage(SocialRoomKind kind, byte[] payload, string source, string rawText)
        {
            Kind = kind;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "socialroom-merchant-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public SocialRoomKind Kind { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Adapter inbox for merchant-room dialog payloads owned by
    /// CPersonalShopDlg::OnPacket and CEntrustedShopDlg::OnPacket.
    /// </summary>
    public sealed class SocialRoomMerchantPacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<SocialRoomMerchantPacketInboxMessage> _pendingMessages = new();
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Merchant-room packet inbox ready for role-session/local ingress.";

        public bool TryDequeue(out SocialRoomMerchantPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueLocal(SocialRoomMerchantPacketInboxMessage message)
        {
            EnqueueMessage(message);
        }

        public void RecordDispatchResult(SocialRoomMerchantPacketInboxMessage message, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "merchant-room payload" : detail;
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "merchant-room-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "merchant-room-inbox"}";
        }

        public void Dispose()
        {
        }

        public static bool TryParsePacketLine(
            string text,
            SocialRoomKind preferredKind,
            out SocialRoomKind kind,
            out byte[] payload,
            out string error)
        {
            kind = preferredKind;
            payload = Array.Empty<byte>();
            error = null;

            if (!IsMerchantKind(preferredKind))
            {
                error = "Merchant-room inbox requires a personal-shop or entrusted-shop owner.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Merchant-room inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            string[] tokens = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                error = "Merchant-room inbox line is empty.";
                return false;
            }

            int index = 0;
            if (tokens[0].Equals("/socialroom", StringComparison.OrdinalIgnoreCase))
            {
                index++;
            }

            if (tokens.Length > index && TryParseMerchantKind(tokens[index], out SocialRoomKind explicitKind))
            {
                kind = explicitKind;
                index++;
            }

            if (kind != preferredKind)
            {
                error = $"Merchant-room inbox is armed for {DescribeKind(preferredKind)}, but the line targeted {DescribeKind(kind)}.";
                return false;
            }

            if (tokens.Length > index && tokens[index].Equals("packet", StringComparison.OrdinalIgnoreCase))
            {
                index++;
            }

            if (tokens.Length <= index)
            {
                error = "Merchant-room inbox line is missing packet data.";
                return false;
            }

            string head = tokens[index];
            if (head.Equals("packetclientraw", StringComparison.OrdinalIgnoreCase) || head.Equals("clientraw", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (tokens.Length <= index)
                {
                    error = "Merchant-room packetclientraw lines require opcode-framed hex bytes.";
                    return false;
                }

                return TryParseHexPayload(string.Join(string.Empty, tokens, index, tokens.Length - index), out payload, out error);
            }

            if (head.Equals("packetraw", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (tokens.Length <= index)
                {
                    error = "Merchant-room packetraw lines require hex bytes.";
                    return false;
                }

                return TryParseHexPayload(string.Join(string.Empty, tokens, index, tokens.Length - index), out payload, out error);
            }

            if (head.Equals("packetrecv", StringComparison.OrdinalIgnoreCase) || head.Equals("recv", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (tokens.Length <= index || !TryParseOpcode(tokens[index], out ushort opcode))
                {
                    error = "Merchant-room recv lines require '<opcode> <hex>' or 'recv <opcode> <hex>'.";
                    return false;
                }

                index++;
                if (tokens.Length <= index)
                {
                    error = "Merchant-room recv lines require a payload after the opcode.";
                    return false;
                }

                if (!TryParseHexPayload(string.Join(string.Empty, tokens, index, tokens.Length - index), out byte[] recvPayload, out error))
                {
                    return false;
                }

                payload = BuildOpcodeWrappedPacket(opcode, recvPayload);
                return true;
            }

            if (TryParseOpcode(head, out ushort inferredOpcode))
            {
                index++;
                if (tokens.Length <= index)
                {
                    error = "Merchant-room recv lines require a payload after the opcode.";
                    return false;
                }

                if (!TryParseHexPayload(string.Join(string.Empty, tokens, index, tokens.Length - index), out byte[] recvPayload, out error))
                {
                    return false;
                }

                payload = BuildOpcodeWrappedPacket(inferredOpcode, recvPayload);
                return true;
            }

            return TryParseHexPayload(string.Join(string.Empty, tokens, index, tokens.Length - index), out payload, out error);
        }

        private static bool IsMerchantKind(SocialRoomKind kind)
        {
            return kind == SocialRoomKind.PersonalShop || kind == SocialRoomKind.EntrustedShop;
        }

        private static bool TryParseMerchantKind(string text, out SocialRoomKind kind)
        {
            kind = text?.Trim().ToLowerInvariant() switch
            {
                "personalshop" or "pshop" or "shop" => SocialRoomKind.PersonalShop,
                "entrustedshop" or "eshop" or "membershop" => SocialRoomKind.EntrustedShop,
                _ => (SocialRoomKind)(-1)
            };

            return IsMerchantKind(kind);
        }

        private static string DescribeKind(SocialRoomKind kind)
        {
            return kind switch
            {
                SocialRoomKind.PersonalShop => "personal shop",
                SocialRoomKind.EntrustedShop => "entrusted shop",
                _ => kind.ToString()
            };
        }

        private static bool TryParseOpcode(string text, out ushort opcode)
        {
            opcode = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.Trim().TrimEnd(':');
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[2..];
            }

            return ushort.TryParse(normalized, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out opcode)
                || ushort.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out opcode);
        }

        private static bool TryParseHexPayload(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            string compactHex = RemoveWhitespace(text);
            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            if (string.IsNullOrWhiteSpace(compactHex))
            {
                error = "Merchant-room payload requires hex bytes.";
                return false;
            }

            try
            {
                payload = Convert.FromHexString(compactHex);
                error = null;
                return true;
            }
            catch (FormatException)
            {
                error = $"Invalid merchant-room hex payload: {text}";
                return false;
            }
        }

        private static string RemoveWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            char[] buffer = new char[text.Length];
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (!char.IsWhiteSpace(ch))
                {
                    buffer[count++] = ch;
                }
            }

            return count == 0 ? string.Empty : new string(buffer, 0, count);
        }

        private static byte[] BuildOpcodeWrappedPacket(ushort opcode, byte[] payload)
        {
            byte[] packet = new byte[(payload?.Length ?? 0) + sizeof(ushort)];
            packet[0] = (byte)(opcode & 0xFF);
            packet[1] = (byte)((opcode >> 8) & 0xFF);
            if (payload != null && payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, packet, sizeof(ushort), payload.Length);
            }

            return packet;
        }

        private void EnqueueMessage(SocialRoomMerchantPacketInboxMessage message)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LastStatus = $"Queued {DescribeKind(message.Kind)} payload from {message.Source}.";
        }
    }
}
