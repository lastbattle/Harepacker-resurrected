using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Expands a client-style login packet script into the existing packet inbox message shape.
    /// Each non-empty line is parsed through the normal login packet transport parser so the
    /// simulator reuses the same packet-owned seams for single packets and packet streams.
    /// </summary>
    public static class LoginPacketScriptCodec
    {
        public static bool TryDecode(string scriptText, string source, out IReadOnlyList<LoginPacketInboxMessage> messages, out string error)
        {
            messages = Array.Empty<LoginPacketInboxMessage>();
            error = null;

            if (string.IsNullOrWhiteSpace(scriptText))
            {
                error = "Login packet script is empty.";
                return false;
            }

            List<LoginPacketInboxMessage> parsedMessages = new();
            string normalizedSource = string.IsNullOrWhiteSpace(source) ? "login-script" : source;
            string[] lines = NormalizeScriptText(scriptText)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith('#') ||
                    line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!LoginPacketInboxManager.TryParsePacketLine(line, out LoginPacketType packetType, out string[] arguments))
                {
                    error = $"Login packet script line {lineIndex + 1} could not be parsed: {line}";
                    return false;
                }

                parsedMessages.Add(new LoginPacketInboxMessage(packetType, normalizedSource, line, arguments));
            }

            messages = parsedMessages;
            return true;
        }

        public static bool TryDecodeArguments(string[] args, string source, out IReadOnlyList<LoginPacketInboxMessage> messages, out string error)
        {
            messages = Array.Empty<LoginPacketInboxMessage>();
            error = null;

            if (args == null || args.Length == 0)
            {
                error = "Usage: /loginpacket stream <line1 | line2 | ... | payloadhex=<utf8-or-framed-hex> | payloadb64=<utf8-or-framed-base64>>";
                return false;
            }

            if (TryDecodePayloadArgument(args, out string payloadText, out byte[] payloadBytes, out error))
            {
                if (!string.IsNullOrWhiteSpace(payloadText) &&
                    TryDecode(payloadText, source, out messages, out error))
                {
                    return true;
                }

                if (payloadBytes?.Length >= sizeof(ushort) &&
                    LoginPacketInboxManager.TryDecodeOpcodeFramedPacket(
                        payloadBytes,
                        out LoginPacketType packetType,
                        out string[] packetArguments))
                {
                    messages = new[]
                    {
                        new LoginPacketInboxMessage(
                            packetType,
                            string.IsNullOrWhiteSpace(source) ? "login-script" : source,
                            Convert.ToHexString(payloadBytes),
                            packetArguments)
                    };
                    error = null;
                    return true;
                }

                error ??= "The login packet payload was neither a valid script transcript nor a supported opcode-framed packet capture.";
                return false;
            }

            if (error != null)
            {
                return false;
            }

            string inlineScript = string.Join(" ", args);
            return TryDecode(inlineScript, source, out messages, out error);
        }

        private static string NormalizeScriptText(string scriptText)
        {
            if (string.IsNullOrWhiteSpace(scriptText))
            {
                return string.Empty;
            }

            string normalized = scriptText.Trim();
            normalized = normalized.Replace("\\r\\n", "\n", StringComparison.Ordinal);
            normalized = normalized.Replace("\\n", "\n", StringComparison.Ordinal);
            normalized = normalized.Replace("\\r", "\n", StringComparison.Ordinal);
            normalized = normalized.Replace('|', '\n');
            return normalized;
        }

        private static bool TryDecodePayloadArgument(string[] args, out string scriptText, out byte[] payloadBytes, out string error)
        {
            scriptText = null;
            payloadBytes = Array.Empty<byte>();
            error = null;
            if (args == null || args.Length != 1)
            {
                return false;
            }

            string arg = args[0]?.Trim() ?? string.Empty;
            if (arg.Length == 0)
            {
                return false;
            }

            if (TryExtractPayloadValue(arg, "payloadhex=", out string hexText) ||
                TryExtractPayloadValue(arg, "scripthex=", out hexText) ||
                TryExtractPayloadValue(arg, "hex=", out hexText))
            {
                if (!TryDecodeHexString(hexText, out byte[] bytes))
                {
                    error = "Login packet script hex payload is invalid.";
                    return false;
                }

                payloadBytes = bytes;
                scriptText = Encoding.UTF8.GetString(bytes);
                return true;
            }

            if (TryExtractPayloadValue(arg, "payloadb64=", out string base64Text) ||
                TryExtractPayloadValue(arg, "scriptb64=", out base64Text) ||
                TryExtractPayloadValue(arg, "base64=", out base64Text))
            {
                try
                {
                    payloadBytes = Convert.FromBase64String(base64Text);
                    scriptText = Encoding.UTF8.GetString(payloadBytes);
                    return true;
                }
                catch (FormatException)
                {
                    error = "Login packet script Base64 payload is invalid.";
                    return false;
                }
            }

            return false;
        }

        private static bool TryExtractPayloadValue(string text, string prefix, out string value)
        {
            value = null;
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            value = text[prefix.Length..].Trim();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryDecodeHexString(string text, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = new(text
                .Where(ch => !char.IsWhiteSpace(ch) && ch != '-')
                .ToArray());
            if ((normalized.Length & 1) != 0 || normalized.Any(ch => !Uri.IsHexDigit(ch)))
            {
                return false;
            }

            try
            {
                bytes = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
