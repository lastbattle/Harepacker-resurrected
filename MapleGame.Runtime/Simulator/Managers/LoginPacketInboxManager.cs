using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginPacketInboxMessage
    {
        public LoginPacketInboxMessage(LoginPacketType packetType, string source, string rawText, string[] arguments)
        {
            PacketType = packetType;
            Source = string.IsNullOrWhiteSpace(source) ? "login-inbox" : source;
            RawText = rawText ?? string.Empty;
            Arguments = arguments ?? Array.Empty<string>();
        }

        public LoginPacketType PacketType { get; }
        public string Source { get; }
        public string RawText { get; }
        public string[] Arguments { get; }
    }

    /// <summary>
    /// Adapter packet inbox for driving the login runtime from role-session or local packet paths.
    /// Lines accept packet names or numeric ids as either
    /// "<packet> <args>", "<packet>:<args>", or "<packet>=<args>" and queues them for the
    /// simulator to drain on the main thread.
    /// </summary>
    public sealed class LoginPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18484;

        private readonly ConcurrentQueue<LoginPacketInboxMessage> _pendingMessages = new();
        private readonly RetiredMapleSocketState _socketState = new("Login packet inbox", DefaultPort, "Login packet inbox ready for role-session/local ingress.");

        public int Port => _socketState.Port;
        public bool IsRunning => _socketState.IsRunning;
        public int ReceivedCount { get; private set; }
        public int ProxyIngressReceivedCount { get; private set; }
        public int LocalIngressReceivedCount { get; private set; }
        public string LastIngressMode { get; private set; } = "none";
        public string LastStatus => _socketState.LastStatus;

        public void Start(int port = DefaultPort)
        {
            _socketState.Start(port);
        }

        public void Stop()
        {
            _socketState.Stop("Login packet inbox ready for role-session/local ingress.");
        }

        public void EnqueueProxy(LoginPacketType packetType, string source, params string[] arguments)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "login-proxy" : source;
            string[] normalizedArguments = arguments?
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .ToArray() ?? Array.Empty<string>();
            string rawText = normalizedArguments.Length == 0
                ? packetType.ToString()
                : $"{packetType} {string.Join(" ", normalizedArguments)}";
            EnqueueMessage(
                new LoginPacketInboxMessage(packetType, packetSource, rawText, normalizedArguments),
                MapSimulatorNetworkIngressMode.Proxy,
                packetSource);
        }

        public void EnqueueProxy(LoginPacketInboxMessage message)
        {
            EnqueueMessage(
                message,
                MapSimulatorNetworkIngressMode.Proxy,
                message?.Source);
        }

        public void EnqueueLocal(LoginPacketType packetType, string source, params string[] arguments)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "login-ui" : source;
            string[] normalizedArguments = arguments?
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .ToArray() ?? Array.Empty<string>();
            string rawText = normalizedArguments.Length == 0
                ? packetType.ToString()
                : $"{packetType} {string.Join(" ", normalizedArguments)}";
            EnqueueMessage(
                new LoginPacketInboxMessage(packetType, packetSource, rawText, normalizedArguments),
                MapSimulatorNetworkIngressMode.Local,
                packetSource);
        }

        public bool TryDequeue(out LoginPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void Dispose()
        {
        }

        internal static bool TryParsePacketLine(string text, out LoginPacketType packetType, out string[] arguments)
        {
            packetType = LoginPacketType.CheckPasswordResult;
            arguments = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed.StartsWith("/loginpacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/loginpacket".Length..].TrimStart();
                if (trimmed.Length == 0)
                {
                    return false;
                }
            }

            if (TryParseRelayPacketLine(trimmed, out packetType, out arguments))
            {
                return true;
            }

            string token;
            string argumentText = null;
            int separatorIndex = FindTokenSeparatorIndex(trimmed);
            if (separatorIndex >= 0)
            {
                token = trimmed[..separatorIndex].Trim();
                char separator = trimmed[separatorIndex];
                int argumentStart = separatorIndex + 1;
                if (separator != ':' && separator != '=')
                {
                    while (argumentStart < trimmed.Length && char.IsWhiteSpace(trimmed[argumentStart]))
                    {
                        argumentStart++;
                    }
                }

                argumentText = argumentStart < trimmed.Length
                    ? trimmed[argumentStart..].Trim()
                    : null;
            }
            else
            {
                token = trimmed;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(argumentText))
            {
                arguments = argumentText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            }

            if (LoginRuntimeManager.TryParsePacketType(token, out packetType))
            {
                return true;
            }

            if (TryResolvePacketToken(token, out packetType))
            {
                return true;
            }

            return TryParseOpcodeFramedPacketLine(trimmed, out packetType, out arguments);
        }

        private static bool TryParseRelayPacketLine(string text, out LoginPacketType packetType, out string[] arguments)
        {
            packetType = LoginPacketType.CheckPasswordResult;
            arguments = Array.Empty<string>();

            string[] tokens = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return false;
            }

            List<string> trailingArguments = new();
            string normalizedPayloadArgument = null;
            bool packetResolved = false;
            bool seenPacketToken = false;

            foreach (string rawToken in tokens)
            {
                string token = SanitizeRelayToken(rawToken);
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (TryParseRelayKeyValueToken(token, out string key, out string value))
                {
                    if (IsRelayMetadataKey(key))
                    {
                        continue;
                    }

                    if (!packetResolved && IsRelayPacketKey(key) && TryResolvePacketToken(value, out packetType))
                    {
                        packetResolved = true;
                        seenPacketToken = true;
                        continue;
                    }

                    if (IsRelayPayloadKey(key))
                    {
                        normalizedPayloadArgument = NormalizeRelayPayloadArgument(key, value);
                        continue;
                    }
                }

                if (!packetResolved && TryResolvePacketToken(token, out packetType))
                {
                    packetResolved = true;
                    seenPacketToken = true;
                    continue;
                }

                if (!seenPacketToken && IsRelayDirectionToken(token))
                {
                    continue;
                }

                trailingArguments.Add(token);
            }

            if (!packetResolved)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(normalizedPayloadArgument))
            {
                trailingArguments.Insert(0, normalizedPayloadArgument);
            }

            arguments = trailingArguments.ToArray();
            return true;
        }

        private static bool TryResolvePacketToken(string token, out LoginPacketType packetType)
        {
            packetType = LoginPacketType.CheckPasswordResult;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string candidate = SanitizeRelayToken(token);
            if (LoginRuntimeManager.TryParsePacketType(candidate, out packetType))
            {
                return true;
            }

            return int.TryParse(candidate, out int numericPacket) &&
                   Enum.IsDefined(typeof(LoginPacketType), numericPacket) &&
                   LoginRuntimeManager.TryParsePacketType(((LoginPacketType)numericPacket).ToString(), out packetType);
        }

        internal static bool TryDecodeOpcodeFramedPacket(
            byte[] packetBytes,
            out LoginPacketType packetType,
            out string[] arguments)
        {
            packetType = LoginPacketType.CheckPasswordResult;
            arguments = Array.Empty<string>();

            if (packetBytes == null || packetBytes.Length < sizeof(ushort))
            {
                return false;
            }

            ushort rawPacketType = BitConverter.ToUInt16(packetBytes, 0);
            if (!TryResolvePacketToken(rawPacketType.ToString(), out packetType))
            {
                return false;
            }

            byte[] payloadBytes = packetBytes.Length > sizeof(ushort)
                ? packetBytes[sizeof(ushort)..]
                : Array.Empty<byte>();

            arguments = new[] { $"payloadhex={Convert.ToHexString(payloadBytes)}" };
            return true;
        }

        private static bool TryParseOpcodeFramedPacketLine(string text, out LoginPacketType packetType, out string[] arguments)
        {
            packetType = LoginPacketType.CheckPasswordResult;
            arguments = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (TryDecodeBinaryPacketFrame(trimmed, out byte[] framedPacketBytes))
            {
                return TryDecodeOpcodeFramedPacket(framedPacketBytes, out packetType, out arguments);
            }

            string[] tokens = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return false;
            }

            LoginPacketType explicitPacketType = LoginPacketType.CheckPasswordResult;
            bool hasExplicitPacketType = false;
            byte[] payloadBytes = null;
            byte[] framedBytes = null;

            foreach (string rawToken in tokens)
            {
                string token = SanitizeRelayToken(rawToken);
                if (string.IsNullOrWhiteSpace(token) ||
                    !TryParseRelayKeyValueToken(token, out string key, out string value))
                {
                    continue;
                }

                if (!hasExplicitPacketType &&
                    IsRelayOpcodeKey(key) &&
                    TryResolvePacketToken(NormalizeNumericToken(value), out explicitPacketType))
                {
                    hasExplicitPacketType = true;
                    continue;
                }

                if (payloadBytes == null &&
                    IsRelayPayloadKey(key) &&
                    TryDecodeBinaryPacketValue(key, value, out byte[] decodedPayloadBytes))
                {
                    payloadBytes = decodedPayloadBytes;
                    continue;
                }

                if (framedBytes == null &&
                    IsRelayFramedPacketKey(key) &&
                    TryDecodeBinaryPacketValue(key, value, out byte[] decodedFramedBytes))
                {
                    framedBytes = decodedFramedBytes;
                }
            }

            if (framedBytes != null)
            {
                return TryDecodeOpcodeFramedPacket(framedBytes, out packetType, out arguments);
            }

            if (!hasExplicitPacketType || payloadBytes == null)
            {
                return false;
            }

            packetType = explicitPacketType;
            arguments = new[] { $"payloadhex={Convert.ToHexString(payloadBytes)}" };
            return true;
        }

        private static string SanitizeRelayToken(string token)
        {
            return token?.Trim().Trim('"', '\'', '[', ']', '(', ')', '{', '}', ',', ';');
        }

        private static bool TryParseRelayKeyValueToken(string token, out string key, out string value)
        {
            key = null;
            value = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            int separatorIndex = token.IndexOf('=');
            if (separatorIndex <= 0)
            {
                return false;
            }

            key = token[..separatorIndex].Trim();
            value = separatorIndex + 1 < token.Length ? token[(separatorIndex + 1)..].Trim() : string.Empty;
            return key.Length > 0;
        }

        private static bool IsRelayPacketKey(string key)
        {
            return key.Equals("packet", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("type", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("packettype", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("handler", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRelayOpcodeKey(string key)
        {
            return key.Equals("opcode", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("op", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("header", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("packetid", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("id", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRelayPayloadKey(string key)
        {
            return key.Equals("payload", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("payloadhex", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("payloadb64", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("hex", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("data", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("bytes", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("raw", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("b64", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("base64", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRelayFramedPacketKey(string key)
        {
            return key.Equals("frame", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("framehex", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("frameb64", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("packethex", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("packetb64", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("capturehex", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("captureb64", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("rawhex", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("rawb64", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRelayMetadataKey(string key)
        {
            return key.Equals("dir", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("direction", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("from", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("to", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("len", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("length", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("size", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("timestamp", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("time", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("source", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("target", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRelayDirectionToken(string token)
        {
            return token.Equals("recv", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("receive", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("received", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("incoming", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("inbound", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("server", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("serverbound", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("clientbound", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("s2c", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("s->c", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("s2clogin", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRelayPayloadArgument(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return $"{key}=";
            }

            if (key.Equals("payloadhex", StringComparison.OrdinalIgnoreCase))
            {
                return $"payloadhex={value}";
            }

            if (key.Equals("payloadb64", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("b64", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("base64", StringComparison.OrdinalIgnoreCase))
            {
                return $"payloadb64={value}";
            }

            return $"payload={value}";
        }

        private static int FindTokenSeparatorIndex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == ':' || text[i] == '=')
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryDecodeBinaryPacketFrame(string text, out byte[] packetBytes)
        {
            packetBytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (TryParseRelayKeyValueToken(trimmed, out string key, out string value) &&
                IsRelayFramedPacketKey(key))
            {
                return TryDecodeBinaryPacketValue(key, value, out packetBytes);
            }

            if (LooksLikeHexByteSequence(trimmed))
            {
                return TryDecodeHexString(trimmed, out packetBytes);
            }

            return TryDecodeBase64String(trimmed, out packetBytes);
        }

        private static bool TryDecodeBinaryPacketValue(string key, string value, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (key.EndsWith("b64", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("base64", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("b64", StringComparison.OrdinalIgnoreCase))
            {
                return TryDecodeBase64String(value, out bytes);
            }

            if (key.EndsWith("hex", StringComparison.OrdinalIgnoreCase))
            {
                return TryDecodeHexString(value, out bytes);
            }

            return LooksLikeHexByteSequence(value)
                ? TryDecodeHexString(value, out bytes)
                : TryDecodeBase64String(value, out bytes);
        }

        private static bool LooksLikeHexByteSequence(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = new(text
                .Where(ch => !char.IsWhiteSpace(ch) && ch != '-' && ch != ':')
                .ToArray());
            return normalized.Length >= 4 &&
                   (normalized.Length & 1) == 0 &&
                   normalized.All(Uri.IsHexDigit);
        }

        private static bool TryDecodeHexString(string text, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (!LooksLikeHexByteSequence(text))
            {
                return false;
            }

            string normalized = new(text
                .Where(ch => !char.IsWhiteSpace(ch) && ch != '-' && ch != ':')
                .ToArray());

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

        private static bool TryDecodeBase64String(string text, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                bytes = Convert.FromBase64String(text.Trim());
                return bytes.Length >= sizeof(ushort);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string NormalizeNumericToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber, null, out int hexValue))
            {
                return hexValue.ToString();
            }

            return trimmed;
        }

        private void EnqueueMessage(LoginPacketInboxMessage message, string ingressMode, string statusSource)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LastIngressMode = ingressMode;
            if (!MapSimulatorNetworkIngressMode.IsKnown(LastIngressMode))
            {
                LastIngressMode = MapSimulatorNetworkIngressMode.Proxy;
            }

            switch (LastIngressMode)
            {
                case MapSimulatorNetworkIngressMode.Proxy:
                    ProxyIngressReceivedCount++;
                    break;
                case MapSimulatorNetworkIngressMode.Local:
                    LocalIngressReceivedCount++;
                    break;
            }

            string source = string.IsNullOrWhiteSpace(statusSource)
                ? message.Source
                : statusSource;
            _socketState.SetStatus($"Queued {message.PacketType} from {source}.");
        }
    }
}
