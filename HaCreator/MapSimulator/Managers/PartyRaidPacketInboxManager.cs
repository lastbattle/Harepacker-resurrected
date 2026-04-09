using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.Managers
{
    public enum PartyRaidPacketScope
    {
        Field,
        Party,
        Session,
        Clock,
        Packet
    }

    public sealed class PartyRaidPacketInboxMessage
    {
        public PartyRaidPacketInboxMessage(PartyRaidPacketScope scope, string key, string value, string source, string rawText)
        {
            Scope = scope;
            Key = key ?? string.Empty;
            Value = value ?? string.Empty;
            Source = string.IsNullOrWhiteSpace(source) ? "partyraid-inbox" : source;
            RawText = rawText ?? string.Empty;
            Payload = Array.Empty<byte>();
        }

        public PartyRaidPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            Scope = PartyRaidPacketScope.Packet;
            Key = string.Empty;
            Value = string.Empty;
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "partyraid-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public PartyRaidPacketScope Scope { get; }
        public string Key { get; }
        public string Value { get; }
        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Optional loopback inbox for Party Raid runtime updates.
    /// Each line is encoded as "<scope> <key> <value>", where scope is
    /// "field", "party", "session", "clock", or packet-oriented aliases such as
    /// "packet 169 <payloadHex>" or "packetclientraw <opcodeFramedHex>".
    /// </summary>
    public sealed class PartyRaidPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18486;

        private readonly ConcurrentQueue<PartyRaidPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Party Raid packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Party Raid packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal(clearPending: true);

                try
                {
                    Port = port <= 0 ? DefaultPort : port;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, Port);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Party Raid packet inbox listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Party Raid packet inbox failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
                LastStatus = "Party Raid packet inbox stopped.";
            }
        }

        public void EnqueueLocal(PartyRaidPacketScope scope, string key, string value, string source)
        {
            _pendingMessages.Enqueue(new PartyRaidPacketInboxMessage(scope, key, value, source, $"{scope} {key} {value}".Trim()));
        }

        public void EnqueuePacket(int packetType, byte[] payload, string source)
        {
            _pendingMessages.Enqueue(new PartyRaidPacketInboxMessage(packetType, payload, source, $"packet {packetType}"));
        }

        public bool TryDequeue(out PartyRaidPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, PartyRaidPacketScope scope, string key, bool success, string message)
        {
            string target = DescribeScope(scope, key);
            string summary = string.IsNullOrWhiteSpace(message) ? target : $"{target}: {message}";
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
            }
        }

        public static bool TryParsePacketLine(string text, out PartyRaidPacketScope scope, out string key, out string value, out string error)
        {
            scope = PartyRaidPacketScope.Field;
            key = string.Empty;
            value = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Party Raid inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (TryParsePacketLine(trimmed, out int packetType, out byte[] packetPayload, out error))
            {
                scope = PartyRaidPacketScope.Packet;
                key = packetType.ToString();
                value = Convert.ToHexString(packetPayload);
                return true;
            }

            if (TryParsePipeDelimitedPacketLine(trimmed, out scope, out key, out value))
            {
                return true;
            }

            string[] parts = trimmed.Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                error = "Party Raid inbox line must be '<scope> <key> <value>' or '<scope>|<key>|<value>'.";
                return false;
            }

            if (!TryParseScope(parts[0], out scope))
            {
                error = $"Unsupported Party Raid scope: {parts[0]}";
                return false;
            }

            key = parts[1];
            value = parts.Length >= 3 ? parts[2] : string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Party Raid inbox key is empty.";
                return false;
            }

            if (scope == PartyRaidPacketScope.Clock
                && string.IsNullOrWhiteSpace(value)
                && !string.Equals(key, "clear", StringComparison.OrdinalIgnoreCase))
            {
                error = "Party Raid clock inbox lines require '<scope> <seconds|clear> [value]'.";
                return false;
            }

            return true;
        }

        public static bool TryParsePacketLine(string text, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Party Raid packet line is empty.";
                return false;
            }

            string[] parts = text.Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                error = "Party Raid packet line is empty.";
                return false;
            }

            string action = parts[0].Trim().ToLowerInvariant();
            if (action is "packetclientraw" or "packetraw" or "wrapped" or "opcode")
            {
                if (parts.Length < 2)
                {
                    error = "Party Raid packetclientraw requires an opcode-framed hex payload.";
                    return false;
                }

                if (!TryParseHexPayload(parts.Length == 2 ? parts[1] : $"{parts[1]}{parts[2]}", out byte[] rawPacket))
                {
                    error = "Party Raid packetclientraw payload must be valid hex.";
                    return false;
                }

                if (!PacketFieldIngressRouter.TryDecodeClientOpcodePacket(rawPacket, out packetType, out payload, out error))
                {
                    return false;
                }

                return true;
            }

            if (action != "packet")
            {
                error = $"Unsupported Party Raid packet action: {parts[0]}";
                return false;
            }

            if (parts.Length < 2 || !int.TryParse(parts[1], out packetType) || !PacketFieldIngressRouter.IsSupportedFieldScopedPacketType(packetType))
            {
                error = "Party Raid packet lines must be 'packet <149|162|166|167|169|174|178> [payloadhex]'.";
                return false;
            }

            if (packetType == 166)
            {
                payload = Array.Empty<byte>();
                return true;
            }

            if (parts.Length < 3 || !TryParseHexPayload(parts[2], out payload))
            {
                error = $"Party Raid packet {packetType} requires a valid hex payload.";
                return false;
            }

            return true;
        }

        private static bool TryParsePipeDelimitedPacketLine(string text, out PartyRaidPacketScope scope, out string key, out string value)
        {
            scope = PartyRaidPacketScope.Field;
            key = string.Empty;
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] parts = text.Split('|', 3, StringSplitOptions.None);
            if (parts.Length < 2 || !TryParseScope(parts[0], out scope))
            {
                return false;
            }

            key = parts[1]?.Trim() ?? string.Empty;
            value = parts.Length >= 3 ? parts[2].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (scope == PartyRaidPacketScope.Clock && parts.Length == 2)
            {
                value = key;
            }

            return !(scope == PartyRaidPacketScope.Clock
                && string.IsNullOrWhiteSpace(value)
                && !string.Equals(key, "clear", StringComparison.OrdinalIgnoreCase));
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"Party Raid packet inbox error: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            string remoteEndpoint = client.Client?.RemoteEndPoint?.ToString() ?? "loopback-client";
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line == null)
                        {
                            break;
                        }

                        if (!TryParsePacketLine(line, out PartyRaidPacketScope scope, out string key, out string value, out string error))
                        {
                            LastStatus = $"Ignored Party Raid inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        if (scope == PartyRaidPacketScope.Packet
                            && int.TryParse(key, out int packetType)
                            && TryParseHexPayload(value, out byte[] payload))
                        {
                            _pendingMessages.Enqueue(new PartyRaidPacketInboxMessage(packetType, payload, remoteEndpoint, line));
                            ReceivedCount++;
                            LastStatus = $"Queued {DescribeScope(PartyRaidPacketScope.Packet, key)} from {remoteEndpoint}.";
                            continue;
                        }

                        _pendingMessages.Enqueue(new PartyRaidPacketInboxMessage(scope, key, value, remoteEndpoint, line));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribeScope(scope, key)} from {remoteEndpoint}.";
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"Party Raid packet inbox client error: {ex.Message}";
            }
        }

        private static bool TryParseScope(string token, out PartyRaidPacketScope scope)
        {
            scope = PartyRaidPacketScope.Field;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return token.Trim().ToLowerInvariant() switch
            {
                "field" => AssignScope(PartyRaidPacketScope.Field, out scope),
                "party" => AssignScope(PartyRaidPacketScope.Party, out scope),
                "session" or "result" => AssignScope(PartyRaidPacketScope.Session, out scope),
                "clock" or "timer" => AssignScope(PartyRaidPacketScope.Clock, out scope),
                "packet" => AssignScope(PartyRaidPacketScope.Packet, out scope),
                _ => false
            };
        }

        private static bool AssignScope(PartyRaidPacketScope value, out PartyRaidPacketScope scope)
        {
            scope = value;
            return true;
        }

        private static string DescribeScope(PartyRaidPacketScope scope, string key)
        {
            string suffix = string.IsNullOrWhiteSpace(key) ? string.Empty : $" {key}";
            return scope switch
            {
                PartyRaidPacketScope.Field => $"field{suffix}",
                PartyRaidPacketScope.Party => $"party{suffix}",
                PartyRaidPacketScope.Session => $"session{suffix}",
                PartyRaidPacketScope.Clock => $"clock{suffix}",
                PartyRaidPacketScope.Packet => $"packet{suffix}",
                _ => $"partyraid{suffix}"
            };
        }

        private static bool TryParseHexPayload(string text, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = RemoveWhitespace(text);
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[2..];
            }

            if ((normalized.Length & 1) != 0)
            {
                return false;
            }

            try
            {
                payload = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string RemoveWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            char[] buffer = new char[value.Length];
            int count = 0;
            foreach (char c in value)
            {
                if (!char.IsWhiteSpace(c))
                {
                    buffer[count++] = c;
                }
            }

            return new string(buffer, 0, count);
        }

        private void StopInternal(bool clearPending)
        {
            try
            {
                _listenerCancellation?.Cancel();
            }
            catch
            {
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
            _listener = null;
            _listenerTask = null;

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }
            }
        }
    }
}
